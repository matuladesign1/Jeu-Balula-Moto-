// BALULA MOTO — base de données du jeu (chargée depuis les JSON, extensible par paquets de contenu).
using System;
using System.Collections.Generic;

namespace BalulaMoto.Core
{
    public sealed class GameDatabase
    {
        public readonly List<VehicleDef> Vehicles = new List<VehicleDef>();
        public readonly List<LocationDef> Locations = new List<LocationDef>();
        public readonly List<ProvinceDef> Provinces = new List<ProvinceDef>();
        public readonly List<MissionDef> Missions = new List<MissionDef>();
        public readonly List<DialogueEntry> Dialogues = new List<DialogueEntry>();
        public readonly List<ReferenceEntry> References = new List<ReferenceEntry>();
        public EconomyConfig Economy;
        public ClientsConfig Clients;
        public double OriginLat = -4.32194;
        public double OriginLon = 15.31194;
        public string OriginDescription = "";

        // --- Ajout / fusion (un paquet de contenu remplace les éléments de même id) ---

        public void AddVehicles(VehiclesFile file)
        {
            if (file == null || file.vehicules == null) return;
            foreach (VehicleDef v in file.vehicules)
            {
                if (v == null || string.IsNullOrEmpty(v.id)) continue;
                int i = Vehicles.FindIndex(x => x.id == v.id);
                if (i >= 0) Vehicles[i] = v; else Vehicles.Add(v);
            }
        }

        public void AddLocations(LocationsFile file)
        {
            if (file == null || file.lieux == null) return;
            if (Math.Abs(file.origineLat) > 1e-9 || Math.Abs(file.origineLon) > 1e-9)
            {
                OriginLat = file.origineLat;
                OriginLon = file.origineLon;
                OriginDescription = file.origineDescription ?? "";
            }
            foreach (LocationDef l in file.lieux)
            {
                if (l == null || string.IsNullOrEmpty(l.id)) continue;
                int i = Locations.FindIndex(x => x.id == l.id);
                if (i >= 0) Locations[i] = l; else Locations.Add(l);
            }
        }

        public void AddProvinces(ProvincesFile file)
        {
            if (file == null || file.provinces == null) return;
            foreach (ProvinceDef p in file.provinces)
            {
                if (p == null || string.IsNullOrEmpty(p.id)) continue;
                int i = Provinces.FindIndex(x => x.id == p.id);
                if (i >= 0) Provinces[i] = p; else Provinces.Add(p);
            }
        }

        public void AddMissions(MissionsFile file)
        {
            if (file == null || file.missions == null) return;
            foreach (MissionDef m in file.missions)
            {
                if (m == null || string.IsNullOrEmpty(m.id)) continue;
                int i = Missions.FindIndex(x => x.id == m.id);
                if (i >= 0) Missions[i] = m; else Missions.Add(m);
            }
        }

        public void AddDialogues(DialoguesFile file)
        {
            if (file == null || file.repliques == null) return;
            foreach (DialogueEntry d in file.repliques)
                if (d != null && !string.IsNullOrEmpty(d.cle)) Dialogues.Add(d);
        }

        public void AddReferences(ReferencesFile file)
        {
            if (file == null || file.references == null) return;
            foreach (ReferenceEntry r in file.references)
            {
                if (r == null || string.IsNullOrEmpty(r.id)) continue;
                // Une référence existante n'est jamais remplacée automatiquement (règle du cahier des charges).
                if (References.FindIndex(x => x.id == r.id) < 0) References.Add(r);
            }
        }

        /// <summary>À appeler après chargement : convertit les catégories, complète les valeurs manquantes.</summary>
        public void FinalizeData()
        {
            foreach (VehicleDef v in Vehicles)
            {
                VehicleCategory cat;
                if (!CategoryInfo.TryParse(v.categorie, out cat)) cat = VehicleCategory.MotoClassique;
                v.Category = cat;
                v.Kind = string.Equals(v.type, "Tricycle", StringComparison.OrdinalIgnoreCase) ? VehicleKind.Tricycle : VehicleKind.Moto;
                if (v.rapports <= 0) v.rapports = v.Kind == VehicleKind.Moto ? 5 : 4;
                if (v.rayonRoueM <= 0f) v.rayonRoueM = v.Kind == VehicleKind.Moto ? 0.30f : 0.21f;
                if (v.voieArriereM <= 0f) v.voieArriereM = 1.15f;
                if (v.masseKg <= 0f) v.masseKg = v.Kind == VehicleKind.Moto ? 120f : 360f;
                if (v.reservoirL <= 0f) v.reservoirL = 10f;
                if (v.vitesseMaxKmh <= 0f) v.vitesseMaxKmh = 80f;
                if (v.puissanceKw <= 0f) v.puissanceKw = 8f;
                if (v.nom == null) v.nom = v.id;
            }
            foreach (LocationDef l in Locations)
            {
                if (l.references == null) l.references = new List<string>();
                if (l.poids <= 0) l.poids = 1;
                if (string.IsNullOrEmpty(l.statutCoordonnees)) l.statutCoordonnees = "a_verifier";
            }
            foreach (DialogueEntry d in Dialogues)
            {
                if (d.fr == null) d.fr = new List<string>();
                if (d.ln == null) d.ln = new List<string>();
                if (d.quartier == null) d.quartier = "";
            }
            Provinces.Sort((a, b) => a.ordre.CompareTo(b.ordre));
            if (Economy == null) Economy = new EconomyConfig();
            if (Economy.tarifs == null) Economy.tarifs = new FareRules { baseMoto = 500, parKmMoto = 450, baseTricyclePassager = 300, parKmTricyclePassager = 200, minimum = 1000, arrondi = 500, facteurDetour = 1.3f, vitesseMoyenneKmh = 22f };
            if (Economy.carburant == null) Economy.carburant = new List<FuelVendorDef>();
            if (Economy.atelier == null) Economy.atelier = new List<WorkshopServiceDef>();
            if (Economy.ameliorations == null) Economy.ameliorations = new List<UpgradeDef>();
            if (Economy.personnalisation == null) Economy.personnalisation = new List<CosmeticDef>();
            if (Economy.amendes == null) Economy.amendes = new List<FineDef>();
            if (Economy.documents == null) Economy.documents = new DocumentsDef { prix = 30000, dureeJours = 30, description = "Permis, assurance et vignette" };
            if (Economy.tauxUsdCdf <= 0) Economy.tauxUsdCdf = 2265;
            if (Economy.facteurPrixVehicules <= 0f) Economy.facteurPrixVehicules = 1f;
            if (Economy.facteurRemunerationMissions <= 0f) Economy.facteurRemunerationMissions = 1f;
            if (Economy.facteurUsure <= 0f) Economy.facteurUsure = 1f;
            if (Economy.facteurConsommation <= 0f) Economy.facteurConsommation = 1f;
            if (Economy.intervalleVidangeKm <= 0f) Economy.intervalleVidangeKm = 250f;
            if (Economy.minutesJeuParSeconde <= 0f) Economy.minutesJeuParSeconde = 1f;
            if (Clients == null) Clients = new ClientsConfig();
            if (Clients.prenomsHommes == null) Clients.prenomsHommes = new List<string> { "Jean" };
            if (Clients.prenomsFemmes == null) Clients.prenomsFemmes = new List<string> { "Marie" };
            if (Clients.postnoms == null) Clients.postnoms = new List<string>();
            if (Clients.types == null) Clients.types = new List<ClientType>();
        }

        // --- Accès ---

        public VehicleDef Vehicle(string id) { return Vehicles.Find(v => v.id == id); }
        public LocationDef Location(string id) { return Locations.Find(l => l.id == id); }
        public ProvinceDef Province(string id) { return Provinces.Find(p => p.id == id); }
        public MissionDef Mission(string id) { return Missions.Find(m => m.id == id); }
        public FuelVendorDef FuelVendor(string id) { return Economy.carburant.Find(f => f.id == id); }
        public WorkshopServiceDef WorkshopService(string id) { return Economy.atelier.Find(w => w.id == id); }
        public UpgradeDef Upgrade(string id) { return Economy.ameliorations.Find(u => u.id == id); }
        public CosmeticDef Cosmetic(string id) { return Economy.personnalisation.Find(c => c.id == id); }
        public FineDef Fine(string id) { return Economy.amendes.Find(f => f.id == id); }

        public List<VehicleDef> VehiclesInCategory(VehicleCategory category)
        {
            List<VehicleDef> list = Vehicles.FindAll(v => v.Category == category && !v.masqueBoutique);
            list.Sort((a, b) => a.prixUsd.CompareTo(b.prixUsd));
            return list;
        }

        public List<VehicleDef> StartingVehicles()
        {
            return Vehicles.FindAll(v => v.vehiculeDepart);
        }

        /// <summary>Contrôles de cohérence. Retourne la liste des problèmes (vide si tout va bien).</summary>
        public List<string> Validate()
        {
            List<string> issues = new List<string>();
            HashSet<string> ids = new HashSet<string>();
            foreach (VehicleDef v in Vehicles)
            {
                VehicleCategory c;
                if (!CategoryInfo.TryParse(v.categorie, out c)) issues.Add("Véhicule " + v.id + " : catégorie inconnue '" + v.categorie + "'");
                if (v.type != "Moto" && v.type != "Tricycle") issues.Add("Véhicule " + v.id + " : type doit être Moto ou Tricycle");
                if (v.prixUsd <= 0 && v.prixCdf <= 0) issues.Add("Véhicule " + v.id + " : prix manquant");
                if (v.passagers != v.placesAvant + v.placesArriere) issues.Add("Véhicule " + v.id + " : passagers (" + v.passagers + ") ≠ placesAvant + placesArriere");
                if (string.Equals(v.type, "Moto") && v.passagers > 2) issues.Add("Véhicule " + v.id + " : une moto ne peut pas transporter plus de 2 passagers");
            }
            if (StartingVehicles().Count == 0) issues.Add("Aucun véhicule de départ (vehiculeDepart = true)");
            ids.Clear();
            foreach (LocationDef l in Locations)
                if (!ids.Add(l.id)) issues.Add("Lieu en double : " + l.id);
            foreach (MissionDef m in Missions)
            {
                if (!MissionObjectives.IsKnown(m.objectif)) issues.Add("Mission " + m.id + " : objectif inconnu '" + m.objectif + "'");
                if (!string.IsNullOrEmpty(m.prerequis) && Mission(m.prerequis) == null) issues.Add("Mission " + m.id + " : prérequis inconnu " + m.prerequis);
                if (!string.IsNullOrEmpty(m.province) && Province(m.province) == null) issues.Add("Mission " + m.id + " : province inconnue " + m.province);
            }
            foreach (ProvinceDef p in Provinces)
                if (!string.IsNullOrEmpty(p.missionRequise) && Mission(p.missionRequise) == null) issues.Add("Province " + p.id + " : mission requise inconnue " + p.missionRequise);
            if (Provinces.Count != 26) issues.Add("La RDC compte 26 provinces ; " + Provinces.Count + " définies.");
            foreach (UpgradeDef u in Economy.ameliorations)
                if (Array.IndexOf(OwnedVehicle.Upgrades, u.id) < 0) issues.Add("Amélioration inconnue : " + u.id);
            foreach (WorkshopServiceDef w in Economy.atelier)
                if (w.composant != "tous" && w.composant != "vidange" && Array.IndexOf(OwnedVehicle.Components, w.composant) < 0) issues.Add("Service d'atelier " + w.id + " : composant inconnu " + w.composant);
            return issues;
        }
    }
}
