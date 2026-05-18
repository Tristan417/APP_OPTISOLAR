using MaterialSkin;
using MaterialSkin.Controls;
using MQTTnet;
using System.Buffers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace APP_OptiSolar
{
    public partial class MainForm : MaterialForm
    {
        private Panel navPanel;
        private Panel contentPanel;
        private MaterialButton btnNavDashboard;
        private MaterialButton btnNavMap;
        private MaterialButton btnNavList;
        private MaterialButton btnNavAlerts;
        private MaterialButton btnNavHistory;
        private MaterialButton btnNavSettings;

        private List<SolarPanel> solarPanels;
        private System.Windows.Forms.Timer refreshTimer;
        private System.Windows.Forms.Timer simulationTimer;
        private bool _simulationRunning = false;
        private readonly Random _rng = new Random();
        private MaterialButton btnSimulation;

        // Nouvelles pages
        private DashboardPage dashboardPage;
        private AlertsPage alertsPage;
        private AlertSystem alertSystem;
        private NotificationManager notificationManager;
        private List<Alert> previousAlerts;

        // Suivi de la page active
        private string currentPage = "dashboard";

        // Historique de production
        private ProductionHistory productionHistory;

        // Label de dernière mise à jour (page liste)
        private Label lblLastUpdate;

        // Contrôles pour la page Map
        private WebView2 webView;
        private MaterialButton btnRefreshMap;
        private MaterialComboBox cmbFilterMap;

        // Contrôles pour la page Liste
        private DataGridView dgvPanels;
        private MaterialButton btnAddPanel;
        private MaterialButton btnRefreshList;
        private MaterialButton btnDeletePanel;
        private MaterialTextBox txtSearch;
        private MaterialComboBox cmbFilter;
        private MaterialComboBox cmbSortVoltage;
        private List<SolarPanel> filteredPanels;

        // ── SERVICE BASE DE DONNÉES ──
        private DatabaseService db = new DatabaseService();

        // ── MQTT ──
        private IMqttClient mqttClient;

        public MainForm()
        {
            InitializeComponent();
            InitializeMaterialSkin();
            InitializeData();
            InitializeNavigation();
            InitializeTimer();

            // Afficher le Dashboard par défaut
            ShowDashboardPage();

            // Shown est déclenché après que la fenêtre est visible et le handle créé
            this.Shown += async (s, e) =>
            {
                Console.WriteLine("[INIT] Fenêtre affichée — démarrage MQTT...");
                await InitializeMQTT();
            };
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            this.ClientSize = new Size(1200, 800);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Text = "OptiSolar - Gestion des Panneaux Solaires";

            this.ResumeLayout(false);
        }

        private void InitializeMaterialSkin()
        {
            var materialSkinManager = MaterialSkinManager.Instance;
            materialSkinManager.AddFormToManage(this);
            materialSkinManager.Theme = MaterialSkinManager.Themes.LIGHT;
            materialSkinManager.ColorScheme = new ColorScheme(
                Primary.Blue700,
                Primary.Blue800,
                Primary.Blue500,
                Accent.LightBlue200,
                TextShade.WHITE
            );
        }

        private void InitializeData()
        {
            solarPanels = new List<SolarPanel>();
            filteredPanels = new List<SolarPanel>();
            previousAlerts = new List<Alert>();

            // Charger les panneaux depuis la base de données MySQL
            LoadPanelsFromDatabase();

            // Initialiser le système d'alertes
            alertSystem = new AlertSystem();

            // Initialiser l'historique de production
            productionHistory = new ProductionHistory();

            // Initialiser les notifications Windows
            notificationManager = new NotificationManager(this);
        }

        private void InitializeNavigation()
        {
            // Panel de navigation en haut
            navPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = Color.FromArgb(250, 250, 250)
            };

            // Bouton Dashboard
            btnNavDashboard = new MaterialButton
            {
                Text = "📊 DASHBOARD",
                Location = new Point(20, 10),
                Size = new Size(180, 40),
                Type = MaterialButton.MaterialButtonType.Contained,
                UseAccentColor = true
            };
            btnNavDashboard.Click += (s, e) => ShowDashboardPage();

            // Bouton Carte
            btnNavMap = new MaterialButton
            {
                Text = "📍 CARTE",
                Location = new Point(210, 10),
                Size = new Size(150, 40),
                Type = MaterialButton.MaterialButtonType.Outlined
            };
            btnNavMap.Click += async (s, e) => await ShowMapPage();

            // Bouton Liste
            btnNavList = new MaterialButton
            {
                Text = "📋 LISTE",
                Location = new Point(370, 10),
                Size = new Size(150, 40),
                Type = MaterialButton.MaterialButtonType.Outlined
            };
            btnNavList.Click += (s, e) => ShowListPage();

            // Bouton Alertes
            btnNavAlerts = new MaterialButton
            {
                Text = "🔔 ALERTES",
                Location = new Point(530, 10),
                Size = new Size(150, 40),
                Type = MaterialButton.MaterialButtonType.Outlined
            };
            btnNavAlerts.Click += (s, e) => ShowAlertsPage();

            // Bouton Historique
            btnNavHistory = new MaterialButton
            {
                Text = "📈 HISTORIQUE",
                Location = new Point(690, 10),
                Size = new Size(170, 40),
                Type = MaterialButton.MaterialButtonType.Outlined
            };
            btnNavHistory.Click += (s, e) => ShowHistoryPage();

            // Bouton Paramètres
            btnNavSettings = new MaterialButton
            {
                Text = "⚙️ PARAMÈTRES",
                Location = new Point(880, 10),
                Size = new Size(180, 40),
                Type = MaterialButton.MaterialButtonType.Outlined
            };
            btnNavSettings.Click += (s, e) => ShowSettingsPage();

            // Bouton Simulation
            btnSimulation = new MaterialButton
            {
                Text = "▶ SIMULATION",
                Location = new Point(1080, 10),
                Size = new Size(170, 40),
                Type = MaterialButton.MaterialButtonType.Outlined,
                UseAccentColor = false
            };
            btnSimulation.Click += BtnSimulation_Click;

            navPanel.Controls.Add(btnNavDashboard);
            navPanel.Controls.Add(btnNavMap);
            navPanel.Controls.Add(btnNavList);
            navPanel.Controls.Add(btnNavAlerts);
            navPanel.Controls.Add(btnNavHistory);
            navPanel.Controls.Add(btnNavSettings);
            navPanel.Controls.Add(btnSimulation);



            // Panel de contenu
            contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White
            };

            this.Controls.Add(contentPanel);
            this.Controls.Add(navPanel);

            // Initialiser les pages
            dashboardPage = new DashboardPage(contentPanel, solarPanels);
            alertsPage = new AlertsPage(contentPanel);

            // Brancher le callback d'acquittement
            alertsPage.OnAcknowledge = (alert) =>
            {
                alertSystem.AcknowledgeAlert(alert);
                var remaining = alertSystem.CheckAlerts(solarPanels);
                alertsPage.Show(remaining);
                UpdateAlertBadge(remaining.Count);
            };
        }

        private void InitializeTimer()
        {
            refreshTimer = new System.Windows.Forms.Timer
            {
                Interval = 30000 // 30 secondes
            };
            refreshTimer.Tick += async (s, e) =>
            {
                await RefreshDataFromDB();
                if (webView != null && webView.Visible)
                {
                    await UpdateMapMarkers();
                }
            };
            refreshTimer.Start();
        }

        private void UpdateNavButtons(string activePage)
        {
            btnNavMap.Type = MaterialButton.MaterialButtonType.Outlined;
            btnNavList.Type = MaterialButton.MaterialButtonType.Outlined;
            btnNavSettings.Type = MaterialButton.MaterialButtonType.Outlined;

            switch (activePage)
            {
                case "map":
                    btnNavMap.Type = MaterialButton.MaterialButtonType.Contained;
                    btnNavMap.UseAccentColor = true;
                    break;
                case "list":
                    btnNavList.Type = MaterialButton.MaterialButtonType.Contained;
                    btnNavList.UseAccentColor = true;
                    break;
                case "settings":
                    btnNavSettings.Type = MaterialButton.MaterialButtonType.Contained;
                    btnNavSettings.UseAccentColor = true;
                    break;
            }
        }

        private void UpdateAlertBadge(int count)
        {
            if (btnNavAlerts == null) return;
            btnNavAlerts.Text = count > 0 ? $"🔔 ALERTES ({count})" : "🔔 ALERTES";
        }

        #region Page Dashboard

        private void ShowDashboardPage()
        {
            currentPage = "dashboard";

            if (webView != null)
                webView.Visible = false;

            btnNavDashboard.Type = MaterialButton.MaterialButtonType.Contained;
            btnNavDashboard.UseAccentColor = true;
            btnNavMap.Type = MaterialButton.MaterialButtonType.Outlined;
            btnNavList.Type = MaterialButton.MaterialButtonType.Outlined;
            btnNavAlerts.Type = MaterialButton.MaterialButtonType.Outlined;
            btnNavSettings.Type = MaterialButton.MaterialButtonType.Outlined;
            btnNavHistory.Type = MaterialButton.MaterialButtonType.Outlined;

            dashboardPage.Show();
        }

        #endregion

        #region Page Alertes

        private void ShowAlertsPage()
        {
            currentPage = "alerts";

            if (webView != null)
                webView.Visible = false;

            btnNavDashboard.Type = MaterialButton.MaterialButtonType.Outlined;
            btnNavMap.Type = MaterialButton.MaterialButtonType.Outlined;
            btnNavList.Type = MaterialButton.MaterialButtonType.Outlined;
            btnNavAlerts.Type = MaterialButton.MaterialButtonType.Contained;
            btnNavAlerts.UseAccentColor = true;
            btnNavSettings.Type = MaterialButton.MaterialButtonType.Outlined;
            btnNavHistory.Type = MaterialButton.MaterialButtonType.Outlined;

            var alerts = alertSystem.CheckAlerts(solarPanels);
            UpdateAlertBadge(alerts.Count);

            alertsPage.Show(alerts);
        }

        #endregion

        #region Page Map

        private async System.Threading.Tasks.Task ShowMapPage()
        {
            currentPage = "map";
            contentPanel.Controls.Clear();

            btnNavDashboard.Type = MaterialButton.MaterialButtonType.Outlined;
            btnNavMap.Type = MaterialButton.MaterialButtonType.Contained;
            btnNavMap.UseAccentColor = true;
            btnNavList.Type = MaterialButton.MaterialButtonType.Outlined;
            btnNavAlerts.Type = MaterialButton.MaterialButtonType.Outlined;
            btnNavSettings.Type = MaterialButton.MaterialButtonType.Outlined;
            btnNavHistory.Type = MaterialButton.MaterialButtonType.Outlined;

            btnRefreshMap = new MaterialButton
            {
                Text = "RAFRAÎCHIR",
                Location = new Point(20, 20),
                Size = new Size(150, 40),
                Type = MaterialButton.MaterialButtonType.Contained,
                UseAccentColor = true
            };
            btnRefreshMap.Click += async (s, e) => await RefreshMap();

            var lblFilterMap = new MaterialLabel
            {
                Text = "Afficher :",
                Location = new Point(190, 30),
                AutoSize = true
            };

            var cmbFilterMap = new MaterialComboBox
            {
                Location = new Point(280, 20),
                Size = new Size(200, 40),
                Hint = "Tous les panneaux"
            };
            cmbFilterMap.Items.Add("Tous les panneaux");
            cmbFilterMap.Items.Add("Actif");
            cmbFilterMap.Items.Add("Inactif");
            cmbFilterMap.Items.Add("Défectueux");
            cmbFilterMap.SelectedIndex = 0;
            cmbFilterMap.SelectedIndexChanged += async (s, e) => await ApplyMapFilter(cmbFilterMap.SelectedIndex);

            contentPanel.Controls.Add(btnRefreshMap);
            contentPanel.Controls.Add(lblFilterMap);
            contentPanel.Controls.Add(cmbFilterMap);

            if (webView == null)
            {
                try
                {
                    webView = new WebView2
                    {
                        Location = new Point(20, 80),
                        Size = new Size(contentPanel.Width - 40, contentPanel.Height - 100),
                        Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
                    };

                    contentPanel.Controls.Add(webView);

                    await webView.EnsureCoreWebView2Async(null);

                    webView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = true;
                    webView.CoreWebView2.Settings.IsScriptEnabled = true;
                    webView.CoreWebView2.Settings.AreDevToolsEnabled = true;
                    webView.CoreWebView2.Settings.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) OptiSolar/1.0";

                    string htmlPath = Path.Combine(Application.StartupPath, "map.html");

                    if (File.Exists(htmlPath))
                    {
                        string htmlContent = File.ReadAllText(htmlPath);
                        webView.CoreWebView2.NavigateToString(htmlContent);
                    }
                    else
                    {
                        MessageBox.Show($"Le fichier map.html est introuvable.\nCherché dans: {htmlPath}\n\nAssurez-vous qu'il est dans le dossier de l'application avec la propriété 'Copier si plus récent'.",
                            "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Erreur initialisation WebView2:\n{ex.Message}\n\nInstallez WebView2 Runtime depuis:\nhttps://developer.microsoft.com/microsoft-edge/webview2/",
                        "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                webView.Location = new Point(20, 80);
                webView.Size = new Size(contentPanel.Width - 40, contentPanel.Height - 100);
                webView.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
                webView.Visible = true;

                contentPanel.Controls.Add(webView);
            }
        }

        private async System.Threading.Tasks.Task RefreshMap()
        {
            try
            {
                await RefreshDataFromDB();
                await UpdateMapMarkers();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"EXCEPTION dans RefreshMap: {ex.Message}");
                MessageBox.Show($"Erreur: {ex.Message}", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async System.Threading.Tasks.Task ApplyMapFilter(int filterIndex)
        {
            if (webView == null || webView.CoreWebView2 == null)
                return;

            try
            {
                List<SolarPanel> filteredPanels;

                switch (filterIndex)
                {
                    case 1:
                        filteredPanels = solarPanels.Where(p => p.Status == PanelStatus.Actif).ToList();
                        break;
                    case 2:
                        filteredPanels = solarPanels.Where(p => p.Status == PanelStatus.Inactif).ToList();
                        break;
                    case 3:
                        filteredPanels = solarPanels.Where(p => p.Status == PanelStatus.Defectueux).ToList();
                        break;
                    default:
                        filteredPanels = solarPanels.ToList();
                        break;
                }

                Console.WriteLine($"Filtre carte: Affichage de {filteredPanels.Count} panneau(x)");

                var settings = new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                };
                string json = JsonConvert.SerializeObject(filteredPanels, settings);
                string escapedJson = json.Replace("'", "\\'").Replace("\r", "").Replace("\n", "");

                string script = $"updatePanels(JSON.parse('{escapedJson}'));";
                await webView.CoreWebView2.ExecuteScriptAsync(script);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur filtre carte: {ex.Message}");
            }
        }

        #endregion

        #region Page Liste

        private void ShowListPage()
        {
            currentPage = "list";
            contentPanel.Controls.Clear();

            btnNavDashboard.Type = MaterialButton.MaterialButtonType.Outlined;
            btnNavMap.Type = MaterialButton.MaterialButtonType.Outlined;
            btnNavList.Type = MaterialButton.MaterialButtonType.Contained;
            btnNavList.UseAccentColor = true;
            btnNavAlerts.Type = MaterialButton.MaterialButtonType.Outlined;
            btnNavSettings.Type = MaterialButton.MaterialButtonType.Outlined;
            btnNavHistory.Type = MaterialButton.MaterialButtonType.Outlined;

            if (webView != null)
            {
                webView.Visible = false;
            }

            var lblSearch = new MaterialLabel
            {
                Text = "🔍 Rechercher :",
                Location = new Point(20, 20),
                AutoSize = true
            };

            txtSearch = new MaterialTextBox
            {
                Location = new Point(20, 45),
                Size = new Size(300, 50),
                Hint = "Nom, Id"
            };
            txtSearch.TextChanged += TxtSearch_TextChanged;

            var lblFilter = new MaterialLabel
            {
                Text = "Filtrer par statut :",
                Location = new Point(340, 20),
                AutoSize = true
            };

            cmbFilter = new MaterialComboBox
            {
                Location = new Point(340, 45),
                Size = new Size(200, 50),
                Hint = "Tous les panneaux"
            };
            cmbFilter.Items.Add("Tous");
            cmbFilter.Items.Add("Actif");
            cmbFilter.Items.Add("Inactif");
            cmbFilter.Items.Add("Défectueux");
            cmbFilter.SelectedIndex = 0;
            cmbFilter.SelectedIndexChanged += CmbFilter_SelectedIndexChanged;

            var lblSortVoltage = new MaterialLabel
            {
                Text = "Trier par tension :",
                Location = new Point(560, 20),
                AutoSize = true
            };

            cmbSortVoltage = new MaterialComboBox
            {
                Location = new Point(560, 45),
                Size = new Size(280, 50),
                Hint = "Aucun tri"
            };
            cmbSortVoltage.Items.Add("Aucun tri");
            cmbSortVoltage.Items.Add("⬆ Croissante (faible → forte)");
            cmbSortVoltage.Items.Add("⬇ Décroissante (forte → faible)");
            cmbSortVoltage.SelectedIndex = 0;
            cmbSortVoltage.SelectedIndexChanged += (s, e) => ApplyFilters();

            // ── Ligne 1 : AJOUTER | RAFRAÎCHIR | EXPORTER CSV ──
            btnAddPanel = new MaterialButton
            {
                Text = "AJOUTER PANNEAU",
                Location = new Point(860, 45),
                Size = new Size(180, 40),
                Type = MaterialButton.MaterialButtonType.Contained,
                UseAccentColor = true
            };
            btnAddPanel.Click += BtnAddPanel_Click;

            btnRefreshList = new MaterialButton
            {
                Text = "RAFRAÎCHIR",
                Location = new Point(1050, 45),
                Size = new Size(150, 40),
                Type = MaterialButton.MaterialButtonType.Contained,
                UseAccentColor = true
            };
            btnRefreshList.Click += async (s, e) => await RefreshList();

            var btnExportCSV = new MaterialButton
            {
                Text = "📁 EXPORTER CSV",
                Location = new Point(1210, 45),
                Size = new Size(160, 40),
                Type = MaterialButton.MaterialButtonType.Outlined
            };
            btnExportCSV.Click += (s, e) => ExcelExporter.ExportToCSV(solarPanels);

            // ── Ligne 2 : SUPPRIMER | MODIFIER | RAPPORT ──
            btnDeletePanel = new MaterialButton
            {
                Text = "SUPPRIMER",
                Location = new Point(860, 95),
                Size = new Size(150, 40),
                Type = MaterialButton.MaterialButtonType.Contained,
                UseAccentColor = true
            };
            btnDeletePanel.Click += BtnDeletePanel_Click;

            var btnEditPanel = new MaterialButton
            {
                Text = "MODIFIER",
                Location = new Point(1050, 95),
                Size = new Size(150, 40),
                Type = MaterialButton.MaterialButtonType.Contained,
                UseAccentColor = true
            };
            btnEditPanel.Click += BtnEditPanel_Click;

            var btnReport = new MaterialButton
            {
                Text = "📄 RAPPORT",
                Location = new Point(1210, 95),
                Size = new Size(160, 40),
                Type = MaterialButton.MaterialButtonType.Outlined
            };
            btnReport.Click += (s, e) => ExcelExporter.ExportReport(solarPanels);

            dgvPanels = new DataGridView
            {
                Location = new Point(20, 165),
                Size = new Size(contentPanel.Width - 40, contentPanel.Height - 185),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ColumnHeadersHeight = 40,
                RowTemplate = { Height = 40 },
                EnableHeadersVisualStyles = false,
                CellBorderStyle = DataGridViewCellBorderStyle.None,
                RowHeadersVisible = false,
                AutoGenerateColumns = false,
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(245, 245, 245),
                    ForeColor = Color.FromArgb(80, 80, 80),
                    Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                    Alignment = DataGridViewContentAlignment.MiddleLeft,
                    Padding = new Padding(10, 0, 0, 0)
                },
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.White,
                    ForeColor = Color.Black,
                    Font = new Font("Segoe UI", 9F),
                    SelectionBackColor = Color.FromArgb(187, 210, 225),
                    SelectionForeColor = Color.Black,
                    Alignment = DataGridViewContentAlignment.MiddleLeft,
                    Padding = new Padding(10, 0, 0, 0)
                }
            };

            var colStatus = new DataGridViewImageColumn
            {
                Name = "StatusBadge",
                HeaderText = "Statut",
                Width = 120,
                ImageLayout = DataGridViewImageCellLayout.Normal,
                ValuesAreIcons = false
            };
            dgvPanels.Columns.Add(colStatus);
            dgvPanels.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", HeaderText = "Id", DataPropertyName = "Id", Width = 220 });
            dgvPanels.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "Nom", DataPropertyName = "Name", Width = 140 });
            dgvPanels.Columns.Add(new DataGridViewTextBoxColumn { Name = "Latitude", HeaderText = "Latitude", DataPropertyName = "Latitude", Width = 110 });
            dgvPanels.Columns.Add(new DataGridViewTextBoxColumn { Name = "Longitude", HeaderText = "Longitude", DataPropertyName = "Longitude", Width = 110 });
            dgvPanels.Columns.Add(new DataGridViewTextBoxColumn { Name = "Voltage", HeaderText = "Tension (V)", DataPropertyName = "Voltage", Width = 110 });
            dgvPanels.Columns.Add(new DataGridViewTextBoxColumn { Name = "LastUpdate", HeaderText = "Dernière mise à jour", DataPropertyName = "LastUpdate", Width = 180 });

            dgvPanels.CellFormatting += DgvPanels_CellFormatting;
            dgvPanels.CellDoubleClick += DgvPanels_CellDoubleClick;

            contentPanel.Controls.Add(lblSearch);
            contentPanel.Controls.Add(txtSearch);
            contentPanel.Controls.Add(lblFilter);
            contentPanel.Controls.Add(cmbFilter);
            contentPanel.Controls.Add(lblSortVoltage);
            contentPanel.Controls.Add(cmbSortVoltage);
            contentPanel.Controls.Add(btnAddPanel);
            contentPanel.Controls.Add(btnRefreshList);
            contentPanel.Controls.Add(btnDeletePanel);
            contentPanel.Controls.Add(btnEditPanel);
            contentPanel.Controls.Add(btnExportCSV);
            contentPanel.Controls.Add(btnReport);

            lblLastUpdate = new Label
            {
                Text = $"Dernière MAJ : {DateTime.Now:HH:mm:ss}",
                Font = new Font("Segoe UI", 9F, FontStyle.Italic),
                ForeColor = Color.FromArgb(120, 120, 120),
                Location = new Point(20, 145),
                AutoSize = true
            };
            contentPanel.Controls.Add(lblLastUpdate);

            contentPanel.Controls.Add(dgvPanels);

            RefreshListDisplay();
        }

        private void DgvPanels_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= filteredPanels.Count) return;

            var panel = filteredPanels[e.RowIndex];

            // Fond blanc sur toutes les lignes (suppression coloration)
            dgvPanels.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.White;

            if (e.ColumnIndex == dgvPanels.Columns["StatusBadge"]?.Index)
            {
                Color dotColor;
                string label;
                switch (panel.Status)
                {
                    case PanelStatus.Actif:
                        dotColor = Color.FromArgb(76, 175, 80);
                        label = "Actif";
                        break;
                    case PanelStatus.Defectueux:
                        dotColor = Color.FromArgb(244, 67, 54);
                        label = "Défectueux";
                        break;
                    default:
                        dotColor = Color.FromArgb(158, 158, 158);
                        label = "Inactif";
                        break;
                }

                // Bitmap 110x30 : pastille ronde + texte
                var bmp = new Bitmap(110, 30);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.White);
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.FillEllipse(new SolidBrush(dotColor), 8, 9, 12, 12);
                    g.DrawString(label, new Font("Segoe UI", 9F), new SolidBrush(Color.FromArgb(60, 60, 60)), 26, 7);
                }
                e.Value = bmp;
                e.FormattingApplied = true;
            }
        }

        private async void DgvPanels_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                if (e.RowIndex >= 0 && e.RowIndex < filteredPanels.Count)
                {
                    var panel = filteredPanels[e.RowIndex];

                    await ShowMapPage();

                    for (int i = 0; i < 10; i++)
                    {
                        await System.Threading.Tasks.Task.Delay(300);

                        if (webView != null && webView.CoreWebView2 != null)
                        {
                            try
                            {
                                string checkScript = "typeof map !== 'undefined'";
                                string isReady = await webView.CoreWebView2.ExecuteScriptAsync(checkScript);

                                if (isReady == "true")
                                {
                                    string script = $"map.setView([{panel.Latitude.ToString().Replace(',', '.')}, {panel.Longitude.ToString().Replace(',', '.')}], 18);";
                                    await webView.CoreWebView2.ExecuteScriptAsync(script);

                                    MessageBox.Show($"Carte centrée sur {panel.Name}", "Zoom", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                    return;
                                }
                            }
                            catch { }
                        }
                    }

                    MessageBox.Show("La carte n'est pas encore chargée.\n\nVeuillez cliquer sur le bouton CARTE puis réessayer le double-clic.",
                        "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur : {ex.Message}", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void BtnAddPanel_Click(object sender, EventArgs e)
        {
            using (var form = new AddPanelForm(solarPanels))
            {
                if (form.ShowDialog() == DialogResult.OK)
                {
                    // ── AJOUT EN BASE DE DONNÉES EN PREMIER ──
                    // (avant le rechargement du timer pour éviter la suppression automatique)
                    try
                    {
                        db.AddPanel(form.NewPanel);
                        // Recharger depuis la DB pour avoir l'ID réel assigné par MySQL
                        solarPanels = db.GetAllPanels();
                    }
                    catch (Exception exDb)
                    {
                        Console.WriteLine($"Erreur DB ajout: {exDb.Message}");
                        // Fallback : ajouter localement si la DB est inaccessible
                        solarPanels.Add(form.NewPanel);
                        MessageBox.Show($"Panneau ajouté localement mais erreur base de données :\n{exDb.Message}",
                            "Avertissement", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }

                    RefreshListDisplay();
                    SavePanelsToFile();

                    if (webView != null && webView.CoreWebView2 != null)
                    {
                        await UpdateMapMarkers();
                    }

                    MessageBox.Show($"Panneau {form.NewPanel.Name} ajouté avec succès !", "Succès",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private async void BtnDeletePanel_Click(object sender, EventArgs e)
        {
            if (dgvPanels.SelectedRows.Count > 0)
            {
                int index = dgvPanels.SelectedRows[0].Index;
                var panel = filteredPanels[index];

                var result = MessageBox.Show($"Voulez-vous vraiment supprimer le panneau {panel.Name} ?",
                    "Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    solarPanels.Remove(panel);
                    RefreshListDisplay();
                    SavePanelsToFile();

                    // ── SUPPRESSION EN BASE DE DONNÉES ──
                    try
                    {
                        db.DeletePanel(panel.Id);
                    }
                    catch (Exception exDb)
                    {
                        Console.WriteLine($"Erreur DB suppression: {exDb.Message}");
                        MessageBox.Show($"Panneau supprimé localement mais erreur base de données :\n{exDb.Message}",
                            "Avertissement", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }

                    if (webView != null && webView.CoreWebView2 != null)
                    {
                        await UpdateMapMarkers();
                    }
                }
            }
            else
            {
                MessageBox.Show("Veuillez sélectionner un panneau à supprimer.", "Information",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private async void BtnEditPanel_Click(object sender, EventArgs e)
        {
            try
            {
                if (dgvPanels.SelectedRows.Count > 0)
                {
                    int index = dgvPanels.SelectedRows[0].Index;
                    var panelToEdit = filteredPanels[index];
                    var oldStatus = panelToEdit.Status;

                    using (var form = new EditPanelForm(panelToEdit))
                    {
                        if (form.ShowDialog() == DialogResult.OK)
                        {
                            SavePanelsToFile();
                            RefreshListDisplay();

                            // ── MISE À JOUR EN BASE DE DONNÉES ──
                            try
                            {
                                db.UpdatePanel(panelToEdit);
                            }
                            catch (Exception exDb)
                            {
                                Console.WriteLine($"Erreur DB modification: {exDb.Message}");
                                MessageBox.Show($"Panneau modifié localement mais erreur base de données :\n{exDb.Message}",
                                    "Avertissement", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            }

                            if (webView != null && webView.CoreWebView2 != null)
                            {
                                await UpdateMapMarkers();
                            }

                            try
                            {
                                if (alertSystem != null)
                                {
                                    var currentAlerts = alertSystem.CheckAlerts(solarPanels);

                                    if (oldStatus != PanelStatus.Defectueux && panelToEdit.Status == PanelStatus.Defectueux)
                                    {
                                        if (notificationManager != null)
                                        {
                                            notificationManager.ShowCriticalAlert(
                                                panelToEdit.Name,
                                                "Le panneau est maintenant en état défectueux et nécessite une intervention."
                                            );
                                        }
                                    }

                                    UpdateAlertBadge(currentAlerts.Count);

                                    if (previousAlerts != null)
                                    {
                                        previousAlerts = currentAlerts;
                                    }
                                }
                            }
                            catch (Exception exAlert)
                            {
                                Console.WriteLine($"Erreur lors de la vérification des alertes: {exAlert.Message}");
                            }

                            MessageBox.Show($"Panneau {panelToEdit.Name} modifié avec succès !", "Succès",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }
                }
                else
                {
                    MessageBox.Show("Veuillez sélectionner un panneau à modifier.", "Information",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur lors de la modification du panneau :\n\n{ex.Message}", "Erreur",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RefreshListDisplay()
        {
            ApplyFilters();
        }

        private void ApplyFilters()
        {
            var panels = solarPanels.ToList();

            if (cmbFilter != null && cmbFilter.SelectedIndex > 0)
            {
                switch (cmbFilter.SelectedIndex)
                {
                    case 1:
                        panels = panels.Where(p => p.Status == PanelStatus.Actif).ToList();
                        break;
                    case 2:
                        panels = panels.Where(p => p.Status == PanelStatus.Inactif).ToList();
                        break;
                    case 3:
                        panels = panels.Where(p => p.Status == PanelStatus.Defectueux).ToList();
                        break;
                }
            }

            if (txtSearch != null && !string.IsNullOrWhiteSpace(txtSearch.Text))
            {
                string searchText = txtSearch.Text.ToLower();
                panels = panels.Where(p =>
                    p.Name.ToLower().Contains(searchText) ||
                    (p.DeviceId != null && p.DeviceId.ToLower().Contains(searchText)) ||
                    p.StatusText.ToLower().Contains(searchText)
                ).ToList();
            }

            // Tri par tension
            if (cmbSortVoltage != null)
            {
                switch (cmbSortVoltage.SelectedIndex)
                {
                    case 1: // Croissante (faible → forte)
                        panels = panels.OrderBy(p => p.Voltage ?? double.MaxValue).ToList();
                        break;
                    case 2: // Décroissante (forte → faible)
                        panels = panels.OrderByDescending(p => p.Voltage ?? double.MinValue).ToList();
                        break;
                }
            }

            filteredPanels = panels;

            if (dgvPanels != null)
            {
                dgvPanels.DataSource = null;
                dgvPanels.DataSource = filteredPanels;
            }
        }

        private void TxtSearch_TextChanged(object sender, EventArgs e)
        {
            ApplyFilters();
        }

        private void CmbFilter_SelectedIndexChanged(object sender, EventArgs e)
        {
            ApplyFilters();
        }

        private async System.Threading.Tasks.Task RefreshList()
        {
            await RefreshDataFromDB();
            RefreshListDisplay();
        }

        #endregion

        #region Page Paramètres

        private void ShowSettingsPage()
        {
            currentPage = "settings";
            contentPanel.Controls.Clear();

            btnNavDashboard.Type = MaterialButton.MaterialButtonType.Outlined;
            btnNavMap.Type = MaterialButton.MaterialButtonType.Outlined;
            btnNavList.Type = MaterialButton.MaterialButtonType.Outlined;
            btnNavAlerts.Type = MaterialButton.MaterialButtonType.Outlined;
            btnNavSettings.Type = MaterialButton.MaterialButtonType.Contained;
            btnNavSettings.UseAccentColor = true;
            btnNavHistory.Type = MaterialButton.MaterialButtonType.Outlined;

            if (webView != null)
            {
                webView.Visible = false;
            }

            var lblTitle = new MaterialLabel
            {
                Text = "Paramètres MQTT",
                Font = new Font("Roboto", 18F, FontStyle.Bold),
                Location = new Point(20, 20),
                AutoSize = true
            };

            var lblInfo = new MaterialLabel
            {
                Text = "Le broker MQTT doit tourner sur 127.0.0.1:1883\nTopic attendu : panneau/<NOM_PANNEAU>/voltage",
                Location = new Point(20, 60),
                Size = new Size(600, 60),
                AutoSize = false
            };

            var lblPath = new MaterialLabel
            {
                Text = $"Fichier de sauvegarde : {Path.Combine(Application.StartupPath, "panels.json")}",
                Location = new Point(20, 130),
                Size = new Size(800, 40),
                AutoSize = false,
                ForeColor = Color.Gray
            };

            contentPanel.Controls.Add(lblTitle);
            contentPanel.Controls.Add(lblInfo);
            contentPanel.Controls.Add(lblPath);

        }

        #endregion

        #region Page Historique

        private void ShowHistoryPage()
        {
            currentPage = "history";

            if (webView != null)
                webView.Visible = false;

            btnNavDashboard.Type = MaterialButton.MaterialButtonType.Outlined;
            btnNavMap.Type = MaterialButton.MaterialButtonType.Outlined;
            btnNavList.Type = MaterialButton.MaterialButtonType.Outlined;
            btnNavAlerts.Type = MaterialButton.MaterialButtonType.Outlined;
            btnNavSettings.Type = MaterialButton.MaterialButtonType.Outlined;
            btnNavHistory.Type = MaterialButton.MaterialButtonType.Contained;
            btnNavHistory.UseAccentColor = true;

            productionHistory.ShowChart(contentPanel, solarPanels);
        }

        #endregion

        #region Données et DB

        private async System.Threading.Tasks.Task UpdateMapMarkers()
        {
            if (webView == null || webView.CoreWebView2 == null)
            {
                Console.WriteLine("UpdateMapMarkers: webView non initialisé");
                return;
            }

            try
            {
                Console.WriteLine($"UpdateMapMarkers: Envoi de {solarPanels.Count} panneau(x)");

                var settings = new JsonSerializerSettings
                {
                    ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver()
                };

                string json = JsonConvert.SerializeObject(solarPanels, settings);
                Console.WriteLine($"JSON généré: {json}");

                string escapedJson = json.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\n", "\\n").Replace("\r", "\\r");
                string script = $"updatePanels(JSON.parse('{escapedJson}'));";

                Console.WriteLine($"Script à exécuter: {script.Substring(0, Math.Min(150, script.Length))}...");

                var result = await webView.CoreWebView2.ExecuteScriptAsync(script);
                Console.WriteLine($"Résultat exécution script: {result}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERREUR UpdateMapMarkers: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
            }
        }

        private async System.Threading.Tasks.Task RefreshDataFromDB()
        {
            try
            {
                // Recharger depuis la DB mais conserver les valeurs MQTT en mémoire
                // si elles sont plus récentes que ce qu'il y a en DB
                List<SolarPanel> dbPanels = null;
                await System.Threading.Tasks.Task.Run(() =>
                {
                    dbPanels = db.GetAllPanels();
                });

                // Fusionner : pour chaque panneau en DB, si on a une valeur MQTT
                // plus récente en mémoire, on la conserve
                foreach (var dbPanel in dbPanels)
                {
                    var memPanel = solarPanels.FirstOrDefault(p =>
                        string.Equals(p.Name?.Trim(), dbPanel.Name?.Trim(), StringComparison.OrdinalIgnoreCase));

                    if (memPanel != null && memPanel.LastUpdate > dbPanel.LastUpdate)
                    {
                        // La mémoire est plus fraîche (mise à jour par MQTT) → on garde
                        dbPanel.Voltage = memPanel.Voltage;
                        dbPanel.Status = memPanel.Status;
                        dbPanel.LastUpdate = memPanel.LastUpdate;
                    }
                }

                solarPanels = dbPanels;

                Console.WriteLine($"✅ {solarPanels.Count} panneau(x) rechargé(s) depuis la base de données.");

                productionHistory.Record(solarPanels);

                if (alertSystem != null && notificationManager != null)
                {
                    var currentAlerts = alertSystem.CheckAlerts(solarPanels);

                    notificationManager.CheckAlertsAndNotify(currentAlerts, previousAlerts);

                    UpdateAlertBadge(currentAlerts.Count);

                    if (currentPage == "alerts")
                        alertsPage.Show(currentAlerts);

                    if (dashboardPage != null && currentPage == "dashboard")
                        dashboardPage.Refresh(solarPanels);

                    if (currentPage == "history")
                        productionHistory.ShowChart(contentPanel, solarPanels);

                    if (lblLastUpdate != null && currentPage == "list")
                    {
                        lblLastUpdate.Text = $"Dernière MAJ : {DateTime.Now:HH:mm:ss}";
                        RefreshListDisplay();
                    }

                    previousAlerts = currentAlerts;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur simulation: {ex.Message}");
            }
        }

        private void SavePanelsToFile()
        {
            try
            {
                string filePath = Path.Combine(Application.StartupPath, "panels.json");
                string json = JsonConvert.SerializeObject(solarPanels, Formatting.Indented);
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur sauvegarde: {ex.Message}");
            }
        }

        // ── CHARGEMENT DEPUIS LA BASE DE DONNÉES ──
        private void LoadPanelsFromDatabase()
        {
            try
            {
                solarPanels = db.GetAllPanels();
                Console.WriteLine($"✅ {solarPanels.Count} panneau(x) chargé(s) depuis la base de données.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur chargement DB: {ex.Message}");
                MessageBox.Show($"Impossible de charger les panneaux depuis la base de données :\n{ex.Message}\n\nChargement depuis le fichier local...",
                    "Avertissement", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                // Fallback : charger depuis le fichier JSON si la DB est inaccessible
                LoadPanelsFromFile();
            }
        }

        private const double LAON_LAT = 49.5639;
        private const double LAON_LON = 3.6253;

        private string GetSecteur(double latitude, double longitude)
        {
            return latitude >= LAON_LAT ? "N" : "S";
        }

        public string GeneratePanelName(double latitude, double longitude)
        {
            string secteur = GetSecteur(latitude, longitude);
            int numero = 1;
            foreach (var p in solarPanels)
            {
                if (p.Name != null && p.Name.StartsWith($"LAO-{secteur}-"))
                {
                    string numStr = p.Name.Replace($"LAO-{secteur}-", "");
                    if (int.TryParse(numStr, out int n) && n >= numero)
                        numero = n + 1;
                }
            }
            return $"LAO-{secteur}-{numero:D3}";
        }

        private void RenameExistingPanels()
        {
            var nord = new List<SolarPanel>();
            var sud = new List<SolarPanel>();

            foreach (var panel in solarPanels)
            {
                if (GetSecteur(panel.Latitude, panel.Longitude) == "N")
                    nord.Add(panel);
                else
                    sud.Add(panel);
            }

            int n = 1;
            foreach (var panel in nord)
                panel.Name = $"LAO-N-{n++:D3}";

            int s = 1;
            foreach (var panel in sud)
                panel.Name = $"LAO-S-{s++:D3}";
        }

        private void LoadPanelsFromFile()
        {
            try
            {
                string filePath = Path.Combine(Application.StartupPath, "panels.json");

                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath);
                    solarPanels = JsonConvert.DeserializeObject<List<SolarPanel>>(json) ?? new List<SolarPanel>();

                    RenameExistingPanels();
                    SavePanelsToFile();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur chargement fichier: {ex.Message}");
            }
        }

        #endregion

        #region MQTT

        private async Task InitializeMQTT()
        {
            try
            {
                var factory = new MqttClientFactory();
                mqttClient = factory.CreateMqttClient();

                var options = new MqttClientOptionsBuilder()
                    .WithTcpServer("127.0.0.1", 1883)
                    .Build();

                // Déclenché à chaque message reçu
                mqttClient.ApplicationMessageReceivedAsync += e =>
                {
                    string topic = e.ApplicationMessage.Topic;

                    // MQTTnet v5 : Payload est un ReadOnlySequence<byte>
                    string payload = "";
                    try
                    {
                        var reader = new System.Buffers.SequenceReader<byte>(e.ApplicationMessage.Payload);
                        byte[] bytes = new byte[e.ApplicationMessage.Payload.Length];
                        reader.TryCopyTo(bytes);
                        payload = System.Text.Encoding.UTF8.GetString(bytes);
                    }
                    catch { payload = ""; }

                    if (this.IsHandleCreated)
                        this.BeginInvoke((MethodInvoker)(() => TraiterMessageMQTT(topic, payload)));

                    return Task.CompletedTask;
                };

                mqttClient.DisconnectedAsync += async e =>
                {
                    await Task.Delay(5000);
                    try { await mqttClient.ConnectAsync(options); }
                    catch { }
                };

                var result = await mqttClient.ConnectAsync(options);

                if (result.ResultCode != MqttClientConnectResultCode.Success)
                {
                    UpdateMqttStatus($"❌ Échec connexion MQTT : {result.ResultCode}", System.Drawing.Color.Red);
                    return;
                }

                var topicFilter = new MqttTopicFilterBuilder()
                    .WithTopic("panneau/#")
                    .Build();
                await mqttClient.SubscribeAsync(new MqttClientSubscribeOptionsBuilder()
                    .WithTopicFilter(topicFilter)
                    .Build());

                UpdateMqttStatus("✅ MQTT connecté — en attente de données...", System.Drawing.Color.Green);
            }
            catch (Exception ex)
            {
                UpdateMqttStatus($"❌ Erreur MQTT : {ex.Message}", System.Drawing.Color.Red);
            }
        }

        /// <summary>
        /// Met à jour le label de statut MQTT dans la nav bar
        /// </summary>
        private void UpdateMqttStatus(string message, System.Drawing.Color color)
        {
            // Label MQTT supprimé
        }

        /// <summary>
        /// Démarre ou arrête la simulation de tension
        /// </summary>
        private void BtnSimulation_Click(object sender, EventArgs e)
        {
            _simulationRunning = !_simulationRunning;

            if (_simulationRunning)
            {
                btnSimulation.Text = "⏹ STOP SIMULATION";
                btnSimulation.UseAccentColor = true;
                btnSimulation.Type = MaterialButton.MaterialButtonType.Contained;

                simulationTimer = new System.Windows.Forms.Timer { Interval = 30000 };
                simulationTimer.Tick += SimulationTimer_Tick;
                simulationTimer.Start();
                SimulationTimer_Tick(null, null); // 1er tick immédiat
            }
            else
            {
                btnSimulation.Text = "▶ SIMULATION";
                btnSimulation.UseAccentColor = false;
                btnSimulation.Type = MaterialButton.MaterialButtonType.Outlined;

                simulationTimer?.Stop();
                simulationTimer?.Dispose();
                simulationTimer = null;
            }
        }

        /// <summary>
        /// Génère des tensions aléatoires réalistes pour chaque panneau
        /// Actif → 8.0–12.0V | Inactif → 0V | Défectueux → 2.0–7.0V
        /// </summary>
        private void SimulationTimer_Tick(object sender, EventArgs e)
        {
            foreach (var panel in solarPanels)
            {
                switch (panel.Status)
                {
                    case PanelStatus.Actif:
                        double baseVoltage = panel.Voltage ?? 12.0;
                        double variation = (_rng.NextDouble() - 0.5) * 1.5;
                        panel.Voltage = Math.Round(Math.Clamp(baseVoltage + variation, 8.0, 12.0), 2);
                        break;
                    case PanelStatus.Defectueux:
                        panel.Voltage = Math.Round(2.0 + _rng.NextDouble() * 5.0, 2);
                        break;
                    case PanelStatus.Inactif:
                        panel.Voltage = 0.0;
                        break;
                }
                panel.LastUpdate = DateTime.Now;

                // Sauvegarder en base de données
                try
                {
                    db.UpdatePanelData(panel.Name, panel.Voltage ?? 0, 0, panel.Status.ToString());
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SIMULATION] Erreur DB pour {panel.Name}: {ex.Message}");
                }

                // Publier sur le broker MQTT : topic panneau/<NOM>/voltage
                if (mqttClient != null && mqttClient.IsConnected)
                {
                    try
                    {
                        string topic = $"panneau/{panel.Name}/voltage";
                        string payload = (panel.Voltage ?? 0).ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
                        var message = new MqttApplicationMessageBuilder()
                            .WithTopic(topic)
                            .WithPayload(payload)
                            .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                            .Build();
                        mqttClient.PublishAsync(message);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[SIMULATION] Erreur MQTT publish pour {panel.Name}: {ex.Message}");
                    }
                }
            }

            // Rafraîchir la page courante
            var alerts = alertSystem.CheckAlerts(solarPanels);
            UpdateAlertBadge(alerts.Count);

            if (currentPage == "dashboard") dashboardPage?.Refresh(solarPanels);
            if (currentPage == "list") RefreshListDisplay();
            if (currentPage == "alerts") alertsPage?.Show(alerts);
            if (currentPage == "history") productionHistory?.ShowChart(contentPanel, solarPanels);
            if (lblLastUpdate != null && currentPage == "list")
                lblLastUpdate.Text = $"Dernière MAJ : {DateTime.Now:HH:mm:ss} (simulation)";
        }

        private void TraiterMessageMQTT(string topic, string payload)
        {
            try
            {
                Console.WriteLine($"[MQTT] Message reçu — topic: '{topic}' | payload: '{payload}'");

                // Format attendu : panneau/LAO-001/voltage
                string[] parts = topic.Split('/');
                if (parts.Length < 3)
                {
                    Console.WriteLine($"[MQTT] Topic ignoré (format invalide) : '{topic}'");
                    return;
                }

                string panelName = parts[1].Trim();
                string typeDonnee = parts[2].Trim().ToLower();

                Console.WriteLine($"[MQTT] Recherche panneau '{panelName}' dans {solarPanels.Count} panneau(x)...");

                // Comparaison insensible à la casse et aux espaces
                var panel = solarPanels.FirstOrDefault(p =>
                    string.Equals(p.Name?.Trim(), panelName, StringComparison.OrdinalIgnoreCase));

                if (panel == null)
                {
                    Console.WriteLine($"[MQTT] Panneau '{panelName}' introuvable. Panneaux disponibles : {string.Join(", ", solarPanels.Select(p => p.Name))}");
                    return;
                }

                Console.WriteLine($"[MQTT] Panneau trouvé : {panel.Name} | type donnée : {typeDonnee}");
                UpdateMqttStatus($"✅ MQTT — dernier msg : {panel.Name} = {payload} ({DateTime.Now:HH:mm:ss})", System.Drawing.Color.Green);

                if (typeDonnee == "voltage")
                {
                    // Accepter le point ou la virgule comme séparateur décimal
                    string normalized = payload.Trim().Replace(',', '.');
                    if (!double.TryParse(normalized, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double voltage))
                    {
                        Console.WriteLine($"[MQTT] Valeur voltage invalide : '{payload}'");
                        return;
                    }

                    Console.WriteLine($"[MQTT] Mise à jour {panel.Name} → voltage = {voltage} V");

                    panel.Voltage = voltage;
                    panel.LastUpdate = DateTime.Now;

                    // Mettre à jour le statut selon la tension (max attendu : 12V)
                    if (voltage <= 0)
                        panel.Status = PanelStatus.Inactif;
                    else if (voltage < 8)
                        panel.Status = PanelStatus.Defectueux;
                    else
                        panel.Status = PanelStatus.Actif;

                    // Sauvegarde en base de données
                    try
                    {
                        db.UpdatePanelData(panel.Name, voltage, 0, panel.Status.ToString());
                        Console.WriteLine($"[MQTT] DB mise à jour pour {panel.Name}");
                    }
                    catch (Exception exDb)
                    {
                        Console.WriteLine($"[MQTT] Erreur DB : {exDb.Message}");
                    }
                }

                // Vérifier les alertes
                var currentAlerts = alertSystem.CheckAlerts(solarPanels);
                notificationManager.CheckAlertsAndNotify(currentAlerts, previousAlerts);
                UpdateAlertBadge(currentAlerts.Count);
                previousAlerts = currentAlerts;

                // Rafraîchir l'affichage selon la page active
                switch (currentPage)
                {
                    case "dashboard":
                        dashboardPage.Refresh(solarPanels);
                        break;
                    case "list":
                        RefreshListDisplay();
                        if (lblLastUpdate != null)
                            lblLastUpdate.Text = $"Dernière MAJ : {DateTime.Now:HH:mm:ss}";
                        break;
                    case "alerts":
                        alertsPage.Show(currentAlerts);
                        break;
                }

                Console.WriteLine($"[MQTT] Affichage rafraîchi (page active : {currentPage})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MQTT] Erreur traitement : {ex.Message}\n{ex.StackTrace}");
            }
        }

        #endregion

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            refreshTimer?.Stop();
            refreshTimer?.Dispose();
            simulationTimer?.Stop();
            simulationTimer?.Dispose();
            notificationManager?.Dispose();

            // Déconnecter proprement le client MQTT
            if (mqttClient != null && mqttClient.IsConnected)
            {
                mqttClient.DisconnectAsync().GetAwaiter().GetResult();
                mqttClient.Dispose();
            }

            SavePanelsToFile();
            base.OnFormClosing(e);
        }
    }
}