using System;
using System.Collections.Generic;
using MySql.Data.MySqlClient;

namespace APP_OptiSolar
{
    public class DatabaseService
    {
        private string connectionString = "Server=localhost; Database=pilone; Uid=root; Pwd=;";

        // Créer la table historique si elle n'existe pas
        public void EnsureHistoryTableExists()
        {
            using (MySqlConnection conn = new MySqlConnection(connectionString))
            {
                conn.Open();
                string sql = @"
                    CREATE TABLE IF NOT EXISTS historique_panneaux (
                        id          INT AUTO_INCREMENT PRIMARY KEY,
                        nom_panneau VARCHAR(100) NOT NULL,
                        tension     DOUBLE,
                        statut      VARCHAR(50),
                        horodatage  DATETIME NOT NULL
                    )";
                MySqlCommand cmd = new MySqlCommand(sql, conn);
                cmd.ExecuteNonQuery();
            }
        }

        // Récupérer tous les panneaux depuis MySQL
        public List<SolarPanel> GetAllPanels()
        {
            List<SolarPanel> list = new List<SolarPanel>();
            using (MySqlConnection conn = new MySqlConnection(connectionString))
            {
                conn.Open();
                string sql = "SELECT * FROM panneaux_solaires";
                MySqlCommand cmd = new MySqlCommand(sql, conn);
                using (MySqlDataReader rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        list.Add(new SolarPanel
                        {
                            Id = rdr["id"].ToString(),
                            Name = rdr["nom_panneau"].ToString(),
                            Voltage = rdr["tension"] != DBNull.Value ? Convert.ToDouble(rdr["tension"]) : (double?)null,
                            Latitude = Convert.ToDouble(rdr["latitude"]),
                            Longitude = Convert.ToDouble(rdr["longitude"]),
                            Status = Enum.TryParse(rdr["statut"].ToString(), out PanelStatus s) ? s : PanelStatus.Inactif,
                            LastUpdate = Convert.ToDateTime(rdr["derniere_maj"]),
                            DeviceId = rdr["id"].ToString()
                        });
                    }
                }
            }
            return list;
        }

        // Mettre à jour un panneau ET sauvegarder dans l'historique
        public void UpdatePanelData(string nom, double voltage, double courant, string statut)
        {
            // Normaliser le statut pour correspondre exactement à ce qui est en DB
            // PanelStatus.ToString() donne "Defectueux" sans accent — on s'assure de la cohérence
            string statutNormalise = statut.Replace("é", "e").Replace("É", "E");

            using (MySqlConnection conn = new MySqlConnection(connectionString))
            {
                conn.Open();

                // Mise à jour des données actuelles
                string sqlUpdate = "UPDATE panneaux_solaires SET tension=@v, statut=@s, derniere_maj=@maj WHERE nom_panneau=@n";
                MySqlCommand cmdUpdate = new MySqlCommand(sqlUpdate, conn);
                cmdUpdate.Parameters.AddWithValue("@v", voltage);
                cmdUpdate.Parameters.AddWithValue("@s", statutNormalise);
                cmdUpdate.Parameters.AddWithValue("@maj", DateTime.Now);
                cmdUpdate.Parameters.AddWithValue("@n", nom.Trim());
                cmdUpdate.ExecuteNonQuery();

                // Sauvegarde dans l'historique
                string sqlHistory = @"
                    INSERT INTO historique_panneaux (nom_panneau, tension, statut, horodatage)
                    VALUES (@n, @v, @s, @maj)";
                MySqlCommand cmdHistory = new MySqlCommand(sqlHistory, conn);
                cmdHistory.Parameters.AddWithValue("@n", nom.Trim());
                cmdHistory.Parameters.AddWithValue("@v", voltage);
                cmdHistory.Parameters.AddWithValue("@s", statutNormalise);
                cmdHistory.Parameters.AddWithValue("@maj", DateTime.Now);
                cmdHistory.ExecuteNonQuery();
            }
        }

        // Récupérer l'historique d'un panneau (7 derniers jours par défaut)
        public List<PanelHistory> GetPanelHistory(string nomPanneau, int limitJours = 7)
        {
            var list = new List<PanelHistory>();
            using (MySqlConnection conn = new MySqlConnection(connectionString))
            {
                conn.Open();
                string sql = @"
                    SELECT nom_panneau, tension, statut, horodatage
                    FROM historique_panneaux
                    WHERE nom_panneau = @n
                      AND horodatage >= DATE_SUB(NOW(), INTERVAL @j DAY)
                    ORDER BY horodatage DESC";
                MySqlCommand cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@n", nomPanneau);
                cmd.Parameters.AddWithValue("@j", limitJours);
                using (MySqlDataReader rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        list.Add(new PanelHistory
                        {
                            NomPanneau = rdr["nom_panneau"].ToString(),
                            Tension = rdr["tension"] != DBNull.Value ? Convert.ToDouble(rdr["tension"]) : (double?)null,
                            Statut = rdr["statut"].ToString(),
                            Horodatage = Convert.ToDateTime(rdr["horodatage"])
                        });
                    }
                }
            }
            return list;
        }

        // Ajouter un nouveau panneau dans MySQL
        public void AddPanel(SolarPanel panel)
        {
            using (MySqlConnection conn = new MySqlConnection(connectionString))
            {
                conn.Open();
                string sql = "INSERT INTO panneaux_solaires (nom_panneau, statut, latitude, longitude, tension, derniere_maj) " +
                             "VALUES (@nom, @statut, @lat, @lon, @tension, @maj)";
                MySqlCommand cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@nom", panel.Name);
                cmd.Parameters.AddWithValue("@statut", panel.Status.ToString());
                cmd.Parameters.AddWithValue("@lat", panel.Latitude);
                cmd.Parameters.AddWithValue("@lon", panel.Longitude);
                cmd.Parameters.AddWithValue("@tension", panel.Voltage.HasValue ? panel.Voltage.Value : 0);
                cmd.Parameters.AddWithValue("@maj", panel.LastUpdate);
                cmd.ExecuteNonQuery();
            }
        }

        // Supprimer un panneau depuis MySQL
        public void DeletePanel(string id)
        {
            using (MySqlConnection conn = new MySqlConnection(connectionString))
            {
                conn.Open();
                string sql = "DELETE FROM panneaux_solaires WHERE id=@id";
                MySqlCommand cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
        }

        // Modifier un panneau existant dans MySQL
        public void UpdatePanel(SolarPanel panel)
        {
            using (MySqlConnection conn = new MySqlConnection(connectionString))
            {
                conn.Open();
                string sql = "UPDATE panneaux_solaires SET nom_panneau=@nom, statut=@statut, latitude=@lat, longitude=@lon, derniere_maj=@maj WHERE id=@id";
                MySqlCommand cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@nom", panel.Name);
                cmd.Parameters.AddWithValue("@statut", panel.Status.ToString());
                cmd.Parameters.AddWithValue("@lat", panel.Latitude);
                cmd.Parameters.AddWithValue("@lon", panel.Longitude);
                cmd.Parameters.AddWithValue("@maj", panel.LastUpdate);
                cmd.Parameters.AddWithValue("@id", panel.Id);
                cmd.ExecuteNonQuery();
            }
        }
    }

    /// <summary>
    /// Modèle représentant une entrée d'historique d'un panneau
    /// </summary>
    public class PanelHistory
    {
        public string NomPanneau { get; set; }
        public double? Tension { get; set; }
        public string Statut { get; set; }
        public DateTime Horodatage { get; set; }
    }
}