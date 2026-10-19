using System.Data;
using MySql.Data.MySqlClient;

namespace Project_Malawi_Resto.Models;

public class Produit
{
    public int IdProduit { get; set; }
    public string Nom { get; set; } = string.Empty;
    public int Quantite { get; set; }
    public string? Description { get; set; }
    public decimal Prix { get; set; }
    public string Categorie { get; set; } = string.Empty;
    public string TypeProduit { get; set; } = string.Empty;
    public bool Disponibilite { get; set; } = true;
}

internal static class ProduitRepository
{
    internal static bool Insert(Produit produit)
    {
        using var connection = DatabaseHelper.GetConnection();
        connection.Open();
        using var command = new MySqlCommand(
            """
            INSERT INTO Produit
                (Nom, Quantite, `Description`, Prix, Categorie, Type_Produit, Disponibilite)
            VALUES (@name, @quantity, @description, @price, @category, @type, @available)
            """,
            connection);
        AddParameters(command, produit);
        return command.ExecuteNonQuery() > 0;
    }

    internal static Produit? Find(int id, string type)
    {
        using var connection = DatabaseHelper.GetConnection();
        connection.Open();
        using var command = new MySqlCommand(
            """
            SELECT Id_Produit, Nom, Quantite, `Description`, Prix, Categorie,
                   Type_Produit, Disponibilite
            FROM Produit
            WHERE Id_Produit = @id AND Type_Produit = @type
            """,
            connection);
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@type", type);
        using var reader = command.ExecuteReader();
        return reader.Read() ? Read(reader) : null;
    }

    internal static bool Update(Produit produit, string type)
    {
        using var connection = DatabaseHelper.GetConnection();
        connection.Open();
        using var command = new MySqlCommand(
            """
            UPDATE Produit
            SET Nom = @name, Quantite = @quantity, `Description` = @description,
                Prix = @price, Categorie = @category, Disponibilite = @available
            WHERE Id_Produit = @id AND Type_Produit = @type
            """,
            connection);
        AddParameters(command, produit);
        command.Parameters.AddWithValue("@id", produit.IdProduit);
        command.Parameters.AddWithValue("@type", type);
        return command.ExecuteNonQuery() > 0;
    }

    internal static bool Deactivate(int id, string type)
    {
        using var connection = DatabaseHelper.GetConnection();
        connection.Open();
        using var command = new MySqlCommand(
            "UPDATE Produit SET Disponibilite = FALSE WHERE Id_Produit = @id AND Type_Produit = @type",
            connection);
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@type", type);
        return command.ExecuteNonQuery() > 0;
    }

    internal static List<Produit> List(string type, bool availableOnly = false)
    {
        using var connection = DatabaseHelper.GetConnection();
        connection.Open();
        using var command = new MySqlCommand(
            """
            SELECT Id_Produit, Nom, Quantite, `Description`, Prix, Categorie,
                   Type_Produit, Disponibilite
            FROM Produit
            WHERE Type_Produit = @type
              AND (@availableOnly = FALSE OR (Disponibilite = TRUE AND Quantite > 0))
            ORDER BY Categorie, Nom
            """,
            connection);
        command.Parameters.AddWithValue("@type", type);
        command.Parameters.AddWithValue("@availableOnly", availableOnly);
        using var reader = command.ExecuteReader();
        var products = new List<Produit>();
        while (reader.Read())
        {
            products.Add(Read(reader));
        }

        return products;
    }

    internal static bool SetQuantity(int id, int quantity, string type)
    {
        using var connection = DatabaseHelper.GetConnection();
        connection.Open();
        using var command = new MySqlCommand(
            "UPDATE Produit SET Quantite = @quantity WHERE Id_Produit = @id AND Type_Produit = @type",
            connection);
        command.Parameters.AddWithValue("@quantity", quantity);
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@type", type);
        return command.ExecuteNonQuery() > 0;
    }

    internal static DataTable TopSellers(DateTime start, DateTime end, int limit, string type, string idAlias, string nameAlias)
    {
        using var connection = DatabaseHelper.GetConnection();
        connection.Open();
        using var command = new MySqlCommand(
            $"""
            SELECT p.Id_Produit AS `{idAlias}`, p.Nom AS `{nameAlias}`,
                   SUM(d.Quantite) AS TotalVendu, p.Prix
            FROM Produit p
            INNER JOIN Detail_Commande d ON d.Id_Produit = p.Id_Produit
            INNER JOIN Commande c ON c.Id_Commande = d.Id_Commande
            WHERE p.Type_Produit = @type
              AND c.Date_Commande BETWEEN @start AND @end
              AND c.Statut <> 'Annulée'
            GROUP BY p.Id_Produit, p.Nom, p.Prix
            ORDER BY TotalVendu DESC
            LIMIT @limit
            """,
            connection);
        command.Parameters.AddWithValue("@type", type);
        command.Parameters.AddWithValue("@start", start);
        command.Parameters.AddWithValue("@end", end);
        command.Parameters.AddWithValue("@limit", Math.Max(1, limit));
        using var adapter = new MySqlDataAdapter(command);
        var table = new DataTable();
        adapter.Fill(table);
        return table;
    }

    private static void AddParameters(MySqlCommand command, Produit produit)
    {
        command.Parameters.AddWithValue("@name", produit.Nom);
        command.Parameters.AddWithValue("@quantity", produit.Quantite);
        command.Parameters.AddWithValue("@description", (object?)produit.Description ?? DBNull.Value);
        command.Parameters.AddWithValue("@price", produit.Prix);
        command.Parameters.AddWithValue("@category", produit.Categorie);
        command.Parameters.AddWithValue("@type", produit.TypeProduit);
        command.Parameters.AddWithValue("@available", produit.Disponibilite);
    }

    private static Produit Read(MySqlDataReader reader) => new()
    {
        IdProduit = Convert.ToInt32(reader["Id_Produit"]),
        Nom = reader.GetString(reader.GetOrdinal("Nom")),
        Quantite = Convert.ToInt32(reader["Quantite"]),
        Description = reader.IsDBNull(reader.GetOrdinal("Description"))
            ? null
            : reader.GetString(reader.GetOrdinal("Description")),
        Prix = reader.GetDecimal(reader.GetOrdinal("Prix")),
        Categorie = reader.GetString(reader.GetOrdinal("Categorie")),
        TypeProduit = reader.GetString(reader.GetOrdinal("Type_Produit")),
        Disponibilite = reader.GetBoolean(reader.GetOrdinal("Disponibilite"))
    };
}
