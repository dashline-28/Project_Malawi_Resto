using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;

namespace Project_Malawi_Resto.Models
{
    public class Utilisateur
    {
        #region "Objets de connexion à la base de données"

        public static MySqlConnection mscon = null;
        public static MySqlCommand cmd = null;
        public static MySqlDataReader rd = null;

        #endregion

        #region "Attributs de l'utilisateur"

        private int idUtilisateur;
        private string nom;
        private string prenom;
        private string email;
        private string password;
        private string role;

        #endregion

        #region "Constructeurs"

        public Utilisateur()
        {
            this.idUtilisateur = 0;
            this.nom = null;
            this.prenom = null;
            this.email = null;
            this.password = null;
            this.role = null;
        }

        public Utilisateur(string nom, string prenom,
                           string email, string password,
                           string role)
        {
            this.nom = nom;
            this.prenom = prenom;
            this.email = email;
            this.password = password;
            this.role = role;
        }

        #endregion

        #region "Propriétés"

        public int IdUtilisateur
        {
            get { return idUtilisateur; }
            set { idUtilisateur = value; }
        }

        public string Nom
        {
            get { return nom; }
            set { nom = value; }
        }

        public string Prenom
        {
            get { return prenom; }
            set { prenom = value; }
        }

        public string Email
        {
            get { return email; }
            set { email = value; }
        }

        public string Password
        {
            get { return password; }
            set { password = value; }
        }

        public string Role
        {
            get { return role; }
            set { role = value; }
        }

        #endregion

        #region "Méthodes de gestion des utilisateurs"

        #region "Insertion d'un utilisateur"

        public static string InsererUtilisateur(Utilisateur utilisateur)
        {
            string message = "";

            string requete = "INSERT INTO Utilisateur " +
                             "(nom, prenom, email, mot_de_passe, role) " +
                             "VALUES (@nom, @prenom, @email, @password, @role)";

            try
            {
                using (mscon = DatabaseHelper.GetConnection())
                {
                    mscon.Open();

                    cmd = new MySqlCommand(requete, mscon);

                    cmd.Parameters.AddWithValue("@nom",
                        utilisateur.Nom);

                    cmd.Parameters.AddWithValue("@prenom",
                        utilisateur.Prenom);

                    cmd.Parameters.AddWithValue("@email",
                        utilisateur.Email);

                    cmd.Parameters.AddWithValue("@password",
                        utilisateur.Password);

                    cmd.Parameters.AddWithValue("@role",
                        utilisateur.Role);

                    if (cmd.ExecuteNonQuery() > 0)
                    {
                        message = "Utilisateur ajouté avec succès!";
                    }
                }
            }
            catch (MySqlException ex)
            {
                message = "Erreur lors de l'insertion : " + ex.Message;
            }

            return message;
        }

        #endregion

        #region "Recherche d'un utilisateur"

        public static Utilisateur RechercherUtilisateur(int idUtilisateur)
        {
            Utilisateur utilisateur = null;

            string requete =
                "SELECT * FROM Utilisateur " +
                "WHERE idUtilisateur = @idUtilisateur";

            try
            {
                using (mscon = DatabaseHelper.GetConnection())
                {
                    mscon.Open();

                    cmd = new MySqlCommand(requete, mscon);

                    cmd.Parameters.AddWithValue(
                        "@idUtilisateur",
                        idUtilisateur);

                    using (rd = cmd.ExecuteReader())
                    {
                        if (rd.Read())
                        {
                            utilisateur = new Utilisateur();

                            utilisateur.IdUtilisateur =
                                Convert.ToInt32(rd["idUtilisateur"]);

                            utilisateur.Nom =
                                rd["nom"].ToString();

                            utilisateur.Prenom =
                                rd["prenom"].ToString();

                            utilisateur.Email =
                                rd["email"].ToString();

                            utilisateur.Password =
                                rd["password"].ToString();

                            utilisateur.Role =
                                rd["role"].ToString();
                        }
                    }
                }
            }
            catch (MySqlException ex)
            {
                Console.WriteLine(
                    "Erreur de recherche : " + ex.Message);
            }

            return utilisateur;
        }

        #endregion

        #region "Modification d'un utilisateur"

        public static string ModifierUtilisateur(Utilisateur utilisateur)
        {
            string message = "";

            string requete = "UPDATE Utilisateur SET " +
                             "nom = @nom, " +
                             "prenom = @prenom, " +
                             "email = @email, " +
                             "password = @password, " +
                             "role = @role " +
                             "WHERE idUtilisateur = @idUtilisateur";

            try
            {
                using (mscon = DatabaseHelper.GetConnection())
                {
                    mscon.Open();

                    cmd = new MySqlCommand(requete, mscon);

                    cmd.Parameters.AddWithValue(
                        "@nom",
                        utilisateur.Nom);

                    cmd.Parameters.AddWithValue(
                        "@prenom",
                        utilisateur.Prenom);

                    cmd.Parameters.AddWithValue(
                        "@email",
                        utilisateur.Email);

                    cmd.Parameters.AddWithValue(
                        "@password",
                        utilisateur.Password);

                    cmd.Parameters.AddWithValue(
                        "@role",
                        utilisateur.Role);

                    cmd.Parameters.AddWithValue(
                        "@idUtilisateur",
                        utilisateur.IdUtilisateur);

                    if (cmd.ExecuteNonQuery() > 0)
                    {
                        message = "Utilisateur modifié avec succès!";
                    }
                    else
                    {
                        message = "Aucun utilisateur trouvé avec cet ID.";
                    }
                }
            }
            catch (MySqlException ex)
            {
                message = "Erreur de modification : " + ex.Message;
            }

            return message;
        }

        #endregion

        #region "Suppression d'un utilisateur"

        public static string SupprimerUtilisateur(int idUtilisateur)
        {
            string message = "";

            string requete =
                "DELETE FROM Utilisateur " +
                "WHERE idUtilisateur = @idUtilisateur";

            try
            {
                using (mscon = DatabaseHelper.GetConnection())
                {
                    mscon.Open();

                    cmd = new MySqlCommand(requete, mscon);

                    cmd.Parameters.AddWithValue(
                        "@idUtilisateur",
                        idUtilisateur);

                    if (cmd.ExecuteNonQuery() > 0)
                    {
                        message = "Utilisateur supprimé avec succès!";
                    }
                    else
                    {
                        message = "Aucun utilisateur trouvé avec cet ID.";
                    }
                }
            }
            catch (MySqlException ex)
            {
                message = "Erreur de suppression : " + ex.Message;
            }

            return message;
        }

        #endregion

        #region "Liste de tous les utilisateurs"

        public static List<Utilisateur> ListerUtilisateurs()
        {
            List<Utilisateur> listeUtilisateurs = new List<Utilisateur>();

            string requete =
                "SELECT * FROM Utilisateur " +
                "ORDER BY nom, prenom";

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
                            Utilisateur u = new Utilisateur();

                            u.IdUtilisateur =
                                Convert.ToInt32(
                                    rd["idUtilisateur"]);

                            u.Nom =
                                rd["nom"].ToString();

                            u.Prenom =
                                rd["prenom"].ToString();

                            u.Email =
                                rd["email"].ToString();

                            u.Password =
                                rd["password"].ToString();

                            u.Role =
                                rd["role"].ToString();

                            listeUtilisateurs.Add(u);
                        }
                    }
                }
            }
            catch (MySqlException ex)
            {
                Console.WriteLine(
                    "Erreur de listage : " + ex.Message);
            }

            return listeUtilisateurs;
        }

        #endregion
        #endregion
    }
}
