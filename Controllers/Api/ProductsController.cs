using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using Project_Malawi_Resto.Models;

namespace Project_Malawi_Resto.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
public sealed class ProductsController : ControllerBase
{
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(ILogger<ProductsController> logger)
    {
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        const string query = """
            SELECT p.Id_Produit AS Id, p.Nom AS Name, p.`Description` AS Description,
                   p.Prix AS Price, p.Categorie AS Category, p.Type_Produit AS ProductType,
                   (p.Disponibilite AND p.Quantite > 0) AS IsAvailable,
                   p.Quantite AS Quantity, p.Disponibilite AS IsEnabled
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
            var products = new List<MenuItemResponse>();
            while (await reader.ReadAsync(cancellationToken))
            {
                products.Add(ReadProduct(reader));
            }

            return Ok(products);
        }
        catch (MySqlException exception)
        {
            _logger.LogError(exception, "Unable to list products.");
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
                SELECT p.Id_Produit AS Id, p.Nom AS Name, p.`Description` AS Description,
                       p.Prix AS Price, p.Categorie AS Category, p.Type_Produit AS ProductType,
                       (p.Disponibilite AND p.Quantite > 0) AS IsAvailable,
                       p.Quantite AS Quantity, p.Disponibilite AS IsEnabled
                FROM Produit p
                INNER JOIN Categorie c ON c.Nom_Categorie = p.Categorie
                WHERE p.Id_Produit = @id AND c.Active = TRUE
                """,
                connection);
            command.Parameters.AddWithValue("@id", id);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await reader.ReadAsync(cancellationToken) ? Ok(ReadProduct(reader)) : NotFound();
        }
        catch (MySqlException exception)
        {
            _logger.LogError(exception, "Unable to retrieve product {ProductId}.", id);
            return DatabaseUnavailable();
        }
    }

    [Authorize(Roles = "Administrateur")]
    [HttpPost]
    public async Task<IActionResult> Create(ProductRequest request, CancellationToken cancellationToken)
    {
        if (!IsProductTypeValid(request.TypeProduit))
        {
            return BadRequest(new ProblemDetails { Title = "Le type de produit doit être Plat ou Boisson." });
        }

        try
        {
            await using var connection = DatabaseHelper.GetConnection();
            await connection.OpenAsync(cancellationToken);
            await using var command = new MySqlCommand(
                """
                INSERT INTO Produit (Nom, Quantite, `Description`, Prix, Categorie, Type_Produit, Disponibilite)
                SELECT @name, @quantity, @description, @price, Nom_Categorie, @type, @available
                FROM Categorie
                WHERE Nom_Categorie = @category AND Active = TRUE
                """,
                connection);
            AddProductParameters(command, request);
            if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
            {
                return BadRequest(new ProblemDetails { Title = "La catégorie est introuvable ou inactive." });
            }

            var id = Convert.ToUInt32(await new MySqlCommand("SELECT LAST_INSERT_ID()", connection)
                .ExecuteScalarAsync(cancellationToken));
            return CreatedAtAction(nameof(GetById), new { id }, new { id, nom = request.Nom.Trim() });
        }
        catch (MySqlException exception)
        {
            _logger.LogError(exception, "Unable to create product.");
            return DatabaseUnavailable();
        }
    }

    [Authorize(Roles = "Administrateur")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, ProductRequest request, CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return BadRequest();
        }

        if (!IsProductTypeValid(request.TypeProduit))
        {
            return BadRequest(new ProblemDetails { Title = "Le type de produit doit être Plat ou Boisson." });
        }

        try
        {
            await using var connection = DatabaseHelper.GetConnection();
            await connection.OpenAsync(cancellationToken);
            await using var command = new MySqlCommand(
                """
                UPDATE Produit p
                INNER JOIN Categorie c ON c.Nom_Categorie = @category AND c.Active = TRUE
                SET p.Nom = @name, p.Quantite = @quantity, p.`Description` = @description,
                    p.Prix = @price, p.Categorie = c.Nom_Categorie,
                    p.Type_Produit = @type, p.Disponibilite = @available
                WHERE p.Id_Produit = @id
                """,
                connection);
            command.Parameters.AddWithValue("@id", id);
            AddProductParameters(command, request);
            if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
            {
                await using var categoryCheck = new MySqlCommand(
                    "SELECT COUNT(*) FROM Categorie WHERE Nom_Categorie = @category AND Active = TRUE",
                    connection);
                categoryCheck.Parameters.AddWithValue("@category", request.Categorie.Trim());
                if (Convert.ToInt32(await categoryCheck.ExecuteScalarAsync(cancellationToken)) == 0)
                {
                    return BadRequest(new ProblemDetails { Title = "La catégorie est introuvable ou inactive." });
                }

                await using var productCheck = new MySqlCommand(
                    "SELECT COUNT(*) FROM Produit WHERE Id_Produit = @id",
                    connection);
                productCheck.Parameters.AddWithValue("@id", id);
                if (Convert.ToInt32(await productCheck.ExecuteScalarAsync(cancellationToken)) == 0)
                {
                    return NotFound();
                }
            }

            return NoContent();
        }
        catch (MySqlException exception)
        {
            _logger.LogError(exception, "Unable to update product {ProductId}.", id);
            return DatabaseUnavailable();
        }
    }

    [Authorize(Roles = "Administrateur")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Disable(int id, CancellationToken cancellationToken)
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
                "UPDATE Produit SET Disponibilite = FALSE WHERE Id_Produit = @id",
                connection);
            command.Parameters.AddWithValue("@id", id);
            return await command.ExecuteNonQueryAsync(cancellationToken) == 0 ? NotFound() : NoContent();
        }
        catch (MySqlException exception)
        {
            _logger.LogError(exception, "Unable to disable product {ProductId}.", id);
            return DatabaseUnavailable();
        }
    }

    private static bool IsProductTypeValid(string type) => type is "Plat" or "Boisson";

    private static void AddProductParameters(MySqlCommand command, ProductRequest request)
    {
        command.Parameters.AddWithValue("@name", request.Nom.Trim());
        command.Parameters.AddWithValue("@quantity", request.Quantite);
        command.Parameters.AddWithValue("@description", (object?)request.Description?.Trim() ?? DBNull.Value);
        command.Parameters.AddWithValue("@price", request.Prix);
        command.Parameters.AddWithValue("@category", request.Categorie.Trim());
        command.Parameters.AddWithValue("@type", request.TypeProduit);
        command.Parameters.AddWithValue("@available", request.Disponibilite);
    }

    private static MenuItemResponse ReadProduct(System.Data.Common.DbDataReader reader)
    {
        var descriptionOrdinal = reader.GetOrdinal("Description");
        return new MenuItemResponse(
            Convert.ToUInt32(reader["Id"]),
            reader.GetString(reader.GetOrdinal("Name")),
            reader.IsDBNull(descriptionOrdinal) ? null : reader.GetString(descriptionOrdinal),
            reader.GetDecimal(reader.GetOrdinal("Price")),
            reader.GetString(reader.GetOrdinal("Category")),
            reader.GetBoolean(reader.GetOrdinal("IsAvailable")),
            reader.GetString(reader.GetOrdinal("ProductType")),
            Convert.ToInt32(reader["Quantity"]),
            reader.GetBoolean(reader.GetOrdinal("IsEnabled")));
    }

    private ObjectResult DatabaseUnavailable() => Problem(
        title: "Le service des produits est temporairement indisponible.",
        statusCode: StatusCodes.Status503ServiceUnavailable);
}
