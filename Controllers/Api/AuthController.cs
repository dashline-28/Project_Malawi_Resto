using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using Project_Malawi_Resto.Models;

namespace Project_Malawi_Resto.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
public sealed class AuthController : ControllerBase
{
    private readonly ILogger<AuthController> _logger;
    private readonly IPasswordHasher<AccountPassword> _passwordHasher;
    private readonly IConfiguration _configuration;

    public AuthController(
        ILogger<AuthController> logger,
        IPasswordHasher<AccountPassword> passwordHasher,
        IConfiguration configuration)
    {
        _logger = logger;
        _passwordHasher = passwordHasher;
        _configuration = configuration;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(
        RegisterClientRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var account = new AccountPassword(email);
        var passwordHash = _passwordHasher.HashPassword(account, request.MotDePasse);

        try
        {
            await using var connection = DatabaseHelper.GetConnection();
            await connection.OpenAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            await using (var userCommand = new MySqlCommand(
                """
                INSERT INTO Utilisateur (nom, prenom, email, mot_de_passe, role)
                VALUES (@nom, @prenom, @email, @password, 'Client')
                """,
                connection,
                (MySqlTransaction)transaction))
            {
                userCommand.Parameters.AddWithValue("@nom", request.Nom.Trim());
                userCommand.Parameters.AddWithValue("@prenom", request.Prenom.Trim());
                userCommand.Parameters.AddWithValue("@email", email);
                userCommand.Parameters.AddWithValue("@password", passwordHash);
                await userCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            var userId = Convert.ToUInt32(
                await new MySqlCommand("SELECT LAST_INSERT_ID()", connection, (MySqlTransaction)transaction)
                    .ExecuteScalarAsync(cancellationToken));

            await using (var clientCommand = new MySqlCommand(
                """
                INSERT INTO Client (nomClient, prenomClient, telephone, adresse, email, Id_Utilisateur)
                VALUES (@nom, @prenom, @telephone, @adresse, @email, @userId)
                """,
                connection,
                (MySqlTransaction)transaction))
            {
                clientCommand.Parameters.AddWithValue("@nom", request.Nom.Trim());
                clientCommand.Parameters.AddWithValue("@prenom", request.Prenom.Trim());
                clientCommand.Parameters.AddWithValue("@telephone", (object?)request.Telephone?.Trim() ?? DBNull.Value);
                clientCommand.Parameters.AddWithValue("@adresse", (object?)request.Adresse?.Trim() ?? DBNull.Value);
                clientCommand.Parameters.AddWithValue("@email", email);
                clientCommand.Parameters.AddWithValue("@userId", userId);
                await clientCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return StatusCode(StatusCodes.Status201Created, new { id = userId, email });
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
            _logger.LogError(exception, "Client registration failed.");
            return Problem(
                title: "La création du compte est temporairement indisponible.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    [HttpPost("bootstrap-admin")]
    public async Task<IActionResult> BootstrapAdmin(
        BootstrapAdminRequest request,
        CancellationToken cancellationToken)
    {
        var setupKey = _configuration["Bootstrap:AdminSetupKey"];
        if (string.IsNullOrWhiteSpace(setupKey) || setupKey.Length < 32)
        {
            return NotFound();
        }

        var expectedKey = Encoding.UTF8.GetBytes(setupKey);
        var providedKey = Encoding.UTF8.GetBytes(request.CleConfiguration);
        if (expectedKey.Length != providedKey.Length ||
            !CryptographicOperations.FixedTimeEquals(expectedKey, providedKey))
        {
            return Unauthorized();
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var account = new AccountPassword(email);
        try
        {
            await using var connection = DatabaseHelper.GetConnection();
            await connection.OpenAsync(cancellationToken);

            await using (var lockCommand = new MySqlCommand(
                "SELECT GET_LOCK('malawi:first-admin-bootstrap', 10)",
                connection))
            {
                if (Convert.ToInt32(await lockCommand.ExecuteScalarAsync(cancellationToken)) != 1)
                {
                    return Conflict(new ProblemDetails
                    {
                        Title = "L'initialisation administrateur est déjà en cours."
                    });
                }
            }

            try
            {
                await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
                await using var existingAdmin = new MySqlCommand(
                    "SELECT COUNT(*) FROM Utilisateur WHERE role = 'Administrateur'",
                    connection,
                    (MySqlTransaction)transaction);
                if (Convert.ToInt32(await existingAdmin.ExecuteScalarAsync(cancellationToken)) != 0)
                {
                    return Conflict(new ProblemDetails
                    {
                        Title = "Un administrateur existe déjà. Utilisez la connexion normale."
                    });
                }

                await using var createAdmin = new MySqlCommand(
                    """
                    INSERT INTO Utilisateur (nom, prenom, email, mot_de_passe, role)
                    SELECT @nom, @prenom, @email, @password, Nom_Role
                    FROM Role
                    WHERE Nom_Role = 'Administrateur'
                    """,
                    connection,
                    (MySqlTransaction)transaction);
                createAdmin.Parameters.AddWithValue("@nom", request.Nom.Trim());
                createAdmin.Parameters.AddWithValue("@prenom", request.Prenom.Trim());
                createAdmin.Parameters.AddWithValue("@email", email);
                createAdmin.Parameters.AddWithValue(
                    "@password",
                    _passwordHasher.HashPassword(account, request.MotDePasse));
                if (await createAdmin.ExecuteNonQueryAsync(cancellationToken) == 0)
                {
                    return Conflict(new ProblemDetails
                    {
                        Title = "Le rôle Administrateur manque dans la base. Exécutez les scripts d'initialisation."
                    });
                }

                await transaction.CommitAsync(cancellationToken);
                return StatusCode(StatusCodes.Status201Created, new { email });
            }
            finally
            {
                await using var releaseLock = new MySqlCommand(
                    "SELECT RELEASE_LOCK('malawi:first-admin-bootstrap')",
                    connection);
                await releaseLock.ExecuteScalarAsync(cancellationToken);
            }
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
            _logger.LogError(exception, "Initial administrator setup failed.");
            return Problem(
                title: "L'initialisation du compte administrateur est temporairement indisponible.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = DatabaseHelper.GetConnection();
            await connection.OpenAsync(cancellationToken);
            await using var command = new MySqlCommand(
                """
                SELECT idUtilisateur, nom, prenom, email, mot_de_passe, role
                FROM Utilisateur
                WHERE email = @email AND actif = TRUE
                LIMIT 1
                """,
                connection);
            command.Parameters.AddWithValue("@email", request.Email.Trim().ToLowerInvariant());
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
            {
                return Unauthorized(new ProblemDetails
                {
                    Title = "Adresse électronique ou mot de passe incorrect.",
                    Status = StatusCodes.Status401Unauthorized
                });
            }

            var userId = Convert.ToUInt32(reader["idUtilisateur"]);
            var firstName = reader.GetString(reader.GetOrdinal("nom"));
            var lastName = reader.GetString(reader.GetOrdinal("prenom"));
            var email = reader.GetString(reader.GetOrdinal("email"));
            var storedHash = reader.GetString(reader.GetOrdinal("mot_de_passe"));
            var role = reader.GetString(reader.GetOrdinal("role"));
            var account = new AccountPassword(email);
            var verification = _passwordHasher.VerifyHashedPassword(account, storedHash, request.MotDePasse);

            if (verification == PasswordVerificationResult.Failed)
            {
                return Unauthorized(new ProblemDetails
                {
                    Title = "Adresse électronique ou mot de passe incorrect.",
                    Status = StatusCodes.Status401Unauthorized
                });
            }

            await reader.DisposeAsync();
            if (verification == PasswordVerificationResult.SuccessRehashNeeded)
            {
                await using var updateHash = new MySqlCommand(
                    "UPDATE Utilisateur SET mot_de_passe = @password WHERE idUtilisateur = @id",
                    connection);
                updateHash.Parameters.AddWithValue("@password", _passwordHasher.HashPassword(account, request.MotDePasse));
                updateHash.Parameters.AddWithValue("@id", userId);
                await updateHash.ExecuteNonQueryAsync(cancellationToken);
            }

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name, $"{firstName} {lastName}"),
                new Claim(ClaimTypes.GivenName, firstName),
                new Claim(ClaimTypes.Surname, lastName),
                new Claim(ClaimTypes.Email, email),
                new Claim(ClaimTypes.Role, role)
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity));

            return Ok(new AuthenticatedUserResponse(userId, firstName, lastName, email, role));
        }
        catch (MySqlException exception)
        {
            _logger.LogError(exception, "User login failed because the account store is unavailable.");
            return Problem(
                title: "La connexion est temporairement indisponible.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        if (!uint.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
        {
            return Unauthorized();
        }

        return Ok(new AuthenticatedUserResponse(
            id,
            User.FindFirstValue(ClaimTypes.GivenName) ?? string.Empty,
            User.FindFirstValue(ClaimTypes.Surname) ?? string.Empty,
            User.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
            User.FindFirstValue(ClaimTypes.Role) ?? string.Empty));
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (!uint.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Unauthorized();
        }

        try
        {
            await using var connection = DatabaseHelper.GetConnection();
            await connection.OpenAsync(cancellationToken);
            await using var command = new MySqlCommand(
                "SELECT email, mot_de_passe FROM Utilisateur WHERE idUtilisateur = @id AND actif = TRUE",
                connection);
            command.Parameters.AddWithValue("@id", userId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return Unauthorized();
            }

            var account = new AccountPassword(reader.GetString(reader.GetOrdinal("email")));
            var storedHash = reader.GetString(reader.GetOrdinal("mot_de_passe"));
            var verification = _passwordHasher.VerifyHashedPassword(
                account,
                storedHash,
                request.MotDePasseActuel);
            if (verification == PasswordVerificationResult.Failed)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Le mot de passe actuel est incorrect.",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            await reader.DisposeAsync();
            await using var update = new MySqlCommand(
                "UPDATE Utilisateur SET mot_de_passe = @password WHERE idUtilisateur = @id",
                connection);
            update.Parameters.AddWithValue(
                "@password",
                _passwordHasher.HashPassword(account, request.NouveauMotDePasse));
            update.Parameters.AddWithValue("@id", userId);
            await update.ExecuteNonQueryAsync(cancellationToken);
            return NoContent();
        }
        catch (MySqlException exception)
        {
            _logger.LogError(exception, "Unable to change the password for user {UserId}.", userId);
            return Problem(
                title: "Le changement de mot de passe est temporairement indisponible.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}
