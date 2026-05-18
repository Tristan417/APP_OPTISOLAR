using System;
using System.Collections.Generic;
using System.Linq;

namespace APP_OptiSolar
{
    /// <summary>
    /// Système de détection et gestion des alertes
    /// </summary>
    public class AlertSystem
    {
        private List<Alert> alerts;
        private HashSet<string> acknowledgedKeys; // Clés des alertes acquittées

        public AlertSystem()
        {
            alerts = new List<Alert>();
            acknowledgedKeys = new HashSet<string>();
        }

        /// <summary>
        /// Clé unique d'une alerte (PanelId + Severity + Title)
        /// </summary>
        private string GetAlertKey(Alert alert)
        {
            return $"{alert.PanelId}|{alert.Severity}|{alert.Title}";
        }

        /// <summary>
        /// Acquitter une alerte — elle disparaît jusqu'au prochain cycle de vérification
        /// </summary>
        public void AcknowledgeAlert(Alert alert)
        {
            acknowledgedKeys.Add(GetAlertKey(alert));
        }

        /// <summary>
        /// Réinitialiser les acquittements (appelé automatiquement à chaque CheckAlerts)
        /// </summary>
        public void ClearAcknowledgements()
        {
            acknowledgedKeys.Clear();
        }

        /// <summary>
        /// Retourne le nombre d'alertes acquittées
        /// </summary>
        public int AcknowledgedCount => acknowledgedKeys.Count;

        /// <summary>
        /// Analyser les panneaux et générer les alertes
        /// </summary>
        public List<Alert> CheckAlerts(List<SolarPanel> panels)
        {
            alerts.Clear();

            foreach (var panel in panels)
            {
                // Alerte 1 : Panneau défectueux
                if (panel.Status == PanelStatus.Defectueux)
                {
                    alerts.Add(new Alert
                    {
                        Title = $"Panneau {panel.Name} DÉFECTUEUX",
                        Message = $"Le panneau est en état défectueux et nécessite une intervention.",
                        Severity = AlertSeverity.Critical,
                        PanelId = panel.Id,
                        Timestamp = DateTime.Now
                    });
                }

                // Alerte 2 : Pas de données depuis 1h
                if ((DateTime.Now - panel.LastUpdate).TotalHours > 1 && panel.Status != PanelStatus.Inactif)
                {
                    alerts.Add(new Alert
                    {
                        Title = $"Aucune donnée - {panel.Name}",
                        Message = $"Dernière mise à jour : {panel.LastUpdate:dd/MM/yyyy HH:mm} (il y a {(DateTime.Now - panel.LastUpdate).TotalHours:F1}h)",
                        Severity = AlertSeverity.Info,
                        PanelId = panel.Id,
                        Timestamp = DateTime.Now
                    });
                }

                // Alerte 4 : Tension anormalement basse (< 8V pour un panneau actif, max attendu 12V)
                if (panel.Status == PanelStatus.Actif && panel.Voltage.HasValue && panel.Voltage < 8)
                {
                    alerts.Add(new Alert
                    {
                        Title = $"Tension faible - {panel.Name}",
                        Message = $"Tension actuelle : {panel.Voltage:F1} V (attendu : > 8V)",
                        Severity = AlertSeverity.Warning,
                        PanelId = panel.Id,
                        Timestamp = DateTime.Now
                    });
                }
            }

            // Retourner uniquement les alertes non acquittées
            return alerts.Where(a => !acknowledgedKeys.Contains(GetAlertKey(a))).ToList();
        }

        /// <summary>
        /// Obtenir le nombre d'alertes par gravité
        /// </summary>
        public Dictionary<AlertSeverity, int> GetAlertCounts()
        {
            return new Dictionary<AlertSeverity, int>
            {
                { AlertSeverity.Critical, alerts.Count(a => a.Severity == AlertSeverity.Critical) },
                { AlertSeverity.Warning, alerts.Count(a => a.Severity == AlertSeverity.Warning) },
                { AlertSeverity.Info, alerts.Count(a => a.Severity == AlertSeverity.Info) }
            };
        }

        /// <summary>
        /// Obtenir toutes les alertes
        /// </summary>
        public List<Alert> GetAllAlerts()
        {
            return alerts;
        }
    }

    /// <summary>
    /// Classe représentant une alerte
    /// </summary>
    public class Alert
    {
        public string Title { get; set; }
        public string Message { get; set; }
        public AlertSeverity Severity { get; set; }
        public string PanelId { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Niveaux de gravité des alertes
    /// </summary>
    public enum AlertSeverity
    {
        Info,       // Information
        Warning,    // Avertissement
        Critical    // Critique
    }
}