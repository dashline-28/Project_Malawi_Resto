using MySql.Data.MySqlClient;
using Microsoft.AspNetCore.Mvc;
using Project_Malawi_Resto.Models;

namespace Project_Malawi_Resto.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
public sealed class MenuController : ControllerBase
{
    private readonly ILogger<MenuController> _logger;

    public MenuController(ILogger<MenuController> logger)
{
    _logger = logger;
}

[HttpGet]
public async Task<ActionResult<IReadOnlyList<MenuItemResponse>>> Get(
    CancellationToken cancellationToken)
{
    const string query = """
        SELECT p.Id_Produit AS Id,
               p.Nom AS Name,
               p.`Description` AS Description,
               p.Prix AS Price,
               p.Categorie AS Category,
               (p.Disponibilite AND p.Quantite > 0) AS IsAvailable,
               p.Type_Produit AS ProductType, p.Disponibilite AS IsEnabled,
               p.Quantite AS Quantity
        FROM Produit p
        INNER JOIN Categorie c ON c.Nom_Categorie = p.Categorie
        WHERE c.Active = TRUE
        ORDER BY Category, ProductType, Name
        """;

    try
    {
        await using var connection = DatabaseHelper.GetConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(query, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var items = new List<MenuItemResponse>();
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new MenuItemResponse(
                Id: Convert.ToUInt32(reader["Id"]),
                Name: reader.GetString(reader.GetOrdinal("Name")),
                Description: reader.IsDBNull(reader.GetOrdinal("Description"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("Description")),
                Price: reader.GetDecimal(reader.GetOrdinal("Price")),
                Category: reader.GetString(reader.GetOrdinal("Category")),
                IsAvailable: reader.GetBoolean(reader.GetOrdinal("IsAvailable")),
                ProductType: reader.GetString(reader.GetOrdinal("ProductType")),
                Quantity: Convert.ToInt32(reader["Quantity"]),
                IsEnabled: reader.GetBoolean(reader.GetOrdinal("IsEnabled"))));
        }

        return Ok(items);
    }
    catch (MySqlException exception)
    {
        _logger.LogError(exception, "Unable to retrieve the restaurant menu.");
        return Problem(
            title: "Le menu est temporairement indisponible.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}

}
