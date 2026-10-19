using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using Project_Malawi_Resto.Models;

namespace Project_Malawi_Resto.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class ClientsController : ControllerBase
{
    private readonly ILogger<ClientsController> _logger;

    public ClientsController(ILogger<ClientsController> logger)
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
                SELECT idClient, nomClient, prenomClient, telephone, adresse, email, Id_Utilisateur
                FROM Client
                WHERE (@isStaff = TRUE OR Id_Utilisateur = @userId)
                ORDER BY nomClient, prenomClient
                """,
                connection);
            AddAccessParameters(command);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var clients = new List<object>();
            while (await reader.ReadAsync(cancellationToken))
            {
                clients.Add(ToResponse(reader));
            }

            return Ok(clients);
        }
        catch (MySqlException exception)
        {
            _logger.LogError(exception, "Unable to retrieve clients.");
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
                SELECT idClient, nomClient, prenomClient, telephone, adresse, email, Id_Utilisateur
                FROM Client
                WHERE idClient = @id AND (@isStaff = TRUE OR Id_Utilisateur = @userId)
                """,
                connection);
            command.Parameters.AddWithValue("@id", id);
            AddAccessParameters(command);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await reader.ReadAsync(cancellationToken) ? Ok(ToResponse(reader)) : NotFound();
        }
        catch (MySqlException exception)
        {
            _logger.LogError(exception, "Unable to retrieve client {ClientId}.", id);
            return DatabaseUnavailable();
        }
    }

    [Authorize(Roles = "Administrateur,Caissier")]
    [HttpPost]
    public async Task<IActionResult> Create(CreateClientRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = DatabaseHelper.GetConnection();
            await connection.OpenAsync(cancellationToken);
            await using var command = new MySqlCommand(
                """
                INSERT INTO Client (nomClient, prenomClient, telephone, adresse, email)
                VALUES (@nom, @prenom, @telephone, @adresse, @email)
                """,
                connection);
            AddClientParameters(command, request.Nom, request.Prenom, request.Telephone, request.Adresse, request.Email);
            await command.ExecuteNonQueryAsync(cancellationToken);
            var id = Convert.ToUInt32(await new MySqlCommand("SELECT LAST_INSERT_ID()", connection)
                .ExecuteScalarAsync(cancellationToken));
            return CreatedAtAction(nameof(GetById), new { id }, new { id });
        }
        catch (MySqlException exception) when (exception.Number == 1062)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Un client utilise déjà cette adresse électronique.",
                Status = StatusCodes.Status409Conflict
            });
        }
        catch (MySqlException exception)
        {
            _logger.LogError(exception, "Unable to create client.");
            return DatabaseUnavailable();
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateClientRequest request, CancellationToken cancellationToken)
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
                UPDATE Client
                SET nomClient = @nom, prenomClient = @prenom, telephone = @telephone,
                    adresse = @adresse, email = @email
                WHERE idClient = @id AND (@isStaff = TRUE OR Id_Utilisateur = @userId)
                """,
                connection);
            command.Parameters.AddWithValue("@id", id);
            AddAccessParameters(command);
            AddClientParameters(command, request.Nom, request.Prenom, request.Telephone, request.Adresse, request.Email);
            return await command.ExecuteNonQueryAsync(cancellationToken) == 0 ? NotFound() : NoContent();
        }
        catch (MySqlException exception) when (exception.Number == 1062)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Un client utilise déjà cette adresse électronique.",
                Status = StatusCodes.Status409Conflict
            });
        }
        catch (MySqlException exception)
        {
            _logger.LogError(exception, "Unable to update client {ClientId}.", id);
            return DatabaseUnavailable();
        }
    }

    private void AddAccessParameters(MySqlCommand command)
    {
        var isStaff = User.IsInRole("Administrateur") || User.IsInRole("Caissier");
        command.Parameters.AddWithValue("@isStaff", isStaff);
        command.Parameters.AddWithValue(
            "@userId",
            uint.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : 0);
    }

    private static void AddClientParameters(
        MySqlCommand command,
        string nom,
        string prenom,
        string? telephone,
        string? adresse,
        string? email)
    {
        command.Parameters.AddWithValue("@nom", nom.Trim());
        command.Parameters.AddWithValue("@prenom", prenom.Trim());
        command.Parameters.AddWithValue("@telephone", (object?)telephone?.Trim() ?? DBNull.Value);
        command.Parameters.AddWithValue("@adresse", (object?)adresse?.Trim() ?? DBNull.Value);
        command.Parameters.AddWithValue("@email", (object?)email?.Trim().ToLowerInvariant() ?? DBNull.Value);
    }

    private static object ToResponse(System.Data.Common.DbDataReader reader) => new
    {
        id = Convert.ToUInt32(reader["idClient"]),
        nom = reader.GetString(reader.GetOrdinal("nomClient")),
        prenom = reader.GetString(reader.GetOrdinal("prenomClient")),
        telephone = reader.IsDBNull(reader.GetOrdinal("telephone")) ? null : reader.GetString(reader.GetOrdinal("telephone")),
        adresse = reader.IsDBNull(reader.GetOrdinal("adresse")) ? null : reader.GetString(reader.GetOrdinal("adresse")),
        email = reader.IsDBNull(reader.GetOrdinal("email")) ? null : reader.GetString(reader.GetOrdinal("email"))
    };

    private ObjectResult DatabaseUnavailable() => Problem(
        title: "Le service des clients est temporairement indisponible.",
        statusCode: StatusCodes.Status503ServiceUnavailable);
}
