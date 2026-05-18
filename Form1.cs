using System;
using System.Windows.Forms;

namespace APP_OptiSolar
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();

            // Lancer MainForm au chargement
            this.Load += Form1_Load;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            try
            {
                // Masquer Form1
                this.Hide();

                // Lancer MainForm
                MainForm mainForm = new MainForm();
                mainForm.FormClosed += MainForm_FormClosed;
                mainForm.Show();
            }
            catch (Exception ex)
            {
                // Afficher l'erreur pour diagnostiquer
                MessageBox.Show($"Erreur au lancement de MainForm:\n\n{ex.Message}\n\n{ex.StackTrace}",
                    "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.Close();
            }
        }

        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            // Fermer Form1 quand MainForm se ferme
            this.Close();
        }
    }
}