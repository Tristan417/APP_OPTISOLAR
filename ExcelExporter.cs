using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace APP_OptiSolar
{
    /// <summary>
    /// Classe pour exporter les données des panneaux en CSV (compatible Excel)
    /// </summary>
    public class ExcelExporter
    {
        /// <summary>
        /// Exporter les panneaux en fichier CSV
        /// </summary>
        public static void ExportToCSV(List<SolarPanel> panels)
        {
            try
            {
                // Demander où sauvegarder
                using (SaveFileDialog sfd = new SaveFileDialog())
                {
                    sfd.Filter = "Fichier CSV|*.csv|Tous les fichiers|*.*";
                    sfd.FileName = $"OptiSolar_Export_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                    sfd.Title = "Exporter les données en CSV";

                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        StringBuilder csv = new StringBuilder();

                        // En-têtes
                        csv.AppendLine("Nom;Statut;Latitude;Longitude;Device ID;Tension (V);Dernière MAJ");

                        // Données
                        foreach (var panel in panels)
                        {
                            csv.AppendLine(string.Format(
                                "{0};{1};{2};{3};{4};{5};{6}",
                                panel.Name,
                                panel.StatusText,
                                panel.Latitude.ToString("F6").Replace(',', '.'),
                                panel.Longitude.ToString("F6").Replace(',', '.'),
                                panel.DeviceId ?? "N/A",
                                panel.Voltage?.ToString("F2").Replace(',', '.') ?? "N/A",
                                panel.LastUpdate.ToString("dd/MM/yyyy HH:mm:ss")
                            ));
                        }

                        // Sauvegarder
                        File.WriteAllText(sfd.FileName, csv.ToString(), Encoding.UTF8);

                        MessageBox.Show(
                            $"✅ Export réussi !\n\n{panels.Count} panneau(x) exporté(s).\n\nFichier : {Path.GetFileName(sfd.FileName)}",
                            "Export CSV",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"❌ Erreur lors de l'export :\n\n{ex.Message}",
                    "Erreur",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        /// <summary>
        /// Exporter un rapport détaillé avec statistiques
        /// </summary>
        public static void ExportReport(List<SolarPanel> panels)
        {
            try
            {
                using (SaveFileDialog sfd = new SaveFileDialog())
                {
                    sfd.Filter = "Fichier CSV|*.csv|Tous les fichiers|*.*";
                    sfd.FileName = $"OptiSolar_Rapport_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                    sfd.Title = "Exporter le rapport";

                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        StringBuilder csv = new StringBuilder();

                        // Titre
                        csv.AppendLine("RAPPORT OPTISOLAR");
                        csv.AppendLine($"Généré le : {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
                        csv.AppendLine();

                        // Statistiques globales
                        csv.AppendLine("=== STATISTIQUES GLOBALES ===");
                        csv.AppendLine($"Nombre total de panneaux;{panels.Count}");
                        csv.AppendLine($"Panneaux actifs;{panels.Count(p => p.Status == PanelStatus.Actif)}");
                        csv.AppendLine($"Panneaux inactifs;{panels.Count(p => p.Status == PanelStatus.Inactif)}");
                        csv.AppendLine($"Panneaux défectueux;{panels.Count(p => p.Status == PanelStatus.Defectueux)}");

                        var panelsWithVoltage = panels.Where(p => p.Voltage.HasValue).ToList();
                        if (panelsWithVoltage.Any())
                        {
                            csv.AppendLine($"Tension moyenne;{panelsWithVoltage.Average(p => p.Voltage.Value):F2} V");
                            csv.AppendLine($"Disponibilité;{(panels.Count(p => p.Status == PanelStatus.Actif) * 100.0 / panels.Count):F0} %");
                        }
                        csv.AppendLine();

                        // Liste détaillée
                        csv.AppendLine("=== LISTE DÉTAILLÉE DES PANNEAUX ===");
                        csv.AppendLine("Nom;Statut;Latitude;Longitude;Device ID;Tension (V);Dernière MAJ");

                        foreach (var panel in panels.OrderBy(p => p.Name))
                        {
                            csv.AppendLine(string.Format(
                                "{0};{1};{2};{3};{4};{5};{6}",
                                panel.Name,
                                panel.StatusText,
                                panel.Latitude.ToString("F6").Replace(',', '.'),
                                panel.Longitude.ToString("F6").Replace(',', '.'),
                                panel.DeviceId ?? "N/A",
                                panel.Voltage?.ToString("F2").Replace(',', '.') ?? "N/A",
                                panel.LastUpdate.ToString("dd/MM/yyyy HH:mm:ss")
                            ));
                        }

                        File.WriteAllText(sfd.FileName, csv.ToString(), Encoding.UTF8);

                        MessageBox.Show(
                            $"✅ Rapport généré avec succès !\n\nFichier : {Path.GetFileName(sfd.FileName)}",
                            "Rapport",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"❌ Erreur lors de la génération du rapport :\n\n{ex.Message}",
                    "Erreur",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }
    }
}