using System.Data;
using MySql.Data.MySqlClient;

namespace Project_Malawi_Resto.Models;

public sealed class Plat : Produit
{
    public Plat()
    {
        TypeProduit = "Plat";
    }

    public Plat(
        string nomPlat,
        int quantite,
        string? description,
        decimal prix,
        string categorie,
        bool disponibilite = true)
        : this()
    {
        NomPlat = nomPlat;
        Quantite = quantite;
        Description = description;
        Prix = prix;
        Categorie = categorie;
        Disponibilite = disponibilite;
    }

    public int IdPlat
    {
        get => IdProduit;
        set => IdProduit = value;
    }

    public string NomPlat
    {
        get => Nom;
        set => Nom = value;
    }

    public static string InsererPlat(Plat plat) =>
        Execute(() => ProduitRepository.Insert(plat), "Plat ajouté avec succès!", "Le plat n'a pas été ajouté.");

    public static Plat? RechercherPlat(int idPlat) => FromProduct(ProduitRepository.Find(idPlat, "Plat"));

    public static string ModifierPlat(Plat plat) =>
        Execute(() => ProduitRepository.Update(plat, "Plat"), "Plat modifié avec succès!", "Aucun plat trouvé avec cet ID.");

    public static string SupprimerPlat(int idPlat) =>
        Execute(() => ProduitRepository.Deactivate(idPlat, "Plat"), "Plat désactivé avec succès!", "Aucun plat trouvé avec cet ID.");

    public static List<Plat> ListerPlats() =>
        ProduitRepository.List("Plat").Select(product => FromProduct(product)!).ToList();

    public static string ModifierQuantite(int idPlat, int quantite) =>
        Execute(() => ProduitRepository.SetQuantity(idPlat, quantite, "Plat"),
            "Quantité modifiée avec succès!", "Aucun plat trouvé avec cet ID.");

    public static DataTable GetPlatPlusVendus(DateTime dateDebut, DateTime dateFin, int limit = 10) =>
        ProduitRepository.TopSellers(dateDebut, dateFin, limit, "Plat", "IdPlat", "NomPlat");

    private static Plat? FromProduct(Produit? product)
    {
        if (product is null)
        {
            return null;
        }

        return new Plat(product.Nom, product.Quantite, product.Description, product.Prix,
            product.Categorie, product.Disponibilite)
        {
            IdProduit = product.IdProduit
        };
    }

    private static string Execute(Func<bool> operation, string success, string notFound)
    {
        try
        {
            return operation() ? success : notFound;
        }
        catch (MySqlException exception)
        {
            return $"Erreur produit: {exception.Message}";
        }
    }
}
