// BALULA MOTO — missions du mode carrière (objectifs, progression, récompenses).
using System;
using System.Collections.Generic;

namespace BalulaMoto.Core
{
    public static class MissionObjectives
    {
        public const string Courses = "COURSES";
        public const string CourseVersLieu = "COURSE_VERS_LIEU";
        public const string Livraisons = "LIVRAISONS";
        public const string RevenusJour = "REVENUS_JOUR";
        public const string CoursesPointe = "COURSES_POINTE";
        public const string SerieSansAccident = "SERIE_SANS_ACCIDENT";
        public const string Solde = "SOLDE";
        public const string Niveau = "NIVEAU";
        public const string VisiterLieu = "VISITER_LIEU";
        public const string NoteMoyenne = "NOTE_MOYENNE";
        public const string PossederCategorie = "POSSEDER_CATEGORIE";
        public const string SuivreVehicule = "SUIVRE_VEHICULE";
        public const string DebloquerProvince = "DEBLOQUER_PROVINCE";
        public const string CoursesGroupe = "COURSES_GROUPE";
        public const string CoursesCinqEtoiles = "COURSES_5_ETOILES";
        public const string CoursesNuit = "COURSES_NUIT";
        public const string CoursesPluie = "COURSES_PLUIE";

        private static readonly string[] all =
        {
            Courses, CourseVersLieu, Livraisons, RevenusJour, CoursesPointe, SerieSansAccident, Solde, Niveau,
            VisiterLieu, NoteMoyenne, PossederCategorie, SuivreVehicule, DebloquerProvince, CoursesGroupe,
            CoursesCinqEtoiles, CoursesNuit, CoursesPluie
        };

        public static bool IsKnown(string objective) { return Array.IndexOf(all, objective) >= 0; }
    }

    public sealed class MissionSystem
    {
        public const int MaxActive = 3;
        private readonly GameSession s;
        public MissionSystem(GameSession session) { s = session; }

        // ---------------- Consultation ----------------

        public MissionProgress Progress(string id) { return s.Profile.FindMission(id); }

        public bool IsCompleted(string id)
        {
            MissionProgress p = Progress(id);
            return p != null && (p.terminee || p.fois > 0);
        }

        public List<MissionProgress> Active()
        {
            List<MissionProgress> list = new List<MissionProgress>();
            foreach (MissionProgress p in s.Profile.missions) if (p.active && !p.terminee) list.Add(p);
            return list;
        }

        public bool IsAvailable(MissionDef m, out string reason)
        {
            reason = null;
            MissionProgress p = Progress(m.id);
            if (p != null && p.active) { reason = "En cours"; return false; }
            if (p != null && p.terminee && !m.repetable) { reason = "Terminée"; return false; }
            if (s.Profile.niveau < m.niveauMin) { reason = "Niveau " + m.niveauMin + " requis"; return false; }
            if (!string.IsNullOrEmpty(m.prerequis) && !IsCompleted(m.prerequis))
            {
                MissionDef pre = s.Db.Mission(m.prerequis);
                reason = "Terminer d'abord : " + (pre != null ? pre.titre : m.prerequis);
                return false;
            }
            string province = string.IsNullOrEmpty(m.province) ? "kinshasa" : m.province;
            if (!s.Career.IsUnlocked(province)) { reason = "Province verrouillée"; return false; }
            return true;
        }

        public List<MissionDef> Available()
        {
            List<MissionDef> list = new List<MissionDef>();
            string reason;
            foreach (MissionDef m in s.Db.Missions) if (IsAvailable(m, out reason)) list.Add(m);
            return list;
        }

        public bool Activate(string id, out string error)
        {
            error = null;
            MissionDef m = s.Db.Mission(id);
            if (m == null) { error = "Mission inconnue."; return false; }
            if (!IsAvailable(m, out error)) return false;
            if (Active().Count >= MaxActive) { error = "Maximum " + MaxActive + " missions en même temps."; return false; }
            MissionProgress p = Progress(id);
            if (p == null)
            {
                p = new MissionProgress { id = id };
                s.Profile.missions.Add(p);
            }
            p.active = true;
            p.terminee = false;
            p.progression = 0;
            p.jourDebut = s.Profile.jour;
            s.Notify("Mission acceptée : " + m.titre);
            RefreshStateObjectives();
            return true;
        }

        public void Abandon(string id)
        {
            MissionProgress p = Progress(id);
            if (p != null) { p.active = false; p.progression = 0; }
        }

        public float Progress01(MissionDef m)
        {
            MissionProgress p = Progress(m.id);
            if (p == null) return 0f;
            if (p.terminee) return 1f;
            return MathUtil.Clamp01(p.progression / (float)Math.Max(1, m.cible));
        }

        public static string Difficulty(int d)
        {
            d = Math.Max(1, Math.Min(5, d));
            return new string('*', d) + new string('·', 5 - d);
        }

        // ---------------- Événements du jeu ----------------

        public void OnRideCompleted(ClientRequest c, RideResult r, bool rushHour)
        {
            foreach (MissionProgress p in Active())
            {
                MissionDef m = s.Db.Mission(p.id);
                if (m == null) continue;
                switch (m.objectif)
                {
                    case MissionObjectives.Courses: Add(p, m, 1); break;
                    case MissionObjectives.Livraisons: if (c.IsDelivery || c.CargoKg > 0f) Add(p, m, 1); break;
                    case MissionObjectives.CoursesPointe: if (rushHour) Add(p, m, 1); break;
                    case MissionObjectives.CoursesGroupe: if (c.Passengers >= 2) Add(p, m, 1); break;
                    case MissionObjectives.CoursesCinqEtoiles: if (r.Stars >= 5) Add(p, m, 1); break;
                    case MissionObjectives.CoursesNuit: if (c.Night) Add(p, m, 1); break;
                    case MissionObjectives.CoursesPluie: if (c.Rain) Add(p, m, 1); break;
                    case MissionObjectives.CourseVersLieu:
                        if (MatchesPlace(c.Destination, m.parametre)) Add(p, m, 1);
                        break;
                }
            }
            RefreshStateObjectives();
        }

        public void OnMoneyChanged() { RefreshStateObjectives(); }
        public void OnLevelChanged() { RefreshStateObjectives(); }
        public void OnVehicleOwned() { RefreshStateObjectives(); }
        public void OnProvinceUnlocked(string provinceId) { RefreshStateObjectives(); }

        public void OnLocationVisited(string placeId)
        {
            foreach (MissionProgress p in Active())
            {
                MissionDef m = s.Db.Mission(p.id);
                if (m != null && m.objectif == MissionObjectives.VisiterLieu && placeId == m.parametre) Set(p, m, m.cible);
            }
        }

        /// <summary>Lieu atteint pendant la conduite (pas forcément en course).</summary>
        public void OnPlaceReached(PlaceRef place)
        {
            if (place == null) return;
            if (!s.Profile.lieuxVisites.Contains(place.Id)) s.Profile.lieuxVisites.Add(place.Id);
            foreach (MissionProgress p in Active())
            {
                MissionDef m = s.Db.Mission(p.id);
                if (m != null && m.objectif == MissionObjectives.VisiterLieu && MatchesPlace(place, m.parametre)) Set(p, m, m.cible);
            }
        }

        public void OnFollowProgress(int secondsFollowed)
        {
            foreach (MissionProgress p in Active())
            {
                MissionDef m = s.Db.Mission(p.id);
                if (m != null && m.objectif == MissionObjectives.SuivreVehicule) Set(p, m, secondsFollowed);
            }
        }

        public void OnFollowFailed()
        {
            foreach (MissionProgress p in Active())
            {
                MissionDef m = s.Db.Mission(p.id);
                if (m != null && m.objectif == MissionObjectives.SuivreVehicule && !p.terminee)
                {
                    p.progression = 0;
                    s.Notify("Poursuite ratée : vous avez perdu le véhicule de vue. Réessayez.");
                }
            }
        }

        public MissionDef ActiveFollowMission()
        {
            foreach (MissionProgress p in Active())
            {
                MissionDef m = s.Db.Mission(p.id);
                if (m != null && m.objectif == MissionObjectives.SuivreVehicule) return m;
            }
            return null;
        }

        /// <summary>Objectifs qui dépendent d'un état (solde, niveau, revenus du jour...).</summary>
        public void RefreshStateObjectives()
        {
            PlayerProfile pr = s.Profile;
            foreach (MissionProgress p in Active())
            {
                MissionDef m = s.Db.Mission(p.id);
                if (m == null) continue;
                switch (m.objectif)
                {
                    case MissionObjectives.Solde: Set(p, m, (int)Math.Min(int.MaxValue, pr.argent)); break;
                    case MissionObjectives.RevenusJour: Set(p, m, (int)Math.Min(int.MaxValue, pr.revenusDuJour)); break;
                    case MissionObjectives.Niveau: Set(p, m, pr.niveau); break;
                    case MissionObjectives.SerieSansAccident: Set(p, m, pr.serieSansAccident); break;
                    case MissionObjectives.NoteMoyenne:
                        if (pr.notes.Count >= 10) Set(p, m, (int)Math.Round(pr.Reputation() * 10f));
                        break;
                    case MissionObjectives.PossederCategorie:
                        VehicleCategory cat;
                        if (CategoryInfo.TryParse(m.parametre, out cat) && s.Garage.OwnsCategory(cat)) Set(p, m, m.cible);
                        break;
                    case MissionObjectives.DebloquerProvince:
                        if (s.Career.IsUnlocked(m.parametre)) Set(p, m, m.cible);
                        break;
                }
            }
        }

        // ---------------- Interne ----------------

        private static bool MatchesPlace(PlaceRef place, string parameter)
        {
            if (place == null || string.IsNullOrEmpty(parameter)) return false;
            if (place.Id == parameter) return true;
            string n = TextUtil.Normalize(place.Name);
            string q = TextUtil.Normalize(parameter);
            return q.Length > 2 && (n.Contains(q) || TextUtil.Normalize(place.District).Contains(q));
        }

        private void Add(MissionProgress p, MissionDef m, int amount) { Set(p, m, p.progression + amount); }

        private void Set(MissionProgress p, MissionDef m, int value)
        {
            if (p.terminee || !p.active) return;
            p.progression = Math.Max(0, value);
            if (p.progression >= m.cible) Complete(p, m);
        }

        private void Complete(MissionProgress p, MissionDef m)
        {
            p.terminee = true;
            p.active = false;
            p.fois++;
            long reward = Cdf.RoundTo(m.recompenseCdf * s.Db.Economy.facteurRemunerationMissions, 500);
            s.Notify("Mission réussie : " + m.titre + " (+" + Cdf.Format(reward) + ", +" + m.recompenseXp + " XP)");
            s.RaiseMissionCompleted(m);
            if (m.repetable) { p.terminee = false; p.progression = 0; }
            if (reward > 0) s.Economy.Earn(reward, LedgerCategories.Mission, m.titre);
            s.Career.AddXp(m.recompenseXp);
        }
    }
}
