// BALULA MOTO — panneaux de jeu : négociation, fin de course, carburant, atelier, panne sèche, contrôle de police.
using System.Collections.Generic;
using System.Text;
using BalulaMoto.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace BalulaMoto.Runtime
{
    // =====================================================================
    // Négociation du prix (« Combien jusqu'à... ? »)
    // =====================================================================
    public class NegotiationPanel : UIPanel
    {
        private Text clientText, logText, priceText, statusText;
        private RectTransform suggestions;
        private Button proposeButton, acceptButton, refuseButton, doneButton;
        private readonly List<string> log = new List<string>();
        private long price, step = 500, minimum = 1000;
        private bool finished;

        private static GameplayDirector G { get { return GameplayDirector.Instance; } }

        protected override void Build()
        {
            Frame("Négociation", 1560, 960, false);
            clientText = UIKit.Label(content, "", 26, UIKit.Text, TextAnchor.UpperLeft);
            UIKit.TopLeft(clientText.rectTransform, 0, 0, 1512, 150);

            Image logBox = UIKit.Box(content, "Conversation", UIKit.Bg);
            UIKit.TopLeft(logBox.rectTransform, 0, 160, 1512, 280);
            logText = UIKit.Label(logBox.transform, "", 28, UIKit.Text, TextAnchor.LowerLeft);
            UIKit.Stretch(logText.rectTransform, 20, 10, 20, 10);

            RectTransform priceRow = UIKit.Rect(content, "Prix");
            UIKit.TopLeft(priceRow, 0, 456, 1512, 110);
            HorizontalLayoutGroup h = priceRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 16f;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandHeight = true;
            h.childForceExpandWidth = false;
            UIKit.Size(UIKit.Button(priceRow, "− " + step, UIKit.PanelLight, Color.white, 34, () => Change(-step)), 110f, 240f);
            Image pb = UIKit.Box(priceRow, "PrixAffiche", UIKit.Bg);
            UIKit.Size(pb, 110f, 520f);
            priceText = UIKit.Label(pb.transform, "", 52, UIKit.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Stretch(priceText.rectTransform, 6, 4, 6, 4);
            UIKit.Size(UIKit.Button(priceRow, "+ " + step, UIKit.PanelLight, Color.white, 34, () => Change(step)), 110f, 240f);
            statusText = UIKit.Label(priceRow, "", 22, UIKit.TextDim, TextAnchor.MiddleLeft);
            UIKit.Size(statusText, 110f, 460f);

            suggestions = UIKit.Rect(content, "Suggestions");
            UIKit.TopLeft(suggestions, 0, 582, 1512, 90);
            HorizontalLayoutGroup hs = suggestions.gameObject.AddComponent<HorizontalLayoutGroup>();
            hs.spacing = 14f;
            hs.childControlWidth = true;
            hs.childControlHeight = true;
            hs.childForceExpandWidth = true;
            hs.childForceExpandHeight = true;

            RectTransform actions = UIKit.Rect(content, "Actions");
            UIKit.TopLeft(actions, 0, 696, 1512, 120);
            HorizontalLayoutGroup ha = actions.gameObject.AddComponent<HorizontalLayoutGroup>();
            ha.spacing = 16f;
            ha.childControlWidth = true;
            ha.childControlHeight = true;
            ha.childForceExpandWidth = true;
            ha.childForceExpandHeight = true;
            proposeButton = UIKit.Button(actions, "Proposer", UIKit.Accent, Color.black, 34, Propose);
            acceptButton = UIKit.Button(actions, "Accepter son prix", UIKit.Green, Color.white, 30, AcceptCounter);
            refuseButton = UIKit.Button(actions, "Refuser la course", UIKit.Red, Color.white, 30, Refuse);
            doneButton = UIKit.Button(actions, "Démarrer la course", UIKit.Green, Color.white, 34, () => ui.Close(this));
        }

        public override void Refresh()
        {
            GameplayDirector g = G;
            if (g == null || g.Client == null || g.Nego == null) { ui.Close(this); return; }
            FareRules rules = g.Session.Db.Economy.tarifs;
            step = System.Math.Max(100, rules.arrondi);
            minimum = rules.minimum;
            finished = false;
            log.Clear();
            ClientRequest c = g.Client;
            string lang = g.ClientLanguage() == DialogueLanguage.Lingala ? "parle lingala" : "parle français";
            clientText.text = "<b>" + c.Name + "</b> — " + c.TypeLabel + "  " + UIKit.Colored("(" + lang + ")", UIKit.TextDim) + "\n" +
                              "Destination : <b>" + c.Destination.Name + "</b> (" + c.Destination.District + ") · ≈ " + c.DistanceKm.ToString("0.0").Replace('.', ',') + " km" +
                              (c.Passengers > 1 ? " · " + c.Passengers + " passagers" : "") + (c.CargoKg > 0f ? " · " + c.CargoKg.ToString("0") + " kg de marchandises" : "") + "\n" +
                              UIKit.Colored("Tarif habituel pour ce trajet : ≈ " + Cdf.Format(c.ReferenceFare) + (c.Night ? " · nuit" : "") + (c.Rain ? " · pluie" : "") + (c.RushHour ? " · heure de pointe" : ""), UIKit.TextDim);
            Log(c.Name, g.OpeningLine);
            long[] offers = g.Nego.SuggestedOffers();
            price = offers.Length > 1 ? offers[1] : c.ReferenceFare;
            UIKit.Clear(suggestions);
            foreach (long o in offers)
            {
                long v = o;
                UIKit.Button(suggestions, Cdf.Format(v), UIKit.PanelLight, Color.white, 28, () => { price = v; UpdateView(); });
            }
            UpdateView();
        }

        private void Change(long delta)
        {
            price = System.Math.Max(minimum, price + delta);
            UpdateView();
        }

        private void Log(string speaker, string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            log.Add(UIKit.Colored(speaker + " :", speaker == "Vous" ? UIKit.Accent : new Color(0.55f, 0.85f, 1f)) + " " + line);
            while (log.Count > 6) log.RemoveAt(0);
            logText.text = string.Join("\n", log.ToArray());
        }

        private void UpdateView()
        {
            GameplayDirector g = G;
            priceText.text = Cdf.Format(price);
            Negotiation n = g != null ? g.Nego : null;
            bool open = !finished && n != null && !n.Finished;
            proposeButton.gameObject.SetActive(open);
            refuseButton.gameObject.SetActive(open);
            acceptButton.gameObject.SetActive(open && n.ClientOffer > 0);
            if (n != null && n.ClientOffer > 0) UIKit.SetButtonText(acceptButton, "Accepter " + Cdf.Format(n.ClientOffer));
            UIKit.SetButtonText(proposeButton, "Proposer " + Cdf.Format(price));
            doneButton.gameObject.SetActive(!open);
            suggestions.gameObject.SetActive(open);
            if (n != null)
            {
                StringBuilder sb = new StringBuilder("Patience du client : ");
                for (int i = 0; i < n.Client.Patience; i++) sb.Append(i < n.RoundsLeft ? "●" : "○");
                statusText.text = sb.ToString();
            }
        }

        private void Propose()
        {
            GameplayDirector g = G;
            if (g == null) return;
            string clientName = g.Client != null ? g.Client.Name : "Client";
            string driver, client;
            NegotiationStep st = g.Propose(price, out driver, out client);
            Log("Vous", driver);
            Log(clientName, client);
            Finish(st);
        }

        private void AcceptCounter()
        {
            GameplayDirector g = G;
            if (g == null) return;
            string driver;
            NegotiationStep st = g.AcceptCounter(out driver);
            Log("Vous", driver);
            Finish(st);
        }

        private void Finish(NegotiationStep st)
        {
            if (st.Outcome == NegotiationOutcome.Accepted)
            {
                finished = true;
                UIKit.SetButtonText(doneButton, "Démarrer la course (" + Cdf.Format(st.Price) + ")");
                if (AmbientAudio.Instance != null) AmbientAudio.Instance.Play(AudioSynth.Click, 0.5f);
            }
            else if (st.Outcome == NegotiationOutcome.ClientLeft)
            {
                finished = true;
                UIKit.SetButtonText(doneButton, "Le client est parti — Fermer");
            }
            else if (st.Outcome == NegotiationOutcome.CounterOffer) price = st.Price;
            UpdateView();
        }

        private void Refuse()
        {
            GameplayDirector g = G;
            if (g == null) { ui.Close(this); return; }
            Log("Vous", g.RefuseClient());
            finished = true;
            UIKit.SetButtonText(doneButton, "Fermer");
            UpdateView();
        }

        public override void OnClose()
        {
            GameplayDirector g = G;
            if (g != null && g.State == RideState.Negociation) g.RefuseClient();
        }
    }

    // =====================================================================
    // Fin de course
    // =====================================================================
    public class RideSummaryPanel : UIPanel
    {
        private Text head, body;
        private readonly List<RectTransform> bars = new List<RectTransform>();
        private readonly List<Text> barLabels = new List<Text>();
        private static readonly string[] Criteria = { "Prix", "Temps", "Sécurité", "Conduite", "Confort", "Respect" };

        protected override void Build()
        {
            Frame("Course terminée", 1300, 940, false);
            Closable = true;
            head = UIKit.Label(content, "", 46, UIKit.Accent, TextAnchor.UpperCenter, FontStyle.Bold);
            UIKit.TopLeft(head.rectTransform, 0, 0, 1252, 130);
            body = UIKit.Label(content, "", 26, UIKit.Text, TextAnchor.UpperLeft);
            UIKit.TopLeft(body.rectTransform, 20, 140, 1212, 230);
            for (int i = 0; i < Criteria.Length; i++)
            {
                Text l = UIKit.Label(content, Criteria[i], 24, UIKit.TextDim, TextAnchor.MiddleLeft);
                UIKit.TopLeft(l.rectTransform, 20, 380 + i * 52, 260, 44);
                barLabels.Add(l);
                RectTransform fill;
                RectTransform bar = UIKit.Bar(content, new Color(1f, 1f, 1f, 0.1f), UIKit.Green, out fill);
                UIKit.TopLeft(bar, 290, 392 + i * 52, 900, 22);
                bars.Add(fill);
            }
            Button ok = UIKit.Button(content, "Continuer", UIKit.Accent, Color.black, 36, () => ui.Close(this));
            UIKit.Place((RectTransform)ok.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(520f, 96f));
        }

        public void Setup(RideResult r, ClientRequest c, string line)
        {
            head.text = TextUtil.Stars(r.Stars) + "   " + r.Stars + "/5\n<size=28>" + (c != null ? c.Name : "") + " — satisfaction " + UIFormat.Percent(r.Satisfaction) + "</size>";
            StringBuilder sb = new StringBuilder();
            if (!string.IsNullOrEmpty(line)) sb.Append("« ").Append(line).Append(" »\n");
            sb.Append("Prix convenu ").Append(Cdf.Format(r.Agreed)).Append("  ·  Payé ").Append(UIKit.Colored(Cdf.Format(r.Paid), r.Paid >= r.Agreed ? UIKit.Green : UIKit.Red));
            if (r.Tip > 0) sb.Append("  ·  Pourboire ").Append(UIKit.Colored(Cdf.Format(r.Tip), UIKit.Green));
            sb.Append("  ·  +").Append(r.Xp).Append(" XP\n");
            sb.Append("Durée : ").Append(r.ActualMinutes.ToString("0.0")).Append(" min (attendue ≈ ").Append(r.ExpectedMinutes.ToString("0.0")).Append(" min)");
            if (r.Accident) sb.Append("\n").Append(UIKit.Colored("Accident pendant la course : le client s'en souviendra.", UIKit.Red));
            body.text = sb.ToString();
            float[] scores = { r.PriceScore, r.TimeScore, r.SafetyScore, r.DrivingScore, r.ComfortScore, r.RespectScore };
            for (int i = 0; i < bars.Count; i++)
            {
                UIKit.SetBar(bars[i], scores[i]);
                bars[i].GetComponent<Image>().color = scores[i] < 0.4f ? UIKit.Red : (scores[i] < 0.7f ? new Color(1f, 0.7f, 0.2f) : UIKit.Green);
                barLabels[i].text = Criteria[i] + "  " + UIFormat.Percent(scores[i]);
            }
        }
    }

    // =====================================================================
    // Carburant : station, point de vente, Kadafi
    // =====================================================================
    public class FuelPanel : UIPanel
    {
        public string VendorId = "station";
        private Text info, total;
        private float liters;

        protected override void Build()
        {
            Frame("Carburant", 1300, 860, true);
            info = UIKit.Label(content, "", 26, UIKit.Text, TextAnchor.UpperLeft);
            UIKit.TopLeft(info.rectTransform, 0, 0, 1252, 300);
            RectTransform row = UIKit.Rect(content, "Quantites");
            UIKit.TopLeft(row, 0, 320, 1252, 110);
            HorizontalLayoutGroup h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 14f;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = true;
            UIKit.Button(row, "+ 1 L", UIKit.PanelLight, Color.white, 32, () => Add(1f));
            UIKit.Button(row, "+ 2 L", UIKit.PanelLight, Color.white, 32, () => Add(2f));
            UIKit.Button(row, "+ 5 L", UIKit.PanelLight, Color.white, 32, () => Add(5f));
            UIKit.Button(row, "Plein", UIKit.Blue, Color.white, 32, () => Add(999f));
            UIKit.Button(row, "Effacer", UIKit.Red, Color.white, 30, () => { liters = 0f; UpdateTotal(); });
            total = UIKit.Label(content, "", 40, UIKit.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.TopLeft(total.rectTransform, 0, 450, 1252, 90);
            Button pay = UIKit.Button(content, "Payer", UIKit.Accent, Color.black, 38, Pay);
            UIKit.Place((RectTransform)pay.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(560f, 104f));
        }

        private GameSession S { get { return BalulaGame.Instance.Session; } }

        public override void Refresh()
        {
            liters = 0f;
            GameSession s = S;
            if (s == null) return;
            FuelVendorDef v = s.Db.FuelVendor(VendorId);
            if (v == null) { ui.Close(this); return; }
            if (titleText != null) titleText.text = v.nom;
            OwnedVehicle ov = s.ActiveVehicle;
            VehicleStats st = GameplayDirector.Instance != null ? GameplayDirector.Instance.Stats : s.ActiveStats();
            string greet = s.Dialogue.Line(VendorId == "kadafi" ? "kadafi" : (VendorId == "point_vente" ? "point_vente" : "station"), "", "prix", Cdf.Format(v.prixLitre));
            info.text = UIKit.Colored("« " + greet + " »", new Color(0.55f, 0.85f, 1f)) + "\n" +
                        "Prix : <b>" + Cdf.Format(v.prixLitre) + " le litre</b>" + (v.litresMin > 0f ? " · minimum " + v.litresMin.ToString("0") + " L" : "") + "\n" +
                        UIKit.Colored(v.description, UIKit.TextDim) + "\n" +
                        (v.risqueQualite > 0f ? UIKit.Colored("Qualité variable : risque d'abîmer le moteur (" + Mathf.RoundToInt(v.risqueQualite * 100f) + " %).", new Color(1f, 0.6f, 0.3f)) + "\n" : "") +
                        "Réservoir : " + ov.carburantL.ToString("0.0") + " / " + st.TankL.ToString("0") + " L · Solde : " + Cdf.Format(s.Profile.argent);
            UpdateTotal();
        }

        private void Add(float l)
        {
            GameSession s = S;
            VehicleStats st = GameplayDirector.Instance != null ? GameplayDirector.Instance.Stats : s.ActiveStats();
            float missing = s.Fuel.MissingLiters(s.ActiveVehicle, st);
            liters = Mathf.Min(missing, liters + l);
            UpdateTotal();
        }

        private void UpdateTotal()
        {
            GameSession s = S;
            if (s == null) return;
            FuelVendorDef v = s.Db.FuelVendor(VendorId);
            total.text = liters <= 0f ? "Choisissez une quantité" : liters.ToString("0.0").Replace('.', ',') + " L  =  " + Cdf.Format(s.Fuel.Price(v, liters));
        }

        private void Pay()
        {
            if (liters <= 0f) { ui.Toast("Choisissez une quantité."); return; }
            GameSession s = S;
            GameplayDirector g = GameplayDirector.Instance;
            VehicleStats st = g != null ? g.Stats : s.ActiveStats();
            RefuelResult r = s.Fuel.Refuel(s.ActiveVehicle, st, VendorId, liters);
            ui.Toast(r.Message);
            if (r.Success)
            {
                if (AmbientAudio.Instance != null) AmbientAudio.Instance.Play(AudioSynth.Cash, 0.5f);
                if (g != null && g.Player != null && s.ActiveVehicle.carburantL > 0f) { g.Player.EngineCut = false; g.Player.PushMode = false; }
                if (r.BadQuality && g != null) g.RefreshStats();
            }
            Refresh();
        }
    }

    // =====================================================================
    // Atelier mécanique
    // =====================================================================
    public class WorkshopPanel : UIPanel
    {
        private RectTransform list;

        protected override void Build()
        {
            Frame("Atelier mécanique", 1500, 980, true);
            RectTransform view = UIKit.ScrollList(content, out list);
            UIKit.Stretch(view, 0, 0, 0, 0);
        }

        public override void Refresh()
        {
            UIKit.Clear(list);
            GameSession s = BalulaGame.Instance.Session;
            if (s == null) return;
            OwnedVehicle v = s.ActiveVehicle;
            VehicleDef d = s.ActiveDef;
            if (v == null || d == null) return;
            UIKit.Card(list, UIKit.Colored("« " + s.Dialogue.Line("atelier", "") + " »", new Color(0.55f, 0.85f, 1f)) + "\n" + d.nom + " · état général " + v.OverallCondition().ToString("0") + " % · solde " + Cdf.Format(s.Profile.argent), 100f, UIKit.Bg);
            foreach (WorkshopQuote q in s.Workshop.Quotes(v))
            {
                string id = q.ServiceId;
                string text = "<b>" + q.Label + "</b>\n" + UIKit.Colored(q.Detail, UIKit.TextDim) + (q.Useful ? "" : "\n" + UIKit.Colored("Pas nécessaire pour le moment", UIKit.Green));
                ListCard.Make(list, text, 130f, Cdf.Format(q.Cost), UIKit.Accent, q.Useful && s.Economy.CanAfford(q.Cost), () =>
                {
                    string message;
                    bool ok = s.Workshop.Perform(v, id, out message);
                    ui.Toast(ok ? (message ?? "Travaux terminés.") : message);
                    if (ok && GameplayDirector.Instance != null) GameplayDirector.Instance.RefreshStats();
                    Refresh();
                });
            }
        }
    }

    // =====================================================================
    // Panne sèche
    // =====================================================================
    public class EmptyTankPanel : UIPanel
    {
        private Text info;

        protected override void Build()
        {
            Frame("Panne sèche", 1200, 760, false);
            Closable = true;
            info = UIKit.Label(content, "", 28, UIKit.Text, TextAnchor.UpperCenter);
            UIKit.TopLeft(info.rectTransform, 0, 0, 1152, 260);
            RectTransform col = UIKit.Rect(content, "Choix");
            UIKit.TopLeft(col, 150, 270, 852, 360);
            UIKit.VList(col, 16f, 0);
            UIKit.Size(UIKit.Button(col, "Appeler un Kadafi (livraison sur place)", UIKit.Accent, Color.black, 30, () =>
            {
                ui.Close(this);
                if (GameplayDirector.Instance != null) GameplayDirector.Instance.CallKadafiDelivery();
            }), 104f);
            UIKit.Size(UIKit.Button(col, "Pousser jusqu'à une station ou un Kadafi", UIKit.PanelLight, Color.white, 30, () =>
            {
                ui.Close(this);
                if (GameplayDirector.Instance != null) GameplayDirector.Instance.StartPushing();
            }), 104f);
            UIKit.Size(UIKit.Button(col, "Plus tard", UIKit.Red, Color.white, 30, () => ui.Close(this)), 96f);
        }

        public override void Refresh()
        {
            GameSession s = BalulaGame.Instance.Session;
            if (s == null) return;
            FuelVendorDef k = s.Db.FuelVendor("kadafi");
            long estimate = Cdf.RoundUpTo(2f * (k != null ? k.prixLitre : 3500), 50) + 2000;
            GameplayDirector g = GameplayDirector.Instance;
            bool near = g != null && g.NearbyInteraction != null &&
                        (g.NearbyInteraction.Kind == InteractionKind.StationService || g.NearbyInteraction.Kind == InteractionKind.PointVente || g.NearbyInteraction.Kind == InteractionKind.Kadafi);
            info.text = "Le réservoir est vide.\nUn vendeur Kadafi peut vous apporter 2 litres (≈ " + Cdf.Format(estimate) + " livraison comprise), ou vous poussez le véhicule." +
                        (near ? "\n" + UIKit.Colored("Un vendeur de carburant est juste à côté : fermez et utilisez le bouton d'action.", UIKit.Green) : "");
        }
    }

    // =====================================================================
    // Contrôle de police (amendes fictives, aucune corruption représentée)
    // =====================================================================
    public class PolicePanel : UIPanel
    {
        private Text agent, details;
        private Button payButton, contestButton, leaveButton;
        private string infraction;
        private bool fled, contested, resolved;

        protected override void Build()
        {
            Frame("Contrôle de police", 1300, 880, false);
            agent = UIKit.Label(content, "", 30, new Color(0.55f, 0.85f, 1f), TextAnchor.UpperLeft);
            UIKit.TopLeft(agent.rectTransform, 0, 0, 1252, 220);
            details = UIKit.Label(content, "", 26, UIKit.Text, TextAnchor.UpperLeft);
            UIKit.TopLeft(details.rectTransform, 0, 230, 1252, 260);
            RectTransform row = UIKit.Rect(content, "Actions");
            UIKit.Place(row, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 10f), new Vector2(1252f, 110f));
            HorizontalLayoutGroup h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 16f;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = true;
            payButton = UIKit.Button(row, "Payer l'amende", UIKit.Accent, Color.black, 30, Pay);
            contestButton = UIKit.Button(row, "Contester poliment", UIKit.PanelLight, Color.white, 30, Contest);
            leaveButton = UIKit.Button(row, "Reprendre la route", UIKit.Green, Color.white, 30, () => ui.Close(this));
        }

        public void Setup(string infractionId, bool hasFled)
        {
            infraction = infractionId;
            fled = hasFled;
            contested = false;
            resolved = false;
        }

        private GameSession S { get { return BalulaGame.Instance.Session; } }

        public override void Refresh()
        {
            GameSession s = S;
            if (s == null) { ui.Close(this); return; }
            if (infraction == "controle")
            {
                agent.text = "Agent : « " + s.Dialogue.Line("police_controle", "") + " »";
                if (s.Police.DocumentsValid)
                {
                    details.text = "Vous présentez le permis, l'assurance et l'autorisation de taxi.\n" + UIKit.Colored("Tout est en règle. Bonne route !", UIKit.Green);
                    resolved = true;
                    UpdateButtons();
                    return;
                }
                infraction = PoliceSystem.NoDocuments;
                agent.text += "\nAgent : « " + s.Dialogue.Line("police_infraction", "", "infraction", s.Police.InfractionLabel(infraction).ToLowerInvariant()) + " »";
            }
            else agent.text = "Agent : « " + s.Dialogue.Line("police_infraction", "", "infraction", s.Police.InfractionLabel(infraction).ToLowerInvariant()) + " »";
            long fine = s.Police.FineAmount(infraction, fled);
            agent.text += "\nAgent : « " + s.Dialogue.Line("police_amende", "", "prix", Cdf.Format(fine)) + " »";
            details.text = "Infraction : <b>" + s.Police.InfractionLabel(infraction) + "</b>" + (fled ? " + refus d'obtempérer" : "") + "\n" +
                           "Amende (fictive) : <b>" + Cdf.Format(fine) + "</b> · votre solde : " + Cdf.Format(s.Profile.argent) + "\n" +
                           UIKit.Colored("Payer donne une quittance officielle. Contester poliment peut aboutir à un simple avertissement (une seule tentative).", UIKit.TextDim);
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            payButton.gameObject.SetActive(!resolved);
            contestButton.gameObject.SetActive(!resolved && !contested && !fled);
            leaveButton.gameObject.SetActive(resolved);
        }

        private void Pay()
        {
            GameSession s = S;
            long paid = s.Police.PayFine(infraction, fled);
            s.Police.RecordInfraction();
            details.text = "Amende payée : " + Cdf.Format(paid) + ". Quittance remise." + (paid < s.Police.FineAmount(infraction, fled) ? "\n" + UIKit.Colored("Solde insuffisant : le reste est noté, votre réputation en souffre.", UIKit.Red) : "");
            resolved = true;
            UpdateButtons();
        }

        private void Contest()
        {
            GameSession s = S;
            contested = true;
            if (s.Police.ContestPolitely())
            {
                agent.text = "Agent : « " + s.Dialogue.Line("police_avertissement", "") + " »";
                details.text = UIKit.Colored("Simple avertissement : pas d'amende cette fois.", UIKit.Green);
                resolved = true;
            }
            else details.text += "\n" + UIKit.Colored("L'agent maintient l'amende.", UIKit.Red);
            UpdateButtons();
        }

        public override void OnClose()
        {
            if (GameplayDirector.Instance != null) GameplayDirector.Instance.PoliceResolved();
        }
    }
}
