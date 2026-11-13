// BALULA MOTO — chargement des données JSON, paquets de contenu, sauvegardes et réglages.
using System.Collections.Generic;
using System.IO;
using BalulaMoto.Core;
using UnityEngine;

namespace BalulaMoto.Runtime
{
    /// <summary>
    /// Charge les données de Resources/BalulaData puis les paquets de contenu facultatifs placés dans
    /// [persistentDataPath]/contenu/ (vehicules_*.json, missions_*.json, dialogues_*.json, lieux_*.json).
    /// Ajouter un véhicule ou une mission = ajouter une entrée JSON, sans modifier le code.
    /// </summary>
    public static class DataLoader
    {
        public const string Folder = "BalulaData/";

        public static GameDatabase LoadAll(List<string> report)
        {
            GameDatabase db = new GameDatabase();
            db.AddVehicles(Load<VehiclesFile>("vehicles", report));
            db.AddLocations(Load<LocationsFile>("locations_kinshasa", report));
            db.AddProvinces(Load<ProvincesFile>("provinces", report));
            db.AddMissions(Load<MissionsFile>("missions", report));
            db.AddDialogues(Load<DialoguesFile>("dialogues", report));
            db.AddReferences(Load<ReferencesFile>("references", report));
            db.Economy = Load<EconomyConfig>("economy", report);
            db.Clients = Load<ClientsConfig>("clients", report);
            LoadContentPacks(db, report);
            db.FinalizeData();
            foreach (string issue in db.Validate()) report.Add("Données : " + issue);
            return db;
        }

        private static T Load<T>(string name, List<string> report) where T : class
        {
            TextAsset asset = Resources.Load<TextAsset>(Folder + name);
            if (asset == null)
            {
                report.Add("Fichier manquant : Resources/" + Folder + name + ".json");
                return null;
            }
            try { return JsonUtility.FromJson<T>(asset.text); }
            catch (System.Exception e)
            {
                report.Add("JSON invalide (" + name + ") : " + e.Message);
                return null;
            }
        }

        private static void LoadContentPacks(GameDatabase db, List<string> report)
        {
            string dir = Path.Combine(Application.persistentDataPath, "contenu");
            if (!Directory.Exists(dir)) return;
            foreach (string file in Directory.GetFiles(dir, "*.json"))
            {
                string name = Path.GetFileName(file).ToLowerInvariant();
                try
                {
                    string json = File.ReadAllText(file);
                    if (name.StartsWith("vehicules")) db.AddVehicles(JsonUtility.FromJson<VehiclesFile>(json));
                    else if (name.StartsWith("missions")) db.AddMissions(JsonUtility.FromJson<MissionsFile>(json));
                    else if (name.StartsWith("dialogues")) db.AddDialogues(JsonUtility.FromJson<DialoguesFile>(json));
                    else if (name.StartsWith("lieux")) db.AddLocations(JsonUtility.FromJson<LocationsFile>(json));
                    else continue;
                    report.Add("Paquet de contenu chargé : " + name);
                }
                catch (System.Exception e)
                {
                    report.Add("Paquet de contenu ignoré (" + name + ") : " + e.Message);
                }
            }
        }
    }

    /// <summary>Sauvegardes JSON atomiques (fichier temporaire + copie de secours).</summary>
    public static class SaveService
    {
        public const string SlotCareer = "carriere";
        public const string SlotFree = "libre";

        public static string Dir
        {
            get
            {
                string d = Path.Combine(Application.persistentDataPath, "sauvegardes");
                if (!Directory.Exists(d)) Directory.CreateDirectory(d);
                return d;
            }
        }

        public static string PathFor(string slot) { return Path.Combine(Dir, slot + ".json"); }

        public static bool Exists(string slot) { return File.Exists(PathFor(slot)) || File.Exists(PathFor(slot) + ".bak"); }

        public static string LastSlot
        {
            get { return PlayerPrefs.GetString("balula_dernier_emplacement", ""); }
            set { PlayerPrefs.SetString("balula_dernier_emplacement", value); PlayerPrefs.Save(); }
        }

        public static bool Save(PlayerProfile p)
        {
            if (p == null || string.IsNullOrEmpty(p.emplacement)) return false;
            try
            {
                p.dateSauvegarde = System.DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm");
                string path = PathFor(p.emplacement);
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(p, false));
                if (File.Exists(path))
                {
                    if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
                    File.Move(path, path + ".bak");
                }
                File.Move(tmp, path);
                LastSlot = p.emplacement;
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError("[BALULA] Échec de sauvegarde : " + e.Message);
                return false;
            }
        }

        public static PlayerProfile Load(string slot)
        {
            PlayerProfile p = TryRead(PathFor(slot));
            if (p == null) p = TryRead(PathFor(slot) + ".bak");
            if (p != null)
            {
                p.emplacement = slot;
                p.Normalize();
            }
            return p;
        }

        private static PlayerProfile TryRead(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                PlayerProfile p = JsonUtility.FromJson<PlayerProfile>(File.ReadAllText(path));
                return p != null && p.vehicules != null && p.vehicules.Count > 0 ? p : null;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[BALULA] Sauvegarde illisible (" + path + ") : " + e.Message);
                return null;
            }
        }

        public static void Delete(string slot)
        {
            string path = PathFor(slot);
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
        }

        public static string Summary(string slot)
        {
            PlayerProfile p = Load(slot);
            if (p == null) return "Aucune partie";
            return "Jour " + p.jour + " · Niveau " + p.niveau + " · " + Cdf.Format(p.argent);
        }
    }

    /// <summary>Réglages globaux (persistés dans PlayerPrefs, recopiés dans le profil).</summary>
    public static class GameSettings
    {
        public static string Language
        {
            get { return PlayerPrefs.GetString("balula_langue", "Mixte"); }
            set { PlayerPrefs.SetString("balula_langue", value); }
        }

        public static int Quality
        {
            get { return PlayerPrefs.GetInt("balula_qualite", 1); }
            set { PlayerPrefs.SetInt("balula_qualite", Mathf.Clamp(value, 0, 2)); }
        }

        public static float Volume
        {
            get { return PlayerPrefs.GetFloat("balula_volume", 0.8f); }
            set { PlayerPrefs.SetFloat("balula_volume", Mathf.Clamp01(value)); }
        }

        public static float CameraSensitivity
        {
            get { return PlayerPrefs.GetFloat("balula_sensibilite", 1f); }
            set { PlayerPrefs.SetFloat("balula_sensibilite", Mathf.Clamp(value, 0.3f, 3f)); }
        }

        public static bool ShowHelp
        {
            get { return PlayerPrefs.GetInt("balula_aide", 1) == 1; }
            set { PlayerPrefs.SetInt("balula_aide", value ? 1 : 0); }
        }

        public static string QualityLabel(int q) { return q == 0 ? "Basse (économie batterie)" : (q == 1 ? "Moyenne" : "Haute"); }

        public static void Apply()
        {
            int q = Quality;
            int levels = QualitySettings.names.Length;
            if (levels > 0)
            {
                int index = q == 0 ? 0 : (q == 1 ? levels / 2 : levels - 1);
                QualitySettings.SetQualityLevel(index, true);
            }
            QualitySettings.shadowDistance = q == 0 ? 25f : (q == 1 ? 50f : 90f);
            Application.targetFrameRate = q == 0 ? 30 : 60;
            AudioListener.volume = Volume;
            PlayerPrefs.Save();
        }

        public static void CopyTo(PlayerProfile p)
        {
            if (p == null) return;
            if (p.parametres == null) p.parametres = PlayerProfile.DefaultSettings();
            p.parametres.langueDialogues = Language;
            p.parametres.qualite = Quality;
            p.parametres.volume = Volume;
            p.parametres.sensibiliteCamera = CameraSensitivity;
            p.parametres.afficherAide = ShowHelp;
        }
    }
}
