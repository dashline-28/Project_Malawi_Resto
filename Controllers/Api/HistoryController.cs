using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using Project_Malawi_Resto.Models;

namespace Project_Malawi_Resto.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Administrateur,Superviseur,Directeur")]
public sealed class HistoryController : ControllerBase
{
    private readonly ILogger<HistoryController> _logger;

    public HistoryController(ILogger<HistoryController> logger)
    {
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetRecent(
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 500)
        {
            return BadRequest(new ProblemDetails { Title = "La limite doit être comprise entre 1 et 500." });
        }

        try
        {
            await using var connection = DatabaseHelper.GetConnection();
            await connection.OpenAsync(cancellationToken);
            var canViewAllHistory = User.IsInRole("Administrateur") || User.IsInRole("Directeur");
            await using var command = new MySqlCommand(
                """
                SELECT h.Id_Operation, h.Id_Utilisateur, u.email AS Email,
                       h.Type_Operation, h.Entite, h.Id_Entite, h.Details, h.Date_Operation
                FROM Historique_Operation h
                LEFT JOIN Utilisateur u ON u.idUtilisateur = h.Id_Utilisateur
                WHERE @canViewAllHistory = TRUE
                   OR h.Entite IN ('Commande', 'Livraison', 'Paiement', 'Remboursement')
                ORDER BY h.Date_Operation DESC, h.Id_Operation DESC
                LIMIT @limit
                """,
                connection);
            command.Parameters.AddWithValue("@canViewAllHistory", canViewAllHistory);
            command.Parameters.AddWithValue("@limit", limit);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var history = new List<object>();
            while (await reader.ReadAsync(cancellationToken))
            {
                history.Add(new
                {
                    id = Convert.ToUInt64(reader["Id_Operation"]),
                    idUtilisateur = reader.IsDBNull(reader.GetOrdinal("Id_Utilisateur"))
                        ? (uint?)null
                        : Convert.ToUInt32(reader["Id_Utilisateur"]),
                    email = reader.IsDBNull(reader.GetOrdinal("Email"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("Email")),
                    type = reader.GetString(reader.GetOrdinal("Type_Operation")),
                    entite = reader.GetString(reader.GetOrdinal("Entite")),
                    idEntite = reader.IsDBNull(reader.GetOrdinal("Id_Entite"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("Id_Entite")),
                    details = reader.IsDBNull(reader.GetOrdinal("Details"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("Details")),
                    date = reader.GetDateTime(reader.GetOrdinal("Date_Operation"))
                });
            }

            return Ok(history);
        }
        catch (MySqlException exception)
        {
            _logger.LogError(exception, "Unable to retrieve operation history.");
            return Problem(
                title: "Le service d'historique est temporairement indisponible.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}
