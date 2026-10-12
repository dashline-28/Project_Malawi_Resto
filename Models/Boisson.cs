using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;

namespace Project_Malawi_Resto.Models
{
    public class Boisson
    {
        #region "Objets de connexion à la base de données"
        public static MySqlConnection mscon = null;
        public static MySqlCommand cmd = null;
        public static MySqlDataReader rd = null;
        #endregion

        #region "Attributs de la boisson"
        private int idBoisson;
        private string nomBoisson;
        private int quantite;
        private string description;
        private decimal prix;
        private bool disponibilite;
        private string typeBoisson;
        #endregion

        #region "Constructeurs"
        public Boisson()
        {
            this.idBoisson = 0;
            this.nomBoisson = null;
            this.quantite = 0;
            this.description = null;
            this.prix = 0;
            this.disponibilite = true;
            this.typeBoisson = null;
        }

        public Boisson(string nomBoisson, int quantite, string description, decimal prix, string typeBoisson, bool disponibilite = true)
        {
            this.nomBoisson = nomBoisson;
            this.quantite = quantite;
            this.description = description;
            this.prix = prix;
            this.disponibilite = disponibilite;
            this.typeBoisson = typeBoisson;
        }
        #endregion

        #region "Propriétés"
        public int IdBoisson
        {
            get { return idBoisson; }
            set { idBoisson = value; }
        }

        public string NomBoisson
        {
            get { return nomBoisson; }
            set { nomBoisson = value; }
        }

        public int Quantite
        {
            get { return quantite; }
            set { quantite = value; }
        }

        public string Description
        {
            get { return description; }
            set { description = value; }
        }

        public decimal Prix
        {
            get { return prix; }
            set { prix = value; }
        }

        public bool Disponibilite
        {
            get { return disponibilite; }
            set { disponibilite = value; }
        }

        public string TypeBoisson
        {
            get { return typeBoisson; }
            set { typeBoisson = value; }
        }
        #endregion

        #region "Méthodes de gestion des boissons"

        #region "Insertion d'une boisson"
        public static string InsererBoisson(Boisson boisson)
        {
            string message = "";
            string requete = "INSERT INTO Boisson (Nom_Boisson, Quantite, Description, Prix, Disponibilite, Type_Boisson) " +
                            "VALUES(@nomBoisson, @quantite, @description, @prix, @disponibilite, @typeBoisson)";

            try
            {
                using (mscon = DatabaseHelper.GetConnection())
                {
                    mscon.Open();
                    cmd = new MySqlCommand(requete, mscon);

                    cmd.Parameters.AddWithValue("@nomBoisson", boisson.NomBoisson);
                    cmd.Parameters.AddWithValue("@quantite", boisson.Quantite);
                    cmd.Parameters.AddWithValue("@description", boisson.Description ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@prix", boisson.Prix);
                    cmd.Parameters.AddWithValue("@disponibilite", boisson.Disponibilite);
                    cmd.Parameters.AddWithValue("@typeBoisson", boisson.TypeBoisson ?? (object)DBNull.Value);

                    if (cmd.ExecuteNonQuery() > 0)
                    {
                        message = "Boisson ajoutée avec succès!";
                    }
                }
            }
            catch (MySqlException ex)
            {
                message = "Erreur lors de l'insertion: " + ex.Message;
            }

            return message;
        }
        #endregion

        #region "Recherche d'une boisson"
        public static Boisson RechercherBoisson(int idBoisson)
        {
            Boisson boisson = null;
            string requete = "SELECT * FROM Boisson WHERE Id_Boisson = @idBoisson";

            try
            {
                using (mscon = DatabaseHelper.GetConnection())
                {
                    mscon.Open();
                    cmd = new MySqlCommand(requete, mscon);
                    cmd.Parameters.AddWithValue("@idBoisson", idBoisson);

                    using (rd = cmd.ExecuteReader())
                    {
                        if (rd.Read())
                        {
                            boisson = new Boisson();
                            boisson.IdBoisson = Convert.ToInt32(rd["Id_Boisson"]);
                            boisson.NomBoisson = rd["Nom_Boisson"].ToString();
                            boisson.Quantite = Convert.ToInt32(rd["Quantite"]);
                            boisson.Description = rd["Description"] != DBNull.Value ? rd["Description"].ToString() : null;
                            boisson.Prix = Convert.ToDecimal(rd["Prix"]);
                            boisson.Disponibilite = Convert.ToBoolean(rd["Disponibilite"]);
                            boisson.TypeBoisson = rd["Type_Boisson"] != DBNull.Value ? rd["Type_Boisson"].ToString() : null;
                        }
                    }
                }
            }
            catch (MySqlException ex)
            {
                Console.WriteLine("Erreur recherche: " + ex.Message);
            }

            return boisson;
        }
        #endregion

        #region "Modification d'une boisson"
        public static string ModifierBoisson(Boisson boisson)
        {
            string message = "";
            string requete = "UPDATE Boisson SET Nom_Boisson=@nomBoisson, Quantite=@quantite, " +
                            "Description=@description, Prix=@prix, Disponibilite=@disponibilite, " +
                            "Type_Boisson=@typeBoisson WHERE Id_Boisson=@idBoisson";

            try
            {
                using (mscon = DatabaseHelper.GetConnection())
                {
                    mscon.Open();
                    cmd = new MySqlCommand(requete, mscon);

                    cmd.Parameters.AddWithValue("@nomBoisson", boisson.NomBoisson);
                    cmd.Parameters.AddWithValue("@quantite", boisson.Quantite);
                    cmd.Parameters.AddWithValue("@description", boisson.Description ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@prix", boisson.Prix);
                    cmd.Parameters.AddWithValue("@disponibilite", boisson.Disponibilite);
                    cmd.Parameters.AddWithValue("@typeBoisson", boisson.TypeBoisson ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@idBoisson", boisson.IdBoisson);

                    if (cmd.ExecuteNonQuery() > 0)
                    {
                        message = "Boisson modifiée avec succès!";
                    }
                    else
                    {
                        message = "Aucune boisson trouvée avec cet ID.";
                    }
                }
            }
            catch (MySqlException ex)
            {
                message = "Erreur modification: " + ex.Message;
            }

            return message;
        }
        #endregion

        #region "Suppression d'une boisson"
        public static string SupprimerBoisson(int idBoisson)
        {
            string message = "";
            string requete = "DELETE FROM Boisson WHERE Id_Boisson = @idBoisson";

            try
            {
                using (mscon = DatabaseHelper.GetConnection())
                {
                    mscon.Open();
                    cmd = new MySqlCommand(requete, mscon);
                    cmd.Parameters.AddWithValue("@idBoisson", idBoisson);

                    if (cmd.ExecuteNonQuery() > 0)
                    {
                        message = "Boisson supprimée avec succès!";
                    }
                    else
                    {
                        message = "Aucune boisson trouvée avec cet ID.";
                    }
                }
            }
            catch (MySqlException ex)
            {
                message = "Erreur suppression: " + ex.Message;
            }

            return message;
        }
        #endregion

        #region "Liste de toutes les boissons"
        public static List<Boisson> ListerToutesBoissons()
        {
            List<Boisson> listeBoissons = new List<Boisson>();
            string requete = "SELECT * FROM Boisson ORDER BY Type_Boisson, Nom_Boisson";

            try
            {
                using (mscon = DatabaseHelper.GetConnection())
                {
                    mscon.Open();
                    cmd = new MySqlCommand(requete, mscon);

                    using (rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            Boisson b = new Boisson();
                            b.IdBoisson = Convert.ToInt32(rd["Id_Boisson"]);
                            b.NomBoisson = rd["Nom_Boisson"].ToString();
                            b.Quantite = Convert.ToInt32(rd["Quantite"]);
                            b.Description = rd["Description"] != DBNull.Value ? rd["Description"].ToString() : null;
                            b.Prix = Convert.ToDecimal(rd["Prix"]);
                            b.Disponibilite = Convert.ToBoolean(rd["Disponibilite"]);
                            b.TypeBoisson = rd["Type_Boisson"] != DBNull.Value ? rd["Type_Boisson"].ToString() : null;

                            listeBoissons.Add(b);
                        }
                    }
                }
            }
            catch (MySqlException ex)
            {
                Console.WriteLine("Erreur listage: " + ex.Message);
            }

            return listeBoissons;
        }
        #endregion

        #region "Liste des boissons disponibles"
        public static List<Boisson> ListerBoissonsDisponibles()
        {
            List<Boisson> listeBoissons = new List<Boisson>();
            string requete = "SELECT * FROM Boisson WHERE Disponibilite = TRUE AND Quantite > 0 ORDER BY Type_Boisson, Nom_Boisson";

            try
            {
                using (mscon = DatabaseHelper.GetConnection())
                {
                    mscon.Open();
                    cmd = new MySqlCommand(requete, mscon);

                    using (rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            Boisson b = new Boisson();
                            b.IdBoisson = Convert.ToInt32(rd["Id_Boisson"]);
                            b.NomBoisson = rd["Nom_Boisson"].ToString();
                            b.Quantite = Convert.ToInt32(rd["Quantite"]);
                            b.Description = rd["Description"] != DBNull.Value ? rd["Description"].ToString() : null;
                            b.Prix = Convert.ToDecimal(rd["Prix"]);
                            b.Disponibilite = Convert.ToBoolean(rd["Disponibilite"]);
                            b.TypeBoisson = rd["Type_Boisson"] != DBNull.Value ? rd["Type_Boisson"].ToString() : null;

                            listeBoissons.Add(b);
                        }
                    }
                }
            }
            catch (MySqlException ex)
            {
                Console.WriteLine("Erreur listage: " + ex.Message);
            }

            return listeBoissons;
        }
        #endregion

        #region "Modifier quantité"
        public static string ModifierQuantite(int idBoisson, int quantite)
        {
            string message = "";
            string requete = "UPDATE Boisson SET Quantite = @quantite WHERE Id_Boisson = @idBoisson";

            try
            {
                using (mscon = DatabaseHelper.GetConnection())
                {
                    mscon.Open();
                    cmd = new MySqlCommand(requete, mscon);
                    cmd.Parameters.AddWithValue("@quantite", quantite);
                    cmd.Parameters.AddWithValue("@idBoisson", idBoisson);

                    if (cmd.ExecuteNonQuery() > 0)
                    {
                        message = "Quantité modifiée avec succès!";
                    }
                    else
                    {
                        message = "Aucune boisson trouvée avec cet ID.";
                    }
                }
            }
            catch (MySqlException ex)
            {
                message = "Erreur modification: " + ex.Message;
            }

            return message;
        }
        #endregion

        #region "Statistiques"
        public static DataTable GetBoissonsPlusVendues(DateTime dateDebut, DateTime dateFin, int limit = 10)
        {
            DataTable dt = new DataTable();
            string requete = @"SELECT b.Id_Boisson, b.Nom_Boisson, SUM(dc.Quantite) as TotalVendu, b.Prix
                              FROM Boisson b
                              INNER JOIN Detail_Commande dc ON b.Id_Boisson = dc.Id_Boisson
                              INNER JOIN Commande c ON dc.Id_Commande = c.Id_Commande
                              WHERE c.Date_Commande BETWEEN @dateDebut AND @dateFin
                              AND c.Statut != 'annulée'
                              GROUP BY b.Id_Boisson, b.Nom_Boisson, b.Prix
                              ORDER BY TotalVendu DESC
                              LIMIT @limit";

            try
            {
                using (mscon = DatabaseHelper.GetConnection())
                using (cmd = new MySqlCommand(requete, mscon))
                {
                    cmd.Parameters.AddWithValue("@dateDebut", dateDebut);
                    cmd.Parameters.AddWithValue("@dateFin", dateFin);
                    cmd.Parameters.AddWithValue("@limit", limit);
                    mscon.Open();
                    using (var adapter = new MySqlDataAdapter(cmd))
                    {
                        adapter.Fill(dt);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erreur statistiques: " + ex.Message);
            }

            return dt;
        }
        #endregion
        #endregion
    }
}