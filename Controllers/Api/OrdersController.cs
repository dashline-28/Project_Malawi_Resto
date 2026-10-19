using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using Project_Malawi_Resto.Models;

namespace Project_Malawi_Resto.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class OrdersController : ControllerBase
{
    private static readonly HashSet<string> OrderStatuses =
    [
        "En attente de paiement", "Confirmée", "En préparation", "Prête",
        "En livraison", "Livrée", "Retirée au restaurant", "Annulée"
    ];

    private readonly ILogger<OrdersController> _logger;

    public OrdersController(ILogger<OrdersController> logger)
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
                SELECT c.Id_Commande, c.Date_Commande, c.Montant_Total, c.Devise, c.Statut,
                       c.Mode_Service, c.Adresse_Livraison, c.Id_Client
                FROM Commande c
                INNER JOIN Client cl ON cl.idClient = c.Id_Client
                WHERE (@isStaff = TRUE OR cl.Id_Utilisateur = @userId)
                ORDER BY c.Date_Commande DESC, c.Id_Commande DESC
                LIMIT 500
                """,
                connection);
            AddAccessParameters(command, includeSupervisor: true);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var orders = new List<object>();
            while (await reader.ReadAsync(cancellationToken))
            {
                orders.Add(ReadOrder(reader));
            }

            return Ok(orders);
        }
        catch (MySqlException exception)
        {
            _logger.LogError(exception, "Unable to list orders.");
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
                SELECT c.Id_Commande, c.Date_Commande, c.Montant_Total, c.Devise, c.Statut,
                       c.Mode_Service, c.Adresse_Livraison, c.Id_Client
                FROM Commande c
                INNER JOIN Client cl ON cl.idClient = c.Id_Client
                WHERE c.Id_Commande = @id
                  AND (@isStaff = TRUE OR cl.Id_Utilisateur = @userId)
                """,
                connection);
            command.Parameters.AddWithValue("@id", id);
            AddAccessParameters(command, includeSupervisor: true);

            object? order;
            uint clientId;
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                if (!await reader.ReadAsync(cancellationToken))
                {
                    return NotFound();
                }

                order = ReadOrder(reader);
                clientId = Convert.ToUInt32(reader["Id_Client"]);
            }

            await using var detailsCommand = new MySqlCommand(
                """
                SELECT d.Id_Detail, d.Id_Produit, p.Nom, p.Type_Produit,
                       d.Quantite, d.Prix_Unitaire, d.Montant_Ligne
                FROM Detail_Commande d
                INNER JOIN Produit p ON p.Id_Produit = d.Id_Produit
                WHERE d.Id_Commande = @id
                ORDER BY d.Id_Detail
                """,
                connection);
            detailsCommand.Parameters.AddWithValue("@id", id);
            await using var detailsReader = await detailsCommand.ExecuteReaderAsync(cancellationToken);
            var lines = new List<object>();
            while (await detailsReader.ReadAsync(cancellationToken))
            {
                lines.Add(new
                {
                    id = Convert.ToUInt32(detailsReader["Id_Detail"]),
                    typeProduit = detailsReader.GetString(detailsReader.GetOrdinal("Type_Produit")),
                    idProduit = Convert.ToUInt32(detailsReader["Id_Produit"]),
                    nom = detailsReader.GetString(detailsReader.GetOrdinal("Nom")),
                    quantite = Convert.ToInt32(detailsReader["Quantite"]),
                    prixUnitaire = detailsReader.GetDecimal(detailsReader.GetOrdinal("Prix_Unitaire")),
                    montant = detailsReader.GetDecimal(detailsReader.GetOrdinal("Montant_Ligne"))
                });
            }

            return Ok(new { commande = order, idClient = clientId, lignes = lines });
        }
        catch (MySqlException exception)
        {
            _logger.LogError(exception, "Unable to retrieve order {OrderId}.", id);
            return DatabaseUnavailable();
        }
    }

    [Authorize(Roles = "Administrateur,Caissier,Client")]
    [HttpPost]
    public async Task<IActionResult> Create(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        var isStaff = IsStaff();
        if (request.ModeService is not ("Livraison" or "Retrait"))
        {
            return BadRequest(new ProblemDetails { Title = "Mode de service invalide." });
        }

        var address = string.IsNullOrWhiteSpace(request.AdresseLivraison)
            ? null
            : request.AdresseLivraison.Trim();
        if ((request.ModeService == "Livraison" && address is null) ||
            (request.ModeService == "Retrait" && address is not null))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Une adresse est obligatoire pour la livraison et interdite pour le retrait."
            });
        }

        if (!isStaff && request.IdClient.HasValue)
        {
            return Forbid();
        }

        if (isStaff && request.IdClient is null)
        {
            return BadRequest(new ProblemDetails { Title = "Un client doit être associé à la commande." });
        }

        try
        {
            await using var connection = DatabaseHelper.GetConnection();
            await connection.OpenAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            uint clientId;
            if (isStaff)
            {
                clientId = Convert.ToUInt32(request.IdClient!.Value);
                await using var clientCheck = new MySqlCommand(
                    "SELECT COUNT(*) FROM Client WHERE idClient = @id",
                    connection,
                    (MySqlTransaction)transaction);
                clientCheck.Parameters.AddWithValue("@id", clientId);
                if (Convert.ToInt32(await clientCheck.ExecuteScalarAsync(cancellationToken)) == 0)
                {
                    return BadRequest(new ProblemDetails { Title = "Le client indiqué n'existe pas." });
                }
            }
            else
            {
                await using var clientLookup = new MySqlCommand(
                    "SELECT idClient FROM Client WHERE Id_Utilisateur = @userId",
                    connection,
                    (MySqlTransaction)transaction);
                clientLookup.Parameters.AddWithValue("@userId", GetUserId());
                var result = await clientLookup.ExecuteScalarAsync(cancellationToken);
                if (result is null || result == DBNull.Value)
                {
                    return Conflict(new ProblemDetails
                    {
                        Title = "Aucun profil client n'est associé à ce compte."
                    });
                }

                clientId = Convert.ToUInt32(result);
            }

            var total = 0m;
            var validatedLines = new List<(uint ProductId, int Quantity, decimal Price)>();
            var groupedLines = request.Lignes
                .GroupBy(line => line.IdProduit)
                .Select(group => new
                {
                    IdProduit = group.Key,
                    Quantity = group.Sum(line => (long)line.Quantite),
                    Types = group.Select(line => line.TypeProduit)
                        .Where(type => !string.IsNullOrWhiteSpace(type))
                        .Distinct()
                        .ToList()
                })
                .ToList();

            foreach (var line in groupedLines)
            {
                if (line.Quantity is < 1 or > 1000 ||
                    line.IdProduit <= 0 ||
                    line.Types.Count > 1 ||
                    line.Types.Any(type => type is not ("Plat" or "Boisson")))
                {
                    return BadRequest(new ProblemDetails { Title = "Une ligne de commande est invalide." });
                }

                await using var productCommand = new MySqlCommand(
                    """
                    SELECT p.Prix, p.Disponibilite, p.Quantite, p.Type_Produit
                    FROM Produit p
                    INNER JOIN Categorie c ON c.Nom_Categorie = p.Categorie
                    WHERE p.Id_Produit = @id AND c.Active = TRUE
                    FOR UPDATE
                    """,
                    connection,
                    (MySqlTransaction)transaction);
                productCommand.Parameters.AddWithValue("@id", line.IdProduit);
                await using var productReader = await productCommand.ExecuteReaderAsync(cancellationToken);
                if (!await productReader.ReadAsync(cancellationToken))
                {
                    return BadRequest(new ProblemDetails { Title = "Un produit de la commande est introuvable." });
                }

                var price = productReader.GetDecimal(productReader.GetOrdinal("Prix"));
                var isAvailable = productReader.GetBoolean(productReader.GetOrdinal("Disponibilite"));
                var stockQuantity = Convert.ToInt32(productReader["Quantite"]);
                var productType = productReader.GetString(productReader.GetOrdinal("Type_Produit"));
                await productReader.DisposeAsync();
                if (line.Types.Count == 1 && line.Types[0] != productType)
                {
                    return BadRequest(new ProblemDetails { Title = "Le type du produit indiqué ne correspond pas." });
                }

                if (!isAvailable || stockQuantity < line.Quantity)
                {
                    return Conflict(new ProblemDetails
                    {
                        Title = "Un produit de la commande est indisponible ou la quantité demandée dépasse le stock."
                    });
                }

                total += price * line.Quantity;
                if (total > 9999999999.99m)
                {
                    return BadRequest(new ProblemDetails { Title = "Le montant total dépasse la limite autorisée." });
                }

                validatedLines.Add((Convert.ToUInt32(line.IdProduit), (int)line.Quantity, price));
            }

            await using var orderCommand = new MySqlCommand(
                """
                INSERT INTO Commande
                    (Montant_Total, Statut, Mode_Service, Adresse_Livraison, Id_Client, Id_Utilisateur)
                VALUES
                    (@total, 'En attente de paiement', @mode, @address, @clientId, @userId)
                """,
                connection,
                (MySqlTransaction)transaction);
            orderCommand.Parameters.AddWithValue("@total", total);
            orderCommand.Parameters.AddWithValue("@mode", request.ModeService);
            orderCommand.Parameters.AddWithValue("@address", (object?)address ?? DBNull.Value);
            orderCommand.Parameters.AddWithValue("@clientId", clientId);
            orderCommand.Parameters.AddWithValue("@userId", isStaff ? GetUserId() : DBNull.Value);
            await orderCommand.ExecuteNonQueryAsync(cancellationToken);
            var orderId = Convert.ToUInt32(await new MySqlCommand(
                    "SELECT LAST_INSERT_ID()", connection, (MySqlTransaction)transaction)
                .ExecuteScalarAsync(cancellationToken));

            foreach (var line in validatedLines)
            {
                await using var detailCommand = new MySqlCommand(
                    """
                    INSERT INTO Detail_Commande (Id_Commande, Id_Produit, Quantite, Prix_Unitaire)
                    VALUES (@orderId, @productId, @quantity, @price)
                    """,
                    connection,
                    (MySqlTransaction)transaction);
                detailCommand.Parameters.AddWithValue("@orderId", orderId);
                detailCommand.Parameters.AddWithValue("@productId", line.ProductId);
                detailCommand.Parameters.AddWithValue("@quantity", line.Quantity);
                detailCommand.Parameters.AddWithValue("@price", line.Price);
                await detailCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await OperationAudit.WriteAsync(
                connection,
                (MySqlTransaction)transaction,
                isStaff ? GetUserId() : null,
                "Commande créée",
                "Commande",
                orderId.ToString(),
                new { clientId, request.ModeService, total, nombreLignes = validatedLines.Count },
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return CreatedAtAction(
                nameof(GetById),
                new { id = orderId },
                new { id = orderId, montantTotal = total, devise = "HTG", statut = "En attente de paiement" });
        }
        catch (MySqlException exception)
        {
            _logger.LogError(exception, "Unable to create an order.");
            return DatabaseUnavailable();
        }
    }

    [Authorize(Roles = "Administrateur,Caissier")]
    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(
        int id,
        UpdateOrderStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (id <= 0 || !OrderStatuses.Contains(request.Statut))
        {
            return BadRequest(new ProblemDetails { Title = "Identifiant ou statut de commande invalide." });
        }

        try
        {
            await using var connection = DatabaseHelper.GetConnection();
            await connection.OpenAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            string? currentStatus;
            string? serviceMode;
            await using (var select = new MySqlCommand(
                "SELECT Statut, Mode_Service FROM Commande WHERE Id_Commande = @id FOR UPDATE",
                connection,
                (MySqlTransaction)transaction))
            {
                select.Parameters.AddWithValue("@id", id);
                await using var reader = await select.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                {
                    return NotFound();
                }

                currentStatus = reader.GetString(reader.GetOrdinal("Statut"));
                serviceMode = reader.GetString(reader.GetOrdinal("Mode_Service"));
            }

            if (!IsAllowedTransition(currentStatus, request.Statut, serviceMode))
            {
                return Conflict(new ProblemDetails
                {
                    Title = $"Transition de commande non autorisée : {currentStatus} vers {request.Statut}."
                });
            }

            await using var update = new MySqlCommand(
                "UPDATE Commande SET Statut = @status WHERE Id_Commande = @id",
                connection,
                (MySqlTransaction)transaction);
            update.Parameters.AddWithValue("@status", request.Statut);
            update.Parameters.AddWithValue("@id", id);
            await update.ExecuteNonQueryAsync(cancellationToken);
            await OperationAudit.WriteAsync(
                connection,
                (MySqlTransaction)transaction,
                GetUserId(),
                "Statut de commande modifié",
                "Commande",
                id.ToString(),
                new { ancienStatut = currentStatus, nouveauStatut = request.Statut },
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return NoContent();
        }
        catch (MySqlException exception)
        {
            _logger.LogError(exception, "Unable to change status for order {OrderId}.", id);
            return DatabaseUnavailable();
        }
    }

    private bool IsAllowedTransition(string? current, string next, string? serviceMode) =>
        (current, next) switch
        {
            ("Confirmée", "En préparation") => true,
            ("En préparation", "Prête") => true,
            ("Prête", "Retirée au restaurant") => serviceMode == "Retrait",
            ("En préparation", "Annulée") => User.IsInRole("Administrateur"),
            _ => false
        };

    private void AddAccessParameters(MySqlCommand command, bool includeSupervisor = false)
    {
        command.Parameters.AddWithValue(
            "@isStaff",
            IsStaff() || (includeSupervisor && User.IsInRole("Superviseur")));
        command.Parameters.AddWithValue("@userId", GetUserId());
    }

    private bool IsStaff() => User.IsInRole("Administrateur") || User.IsInRole("Caissier");

    private uint GetUserId() =>
        uint.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : 0;

    private static object ReadOrder(System.Data.Common.DbDataReader reader) => new
    {
        id = Convert.ToUInt32(reader["Id_Commande"]),
        date = reader.GetDateTime(reader.GetOrdinal("Date_Commande")),
        montantTotal = reader.GetDecimal(reader.GetOrdinal("Montant_Total")),
        devise = reader.GetString(reader.GetOrdinal("Devise")).Trim(),
        statut = reader.GetString(reader.GetOrdinal("Statut")),
        modeService = reader.GetString(reader.GetOrdinal("Mode_Service")),
        adresseLivraison = reader.IsDBNull(reader.GetOrdinal("Adresse_Livraison"))
            ? null
            : reader.GetString(reader.GetOrdinal("Adresse_Livraison")),
        idClient = Convert.ToUInt32(reader["Id_Client"])
    };

    private ObjectResult DatabaseUnavailable() => Problem(
        title: "Le service des commandes est temporairement indisponible.",
        statusCode: StatusCodes.Status503ServiceUnavailable);
}
