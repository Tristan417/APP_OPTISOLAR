using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using MaterialSkin.Controls;

namespace APP_OptiSolar
{
    /// <summary>
    /// Page Dashboard avec statistiques et graphiques de production
    /// </summary>
    public class DashboardPage
    {
        private Panel parentPanel;
        private System.Collections.Generic.List<SolarPanel> solarPanels;

        public DashboardPage(Panel panel, System.Collections.Generic.List<SolarPanel> panels)
        {
            parentPanel = panel;
            solarPanels = panels;
        }

        public void Show()
        {
            parentPanel.Controls.Clear();

            // Titre
            var lblTitle = new MaterialLabel
            {
                Text = "📊 TABLEAU DE BORD",
                Font = new Font("Roboto", 20F, FontStyle.Bold),
                Location = new Point(20, 20),
                AutoSize = true
            };
            parentPanel.Controls.Add(lblTitle);

            // Calculer les statistiques
            int totalPanels = solarPanels.Count;
            int actifCount = solarPanels.Count(p => p.Status == PanelStatus.Actif);
            int inactifCount = solarPanels.Count(p => p.Status == PanelStatus.Inactif);
            int defectueuxCount = solarPanels.Count(p => p.Status == PanelStatus.Defectueux);

            var panelsWithVoltage = solarPanels.Where(p => p.Voltage.HasValue).ToList();
            double avgVoltage = panelsWithVoltage.Any() ? panelsWithVoltage.Average(p => p.Voltage.Value) : 0;

            // ── Ligne 1 : PANNEAUX TOTAL ──
            CreateStatCard("PANNEAUX TOTAL", totalPanels.ToString(), Color.FromArgb(33, 150, 243), 20, 80);

            // ── Ligne 2 : ACTIFS, INACTIFS, DÉFECTUEUX ──
            CreateStatCard("ACTIFS", actifCount.ToString(), Color.FromArgb(76, 175, 80), 20, 220);
            CreateStatCard("INACTIFS", inactifCount.ToString(), Color.FromArgb(158, 158, 158), 230, 220);
            CreateStatCard("DÉFECTUEUX", defectueuxCount.ToString(), Color.FromArgb(244, 67, 54), 440, 220);

            // SECTION : Meilleurs et Pires Panneaux
            int yPosition = 370;

            var lblBest = new MaterialLabel
            {
                Text = "🏆 TOP 3 - MEILLEURS PANNEAUX",
                Font = new Font("Roboto", 14F, FontStyle.Bold),
                Location = new Point(20, yPosition),
                AutoSize = true
            };
            parentPanel.Controls.Add(lblBest);

            var topPanels = solarPanels
                .Where(p => p.Voltage.HasValue && p.Status == PanelStatus.Actif)
                .OrderByDescending(p => p.Voltage)
                .Take(3)
                .ToList();

            yPosition += 40;
            foreach (var panel in topPanels)
            {
                var lblPanel = new MaterialLabel
                {
                    Text = $"✅ {panel.Name} - {panel.Voltage:F1} V",
                    Location = new Point(40, yPosition),
                    AutoSize = true,
                    Font = new Font("Roboto", 11F)
                };
                parentPanel.Controls.Add(lblPanel);
                yPosition += 30;
            }

            // Pires panneaux
            yPosition += 20;
            var lblWorst = new MaterialLabel
            {
                Text = "⚠️ PANNEAUX À SURVEILLER",
                Font = new Font("Roboto", 14F, FontStyle.Bold),
                Location = new Point(20, yPosition),
                AutoSize = true,
                ForeColor = Color.FromArgb(244, 67, 54)
            };
            parentPanel.Controls.Add(lblWorst);

            var worstPanels = solarPanels
                .Where(p => p.Status == PanelStatus.Defectueux)
                .OrderBy(p => p.Voltage ?? 0)
                .Take(3)
                .ToList();

            yPosition += 40;
            foreach (var panel in worstPanels)
            {
                string voltageText = panel.Voltage.HasValue ? $"{panel.Voltage:F1} V" : "N/A";
                var lblPanel = new MaterialLabel
                {
                    Text = $"❌ {panel.Name} - {voltageText} - {panel.StatusText}",
                    Location = new Point(40, yPosition),
                    AutoSize = true,
                    Font = new Font("Roboto", 11F),
                    ForeColor = Color.FromArgb(244, 67, 54)
                };
                parentPanel.Controls.Add(lblPanel);
                yPosition += 30;
            }
        }

        private void CreateStatCard(string title, string value, Color color, int x, int y)
        {
            // Panel carte
            var card = new Panel
            {
                Location = new Point(x, y),
                Size = new Size(180, 120),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            // Bandeau de couleur en haut
            var colorBar = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(180, 5),
                BackColor = color
            };
            card.Controls.Add(colorBar);

            // Titre
            var lblTitle = new Label
            {
                Text = title,
                Font = new Font("Roboto", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 100, 100),
                Location = new Point(10, 15),
                AutoSize = true
            };
            card.Controls.Add(lblTitle);

            // Valeur
            var lblValue = new Label
            {
                Text = value,
                Font = new Font("Roboto", 24F, FontStyle.Bold),
                ForeColor = color,
                Location = new Point(10, 50),
                AutoSize = true
            };
            card.Controls.Add(lblValue);

            parentPanel.Controls.Add(card);
        }

        public void Refresh(System.Collections.Generic.List<SolarPanel> panels)
        {
            solarPanels = panels;
            Show(); // Réafficher avec les nouvelles données
        }
    }
}