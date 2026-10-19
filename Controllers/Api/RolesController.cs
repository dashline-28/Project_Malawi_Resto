using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using Project_Malawi_Resto.Models;

namespace Project_Malawi_Resto.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Administrateur")]
public sealed class RolesController : ControllerBase
{
    private readonly ILogger<RolesController> _logger;

    public RolesController(ILogger<RolesController> logger)
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
                "SELECT Nom_Role FROM Role WHERE Nom_Role <> 'Client' ORDER BY Nom_Role",
                connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var roles = new List<string>();
            while (await reader.ReadAsync(cancellationToken))
            {
                roles.Add(reader.GetString(0));
            }

            return Ok(roles);
        }
        catch (MySqlException exception)
        {
            _logger.LogError(exception, "Unable to list application roles.");
            return Problem(
                title: "Le service des rôles est temporairement indisponible.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}
