using System;

namespace APP_OptiSolar
{
    /// <summary>
    /// Modèle représentant un panneau solaire
    /// </summary>
    public class SolarPanel
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public PanelStatus Status { get; set; }
        public DateTime LastUpdate { get; set; }
        public string DeviceId { get; set; } // ID du device TTN

        // Données capteurs (venant de TTN)
        public double? Voltage { get; set; }

        public SolarPanel()
        {
            Id = Guid.NewGuid().ToString();
            LastUpdate = DateTime.Now;
            Status = PanelStatus.Inactif;
        }

        public string StatusText
        {
            get
            {
                return Status switch
                {
                    PanelStatus.Actif => "Actif",
                    PanelStatus.Inactif => "Inactif",
                    PanelStatus.Defectueux => "Défectueux",
                    _ => "Inconnu"
                };
            }
        }
    }

    public enum PanelStatus
    {
        Actif,
        Inactif,
        Defectueux
    }
}