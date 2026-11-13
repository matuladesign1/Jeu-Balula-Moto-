// BALULA MOTO — véhicules : caractéristiques effectives, usure, carburant, magasin, garage, ateliers.
using System;
using System.Collections.Generic;

namespace BalulaMoto.Core
{
    /// <summary>Caractéristiques effectives d'un véhicule (définition + améliorations + usure).</summary>
    public sealed class VehicleStats
    {
        public VehicleKind Kind;
        public float MaxSpeedKmh;
        public float PowerKw;
        public float LowSpeedTraction;
        public float BrakeDecel;
        public float Grip;
        public float Comfort01;
        public float Stability01;
        public float ConsumptionL100;
        public float TankL;
        public float MassKg;
        public int Passengers;
        public int FrontSeats;
        public int RearSeats;
        public float CargoKg;
        public float Resistance01;
        public bool HasReverse;
        public int Gears;
        public float Accel0To50s;
    }

    public static class VehicleStatsCalculator
    {
        public static float UpgradeBonus(EconomyConfig eco, OwnedVehicle v, string upgradeId)
        {
            if (eco == null || v == null) return 0f;
            UpgradeDef u = eco.ameliorations != null ? eco.ameliorations.Find(x => x.id == upgradeId) : null;
            if (u == null) return 0f;
            return u.bonusParNiveau * v.GetUpgradeLevel(upgradeId);
        }

        public static VehicleStats Compute(VehicleDef d, OwnedVehicle v, EconomyConfig eco)
        {
            VehicleStats s = new VehicleStats();
            s.Kind = d.Kind;
            float engine = v != null ? v.moteur / 100f : 1f;
            float tyres = v != null ? v.pneus / 100f : 1f;
            float brakes = v != null ? v.freins / 100f : 1f;
            float susp = v != null ? v.suspension / 100f : 1f;
            float clean = v != null ? v.proprete / 100f : 1f;
            bool oilOverdue = v != null && eco != null && v.kmDepuisVidange > eco.intervalleVidangeKm;

            float engineHealth = 0.55f + 0.45f * engine;
            if (oilOverdue) engineHealth *= 0.95f;

            s.PowerKw = d.puissanceKw * (1f + UpgradeBonus(eco, v, "moteur")) * engineHealth;
            s.LowSpeedTraction = 1f + UpgradeBonus(eco, v, "acceleration");
            s.MaxSpeedKmh = d.vitesseMaxKmh * (1f + UpgradeBonus(eco, v, "transmission")) * (0.85f + 0.15f * engine);
            s.BrakeDecel = (3.5f + 4.5f * (d.freinage / 100f)) * (1f + UpgradeBonus(eco, v, "freinage")) * (0.45f + 0.55f * brakes);
            s.Grip = (1f + UpgradeBonus(eco, v, "pneus")) * (0.65f + 0.35f * tyres);
            s.Comfort01 = MathUtil.Clamp01(d.confort / 100f * (1f + UpgradeBonus(eco, v, "suspension")) * (0.6f + 0.4f * susp) * (0.85f + 0.15f * clean));
            s.Stability01 = MathUtil.Clamp01(d.stabilite / 100f * (1f + 0.5f * UpgradeBonus(eco, v, "suspension")) * (0.8f + 0.2f * susp));
            s.ConsumptionL100 = d.consommationL100 * (1f - UpgradeBonus(eco, v, "consommation")) * (1f + 0.35f * (1f - engine)) * (oilOverdue ? 1.08f : 1f);
            s.TankL = d.reservoirL;
            s.MassKg = d.masseKg;
            s.Passengers = d.passagers;
            s.FrontSeats = d.placesAvant;
            s.RearSeats = d.placesArriere;
            s.CargoKg = d.chargeUtileKg;
            s.Resistance01 = MathUtil.Clamp01(d.resistance / 100f);
            s.HasReverse = d.marcheArriere;
            s.Gears = d.rapports;
            float riderMass = 75f;
            s.Accel0To50s = EstimateAccel0To50(s.PowerKw, d.masseKg + riderMass, s.Grip * s.LowSpeedTraction, s.MaxSpeedKmh, d.Kind);
            return s;
        }

        /// <summary>Estimation du 0-50 km/h avec le même modèle que la physique (puissance, adhérence, traînée).</summary>
        public static float EstimateAccel0To50(float powerKw, float massKg, float tractionFactor, float maxSpeedKmh, VehicleKind kind)
        {
            float target = 50f / 3.6f;
            float vmax = maxSpeedKmh / 3.6f;
            if (vmax <= target) return 99f;
            float p = powerKw * 1000f;
            float cda = 2f * p / (1.2f * vmax * vmax * vmax);
            float maxTraction = massKg * 9.81f * (kind == VehicleKind.Moto ? 0.5f : 0.45f) * 0.8f * tractionFactor;
            float v = 0.5f, t = 0f, dt = 0.02f;
            while (v < target && t < 60f)
            {
                float drive = Math.Min(maxTraction, p / Math.Max(v, 2f));
                float drag = 0.6f * cda * v * v + massKg * 9.81f * 0.015f;
                v += (drive - drag) / massKg * dt;
                t += dt;
            }
            return t;
        }

        public static string Grade(float value01)
        {
            if (value01 >= 0.85f) return "Excellent";
            if (value01 >= 0.7f) return "Très bon";
            if (value01 >= 0.5f) return "Correct";
            if (value01 >= 0.3f) return "Faible";
            return "Très faible";
        }
    }

    /// <summary>Usure progressive : un véhicule mal entretenu perd des performances.</summary>
    public static class VehicleWear
    {
        public static void ApplyDistance(OwnedVehicle v, EconomyConfig eco, float km, SurfaceKind surface, bool wet, float throttle01)
        {
            if (v == null || km <= 0f) return;
            float k = eco != null ? eco.facteurUsure : 1f;
            float rough = SurfaceInfo.Wear(surface);
            v.kmTotal += km;
            v.kmDepuisVidange += km;
            bool oilOverdue = eco != null && v.kmDepuisVidange > eco.intervalleVidangeKm;
            v.pneus = MathUtil.Clamp(v.pneus - km * 0.035f * rough * k, 0f, 100f);
            v.suspension = MathUtil.Clamp(v.suspension - km * 0.012f * rough * k, 0f, 100f);
            v.moteur = MathUtil.Clamp(v.moteur - km * 0.006f * k * (0.6f + throttle01) * (oilOverdue ? 3.5f : 1f), 0f, 100f);
            float dirt = (SurfaceInfo.IsDirty(surface) ? 2.5f : 1f) * (wet ? 1.8f : 1f);
            v.proprete = MathUtil.Clamp(v.proprete - km * 0.5f * dirt * k, 0f, 100f);
        }

        public static void ApplyBraking(OwnedVehicle v, EconomyConfig eco, float speedLostMs)
        {
            if (v == null || speedLostMs <= 0f) return;
            float k = eco != null ? eco.facteurUsure : 1f;
            v.freins = MathUtil.Clamp(v.freins - speedLostMs * 0.0012f * k, 0f, 100f);
        }

        /// <summary>Choc : retourne la gravité 0..1 et dégrade carrosserie / moteur / suspension.</summary>
        public static float ApplyImpact(OwnedVehicle v, float deltaVms, float resistance01)
        {
            if (v == null || deltaVms < 1.5f) return 0f;
            float severity = MathUtil.Clamp01((deltaVms - 1.5f) / 10f);
            float protection = 1.25f - resistance01 * 0.6f;
            v.carrosserie = MathUtil.Clamp(v.carrosserie - deltaVms * 3.2f * protection, 0f, 100f);
            if (deltaVms > 5f) v.moteur = MathUtil.Clamp(v.moteur - (deltaVms - 5f) * 2.2f * protection, 0f, 100f);
            if (deltaVms > 3f) v.suspension = MathUtil.Clamp(v.suspension - (deltaVms - 3f) * 1.5f * protection, 0f, 100f);
            v.proprete = MathUtil.Clamp(v.proprete - deltaVms, 0f, 100f);
            return severity;
        }

        public static void ApplyPothole(OwnedVehicle v, EconomyConfig eco, float speedKmh)
        {
            if (v == null || speedKmh < 18f) return;
            float k = eco != null ? eco.facteurUsure : 1f;
            float hit = (speedKmh - 18f) * 0.04f * k;
            v.suspension = MathUtil.Clamp(v.suspension - hit, 0f, 100f);
            v.pneus = MathUtil.Clamp(v.pneus - hit * 0.5f, 0f, 100f);
        }

        public static bool OilOverdue(OwnedVehicle v, EconomyConfig eco)
        {
            return v != null && eco != null && v.kmDepuisVidange > eco.intervalleVidangeKm;
        }

        /// <summary>Messages d'alerte d'entretien à afficher au joueur.</summary>
        public static List<string> Warnings(OwnedVehicle v, EconomyConfig eco)
        {
            List<string> w = new List<string>();
            if (v == null) return w;
            if (OilOverdue(v, eco)) w.Add("Vidange à faire");
            if (v.pneus < 30f) w.Add("Pneus usés");
            if (v.freins < 30f) w.Add("Freins usés");
            if (v.moteur < 35f) w.Add("Moteur fatigué");
            if (v.suspension < 30f) w.Add("Suspensions fatiguées");
            if (v.carrosserie < 40f) w.Add("Carrosserie abîmée");
            return w;
        }
    }

    public sealed class RefuelResult
    {
        public bool Success;
        public float Liters;
        public long Cost;
        public bool BadQuality;
        public string Message;
    }

    /// <summary>Carburant : consommation, stations-service, points de vente et vendeurs Kadafi.</summary>
    public sealed class FuelSystem
    {
        private readonly GameSession s;
        public FuelSystem(GameSession session) { s = session; }

        /// <summary>Consomme le carburant pour un trajet. Retourne vrai si le réservoir vient de se vider.</summary>
        public bool Consume(OwnedVehicle v, VehicleStats st, float km, float throttle01, float idleSeconds)
        {
            if (v == null || st == null) return false;
            if (v.carburantL <= 0f) return false;
            float k = s.Db.Economy.facteurConsommation;
            float liters = km * st.ConsumptionL100 / 100f * (0.55f + 0.9f * MathUtil.Clamp01(throttle01)) * k;
            liters += idleSeconds * 0.00012f * k * (st.Kind == VehicleKind.Moto ? 1f : 1.6f);
            v.carburantL = Math.Max(0f, v.carburantL - liters);
            return v.carburantL <= 0f;
        }

        public float Fraction(OwnedVehicle v, VehicleStats st)
        {
            if (v == null || st == null || st.TankL <= 0f) return 0f;
            return MathUtil.Clamp01(v.carburantL / st.TankL);
        }

        public long Price(FuelVendorDef vendor, float liters)
        {
            if (vendor == null || liters <= 0f) return 0;
            return Cdf.RoundUpTo(liters * vendor.prixLitre, 50);
        }

        public float MissingLiters(OwnedVehicle v, VehicleStats st)
        {
            return Math.Max(0f, st.TankL - v.carburantL);
        }

        public RefuelResult Refuel(OwnedVehicle v, VehicleStats st, string vendorId, float liters)
        {
            RefuelResult r = new RefuelResult();
            FuelVendorDef vendor = s.Db.FuelVendor(vendorId);
            if (vendor == null || v == null || st == null) { r.Message = "Vendeur introuvable."; return r; }
            liters = Math.Min(liters, MissingLiters(v, st));
            if (liters < 0.1f) { r.Message = "Le réservoir est déjà plein."; return r; }
            if (vendor.litresMin > 0f && liters < vendor.litresMin) liters = Math.Min(vendor.litresMin, MissingLiters(v, st));
            long cost = Price(vendor, liters);
            if (!s.Economy.CanAfford(cost))
            {
                // On achète ce que l'on peut payer
                liters = (float)Math.Floor(s.Profile.argent / (double)vendor.prixLitre * 10.0) / 10f;
                if (liters < 0.1f) { r.Message = "Pas assez d'argent pour acheter du carburant."; return r; }
                cost = Price(vendor, liters);
                if (!s.Economy.CanAfford(cost)) { liters -= 0.1f; cost = Price(vendor, liters); }
                if (liters < 0.1f || !s.Economy.CanAfford(cost)) { r.Message = "Pas assez d'argent pour acheter du carburant."; return r; }
            }
            s.Economy.Spend(cost, LedgerCategories.Carburant, vendor.nom + " — " + liters.ToString("0.0") + " L");
            v.carburantL = Math.Min(st.TankL, v.carburantL + liters);
            r.Success = true;
            r.Liters = liters;
            r.Cost = cost;
            if (vendor.risqueQualite > 0f && s.Rng.Chance(vendor.risqueQualite))
            {
                r.BadQuality = true;
                v.moteur = MathUtil.Clamp(v.moteur - s.Rng.Range(4f, 11f), 0f, 100f);
            }
            r.Message = liters.ToString("0.0") + " L pour " + Cdf.Format(cost) + (r.BadQuality ? " — carburant de mauvaise qualité, le moteur a souffert !" : "");
            return r;
        }

        /// <summary>Panne sèche : un vendeur Kadafi apporte quelques litres (prix majoré + livraison).</summary>
        public RefuelResult EmergencyDelivery(OwnedVehicle v, VehicleStats st, float liters, long deliveryFee)
        {
            RefuelResult r = new RefuelResult();
            FuelVendorDef kadafi = s.Db.FuelVendor("kadafi");
            long perLiter = kadafi != null ? kadafi.prixLitre : 3500;
            liters = Math.Min(liters, MissingLiters(v, st));
            long cost = Cdf.RoundUpTo(liters * perLiter, 50) + deliveryFee;
            if (!s.Economy.Spend(cost, LedgerCategories.Depannage, "Kadafi livré sur place — " + liters.ToString("0.0") + " L"))
            {
                r.Message = "Il faut " + Cdf.Format(cost) + " pour se faire livrer du carburant.";
                return r;
            }
            v.carburantL = Math.Min(st.TankL, v.carburantL + liters);
            r.Success = true;
            r.Liters = liters;
            r.Cost = cost;
            r.Message = "Un Kadafi vous apporte " + liters.ToString("0.0") + " L (" + Cdf.Format(cost) + ").";
            return r;
        }
    }

    /// <summary>Magasin et garage : achats, ventes, véhicule actif, améliorations, personnalisation.</summary>
    public sealed class GarageSystem
    {
        private readonly GameSession s;
        public GarageSystem(GameSession session) { s = session; }

        public static long PriceCdf(VehicleDef d, EconomyConfig eco)
        {
            double factor = eco != null && eco.facteurPrixVehicules > 0 ? eco.facteurPrixVehicules : 1.0;
            double baseCdf = d.prixCdf > 0 ? d.prixCdf : d.prixUsd * (eco != null ? eco.tauxUsdCdf : 2265.0);
            return Cdf.RoundTo(baseCdf * factor, 10000);
        }

        public long Price(VehicleDef d) { return PriceCdf(d, s.Db.Economy); }

        public static OwnedVehicle NewOwnedVehicle(PlayerProfile p, VehicleDef def, float condition)
        {
            p.prochainIdVehicule++;
            OwnedVehicle v = new OwnedVehicle();
            v.instanceId = "V" + p.prochainIdVehicule.ToString("000");
            v.defId = def.id;
            v.surnom = def.nom;
            v.moteur = condition;
            v.pneus = condition;
            v.freins = condition;
            v.suspension = condition;
            v.carrosserie = condition;
            v.proprete = 100f;
            v.carburantL = Math.Min(2f, def.reservoirL);
            v.couleurPrincipale = def.couleurPrincipale;
            v.couleurSecondaire = def.couleurSecondaire;
            v.plaque = FictionalPlate(p.prochainIdVehicule);
            v.accessoires = new List<string>();
            v.jourAchat = p.jour;
            return v;
        }

        /// <summary>Plaque fictive (ne reproduit pas le format officiel).</summary>
        public static string FictionalPlate(int number)
        {
            return "BLM-" + (1000 + number * 37 % 9000).ToString("0000") + "-KN";
        }

        public bool Owns(string defId)
        {
            foreach (OwnedVehicle v in s.Profile.vehicules) if (v.defId == defId) return true;
            return false;
        }

        public int CountOwned(string defId)
        {
            int n = 0;
            foreach (OwnedVehicle v in s.Profile.vehicules) if (v.defId == defId) n++;
            return n;
        }

        public bool OwnsCategory(VehicleCategory category)
        {
            foreach (OwnedVehicle v in s.Profile.vehicules)
            {
                VehicleDef d = s.Db.Vehicle(v.defId);
                if (d != null && d.Category == category) return true;
            }
            return false;
        }

        /// <summary>Achat : le véhicule s'ajoute à la collection (jamais de revente forcée).</summary>
        public OwnedVehicle Buy(string defId, out string error)
        {
            error = null;
            VehicleDef d = s.Db.Vehicle(defId);
            if (d == null) { error = "Véhicule inconnu."; return null; }
            long price = Price(d);
            if (!s.Economy.CanAfford(price))
            {
                error = "Solde insuffisant : il manque " + Cdf.Format(price - s.Profile.argent) + ".";
                return null;
            }
            s.Economy.Spend(price, LedgerCategories.AchatVehicule, d.nom);
            OwnedVehicle v = NewOwnedVehicle(s.Profile, d, 100f);
            v.prixAchat = price;
            s.Profile.vehicules.Add(v);
            s.Notify(d.nom + " ajouté à votre garage.");
            s.RaiseVehiclesChanged();
            s.Missions.OnVehicleOwned();
            return v;
        }

        public long SellPrice(OwnedVehicle v)
        {
            VehicleDef d = s.Db.Vehicle(v.defId);
            if (d == null) return 0;
            EconomyConfig eco = s.Db.Economy;
            long reference = v.prixAchat > 0 ? v.prixAchat : Price(d);
            float condition = v.OverallCondition() / 100f;
            float mileage = MathUtil.Clamp01(v.kmTotal / 40000f);
            float min = eco.decoteVenteMin > 0f ? eco.decoteVenteMin : 0.35f;
            float max = eco.decoteVenteMax > 0f ? eco.decoteVenteMax : 0.75f;
            float factor = MathUtil.Lerp(min, max, condition * (1f - 0.5f * mileage));
            float upgrades = 0f;
            foreach (string u in OwnedVehicle.Upgrades) upgrades += v.GetUpgradeLevel(u);
            factor += upgrades * 0.01f;
            return Cdf.RoundTo(reference * factor, 5000);
        }

        public bool Sell(string instanceId, out string error)
        {
            error = null;
            PlayerProfile p = s.Profile;
            OwnedVehicle v = p.FindVehicle(instanceId);
            if (v == null) { error = "Véhicule introuvable."; return false; }
            if (p.vehicules.Count <= 1) { error = "Vous devez garder au moins un véhicule pour travailler."; return false; }
            long price = SellPrice(v);
            VehicleDef d = s.Db.Vehicle(v.defId);
            p.vehicules.Remove(v);
            if (p.vehiculeActif == instanceId) p.vehiculeActif = p.vehicules[0].instanceId;
            s.Economy.Earn(price, LedgerCategories.VenteVehicule, d != null ? d.nom : v.defId);
            s.Notify("Véhicule vendu pour " + Cdf.Format(price) + ".");
            s.RaiseVehiclesChanged();
            return true;
        }

        public bool SetActive(string instanceId)
        {
            if (s.Profile.FindVehicle(instanceId) == null) return false;
            s.Profile.vehiculeActif = instanceId;
            s.RaiseVehiclesChanged();
            return true;
        }

        public bool ToggleFavorite(string defId)
        {
            List<string> f = s.Profile.favoris;
            bool added;
            if (f.Contains(defId)) { f.Remove(defId); added = false; }
            else { f.Add(defId); added = true; }
            return added;
        }

        public bool IsFavorite(string defId) { return s.Profile.favoris.Contains(defId); }

        // ---------- Améliorations mécaniques ----------

        public long UpgradeCost(OwnedVehicle v, string upgradeId)
        {
            UpgradeDef u = s.Db.Upgrade(upgradeId);
            VehicleDef d = s.Db.Vehicle(v.defId);
            if (u == null || d == null) return 0;
            int next = v.GetUpgradeLevel(upgradeId) + 1;
            if (next > u.niveauMax) return 0;
            long basePrice = d.Kind == VehicleKind.Moto ? u.prixNiveau1Moto : u.prixNiveau1Tricycle;
            double mult = Math.Pow(Math.Max(1f, u.multiplicateurNiveau), next - 1);
            return Cdf.RoundTo(basePrice * mult, 1000);
        }

        public bool Upgrade(OwnedVehicle v, string upgradeId, out string error)
        {
            error = null;
            UpgradeDef u = s.Db.Upgrade(upgradeId);
            if (u == null || v == null) { error = "Amélioration inconnue."; return false; }
            int level = v.GetUpgradeLevel(upgradeId);
            if (level >= u.niveauMax) { error = "Niveau maximum atteint."; return false; }
            long cost = UpgradeCost(v, upgradeId);
            if (!s.Economy.Spend(cost, LedgerCategories.Amelioration, u.nom + " niveau " + (level + 1)))
            {
                error = "Il faut " + Cdf.Format(cost) + ".";
                return false;
            }
            v.SetUpgradeLevel(upgradeId, level + 1);
            s.Notify(u.nom + " : niveau " + (level + 1) + " installé.");
            s.RaiseVehiclesChanged();
            return true;
        }

        // ---------- Personnalisation esthétique ----------

        public bool ApplyCosmetic(OwnedVehicle v, string cosmeticId, string option, out string error)
        {
            error = null;
            CosmeticDef c = s.Db.Cosmetic(cosmeticId);
            if (c == null || v == null) { error = "Option inconnue."; return false; }
            if (v.GetCosmetic(c.type) == option || (v.accessoires != null && v.accessoires.Contains(option))) { error = "Déjà installé."; return false; }
            if (!s.Economy.Spend(c.prix, LedgerCategories.Personnalisation, c.nom + " : " + option))
            {
                error = "Il faut " + Cdf.Format(c.prix) + ".";
                return false;
            }
            v.SetCosmetic(c.type, option);
            s.Notify(c.nom + " : " + option);
            s.RaiseVehiclesChanged();
            return true;
        }

        // ---------- Comparaison ----------

        public List<string[]> Compare(VehicleDef a, VehicleDef b)
        {
            EconomyConfig eco = s.Db.Economy;
            VehicleStats sa = VehicleStatsCalculator.Compute(a, null, eco);
            VehicleStats sb = VehicleStatsCalculator.Compute(b, null, eco);
            List<string[]> rows = new List<string[]>();
            rows.Add(new[] { "Prix", Cdf.Format(Price(a)), Cdf.Format(Price(b)) });
            rows.Add(new[] { "Vitesse max", a.vitesseMaxKmh.ToString("0") + " km/h", b.vitesseMaxKmh.ToString("0") + " km/h" });
            rows.Add(new[] { "0-50 km/h", sa.Accel0To50s.ToString("0.0") + " s", sb.Accel0To50s.ToString("0.0") + " s" });
            rows.Add(new[] { "Puissance", a.puissanceKw.ToString("0.0") + " kW", b.puissanceKw.ToString("0.0") + " kW" });
            rows.Add(new[] { "Consommation", a.consommationL100.ToString("0.0") + " L/100", b.consommationL100.ToString("0.0") + " L/100" });
            rows.Add(new[] { "Réservoir", a.reservoirL.ToString("0") + " L", b.reservoirL.ToString("0") + " L" });
            rows.Add(new[] { "Passagers", a.passagers.ToString(), b.passagers.ToString() });
            rows.Add(new[] { "Charge utile", a.chargeUtileKg.ToString("0") + " kg", b.chargeUtileKg.ToString("0") + " kg" });
            rows.Add(new[] { "Freinage", a.freinage + "/100", b.freinage + "/100" });
            rows.Add(new[] { "Résistance", a.resistance + "/100", b.resistance + "/100" });
            rows.Add(new[] { "Confort", a.confort + "/100", b.confort + "/100" });
            return rows;
        }
    }

    public sealed class WorkshopQuote
    {
        public string ServiceId;
        public string Label;
        public long Cost;
        public bool Useful;
        public string Detail;
    }

    /// <summary>Ateliers mécaniques de quartier.</summary>
    public sealed class WorkshopSystem
    {
        private readonly GameSession s;
        public WorkshopSystem(GameSession session) { s = session; }

        public List<WorkshopQuote> Quotes(OwnedVehicle v)
        {
            List<WorkshopQuote> list = new List<WorkshopQuote>();
            foreach (WorkshopServiceDef w in s.Db.Economy.atelier) list.Add(Quote(v, w.id));
            return list;
        }

        public WorkshopQuote Quote(OwnedVehicle v, string serviceId)
        {
            WorkshopServiceDef w = s.Db.WorkshopService(serviceId);
            VehicleDef d = v != null ? s.Db.Vehicle(v.defId) : null;
            WorkshopQuote q = new WorkshopQuote { ServiceId = serviceId, Label = w != null ? w.nom : serviceId };
            if (w == null || d == null) return q;
            long basePrice = d.Kind == VehicleKind.Moto ? w.prixMoto : w.prixTricycle;
            if (w.composant == "vidange")
            {
                float ratio = MathUtil.Clamp01(v.kmDepuisVidange / Math.Max(1f, s.Db.Economy.intervalleVidangeKm));
                q.Cost = basePrice;
                q.Useful = ratio > 0.25f;
                q.Detail = "Dernière vidange il y a " + v.kmDepuisVidange.ToString("0") + " km";
            }
            else if (w.composant == "tous")
            {
                q.Cost = basePrice;
                q.Useful = true;
                q.Detail = "Contrôle général";
            }
            else
            {
                float missing = 100f - v.GetCondition(w.composant);
                long price = (long)(basePrice * (w.parRoue ? 1f : missing / 100f));
                if (w.parRoue) price = basePrice * (d.Kind == VehicleKind.Moto ? 2 : 3);
                q.Cost = Math.Max(1000, Cdf.RoundUpTo(price, 500));
                q.Useful = missing >= 3f;
                q.Detail = "État actuel : " + v.GetCondition(w.composant).ToString("0") + " %";
            }
            return q;
        }

        public bool Perform(OwnedVehicle v, string serviceId, out string message)
        {
            message = null;
            WorkshopServiceDef w = s.Db.WorkshopService(serviceId);
            if (w == null || v == null) { message = "Service inconnu."; return false; }
            WorkshopQuote q = Quote(v, serviceId);
            if (!q.Useful) { message = "Ce n'est pas nécessaire pour le moment."; return false; }
            string category = w.composant == "proprete" || w.composant == "vidange" ? LedgerCategories.Entretien : LedgerCategories.Reparation;
            if (!s.Economy.Spend(q.Cost, category, w.nom))
            {
                message = "Il faut " + Cdf.Format(q.Cost) + ".";
                return false;
            }
            if (w.composant == "vidange") v.kmDepuisVidange = 0f;
            else if (w.composant == "tous")
            {
                foreach (string c in OwnedVehicle.Components) v.SetCondition(c, Math.Max(v.GetCondition(c), 60f));
            }
            else v.SetCondition(w.composant, 100f);
            message = w.nom + " : terminé (" + Cdf.Format(q.Cost) + ").";
            s.Notify(message);
            s.RaiseVehiclesChanged();
            return true;
        }
    }
}
