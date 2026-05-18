using System;
using System.Drawing;
using System.Windows.Forms;

namespace APP_OptiSolar
{
    /// <summary>
    /// Gestionnaire de notifications Windows pour OptiSolar
    /// </summary>
    public class NotificationManager
    {
        private NotifyIcon notifyIcon;
        private Form parentForm;

        public NotificationManager(Form parent)
        {
            parentForm = parent;
            InitializeNotifyIcon();
        }

        private void InitializeNotifyIcon()
        {
            notifyIcon = new NotifyIcon
            {
                Icon = SystemIcons.Information,
                Visible = true,
                Text = "OptiSolar - Gestion Panneaux Solaires"
            };

            // Menu contextuel pour l'icône
            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Ouvrir OptiSolar", null, (s, e) =>
            {
                parentForm.Show();
                parentForm.WindowState = FormWindowState.Normal;
                parentForm.BringToFront();
            });
            contextMenu.Items.Add("-");
            contextMenu.Items.Add("Quitter", null, (s, e) => Application.Exit());

            notifyIcon.ContextMenuStrip = contextMenu;
            notifyIcon.DoubleClick += (s, e) =>
            {
                parentForm.Show();
                parentForm.WindowState = FormWindowState.Normal;
                parentForm.BringToFront();
            };
        }

        /// <summary>
        /// Afficher une notification de panne critique
        /// </summary>
        public void ShowCriticalAlert(string panelName, string message)
        {
            notifyIcon.BalloonTipIcon = ToolTipIcon.Error;
            notifyIcon.BalloonTipTitle = $"🔴 PANNE - {panelName}";
            notifyIcon.BalloonTipText = message;
            notifyIcon.ShowBalloonTip(10000); // 10 secondes
        }

        /// <summary>
        /// Afficher une notification d'avertissement
        /// </summary>
        public void ShowWarning(string panelName, string message)
        {
            notifyIcon.BalloonTipIcon = ToolTipIcon.Warning;
            notifyIcon.BalloonTipTitle = $"⚠️ Avertissement - {panelName}";
            notifyIcon.BalloonTipText = message;
            notifyIcon.ShowBalloonTip(8000); // 8 secondes
        }

        /// <summary>
        /// Afficher une notification d'information
        /// </summary>
        public void ShowInfo(string title, string message)
        {
            notifyIcon.BalloonTipIcon = ToolTipIcon.Info;
            notifyIcon.BalloonTipTitle = $"ℹ️ {title}";
            notifyIcon.BalloonTipText = message;
            notifyIcon.ShowBalloonTip(5000); // 5 secondes
        }

        /// <summary>
        /// Afficher une notification de succès
        /// </summary>
        public void ShowSuccess(string title, string message)
        {
            notifyIcon.BalloonTipIcon = ToolTipIcon.Info;
            notifyIcon.BalloonTipTitle = $"✅ {title}";
            notifyIcon.BalloonTipText = message;
            notifyIcon.ShowBalloonTip(3000); // 3 secondes
        }

        /// <summary>
        /// Vérifier les alertes et envoyer des notifications
        /// </summary>
        public void CheckAlertsAndNotify(System.Collections.Generic.List<Alert> alerts,
                                        System.Collections.Generic.List<Alert> previousAlerts)
        {
            // Comparer avec les alertes précédentes pour ne notifier que les nouvelles
            foreach (var alert in alerts)
            {
                // Vérifier si c'est une nouvelle alerte
                bool isNew = !previousAlerts.Exists(a =>
                    a.PanelId == alert.PanelId &&
                    a.Severity == alert.Severity &&
                    a.Title == alert.Title);

                if (isNew)
                {
                    switch (alert.Severity)
                    {
                        case AlertSeverity.Critical:
                            ShowCriticalAlert(alert.Title, alert.Message);
                            break;
                        case AlertSeverity.Warning:
                            ShowWarning(alert.Title, alert.Message);
                            break;
                        case AlertSeverity.Info:
                            // Ne pas notifier pour les alertes Info (trop de bruit)
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Libérer les ressources
        /// </summary>
        public void Dispose()
        {
            if (notifyIcon != null)
            {
                notifyIcon.Visible = false;
                notifyIcon.Dispose();
            }
        }
    }
}