using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;

namespace Project_Malawi_Resto.Models
{
    public class Client
    {
        #region "Objets de connexion à la base de données"

        public static MySqlConnection mscon = null;
        public static MySqlCommand cmd = null;
        public static MySqlDataReader rd = null;

        #endregion


        #region "Attributs du client"

        private int idClient;
        private string nomClient;
        private string prenomClient;
        private string telephone;
        private string adresse;

        #endregion


        #region "Constructeurs"

        public Client()
        {
            this.idClient = 0;
            this.nomClient = null;
            this.prenomClient = null;
            this.telephone = null;
            this.adresse = null;
        }

        public Client(string nomClient, string prenomClient,
                      string telephone, string adresse)
        {
            this.nomClient = nomClient;
            this.prenomClient = prenomClient;
            this.telephone = telephone;
            this.adresse = adresse;
        }

        #endregion


        #region "Propriétés"

        public int IdClient
        {
            get { return idClient; }
            set { idClient = value; }
        }

        public string NomClient
        {
            get { return nomClient; }
            set { nomClient = value; }
        }

        public string PrenomClient
        {
            get { return prenomClient; }
            set { prenomClient = value; }
        }

        public string Telephone
        {
            get { return telephone; }
            set { telephone = value; }
        }

        public string Adresse
        {
            get { return adresse; }
            set { adresse = value; }
        }

        #endregion


        #region "Méthodes de gestion des clients"


        #region "Insertion d'un client"

        public static string InsererClient(Client client)
        {
            string message = "";

            string requete = "INSERT INTO Client " +
                             "(nomClient, prenomClient, telephone, adresse) " +
                             "VALUES (@nomClient, @prenomClient, @telephone, @adresse)";

            try
            {
                using (mscon = DatabaseHelper.GetConnection())
                {
                    mscon.Open();

                    cmd = new MySqlCommand(requete, mscon);

                    cmd.Parameters.AddWithValue("@nomClient", client.NomClient);
                    cmd.Parameters.AddWithValue("@prenomClient", client.PrenomClient);
                    cmd.Parameters.AddWithValue("@telephone",
                        client.Telephone ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@adresse",
                        client.Adresse ?? (object)DBNull.Value);

                    if (cmd.ExecuteNonQuery() > 0)
                    {
                        message = "Client ajouté avec succès!";
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


        #region "Recherche d'un client"

        public static Client RechercherClient(int idClient)
        {
            Client client = null;

            string requete = "SELECT * FROM Client WHERE idClient = @idClient";

            try
            {
                using (mscon = DatabaseHelper.GetConnection())
                {
                    mscon.Open();

                    cmd = new MySqlCommand(requete, mscon);

                    cmd.Parameters.AddWithValue("@idClient", idClient);

                    using (rd = cmd.ExecuteReader())
                    {
                        if (rd.Read())
                        {
                            client = new Client();

                            client.IdClient =
                                Convert.ToInt32(rd["idClient"]);

                            client.NomClient =
                                rd["nomClient"].ToString();

                            client.PrenomClient =
                                rd["prenomClient"].ToString();

                            client.Telephone =
                                rd["telephone"] != DBNull.Value
                                ? rd["telephone"].ToString()
                                : null;

                            client.Adresse =
                                rd["adresse"] != DBNull.Value
                                ? rd["adresse"].ToString()
                                : null;
                        }
                    }
                }
            }
            catch (MySqlException ex)
            {
                Console.WriteLine(
                    "Erreur de recherche : " + ex.Message);
            }

            return client;
        }

        #endregion


        #region "Modification d'un client"

        public static string ModifierClient(Client client)
        {
            string message = "";

            string requete = "UPDATE Client SET " +
                             "nomClient = @nomClient, " +
                             "prenomClient = @prenomClient, " +
                             "telephone = @telephone, " +
                             "adresse = @adresse " +
                             "WHERE idClient = @idClient";

            try
            {
                using (mscon = DatabaseHelper.GetConnection())
                {
                    mscon.Open();

                    cmd = new MySqlCommand(requete, mscon);

                    cmd.Parameters.AddWithValue("@nomClient",
                        client.NomClient);

                    cmd.Parameters.AddWithValue("@prenomClient",
                        client.PrenomClient);

                    cmd.Parameters.AddWithValue("@telephone",
                        client.Telephone ?? (object)DBNull.Value);

                    cmd.Parameters.AddWithValue("@adresse",
                        client.Adresse ?? (object)DBNull.Value);

                    cmd.Parameters.AddWithValue("@idClient",
                        client.IdClient);

                    if (cmd.ExecuteNonQuery() > 0)
                    {
                        message = "Client modifié avec succès!";
                    }
                    else
                    {
                        message = "Aucun client trouvé avec cet ID.";
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


        #region "Suppression d'un client"

        public static string SupprimerClient(int idClient)
        {
            string message = "";

            string requete =
                "DELETE FROM Client WHERE idClient = @idClient";

            try
            {
                using (mscon = DatabaseHelper.GetConnection())
                {
                    mscon.Open();

                    cmd = new MySqlCommand(requete, mscon);

                    cmd.Parameters.AddWithValue("@idClient", idClient);

                    if (cmd.ExecuteNonQuery() > 0)
                    {
                        message = "Client supprimé avec succès!";
                    }
                    else
                    {
                        message = "Aucun client trouvé avec cet ID.";
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


        #region "Liste de tous les clients"

        public static List<Client> ListerClients()
        {
            List<Client> listeClients = new List<Client>();

            string requete =
                "SELECT * FROM Client ORDER BY nomClient, prenomClient";

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
                            Client c = new Client();

                            c.IdClient =
                                Convert.ToInt32(rd["idClient"]);

                            c.NomClient =
                                rd["nomClient"].ToString();

                            c.PrenomClient =
                                rd["prenomClient"].ToString();

                            c.Telephone =
                                rd["telephone"] != DBNull.Value
                                ? rd["telephone"].ToString()
                                : null;

                            c.Adresse =
                                rd["adresse"] != DBNull.Value
                                ? rd["adresse"].ToString()
                                : null;

                            listeClients.Add(c);
                        }
                    }
                }
            }
            catch (MySqlException ex)
            {
                Console.WriteLine(
                    "Erreur de listage : " + ex.Message);
            }

            return listeClients;
        }

        #endregion


        #endregion
    }
}