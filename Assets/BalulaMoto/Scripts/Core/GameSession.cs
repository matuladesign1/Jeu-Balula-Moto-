// BALULA MOTO — session de jeu : relie le profil, les données et tous les systèmes.
using System;
using System.Collections.Generic;

namespace BalulaMoto.Core
{
    public static class LedgerCategories
    {
        public const string Course = "Course";
        public const string Livraison = "Livraison";
        public const string Mission = "Mission";
        public const string Pourboire = "Pourboire";
        public const string VenteVehicule = "Vente de véhicule";
        public const string Carburant = "Carburant";
        public const string Reparation = "Réparation";
        public const string Entretien = "Entretien";
        public const string AchatVehicule = "Achat de véhicule";
        public const string Personnalisation = "Personnalisation";
        public const string Amelioration = "Amélioration";
        public const string Amende = "Amende";
        public const string Deblocage = "Déblocage de province";
        public const string Documents = "Documents du véhicule";
        public const string Depannage = "Dépannage";
    }

    public sealed class GameSession
    {
        public readonly GameDatabase Db;
        public readonly PlayerProfile Profile;
        public readonly GameRandom Rng;
        public readonly EconomySystem Economy;
        public readonly GarageSystem Garage;
        public readonly FuelSystem Fuel;
        public readonly WorkshopSystem Workshop;
        public readonly TaxiSystem Taxi;
        public readonly DialogueSystem Dialogue;
        public readonly CareerSystem Career;
        public readonly MissionSystem Missions;
        public readonly PoliceSystem Police;

        public event Action<string> Toast;
        public event Action MoneyChanged;
        public event Action<int> LevelUp;
        public event Action<MissionDef> MissionCompleted;
        public event Action VehiclesChanged;
        public event Action<int> NewDay;

        public GameSession(GameDatabase db, PlayerProfile profile, int seed)
        {
            Db = db;
            Profile = profile;
            Profile.Normalize();
            Rng = new GameRandom(seed);
            Economy = new EconomySystem(this);
            Garage = new GarageSystem(this);
            Fuel = new FuelSystem(this);
            Workshop = new WorkshopSystem(this);
            Taxi = new TaxiSystem(this);
            Dialogue = new DialogueSystem(this);
            Career = new CareerSystem(this);
            Missions = new MissionSystem(this);
            Police = new PoliceSystem(this);
            if (Profile.FindVehicle(Profile.vehiculeActif) == null && Profile.vehicules.Count > 0)
                Profile.vehiculeActif = Profile.vehicules[0].instanceId;
        }

        // ---------------- Notifications ----------------

        public void Notify(string message) { if (Toast != null) Toast(message); }
        internal void RaiseMoneyChanged() { if (MoneyChanged != null) MoneyChanged(); }
        internal void RaiseLevelUp(int level) { if (LevelUp != null) LevelUp(level); }
        internal void RaiseMissionCompleted(MissionDef m) { if (MissionCompleted != null) MissionCompleted(m); }
        internal void RaiseVehiclesChanged() { if (VehiclesChanged != null) VehiclesChanged(); }

        // ---------------- Véhicule actif ----------------

        public OwnedVehicle ActiveVehicle { get { return Profile.FindVehicle(Profile.vehiculeActif); } }

        public VehicleDef ActiveDef
        {
            get
            {
                OwnedVehicle v = ActiveVehicle;
                return v != null ? Db.Vehicle(v.defId) : null;
            }
        }

        public VehicleStats ActiveStats()
        {
            OwnedVehicle v = ActiveVehicle;
            VehicleDef d = ActiveDef;
            if (v == null || d == null) return null;
            return VehicleStatsCalculator.Compute(d, v, Db.Economy);
        }

        // ---------------- Temps de jeu ----------------

        public float Hour { get { return Profile.minuteDuJour / 60f; } }
        public bool IsNight { get { float h = Hour; return h >= 19f || h < 5.75f; } }
        public bool IsRushHour { get { float h = Hour; return (h >= 6.5f && h < 9f) || (h >= 16f && h < 19f); } }

        public DayPhase Phase
        {
            get
            {
                float h = Hour;
                if (h < 5.75f || h >= 19.5f) return DayPhase.Nuit;
                if (h < 11.5f) return DayPhase.Matin;
                if (h < 14f) return DayPhase.Midi;
                if (h < 17.5f) return DayPhase.ApresMidi;
                return DayPhase.Soir;
            }
        }

        public static string PhaseLabel(DayPhase p)
        {
            switch (p)
            {
                case DayPhase.Nuit: return "Nuit";
                case DayPhase.Matin: return "Matin";
                case DayPhase.Midi: return "Midi";
                case DayPhase.ApresMidi: return "Après-midi";
                case DayPhase.Soir: return "Soir";
            }
            return "";
        }

        public void AdvanceMinutes(float minutes)
        {
            if (minutes <= 0f) return;
            Profile.minuteDuJour += minutes;
            while (Profile.minuteDuJour >= 1440f)
            {
                Profile.minuteDuJour -= 1440f;
                StartNewDay();
            }
        }

        private void StartNewDay()
        {
            Profile.jour++;
            Profile.revenusDuJour = 0;
            Profile.depensesDuJour = 0;
            Profile.coursesDuJour = 0;
            Notify("Jour " + Profile.jour + " : une nouvelle journée commence.");
            if (!Police.DocumentsValid) Notify("Vos documents (permis, assurance, vignette) ne sont plus valides : renouvelez-les.");
            if (NewDay != null) NewDay(Profile.jour);
        }

        // ---------------- Création de partie ----------------

        public static PlayerProfile CreateProfile(GameDatabase db, GameMode mode, string startVehicleId, string playerName, string slot)
        {
            EconomyConfig eco = db.Economy;
            PlayerProfile p = new PlayerProfile();
            p.version = PlayerProfile.CurrentVersion;
            p.emplacement = slot;
            p.mode = mode == GameMode.Libre ? "Libre" : "Carriere";
            p.nomJoueur = string.IsNullOrEmpty(playerName) ? "Chauffeur" : playerName;
            p.argent = mode == GameMode.Libre ? eco.argentDepartLibre : eco.argentDepartCarriere;
            p.niveau = 1;
            p.jour = 1;
            p.minuteDuJour = 6 * 60 + 30;
            p.meteo = WeatherKind.Soleil.ToString();
            p.provinceActuelle = "kinshasa";
            p.dateCreation = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm");
            p.documentsValidesJusquAuJour = mode == GameMode.Libre ? 99999 : 7;
            p.Normalize();
            foreach (ProvinceDef prov in db.Provinces)
                if (prov.debloqueeAuDepart || mode == GameMode.Libre) p.provincesDebloquees.Add(prov.id);
            if (!p.provincesDebloquees.Contains("kinshasa")) p.provincesDebloquees.Add("kinshasa");

            VehicleDef def = db.Vehicle(startVehicleId);
            if (def == null)
            {
                List<VehicleDef> starters = db.StartingVehicles();
                def = starters.Count > 0 ? starters[0] : db.Vehicles[0];
            }
            float condition = mode == GameMode.Libre ? 100f : MathUtil.Clamp(eco.etatDepartCarriere, 30f, 100f);
            OwnedVehicle v = GarageSystem.NewOwnedVehicle(p, def, condition);
            v.carburantL = def.reservoirL * (mode == GameMode.Libre ? 1f : 0.45f);
            v.kmTotal = mode == GameMode.Libre ? 0f : 18000f;
            v.kmDepuisVidange = mode == GameMode.Libre ? 0f : eco.intervalleVidangeKm * 0.6f;
            v.proprete = mode == GameMode.Libre ? 100f : 65f;
            v.prixAchat = GarageSystem.PriceCdf(def, eco);
            p.vehicules.Add(v);
            p.vehiculeActif = v.instanceId;
            return p;
        }
    }

    // =====================================================================
    // Économie
    // =====================================================================
    public sealed class EconomySystem
    {
        private readonly GameSession s;
        public EconomySystem(GameSession session) { s = session; }

        public long Balance { get { return s.Profile.argent; } }

        public bool CanAfford(long amount) { return amount >= 0 && amount <= s.Profile.argent; }

        public void Earn(long amount, string category, string label)
        {
            if (amount <= 0) return;
            PlayerProfile p = s.Profile;
            p.argent += amount;
            p.totalRevenus += amount;
            p.revenusDuJour += amount;
            AddEntry(category, label, amount);
            s.RaiseMoneyChanged();
            s.Missions.OnMoneyChanged();
        }

        public bool Spend(long amount, string category, string label)
        {
            if (amount < 0) return false;
            PlayerProfile p = s.Profile;
            if (amount > p.argent) return false;
            p.argent -= amount;
            p.totalDepenses += amount;
            p.depensesDuJour += amount;
            AddEntry(category, label, -amount);
            s.RaiseMoneyChanged();
            s.Missions.OnMoneyChanged();
            return true;
        }

        private void AddEntry(string category, string label, long signedAmount)
        {
            PlayerProfile p = s.Profile;
            p.historique.Add(new LedgerEntry
            {
                jour = p.jour,
                minute = (int)p.minuteDuJour,
                categorie = category,
                libelle = label,
                montant = signedAmount,
                solde = p.argent
            });
            const int max = 400;
            if (p.historique.Count > max) p.historique.RemoveRange(0, p.historique.Count - max);
        }

        /// <summary>Totaux par catégorie sur les N derniers jours (0 = tout l'historique conservé).</summary>
        public Dictionary<string, long> TotalsByCategory(int lastDays)
        {
            Dictionary<string, long> totals = new Dictionary<string, long>();
            int fromDay = lastDays > 0 ? s.Profile.jour - lastDays + 1 : int.MinValue;
            foreach (LedgerEntry e in s.Profile.historique)
            {
                if (e.jour < fromDay) continue;
                long t;
                totals.TryGetValue(e.categorie, out t);
                totals[e.categorie] = t + e.montant;
            }
            return totals;
        }
    }

    // =====================================================================
    // Carrière : expérience, niveaux, provinces
    // =====================================================================
    public sealed class CareerSystem
    {
        private readonly GameSession s;
        public CareerSystem(GameSession session) { s = session; }

        /// <summary>XP cumulée nécessaire pour atteindre un niveau (niveau 2 = 100 XP, niveau 5 = 1000 XP...).</summary>
        public static int XpForLevel(int level) { return level <= 1 ? 0 : 50 * level * (level - 1); }

        public float LevelProgress01
        {
            get
            {
                int lvl = s.Profile.niveau;
                int a = XpForLevel(lvl), b = XpForLevel(lvl + 1);
                return MathUtil.Clamp01((s.Profile.xp - a) / (float)Math.Max(1, b - a));
            }
        }

        public void AddXp(int amount)
        {
            if (amount <= 0) return;
            PlayerProfile p = s.Profile;
            p.xp += amount;
            bool changed = false;
            while (p.xp >= XpForLevel(p.niveau + 1))
            {
                p.niveau++;
                changed = true;
                s.Notify("Niveau " + p.niveau + " atteint !");
                s.RaiseLevelUp(p.niveau);
            }
            if (changed) s.Missions.OnLevelChanged();
        }

        public float Reputation { get { return s.Profile.Reputation(); } }

        /// <summary>Nombre de missions réussies, répétitions des contrats comprises.</summary>
        public int CompletedMissionCount()
        {
            int n = 0;
            foreach (MissionProgress m in s.Profile.missions) n += m.fois;
            return n;
        }

        public bool IsUnlocked(string provinceId) { return s.Profile.provincesDebloquees.Contains(provinceId); }

        public List<string> MissingRequirements(ProvinceDef prov)
        {
            List<string> missing = new List<string>();
            PlayerProfile p = s.Profile;
            if (IsUnlocked(prov.id)) return missing;
            ProvinceDef previous = null;
            foreach (ProvinceDef q in s.Db.Provinces)
                if (q.ordre < prov.ordre && (previous == null || q.ordre > previous.ordre)) previous = q;
            if (previous != null && !IsUnlocked(previous.id)) missing.Add("Débloquer d'abord : " + previous.nom);
            if (p.niveau < prov.niveauMin) missing.Add("Niveau " + prov.niveauMin + " requis (actuel : " + p.niveau + ")");
            if (prov.reputationMin > 0f && (p.notes.Count < 5 || p.Reputation() < prov.reputationMin))
                missing.Add("Réputation " + TextUtil.Rating(prov.reputationMin) + " sur au moins 5 courses");
            int done = CompletedMissionCount();
            if (done < prov.missionsMin) missing.Add(prov.missionsMin + " missions réussies requises (" + done + " faites)");
            if (!string.IsNullOrEmpty(prov.missionRequise))
            {
                MissionProgress mp = p.FindMission(prov.missionRequise);
                MissionDef md = s.Db.Mission(prov.missionRequise);
                if (mp == null || (!mp.terminee && mp.fois == 0)) missing.Add("Mission requise : " + (md != null ? md.titre : prov.missionRequise));
            }
            if (p.argent < prov.coutCdf) missing.Add("Frais de voyage et d'installation : " + Cdf.Format(prov.coutCdf));
            return missing;
        }

        public bool Unlock(string provinceId, out string error)
        {
            error = null;
            ProvinceDef prov = s.Db.Province(provinceId);
            if (prov == null) { error = "Province inconnue."; return false; }
            if (IsUnlocked(provinceId)) { error = "Déjà débloquée."; return false; }
            List<string> missing = MissingRequirements(prov);
            if (missing.Count > 0) { error = string.Join("\n", missing.ToArray()); return false; }
            if (!s.Economy.Spend(prov.coutCdf, LedgerCategories.Deblocage, prov.nom)) { error = "Solde insuffisant."; return false; }
            s.Profile.provincesDebloquees.Add(provinceId);
            s.Notify("Province débloquée : " + prov.nom + " (" + prov.chefLieu + ")");
            s.Missions.OnProvinceUnlocked(provinceId);
            return true;
        }
    }

    // =====================================================================
    // Police et documents (amendes fictives)
    // =====================================================================
    public sealed class PoliceSystem
    {
        public const string RedLight = "feu_rouge";
        public const string Speeding = "exces_vitesse";
        public const string Dangerous = "conduite_dangereuse";
        public const string NoLights = "sans_phare";
        public const string NoDocuments = "documents";
        public const string Fleeing = "refus_obtemperer";
        public const string Pedestrian = "pieton_bouscule";
        public const string Collision = "accrochage";

        private readonly GameSession s;
        public PoliceSystem(GameSession session) { s = session; }

        public bool DocumentsValid { get { return s.Profile.documentsValidesJusquAuJour >= s.Profile.jour; } }

        public string InfractionLabel(string id)
        {
            FineDef f = s.Db.Fine(id);
            return f != null ? f.libelle : id;
        }

        public long FineAmount(string infractionId, bool fled)
        {
            FineDef f = s.Db.Fine(infractionId);
            long amount = f != null ? f.montant : 10000;
            if (fled)
            {
                FineDef flee = s.Db.Fine(Fleeing);
                amount = amount * 2 + (flee != null ? flee.montant : 50000);
            }
            return amount;
        }

        public void RecordInfraction() { s.Profile.infractions++; }

        /// <summary>Contester poliment : chance d'obtenir un simple avertissement selon la réputation.</summary>
        public bool ContestPolitely()
        {
            float chance = 0.25f + 0.08f * (s.Profile.Reputation() - 3f);
            if (s.Profile.infractions > 5) chance -= 0.1f;
            return s.Rng.Chance(MathUtil.Clamp(chance, 0.05f, 0.6f));
        }

        /// <summary>Paie l'amende. Si le solde est insuffisant, on paie ce qu'on peut et le reste est perdu en réputation.</summary>
        public long PayFine(string infractionId, bool fled)
        {
            long amount = FineAmount(infractionId, fled);
            long paid = Math.Min(amount, s.Profile.argent);
            if (paid > 0) s.Economy.Spend(paid, LedgerCategories.Amende, InfractionLabel(infractionId) + (fled ? " + refus d'obtempérer" : ""));
            s.Profile.amendesPayees += paid;
            s.Profile.rechercheParPolice = false;
            if (paid < amount && s.Profile.notes.Count > 0) s.Profile.notes.Add(1);
            return paid;
        }

        public bool RenewDocuments(out string error)
        {
            error = null;
            DocumentsDef d = s.Db.Economy.documents;
            if (!s.Economy.Spend(d.prix, LedgerCategories.Documents, d.description))
            {
                error = "Il faut " + Cdf.Format(d.prix) + " pour renouveler les documents.";
                return false;
            }
            int start = Math.Max(s.Profile.jour, s.Profile.documentsValidesJusquAuJour);
            s.Profile.documentsValidesJusquAuJour = start + d.dureeJours;
            s.Notify("Documents en règle jusqu'au jour " + s.Profile.documentsValidesJusquAuJour + ".");
            return true;
        }
    }

    // =====================================================================
    // Dialogues en français et en lingala
    // =====================================================================
    public sealed class DialogueSystem
    {
        private readonly GameSession s;
        public DialogueSystem(GameSession session) { s = session; }

        public DialogueLanguage Preference
        {
            get
            {
                string l = s.Profile.parametres != null ? s.Profile.parametres.langueDialogues : "Mixte";
                if (l == "Francais") return DialogueLanguage.Francais;
                if (l == "Lingala") return DialogueLanguage.Lingala;
                return DialogueLanguage.Mixte;
            }
        }

        public bool HasKey(string key)
        {
            foreach (DialogueEntry d in s.Db.Dialogues) if (d.cle == key) return true;
            return false;
        }

        /// <summary>Choisit une réplique pour la situation "key" (et le quartier si une variante existe).</summary>
        public string Line(string key, string district, DialogueLanguage language, params string[] keyValues)
        {
            List<DialogueEntry> general = new List<DialogueEntry>();
            List<DialogueEntry> local = new List<DialogueEntry>();
            string nd = TextUtil.Normalize(district);
            foreach (DialogueEntry d in s.Db.Dialogues)
            {
                if (d.cle != key) continue;
                if (string.IsNullOrEmpty(d.quartier)) general.Add(d);
                else if (nd.Length > 0 && nd.Contains(TextUtil.Normalize(d.quartier))) local.Add(d);
            }
            List<DialogueEntry> pool = local.Count > 0 && s.Rng.Chance(0.6f) ? local : general;
            if (pool.Count == 0) pool = local;
            if (pool.Count == 0) return "";
            DialogueEntry e = s.Rng.Pick(pool);
            List<string> lines;
            if (language == DialogueLanguage.Francais) lines = e.fr;
            else if (language == DialogueLanguage.Lingala) lines = e.ln.Count > 0 ? e.ln : e.fr;
            else lines = (e.ln.Count > 0 && (e.fr.Count == 0 || s.Rng.Chance(0.5f))) ? e.ln : e.fr;
            if (lines == null || lines.Count == 0) lines = e.fr.Count > 0 ? e.fr : e.ln;
            if (lines == null || lines.Count == 0) return "";
            return TextUtil.Fill(s.Rng.Pick(lines), keyValues);
        }

        public string Line(string key, string district, params string[] keyValues)
        {
            return Line(key, district, Preference, keyValues);
        }
    }
}
