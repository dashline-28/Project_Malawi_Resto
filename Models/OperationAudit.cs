using System.Text.Json;
using MySql.Data.MySqlClient;

namespace Project_Malawi_Resto.Models;

public static class OperationAudit
{
    public static async Task WriteAsync(
        MySqlConnection connection,
        MySqlTransaction? transaction,
        uint? userId,
        string operation,
        string entity,
        string? entityId,
        object? details,
        CancellationToken cancellationToken)
    {
        const string query = """
            INSERT INTO Historique_Operation
                (Id_Utilisateur, Type_Operation, Entite, Id_Entite, Details)
            VALUES
                (@userId, @operation, @entity, @entityId, @details)
            """;

        await using var command = transaction is null
            ? new MySqlCommand(query, connection)
            : new MySqlCommand(query, connection, transaction);
        command.Parameters.AddWithValue("@userId", (object?)userId ?? DBNull.Value);
        command.Parameters.AddWithValue("@operation", operation);
        command.Parameters.AddWithValue("@entity", entity);
        command.Parameters.AddWithValue("@entityId", (object?)entityId ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "@details",
            details is null ? DBNull.Value : JsonSerializer.Serialize(details));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
