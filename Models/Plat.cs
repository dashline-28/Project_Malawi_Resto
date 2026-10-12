using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;

namespace Project_Malawi_Resto.Models
{
    public class Plat
    {
        #region "Objets de connexion à la base de données"
        public static MySqlConnection mscon = null;
        public static MySqlCommand cmd = null;
        public static MySqlDataReader rd = null;
        #endregion

        #region "Attributs du plat"
        private int idPlat;
        private string nomPlat;
        private int quantite;
        private string description;
        private decimal prix;
        private string categorie;
        private bool disponibilite;
        #endregion

        #region "Constructeurs"
        public Plat()
        {
            this.idPlat = 0;
            this.nomPlat = null;
            this.quantite = 0;
            this.description = null;
            this.prix = 0;
            this.categorie = null;
            this.disponibilite = true;
        }

        public Plat(string nomPlat, int quantite, string description, decimal prix, string categorie, bool disponibilite = true)
        {
            this.nomPlat = nomPlat;
            this.quantite = quantite;
            this.description = description;
            this.prix = prix;
            this.categorie = categorie;
            this.disponibilite = disponibilite;
        }
        #endregion

        #region "Propriétés"
        public int IdPlat
        {
            get { return idPlat; }
            set { idPlat = value; }
        }

        public string NomPlat
        {
            get { return nomPlat; }
            set { nomPlat = value; }
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

        public string Categorie
        {
            get { return categorie; }
            set { categorie = value; }
        }

        public bool Disponibilite
        {
            get { return disponibilite; }
            set { disponibilite = value; }
        }
        #endregion

        #region "Méthodes de gestion des Plats"

        #region "Insertion d'un plat"
        public static string InsererPlat(Plat plat)
        {
            string message = "";
            string requete = "INSERT INTO Plat (NomPlat, Quantite, Description, Prix, Categorie, Disponibilite) " +
                            "VALUES(@nomPlat, @quantite, @description, @prix, @categorie, @disponibilite)";

            try
            {
                using (mscon = DatabaseHelper.GetConnection())
                {
                    mscon.Open();
                    cmd = new MySqlCommand(requete, mscon);

                    cmd.Parameters.AddWithValue("@nomplat", plat.NomPlat);
                    cmd.Parameters.AddWithValue("@quantite", plat.Quantite);
                    cmd.Parameters.AddWithValue("@description", plat.Description ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@prix", plat.Prix);
                    cmd.Parameters.AddWithValue("@categorie", plat.Categorie ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@disponibilite", plat.Disponibilite);

                    if (cmd.ExecuteNonQuery() > 0)
                    {
                        message = "Plat ajouté avec succès!";
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

        #region "Recherche d'un Plat"
        public static Plat RechercherPlat(int idPlat)
        {
            Plat plat = null;

            string requete = "SELECT * FROM Plat WHERE IdPlat = @idPlat";

            try
            {
                using (mscon = DatabaseHelper.GetConnection())
                {
                    mscon.Open();

                    cmd = new MySqlCommand(requete, mscon);
                    cmd.Parameters.AddWithValue("@idPlat", idPlat);

                    using (rd = cmd.ExecuteReader())
                    {
                        if (rd.Read())
                        {
                            plat = new Plat();

                            plat.IdPlat = Convert.ToInt32(rd["IdPlat"]);
                            plat.NomPlat = rd["NomPlat"].ToString();
                            plat.Quantite = Convert.ToInt32(rd["Quantite"]);

                            plat.Description =
                                rd["Description"] != DBNull.Value
                                ? rd["Description"].ToString()
                                : null;

                            plat.Prix = Convert.ToDecimal(rd["Prix"]);

                            plat.Categorie =
                                rd["Categorie"] != DBNull.Value
                                ? rd["Categorie"].ToString()
                                : null;

                            plat.Disponibilite =
                                Convert.ToBoolean(rd["Disponibilite"]);
                        }
                    }
                }
            }
            catch (MySqlException ex)
            {
                Console.WriteLine("Erreur de la recherche : " + ex.Message);
            }

            return plat;
        }
        #endregion

        #region "Modification d'un Plat"
        public static string ModifierPlat(Plat plat)
        {
            string message = "";
            string requete = "UPDATE Plat SET NomPlat=@nomPlat, Quantite=@quantite, " +
                            "Description=@description, Prix=@prix, Categorie=@categorie, " +
                            "Disponibilite=@disponibilite WHERE IdPlat=@idPlat";

            try
            {
                using (mscon = DatabaseHelper.GetConnection())
                {
                    mscon.Open();
                    cmd = new MySqlCommand(requete, mscon);

                    cmd.Parameters.AddWithValue("@nomPlat", plat.NomPlat);
                    cmd.Parameters.AddWithValue("@quantite", plat.Quantite);
                    cmd.Parameters.AddWithValue("@description", plat.Description ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@prix", plat.Prix);
                    cmd.Parameters.AddWithValue("@categorie", plat.Categorie ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@disponibilite", plat.Disponibilite);
                    cmd.Parameters.AddWithValue("@idplat", plat.IdPlat);

                    if (cmd.ExecuteNonQuery() > 0)
                    {
                        message = "Plat modifié avec succès!";
                    }
                    else
                    {
                        message = "Aucun plat trouvé avec cet ID.";
                    }
                }
            }

            catch (MySqlException ex)
            {
                message = "Erreur de modification: " + ex.Message;
            }

            return message;
        }
        #endregion

        #region "Suppression d'un plat"
        public static string SupprimerPlat(int idPlat)
        {
            string message = "";
            string requete = "DELETE FROM Plat WHERE IdPlat = @idPlat";

            try
            {
                using (mscon = DatabaseHelper.GetConnection())
                {
                    mscon.Open();
                    cmd = new MySqlCommand(requete, mscon);
                    cmd.Parameters.AddWithValue("@idPlat", idPlat);

                    if (cmd.ExecuteNonQuery() > 0)
                    {
                        message = "Plat supprimé avec succès!";
                    }
                    else
                    {
                        message = "Aucun plat trouvé avec cet ID.";
                    }
                }
            }

            catch (MySqlException ex)
            {
                message = "Erreur de suppression: " + ex.Message;
            }

            return message;
        }
        #endregion

        #region "Liste de tous les plats"
        public static List<Plat> ListerPlats()
        {
            List<Plat> listePlat = new List<Plat>();

            string requete = "SELECT * FROM Plat ORDER BY Disponibilite, NomPlat";

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
                            Plat p = new Plat();

                            p.IdPlat = Convert.ToInt32(rd["IdPlat"]);
                            p.NomPlat = rd["NomPlat"].ToString();
                            p.Quantite = Convert.ToInt32(rd["Quantite"]);

                            p.Description =
                                rd["Description"] != DBNull.Value
                                ? rd["Description"].ToString()
                                : null;

                            p.Prix = Convert.ToDecimal(rd["Prix"]);

                            p.Categorie =
                                rd["Categorie"] != DBNull.Value
                                ? rd["Categorie"].ToString()
                                : null;

                            p.Disponibilite =
                                Convert.ToBoolean(rd["Disponibilite"]);

                            listePlat.Add(p);
                        }
                    }
                }
            }
            catch (MySqlException ex)
            {
                Console.WriteLine("Erreur de listage : " + ex.Message);
            }

            return listePlat;
        }
        #endregion

        #region "Modifier quantité"
        public static string ModifierQuantite(int idPlat, int quantite)
        {
            string message = "";
            string requete = "UPDATE Plat SET Quantite = @quantite WHERE IdPlat = @idPlat";

            try
            {
                using (mscon = DatabaseHelper.GetConnection())
                {
                    mscon.Open();
                    cmd = new MySqlCommand(requete, mscon);
                    cmd.Parameters.AddWithValue("@quantite", quantite);
                    cmd.Parameters.AddWithValue("@idPlat", idPlat);

                    if (cmd.ExecuteNonQuery() > 0)
                    {
                        message = "Quantité modifiée avec succès!";
                    }
                    else
                    {
                        message = "Aucun plat trouvée avec cet ID.";
                    }
                }
            }
            catch (MySqlException ex)
            {
                message = "Erreur de modification: " + ex.Message;
            }

            return message;
        }
        #endregion

        #region "Statistiques"
        public static DataTable GetPlatPlusVendus(DateTime dateDebut, DateTime dateFin, int limit = 10)
        {
            DataTable dt = new DataTable();
            string requete = @"SELECT p.IdPlat, p.NomPlat, SUM(dc.Quantite) as TotalVendu, p.Prix
                              FROM Plat p
                              INNER JOIN Detail_Commande dc ON p.IdPlat = dc.IdPlat
                              INNER JOIN Commande c ON dc.Id_Commande = c.Id_Commande
                              WHERE c.Date_Commande BETWEEN @dateDebut AND @dateFin
                              AND c.Statut != 'annulée'
                              GROUP BY p.IdPlat, p.NomPlat, p.Prix
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