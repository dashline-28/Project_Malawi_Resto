using System.ComponentModel.DataAnnotations;

namespace Project_Malawi_Resto.Models;

public sealed record RegisterClientRequest(
    [param: Required, StringLength(100, MinimumLength = 1)] string Nom,
    [param: Required, StringLength(100, MinimumLength = 1)] string Prenom,
    [param: Required, EmailAddress, StringLength(254)] string Email,
    [param: Required, StringLength(128, MinimumLength = 8)] string MotDePasse,
    [param: StringLength(30)] string? Telephone,
    [param: StringLength(500)] string? Adresse);

public sealed record LoginRequest(
    [param: Required, EmailAddress, StringLength(254)] string Email,
    [param: Required, StringLength(128)] string MotDePasse);

public sealed record BootstrapAdminRequest(
    [param: Required, StringLength(100, MinimumLength = 1)] string Nom,
    [param: Required, StringLength(100, MinimumLength = 1)] string Prenom,
    [param: Required, EmailAddress, StringLength(254)] string Email,
    [param: Required, StringLength(128, MinimumLength = 8)] string MotDePasse,
    [param: Required, StringLength(256, MinimumLength = 32)] string CleConfiguration);

public sealed record ChangePasswordRequest(
    [param: Required, StringLength(128)] string MotDePasseActuel,
    [param: Required, StringLength(128, MinimumLength = 8)] string NouveauMotDePasse);

public sealed record CreateUserRequest(
    [param: Required, StringLength(100, MinimumLength = 1)] string Nom,
    [param: Required, StringLength(100, MinimumLength = 1)] string Prenom,
    [param: Required, EmailAddress, StringLength(254)] string Email,
    [param: Required, StringLength(128, MinimumLength = 8)] string MotDePasse,
    [param: Required, StringLength(30)] string Role);

public sealed record UpdateUserRequest(
    [param: Required, StringLength(100, MinimumLength = 1)] string Nom,
    [param: Required, StringLength(100, MinimumLength = 1)] string Prenom,
    [param: Required, StringLength(30)] string Role,
    bool Actif);

public sealed record UpdatePasswordRequest(
    [param: Required, StringLength(128, MinimumLength = 8)] string NouveauMotDePasse);

public sealed record CreateClientRequest(
    [param: Required, StringLength(100, MinimumLength = 1)] string Nom,
    [param: Required, StringLength(100, MinimumLength = 1)] string Prenom,
    [param: StringLength(30)] string? Telephone,
    [param: StringLength(500)] string? Adresse,
    [EmailAddress, StringLength(254)] string? Email);

public sealed record UpdateClientRequest(
    [param: Required, StringLength(100, MinimumLength = 1)] string Nom,
    [param: Required, StringLength(100, MinimumLength = 1)] string Prenom,
    [StringLength(30)] string? Telephone,
    [StringLength(500)] string? Adresse,
    [EmailAddress, StringLength(254)] string? Email);

public sealed record CategoryRequest(
    [param: Required, StringLength(100, MinimumLength = 1)] string Nom,
    bool Active);

public sealed record ProductRequest(
    [param: Required, StringLength(150, MinimumLength = 1)] string Nom,
    [Range(0, int.MaxValue)] int Quantite,
    [StringLength(4000)] string? Description,
    [Range(typeof(decimal), "0", "9999999999.99")] decimal Prix,
    [param: Required, StringLength(100, MinimumLength = 1)] string Categorie,
    [param: Required, StringLength(20)] string TypeProduit,
    bool Disponibilite);

public sealed record CreateOrderRequest(
    [param: Required] string ModeService,
    [StringLength(500)] string? AdresseLivraison,
    [Range(1, int.MaxValue)] int? IdClient,
    [param: Required, MinLength(1)] IReadOnlyList<OrderLineRequest> Lignes);

public sealed record OrderLineRequest(
    [Range(1, 1000)] int IdProduit,
    [Range(1, 1000)] int Quantite,
    string? TypeProduit = null);

public sealed record UpdateOrderStatusRequest(
    [param: Required, StringLength(40)] string Statut);

public sealed record CreatePaymentRequest(
    [Range(1, int.MaxValue)] int IdCommande,
    [Required, StringLength(20)] string ModePaiement);

public sealed record AssignDeliveryRequest(
    [Range(1, int.MaxValue)] int IdCommande,
    [Range(1, int.MaxValue)] int IdLivreur);

public sealed record UpdateDeliveryStatusRequest(
    [Required, StringLength(20)] string Statut);

public sealed record RecordRefundRequest(
    [Range(1, int.MaxValue)] int IdPaiement,
    [Range(typeof(decimal), "0.01", "9999999999.99")] decimal Montant,
    [param: Required, StringLength(191, MinimumLength = 1)] string ReferenceExterne);

public sealed record AuthenticatedUserResponse(
    uint Id,
    string Nom,
    string Prenom,
    string Email,
    string Role);
