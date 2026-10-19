using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using Project_Malawi_Resto.Models;

namespace Project_Malawi_Resto.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Administrateur,Caissier,Livreur,Superviseur")]
public sealed class DeliveriesController : ControllerBase
{
    private readonly ILogger<DeliveriesController> _logger;

    public DeliveriesController(ILogger<DeliveriesController> logger)
    {
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var isStaff = User.IsInRole("Administrateur") ||
                      User.IsInRole("Caissier") ||
                      User.IsInRole("Superviseur");
        try
        {
            await using var connection = DatabaseHelper.GetConnection();
            await connection.OpenAsync(cancellationToken);
            await using var command = new MySqlCommand(
                """
                SELECT l.Id_Livraison, l.Id_Commande, l.Id_Livreur, l.Date_Debut,
                       l.Date_Livree, l.Statut, c.Adresse_Livraison, cl.telephone,
                       cl.nomClient, cl.prenomClient
                FROM Livraison l
                INNER JOIN Commande c ON c.Id_Commande = l.Id_Commande
                INNER JOIN Client cl ON cl.idClient = c.Id_Client
                WHERE (@isStaff = TRUE OR l.Id_Livreur = @userId)
                ORDER BY l.Date_Debut DESC
                LIMIT 500
                """,
                connection);
            command.Parameters.AddWithValue("@isStaff", isStaff);
            command.Parameters.AddWithValue("@userId", GetUserId());
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var deliveries = new List<object>();
            while (await reader.ReadAsync(cancellationToken))
            {
                deliveries.Add(ToResponse(reader));
            }

            return Ok(deliveries);
        }
        catch (MySqlException exception)
        {
            _logger.LogError(exception, "Unable to retrieve deliveries.");
            return DatabaseUnavailable();
        }
    }

    [Authorize(Roles = "Administrateur,Superviseur")]
    [HttpPost]
    public async Task<IActionResult> Assign(AssignDeliveryRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = DatabaseHelper.GetConnection();
            await connection.OpenAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            string? status;
            string? serviceMode;
            await using (var orderCommand = new MySqlCommand(
                "SELECT Statut, Mode_Service FROM Commande WHERE Id_Commande = @id FOR UPDATE",
                connection,
                (MySqlTransaction)transaction))
            {
                orderCommand.Parameters.AddWithValue("@id", request.IdCommande);
                await using var reader = await orderCommand.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                {
                    return NotFound(new ProblemDetails { Title = "Commande introuvable." });
                }

                status = reader.GetString(reader.GetOrdinal("Statut"));
                serviceMode = reader.GetString(reader.GetOrdinal("Mode_Service"));
            }

            if (status != "Prête" || serviceMode != "Livraison")
            {
                return Conflict(new ProblemDetails
                {
                    Title = "Seule une commande prête et destinée à la livraison peut être assignée."
                });
            }

            await using (var courierCheck = new MySqlCommand(
                """
                SELECT idUtilisateur
                FROM Utilisateur
                WHERE idUtilisateur = @id AND role = 'Livreur' AND actif = TRUE
                FOR UPDATE
                """,
                connection,
                (MySqlTransaction)transaction))
            {
                courierCheck.Parameters.AddWithValue("@id", request.IdLivreur);
                if (await courierCheck.ExecuteScalarAsync(cancellationToken) is null)
                {
                    return BadRequest(new ProblemDetails { Title = "Le livreur est introuvable ou inactif." });
                }
            }

            await using (var workloadCheck = new MySqlCommand(
                "SELECT COUNT(*) FROM Livraison WHERE Id_Livreur = @id AND Statut = 'En cours'",
                connection,
                (MySqlTransaction)transaction))
            {
                workloadCheck.Parameters.AddWithValue("@id", request.IdLivreur);
                if (Convert.ToInt32(await workloadCheck.ExecuteScalarAsync(cancellationToken)) > 0)
                {
                    return Conflict(new ProblemDetails { Title = "Le livreur a déjà une livraison en cours." });
                }
            }

            await using (var insert = new MySqlCommand(
                "INSERT INTO Livraison (Id_Commande, Id_Livreur) VALUES (@orderId, @courierId)",
                connection,
                (MySqlTransaction)transaction))
            {
                insert.Parameters.AddWithValue("@orderId", request.IdCommande);
                insert.Parameters.AddWithValue("@courierId", request.IdLivreur);
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var updateOrder = new MySqlCommand(
                "UPDATE Commande SET Statut = 'En livraison' WHERE Id_Commande = @id AND Statut = 'Prête'",
                connection,
                (MySqlTransaction)transaction))
            {
                updateOrder.Parameters.AddWithValue("@id", request.IdCommande);
                await updateOrder.ExecuteNonQueryAsync(cancellationToken);
            }

            var deliveryId = Convert.ToUInt32(await new MySqlCommand(
                    "SELECT LAST_INSERT_ID()", connection, (MySqlTransaction)transaction)
                .ExecuteScalarAsync(cancellationToken));
            await OperationAudit.WriteAsync(
                connection,
                (MySqlTransaction)transaction,
                GetUserId(),
                "Livraison assignée",
                "Livraison",
                deliveryId.ToString(),
                new { idCommande = request.IdCommande, idLivreur = request.IdLivreur },
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return StatusCode(StatusCodes.Status201Created, new { id = deliveryId, idCommande = request.IdCommande });
        }
        catch (MySqlException exception) when (exception.Number == 1062)
        {
            return Conflict(new ProblemDetails { Title = "Une livraison est déjà assignée à cette commande." });
        }
        catch (MySqlException exception)
        {
            _logger.LogError(exception, "Unable to assign delivery for order {OrderId}.", request.IdCommande);
            return DatabaseUnavailable();
        }
    }

    [Authorize(Roles = "Administrateur,Superviseur")]
    [HttpGet("couriers")]
    public async Task<IActionResult> GetAvailableCouriers(CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = DatabaseHelper.GetConnection();
            await connection.OpenAsync(cancellationToken);
            await using var command = new MySqlCommand(
                """
                SELECT u.idUtilisateur AS Id, u.nom, u.prenom
                FROM Utilisateur u
                WHERE u.role = 'Livreur'
                  AND u.actif = TRUE
                  AND NOT EXISTS (
                      SELECT 1
                      FROM Livraison l
                      WHERE l.Id_Livreur = u.idUtilisateur
                        AND l.Statut = 'En cours'
                  )
                ORDER BY u.nom, u.prenom
                """,
                connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var couriers = new List<object>();
            while (await reader.ReadAsync(cancellationToken))
            {
                couriers.Add(new
                {
                    id = Convert.ToUInt32(reader["Id"]),
                    nom = reader.GetString(reader.GetOrdinal("nom")),
                    prenom = reader.GetString(reader.GetOrdinal("prenom"))
                });
            }

            return Ok(couriers);
        }
        catch (MySqlException exception)
        {
            _logger.LogError(exception, "Unable to retrieve available couriers.");
            return DatabaseUnavailable();
        }
    }

    [Authorize(Roles = "Administrateur,Livreur")]
    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(
        int id,
        UpdateDeliveryStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (id <= 0 || request.Statut != "Livrée")
        {
            return BadRequest(new ProblemDetails { Title = "Le seul changement de statut permis est « Livrée »." });
        }

        try
        {
            await using var connection = DatabaseHelper.GetConnection();
            await connection.OpenAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            uint orderId;
            uint courierId;
            string status;
            await using (var select = new MySqlCommand(
                "SELECT Id_Commande, Id_Livreur, Statut FROM Livraison WHERE Id_Livraison = @id FOR UPDATE",
                connection,
                (MySqlTransaction)transaction))
            {
                select.Parameters.AddWithValue("@id", id);
                await using var reader = await select.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                {
                    return NotFound();
                }

                orderId = Convert.ToUInt32(reader["Id_Commande"]);
                courierId = Convert.ToUInt32(reader["Id_Livreur"]);
                status = reader.GetString(reader.GetOrdinal("Statut"));
            }

            if (status != "En cours" ||
                (!User.IsInRole("Administrateur") && courierId != GetUserId()))
            {
                return Conflict(new ProblemDetails { Title = "Cette livraison ne peut pas être modifiée par ce compte." });
            }

            await using (var updateDelivery = new MySqlCommand(
                """
                UPDATE Livraison
                SET Statut = 'Livrée', Date_Livree = CURRENT_TIMESTAMP
                WHERE Id_Livraison = @id AND Statut = 'En cours'
                """,
                connection,
                (MySqlTransaction)transaction))
            {
                updateDelivery.Parameters.AddWithValue("@id", id);
                await updateDelivery.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var updateOrder = new MySqlCommand(
                "UPDATE Commande SET Statut = 'Livrée' WHERE Id_Commande = @id AND Statut = 'En livraison'",
                connection,
                (MySqlTransaction)transaction))
            {
                updateOrder.Parameters.AddWithValue("@id", orderId);
                await updateOrder.ExecuteNonQueryAsync(cancellationToken);
            }

            await OperationAudit.WriteAsync(
                connection,
                (MySqlTransaction)transaction,
                GetUserId(),
                "Livraison terminée",
                "Livraison",
                id.ToString(),
                new { idCommande = orderId, idLivreur = courierId },
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return NoContent();
        }
        catch (MySqlException exception)
        {
            _logger.LogError(exception, "Unable to complete delivery {DeliveryId}.", id);
            return DatabaseUnavailable();
        }
    }

    private uint GetUserId() =>
        uint.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : 0;

    private static object ToResponse(System.Data.Common.DbDataReader reader) => new
    {
        id = Convert.ToUInt32(reader["Id_Livraison"]),
        idCommande = Convert.ToUInt32(reader["Id_Commande"]),
        idLivreur = Convert.ToUInt32(reader["Id_Livreur"]),
        dateDebut = reader.GetDateTime(reader.GetOrdinal("Date_Debut")),
        dateLivree = reader.IsDBNull(reader.GetOrdinal("Date_Livree"))
            ? (DateTime?)null
            : reader.GetDateTime(reader.GetOrdinal("Date_Livree")),
        statut = reader.GetString(reader.GetOrdinal("Statut")),
        adresse = reader.IsDBNull(reader.GetOrdinal("Adresse_Livraison"))
            ? null
            : reader.GetString(reader.GetOrdinal("Adresse_Livraison")),
        telephoneClient = reader.IsDBNull(reader.GetOrdinal("telephone"))
            ? null
            : reader.GetString(reader.GetOrdinal("telephone")),
        client = $"{reader.GetString(reader.GetOrdinal("prenomClient"))} {reader.GetString(reader.GetOrdinal("nomClient"))}"
    };

    private ObjectResult DatabaseUnavailable() => Problem(
        title: "Le service des livraisons est temporairement indisponible.",
        statusCode: StatusCodes.Status503ServiceUnavailable);
}
