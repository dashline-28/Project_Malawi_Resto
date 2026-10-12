using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;


namespace Project_Malawi_Resto.Models
{
    public class Paiement
    {
        #region "Attributs du Paiement"
        private int idPaiement;
        private string datePaiement;
        private decimal montant;
        private string modePaiement;
        private string referenceTransaction;

        // Cle etrangere
        private int idCommande;

        // Propriete de navigation
        private Commande commande;
        #endregion

        #region "Modes de paiement et statuts"
        public const string ESPECES = "Espèces";
        public const string MONCASH = "Moncash";
        public const string NATCASH = "Natcash";
        public const string CARTE = "Carte bancaire";

        private const string statutValidee = "Validée";
        private const string statutPayee = "Payée";

        public static readonly string[] ModesValides =
        {
         ESPECES,   MONCASH, NATCASH, CARTE
        };
        #endregion

        #region "Constructeurs"
        public Paiement()
        {
            this.idPaiement = 0;
            this.datePaiement = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            this.montant = 0;
            this.modePaiement = null;
            this.referenceTransaction = null;
            this.idCommande = 0;
        }

        public Paiement(int idPaiement, string datePaiement, decimal montant, string modePaiement,
                        string referenceTransaction, int idCommande)
        {
            this.idPaiement = idPaiement;
            this.datePaiement = datePaiement;
            this.montant = montant;
            this.modePaiement = modePaiement;
            this.referenceTransaction = referenceTransaction;
            this.idCommande = idCommande;
        }
        #endregion

        #region "Proprietes"
        public int IdPaiement
        {
            get { return idPaiement; }
            set { idPaiement = value; }
        }
        public string DatePaiement
        {
            get { return datePaiement; }
            set { datePaiement = value; }
        }
        public decimal Montant
        {
            get { return montant; }
            set { montant = value; }
        }
        public string ModePaiement
        {
            get { return modePaiement; }
            set { modePaiement = value; }
        }
        public string ReferenceTransaction
        {
            get { return referenceTransaction; }
            set { referenceTransaction = value; }
        }
        public int IdCommande
        {
            get { return idCommande; }
            set { idCommande = value; }
        }
        public Commande Commande
        {
            get { return commande; }
            set { commande = value; }
        }
        #endregion

        #region "Méthodes de gestion des paiements"

        #region "Enregistrement d'un paiement"
        public static string EnregistrerPaiement(Paiement paiement)
        {
            // Validations simples, avant d'ouvrir la base
            if (paiement.Montant <= 0)
                return "Le montant du paiement doit être supérieur à 0.";

            if (Array.IndexOf(ModesValides, paiement.ModePaiement) < 0)
                return "Mode de paiement invalide.";

            if (paiement.ModePaiement != ESPECES && string.IsNullOrWhiteSpace(paiement.ReferenceTransaction))
                return "Une référence de transaction est obligatoire pour ce mode de paiement.";

            string requeteCommande = "SELECT Statut, Montant_Total FROM Commande WHERE Id_Commande = @idCommande FOR UPDATE";
            string requeteDejaPaye = "SELECT COALESCE(SUM(Montant), 0) FROM Paiement WHERE Id_Commande = @idCommande";
            string requeteInsert = "INSERT INTO Paiement (Date_Paiement, Montant, Mode_Paiement, Reference_Transaction, Id_Commande) " +
                                   "VALUES (@datePaiement, @montant, @modePaiement, @reference, @idCommande)";
            string requeteStatut = "UPDATE Commande SET Statut = @statut WHERE Id_Commande = @idCommande";

            try
            {
                using (MySqlConnection con = DatabaseHelper.GetConnection())
                {
                    con.Open();

                    // Transaction : tout réussit ou rien n'est enregistré
                    using (MySqlTransaction tx = con.BeginTransaction())
                    {
                        string statutCommande = null;
                        decimal montantTotal = 0;
                        bool existe = false;

                        // 1. Lire la commande (verrouillée jusqu'à la fin de la transaction)
                        using (MySqlCommand cmd = new MySqlCommand(requeteCommande, con, tx))
                        {
                            cmd.Parameters.AddWithValue("@idCommande", paiement.IdCommande);
                            using (MySqlDataReader rd = cmd.ExecuteReader())
                            {
                                if (rd.Read())
                                {
                                    existe = true;
                                    statutCommande = rd["Statut"] != DBNull.Value ? rd["Statut"].ToString() : null;
                                    montantTotal = rd["Montant_Total"] != DBNull.Value ? Convert.ToDecimal(rd["Montant_Total"]) : 0;
                                }
                            }
                        }

                        if (!existe)
                            return "Aucune commande trouvée avec cet ID.";

                        if (statutCommande == statutPayee)
                            return "Cette commande est déjà entièrement payée.";

                        if (statutCommande != statutValidee)
                            return "La commande doit être validée avant le paiement. Statut actuel : " + statutCommande;

                        // 2. Calculer ce qui a déjà été payé
                        decimal dejaPaye;
                        using (MySqlCommand cmd = new MySqlCommand(requeteDejaPaye, con, tx))
                        {
                            cmd.Parameters.AddWithValue("@idCommande", paiement.IdCommande);
                            dejaPaye = Convert.ToDecimal(cmd.ExecuteScalar());
                        }

                        decimal reste = montantTotal - dejaPaye;

                        if (paiement.Montant > reste)
                            return "Le montant dépasse le reste à payer (" + reste.ToString("N2") + ").";

                        // 3. Insérer le paiement
                        using (MySqlCommand cmd = new MySqlCommand(requeteInsert, con, tx))
                        {
                            cmd.Parameters.AddWithValue("@datePaiement", paiement.DatePaiement);
                            cmd.Parameters.AddWithValue("@montant", paiement.Montant);
                            cmd.Parameters.AddWithValue("@modePaiement", paiement.ModePaiement);
                            cmd.Parameters.AddWithValue("@reference",
                                string.IsNullOrWhiteSpace(paiement.ReferenceTransaction)
                                    ? (object)DBNull.Value : paiement.ReferenceTransaction);
                            cmd.Parameters.AddWithValue("@idCommande", paiement.IdCommande);
                            cmd.ExecuteNonQuery();
                        }

                        // 4. Si tout est payé, la commande passe à "Payée"
                        if (paiement.Montant == reste)
                        {
                            using (MySqlCommand cmd = new MySqlCommand(requeteStatut, con, tx))
                            {
                                cmd.Parameters.AddWithValue("@statut", statutPayee);
                                cmd.Parameters.AddWithValue("@idCommande", paiement.IdCommande);
                                cmd.ExecuteNonQuery();
                            }
                        }

                        tx.Commit();

                        return paiement.Montant == reste
                            ? "Paiement enregistré. La commande est entièrement payée!"
                            : "Paiement partiel enregistré. Reste à payer : " + (reste - paiement.Montant).ToString("N2");
                    }
                }
            }
            catch (Exception ex)
            {
                return "Erreur lors de l'enregistrement du paiement : " + ex.Message;
            }
        }
        #endregion

        #region "Suppression d'un paiement"
        public static string SupprimerPaiement(int idPaiement)
        {
            string message = "";
            string requeteIdCommande = "SELECT Id_Commande FROM Paiement WHERE Id_Paiement = @idPaiement";
            string requeteDelete = "DELETE FROM Paiement WHERE Id_Paiement = @idPaiement";
            string requeteStatut = "UPDATE Commande SET Statut = @statut WHERE Id_Commande = @idCommande AND Statut = @ancien";

            try
            {
                using (MySqlConnection con = DatabaseHelper.GetConnection())
                {
                    con.Open();
                    using (MySqlTransaction tx = con.BeginTransaction())
                    {
                        object resultat;
                        using (MySqlCommand cmd = new MySqlCommand(requeteIdCommande, con, tx))
                        {
                            cmd.Parameters.AddWithValue("@idPaiement", idPaiement);
                            resultat = cmd.ExecuteScalar();
                        }

                        if (resultat == null)
                            return "Aucun paiement trouvé avec cet ID.";

                        int idCommande = Convert.ToInt32(resultat);

                        using (MySqlCommand cmd = new MySqlCommand(requeteDelete, con, tx))
                        {
                            cmd.Parameters.AddWithValue("@idPaiement", idPaiement);
                            cmd.ExecuteNonQuery();
                        }

                        // Si la commande était "Payée", elle redevient "Validée"
                        using (MySqlCommand cmd = new MySqlCommand(requeteStatut, con, tx))
                        {
                            cmd.Parameters.AddWithValue("@statut", statutValidee);
                            cmd.Parameters.AddWithValue("@idCommande", idCommande);
                            cmd.Parameters.AddWithValue("@ancien", statutPayee);
                            cmd.ExecuteNonQuery();
                        }

                        tx.Commit();
                        message = "Paiement supprimé avec succès!";
                    }
                }
            }
            catch (Exception ex)
            {
                message = "Erreur de suppression : " + ex.Message;
            }

            return message;
        }
        #endregion

        #region "Liste des paiements"
        public static List<Paiement> ListerPaiementsParCommande(int idCommande)
        {
            List<Paiement> liste = new List<Paiement>();
            string requete = "SELECT * FROM Paiement WHERE Id_Commande = @idCommande ORDER BY Date_Paiement";

            try
            {
                using (MySqlConnection con = DatabaseHelper.GetConnection())
                {
                    con.Open();
                    using (MySqlCommand cmd = new MySqlCommand(requete, con))
                    {
                        cmd.Parameters.AddWithValue("@idCommande", idCommande);

                        using (MySqlDataReader rd = cmd.ExecuteReader())
                        {
                            while (rd.Read())
                            {
                                Paiement p = new Paiement();
                                p.IdPaiement = Convert.ToInt32(rd["Id_Paiement"]);
                                p.DatePaiement = rd["Date_Paiement"].ToString();
                                p.Montant = Convert.ToDecimal(rd["Montant"]);
                                p.ModePaiement = rd["Mode_Paiement"].ToString();
                                p.ReferenceTransaction = rd["Reference_Transaction"] != DBNull.Value
                                    ? rd["Reference_Transaction"].ToString() : null;
                                p.IdCommande = Convert.ToInt32(rd["Id_Commande"]);
                                liste.Add(p);
                            }
                        }
                    }
                }
            }
            catch (MySqlException ex)
            {
                Console.WriteLine("Erreur listage : " + ex.Message);
            }

            return liste;
        }

        public static List<Paiement> ListerTousPaiements()
        {
            List<Paiement> liste = new List<Paiement>();
            string requete = "SELECT * FROM Paiement ORDER BY Date_Paiement DESC";

            try
            {
                using (MySqlConnection con = DatabaseHelper.GetConnection())
                {
                    con.Open();
                    using (MySqlCommand cmd = new MySqlCommand(requete, con))
                    using (MySqlDataReader rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            Paiement p = new Paiement();
                            p.IdPaiement = Convert.ToInt32(rd["Id_Paiement"]);
                            p.DatePaiement = rd["Date_Paiement"].ToString();
                            p.Montant = Convert.ToDecimal(rd["Montant"]);
                            p.ModePaiement = rd["Mode_Paiement"].ToString();
                            p.ReferenceTransaction = rd["Reference_Transaction"] != DBNull.Value
                                ? rd["Reference_Transaction"].ToString() : null;
                            p.IdCommande = Convert.ToInt32(rd["Id_Commande"]);
                            liste.Add(p);
                        }
                    }
                }
            }
            catch (MySqlException ex)
            {
                Console.WriteLine("Erreur listage : " + ex.Message);
            }

            return liste;
        }
        #endregion

        #region "Facturation"
        // Génère le texte de la facture d'une commande entièrement payée
        public static string GenererFacture(int idCommande)
        {
            string requete = "SELECT Date_Commande, Montant_Total, Statut FROM Commande WHERE Id_Commande = @idCommande";

            try
            {
                string dateCommande;
                decimal total;
                string statut;

                using (MySqlConnection con = DatabaseHelper.GetConnection())
                {
                    con.Open();
                    using (MySqlCommand cmd = new MySqlCommand(requete, con))
                    {
                        cmd.Parameters.AddWithValue("@idCommande", idCommande);
                        using (MySqlDataReader rd = cmd.ExecuteReader())
                        {
                            if (!rd.Read())
                                return "Aucune commande trouvée avec cet ID.";

                            dateCommande = rd["Date_Commande"].ToString();
                            total = Convert.ToDecimal(rd["Montant_Total"]);
                            statut = rd["Statut"] != DBNull.Value ? rd["Statut"].ToString() : "";
                        }
                    }
                }

                if (statut != statutPayee)
                    return "La facture n'est disponible que pour une commande entièrement payée.";

                List<Paiement> paiements = ListerPaiementsParCommande(idCommande);

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("=========== FACTURE ===========");
                sb.AppendLine("Facture N° : F-" + idCommande.ToString("D6"));
                sb.AppendLine("Commande N° : " + idCommande);
                sb.AppendLine("Date commande : " + dateCommande);
                sb.AppendLine("-------------------------------");
                sb.AppendLine("Paiements :");

                foreach (Paiement p in paiements)
                {
                    sb.Append("  " + p.DatePaiement + " | " + p.ModePaiement + " | " + p.Montant.ToString("N2"));
                    if (!string.IsNullOrEmpty(p.ReferenceTransaction))
                        sb.Append(" | Réf : " + p.ReferenceTransaction);
                    sb.AppendLine();
                }

                sb.AppendLine("-------------------------------");
                sb.AppendLine("TOTAL PAYÉ : " + total.ToString("N2"));
                sb.AppendLine("Merci de votre visite!");
                sb.AppendLine("===============================");

                return sb.ToString();
            }
            catch (Exception ex)
            {
                return "Erreur lors de la génération de la facture : " + ex.Message;
            }
        }
        #endregion

        #endregion
    }
}
