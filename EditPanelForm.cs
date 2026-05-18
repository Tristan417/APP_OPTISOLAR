using System;
using System.Globalization;
using System.Windows.Forms;
using MaterialSkin;
using MaterialSkin.Controls;

namespace APP_OptiSolar
{
    /// <summary>
    /// Formulaire pour MODIFIER un panneau existant (différent de AddPanelForm qui CRÉE un nouveau panneau)
    /// </summary>
    public partial class EditPanelForm : MaterialForm
    {
        public SolarPanel Panel { get; private set; }

        private MaterialTextBox txtName;
        private MaterialTextBox txtLatitude;
        private MaterialTextBox txtLongitude;
        private MaterialTextBox txtDeviceId;
        private MaterialComboBox cmbStatus;
        private MaterialButton btnSave;
        private MaterialButton btnCancel;
        private MaterialLabel lblTitle;
        private MaterialLabel lblName;
        private MaterialLabel lblLatitude;
        private MaterialLabel lblLongitude;
        private MaterialLabel lblDeviceId;
        private MaterialLabel lblStatus;

        public EditPanelForm(SolarPanel panel)
        {
            Panel = panel;
            InitializeComponent();
            InitializeMaterialSkin();
            LoadPanelData(); // Charger les données existantes du panneau
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // Configuration du formulaire
            this.ClientSize = new System.Drawing.Size(450, 520);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Text = "Modifier un Panneau Solaire";

            // Titre
            lblTitle = new MaterialLabel
            {
                Text = "✏️ Modifier le Panneau",
                Font = new System.Drawing.Font("Roboto", 18F, System.Drawing.FontStyle.Bold),
                Location = new System.Drawing.Point(20, 80),
                AutoSize = true
            };

            // Label Nom
            lblName = new MaterialLabel
            {
                Text = "Nom du panneau",
                Location = new System.Drawing.Point(20, 140),
                AutoSize = true
            };

            // TextBox Nom
            txtName = new MaterialTextBox
            {
                Location = new System.Drawing.Point(20, 165),
                Size = new System.Drawing.Size(400, 50),
                Hint = "LP-001"
            };

            // Label Latitude
            lblLatitude = new MaterialLabel
            {
                Text = "Latitude",
                Location = new System.Drawing.Point(20, 225),
                AutoSize = true
            };

            // TextBox Latitude
            txtLatitude = new MaterialTextBox
            {
                Location = new System.Drawing.Point(20, 250),
                Size = new System.Drawing.Size(190, 50),
                Hint = "49.5654"
            };

            // Label Longitude
            lblLongitude = new MaterialLabel
            {
                Text = "Longitude",
                Location = new System.Drawing.Point(230, 225),
                AutoSize = true
            };

            // TextBox Longitude
            txtLongitude = new MaterialTextBox
            {
                Location = new System.Drawing.Point(230, 250),
                Size = new System.Drawing.Size(190, 50),
                Hint = "3.6242"
            };

            // Label Device ID
            lblDeviceId = new MaterialLabel
            {
                Text = "Device ID TTN (optionnel)",
                Location = new System.Drawing.Point(20, 310),
                AutoSize = true
            };

            // TextBox Device ID
            txtDeviceId = new MaterialTextBox
            {
                Location = new System.Drawing.Point(20, 335),
                Size = new System.Drawing.Size(400, 50),
                Hint = "eui-xxxxxxxxxxxxx"
            };

            // Label Statut
            lblStatus = new MaterialLabel
            {
                Text = "Statut",
                Location = new System.Drawing.Point(20, 395),
                AutoSize = true
            };

            // ComboBox Statut
            cmbStatus = new MaterialComboBox
            {
                Location = new System.Drawing.Point(20, 420),
                Size = new System.Drawing.Size(400, 50),
                Hint = "Sélectionner un statut"
            };
            cmbStatus.Items.Add("Actif");
            cmbStatus.Items.Add("Inactif");
            cmbStatus.Items.Add("Défectueux");

            // Bouton Enregistrer
            btnSave = new MaterialButton
            {
                Text = "💾 ENREGISTRER",
                Location = new System.Drawing.Point(240, 490),
                Size = new System.Drawing.Size(180, 40),
                Type = MaterialButton.MaterialButtonType.Contained,
                UseAccentColor = true
            };
            btnSave.Click += BtnSave_Click;

            // Bouton Annuler
            btnCancel = new MaterialButton
            {
                Text = "ANNULER",
                Location = new System.Drawing.Point(50, 490),
                Size = new System.Drawing.Size(180, 40),
                Type = MaterialButton.MaterialButtonType.Text
            };
            btnCancel.Click += BtnCancel_Click;

            // Ajouter les contrôles au formulaire
            this.Controls.Add(lblTitle);
            this.Controls.Add(lblName);
            this.Controls.Add(txtName);
            this.Controls.Add(lblLatitude);
            this.Controls.Add(txtLatitude);
            this.Controls.Add(lblLongitude);
            this.Controls.Add(txtLongitude);
            this.Controls.Add(lblDeviceId);
            this.Controls.Add(txtDeviceId);
            this.Controls.Add(lblStatus);
            this.Controls.Add(cmbStatus);
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

        /// <summary>
        /// Charger les données du panneau existant dans le formulaire
        /// </summary>
        private void LoadPanelData()
        {
            txtName.Text = Panel.Name;
            txtLatitude.Text = Panel.Latitude.ToString(CultureInfo.InvariantCulture);
            txtLongitude.Text = Panel.Longitude.ToString(CultureInfo.InvariantCulture);
            txtDeviceId.Text = Panel.DeviceId ?? "";

            // Sélectionner le statut actuel
            switch (Panel.Status)
            {
                case PanelStatus.Actif:
                    cmbStatus.SelectedIndex = 0;
                    break;
                case PanelStatus.Inactif:
                    cmbStatus.SelectedIndex = 1;
                    break;
                case PanelStatus.Defectueux:
                    cmbStatus.SelectedIndex = 2;
                    break;
            }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            // Validation
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                MessageBox.Show("Veuillez entrer un nom pour le panneau.", "Erreur",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Remplacer la virgule par un point pour accepter les deux formats
            string latitudeText = txtLatitude.Text.Replace(',', '.');
            string longitudeText = txtLongitude.Text.Replace(',', '.');

            // Parser avec InvariantCulture (format avec point)
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

            if (cmbStatus.SelectedIndex == -1)
            {
                MessageBox.Show("Veuillez sélectionner un statut.", "Erreur",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Mettre à jour les propriétés du panneau existant
            Panel.Name = txtName.Text.Trim();
            Panel.Latitude = latitude;
            Panel.Longitude = longitude;
            Panel.DeviceId = txtDeviceId.Text.Trim();

            switch (cmbStatus.SelectedIndex)
            {
                case 0:
                    Panel.Status = PanelStatus.Actif;
                    break;
                case 1:
                    Panel.Status = PanelStatus.Inactif;
                    break;
                case 2:
                    Panel.Status = PanelStatus.Defectueux;
                    break;
            }

            Panel.LastUpdate = DateTime.Now;

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
