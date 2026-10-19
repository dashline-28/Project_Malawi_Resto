using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using Project_Malawi_Resto.Models;

namespace Project_Malawi_Resto.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Administrateur,Caissier,Superviseur,Directeur")]
public sealed class ReportsController : ControllerBase
{
    private readonly ILogger<ReportsController> _logger;

    public ReportsController(ILogger<ReportsController> logger)
    {
        _logger = logger;
    }

    [HttpGet("sales")]
    public async Task<IActionResult> GetSales(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        var start = from ?? DateOnly.FromDateTime(DateTime.Today.AddDays(-29));
        var end = to ?? DateOnly.FromDateTime(DateTime.Today);
        if (end < start || end.DayNumber - start.DayNumber > 365)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "La période est invalide ou dépasse 366 jours."
            });
        }

        var startDate = start.ToDateTime(TimeOnly.MinValue);
        var endDateExclusive = end.AddDays(1).ToDateTime(TimeOnly.MinValue);

        try
        {
            await using var connection = DatabaseHelper.GetConnection();
            await connection.OpenAsync(cancellationToken);

            var salesByDay = new List<object>();
            await using (var salesCommand = new MySqlCommand(
                """
                SELECT DATE(Date_Paiement) AS Jour, Devise,
                       SUM(Montant) AS Total, COUNT(DISTINCT Id_Commande) AS NombreCommandes
                FROM Paiement
                WHERE Statut = 'Réussi'
                  AND Date_Paiement >= @start AND Date_Paiement < @end
                GROUP BY DATE(Date_Paiement), Devise
                ORDER BY Jour
                """,
                connection))
            {
                salesCommand.Parameters.AddWithValue("@start", startDate);
                salesCommand.Parameters.AddWithValue("@end", endDateExclusive);
                await using var reader = await salesCommand.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    salesByDay.Add(new
                    {
                        date = reader.GetDateTime(reader.GetOrdinal("Jour")),
                        devise = reader.GetString(reader.GetOrdinal("Devise")).Trim(),
                        total = reader.GetDecimal(reader.GetOrdinal("Total")),
                        nombreCommandes = Convert.ToInt32(reader["NombreCommandes"])
                    });
                }
            }

            var topProducts = new List<object>();
            await using (var productsCommand = new MySqlCommand(
                """
                SELECT Produit, TypeProduit, SUM(Quantite) AS QuantiteVendue
                FROM (
                      SELECT p.Nom AS Produit, p.Type_Produit AS TypeProduit, d.Quantite
                      FROM Detail_Commande d
                      INNER JOIN Commande c ON c.Id_Commande = d.Id_Commande
                      INNER JOIN Produit p ON p.Id_Produit = d.Id_Produit
                      WHERE EXISTS (
                          SELECT 1 FROM Paiement pa
                          WHERE pa.Id_Commande = c.Id_Commande
                            AND pa.Statut = 'Réussi'
                            AND pa.Date_Paiement >= @start AND pa.Date_Paiement < @end
                      )
                ) AS ventes
                GROUP BY Produit, TypeProduit
                ORDER BY QuantiteVendue DESC, Produit
                LIMIT 10
                """,
                connection))
            {
                productsCommand.Parameters.AddWithValue("@start", startDate);
                productsCommand.Parameters.AddWithValue("@end", endDateExclusive);
                await using var reader = await productsCommand.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    topProducts.Add(new
                    {
                        nom = reader.GetString(reader.GetOrdinal("Produit")),
                        type = reader.GetString(reader.GetOrdinal("TypeProduit")),
                        quantiteVendue = Convert.ToInt64(reader["QuantiteVendue"])
                    });
                }
            }

            return Ok(new { du = start, au = end, ventesParJour = salesByDay, produitsLesPlusVendus = topProducts });
        }
        catch (MySqlException exception)
        {
            _logger.LogError(exception, "Unable to generate sales report for {Start} to {End}.", start, end);
            return Problem(
                title: "Le service des rapports est temporairement indisponible.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}
