using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using Project_Malawi_Resto.Models;

namespace Project_Malawi_Resto.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class PaymentsController : ControllerBase
{
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(ILogger<PaymentsController> logger)
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
                SELECT p.Id_Paiement, p.Date_Paiement, p.Montant, p.Devise,
                       p.Mode_Paiement, p.Reference_Transaction, p.Statut, p.Id_Commande
                FROM Paiement p
                INNER JOIN Commande c ON c.Id_Commande = p.Id_Commande
                INNER JOIN Client cl ON cl.idClient = c.Id_Client
                WHERE (@isStaff = TRUE OR cl.Id_Utilisateur = @userId)
                ORDER BY p.Date_Paiement DESC
                LIMIT 500
                """,
                connection);
            AddAccessParameters(command);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var payments = new List<object>();
            while (await reader.ReadAsync(cancellationToken))
            {
                payments.Add(ToResponse(reader));
            }

            return Ok(payments);
        }
        catch (MySqlException exception)
        {
            _logger.LogError(exception, "Unable to retrieve payments.");
            return DatabaseUnavailable();
        }
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return BadRequest();
        }

        try
        {
            await using var connection = DatabaseHelper.GetConnection();
            await connection.OpenAsync(cancellationToken);
            await using var command = new MySqlCommand(
                """
                SELECT p.Id_Paiement, p.Date_Paiement, p.Montant, p.Devise,
                       p.Mode_Paiement, p.Reference_Transaction, p.Statut, p.Id_Commande
                FROM Paiement p
                INNER JOIN Commande c ON c.Id_Commande = p.Id_Commande
                INNER JOIN Client cl ON cl.idClient = c.Id_Client
                WHERE p.Id_Paiement = @id
                  AND (@isStaff = TRUE OR cl.Id_Utilisateur = @userId)
                """,
                connection);
            command.Parameters.AddWithValue("@id", id);
            AddAccessParameters(command);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await reader.ReadAsync(cancellationToken) ? Ok(ToResponse(reader)) : NotFound();
        }
        catch (MySqlException exception)
        {
            _logger.LogError(exception, "Unable to retrieve payment {PaymentId}.", id);
            return DatabaseUnavailable();
        }
    }

    [HttpPost]
    public IActionResult CreatePaymentIntent(CreatePaymentRequest request)
    {
        if (request.ModePaiement is not ("MonCash" or "NatCash"))
        {
            return BadRequest(new ProblemDetails { Title = "Mode de paiement invalide." });
        }

        return Problem(
            title: "Le paiement en ligne n'est pas encore connecté à un fournisseur. Aucun paiement n'a été créé.",
            statusCode: StatusCodes.Status501NotImplemented);
    }

    private void AddAccessParameters(MySqlCommand command)
    {
        command.Parameters.AddWithValue(
            "@isStaff",
            User.IsInRole("Administrateur") ||
            User.IsInRole("Caissier") ||
            User.IsInRole("Superviseur"));
        command.Parameters.AddWithValue(
            "@userId",
            uint.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : 0);
    }

    private static object ToResponse(System.Data.Common.DbDataReader reader) => new
    {
        id = Convert.ToUInt32(reader["Id_Paiement"]),
        date = reader.GetDateTime(reader.GetOrdinal("Date_Paiement")),
        montant = reader.GetDecimal(reader.GetOrdinal("Montant")),
        devise = reader.GetString(reader.GetOrdinal("Devise")).Trim(),
        modePaiement = reader.GetString(reader.GetOrdinal("Mode_Paiement")),
        referenceTransaction = reader.IsDBNull(reader.GetOrdinal("Reference_Transaction"))
            ? null
            : reader.GetString(reader.GetOrdinal("Reference_Transaction")),
        statut = reader.GetString(reader.GetOrdinal("Statut")),
        idCommande = Convert.ToUInt32(reader["Id_Commande"])
    };

    private ObjectResult DatabaseUnavailable() => Problem(
        title: "Le service des paiements est temporairement indisponible.",
        statusCode: StatusCodes.Status503ServiceUnavailable);
}
