namespace Project_Malawi_Resto.Models;

public sealed class DetailCommande
{
    public int IdDetail { get; set; }
    public int IdCommande { get; set; }
    public int IdProduit { get; set; }
    public int Quantite { get; set; }
    public decimal PrixUnitaire { get; set; }
    public decimal MontantLigne { get; set; }
    public Produit? Produit { get; set; }
}
