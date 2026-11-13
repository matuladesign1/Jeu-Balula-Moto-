// BALULA MOTO — magasin (10 catégories, aperçu 3D, comparaison, favoris) et garage (véhicules possédés,
// améliorations mécaniques, personnalisation, vente jamais obligatoire).
using System.Collections.Generic;
using System.Text;
using BalulaMoto.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BalulaMoto.Runtime
{
    // =====================================================================
    // Aperçu 3D hors champ (caméra dédiée rendue dans une texture)
    // =====================================================================
    public static class VehiclePreview
    {
        private static readonly Vector3 Origin = new Vector3(0f, -2000f, 0f);
        private static Camera cam;
        private static RenderTexture texture;
        private static Transform pivot;
        private static GameObject model;

        public static Texture Show(VehicleDef def, OwnedVehicle ov)
        {
            Ensure();
            if (model != null) Object.Destroy(model);
            model = VehicleBuilder.BuildPreview(def, ov);
            model.transform.SetParent(pivot, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            foreach (Collider c in model.GetComponentsInChildren<Collider>()) c.enabled = false;
            foreach (Rigidbody r in model.GetComponentsInChildren<Rigidbody>()) r.isKinematic = true;
            float size = Mathf.Max(def.longueurM, def.hauteurM * 1.3f, 1.6f);
            cam.transform.position = Origin + new Vector3(size * 0.9f, size * 0.55f, -size * 1.55f);
            cam.transform.LookAt(Origin + Vector3.up * (def.hauteurM * 0.45f));
            cam.enabled = true;
            return texture;
        }

        public static void Rotate(float degrees) { if (pivot != null) pivot.Rotate(0f, degrees, 0f, Space.World); }

        public static void Hide()
        {
            if (model != null) Object.Destroy(model);
            model = null;
            if (cam != null) cam.enabled = false;
        }

        private static void Ensure()
        {
            if (cam != null) return;
            texture = new RenderTexture(1024, 640, 24);
            texture.name = "ApercuVehicule";
            GameObject root = new GameObject("Studio_Apercu");
            root.transform.position = Origin;
            pivot = new GameObject("Pivot").transform;
            pivot.SetParent(root.transform, false);
            GameObject c = new GameObject("CameraApercu");
            c.transform.SetParent(root.transform, false);
            cam = c.AddComponent<Camera>();
            cam.targetTexture = texture;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.14f, 0.19f);
            cam.fieldOfView = 34f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 40f;
            cam.enabled = false;
            GameObject l = new GameObject("Eclairage");
            l.transform.SetParent(root.transform, false);
            l.transform.localPosition = new Vector3(2.5f, 4f, -3.5f);
            Light pl = l.AddComponent<Light>();
            pl.type = LightType.Point;
            pl.range = 25f;
            pl.intensity = 3f;
            Prims.Cyl(root.transform, "Socle", new Vector3(0f, -0.06f, 0f), 6f, 0.12f, new Color(0.22f, 0.24f, 0.3f));
        }
    }

    /// <summary>Rotation automatique de l'aperçu (temps réel, même jeu en pause).</summary>
    public class PreviewSpinner : MonoBehaviour
    {
        public float Pause;
        private void Update()
        {
            if (Pause > 0f) { Pause -= Time.unscaledDeltaTime; return; }
            VehiclePreview.Rotate(Time.unscaledDeltaTime * 22f);
        }
    }

    // =====================================================================
    // Magasin
    // =====================================================================
    public class ShopPanel : UIPanel
    {
        private RectTransform categories, vehicles, detailRoot;
        private Text detailText, headerText;
        private RawImage preview;
        private Button favButton, buyButton, viewButton, compareButton;
        private string selectedCategory = VehicleCategory.MotoTaxiEconomique.ToString();
        private VehicleDef selected;
        private bool showing3D, comparing;

        protected override void Build()
        {
            Frame("Magasin", 1820, 1000, true);
            Image catBox = UIKit.Box(content, "Categories", UIKit.Bg);
            UIKit.TopLeft(catBox.rectTransform, 0, 0, 340, 870);
            RectTransform v1 = UIKit.ScrollList(catBox.transform, out categories);
            UIKit.Stretch(v1, 6, 6, 6, 6);

            Image vehBox = UIKit.Box(content, "Vehicules", UIKit.Bg);
            UIKit.TopLeft(vehBox.rectTransform, 356, 0, 500, 870);
            RectTransform v2 = UIKit.ScrollList(vehBox.transform, out vehicles);
            UIKit.Stretch(v2, 6, 6, 6, 6);

            Image det = UIKit.Box(content, "Details", UIKit.PanelLight);
            UIKit.TopLeft(det.rectTransform, 872, 0, 900, 870);
            detailRoot = det.rectTransform;
            headerText = UIKit.Label(detailRoot, "", 34, UIKit.Accent, TextAnchor.UpperLeft, FontStyle.Bold);
            UIKit.TopLeft(headerText.rectTransform, 24, 16, 852, 96);
            detailText = UIKit.Label(detailRoot, "", 24, UIKit.Text, TextAnchor.UpperLeft);
            UIKit.TopLeft(detailText.rectTransform, 24, 116, 852, 610);
            detailText.resizeTextForBestFit = true;
            detailText.resizeTextMinSize = 16;
            detailText.resizeTextMaxSize = 24;

            preview = new GameObject("Apercu3D", typeof(RectTransform)).AddComponent<RawImage>();
            preview.transform.SetParent(detailRoot, false);
            UIKit.TopLeft(preview.rectTransform, 24, 116, 852, 532);
            DragZone dz = preview.gameObject.AddComponent<DragZone>();
            PreviewSpinner spin = preview.gameObject.AddComponent<PreviewSpinner>();
            dz.Dragged = d => { spin.Pause = 2.5f; VehiclePreview.Rotate(-d.x * 0.4f); };
            preview.gameObject.SetActive(false);

            RectTransform row = UIKit.Rect(detailRoot, "Actions");
            UIKit.Place(row, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 18f), new Vector2(852f, 100f));
            HorizontalLayoutGroup h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 14f;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = true;
            viewButton = UIKit.Button(row, "Voir en 3D", UIKit.Blue, Color.white, 28, Toggle3D);
            compareButton = UIKit.Button(row, "Comparer", UIKit.PanelLight, Color.white, 28, ToggleCompare);
            favButton = UIKit.Button(row, "Favori", UIKit.PanelLight, Color.white, 28, ToggleFavorite);
            buyButton = UIKit.Button(row, "Acheter", UIKit.Accent, Color.black, 30, Buy);
        }

        private GameSession S { get { return BalulaGame.Instance.Session; } }

        public override void Refresh()
        {
            if (S == null) return;
            UIKit.Clear(categories);
            Button fav = UIKit.Button(categories, "Favoris (" + S.Profile.favoris.Count + ")", selectedCategory == "Favoris" ? UIKit.Accent : UIKit.PanelLight, selectedCategory == "Favoris" ? Color.black : Color.white, 26, () => SelectCategory("Favoris"));
            UIKit.Size(fav, 82f);
            foreach (VehicleCategory c in CategoryInfo.All)
            {
                string key = c.ToString();
                int count = BalulaGame.Instance.Db.VehiclesInCategory(c).Count;
                bool sel = key == selectedCategory;
                Button b = UIKit.Button(categories, CategoryInfo.Label(c) + " (" + count + ")", sel ? UIKit.Accent : UIKit.PanelLight, sel ? Color.black : Color.white, 24, () => SelectCategory(key));
                UIKit.Size(b, 82f);
            }
            FillVehicles();
        }

        private void SelectCategory(string key)
        {
            selectedCategory = key;
            selected = null;
            Refresh();
        }

        private List<VehicleDef> CurrentList()
        {
            GameDatabase db = BalulaGame.Instance.Db;
            if (selectedCategory == "Favoris")
            {
                List<VehicleDef> f = new List<VehicleDef>();
                foreach (string id in S.Profile.favoris) { VehicleDef d = db.Vehicle(id); if (d != null) f.Add(d); }
                return f;
            }
            VehicleCategory cat;
            return CategoryInfo.TryParse(selectedCategory, out cat) ? db.VehiclesInCategory(cat) : new List<VehicleDef>();
        }

        private void FillVehicles()
        {
            UIKit.Clear(vehicles);
            List<VehicleDef> list = CurrentList();
            if (list.Count == 0)
            {
                UIKit.Card(vehicles, UIKit.Colored("Aucun modèle dans cette catégorie pour l'instant.", UIKit.TextDim), 90f, UIKit.Bg);
                ShowDetails(null);
                return;
            }
            if (selected == null || !list.Contains(selected)) selected = list[0];
            foreach (VehicleDef d in list)
            {
                VehicleDef dd = d;
                int owned = S.Garage.CountOwned(d.id);
                string label = "<b>" + d.nom + "</b>" + (S.Garage.IsFavorite(d.id) ? "  (favori)" : "") + "\n" + Cdf.Format(S.Garage.Price(d)) +
                               "   ·   " + d.vitesseMaxKmh.ToString("0") + " km/h" + (owned > 0 ? "\n" + UIKit.Colored("Possédé ×" + owned, UIKit.Green) : "");
                bool sel = d == selected;
                Button b = UIKit.Button(vehicles, label, sel ? UIKit.Accent : UIKit.PanelLight, sel ? Color.black : Color.white, 24, () => { selected = dd; FillVehicles(); });
                b.GetComponentInChildren<Text>().fontStyle = FontStyle.Normal;
                b.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
                UIKit.Size(b, 130f);
            }
            ShowDetails(selected);
        }

        private void ShowDetails(VehicleDef d)
        {
            bool has = d != null;
            buyButton.interactable = has;
            favButton.interactable = has;
            viewButton.interactable = has;
            compareButton.interactable = has;
            if (!has) { headerText.text = ""; detailText.text = ""; Show3D(false); return; }
            EconomyConfig eco = BalulaGame.Instance.Db.Economy;
            long price = S.Garage.Price(d);
            headerText.text = d.nom + "\n<size=24>" + CategoryInfo.Label(d.Category) + " · " + Cdf.Format(price) + "  (" + Cdf.FormatUsd(price / eco.tauxUsdCdf) + ")</size>";
            UIKit.SetButtonText(favButton, S.Garage.IsFavorite(d.id) ? "Retirer favori" : "Favori");
            UIKit.SetButtonText(buyButton, S.Economy.CanAfford(price) ? "Acheter" : "Solde insuffisant");
            if (comparing) detailText.text = CompareText(d);
            else detailText.text = Sheet(d);
            if (showing3D) preview.texture = VehiclePreview.Show(d, null);
        }

        private string Sheet(VehicleDef d)
        {
            EconomyConfig eco = BalulaGame.Instance.Db.Economy;
            VehicleStats st = VehicleStatsCalculator.Compute(d, null, eco);
            StringBuilder sb = new StringBuilder();
            if (!string.IsNullOrEmpty(d.inspireDe)) sb.Append(UIKit.Colored("Inspiré de : " + d.inspireDe, UIKit.TextDim)).Append('\n');
            sb.Append("Vitesse max : <b>").Append(d.vitesseMaxKmh.ToString("0")).Append(" km/h</b>   ·   0-50 km/h : <b>").Append(st.Accel0To50s.ToString("0.0")).Append(" s</b>\n");
            sb.Append("Puissance : ").Append(d.puissanceKw.ToString("0.0")).Append(" kW   ·   Masse : ").Append(d.masseKg.ToString("0")).Append(" kg\n");
            sb.Append("Consommation : ").Append(d.consommationL100.ToString("0.0")).Append(" L/100 km   ·   Réservoir : ").Append(d.reservoirL.ToString("0")).Append(" L\n");
            sb.Append("Passagers : <b>").Append(d.passagers).Append("</b>");
            if (!d.IsMoto) sb.Append(" (").Append(d.placesAvant).Append(" à côté du chauffeur + ").Append(d.placesArriere).Append(" à l'arrière)");
            sb.Append("   ·   Charge utile : ").Append(d.chargeUtileKg.ToString("0")).Append(" kg\n");
            sb.Append("Freinage ").Append(d.freinage).Append("/100 · Résistance ").Append(d.resistance).Append("/100 · Confort ").Append(d.confort).Append("/100 · Stabilité ").Append(d.stabilite).Append("/100\n");
            sb.Append("Boîte : ").Append(d.rapports).Append(" rapports").Append(d.marcheArriere ? " + marche arrière" : "").Append("\n");
            if (!string.IsNullOrEmpty(d.noteCapacite)) sb.Append(UIKit.Colored(d.noteCapacite, new Color(1f, 0.75f, 0.35f))).Append('\n');
            if (!string.IsNullOrEmpty(d.statutDonnees)) sb.Append(UIKit.Colored("Données : " + d.statutDonnees, UIKit.TextDim)).Append('\n');
            int owned = S.Garage.CountOwned(d.id);
            if (owned > 0) sb.Append(UIKit.Colored("Vous en possédez " + owned + ". Acheter n'oblige jamais à vendre un autre véhicule.", UIKit.Green));
            else sb.Append(UIKit.Colored("Le véhicule acheté s'ajoute à votre garage ; vos autres véhicules restent à vous.", UIKit.TextDim));
            return sb.ToString();
        }

        private string CompareText(VehicleDef d)
        {
            VehicleDef mine = S.ActiveDef;
            if (mine == null) return "";
            List<string[]> rows = S.Garage.Compare(d, mine);
            StringBuilder sb = new StringBuilder();
            sb.Append("<b>Critère — ").Append(d.nom).Append(" — ").Append(UIKit.Colored(mine.nom + " (votre véhicule)", UIKit.Accent)).Append("</b>\n");
            foreach (string[] r in rows) sb.Append(r[0]).Append(" :  ").Append(r[1]).Append("   |   ").Append(UIKit.Colored(r[2], UIKit.Accent)).Append('\n');
            return sb.ToString();
        }

        private void Toggle3D() { Show3D(!showing3D); }

        private void Show3D(bool on)
        {
            showing3D = on && selected != null;
            preview.gameObject.SetActive(showing3D);
            detailText.gameObject.SetActive(!showing3D);
            UIKit.SetButtonText(viewButton, showing3D ? "Fiche" : "Voir en 3D");
            if (showing3D) preview.texture = VehiclePreview.Show(selected, null);
            else VehiclePreview.Hide();
        }

        private void ToggleCompare()
        {
            comparing = !comparing;
            if (comparing) Show3D(false);
            UIKit.SetButtonText(compareButton, comparing ? "Fiche" : "Comparer");
            ShowDetails(selected);
        }

        private void ToggleFavorite()
        {
            if (selected == null) return;
            S.Garage.ToggleFavorite(selected.id);
            Refresh();
        }

        private void Buy()
        {
            if (selected == null) return;
            VehicleDef d = selected;
            long price = S.Garage.Price(d);
            if (!S.Economy.CanAfford(price)) { ui.Toast("Il manque " + Cdf.Format(price - S.Profile.argent) + "."); return; }
            ui.Ask("Acheter " + d.nom + " ?", "Prix : " + Cdf.Format(price) + "\nSolde après achat : " + Cdf.Format(S.Profile.argent - price) + "\nLe véhicule sera livré au garage MATULA.", "Acheter", () =>
            {
                string error;
                OwnedVehicle ov = S.Garage.Buy(d.id, out error);
                if (ov == null) ui.Toast(error);
                Refresh();
            });
        }

        public override void OnClose()
        {
            Show3D(false);
            comparing = false;
            UIKit.SetButtonText(compareButton, "Comparer");
        }
    }

    // =====================================================================
    // Garage
    // =====================================================================
    public class GaragePanel : UIPanel
    {
        private enum View { Fiche, Ameliorations, Personnalisation }
        private RectTransform owned, detailList;
        private string selectedId;
        private View view = View.Fiche;
        private Button useButton, sellButton, upButton, customButton, workshopButton;

        protected override void Build()
        {
            Frame("Garage", 1820, 1000, true);
            Image left = UIKit.Box(content, "Possedes", UIKit.Bg);
            UIKit.TopLeft(left.rectTransform, 0, 0, 560, 870);
            RectTransform v1 = UIKit.ScrollList(left.transform, out owned);
            UIKit.Stretch(v1, 6, 6, 6, 6);

            Image right = UIKit.Box(content, "Details", UIKit.PanelLight);
            UIKit.TopLeft(right.rectTransform, 576, 0, 1196, 870);
            RectTransform v2 = UIKit.ScrollList(right.transform, out detailList);
            UIKit.Stretch(v2, 12, 12, 12, 130);

            RectTransform row = UIKit.Rect(right.transform, "Actions");
            UIKit.Place(row, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(16f, 16f), new Vector2(1164f, 100f));
            HorizontalLayoutGroup h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 12f;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = true;
            useButton = UIKit.Button(row, "Utiliser", UIKit.Green, Color.white, 28, Use);
            upButton = UIKit.Button(row, "Améliorations", UIKit.Blue, Color.white, 28, () => SetView(View.Ameliorations));
            customButton = UIKit.Button(row, "Personnaliser", UIKit.Blue, Color.white, 28, () => SetView(View.Personnalisation));
            workshopButton = UIKit.Button(row, "Atelier", UIKit.PanelLight, Color.white, 28, OpenWorkshop);
            sellButton = UIKit.Button(row, "Vendre", UIKit.Red, Color.white, 28, Sell);
        }

        private GameSession S { get { return BalulaGame.Instance.Session; } }

        private OwnedVehicle Selected
        {
            get
            {
                OwnedVehicle v = S.Profile.FindVehicle(selectedId);
                if (v == null) { v = S.ActiveVehicle; if (v != null) selectedId = v.instanceId; }
                return v;
            }
        }

        public override void Refresh()
        {
            if (S == null) return;
            UIKit.Clear(owned);
            OwnedVehicle sel = Selected;
            foreach (OwnedVehicle v in S.Profile.vehicules)
            {
                VehicleDef d = S.Db.Vehicle(v.defId);
                if (d == null) continue;
                string id = v.instanceId;
                bool active = v.instanceId == S.Profile.vehiculeActif;
                bool isSel = sel != null && v.instanceId == sel.instanceId;
                string label = "<b>" + d.nom + "</b>" + (active ? "  " + UIKit.Colored("● en service", isSel ? Color.black : UIKit.Green) : "") +
                               "\n" + v.plaque + " · état " + v.OverallCondition().ToString("0") + " % · " + v.kmTotal.ToString("0") + " km";
                Button b = UIKit.Button(owned, label, isSel ? UIKit.Accent : UIKit.PanelLight, isSel ? Color.black : Color.white, 24, () => { selectedId = id; view = View.Fiche; Refresh(); });
                b.GetComponentInChildren<Text>().fontStyle = FontStyle.Normal;
                b.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
                UIKit.Size(b, 120f);
            }
            Text note = UIKit.Label(owned, "Vendre n'est jamais obligatoire : tous vos véhicules restent au garage.", 20, UIKit.TextDim, TextAnchor.MiddleCenter);
            UIKit.Size(note, 70f);
            FillDetails();
        }

        private void SetView(View v)
        {
            view = view == v ? View.Fiche : v;
            FillDetails();
        }

        private void FillDetails()
        {
            UIKit.Clear(detailList);
            OwnedVehicle v = Selected;
            if (v == null) return;
            VehicleDef d = S.Db.Vehicle(v.defId);
            if (d == null) return;
            bool active = v.instanceId == S.Profile.vehiculeActif;
            useButton.interactable = !active;
            UIKit.SetButtonText(useButton, active ? "En service" : "Utiliser");
            sellButton.interactable = S.Profile.vehicules.Count > 1;
            UIKit.SetButtonText(upButton, view == View.Ameliorations ? "Fiche" : "Améliorations");
            UIKit.SetButtonText(customButton, view == View.Personnalisation ? "Fiche" : "Personnaliser");

            switch (view)
            {
                case View.Ameliorations: FillUpgrades(v, d, active); break;
                case View.Personnalisation: FillCosmetics(v, d, active); break;
                default: FillSheet(v, d); break;
            }
        }

        private void FillSheet(OwnedVehicle v, VehicleDef d)
        {
            EconomyConfig eco = S.Db.Economy;
            VehicleStats st = VehicleStatsCalculator.Compute(d, v, eco);
            ListCard.Header(detailList, d.nom + "  —  " + v.plaque);
            StringBuilder sb = new StringBuilder();
            foreach (string c in OwnedVehicle.Components)
            {
                float val = v.GetCondition(c);
                sb.Append(Label(c)).Append(" : ").Append(UIKit.Colored(val.ToString("0") + " %", val < 35f ? UIKit.Red : (val < 65f ? new Color(1f, 0.7f, 0.2f) : UIKit.Green))).Append("    ");
            }
            UIKit.Card(detailList, sb.ToString(), 110f, UIKit.Bg);
            UIKit.Card(detailList, "Kilométrage : " + v.kmTotal.ToString("0") + " km · depuis la vidange : " + v.kmDepuisVidange.ToString("0") + " km (intervalle " + eco.intervalleVidangeKm.ToString("0") + " km)\n" +
                                   "Carburant : " + v.carburantL.ToString("0.0") + " / " + st.TankL.ToString("0") + " L · Valeur de revente estimée : " + Cdf.Format(S.Garage.SellPrice(v)), 100f, UIKit.Bg);
            UIKit.Card(detailList, "Performances actuelles (usure et améliorations comprises) : " + st.MaxSpeedKmh.ToString("0") + " km/h · 0-50 en " + st.Accel0To50s.ToString("0.0") + " s · freinage " +
                                   st.BrakeDecel.ToString("0.0") + " m/s² · " + st.ConsumptionL100.ToString("0.0") + " L/100 km · confort " + VehicleStatsCalculator.Grade(st.Comfort01) + " · stabilité " + VehicleStatsCalculator.Grade(st.Stability01), 100f, UIKit.Bg);
            List<string> w = VehicleWear.Warnings(v, eco);
            if (w.Count > 0) UIKit.Card(detailList, UIKit.Colored("À surveiller : " + string.Join(" · ", w.ToArray()), new Color(1f, 0.55f, 0.45f)), 70f, UIKit.Bg);
            StringBuilder up = new StringBuilder("Améliorations : ");
            foreach (string u in OwnedVehicle.Upgrades)
            {
                UpgradeDef ud = S.Db.Upgrade(u);
                if (ud != null) up.Append(ud.nom).Append(" ").Append(v.GetUpgradeLevel(u)).Append("/").Append(ud.niveauMax).Append(" · ");
            }
            UIKit.Card(detailList, up.ToString(), 100f, UIKit.Bg);
            string acc = v.accessoires != null && v.accessoires.Count > 0 ? string.Join(", ", v.accessoires.ToArray()) : "aucun";
            UIKit.Card(detailList, "Couleurs : " + ColorUtil.DisplayName(v.couleurPrincipale) + " / " + ColorUtil.DisplayName(v.couleurSecondaire) + " · jantes : " + Or(v.jantes) + " · pneus : " + Or(v.pneusStyle) +
                                   " · selle : " + Or(v.selle) + " · phares : " + Or(v.phares) + " · autocollant : " + Or(v.autocollant) + "\nAccessoires : " + acc, 110f, UIKit.Bg);
        }

        private static string Or(string s) { return string.IsNullOrEmpty(s) ? "d'origine" : s; }

        private static string Label(string component)
        {
            switch (component)
            {
                case "moteur": return "Moteur";
                case "pneus": return "Pneus";
                case "freins": return "Freins";
                case "suspension": return "Suspensions";
                case "carrosserie": return "Carrosserie";
                case "proprete": return "Propreté";
            }
            return component;
        }

        private void FillUpgrades(OwnedVehicle v, VehicleDef d, bool active)
        {
            ListCard.Header(detailList, "Améliorations mécaniques — " + d.nom);
            foreach (UpgradeDef u in S.Db.Economy.ameliorations)
            {
                int level = v.GetUpgradeLevel(u.id);
                long cost = S.Garage.UpgradeCost(v, u.id);
                bool max = level >= u.niveauMax;
                string text = "<b>" + u.nom + "</b>   niveau " + level + "/" + u.niveauMax + "\n" + u.description + "\n" +
                              UIKit.Colored(u.effet + " : +" + Mathf.RoundToInt(u.bonusParNiveau * 100f) + " % par niveau", UIKit.TextDim);
                string id = u.id;
                ListCard.Make(detailList, text, 150f, max ? "Maximum" : Cdf.Format(cost), UIKit.Accent, !max && S.Economy.CanAfford(cost), () =>
                {
                    string error;
                    if (!S.Garage.Upgrade(v, id, out error)) ui.Toast(error);
                    else if (active && GameplayDirector.Instance != null) GameplayDirector.Instance.RefreshStats();
                    FillDetails();
                });
            }
        }

        private void FillCosmetics(OwnedVehicle v, VehicleDef d, bool active)
        {
            ListCard.Header(detailList, "Personnalisation — " + d.nom);
            foreach (CosmeticDef c in S.Db.Economy.personnalisation)
            {
                Text title = UIKit.Label(detailList, "<b>" + c.nom + "</b>   " + UIKit.Colored(Cdf.Format(c.prix) + " par option · actuel : " + Or(ColorUtil.DisplayName(v.GetCosmetic(c.type))), UIKit.TextDim), 24, UIKit.Text, TextAnchor.MiddleLeft);
                UIKit.Size(title, 44f);
                RectTransform row = null;
                for (int i = 0; i < c.options.Count; i++)
                {
                    if (i % 5 == 0) row = UIKit.Row(detailList, 76f, 10f);
                    string option = c.options[i];
                    string cid = c.id;
                    bool installed = v.GetCosmetic(c.type) == option || (v.accessoires != null && v.accessoires.Contains(option));
                    bool isColor = option.Contains("|#");
                    Color bg = isColor ? ColorUtil.Parse(option, UIKit.PanelLight) : (installed ? UIKit.Green : UIKit.Bg);
                    float lum = bg.r * 0.3f + bg.g * 0.59f + bg.b * 0.11f;
                    Button b = UIKit.Button(row, (installed ? "Installé : " : "") + ColorUtil.DisplayName(option), bg, lum > 0.55f ? Color.black : Color.white, 22, () =>
                    {
                        string error;
                        if (!S.Garage.ApplyCosmetic(v, cid, option, out error)) ui.Toast(error);
                        else if (active && GameplayDirector.Instance != null) GameplayDirector.Instance.RebuildInPlace();
                        FillDetails();
                    });
                    b.interactable = !installed;
                }
                if (row != null)
                    for (int k = c.options.Count % 5; k > 0 && k < 5; k++)
                    {
                        RectTransform filler = UIKit.Rect(row, "Vide");
                        filler.gameObject.AddComponent<LayoutElement>();
                    }
            }
        }

        private void Use()
        {
            OwnedVehicle v = Selected;
            if (v == null) return;
            S.Garage.SetActive(v.instanceId);
            BalulaGame g = BalulaGame.Instance;
            if (g.Director != null) g.Director.RespawnAtGarage();
            ui.Toast("Votre véhicule vous attend devant le garage MATULA.");
            Refresh();
        }

        private void Sell()
        {
            OwnedVehicle v = Selected;
            if (v == null) return;
            VehicleDef d = S.Db.Vehicle(v.defId);
            long price = S.Garage.SellPrice(v);
            ui.Ask("Vendre ce véhicule ?", (d != null ? d.nom : v.defId) + " — " + v.plaque + "\nPrix de reprise : " + Cdf.Format(price) + "\n(La vente n'est jamais obligatoire.)", "Vendre", () =>
            {
                bool wasActive = v.instanceId == S.Profile.vehiculeActif;
                string error;
                if (!S.Garage.Sell(v.instanceId, out error)) { ui.Toast(error); return; }
                selectedId = S.Profile.vehiculeActif;
                if (wasActive && BalulaGame.Instance.Director != null) BalulaGame.Instance.Director.RespawnAtGarage();
                Refresh();
            });
        }

        private void OpenWorkshop()
        {
            OwnedVehicle v = Selected;
            if (v == null) return;
            if (v.instanceId != S.Profile.vehiculeActif) { ui.Toast("Mettez d'abord ce véhicule en service pour l'amener à l'atelier."); return; }
            ui.OpenWorkshop();
        }
    }
}
