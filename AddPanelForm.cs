using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;
using MaterialSkin;
using MaterialSkin.Controls;
using Newtonsoft.Json.Linq;

namespace APP_OptiSolar
{
    public partial class AddPanelForm : MaterialForm
    {
        public SolarPanel NewPanel { get; private set; }

        // Panels existants passés depuis MainForm pour calculer le prochain numéro
        private List<SolarPanel> _existingPanels;
        private MaterialTextBox txtName;
        private MaterialTextBox txtLatitude;
        private MaterialTextBox txtLongitude;
        private MaterialTextBox txtDeviceId;
        private MaterialButton btnSave;
        private MaterialButton btnCancel;
        private MaterialLabel lblTitle;
        private MaterialLabel lblName;
        private MaterialLabel lblLatitude;
        private MaterialLabel lblLongitude;
        private MaterialLabel lblDeviceId;
        private MaterialLabel lblNameHint;

        private System.Windows.Forms.Timer _geocodeTimer;
        private static readonly HttpClient _http = new HttpClient();

        public AddPanelForm(List<SolarPanel> existingPanels = null)
        {
            _existingPanels = existingPanels ?? new List<SolarPanel>();

            // Nominatim exige un User-Agent
            if (!_http.DefaultRequestHeaders.Contains("User-Agent"))
                _http.DefaultRequestHeaders.Add("User-Agent", "OptiSolar/1.0");

            InitializeComponent();
            InitializeMaterialSkin();

            // Timer pour ne pas appeler Nominatim à chaque frappe
            _geocodeTimer = new System.Windows.Forms.Timer { Interval = 800 };
            _geocodeTimer.Tick += async (s, e) =>
            {
                _geocodeTimer.Stop();
                await GenerateNameFromCoordsAsync();
            };
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            this.ClientSize = new System.Drawing.Size(450, 470);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Text = "Ajouter un Panneau Solaire";

            lblTitle = new MaterialLabel
            {
                Text = "Nouveau Panneau Solaire",
                Font = new System.Drawing.Font("Roboto", 18F, System.Drawing.FontStyle.Bold),
                Location = new System.Drawing.Point(20, 80),
                AutoSize = true
            };

            lblLatitude = new MaterialLabel
            {
                Text = "Latitude",
                Location = new System.Drawing.Point(20, 145),
                AutoSize = true
            };

            txtLatitude = new MaterialTextBox
            {
                Location = new System.Drawing.Point(20, 170),
                Size = new System.Drawing.Size(190, 50),
                Hint = "49.5639"
            };
            txtLatitude.TextChanged += CoordChanged;

            lblLongitude = new MaterialLabel
            {
                Text = "Longitude",
                Location = new System.Drawing.Point(230, 145),
                AutoSize = true
            };

            txtLongitude = new MaterialTextBox
            {
                Location = new System.Drawing.Point(230, 170),
                Size = new System.Drawing.Size(190, 50),
                Hint = "3.6253"
            };
            txtLongitude.TextChanged += CoordChanged;

            lblName = new MaterialLabel
            {
                Text = "Nom du panneau (généré automatiquement)",
                Location = new System.Drawing.Point(20, 230),
                AutoSize = true
            };

            txtName = new MaterialTextBox
            {
                Location = new System.Drawing.Point(20, 255),
                Size = new System.Drawing.Size(400, 50),
                Hint = "Entrez latitude/longitude pour générer"
            };

            lblNameHint = new MaterialLabel
            {
                Text = "💡 Modifiable manuellement si besoin",
                Font = new System.Drawing.Font("Roboto", 8F),
                ForeColor = System.Drawing.Color.FromArgb(150, 150, 150),
                Location = new System.Drawing.Point(22, 308),
                AutoSize = true
            };

            lblDeviceId = new MaterialLabel
            {
                Text = "ID",
                Location = new System.Drawing.Point(20, 330),
                AutoSize = true
            };

            txtDeviceId = new MaterialTextBox
            {
                Location = new System.Drawing.Point(20, 355),
                Size = new System.Drawing.Size(400, 50),
                Hint = "1"
            };

            btnSave = new MaterialButton
            {
                Text = "ENREGISTRER",
                Location = new System.Drawing.Point(240, 425),
                Size = new System.Drawing.Size(180, 40),
                Type = MaterialButton.MaterialButtonType.Contained,
                UseAccentColor = true
            };
            btnSave.Click += BtnSave_Click;

            btnCancel = new MaterialButton
            {
                Text = "ANNULER",
                Location = new System.Drawing.Point(50, 425),
                Size = new System.Drawing.Size(180, 40),
                Type = MaterialButton.MaterialButtonType.Text
            };
            btnCancel.Click += BtnCancel_Click;

            this.Controls.Add(lblTitle);
            this.Controls.Add(lblLatitude);
            this.Controls.Add(txtLatitude);
            this.Controls.Add(lblLongitude);
            this.Controls.Add(txtLongitude);
            this.Controls.Add(lblName);
            this.Controls.Add(txtName);
            this.Controls.Add(lblNameHint);
            this.Controls.Add(lblDeviceId);
            this.Controls.Add(txtDeviceId);
            this.Controls.Add(btnSave);
            this.Controls.Add(btnCancel);

            this.ResumeLayout(false);
            this.PerformLayout();
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

        // Relancer le timer à chaque frappe (évite d'appeler Nominatim trop souvent)
        private void CoordChanged(object sender, EventArgs e)
        {
            _geocodeTimer.Stop();
            _geocodeTimer.Start();
        }

        /// <summary>
        /// Appelle Nominatim pour récupérer la ville depuis lat/lon,
        /// puis génère le nom au format : VILLE-N001 ou VILLE-S001
        /// </summary>
        private async Task GenerateNameFromCoordsAsync()
        {
            string latText = txtLatitude.Text.Replace(',', '.');
            string lonText = txtLongitude.Text.Replace(',', '.');

            if (!double.TryParse(latText, NumberStyles.Float, CultureInfo.InvariantCulture, out double lat) ||
                !double.TryParse(lonText, NumberStyles.Float, CultureInfo.InvariantCulture, out double lon))
                return;

            // Récupérer le code ville via Nominatim
            string cityCode = await GetCityCodeAsync(lat, lon);

            // Calculer le prochain numéro disponible
            int nextNumber = GetNextPanelNumber(cityCode);

            // Mettre à jour le champ nom sur le thread UI
            if (txtName.InvokeRequired)
                txtName.Invoke(new Action(() => txtName.Text = $"{cityCode}-{nextNumber:D3}"));
            else
                txtName.Text = $"{cityCode}-{nextNumber:D3}";
        }

        /// <summary>
        /// Reverse geocoding via Nominatim — retourne un code ville en majuscules (ex: "LAO", "AMI", "PAR")
        /// </summary>
        private async Task<string> GetCityCodeAsync(double lat, double lon)
        {
            try
            {
                string url = $"https://nominatim.openstreetmap.org/reverse?lat={lat.ToString(CultureInfo.InvariantCulture)}&lon={lon.ToString(CultureInfo.InvariantCulture)}&format=json";
                var response = await _http.GetAsync(url);

                if (!response.IsSuccessStatusCode) return "LAO";

                var content = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(content);

                // Priorité : village > town > city > county > state
                string city = json["address"]?["village"]?.ToString()
                           ?? json["address"]?["town"]?.ToString()
                           ?? json["address"]?["city"]?.ToString()
                           ?? json["address"]?["county"]?.ToString()
                           ?? json["address"]?["state"]?.ToString()
                           ?? "LAO";

                // Garder les 3 premières lettres en majuscules, sans accents
                city = RemoveDiacritics(city).ToUpper();
                city = System.Text.RegularExpressions.Regex.Replace(city, @"[^A-Z]", "");
                return city.Length >= 3 ? city.Substring(0, 3) : city.PadRight(3, 'X');
            }
            catch
            {
                return "LAO";
            }
        }

        /// <summary>
        /// Cherche le prochain numéro disponible pour un préfixe donné
        /// parmi les panneaux existants. Ex: si LAO-N-001 et LAO-N-002 existent → retourne 3
        /// </summary>
        private int GetNextPanelNumber(string cityCode)
        {
            int max = 0;
            foreach (var panel in _existingPanels)
            {
                if (panel.Name != null && panel.Name.StartsWith(cityCode + "-", StringComparison.OrdinalIgnoreCase))
                {
                    string numPart = panel.Name.Substring(cityCode.Length + 1);
                    if (int.TryParse(numPart, out int num) && num > max)
                        max = num;
                }
            }
            return max + 1;
        }

        /// <summary>
        /// Supprime les accents (é→e, à→a, etc.) pour le code ville
        /// </summary>
        private static string RemoveDiacritics(string text)
        {
            var normalized = text.Normalize(System.Text.NormalizationForm.FormD);
            var sb = new System.Text.StringBuilder();
            foreach (char c in normalized)
            {
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) !=
                    System.Globalization.UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }
            return sb.ToString().Normalize(System.Text.NormalizationForm.FormC);
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                MessageBox.Show("Veuillez entrer un nom pour le panneau.", "Erreur",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string latitudeText = txtLatitude.Text.Replace(',', '.');
            string longitudeText = txtLongitude.Text.Replace(',', '.');

            if (!double.TryParse(latitudeText, NumberStyles.Float, CultureInfo.InvariantCulture, out double latitude))
            {
                MessageBox.Show("La latitude doit être un nombre valide.\nExemples: 49.5639 ou 49,5639", "Erreur",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!double.TryParse(longitudeText, NumberStyles.Float, CultureInfo.InvariantCulture, out double longitude))
            {
                MessageBox.Show("La longitude doit être un nombre valide.\nExemples: 3.6253 ou 3,6253", "Erreur",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            NewPanel = new SolarPanel
            {
                Name = txtName.Text.Trim(),
                Latitude = latitude,
                Longitude = longitude,
                DeviceId = txtDeviceId.Text.Trim(),
                Status = PanelStatus.Inactif
            };

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void BtnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }
    }
}