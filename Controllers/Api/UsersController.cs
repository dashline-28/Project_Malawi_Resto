using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using Project_Malawi_Resto.Models;

namespace Project_Malawi_Resto.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Administrateur")]
public sealed class UsersController : ControllerBase
{
    private readonly ILogger<UsersController> _logger;
    private readonly IPasswordHasher<AccountPassword> _passwordHasher;

    public UsersController(
        ILogger<UsersController> logger,
        IPasswordHasher<AccountPassword> passwordHasher)
    {
        _logger = logger;
        _passwordHasher = passwordHasher;
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
                SELECT idUtilisateur AS Id, nom AS Nom, prenom AS Prenom,
                       email AS Email, role AS Role, actif AS Actif,
                       Date_Creation AS DateCreation
                FROM Utilisateur
                WHERE role <> 'Client'
                ORDER BY nom, prenom
                """,
                connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var users = new List<object>();
            while (await reader.ReadAsync(cancellationToken))
            {
                users.Add(new
                {
                    id = Convert.ToUInt32(reader["Id"]),
                    nom = reader.GetString(reader.GetOrdinal("Nom")),
                    prenom = reader.GetString(reader.GetOrdinal("Prenom")),
                    email = reader.GetString(reader.GetOrdinal("Email")),
                    role = reader.GetString(reader.GetOrdinal("Role")),
                    actif = reader.GetBoolean(reader.GetOrdinal("Actif")),
                    dateCreation = reader.GetDateTime(reader.GetOrdinal("DateCreation"))
                });
            }

            return Ok(users);
        }
        catch (MySqlException exception)
        {
            _logger.LogError(exception, "Unable to list user accounts.");
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
                SELECT idUtilisateur AS Id, nom AS Nom, prenom AS Prenom,
                       email AS Email, role AS Role, actif AS Actif,
                       Date_Creation AS DateCreation
                FROM Utilisateur
                WHERE idUtilisateur = @id
                """,
                connection);
            command.Parameters.AddWithValue("@id", id);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return NotFound();
            }

            return Ok(new
            {
                id = Convert.ToUInt32(reader["Id"]),
                nom = reader.GetString(reader.GetOrdinal("Nom")),
                prenom = reader.GetString(reader.GetOrdinal("Prenom")),
                email = reader.GetString(reader.GetOrdinal("Email")),
                role = reader.GetString(reader.GetOrdinal("Role")),
                actif = reader.GetBoolean(reader.GetOrdinal("Actif")),
                dateCreation = reader.GetDateTime(reader.GetOrdinal("DateCreation"))
            });
        }
        catch (MySqlException exception)
        {
            _logger.LogError(exception, "Unable to retrieve user account {UserId}.", id);
            return DatabaseUnavailable();
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        if (string.Equals(request.Role.Trim(), "Client", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Les comptes client doivent être créés avec /api/auth/register afin de créer également leur profil client."
            });
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var account = new AccountPassword(email);

        try
        {
            await using var connection = DatabaseHelper.GetConnection();
            await connection.OpenAsync(cancellationToken);
            await using (var roleCheck = new MySqlCommand(
                "SELECT COUNT(*) FROM Role WHERE Nom_Role = @role",
                connection))
            {
                roleCheck.Parameters.AddWithValue("@role", request.Role.Trim());
                if (Convert.ToInt32(await roleCheck.ExecuteScalarAsync(cancellationToken)) == 0)
                {
                    return BadRequest(new ProblemDetails { Title = "Le rôle demandé n'existe pas." });
                }
            }

            await using var command = new MySqlCommand(
                """
                INSERT INTO Utilisateur (nom, prenom, email, mot_de_passe, role)
                SELECT @nom, @prenom, @email, @password, Nom_Role
                FROM Role
                WHERE Nom_Role = @role
                """,
                connection);
            command.Parameters.AddWithValue("@nom", request.Nom.Trim());
            command.Parameters.AddWithValue("@prenom", request.Prenom.Trim());
            command.Parameters.AddWithValue("@email", email);
            command.Parameters.AddWithValue("@password", _passwordHasher.HashPassword(account, request.MotDePasse));
            command.Parameters.AddWithValue("@role", request.Role.Trim());

            if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Le rôle demandé n'existe pas.",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            var id = Convert.ToUInt32(await new MySqlCommand("SELECT LAST_INSERT_ID()", connection)
                .ExecuteScalarAsync(cancellationToken));
            return CreatedAtAction(nameof(GetById), new { id }, new { id, email });
        }
        catch (MySqlException exception) when (exception.Number == 1062)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Un compte utilise déjà cette adresse électronique.",
                Status = StatusCodes.Status409Conflict
            });
        }
        catch (MySqlException exception)
        {
            _logger.LogError(exception, "Unable to create a user account.");
            return DatabaseUnavailable();
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        uint id,
        UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        if (string.Equals(request.Role.Trim(), "Client", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Le rôle Client est réservé aux comptes créés via l'inscription client."
            });
        }

        if (!uint.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var currentId))
        {
            return Unauthorized();
        }

        if (id == currentId &&
            (!request.Actif || !string.Equals(request.Role, "Administrateur", StringComparison.Ordinal)))
        {
            return Conflict(new ProblemDetails
            {
                Title = "Vous ne pouvez pas désactiver votre propre compte ni retirer votre rôle d'administrateur.",
                Status = StatusCodes.Status409Conflict
            });
        }

        try
        {
            await using var connection = DatabaseHelper.GetConnection();
            await connection.OpenAsync(cancellationToken);
            await using var command = new MySqlCommand(
                """
                UPDATE Utilisateur u
                INNER JOIN Role r ON r.Nom_Role = @role
                SET u.nom = @nom, u.prenom = @prenom, u.role = r.Nom_Role, u.actif = @actif
                WHERE u.idUtilisateur = @id
                """,
                connection);
            command.Parameters.AddWithValue("@id", id);
            command.Parameters.AddWithValue("@nom", request.Nom.Trim());
            command.Parameters.AddWithValue("@prenom", request.Prenom.Trim());
            command.Parameters.AddWithValue("@role", request.Role.Trim());
            command.Parameters.AddWithValue("@actif", request.Actif);
            if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
            {
                return NotFound();
            }

            return NoContent();
        }
        catch (MySqlException exception)
        {
            _logger.LogError(exception, "Unable to update user account {UserId}.", id);
            return DatabaseUnavailable();
        }
    }

    [HttpPut("{id:int}/password")]
    public async Task<IActionResult> ChangePassword(
        uint id,
        UpdatePasswordRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = DatabaseHelper.GetConnection();
            await connection.OpenAsync(cancellationToken);
            await using var command = new MySqlCommand(
                "SELECT email FROM Utilisateur WHERE idUtilisateur = @id",
                connection);
            command.Parameters.AddWithValue("@id", id);
            var email = await command.ExecuteScalarAsync(cancellationToken) as string;
            if (email is null)
            {
                return NotFound();
            }

            var account = new AccountPassword(email);
            await using var updateCommand = new MySqlCommand(
                "UPDATE Utilisateur SET mot_de_passe = @password WHERE idUtilisateur = @id",
                connection);
            updateCommand.Parameters.AddWithValue("@id", id);
            updateCommand.Parameters.AddWithValue("@password", _passwordHasher.HashPassword(account, request.NouveauMotDePasse));
            await updateCommand.ExecuteNonQueryAsync(cancellationToken);
            return NoContent();
        }
        catch (MySqlException exception)
        {
            _logger.LogError(exception, "Unable to change password for user {UserId}.", id);
            return DatabaseUnavailable();
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Deactivate(uint id, CancellationToken cancellationToken)
    {
        if (uint.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var currentId) &&
            id == currentId)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Vous ne pouvez pas désactiver votre propre compte.",
                Status = StatusCodes.Status409Conflict
            });
        }

        try
        {
            await using var connection = DatabaseHelper.GetConnection();
            await connection.OpenAsync(cancellationToken);
            await using var command = new MySqlCommand(
                "UPDATE Utilisateur SET actif = FALSE WHERE idUtilisateur = @id AND actif = TRUE",
                connection);
            command.Parameters.AddWithValue("@id", id);
            return await command.ExecuteNonQueryAsync(cancellationToken) == 0 ? NotFound() : NoContent();
        }
        catch (MySqlException exception)
        {
            _logger.LogError(exception, "Unable to deactivate user {UserId}.", id);
            return DatabaseUnavailable();
        }
    }

    private ObjectResult DatabaseUnavailable() => Problem(
        title: "Le service des comptes est temporairement indisponible.",
        statusCode: StatusCodes.Status503ServiceUnavailable);
}
