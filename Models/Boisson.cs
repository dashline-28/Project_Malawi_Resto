using System.Data;
using MySql.Data.MySqlClient;

namespace Project_Malawi_Resto.Models;

public sealed class Boisson : Produit
{
    public Boisson()
    {
        TypeProduit = "Boisson";
    }

    public Boisson(
        string nomBoisson,
        int quantite,
        string? description,
        decimal prix,
        string typeBoisson,
        bool disponibilite = true)
        : this()
    {
        NomBoisson = nomBoisson;
        Quantite = quantite;
        Description = description;
        Prix = prix;
        TypeBoisson = typeBoisson;
        Disponibilite = disponibilite;
    }

    public int IdBoisson
    {
        get => IdProduit;
        set => IdProduit = value;
    }

    public string NomBoisson
    {
        get => Nom;
        set => Nom = value;
    }

    public string TypeBoisson
    {
        get => Categorie;
        set => Categorie = value;
    }

    public static string InsererBoisson(Boisson boisson) =>
        Execute(() => ProduitRepository.Insert(boisson), "Boisson ajoutée avec succès!", "La boisson n'a pas été ajoutée.");

    public static Boisson? RechercherBoisson(int idBoisson) => FromProduct(ProduitRepository.Find(idBoisson, "Boisson"));

    public static string ModifierBoisson(Boisson boisson) =>
        Execute(() => ProduitRepository.Update(boisson, "Boisson"),
            "Boisson modifiée avec succès!", "Aucune boisson trouvée avec cet ID.");

    public static string SupprimerBoisson(int idBoisson) =>
        Execute(() => ProduitRepository.Deactivate(idBoisson, "Boisson"),
            "Boisson désactivée avec succès!", "Aucune boisson trouvée avec cet ID.");

    public static List<Boisson> ListerToutesBoissons() =>
        ProduitRepository.List("Boisson").Select(product => FromProduct(product)!).ToList();

    public static List<Boisson> ListerBoissonsDisponibles() =>
        ProduitRepository.List("Boisson", availableOnly: true).Select(product => FromProduct(product)!).ToList();

    public static string ModifierQuantite(int idBoisson, int quantite) =>
        Execute(() => ProduitRepository.SetQuantity(idBoisson, quantite, "Boisson"),
            "Quantité modifiée avec succès!", "Aucune boisson trouvée avec cet ID.");

    public static DataTable GetBoissonsPlusVendues(DateTime dateDebut, DateTime dateFin, int limit = 10) =>
        ProduitRepository.TopSellers(dateDebut, dateFin, limit, "Boisson", "Id_Boisson", "Nom_Boisson");

    private static Boisson? FromProduct(Produit? product)
    {
        if (product is null)
        {
            return null;
        }

        return new Boisson(product.Nom, product.Quantite, product.Description, product.Prix,
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
