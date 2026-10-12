using System;
using System.Collections.Generic;
using System.Data;
using MySql.Data.MySqlClient;
using Microsoft.Extensions.Configuration;

namespace Project_Malawi_Resto.Models
{
    public static class DatabaseHelper
    {
        private static string? _chaineConnexion;

        public static void Initialize(IConfiguration configuration)
        {
            _chaineConnexion = configuration.GetConnectionString("DBconnect");
        }

        public static MySqlConnection GetConnection()
        {
            if (string.IsNullOrEmpty(_chaineConnexion))
                throw new InvalidOperationException("La chaîne de connexion n'est pas initialisée.");

            return new MySqlConnection(_chaineConnexion);
        }

        public static DataTable ExecuteQuery(string query, Dictionary<string, object>? parameters = null)
        {
            DataTable dt = new DataTable();
            try
            {
                using (var con = GetConnection())
                using (var cmd = new MySqlCommand(query, con))
                {
                    if (parameters != null)
                    {
                        foreach (var param in parameters)
                        {
                            cmd.Parameters.AddWithValue(param.Key, param.Value);
                        }
                    }
                    con.Open();
                    using (var adapter = new MySqlDataAdapter(cmd))
                    {
                        adapter.Fill(dt);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Erreur lors de l'exécution de la requête: {ex.Message}");
            }
            return dt;
        }

        public static int ExecuteNonQuery(string query, Dictionary<string, object>? parameters = null)
        {
            int result = 0;
            try
            {
                using (var con = GetConnection())
                using (var cmd = new MySqlCommand(query, con))
                {
                    if (parameters != null)
                    {
                        foreach (var param in parameters)
                        {
                            cmd.Parameters.AddWithValue(param.Key, param.Value);
                        }
                    }
                    con.Open();
                    result = cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Erreur lors de l'exécution de la requête: {ex.Message}");
            }
            return result;
        }

        public static object? ExecuteScalar(string query, Dictionary<string, object>? parameters = null)
        {
            object? result = null;
            try
            {
                using (var con = GetConnection())
                using (var cmd = new MySqlCommand(query, con))
                {
                    if (parameters != null)
                    {
                        foreach (var param in parameters)
                        {
                            cmd.Parameters.AddWithValue(param.Key, param.Value);
                        }
                    }
                    con.Open();
                    result = cmd.ExecuteScalar();
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Erreur lors de l'exécution de la requête: {ex.Message}");
            }
            return result;
        }
    }
}