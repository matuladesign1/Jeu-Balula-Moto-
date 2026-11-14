// BALULA MOTO — MATULA DESIGN
// Types de données du jeu (indépendants de Unity, sérialisables par JsonUtility).
// Règle : les fichiers JSON de Resources/BalulaData utilisent exactement ces noms de champs.
using System;
using System.Collections.Generic;

namespace BalulaMoto.Core
{
    public enum VehicleKind { Moto, Tricycle }

    public enum VehicleCategory
    {
        MotoClassique,
        MotoTaxiEconomique,
        MotoTaxiUnePersonne,
        MotoTaxiDeuxPersonnes,
        MotoPuissante,
        MotoProfessionnelle,
        Tricycle5Places,
        Tricycle8Places,
        TricycleMarchandises,
        TricycleGrandeCapacite
    }

    public enum SurfaceKind { Goudron, GoudronDegrade, Terre, NidsDePoule, Poussiere, Boue, Travaux, Caniveau, Ralentisseur }

    public enum WeatherKind { Soleil, Nuageux, Pluie, Orage, Brouillard, Poussiere }

    public enum DayPhase { Nuit, Matin, Midi, ApresMidi, Soir }

    public enum GameMode { Carriere, Libre }

    public enum DialogueLanguage { Francais, Lingala, Mixte }

    public static class CategoryInfo
    {
        public static readonly VehicleCategory[] All =
        {
            VehicleCategory.MotoClassique, VehicleCategory.MotoTaxiEconomique, VehicleCategory.MotoTaxiUnePersonne,
            VehicleCategory.MotoTaxiDeuxPersonnes, VehicleCategory.MotoPuissante, VehicleCategory.MotoProfessionnelle,
            VehicleCategory.Tricycle5Places, VehicleCategory.Tricycle8Places, VehicleCategory.TricycleMarchandises,
            VehicleCategory.TricycleGrandeCapacite
        };

        public static string Label(VehicleCategory c)
        {
            switch (c)
            {
                case VehicleCategory.MotoClassique: return "Motos classiques";
                case VehicleCategory.MotoTaxiEconomique: return "Motos taxi économiques";
                case VehicleCategory.MotoTaxiUnePersonne: return "Motos taxi 1 personne";
                case VehicleCategory.MotoTaxiDeuxPersonnes: return "Motos taxi 2 personnes";
                case VehicleCategory.MotoPuissante: return "Motos puissantes";
                case VehicleCategory.MotoProfessionnelle: return "Motos professionnelles";
                case VehicleCategory.Tricycle5Places: return "Tricycles 5 places + chauffeur";
                case VehicleCategory.Tricycle8Places: return "Tricycles 8 places + chauffeur";
                case VehicleCategory.TricycleMarchandises: return "Tricycles de marchandises";
                case VehicleCategory.TricycleGrandeCapacite: return "Tricycles grande capacité";
            }
            return c.ToString();
        }

        public static bool TryParse(string value, out VehicleCategory category)
        {
            category = VehicleCategory.MotoClassique;
            if (string.IsNullOrEmpty(value)) return false;
            return Enum.TryParse(value, true, out category);
        }
    }

    public static class SurfaceInfo
    {
        public static string Label(SurfaceKind s)
        {
            switch (s)
            {
                case SurfaceKind.Goudron: return "Route goudronnée";
                case SurfaceKind.GoudronDegrade: return "Route dégradée";
                case SurfaceKind.Terre: return "Route en terre";
                case SurfaceKind.NidsDePoule: return "Nids-de-poule";
                case SurfaceKind.Poussiere: return "Route poussiéreuse";
                case SurfaceKind.Boue: return "Route boueuse";
                case SurfaceKind.Travaux: return "Route en travaux";
                case SurfaceKind.Caniveau: return "Caniveau";
                case SurfaceKind.Ralentisseur: return "Ralentisseur";
            }
            return s.ToString();
        }

        /// <summary>Adhérence de base (1 = goudron sec).</summary>
        public static float Grip(SurfaceKind s, bool wet)
        {
            float g;
            switch (s)
            {
                case SurfaceKind.Goudron: g = 1.00f; break;
                case SurfaceKind.GoudronDegrade: g = 0.90f; break;
                case SurfaceKind.Terre: g = 0.74f; break;
                case SurfaceKind.NidsDePoule: g = 0.82f; break;
                case SurfaceKind.Poussiere: g = 0.68f; break;
                case SurfaceKind.Boue: g = 0.45f; break;
                case SurfaceKind.Travaux: g = 0.64f; break;
                case SurfaceKind.Caniveau: g = 0.55f; break;
                default: g = 1.0f; break;
            }
            if (wet)
            {
                bool earth = s == SurfaceKind.Terre || s == SurfaceKind.Poussiere || s == SurfaceKind.Travaux || s == SurfaceKind.Boue;
                g *= earth ? 0.70f : 0.78f;
            }
            return g;
        }

        /// <summary>Coefficient de résistance au roulement.</summary>
        public static float Rolling(SurfaceKind s)
        {
            switch (s)
            {
                case SurfaceKind.Goudron: return 0.015f;
                case SurfaceKind.GoudronDegrade: return 0.022f;
                case SurfaceKind.Terre: return 0.035f;
                case SurfaceKind.NidsDePoule: return 0.035f;
                case SurfaceKind.Poussiere: return 0.030f;
                case SurfaceKind.Boue: return 0.090f;
                case SurfaceKind.Travaux: return 0.075f;
                case SurfaceKind.Caniveau: return 0.250f;
                default: return 0.015f;
            }
        }

        /// <summary>Intensité des secousses (0..1).</summary>
        public static float Bumpiness(SurfaceKind s)
        {
            switch (s)
            {
                case SurfaceKind.GoudronDegrade: return 0.15f;
                case SurfaceKind.Terre: return 0.22f;
                case SurfaceKind.NidsDePoule: return 0.65f;
                case SurfaceKind.Poussiere: return 0.12f;
                case SurfaceKind.Boue: return 0.30f;
                case SurfaceKind.Travaux: return 0.62f;
                case SurfaceKind.Caniveau: return 0.85f;
                default: return 0f;
            }
        }

        /// <summary>Multiplicateur d'usure des pneus et suspensions.</summary>
        public static float Wear(SurfaceKind s)
        {
            switch (s)
            {
                case SurfaceKind.Goudron: return 1.0f;
                case SurfaceKind.GoudronDegrade: return 1.4f;
                case SurfaceKind.Terre: return 1.6f;
                case SurfaceKind.NidsDePoule: return 2.6f;
                case SurfaceKind.Poussiere: return 1.5f;
                case SurfaceKind.Boue: return 1.8f;
                case SurfaceKind.Travaux: return 2.4f;
                case SurfaceKind.Caniveau: return 4.0f;
                default: return 1.0f;
            }
        }

        public static bool IsDirty(SurfaceKind s)
        {
            return s == SurfaceKind.Terre || s == SurfaceKind.Poussiere || s == SurfaceKind.Boue || s == SurfaceKind.Travaux || s == SurfaceKind.Caniveau;
        }
    }

    // ------------------------------------------------------------------
    // Définitions chargées depuis les fichiers JSON
    // ------------------------------------------------------------------

    [Serializable]
    public class VehicleDef
    {
        public string id;
        public string nom;
        public string modele;
        public string inspireDe;
        public string categorie;
        public string type;
        public float prixUsd;
        public long prixCdf;
        public float vitesseMaxKmh;
        public float puissanceKw;
        public float consommationL100;
        public float reservoirL;
        public int freinage;
        public int resistance;
        public int confort;
        public int stabilite;
        public float masseKg;
        public int passagers;
        public int placesAvant;
        public int placesArriere;
        public float chargeUtileKg;
        public bool marcheArriere;
        public int rapports;
        public float longueurM;
        public float largeurM;
        public float hauteurM;
        public float empattementM;
        public float rayonRoueM;
        public float voieArriereM;
        public string couleurPrincipale;
        public string couleurSecondaire;
        public string style;
        public string prefabRessource;
        /// <summary>Modèle 3D fourni (Resources/BalulaMoto/Vehicules/…, voir ModelMotos.cs) ; vide = véhicule dessiné par le jeu.</summary>
        public string modele3d;
        public string referencePhoto;
        public string noteCapacite;
        public string statutDonnees;
        public bool masqueBoutique;
        public bool vehiculeDepart;

        public VehicleCategory Category { get; set; }
        public VehicleKind Kind { get; set; }
        public bool IsMoto { get { return Kind == VehicleKind.Moto; } }
    }

    [Serializable]
    public class LocationDef
    {
        public string id;
        public string nom;
        public string nomLocal;
        public string categorie;
        public string commune;
        public double lat;
        public double lon;
        public string statutCoordonnees;
        public string sourceCoordonnees;
        /// <summary>Emplacement indiqué par le créateur pour poser un modèle 3D (0 = non indiqué).</summary>
        public double latModele;
        public double lonModele;
        public string sourceModele;
        public string description;
        public List<string> references;
        public string acces;
        public bool arret;
        public int poids;
        public string rechercheOsm;

        public bool HasCoordinates
        {
            get { return statutCoordonnees != "manquante" && !(Math.Abs(lat) < 1e-9 && Math.Abs(lon) < 1e-9); }
        }
    }

    [Serializable]
    public class ProvinceDef
    {
        public string id;
        public string nom;
        public string chefLieu;
        public int ordre;
        public int niveauMin;
        public float reputationMin;
        public int missionsMin;
        public long coutCdf;
        public string missionRequise;
        public string architecture;
        public string routes;
        public string culture;
        public string commerce;
        public string vehiculesTypiques;
        public string statutContenu;
        public bool debloqueeAuDepart;
    }

    [Serializable]
    public class MissionDef
    {
        public string id;
        public string titre;
        public string description;
        public string objectif;
        public int cible;
        public string parametre;
        public long recompenseCdf;
        public int recompenseXp;
        public int dureeIndicativeMin;
        public int difficulte;
        public int niveauMin;
        public string prerequis;
        public string conditions;
        public string province;
        public bool repetable;
    }

    [Serializable]
    public class DialogueEntry
    {
        public string cle;
        public string quartier;
        public List<string> fr;
        public List<string> ln;
    }

    [Serializable]
    public class FareRules
    {
        public long baseMoto;
        public long parKmMoto;
        public long baseTricyclePassager;
        public long parKmTricyclePassager;
        public float majorationNuit;
        public float majorationPluie;
        public float majorationPointe;
        public float majorationMarchandises;
        public long minimum;
        public long arrondi;
        public float facteurDetour;
        public float vitesseMoyenneKmh;
    }

    [Serializable]
    public class FuelVendorDef
    {
        public string id;
        public string nom;
        public long prixLitre;
        public float risqueQualite;
        public float litresMin;
        public string description;
    }

    [Serializable]
    public class WorkshopServiceDef
    {
        public string id;
        public string nom;
        public string composant;
        public long prixMoto;
        public long prixTricycle;
        public bool parRoue;
        public string description;
    }

    [Serializable]
    public class UpgradeDef
    {
        public string id;
        public string nom;
        public string effet;
        public float bonusParNiveau;
        public long prixNiveau1Moto;
        public long prixNiveau1Tricycle;
        public float multiplicateurNiveau;
        public int niveauMax;
        public string description;
    }

    [Serializable]
    public class CosmeticDef
    {
        public string id;
        public string nom;
        public string type;
        public long prix;
        public List<string> options;
    }

    [Serializable]
    public class FineDef
    {
        public string id;
        public string libelle;
        public long montant;
    }

    [Serializable]
    public class DocumentsDef
    {
        public long prix;
        public int dureeJours;
        public string description;
    }

    [Serializable]
    public class EconomyConfig
    {
        public double tauxUsdCdf;
        public string sourceTaux;
        public float facteurPrixVehicules;
        public float facteurRemunerationMissions;
        public float facteurUsure;
        public float facteurConsommation;
        public float intervalleVidangeKm;
        public float minutesJeuParSeconde;
        public long argentDepartCarriere;
        public long argentDepartLibre;
        public float etatDepartCarriere;
        public float decoteVenteMin;
        public float decoteVenteMax;
        public FareRules tarifs;
        public List<FuelVendorDef> carburant;
        public List<WorkshopServiceDef> atelier;
        public List<UpgradeDef> ameliorations;
        public List<CosmeticDef> personnalisation;
        public List<FineDef> amendes;
        public DocumentsDef documents;
    }

    [Serializable]
    public class ClientType
    {
        public string id;
        public string libelle;
        public float facteurBudget;
        public float obstination;
        public int patienceMin;
        public int patienceMax;
        public float probaMarchandises;
        public float probaGroupe;
        public int poids;
        public bool motoAutorisee;
        public string tenue;
    }

    [Serializable]
    public class ClientsConfig
    {
        public List<string> prenomsHommes;
        public List<string> prenomsFemmes;
        public List<string> postnoms;
        public List<ClientType> types;
    }

    [Serializable]
    public class ReferenceEntry
    {
        public string id;
        public string fichier;
        public string categorie;
        public string sujet;
        public string statut;
        public string source;
        public List<string> elementsVisibles;
        public string usage;
        public string droits;
        public string lieuId;
        public string vehiculeId;
    }

    // Enveloppes de fichiers (JsonUtility exige un objet racine)
    [Serializable] public class VehiclesFile { public List<VehicleDef> vehicules; }
    [Serializable] public class LocationsFile { public string origineDescription; public double origineLat; public double origineLon; public List<LocationDef> lieux; }
    [Serializable] public class ProvincesFile { public List<ProvinceDef> provinces; }
    [Serializable] public class MissionsFile { public List<MissionDef> missions; }
    [Serializable] public class DialoguesFile { public List<DialogueEntry> repliques; }
    [Serializable] public class ReferencesFile { public List<ReferenceEntry> references; }

    /// <summary>Lieu du monde actif (piste d'essai ou carte importée), en mètres.</summary>
    public sealed class PlaceRef
    {
        public string Id;
        public string Name;
        public string District;
        public string Category;
        public double X;
        public double Y;
        public double Z;
        public int Weight = 1;
        public bool IsStop;
        /// <summary>Zone jouable (« kinshasa », « matadi »...) : les courses restent dans la même zone.</summary>
        public string Zone = "kinshasa";

        public double DistanceTo(PlaceRef other)
        {
            double dx = other.X - X, dz = other.Z - Z;
            return Math.Sqrt(dx * dx + dz * dz);
        }
    }
}
