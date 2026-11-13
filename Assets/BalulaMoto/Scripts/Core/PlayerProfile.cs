// BALULA MOTO — profil du joueur (contenu exact du fichier de sauvegarde).
using System;
using System.Collections.Generic;

namespace BalulaMoto.Core
{
    [Serializable]
    public class LedgerEntry
    {
        public int jour;
        public int minute;
        public string categorie;
        public string libelle;
        public long montant;
        public long solde;
    }

    [Serializable]
    public class OwnedVehicle
    {
        public string instanceId;
        public string defId;
        public string surnom;

        // État des composants, de 0 (hors d'usage) à 100 (neuf)
        public float moteur;
        public float pneus;
        public float freins;
        public float suspension;
        public float carrosserie;
        public float proprete;

        public float kmTotal;
        public float kmDepuisVidange;
        public float carburantL;

        // Niveaux d'amélioration mécanique (0..3)
        public int nivMoteur;
        public int nivAcceleration;
        public int nivFreinage;
        public int nivSuspension;
        public int nivPneus;
        public int nivTransmission;
        public int nivConsommation;

        // Personnalisation esthétique
        public string couleurPrincipale;
        public string couleurSecondaire;
        public string plaque;
        public string autocollant;
        public string jantes;
        public string pneusStyle;
        public string selle;
        public string retroviseurs;
        public string phares;
        public List<string> accessoires;

        public long prixAchat;
        public int jourAchat;

        public static readonly string[] Components = { "moteur", "pneus", "freins", "suspension", "carrosserie", "proprete" };
        public static readonly string[] Upgrades = { "moteur", "acceleration", "freinage", "suspension", "pneus", "transmission", "consommation" };

        public int GetUpgradeLevel(string upgradeId)
        {
            switch (upgradeId)
            {
                case "moteur": return nivMoteur;
                case "acceleration": return nivAcceleration;
                case "freinage": return nivFreinage;
                case "suspension": return nivSuspension;
                case "pneus": return nivPneus;
                case "transmission": return nivTransmission;
                case "consommation": return nivConsommation;
            }
            return 0;
        }

        public void SetUpgradeLevel(string upgradeId, int level)
        {
            switch (upgradeId)
            {
                case "moteur": nivMoteur = level; break;
                case "acceleration": nivAcceleration = level; break;
                case "freinage": nivFreinage = level; break;
                case "suspension": nivSuspension = level; break;
                case "pneus": nivPneus = level; break;
                case "transmission": nivTransmission = level; break;
                case "consommation": nivConsommation = level; break;
            }
        }

        public float GetCondition(string component)
        {
            switch (component)
            {
                case "moteur": return moteur;
                case "pneus": return pneus;
                case "freins": return freins;
                case "suspension": return suspension;
                case "carrosserie": return carrosserie;
                case "proprete": return proprete;
            }
            return 100f;
        }

        public void SetCondition(string component, float value)
        {
            value = MathUtil.Clamp(value, 0f, 100f);
            switch (component)
            {
                case "moteur": moteur = value; break;
                case "pneus": pneus = value; break;
                case "freins": freins = value; break;
                case "suspension": suspension = value; break;
                case "carrosserie": carrosserie = value; break;
                case "proprete": proprete = value; break;
            }
        }

        /// <summary>État moyen pondéré (mécanique surtout), 0..100.</summary>
        public float OverallCondition()
        {
            return moteur * 0.3f + pneus * 0.15f + freins * 0.2f + suspension * 0.15f + carrosserie * 0.15f + proprete * 0.05f;
        }

        public string GetCosmetic(string type)
        {
            switch (type)
            {
                case "couleur": return couleurPrincipale;
                case "couleur2": return couleurSecondaire;
                case "plaque": return plaque;
                case "autocollant": return autocollant;
                case "jantes": return jantes;
                case "pneus": return pneusStyle;
                case "selle": return selle;
                case "retroviseurs": return retroviseurs;
                case "phares": return phares;
            }
            return null;
        }

        public void SetCosmetic(string type, string value)
        {
            switch (type)
            {
                case "couleur": couleurPrincipale = value; break;
                case "couleur2": couleurSecondaire = value; break;
                case "plaque": plaque = value; break;
                case "autocollant": autocollant = value; break;
                case "jantes": jantes = value; break;
                case "pneus": pneusStyle = value; break;
                case "selle": selle = value; break;
                case "retroviseurs": retroviseurs = value; break;
                case "phares": phares = value; break;
                default:
                    if (accessoires == null) accessoires = new List<string>();
                    if (!string.IsNullOrEmpty(value) && !accessoires.Contains(value)) accessoires.Add(value);
                    break;
            }
        }
    }

    [Serializable]
    public class MissionProgress
    {
        public string id;
        public int progression;
        public bool active;
        public bool terminee;
        public int jourDebut;
        public int fois;
    }

    [Serializable]
    public class SettingsData
    {
        public string langueDialogues;
        public int qualite;
        public float volume;
        public float sensibiliteCamera;
        public bool commandesInclinaison;
        public bool afficherAide;
    }

    [Serializable]
    public class PlayerProfile
    {
        public const int CurrentVersion = 1;

        public int version;
        public string emplacement;
        public string mode;
        public string nomJoueur;

        public long argent;
        public int xp;
        public int niveau;

        public List<int> notes;
        public int nbCourses;
        public int nbLivraisons;
        public int nbAccidents;
        public int serieSansAccident;
        public int meilleureSerie;

        public List<OwnedVehicle> vehicules;
        public string vehiculeActif;
        public List<string> favoris;

        public List<LedgerEntry> historique;
        public long totalRevenus;
        public long totalDepenses;
        public long revenusDuJour;
        public long depensesDuJour;
        public int coursesDuJour;

        public List<MissionProgress> missions;
        public List<string> provincesDebloquees;
        public string provinceActuelle;

        public int jour;
        public float minuteDuJour;
        public string meteo;
        public List<string> lieuxVisites;

        public int infractions;
        public long amendesPayees;
        public bool rechercheParPolice;
        public int documentsValidesJusquAuJour;

        public SettingsData parametres;
        public string dateCreation;
        public string dateSauvegarde;
        public int prochainIdVehicule;
        public float tempsJeuSecondes;

        public GameMode Mode { get { return mode == "Libre" ? GameMode.Libre : GameMode.Carriere; } }

        /// <summary>Complète les listes nulles (anciennes sauvegardes, champs absents).</summary>
        public void Normalize()
        {
            if (notes == null) notes = new List<int>();
            if (vehicules == null) vehicules = new List<OwnedVehicle>();
            if (favoris == null) favoris = new List<string>();
            if (historique == null) historique = new List<LedgerEntry>();
            if (missions == null) missions = new List<MissionProgress>();
            if (provincesDebloquees == null) provincesDebloquees = new List<string>();
            if (lieuxVisites == null) lieuxVisites = new List<string>();
            if (parametres == null) parametres = DefaultSettings();
            if (string.IsNullOrEmpty(parametres.langueDialogues)) parametres.langueDialogues = "Mixte";
            if (parametres.volume <= 0f) parametres.volume = 0.8f;
            if (parametres.sensibiliteCamera <= 0f) parametres.sensibiliteCamera = 1f;
            if (niveau < 1) niveau = 1;
            if (jour < 1) jour = 1;
            if (string.IsNullOrEmpty(provinceActuelle)) provinceActuelle = "kinshasa";
            foreach (OwnedVehicle v in vehicules)
                if (v.accessoires == null) v.accessoires = new List<string>();
            version = CurrentVersion;
        }

        public static SettingsData DefaultSettings()
        {
            return new SettingsData { langueDialogues = "Mixte", qualite = 1, volume = 0.8f, sensibiliteCamera = 1f, commandesInclinaison = false, afficherAide = true };
        }

        public OwnedVehicle FindVehicle(string instanceId)
        {
            foreach (OwnedVehicle v in vehicules)
                if (v.instanceId == instanceId) return v;
            return null;
        }

        public MissionProgress FindMission(string missionId)
        {
            foreach (MissionProgress m in missions)
                if (m.id == missionId) return m;
            return null;
        }

        public float Reputation()
        {
            if (notes == null || notes.Count == 0) return 3.0f;
            float sum = 0;
            foreach (int n in notes) sum += n;
            return sum / notes.Count;
        }
    }
}
