using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
                                                                                                                                                        
namespace Project_Malawi_Resto.Models
{
    public class Commande
    {
        #region "Objets de Connexion à la base de données"
        public static MySqlConnection mscon = null;
        public static MySqlCommand cmd = null;
        public static MySqlDataReader rd = null;
        #endregion

        #region "Attributs de la Commande"
        private int idCommande;
        private string dateCommande;
        private decimal montantTotal;
        private string statut;

        // Cles etrangeres
        private int idClient;
        private int idUtilisateur;
        // Proprietes de navigation (objets lies) 
        private Client client;
        private Utilisateur utilisateur;
        private List<DetailCommande> details;
        #endregion

        #region "Constructeurs"
        public Commande()
        {
            this.idCommande = 0;
            this.dateCommande = null;
            this.montantTotal = 0;
            this.statut = "En attente";
            this.details = new List<DetailCommande>();
        }

        public Commande(int idCommande, string dateCommande, decimal montantTotal, string statut, int idClient, int idUtilisateur)
        {
            this.idCommande = idCommande;
            this.dateCommande = dateCommande;
            this.montantTotal = montantTotal;
            this.statut = statut;
            this.idClient = idClient;
            this.idUtilisateur = idUtilisateur;
            this.details = new List<DetailCommande>();
        }
        #endregion

        #region "Proprietes"
        public int IdCommande
        {
            get { return idCommande; }
            set { idCommande = value; }
        }
        public string DateCommande
        {
            get { return dateCommande; }
            set { dateCommande = value; }
        }
        public decimal MontantTotal
        {
            get { return montantTotal; }
            set { montantTotal = value; }
        }
        public string Statut
        {
            get { return statut; }
            set { statut = value; }
        }
        public int IdClient
        {
            get { return idClient; }
            set { idClient = value; }
        }
        public int IdUtilisateur
        {
            get { return idUtilisateur; }
            set { idUtilisateur = value; }
        }
        public Client Client
        {
            get { return client; }
            set { client = value; }
        }
        public Utilisateur Utilisateur
        {
            get { return utilisateur; }
            set { utilisateur = value; }
        }
        public List<DetailCommande> Details
        {
            get { return details; }
            set { details = value; }
        }
        #endregion

        #region "Méthodes de gestion de commandes"

        #region "Insertion d'une commande"
        public static string InsererCommande(Commande commande)
        {
            string message = "";
            string requete = "INSERT INTO Commande (Date_Commande, Montant_Total, Statut)" + "VALUES(@date_commande, @montant_total, @statut)";

            try
            {
                using (mscon = DatabaseHelper.GetConnection())
                {
                    mscon.Open();
                    cmd = new MySqlCommand(requete, mscon);

                    cmd.Parameters.AddWithValue("@date_commande", commande.DateCommande);
                    cmd.Parameters.AddWithValue("@montant_total", commande.MontantTotal);
                    cmd.Parameters.AddWithValue("@statut", commande.Statut);

                    if (cmd.ExecuteNonQuery() > 0)
                    {
                        message = " Commande ajoutée avec succès!";
                    }
                }
            }
            catch (MySqlException ex)
            {
                message = "Erreur lors de l'insertion:" + ex.Message;
            }

            return message;
        }
        #endregion

        #region "Modification d'une commande"
        public static string ModifierCommande(Commande commande)
        {
            string message = "";
            string requete = "UPDATE Commande SET Date_Commandde=@date_commande, Montant_Total=@montant_total," + "Statut=@statut WHERE Id_Commande=@idCommande";

            try
            {
                using (mscon = DatabaseHelper.GetConnection())
                {
                    mscon.Open();
                    cmd = new MySqlCommand(requete, mscon);

                    cmd.Parameters.AddWithValue("@date_commande", commande.DateCommande);
                    cmd.Parameters.AddWithValue("@montant_total", commande.MontantTotal);
                    cmd.Parameters.AddWithValue("@statut", commande.Statut);
                    cmd.Parameters.AddWithValue(@"idCommande", commande.IdCommande);

                    if (cmd.ExecuteNonQuery() > 0) 
                    {
                        message = "Commande modifiée avec succès!";
                    }
                    else
                    {
                        message = " Aucune commande trouvée avec cet ID.";
                    }
                }
            }
            catch (MySqlException ex)
            {
                message = "Erreur de modification: " +ex.Message;
            }
              
           return message;
        }

        #endregion

        #region "Validation d'une commande"

        private const string statut_en_attente = "En attente";
        private const string statut_validee = "Validée";

        public static string ValiderCommande(Commande commande)
        {
            string message = "";
            string requeteSelect = "SELECT Statut, Montant_Total FROM Commande WHERE Id_Commande = @idCommande";
            string requeteUpdate = "UPDATE Commande SET Statut = @nouveauStatut " +
                                   "WHERE Id_Commande = @idCommande AND Statut = @ancienStatut";

            try
            {
                using (MySqlConnection con = DatabaseHelper.GetConnection())
                {
                    con.Open();

                    string statutActuel = null;
                    decimal montant = 0;
                    bool existe = false;

                    // Vérifier que la commande existe et lire son état
                    using (MySqlCommand cmdSelect = new MySqlCommand(requeteSelect, con))
                    {
                        cmdSelect.Parameters.AddWithValue("@idCommande", commande.IdCommande);

                        using (MySqlDataReader rd = cmdSelect.ExecuteReader())
                        {
                            if (rd.Read())
                            {
                                existe = true;
                                statutActuel = rd["Statut"] != DBNull.Value ? rd["Statut"].ToString() : null;
                                montant = rd["Montant_Total"] != DBNull.Value ? Convert.ToDecimal(rd["Montant_Total"]) : 0;
                            }
                        }
                    }

                    if (!existe)
                        return "Aucune commande trouvée avec cet ID.";

                    // Règles de validation
                    if (statutActuel == statut_validee)
                        return "Cette commande est déjà validée.";

                    if (statutActuel != statut_en_attente)
                        return "Impossible de valider une commande dont le statut est : " + statutActuel;

                    if (montant <= 0)
                        return "Impossible de valider une commande dont le montant est nul.";

                    // Mise à jour du statut
                    using (MySqlCommand cmdUpdate = new MySqlCommand(requeteUpdate, con))
                    {
                        cmdUpdate.Parameters.AddWithValue("@nouveauStatut", statut_validee);
                        cmdUpdate.Parameters.AddWithValue("@idCommande", commande.IdCommande);
                        cmdUpdate.Parameters.AddWithValue("@ancienStatut", statut_en_attente);

                        if (cmdUpdate.ExecuteNonQuery() > 0)
                            message = "Commande validée avec succès!";
                        else
                            message = "La commande n'a pas pu être validée (son statut a peut-être changé).";
                    }
                }
            }
            catch (Exception ex)
            {
                message = "Erreur de validation : " + ex.Message;
            }

            return message;
        }

        #endregion

        #region "Suppression d'une commande"
        public static string SupprimerCommande(int idCommande) 
        {
            string message = "";
            string requete = "DELETE FROM Commande WHERE Id_Commande = @idCommande";

            try
            {
                using (mscon = DatabaseHelper.GetConnection())
                {
                    mscon.Open();
                    cmd = new MySqlCommand(requete, mscon);
                    cmd.Parameters.AddWithValue("idCommande", idCommande);

                    if (cmd.ExecuteNonQuery() > 0)
                    {
                        message = "Commande supprimée avec succès!";
                    }

                }
            }
            catch (MySqlException ex) { }






            return message;
        }



        #endregion

        #region "Liste des commandes"

        public static List<Commande> ListerToutesCommandes() 
        { 

            List<Commande> listCommandes = new List<Commande>();
            string requete = "SELECT * FROM Commande ORDER BY Date_Commande, Statut";
            try 
            {
                using (mscon = DatabaseHelper.GetConnection()) 
                {
                    mscon.Open();
                    cmd = new MySqlCommand(requete,mscon);

                    using (rd =cmd.ExecuteReader()) 
                    {
                        while (rd.Read())
                        { 
                            Commande c = new Commande();
                            c.IdCommande = Convert.ToInt32(rd["Id_Commande"]);
                            c.DateCommande = rd["Date_Commande"].ToString();
                            c.MontantTotal = Convert.ToDecimal(rd["Montant_Total"]);
                            c.Statut = rd["Statut"] != DBNull.Value ? rd["Statut"].ToString() : null;

                            listCommandes.Add(c);
                        }
                    
                    }

                }
            }

            catch (MySqlException ex) 
            {
                Console.WriteLine("Erreur listage" +ex.Message);
            }

            return listCommandes;

        }
        #endregion



        #endregion
    }
}
