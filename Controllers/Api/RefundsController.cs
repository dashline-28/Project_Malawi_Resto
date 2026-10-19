using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using Project_Malawi_Resto.Models;

namespace Project_Malawi_Resto.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Administrateur")]
public sealed class RefundsController : ControllerBase
{
    private readonly ILogger<RefundsController> _logger;

    public RefundsController(ILogger<RefundsController> logger)
    {
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = DatabaseHelper.GetConnection();
            await connection.OpenAsync(cancellationToken);
            await using var command = new MySqlCommand(
                """
                SELECT Id_Remboursement, Id_Paiement, Montant, Devise,
                       Reference_Externe, Date_Remboursement, Id_Administrateur
                FROM Remboursement
                ORDER BY Date_Remboursement DESC
                LIMIT 500
                """,
                connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var refunds = new List<object>();
            while (await reader.ReadAsync(cancellationToken))
            {
                refunds.Add(new
                {
                    id = Convert.ToUInt32(reader["Id_Remboursement"]),
                    idPaiement = Convert.ToUInt32(reader["Id_Paiement"]),
                    montant = reader.GetDecimal(reader.GetOrdinal("Montant")),
                    devise = reader.GetString(reader.GetOrdinal("Devise")).Trim(),
                    referenceExterne = reader.GetString(reader.GetOrdinal("Reference_Externe")),
                    date = reader.GetDateTime(reader.GetOrdinal("Date_Remboursement")),
                    idAdministrateur = Convert.ToUInt32(reader["Id_Administrateur"])
                });
            }

            return Ok(refunds);
        }
        catch (MySqlException exception)
        {
            _logger.LogError(exception, "Unable to list refunds.");
            return DatabaseUnavailable();
        }
    }

    [HttpPost]
    public async Task<IActionResult> Record(RecordRefundRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = DatabaseHelper.GetConnection();
            await connection.OpenAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            decimal paymentAmount;
            string currency;
            string paymentStatus;

            await using (var paymentCommand = new MySqlCommand(
                """
                SELECT Montant, Devise, Statut
                FROM Paiement
                WHERE Id_Paiement = @id
                FOR UPDATE
                """,
                connection,
                (MySqlTransaction)transaction))
            {
                paymentCommand.Parameters.AddWithValue("@id", request.IdPaiement);
                await using var reader = await paymentCommand.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                {
                    return NotFound(new ProblemDetails { Title = "Paiement introuvable." });
                }

                paymentAmount = reader.GetDecimal(reader.GetOrdinal("Montant"));
                currency = reader.GetString(reader.GetOrdinal("Devise"));
                paymentStatus = reader.GetString(reader.GetOrdinal("Statut"));
            }

            if (paymentStatus is not ("Réussi" or "Remboursé"))
            {
                return Conflict(new ProblemDetails { Title = "Seul un paiement réussi peut être remboursé." });
            }

            decimal refundedAmount;
            await using (var sumCommand = new MySqlCommand(
                "SELECT COALESCE(SUM(Montant), 0) FROM Remboursement WHERE Id_Paiement = @id",
                connection,
                (MySqlTransaction)transaction))
            {
                sumCommand.Parameters.AddWithValue("@id", request.IdPaiement);
                refundedAmount = Convert.ToDecimal(await sumCommand.ExecuteScalarAsync(cancellationToken));
            }

            var remaining = paymentAmount - refundedAmount;
            if (request.Montant > remaining)
            {
                return Conflict(new ProblemDetails
                {
                    Title = $"Le montant demandé dépasse le solde remboursable ({remaining:0.00} {currency.Trim()})."
                });
            }

            uint refundId;
            await using (var insert = new MySqlCommand(
                """
                INSERT INTO Remboursement
                    (Id_Paiement, Montant, Devise, Reference_Externe, Id_Administrateur)
                VALUES (@paymentId, @amount, @currency, @reference, @adminId)
                """,
                connection,
                (MySqlTransaction)transaction))
            {
                insert.Parameters.AddWithValue("@paymentId", request.IdPaiement);
                insert.Parameters.AddWithValue("@amount", request.Montant);
                insert.Parameters.AddWithValue("@currency", currency);
                insert.Parameters.AddWithValue("@reference", request.ReferenceExterne.Trim());
                insert.Parameters.AddWithValue(
                    "@adminId",
                    uint.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var adminId) ? adminId : 0);
                await insert.ExecuteNonQueryAsync(cancellationToken);
                refundId = Convert.ToUInt32(await new MySqlCommand(
                        "SELECT LAST_INSERT_ID()", connection, (MySqlTransaction)transaction)
                    .ExecuteScalarAsync(cancellationToken));
            }

            if (request.Montant == remaining)
            {
                await using var updatePayment = new MySqlCommand(
                    "UPDATE Paiement SET Statut = 'Remboursé' WHERE Id_Paiement = @id",
                    connection,
                    (MySqlTransaction)transaction);
                updatePayment.Parameters.AddWithValue("@id", request.IdPaiement);
                await updatePayment.ExecuteNonQueryAsync(cancellationToken);
            }

            await OperationAudit.WriteAsync(
                connection,
                (MySqlTransaction)transaction,
                uint.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentAdminId)
                    ? currentAdminId
                    : null,
                "Remboursement enregistré",
                "Remboursement",
                refundId.ToString(),
                new { idPaiement = request.IdPaiement, request.Montant, devise = currency },
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return StatusCode(StatusCodes.Status201Created, new
            {
                id = refundId,
                idPaiement = request.IdPaiement,
                montant = request.Montant,
                devise = currency.Trim(),
                soldeApresRemboursement = remaining - request.Montant
            });
        }
        catch (MySqlException exception) when (exception.Number == 1062)
        {
            return Conflict(new ProblemDetails { Title = "Cette référence de remboursement a déjà été enregistrée." });
        }
        catch (MySqlException exception)
        {
            _logger.LogError(exception, "Unable to record refund for payment {PaymentId}.", request.IdPaiement);
            return DatabaseUnavailable();
        }
    }

    private ObjectResult DatabaseUnavailable() => Problem(
        title: "Le service des remboursements est temporairement indisponible.",
        statusCode: StatusCodes.Status503ServiceUnavailable);
}
