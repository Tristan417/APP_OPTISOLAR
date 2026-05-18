using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace APP_OptiSolar
{
    // ─────────────────────────────────────────────
    //  Modèles de données
    // ─────────────────────────────────────────────

    /// <summary>Point de données pour un panneau à un instant T</summary>
    public class PanelDataPoint
    {
        public DateTime Timestamp { get; set; }
        public double? Voltage { get; set; }
        public PanelStatus Status { get; set; }
    }

    /// <summary>Métrique affichable sur le graphique</summary>
    public enum ChartMetric { Voltage }

    // ─────────────────────────────────────────────
    //  Gestionnaire de l'historique
    // ─────────────────────────────────────────────

    public class ProductionHistory
    {
        private const int MAX_POINTS = 200;

        // panelId → liste de points
        private readonly Dictionary<string, List<PanelDataPoint>> _panelHistory
            = new Dictionary<string, List<PanelDataPoint>>();

        // panelId → nom (pour l'affichage)
        private readonly Dictionary<string, string> _panelNames
            = new Dictionary<string, string>();

        // ── Mémorisation des filtres ─────────────────
        private int _lastPanelIndex = 0;
        private int _lastMetricIndex = 0;
        private int _lastRangeIndex = 1;

        // ── Champs partagés pour le refresh timer ────
        private Action _redrawAction = null;
        private List<SolarPanel> _currentPanels = null;

        // Palette de couleurs pour les courbes
        private static readonly Color[] CURVE_COLORS =
        {
            Color.FromArgb( 33, 150, 243),
            Color.FromArgb( 76, 175,  80),
            Color.FromArgb(244,  67,  54),
            Color.FromArgb(255, 152,   0),
            Color.FromArgb(156,  39, 176),
            Color.FromArgb(  0, 188, 212),
            Color.FromArgb(255, 193,   7),
            Color.FromArgb(233,  30,  99),
        };

        // ── Couleurs UI ─────────────────────────────
        private static readonly Color BG = Color.FromArgb(245, 246, 250);
        private static readonly Color CARD_BG = Color.White;
        private static readonly Color BORDER = Color.FromArgb(220, 222, 235);
        private static readonly Color TEXT_DARK = Color.FromArgb(30, 30, 50);
        private static readonly Color TEXT_MUTED = Color.FromArgb(120, 120, 145);

        // Marges internes du graphique — identiques dans DrawMultiChart et MouseMove
        private const int PAD_L = 70, PAD_R = 25, PAD_T = 20, PAD_B = 50;

        // ── Enregistrement ──────────────────────────

        public void Record(List<SolarPanel> panels)
        {
            foreach (var panel in panels)
            {
                if (!_panelHistory.ContainsKey(panel.Id))
                    _panelHistory[panel.Id] = new List<PanelDataPoint>();

                _panelNames[panel.Id] = panel.Name;

                _panelHistory[panel.Id].Add(new PanelDataPoint
                {
                    Timestamp = DateTime.Now,
                    Voltage = panel.Voltage,
                    Status = panel.Status
                });

                if (_panelHistory[panel.Id].Count > MAX_POINTS)
                    _panelHistory[panel.Id].RemoveAt(0);
            }

            _currentPanels = panels;
            _redrawAction?.Invoke();
        }

        public int TotalPanelsTracked => _panelHistory.Count;

        // ── Point d'entrée principal ─────────────────

        public void ShowChart(Panel parentPanel, List<SolarPanel> panels)
        {
            _currentPanels = panels;

            parentPanel.Controls.Clear();
            parentPanel.BackColor = BG;

            var scroll = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = BG
            };
            parentPanel.Controls.Add(scroll);

            int W = parentPanel.Width - 20;
            int y = 0;

            y = BuildHeader(scroll, W, panels, y);
            y = BuildChartSection(scroll, W, panels, y);
            BuildDataTable(scroll, W, panels, y);
        }

        // ═══════════════════════════════════════════
        //  SECTION 1 — En-tête
        // ═══════════════════════════════════════════

        private int BuildHeader(Panel container, int W, List<SolarPanel> panels, int startY)
        {
            int y = startY + 20;

            container.Controls.Add(new Label
            {
                Text = "📈  HISTORIQUE DE PRODUCTION",
                Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                ForeColor = TEXT_DARK,
                Location = new Point(20, y),
                AutoSize = true,
                BackColor = Color.Transparent
            });

            container.Controls.Add(new Label
            {
                Text = panels.Any()
                    ? $"Dernière mise à jour : {panels.Max(p => p.LastUpdate):dd/MM/yyyy HH:mm:ss}"
                    : "Aucun panneau configuré",
                Font = new Font("Segoe UI", 9F),
                ForeColor = TEXT_MUTED,
                Location = new Point(22, y + 36),
                AutoSize = true,
                BackColor = Color.Transparent
            });

            container.Controls.Add(new Panel
            {
                Location = new Point(20, y + 62),
                Size = new Size(W - 20, 1),
                BackColor = BORDER
            });

            return y + 75;
        }

        // ═══════════════════════════════════════════
        //  SECTION 2 — Graphique
        // ═══════════════════════════════════════════

        private int BuildChartSection(Panel container, int W, List<SolarPanel> panels, int startY)
        {
            int y = startY + 16;

            container.Controls.Add(new Label
            {
                Text = "GRAPHIQUE",
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = TEXT_MUTED,
                Location = new Point(20, y),
                AutoSize = true,
                BackColor = Color.Transparent
            });
            y += 22;

            // ── Barre de contrôles ───────────────────
            var ctrlCard = new Panel
            {
                Location = new Point(20, y),
                Size = new Size(W - 20, 56),
                BackColor = CARD_BG
            };
            ctrlCard.Paint += PaintRoundedPanel;
            container.Controls.Add(ctrlCard);

            AddCtrlLabel(ctrlCard, "Panneau :", 14, 18);
            var cmbPanel = new ComboBox
            {
                Location = new Point(86, 16),
                Size = new Size(210, 28),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9.5F),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(248, 249, 252)
            };
            cmbPanel.Items.Add("🌐  Tous les panneaux");
            foreach (var p in panels)
                cmbPanel.Items.Add($"   {p.Name}");
            cmbPanel.SelectedIndex = Math.Min(_lastPanelIndex, cmbPanel.Items.Count - 1);
            ctrlCard.Controls.Add(cmbPanel);

            AddCtrlLabel(ctrlCard, "Plage :", 315, 18);
            var cmbRange = new ComboBox
            {
                Location = new Point(375, 16),
                Size = new Size(175, 28),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9.5F),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(248, 249, 252)
            };
            cmbRange.Items.Add("10 derniers points");
            cmbRange.Items.Add("30 derniers points");
            cmbRange.Items.Add("60 derniers points");
            cmbRange.Items.Add("Tout l'historique");
            cmbRange.SelectedIndex = _lastRangeIndex;
            ctrlCard.Controls.Add(cmbRange);

            y += 66;

            // ── Légende ───────────────────────────────
            var legendPanel = new Panel
            {
                Location = new Point(20, y),
                Size = new Size(W - 20, 30),
                BackColor = Color.Transparent
            };
            container.Controls.Add(legendPanel);
            y += 34;

            // ── Zone graphique ────────────────────────
            const int chartHeight = 420;
            var chartCard = new DoubleBufferedPanel
            {
                Location = new Point(20, y),
                Size = new Size(W - 20, chartHeight),
                BackColor = CARD_BG
            };
            container.Controls.Add(chartCard);

            // ── Barre de stats ────────────────────────
            y += chartHeight + 8;
            var statsBar = new Label
            {
                Location = new Point(20, y),
                Size = new Size(W - 20, 32),
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = TEXT_MUTED,
                BackColor = CARD_BG,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0),
                Text = "—"
            };
            statsBar.Paint += PaintRoundedPanel;
            container.Controls.Add(statsBar);

            // ── Message d'attente si pas de données ──
            if (_panelHistory.Count == 0 || _panelHistory.Values.All(h => h.Count < 1))
            {
                chartCard.Controls.Add(new Label
                {
                    Text = "⏳  Collecte des données en cours…\n\nL'historique se remplit automatiquement toutes les 30 secondes.",
                    Font = new Font("Segoe UI", 11F),
                    ForeColor = TEXT_MUTED,
                    Location = new Point(0, 0),
                    Size = chartCard.Size,
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = Color.Transparent
                });
                chartCard.Paint += (s, e) => PaintChartBackground(e.Graphics, chartCard);
                statsBar.Visible = false;
                return y + chartHeight + 8;
            }

            // ══════════════════════════════════════════════════════
            //  État partagé — capturé par les lambdas Paint/Mouse
            // ══════════════════════════════════════════════════════
            var currentSeries = new List<(string label, Color color, List<(DateTime t, double v)> pts)>();

            bool tipVisible = false;
            Point tipMousePos = Point.Empty;
            string tipTime = "";
            var tipLines = new List<(string label, Color color, double value)>();

            // ── Handler Paint UNIQUE ──────────────────
            // Jamais ajouté plusieurs fois : on l'enregistre une seule fois ici.
            chartCard.Paint += (s, e) =>
            {
                var g = e.Graphics;

                // 1. Fond arrondi
                PaintChartBackground(g, chartCard);

                // 2. Graphique
                DrawMultiChart(g, chartCard.Size, currentSeries);

                // 3. Overlay tooltip + ligne de croisée
                if (tipVisible && currentSeries.Count > 0)
                {
                    int cW2 = chartCard.Width - PAD_L - PAD_R;
                    int cH2 = chartCard.Height - PAD_T - PAD_B;
                    int mx = tipMousePos.X;

                    if (mx >= PAD_L && mx <= PAD_L + cW2)
                    {
                        using var crossPen = new Pen(Color.FromArgb(130, 150, 150, 170), 1f)
                        { DashStyle = DashStyle.Dash };
                        g.DrawLine(crossPen, mx, PAD_T, mx, PAD_T + cH2);
                    }

                    if (tipLines.Count > 0)
                        DrawTooltip(g, chartCard.Size, tipMousePos, tipTime, tipLines);
                }
            };

            // ── MouseMove ─────────────────────────────
            chartCard.MouseMove += (s, e) =>
            {
                int cW = chartCard.Width - PAD_L - PAD_R;
                int cH = chartCard.Height - PAD_T - PAD_B;

                bool inZone = currentSeries.Count > 0
                    && !currentSeries.All(sr => sr.pts.Count < 2)
                    && e.X >= PAD_L && e.X <= PAD_L + cW
                    && e.Y >= PAD_T && e.Y <= PAD_T + cH;

                if (!inZone)
                {
                    if (tipVisible) { tipVisible = false; chartCard.Invalidate(); }
                    return;
                }

                tipMousePos = e.Location;

                DateTime tMin = currentSeries.SelectMany(sr => sr.pts).Min(p => p.t);
                DateTime tMax = currentSeries.SelectMany(sr => sr.pts).Max(p => p.t);
                double tRange = (tMax - tMin).TotalSeconds;
                if (tRange <= 0) { tipVisible = false; chartCard.Invalidate(); return; }

                double ratio = (double)(e.X - PAD_L) / cW;
                DateTime tCursor = tMin.AddSeconds(ratio * tRange);
                tipTime = tCursor.ToString("HH:mm:ss");

                tipLines.Clear();
                foreach (var (label, color, pts) in currentSeries)
                {
                    if (pts.Count == 0) continue;

                    // Interpolation linéaire entre les deux points encadrants
                    int idx = pts.FindIndex(p => p.t >= tCursor);
                    double val;
                    if (idx <= 0)
                        val = pts[0].v;
                    else if (idx >= pts.Count)
                        val = pts[pts.Count - 1].v;
                    else
                    {
                        var p0 = pts[idx - 1];
                        var p1 = pts[idx];
                        double seg = (p1.t - p0.t).TotalSeconds;
                        double t = seg > 0 ? (tCursor - p0.t).TotalSeconds / seg : 0;
                        val = p0.v + t * (p1.v - p0.v);
                    }
                    tipLines.Add((label, color, val));
                }

                tipVisible = tipLines.Count > 0;
                chartCard.Invalidate();
            };

            chartCard.MouseLeave += (s, e) =>
            {
                if (tipVisible) { tipVisible = false; chartCard.Invalidate(); }
            };

            // ── Redraw ────────────────────────────────
            Action redraw = () =>
            {
                if (cmbPanel.IsDisposed || cmbRange.IsDisposed) return;

                var activePanels = _currentPanels ?? panels;
                int panelIdx = cmbPanel.SelectedIndex;
                int rangeCount = cmbRange.SelectedIndex switch
                {
                    0 => 10,
                    1 => 30,
                    2 => 60,
                    _ => MAX_POINTS
                };

                currentSeries = BuildSeries(activePanels, panelIdx, rangeCount);

                if (!legendPanel.IsDisposed) UpdateLegend(legendPanel, currentSeries);
                if (!chartCard.IsDisposed) chartCard.Invalidate();
                if (!statsBar.IsDisposed) statsBar.Text = BuildStatsText(currentSeries);
            };

            cmbPanel.SelectedIndexChanged += (s, e) => { _lastPanelIndex = cmbPanel.SelectedIndex; redraw(); };
            cmbRange.SelectedIndexChanged += (s, e) => { _lastRangeIndex = cmbRange.SelectedIndex; redraw(); };

            _redrawAction = redraw;
            redraw();

            return y + 42;
        }

        // ═══════════════════════════════════════════
        //  SECTION 3 — Tableau de données récentes
        // ═══════════════════════════════════════════

        private void BuildDataTable(Panel container, int W, List<SolarPanel> panels, int startY)
        {
            if (!_panelHistory.Any()) return;

            int y = startY + 8;

            container.Controls.Add(new Label
            {
                Text = "DERNIÈRES MESURES",
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = TEXT_MUTED,
                Location = new Point(20, y),
                AutoSize = true,
                BackColor = Color.Transparent
            });
            y += 22;

            var tableCard = new Panel
            {
                Location = new Point(20, y),
                Size = new Size(W - 20, 0),
                BackColor = CARD_BG
            };
            tableCard.Paint += PaintRoundedPanel;
            container.Controls.Add(tableCard);

            int[] colWidths = { 200, 140, 120, 120 };
            string[] headers = { "Panneau", "Date/Heure", "Tension (V)", "Statut" };
            int rowHeight = 34;
            int tableY = 0;

            // En-tête
            var headerRow = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(tableCard.Width, rowHeight),
                BackColor = Color.FromArgb(248, 249, 252)
            };
            tableCard.Controls.Add(headerRow);
            int hx = 0;
            for (int i = 0; i < headers.Length; i++)
            {
                headerRow.Controls.Add(new Label
                {
                    Text = headers[i].ToUpper(),
                    Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                    ForeColor = TEXT_MUTED,
                    Location = new Point(hx + 10, 0),
                    Size = new Size(colWidths[i], rowHeight),
                    TextAlign = ContentAlignment.MiddleLeft,
                    BackColor = Color.Transparent
                });
                hx += colWidths[i];
            }
            tableY += rowHeight;

            // Lignes — une seule ligne par panneau (dernière mesure)
            var recentRows = _panelHistory
                .Where(kv => kv.Value.Count > 0)
                .Select(kv => new
                {
                    PanelName = _panelNames.TryGetValue(kv.Key, out var n) ? n : kv.Key,
                    DataPoint = kv.Value.Last()
                })
                .OrderBy(r => r.PanelName)
                .ToList();

            bool alternate = false;
            foreach (var row in recentRows)
            {
                var pt = row.DataPoint;
                Color rowBg = alternate ? Color.FromArgb(252, 252, 255) : CARD_BG;
                alternate = !alternate;

                var dataRow = new Panel
                {
                    Location = new Point(0, tableY),
                    Size = new Size(tableCard.Width, rowHeight),
                    BackColor = rowBg
                };
                int capturedH = rowHeight;
                dataRow.Paint += (s, e) =>
                    e.Graphics.DrawLine(new Pen(BORDER, 1f), 0, capturedH - 1, ((Panel)s).Width, capturedH - 1);

                Color statusColor = pt.Status switch
                {
                    PanelStatus.Actif => Color.FromArgb(76, 175, 80),
                    PanelStatus.Defectueux => Color.FromArgb(244, 67, 54),
                    _ => Color.FromArgb(158, 158, 158)
                };
                string statusText = pt.Status switch
                {
                    PanelStatus.Actif => "✅ Actif",
                    PanelStatus.Defectueux => "❌ Défectueux",
                    _ => "⬛ Inactif"
                };

                string[] cellValues =
                {
                    row.PanelName,
                    pt.Timestamp.ToString("dd/MM HH:mm:ss"),
                    pt.Voltage.HasValue ? $"{pt.Voltage:F2} V" : "—",
                    statusText
                };

                int cx = 0;
                for (int i = 0; i < cellValues.Length; i++)
                {
                    Color fg = (i == 3) ? statusColor : TEXT_DARK;
                    dataRow.Controls.Add(new Label
                    {
                        Text = cellValues[i],
                        Font = new Font("Segoe UI", 9F, i == 0 ? FontStyle.Bold : FontStyle.Regular),
                        ForeColor = fg,
                        Location = new Point(cx + 10, 0),
                        Size = new Size(colWidths[i], rowHeight),
                        TextAlign = ContentAlignment.MiddleLeft,
                        BackColor = Color.Transparent
                    });
                    cx += colWidths[i];
                }

                tableCard.Controls.Add(dataRow);
                tableY += rowHeight;
            }

            if (recentRows.Count == 0)
            {
                tableCard.Controls.Add(new Label
                {
                    Text = "Aucune donnée disponible pour le moment.",
                    Font = new Font("Segoe UI", 10F),
                    ForeColor = TEXT_MUTED,
                    Location = new Point(0, tableY),
                    Size = new Size(tableCard.Width, 50),
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = Color.Transparent
                });
                tableY += 50;
            }

            tableCard.Size = new Size(tableCard.Width, tableY + 10);

            container.Controls.Add(new Label
            {
                Text = $"  {recentRows.Count} entrées affichées  •  {_panelHistory.Sum(kv => kv.Value.Count)} mesures totales enregistrées",
                Font = new Font("Segoe UI", 8F),
                ForeColor = TEXT_MUTED,
                Location = new Point(20, y + tableY + 18),
                AutoSize = true,
                BackColor = Color.Transparent
            });
        }

        // ═══════════════════════════════════════════
        //  Construction des séries
        // ═══════════════════════════════════════════

        private List<(string label, Color color, List<(DateTime t, double v)> pts)>
            BuildSeries(List<SolarPanel> panels, int panelIdx, int rangeCount)
        {
            var result = new List<(string, Color, List<(DateTime, double)>)>();

            if (panelIdx == 0)
            {
                // Vue globale : tous les panneaux qui ont au moins 1 point avec une valeur
                int colorIdx = 0;
                foreach (var panel in panels)
                {
                    if (!_panelHistory.TryGetValue(panel.Id, out var hist) || hist.Count < 1)
                        continue;
                    var pts = ExtractPoints(hist.TakeLast(rangeCount).ToList());
                    if (pts.Count < 1) continue; // aucune valeur Voltage enregistrée
                    result.Add((panel.Name, CURVE_COLORS[colorIdx % CURVE_COLORS.Length], pts));
                    colorIdx++;
                }
            }
            else
            {
                // Vue individuelle : panneau sélectionné
                if (panelIdx - 1 >= panels.Count) return result;
                var sel = panels[panelIdx - 1];
                if (!_panelHistory.TryGetValue(sel.Id, out var hist) || hist.Count < 1)
                    return result;
                var pts = ExtractPoints(hist.TakeLast(rangeCount).ToList());
                if (pts.Count < 1) return result;
                result.Add(($"{sel.Name} — Tension (V)", CURVE_COLORS[0], pts));
            }

            return result;
        }

        private List<(DateTime t, double v)> ExtractPoints(List<PanelDataPoint> hist)
        {
            var result = new List<(DateTime, double)>();
            foreach (var p in hist)
                if (p.Voltage.HasValue)
                    result.Add((p.Timestamp, p.Voltage.Value));
            return result;
        }

        // ═══════════════════════════════════════════
        //  Dessin — fond arrondi
        // ═══════════════════════════════════════════

        private static void PaintChartBackground(Graphics g, Panel chartCard)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = RoundedRect(chartCard.ClientRectangle, 8);
            using var bg = new SolidBrush(CARD_BG);
            using var border = new Pen(BORDER, 1f);
            g.FillPath(bg, path);
            g.DrawPath(border, path);
        }

        // ═══════════════════════════════════════════
        //  Dessin — graphique
        // ═══════════════════════════════════════════

        private static void DrawMultiChart(
            Graphics g, Size size,
            List<(string label, Color color, List<(DateTime t, double v)> pts)> series)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int cW = size.Width - PAD_L - PAD_R;
            int cH = size.Height - PAD_T - PAD_B;

            if (series.Count == 0 || series.All(s => s.pts.Count < 1))
            {
                using var br = new SolidBrush(TEXT_MUTED);
                using var f = new Font("Segoe UI", 11F);
                const string msg = "Aucune donnée disponible pour cette sélection.";
                var sz = g.MeasureString(msg, f);
                g.DrawString(msg, f, br, (size.Width - sz.Width) / 2, (size.Height - sz.Height) / 2);
                return;
            }

            double allMax = series.SelectMany(s => s.pts).Max(p => p.v);
            double allMin = series.SelectMany(s => s.pts).Min(p => p.v);
            if (allMax == allMin) { allMax += 10; allMin = Math.Max(0, allMin - 5); }

            double step = NiceStep(allMax - allMin);
            double niceMax = Math.Ceiling(allMax / step) * step;
            double niceMin = Math.Max(0, Math.Floor(allMin / step) * step);
            double niceRange = niceMax - niceMin;
            if (niceRange <= 0) niceRange = 10;

            DateTime tMin = series.SelectMany(s => s.pts).Min(p => p.t);
            DateTime tMax = series.SelectMany(s => s.pts).Max(p => p.t);
            double tRange = (tMax - tMin).TotalSeconds;
            if (tRange <= 0) tRange = 1;

            using var gridPen = new Pen(Color.FromArgb(238, 238, 245), 1f);
            using var axisPen = new Pen(Color.FromArgb(200, 200, 215), 1.5f);
            using var lblBrush = new SolidBrush(TEXT_MUTED);
            using var lblFont = new Font("Segoe UI", 7.5F);
            using var unitFont = new Font("Segoe UI", 7.5F, FontStyle.Bold);

            for (int i = 0; i <= 6; i++)
            {
                double val = niceMin + niceRange * i / 6;
                int gy = PAD_T + cH - (int)(cH * (val - niceMin) / niceRange);
                g.DrawLine(gridPen, PAD_L, gy, PAD_L + cW, gy);
                string lbl = $"{val:F1}";
                var sz = g.MeasureString(lbl, lblFont);
                g.DrawString(lbl, lblFont, lblBrush, PAD_L - sz.Width - 4, gy - sz.Height / 2);
            }

            g.DrawString("V", unitFont, lblBrush, 4, PAD_T - 2);
            g.DrawLine(axisPen, PAD_L, PAD_T, PAD_L, PAD_T + cH);
            g.DrawLine(axisPen, PAD_L, PAD_T + cH, PAD_L + cW, PAD_T + cH);

            foreach (var (label, color, pts) in series)
            {
                if (pts.Count < 1) continue;

                // Cas 1 seul point : afficher un simple point avec sa valeur
                if (pts.Count == 1)
                {
                    float sx = PAD_L + (float)(cW * 0.5); // centré horizontalement
                    float sy = PAD_T + cH - (float)(cH * (pts[0].v - niceMin) / niceRange);
                    using var dotBrush2 = new SolidBrush(color);
                    using var dotOutline2 = new Pen(Color.White, 1.5f);
                    g.FillEllipse(dotBrush2, sx - 5f, sy - 5f, 10, 10);
                    g.DrawEllipse(dotOutline2, sx - 5f, sy - 5f, 10, 10);
                    using var valFont2 = new Font("Segoe UI", 7.5F, FontStyle.Bold);
                    using var valBrush2 = new SolidBrush(color);
                    string v2 = $"{pts[0].v:F1}";
                    var sz2 = g.MeasureString(v2, valFont2);
                    g.DrawString(v2, valFont2, valBrush2, sx - sz2.Width / 2, sy - sz2.Height - 6);
                    continue;
                }

                var linePoints = pts.Select(p => new PointF(
                    PAD_L + (float)(cW * (p.t - tMin).TotalSeconds / tRange),
                    PAD_T + cH - (float)(cH * (p.v - niceMin) / niceRange)
                )).ToArray();

                var areaList = new List<PointF> { new PointF(linePoints[0].X, PAD_T + cH) };
                areaList.AddRange(linePoints);
                areaList.Add(new PointF(linePoints[linePoints.Length - 1].X, PAD_T + cH));

                using var areaBrush = new LinearGradientBrush(
                    new Point(0, PAD_T), new Point(0, PAD_T + cH),
                    Color.FromArgb(45, color), Color.FromArgb(3, color));
                g.FillPolygon(areaBrush, areaList.ToArray());

                using var pen = new Pen(color, 2.2f) { LineJoin = LineJoin.Round };
                g.DrawLines(pen, linePoints);

                using var dotBrush = new SolidBrush(color);
                using var dotOutline = new Pen(Color.White, 1.5f);
                foreach (var lp in linePoints)
                {
                    g.FillEllipse(dotBrush, lp.X - 3.5f, lp.Y - 3.5f, 7, 7);
                    g.DrawEllipse(dotOutline, lp.X - 3.5f, lp.Y - 3.5f, 7, 7);
                }

                if (linePoints.Length > 0)
                {
                    var last = linePoints[linePoints.Length - 1];
                    string valLbl = $"{pts[pts.Count - 1].v:F1}";
                    using var valFont = new Font("Segoe UI", 7.5F, FontStyle.Bold);
                    using var valBrush = new SolidBrush(color);
                    var valSz = g.MeasureString(valLbl, valFont);
                    g.DrawString(valLbl, valFont, valBrush,
                        Math.Min(last.X - valSz.Width / 2, PAD_L + cW - valSz.Width),
                        Math.Max(PAD_T, last.Y - valSz.Height - 4));
                }
            }

            var allTimes = series.SelectMany(s => s.pts.Select(p => p.t)).OrderBy(t => t).ToList();
            int stepX = Math.Max(1, allTimes.Count / Math.Min(8, allTimes.Count));
            var shown = new HashSet<string>();

            for (int i = 0; i < allTimes.Count; i += stepX)
            {
                var t = allTimes[i];
                float x = PAD_L + (float)(cW * (t - tMin).TotalSeconds / tRange);
                string lbl = t.ToString("HH:mm:ss");
                if (!shown.Add(lbl)) continue;
                var sz = g.MeasureString(lbl, lblFont);
                float lx = Math.Max(PAD_L, Math.Min(x - sz.Width / 2, PAD_L + cW - sz.Width));
                g.DrawString(lbl, lblFont, lblBrush, lx, PAD_T + cH + 6);
            }
            if (allTimes.Count > 0)
            {
                var t = allTimes[allTimes.Count - 1];
                float x = PAD_L + (float)(cW * (t - tMin).TotalSeconds / tRange);
                string lbl = t.ToString("HH:mm:ss");
                var sz = g.MeasureString(lbl, lblFont);
                float lx = Math.Max(PAD_L, Math.Min(x - sz.Width / 2, PAD_L + cW - sz.Width));
                g.DrawString(lbl, lblFont, lblBrush, lx, PAD_T + cH + 6);
            }
        }

        // ═══════════════════════════════════════════
        //  Dessin — tooltip GDI+
        // ═══════════════════════════════════════════

        private static void DrawTooltip(
            Graphics g, Size chartSize,
            Point mousePos, string time,
            List<(string label, Color color, double value)> lines)
        {
            const string unit = "V";
            using var tipFont = new Font("Segoe UI", 9F);
            using var tipFontBold = new Font("Segoe UI", 8.5F, FontStyle.Bold);

            int tipW = 160;
            foreach (var (lbl, _, val) in lines)
            {
                int w = TextRenderer.MeasureText($"{lbl}: {val:F3} {unit}", tipFont).Width + 44;
                if (w > tipW) tipW = w;
            }

            int lineH = 22;
            int tipH = 36 + lines.Count * lineH + 6;

            int tx = mousePos.X + 14;
            if (tx + tipW > chartSize.Width - PAD_R) tx = mousePos.X - tipW - 10;
            int ty = mousePos.Y - tipH / 2;
            ty = Math.Max(PAD_T, Math.Min(ty, chartSize.Height - PAD_B - tipH));

            g.SmoothingMode = SmoothingMode.AntiAlias;

            using var bgBrush = new SolidBrush(Color.FromArgb(225, 25, 25, 45));
            using var tipPath = RoundedRect(new Rectangle(tx, ty, tipW, tipH), 6);
            g.FillPath(bgBrush, tipPath);

            using var timeBrush = new SolidBrush(Color.FromArgb(200, 200, 220));
            g.DrawString(time, tipFontBold, timeBrush, tx + 10, ty + 8);

            using var sepPen = new Pen(Color.FromArgb(80, 80, 100));
            g.DrawLine(sepPen, tx + 8, ty + 27, tx + tipW - 8, ty + 27);

            int ly = ty + 33;
            foreach (var (lbl, color, val) in lines)
            {
                using var dotBrush = new SolidBrush(color);
                g.FillEllipse(dotBrush, tx + 10, ly + 5, 8, 8);
                using var valBrush = new SolidBrush(Color.White);
                g.DrawString($"{lbl}: {val:F3} {unit}", tipFont, valBrush, tx + 24, ly);
                ly += lineH;
            }
        }

        // ═══════════════════════════════════════════
        //  Légende
        // ═══════════════════════════════════════════

        private void UpdateLegend(
            Panel legendPanel,
            List<(string label, Color color, List<(DateTime t, double v)> pts)> series)
        {
            legendPanel.Controls.Clear();
            int x = 0;
            foreach (var (label, color, _) in series)
            {
                legendPanel.Controls.Add(new Panel
                {
                    Location = new Point(x, 7),
                    Size = new Size(14, 14),
                    BackColor = color
                });
                x += 20;

                var lbl = new Label
                {
                    Text = label,
                    Font = new Font("Segoe UI", 9F),
                    ForeColor = TEXT_DARK,
                    Location = new Point(x, 6),
                    AutoSize = true,
                    BackColor = Color.Transparent
                };
                legendPanel.Controls.Add(lbl);
                x += lbl.PreferredWidth + 22;
            }
        }

        // ═══════════════════════════════════════════
        //  Barre de statistiques
        // ═══════════════════════════════════════════

        private string BuildStatsText(
            List<(string label, Color color, List<(DateTime t, double v)> pts)> series)
        {
            if (series.Count == 0 || series.All(s => s.pts.Count == 0))
                return "  Aucune donnée";

            var allValues = series.SelectMany(s => s.pts.Select(p => p.v)).ToList();
            if (!allValues.Any()) return "—";

            double max = allValues.Max();
            double min = allValues.Min();
            double avg = allValues.Average();
            int cnt = allValues.Count;

            string perSerie = series.Count > 1
                ? "   |   " + string.Join("   ", series
                    .Where(s => s.pts.Count > 0)
                    .Select(s => $"{s.label}: moy {s.pts.Average(p => p.v):F1} V"))
                : "";

            return $"  Max : {max:F1} V   •   Min : {min:F1} V   •   Moyenne : {avg:F1} V   •   {cnt} points{perSerie}";
        }

        // ═══════════════════════════════════════════
        //  Helpers statiques
        // ═══════════════════════════════════════════

        private static double NiceStep(double range)
        {
            if (range <= 0) return 10;
            if (range <= 10) return 2;
            if (range <= 50) return 10;
            if (range <= 200) return 50;
            return 100;
        }

        private static void AddCtrlLabel(Panel parent, string text, int x, int y)
        {
            parent.Controls.Add(new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 9F),
                ForeColor = TEXT_MUTED,
                Location = new Point(x, y),
                AutoSize = true,
                BackColor = Color.Transparent
            });
        }

        private static void PaintRoundedPanel(object sender, PaintEventArgs e)
        {
            var p = sender as Control;
            if (p == null) return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = RoundedRect(p.ClientRectangle, 8);
            using var bg = new SolidBrush(p.BackColor);
            e.Graphics.FillPath(bg, path);
            using var border = new Pen(BORDER, 1f);
            e.Graphics.DrawPath(border, path);
        }

        private static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, radius * 2, radius * 2, 180, 90);
            path.AddArc(r.Right - radius * 2, r.Y, radius * 2, radius * 2, 270, 90);
            path.AddArc(r.Right - radius * 2, r.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
            path.AddArc(r.X, r.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    /// <summary>
    /// Panel avec double buffering activé — élimine le scintillement lors des
    /// repaints fréquents causés par MouseMove + Invalidate().
    /// </summary>
    internal class DoubleBufferedPanel : Panel
    {
        public DoubleBufferedPanel()
        {
            this.SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer,
                true);
            this.UpdateStyles();
        }
    }
}