using System;
using System.Windows.Forms;

namespace APP_OptiSolar
{
    static class Program
    {
        /// <summary>
        /// Point d'entrée principal de l'application OptiSolar.
        /// Form1 sert de lanceur et ouvre MainForm automatiquement.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            // Lancer Form1 qui ouvrira MainForm
            Application.Run(new Form1());
        }
    }
}
