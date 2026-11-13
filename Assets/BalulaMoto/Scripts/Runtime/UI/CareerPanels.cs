// BALULA MOTO — carrière : missions, finances et documents, provinces et voyages.
using System.Collections.Generic;
using System.Text;
using BalulaMoto.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace BalulaMoto.Runtime
{
    /// <summary>Carte de liste : texte riche à gauche, bouton à droite (facultatif).</summary>
    public static class ListCard
    {
        public static Button Make(Transform list, string richText, float height, string buttonLabel, Color buttonColor, bool enabled, UnityAction action)
        {
            Image card = UIKit.Box(list, "Carte", UIKit.PanelLight);
            UIKit.Size(card, height);
            Text t = UIKit.Label(card.transform, richText, 24, UIKit.Text, TextAnchor.MiddleLeft);
            UIKit.Stretch(t.rectTransform, 22, 8, buttonLabel != null ? 300 : 22, 8);
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = 16;
            t.resizeTextMaxSize = 24;
            if (buttonLabel == null) return null;
            Button b = UIKit.Button(card.transform, buttonLabel, buttonColor, buttonColor == UIKit.Accent ? Color.black : Color.white, 28, action);
            UIKit.Place((RectTransform)b.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(260f, Mathf.Min(96f, height - 20f)));
            b.interactable = enabled;
            return b;
        }

        public static Text Header(Transform list, string text)
        {
            Text h = UIKit.Label(list, text, 30, UIKit.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIKit.Size(h, 56f);
            return h;
        }
    }

    // =====================================================================
    // Missions
    // =====================================================================
    public class MissionsPanel : UIPanel
    {
        private RectTransform list;

        protected override void Build()
        {
            Frame("Missions", 1700, 980, true);
            RectTransform view = UIKit.ScrollList(content, out list);
            UIKit.Stretch(view, 0, 0, 0, 0);
        }

        public override void Refresh()
        {
            UIKit.Clear(list);
            GameSession s = BalulaGame.Instance.Session;
            if (s == null) return;
            MissionSystem ms = s.Missions;

            List<MissionProgress> active = ms.Active();
            ListCard.Header(list, "En cours (" + active.Count + "/" + MissionSystem.MaxActive + ")");
            if (active.Count == 0) UIKit.Card(list, UIKit.Colored("Aucune mission en cours. Choisissez-en une ci-dessous.", UIKit.TextDim), 70f, UIKit.Bg);
            foreach (MissionProgress p in active)
            {
                MissionDef m = s.Db.Mission(p.id);
                if (m == null) continue;
                string id = m.id;
                ListCard.Make(list, Describe(s, m, p, true), 170f, "Abandonner", UIKit.Red, true, () =>
                    ui.Ask("Abandonner la mission ?", m.titre + "\nLa progression sera perdue.", "Abandonner", () => { s.Missions.Abandon(id); Refresh(); }));
            }

            ListCard.Header(list, "Disponibles");
            int shown = 0;
            foreach (MissionDef m in s.Db.Missions)
            {
                string reason;
                MissionProgress p = ms.Progress(m.id);
                if (p != null && p.active) continue;
                bool available = ms.IsAvailable(m, out reason);
                if (!available && reason == "Terminée") continue;
                string mapIssue = MapIssue(m);
                bool ok = available && mapIssue == null && active.Count < MissionSystem.MaxActive;
                string text = Describe(s, m, p, false);
                if (!available) text += "\n" + UIKit.Colored("Verrouillée : " + reason, UIKit.Red);
                else if (mapIssue != null) text += "\n" + UIKit.Colored(mapIssue, new Color(1f, 0.6f, 0.2f));
                string id = m.id;
                ListCard.Make(list, text, 190f, "Accepter", UIKit.Accent, ok, () =>
                {
                    string error;
                    if (!s.Missions.Activate(id, out error)) ui.Toast(error);
                    Refresh();
                });
                shown++;
            }
            if (shown == 0) UIKit.Card(list, "Toutes les missions disponibles sont en cours ou terminées.", 70f, UIKit.Bg);

            int done = 0;
            foreach (MissionProgress p in s.Profile.missions) if (p.fois > 0) done++;
            ListCard.Header(list, "Réussies : " + done + " mission(s), " + s.Career.CompletedMissionCount() + " au total avec les contrats répétés");
        }

        private static string Describe(GameSession s, MissionDef m, MissionProgress p, bool active)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<b>").Append(m.titre).Append("</b>   ").Append(UIKit.Colored(MissionSystem.Difficulty(m.difficulte), UIKit.Accent));
            if (m.repetable) sb.Append("   ").Append(UIKit.Colored("(répétable)", UIKit.TextDim));
            sb.Append('\n').Append(m.description).Append('\n');
            sb.Append("Récompense : ").Append(Cdf.Format(Cdf.RoundTo(m.recompenseCdf * s.Db.Economy.facteurRemunerationMissions, 500))).Append(" + ").Append(m.recompenseXp).Append(" XP");
            if (m.dureeIndicativeMin > 0) sb.Append("   ·   ≈ ").Append(m.dureeIndicativeMin).Append(" min");
            if (active) sb.Append("   ·   Progression : ").Append(UIFormat.MissionProgressText(m, p));
            return sb.ToString();
        }

        /// <summary>Les missions vers un lieu précis exigent que ce lieu existe dans le monde chargé.</summary>
        private static string MapIssue(MissionDef m)
        {
            if (m.objectif != MissionObjectives.CourseVersLieu && m.objectif != MissionObjectives.VisiterLieu) return null;
            WorldContext w = BalulaGame.Instance.World;
            if (w == null || string.IsNullOrEmpty(m.parametre)) return null;
            string q = TextUtil.Normalize(m.parametre);
            foreach (PlaceRef p in w.Places)
            {
                if (p.Id == m.parametre) return null;
                if (q.Length > 2 && (TextUtil.Normalize(p.Name).Contains(q) || TextUtil.Normalize(p.District).Contains(q))) return null;
            }
            return w.IsTestTrack ? "Lieu absent de la piste d'essai : disponible avec la carte OpenStreetMap de Kinshasa." : "Lieu absent de la carte chargée.";
        }
    }

    // =====================================================================
    // Finances et documents
    // =====================================================================
    public class FinancesPanel : UIPanel
    {
        private RectTransform list;

        protected override void Build()
        {
            Frame("Finances et documents", 1600, 980, true);
            RectTransform view = UIKit.ScrollList(content, out list);
            UIKit.Stretch(view, 0, 0, 0, 0);
        }

        public override void Refresh()
        {
            UIKit.Clear(list);
            GameSession s = BalulaGame.Instance.Session;
            if (s == null) return;
            PlayerProfile p = s.Profile;
            EconomyConfig eco = s.Db.Economy;

            ListCard.Header(list, "Aujourd'hui (jour " + p.jour + ")");
            UIKit.Card(list, "Revenus " + UIKit.Colored(Cdf.Format(p.revenusDuJour), UIKit.Green) + "   ·   Dépenses " + UIKit.Colored(Cdf.Format(p.depensesDuJour), UIKit.Red) +
                             "   ·   Courses " + p.coursesDuJour + "   ·   Solde " + UIKit.Colored(Cdf.Format(p.argent), UIKit.Accent), 80f, UIKit.PanelLight);
            UIKit.Card(list, "Depuis le début : revenus " + Cdf.Format(p.totalRevenus) + " · dépenses " + Cdf.Format(p.totalDepenses) + " · " + p.nbCourses + " courses · " + p.nbLivraisons + " livraisons · " +
                             p.nbAccidents + " accidents · meilleure série sans accident : " + p.meilleureSerie + "\nRepère de change : 1 $ ≈ " + Cdf.Format((long)eco.tauxUsdCdf) + " (" + eco.sourceTaux + ")", 110f, UIKit.PanelLight);

            ListCard.Header(list, "Documents du véhicule (fictifs)");
            bool valid = s.Police.DocumentsValid;
            string docText = (valid ? UIKit.Colored("En règle", UIKit.Green) + " jusqu'au jour " + (p.documentsValidesJusquAuJour >= 99999 ? "∞ (mode libre)" : p.documentsValidesJusquAuJour.ToString())
                                    : UIKit.Colored("Expirés", UIKit.Red) + " — un contrôle de police coûtera une amende.") +
                             "\n" + eco.documents.description + " : " + Cdf.Format(eco.documents.prix) + " pour " + eco.documents.dureeJours + " jours.";
            ListCard.Make(list, docText, 120f, "Renouveler", UIKit.Accent, p.documentsValidesJusquAuJour < 99999, () =>
            {
                string error;
                if (!s.Police.RenewDocuments(out error)) ui.Toast(error);
                Refresh();
            });
            UIKit.Card(list, "Infractions relevées : " + p.infractions + " · amendes payées : " + Cdf.Format(p.amendesPayees) + (p.rechercheParPolice ? "\n" + UIKit.Colored("Vous êtes recherché pour refus d'obtempérer.", UIKit.Red) : ""), 80f, UIKit.PanelLight);

            ListCard.Header(list, "Sur les 7 derniers jours, par catégorie");
            Dictionary<string, long> totals = s.Economy.TotalsByCategory(7);
            if (totals.Count == 0) UIKit.Card(list, UIKit.Colored("Pas encore de mouvements.", UIKit.TextDim), 60f, UIKit.Bg);
            foreach (KeyValuePair<string, long> kv in totals)
                UIKit.Card(list, kv.Key + " : " + UIKit.Colored(Cdf.FormatSigned(kv.Value), kv.Value >= 0 ? UIKit.Green : UIKit.Red), 56f, UIKit.Bg);

            ListCard.Header(list, "Historique récent");
            int n = 0;
            for (int i = p.historique.Count - 1; i >= 0 && n < 40; i--, n++)
            {
                LedgerEntry e = p.historique[i];
                UIKit.Card(list, "J" + e.jour + " " + TextUtil.Clock(e.minute) + "  ·  " + e.categorie + "  ·  " + e.libelle + "   " +
                                 UIKit.Colored(Cdf.FormatSigned(e.montant), e.montant >= 0 ? UIKit.Green : UIKit.Red) + "   " + UIKit.Colored("solde " + Cdf.Format(e.solde), UIKit.TextDim), 56f, UIKit.Bg);
            }
        }
    }

    // =====================================================================
    // Provinces et voyages
    // =====================================================================
    public class ProvincesPanel : UIPanel
    {
        private RectTransform list;

        protected override void Build()
        {
            Frame("Provinces et voyages", 1700, 980, true);
            RectTransform view = UIKit.ScrollList(content, out list);
            UIKit.Stretch(view, 0, 0, 0, 0);
        }

        public override void Refresh()
        {
            UIKit.Clear(list);
            BalulaGame g = BalulaGame.Instance;
            GameSession s = g.Session;
            if (s == null) return;
            string zone = g.CurrentZone();

            ListCard.Header(list, "Voyages disponibles");
            ListCard.Make(list, "<b>Kinshasa</b> — " + (g.World.IsTestTrack ? "piste d'essai (la carte réelle se génère par import OpenStreetMap)" : g.World.Name) +
                                (zone == "kinshasa" ? "\n" + UIKit.Colored("Vous êtes ici.", UIKit.Green) : ""),
                140f, "Y aller", UIKit.Accent, zone != "kinshasa", () => { ui.Close(this); g.TravelTo("kinshasa"); });
            if (g.World.HasMatadi)
            {
                bool unlocked = s.Career.IsUnlocked("kongo-central");
                ListCard.Make(list, "<b>Matadi (Kongo Central)</b> — maquette jouable du Pont Maréchal, pont suspendu sur le fleuve Congo (722 m, travée de 520 m).\n" +
                                    (unlocked ? (zone == MatadiBuilder.Zone ? UIKit.Colored("Vous êtes ici.", UIKit.Green) : "Voyage par la route : plusieurs heures de jeu passent.")
                                              : UIKit.Colored("Débloquez d'abord la province du Kongo Central.", UIKit.Red)),
                    150f, "Y aller", UIKit.Accent, unlocked && zone != MatadiBuilder.Zone, () => { ui.Close(this); g.TravelTo(MatadiBuilder.Zone); });
            }

            ListCard.Header(list, "Les 26 provinces");
            List<ProvinceDef> provs = new List<ProvinceDef>(s.Db.Provinces);
            provs.Sort((a, b) => a.ordre.CompareTo(b.ordre));
            foreach (ProvinceDef p in provs)
            {
                bool unlocked = s.Career.IsUnlocked(p.id);
                List<string> missing = s.Career.MissingRequirements(p);
                StringBuilder sb = new StringBuilder();
                sb.Append("<b>").Append(p.nom).Append("</b> — chef-lieu : ").Append(p.chefLieu);
                sb.Append(unlocked ? "   " + UIKit.Colored("Débloquée", UIKit.Green) : "   " + UIKit.Colored(Cdf.Format(p.coutCdf), UIKit.Accent));
                sb.Append('\n').Append(UIKit.Colored(p.routes + " · " + p.vehiculesTypiques, UIKit.TextDim));
                sb.Append('\n').Append(UIKit.Colored(p.statutContenu, UIKit.TextDim));
                if (!unlocked && missing.Count > 0) sb.Append('\n').Append(UIKit.Colored(string.Join(" · ", missing.ToArray()), new Color(1f, 0.55f, 0.45f)));
                string id = p.id;
                ListCard.Make(list, sb.ToString(), unlocked || missing.Count == 0 ? 150f : 190f, unlocked ? "Ouverte" : "Débloquer", unlocked ? UIKit.PanelLight : UIKit.Accent, !unlocked && missing.Count == 0, () =>
                {
                    string error;
                    if (!s.Career.Unlock(id, out error)) ui.Toast(error);
                    Refresh();
                });
            }
        }
    }
}
