using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using Project_Malawi_Resto.Models;

namespace Project_Malawi_Resto.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
public sealed class CategoriesController : ControllerBase
{
    private readonly ILogger<CategoriesController> _logger;

    public CategoriesController(ILogger<CategoriesController> logger)
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
                "SELECT Id_Categorie AS Id, Nom_Categorie AS Name, Active FROM Categorie ORDER BY Nom_Categorie",
                connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var categories = new List<object>();
            while (await reader.ReadAsync(cancellationToken))
            {
                categories.Add(new
                {
                    id = Convert.ToUInt32(reader["Id"]),
                    nom = reader.GetString(reader.GetOrdinal("Name")),
                    active = reader.GetBoolean(reader.GetOrdinal("Active"))
                });
            }

            return Ok(categories);
        }
        catch (MySqlException exception)
        {
            _logger.LogError(exception, "Unable to list categories.");
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
                SELECT Id_Categorie AS Id, Nom_Categorie AS Name, Active
                FROM Categorie
                WHERE Id_Categorie = @id
                """,
                connection);
            command.Parameters.AddWithValue("@id", id);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await reader.ReadAsync(cancellationToken)
                ? Ok(new
                {
                    id = Convert.ToUInt32(reader["Id"]),
                    nom = reader.GetString(reader.GetOrdinal("Name")),
                    active = reader.GetBoolean(reader.GetOrdinal("Active"))
                })
                : NotFound();
        }
        catch (MySqlException exception)
        {
            _logger.LogError(exception, "Unable to retrieve category {CategoryId}.", id);
            return DatabaseUnavailable();
        }
    }

    [Authorize(Roles = "Administrateur")]
    [HttpPost]
    public async Task<IActionResult> Create(CategoryRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = DatabaseHelper.GetConnection();
            await connection.OpenAsync(cancellationToken);
            await using var command = new MySqlCommand(
                "INSERT INTO Categorie (Nom_Categorie, Active) VALUES (@name, @active)",
                connection);
            command.Parameters.AddWithValue("@name", request.Nom.Trim());
            command.Parameters.AddWithValue("@active", request.Active);
            await command.ExecuteNonQueryAsync(cancellationToken);
            var id = Convert.ToUInt32(await new MySqlCommand("SELECT LAST_INSERT_ID()", connection)
                .ExecuteScalarAsync(cancellationToken));
            return CreatedAtAction(nameof(GetById), new { id }, new { id, nom = request.Nom.Trim() });
        }
        catch (MySqlException exception) when (exception.Number == 1062)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Cette catégorie existe déjà.",
                Status = StatusCodes.Status409Conflict
            });
        }
        catch (MySqlException exception)
        {
            _logger.LogError(exception, "Unable to create a category.");
            return DatabaseUnavailable();
        }
    }

    [Authorize(Roles = "Administrateur")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, CategoryRequest request, CancellationToken cancellationToken)
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
                "UPDATE Categorie SET Nom_Categorie = @name, Active = @active WHERE Id_Categorie = @id",
                connection);
            command.Parameters.AddWithValue("@id", id);
            command.Parameters.AddWithValue("@name", request.Nom.Trim());
            command.Parameters.AddWithValue("@active", request.Active);
            return await command.ExecuteNonQueryAsync(cancellationToken) == 0 ? NotFound() : NoContent();
        }
        catch (MySqlException exception) when (exception.Number == 1062)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Cette catégorie existe déjà.",
                Status = StatusCodes.Status409Conflict
            });
        }
        catch (MySqlException exception)
        {
            _logger.LogError(exception, "Unable to update category {CategoryId}.", id);
            return DatabaseUnavailable();
        }
    }

    [Authorize(Roles = "Administrateur")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
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
                "UPDATE Categorie SET Active = FALSE WHERE Id_Categorie = @id AND Active = TRUE",
                connection);
            command.Parameters.AddWithValue("@id", id);
            return await command.ExecuteNonQueryAsync(cancellationToken) == 0 ? NotFound() : NoContent();
        }
        catch (MySqlException exception)
        {
            _logger.LogError(exception, "Unable to deactivate category {CategoryId}.", id);
            return DatabaseUnavailable();
        }
    }

    private ObjectResult DatabaseUnavailable() => Problem(
        title: "Le service des catégories est temporairement indisponible.",
        statusCode: StatusCodes.Status503ServiceUnavailable);
}
