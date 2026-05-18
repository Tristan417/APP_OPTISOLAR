using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using MaterialSkin.Controls;

namespace APP_OptiSolar
{
    /// <summary>
    /// Page d'affichage des alertes système avec acquittement
    /// </summary>
    public class AlertsPage
    {
        private Panel parentPanel;
        private System.Collections.Generic.List<Alert> alerts;

        // Callback appelé quand l'utilisateur acquitte une alerte
        public Action<Alert> OnAcknowledge { get; set; }

        public AlertsPage(Panel panel)
        {
            parentPanel = panel;
            alerts = new System.Collections.Generic.List<Alert>();
        }

        public void Show(System.Collections.Generic.List<Alert> currentAlerts)
        {
            alerts = currentAlerts;
            parentPanel.Controls.Clear();

            // Scrollable container
            var scrollPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.FromArgb(245, 245, 245)
            };
            parentPanel.Controls.Add(scrollPanel);

            int contentWidth = parentPanel.Width;

            // En-tête
            var lblTitle = new MaterialLabel
            {
                Text = $"🔔 ALERTES ({alerts.Count})",
                Font = new Font("Roboto", 20F, FontStyle.Bold),
                Location = new Point(20, 20),
                AutoSize = true
            };
            scrollPanel.Controls.Add(lblTitle);

            // Bouton Tout acquitter
            if (alerts.Count > 0)
            {
                var btnAckAll = new MaterialButton
                {
                    Text = "✔ TOUT ACQUITTER",
                    Location = new Point(contentWidth - 240, 15),
                    Size = new Size(210, 40),
                    Type = MaterialButton.MaterialButtonType.Outlined
                };
                btnAckAll.Click += (s, e) =>
                {
                    foreach (var alert in alerts.ToList())
                        OnAcknowledge?.Invoke(alert);
                };
                scrollPanel.Controls.Add(btnAckAll);
            }

            // Aucune alerte
            if (alerts.Count == 0)
            {
                var lblNoAlert = new MaterialLabel
                {
                    Text = "✅ Aucune alerte active\n\nTous les panneaux fonctionnent normalement.",
                    Font = new Font("Roboto", 14F),
                    ForeColor = Color.FromArgb(76, 175, 80),
                    Location = new Point(20, 100),
                    Size = new Size(600, 100)
                };
                scrollPanel.Controls.Add(lblNoAlert);
                return;
            }

            // Grouper par gravité
            var criticalAlerts = alerts.Where(a => a.Severity == AlertSeverity.Critical).ToList();
            var warningAlerts = alerts.Where(a => a.Severity == AlertSeverity.Warning).ToList();
            var infoAlerts = alerts.Where(a => a.Severity == AlertSeverity.Info).ToList();

            int yPosition = 80;

            if (criticalAlerts.Count > 0)
            {
                AddGroupLabel(scrollPanel, $"🔴 CRITIQUES ({criticalAlerts.Count})", Color.FromArgb(244, 67, 54), yPosition);
                yPosition += 40;
                foreach (var alert in criticalAlerts)
                {
                    CreateAlertCard(scrollPanel, alert, yPosition, contentWidth);
                    yPosition += 85;
                }
            }

            if (warningAlerts.Count > 0)
            {
                yPosition += 10;
                AddGroupLabel(scrollPanel, $"⚠️ AVERTISSEMENTS ({warningAlerts.Count})", Color.FromArgb(255, 152, 0), yPosition);
                yPosition += 40;
                foreach (var alert in warningAlerts)
                {
                    CreateAlertCard(scrollPanel, alert, yPosition, contentWidth);
                    yPosition += 85;
                }
            }

            if (infoAlerts.Count > 0)
            {
                yPosition += 10;
                AddGroupLabel(scrollPanel, $"ℹ️ INFORMATIONS ({infoAlerts.Count})", Color.FromArgb(33, 150, 243), yPosition);
                yPosition += 40;
                foreach (var alert in infoAlerts)
                {
                    CreateAlertCard(scrollPanel, alert, yPosition, contentWidth);
                    yPosition += 85;
                }
            }
        }

        private void AddGroupLabel(Panel container, string text, Color color, int y)
        {
            var lbl = new MaterialLabel
            {
                Text = text,
                Font = new Font("Roboto", 13F, FontStyle.Bold),
                ForeColor = color,
                Location = new Point(20, y),
                AutoSize = true
            };
            container.Controls.Add(lbl);
        }

        private void CreateAlertCard(Panel container, Alert alert, int yPosition, int containerWidth)
        {
            Color cardColor;
            string icon;

            switch (alert.Severity)
            {
                case AlertSeverity.Critical:
                    cardColor = Color.FromArgb(244, 67, 54);
                    icon = "🔴";
                    break;
                case AlertSeverity.Warning:
                    cardColor = Color.FromArgb(255, 152, 0);
                    icon = "⚠️";
                    break;
                default:
                    cardColor = Color.FromArgb(33, 150, 243);
                    icon = "ℹ️";
                    break;
            }

            var card = new Panel
            {
                Location = new Point(20, yPosition),
                Size = new Size(containerWidth - 55, 75),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            // Bandeau couleur à gauche
            var colorBar = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(5, 75),
                BackColor = cardColor
            };
            card.Controls.Add(colorBar);

            // Titre
            var lblTitle = new Label
            {
                Text = $"{icon} {alert.Title}",
                Font = new Font("Roboto", 11F, FontStyle.Bold),
                Location = new Point(15, 8),
                Size = new Size(card.Width - 165, 22),
                AutoEllipsis = true
            };
            card.Controls.Add(lblTitle);

            // Message
            var lblMessage = new Label
            {
                Text = alert.Message,
                Font = new Font("Roboto", 9F),
                ForeColor = Color.FromArgb(100, 100, 100),
                Location = new Point(15, 32),
                Size = new Size(card.Width - 165, 20),
                AutoEllipsis = true
            };
            card.Controls.Add(lblMessage);

            // Timestamp
            var lblDate = new Label
            {
                Text = alert.Timestamp.ToString("dd/MM/yyyy HH:mm"),
                Font = new Font("Roboto", 8F),
                ForeColor = Color.FromArgb(160, 160, 160),
                Location = new Point(15, 54),
                AutoSize = true
            };
            card.Controls.Add(lblDate);

            // Bouton Acquitter
            var btnAck = new Button
            {
                Text = "✔ Acquitter",
                Location = new Point(card.Width - 135, 21),
                Size = new Size(120, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(240, 240, 240),
                ForeColor = Color.FromArgb(60, 60, 60),
                Font = new Font("Roboto", 9F),
                Cursor = Cursors.Hand
            };
            btnAck.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
            var capturedAlert = alert; // Capture pour le lambda
            btnAck.Click += (s, e) => OnAcknowledge?.Invoke(capturedAlert);
            card.Controls.Add(btnAck);

            container.Controls.Add(card);
        }
    }
}