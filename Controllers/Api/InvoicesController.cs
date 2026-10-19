using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using Project_Malawi_Resto.Models;

namespace Project_Malawi_Resto.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class InvoicesController : ControllerBase
{
    private readonly ILogger<InvoicesController> _logger;

    public InvoicesController(ILogger<InvoicesController> logger)
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
                SELECT f.Id_Facture, f.Numero_Facture, f.Date_Emission, f.Montant_Total,
                       f.Devise, f.Id_Commande
                FROM Facture f
                INNER JOIN Commande c ON c.Id_Commande = f.Id_Commande
                INNER JOIN Client cl ON cl.idClient = c.Id_Client
                WHERE (@isStaff = TRUE OR cl.Id_Utilisateur = @userId)
                ORDER BY f.Date_Emission DESC
                LIMIT 500
                """,
                connection);
            AddAccessParameters(command);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var invoices = new List<object>();
            while (await reader.ReadAsync(cancellationToken))
            {
                invoices.Add(ToResponse(reader));
            }

            return Ok(invoices);
        }
        catch (MySqlException exception)
        {
            _logger.LogError(exception, "Unable to retrieve invoices.");
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
                SELECT f.Id_Facture, f.Numero_Facture, f.Date_Emission, f.Montant_Total,
                       f.Devise, f.Id_Commande
                FROM Facture f
                INNER JOIN Commande c ON c.Id_Commande = f.Id_Commande
                INNER JOIN Client cl ON cl.idClient = c.Id_Client
                WHERE f.Id_Facture = @id
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
            _logger.LogError(exception, "Unable to retrieve invoice {InvoiceId}.", id);
            return DatabaseUnavailable();
        }
    }

    private void AddAccessParameters(MySqlCommand command)
    {
        command.Parameters.AddWithValue(
            "@isStaff",
            User.IsInRole("Administrateur") || User.IsInRole("Caissier"));
        command.Parameters.AddWithValue(
            "@userId",
            uint.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : 0);
    }

    private static object ToResponse(System.Data.Common.DbDataReader reader) => new
    {
        id = Convert.ToUInt32(reader["Id_Facture"]),
        numero = reader.GetString(reader.GetOrdinal("Numero_Facture")),
        dateEmission = reader.GetDateTime(reader.GetOrdinal("Date_Emission")),
        montantTotal = reader.GetDecimal(reader.GetOrdinal("Montant_Total")),
        devise = reader.GetString(reader.GetOrdinal("Devise")).Trim(),
        idCommande = Convert.ToUInt32(reader["Id_Commande"])
    };

    private ObjectResult DatabaseUnavailable() => Problem(
        title: "Le service des factures est temporairement indisponible.",
        statusCode: StatusCodes.Status503ServiceUnavailable);
}
