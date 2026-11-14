// BALULA MOTO — garage du menu (d'après les captures envoyées par MATULA DESIGN, adapté au jeu) : le véhicule tourne sur
// le plateau du garage MATULA, fiche de caractéristiques à gauche, flèches pour passer d'un véhicule à l'autre (vos
// véhicules d'abord, puis les modèles à acheter), onglets VÉHICULES / CHAUFFEUR, et en bas AMÉLIORER, BOUTIQUE et
// ROULER (ou ACHETER). Peu de boutons. Acheter n'oblige jamais à vendre un autre véhicule.
using System.Collections.Generic;
using BalulaMoto.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BalulaMoto.Runtime
{
    public class ShowroomPanel : UIPanel
    {
        private struct Entry { public VehicleDef Def; public OwnedVehicle Own; }

        private MenuStyle.Header header;
        private Text nameText, ownedText, priceLabel, priceText, countText, sectionTitle, noteText;
        private Image priceBox, nameBox;
        private Text vSpeed, vAccel, vBrake, vHandle, vTank, vCons, vSeats, vState;
        private RectTransform fSpeed, fAccel, fBrake, fHandle;
        private Button tabVehicles, tabDriver, actionButton, upgradeButton, prevButton, nextButton;
        private RectTransform statsPanel;
        private bool driverTab, catalog;
        private readonly List<Entry> entries = new List<Entry>();
        private int index, avatarIndex;
        private PlayerProfile profile;
        private GameSession menuSession;
        private readonly List<GameObject> statObjs = new List<GameObject>();
        private CanvasGroup menuGroup;

        /// <summary>À l'ouverture : vrai = directement sur les modèles à acheter (bouton BOUTIQUE).</summary>
        public bool StartOnCatalog;

        protected override void Build()
        {
            Closable = true;
            header = MenuStyle.TopBar(Root, "GARAGE", "Choisissez votre véhicule pour rouler dans les rues de Kinshasa", () => ui.Close(this));

            // Titre et onglets au-dessus du plateau
            Image title = UIKit.Box(Root, "TitreSelection", new Color(0.06f, 0.07f, 0.09f, 0.85f), false);
            title.raycastTarget = false;
            UIKit.Place(title.rectTransform, new Vector2(0.62f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -122f), new Vector2(760f, 76f));
            Outline tol = title.gameObject.AddComponent<Outline>();
            tol.effectColor = new Color(1f, 1f, 1f, 0.5f);
            sectionTitle = UIKit.Label(title.transform, "SÉLECTION DU VÉHICULE", 44, Color.white, TextAnchor.MiddleCenter);
            UIKit.Stretch(sectionTitle.rectTransform, 10, 0, 10, 0);
            tabVehicles = UIKit.Button(Root, "VÉHICULES", MenuStyle.GreyTop, Color.black, 28, () => SetTab(false));
            UIKit.Place((RectTransform)tabVehicles.transform, new Vector2(0.62f, 1f), new Vector2(1f, 1f), new Vector2(-8f, -212f), new Vector2(250f, 60f));
            tabDriver = UIKit.Button(Root, "CHAUFFEUR", new Color(0.02f, 0.25f, 0.33f), Color.white, 28, () => SetTab(true));
            UIKit.Place((RectTransform)tabDriver.transform, new Vector2(0.62f, 1f), new Vector2(0f, 1f), new Vector2(8f, -212f), new Vector2(250f, 60f));

            // Fiche à gauche
            Image panel = UIKit.Box(Root, "Fiche", new Color(0.04f, 0.045f, 0.06f, 0.8f), false);
            statsPanel = panel.rectTransform;
            UIKit.Place(statsPanel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -124f), new Vector2(414f, 760f));
            Outline po = panel.gameObject.AddComponent<Outline>();
            po.effectColor = new Color(1f, 1f, 1f, 0.12f);
            nameBox = UIKit.Box(statsPanel, "Nom", MenuStyle.Teal, false);
            UIKit.TopLeft(nameBox.rectTransform, 12, 14, 390, 88);
            nameText = UIKit.Label(nameBox.transform, "", 38, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Stretch(nameText.rectTransform, 8, 4, 8, 4);
            nameText.resizeTextForBestFit = true;
            nameText.resizeTextMinSize = 20;
            nameText.resizeTextMaxSize = 38;
            ownedText = UIKit.Label(statsPanel, "", 20, new Color(0.85f, 0.88f, 0.92f), TextAnchor.MiddleCenter);
            UIKit.TopLeft(ownedText.rectTransform, 12, 104, 390, 34);
            int firstStat = statsPanel.childCount;
            MenuStyle.StatRow(statsPanel, "Vitesse max", 146, out vSpeed, out fSpeed);
            MenuStyle.StatRow(statsPanel, "Accélération", 210, out vAccel, out fAccel);
            MenuStyle.StatRow(statsPanel, "Freinage", 274, out vBrake, out fBrake);
            MenuStyle.StatRow(statsPanel, "Maniabilité", 338, out vHandle, out fHandle);
            MenuStyle.Line(statsPanel, "Réservoir", 412, out vTank);
            MenuStyle.Line(statsPanel, "Consommation", 446, out vCons);
            MenuStyle.Line(statsPanel, "Places", 480, out vSeats);
            MenuStyle.Line(statsPanel, "État", 514, out vState);
            for (int i = firstStat; i < statsPanel.childCount; i++) statObjs.Add(statsPanel.GetChild(i).gameObject);
            priceBox = UIKit.Box(statsPanel, "Prix", new Color(0.85f, 0.12f, 0.1f, 1f), false);
            UIKit.TopLeft(priceBox.rectTransform, 12, 560, 390, 70);
            Outline pbo = priceBox.gameObject.AddComponent<Outline>();
            pbo.effectColor = new Color(1f, 1f, 1f, 0.8f);
            priceLabel = UIKit.Label(priceBox.transform, "", 26, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIKit.Stretch(priceLabel.rectTransform, 14, 0, 160, 0);
            priceText = UIKit.Label(priceBox.transform, "", 32, Color.white, TextAnchor.MiddleRight, FontStyle.Bold);
            UIKit.Stretch(priceText.rectTransform, 120, 0, 14, 0);
            noteText = UIKit.Label(statsPanel, "", 18, UIKit.TextDim, TextAnchor.UpperLeft);
            UIKit.TopLeft(noteText.rectTransform, 16, 640, 384, 112);

            // Flèches de part et d'autre du plateau
            prevButton = UIKit.Button(Root, "‹", new Color(0f, 0f, 0f, 0.45f), Color.white, 90, () => Step(-1));
            UIKit.Place((RectTransform)prevButton.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(470f, -10f), new Vector2(110f, 150f));
            nextButton = UIKit.Button(Root, "›", new Color(0f, 0f, 0f, 0.45f), Color.white, 90, () => Step(1));
            UIKit.Place((RectTransform)nextButton.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-30f, -10f), new Vector2(110f, 150f));
            countText = UIKit.Label(Root, "", 22, new Color(0.9f, 0.92f, 0.95f), TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Place(countText.rectTransform, new Vector2(0.62f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 140f), new Vector2(600f, 40f));
            Outline co = countText.gameObject.AddComponent<Outline>();
            co.effectColor = new Color(0f, 0f, 0f, 0.8f);

            // Boutons du bas
            upgradeButton = MenuStyle.BigButton(Root, "AMÉLIORER", true, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 14f), new Vector2(380f, 112f), Upgrade);
            MenuStyle.BigButton(Root, "BOUTIQUE", false, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(380f, 112f), () => { SetTab(false); JumpToCatalog(); });
            actionButton = MenuStyle.BigButton(Root, "ROULER", true, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0f, 14f), new Vector2(430f, 112f), Action);
        }

        private GameDatabase Db { get { return BalulaGame.Instance.Db; } }

        public override void Refresh()
        {
            catalog = StartOnCatalog;
            StartOnCatalog = false;
            profile = SaveService.Exists(SaveService.LastSlot) ? SaveService.Load(SaveService.LastSlot) : null;
            menuSession = profile != null ? new GameSession(Db, profile, System.Environment.TickCount) : null;
            BuildEntries();
            index = 0;
            if (profile != null)
                for (int i = 0; i < entries.Count; i++) if (entries[i].Own != null && entries[i].Own.instanceId == profile.vehiculeActif) { index = i; break; }
            if (catalog) JumpToCatalog();
            if (GarageScene.Instance != null) GarageScene.Instance.SetShowroom(true);
            HideMainMenu(true);
            header.Money.text = profile != null ? Cdf.Format(profile.argent) : "0 FC";
            header.Level.text = profile != null ? profile.niveau.ToString() : "1";
            header.Rank.text = profile != null ? TextUtil.Stars(profile.Reputation()) : "";
            avatarIndex = 0;
            string cur = GameSettings.DriverAvatar ?? "";
            for (int i = 0; i < HumanLibrary.DriverChoices.Length; i++) if (HumanLibrary.DriverChoices[i][0] == cur) avatarIndex = i + 1;
            SetTab(false);
        }

        /// <summary>Affiche le modèle demandé sur le plateau (photos de contrôle). Faux s'il n'est pas dans la liste.</summary>
        public bool SelectVehicle(string defId)
        {
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].Def.id == defId) { index = i; if (driverTab) SetTab(false); else ShowCurrent(); return true; }
            return false;
        }

        /// <summary>Ouvre l'onglet chauffeur (photos de contrôle).</summary>
        public void ShowDriverTab() { SetTab(true); }

        /// <summary>Le menu principal reste ouvert sous le garage mais devient invisible et ne capte plus les touches.</summary>
        private void HideMainMenu(bool hide)
        {
            if (ui.MainMenu == null || ui.MainMenu.Root == null) return;
            if (menuGroup == null)
            {
                menuGroup = ui.MainMenu.Root.GetComponent<CanvasGroup>();
                if (menuGroup == null) menuGroup = ui.MainMenu.Root.gameObject.AddComponent<CanvasGroup>();
            }
            menuGroup.alpha = hide ? 0f : 1f;
            menuGroup.blocksRaycasts = !hide;
            menuGroup.interactable = !hide;
        }

        /// <summary>Caméra du garage : cadrage du véhicule sur le plateau, ou du chauffeur debout à côté.</summary>
        private void Frame(VehicleDef d)
        {
            BalulaGame g = BalulaGame.Instance;
            if (g == null || g.Cam == null || GarageScene.Instance == null) return;
            Vector3 pos, look;
            if (driverTab) GarageScene.Instance.DriverShot(out pos, out look);
            else GarageScene.Instance.ShowroomShot(d, out pos, out look);
            g.Cam.SetMenuShot(pos, look);
        }

        private void BuildEntries()
        {
            entries.Clear();
            HashSet<string> ownedIds = new HashSet<string>();
            if (profile != null)
                foreach (OwnedVehicle ov in profile.vehicules)
                {
                    VehicleDef d = Db.Vehicle(ov.defId);
                    if (d == null) continue;
                    entries.Add(new Entry { Def = d, Own = ov });
                    ownedIds.Add(d.id);
                }
            foreach (VehicleDef d in Db.Vehicles)
            {
                if (d.masqueBoutique) continue;
                if (ownedIds.Contains(d.id)) continue;
                entries.Add(new Entry { Def = d, Own = null });
            }
        }

        private void JumpToCatalog()
        {
            for (int i = 0; i < entries.Count; i++) if (entries[i].Own == null) { index = i; break; }
            ShowCurrent();
        }

        private void SetTab(bool driver)
        {
            driverTab = driver;
            UIKit.SetButtonText(tabVehicles, "VÉHICULES");
            tabVehicles.GetComponent<Image>().color = driver ? new Color(0.02f, 0.25f, 0.33f) : MenuStyle.GreyTop;
            tabVehicles.GetComponentInChildren<Text>().color = driver ? Color.white : Color.black;
            tabDriver.GetComponent<Image>().color = driver ? MenuStyle.GreyTop : new Color(0.02f, 0.25f, 0.33f);
            tabDriver.GetComponentInChildren<Text>().color = driver ? Color.black : Color.white;
            sectionTitle.text = driver ? "CHOIX DU CHAUFFEUR" : "SÉLECTION DU VÉHICULE";
            ShowCurrent();
        }

        private void Step(int dir)
        {
            if (driverTab)
            {
                int n = HumanLibrary.DriverChoices.Length + 1;
                avatarIndex = ((avatarIndex + dir) % n + n) % n;
                GameSettings.DriverAvatar = avatarIndex == 0 ? "" : HumanLibrary.DriverChoices[avatarIndex - 1][0];
                if (GarageScene.Instance != null) GarageScene.Instance.RefreshRider();
            }
            else if (entries.Count > 0) index = ((index + dir) % entries.Count + entries.Count) % entries.Count;
            ShowCurrent();
        }

        private void ShowCurrent()
        {
            if (driverTab)
            {
                string label = avatarIndex == 0 ? "Au hasard" : HumanLibrary.DriverChoices[avatarIndex - 1][1];
                nameText.text = label;
                ownedText.text = "Votre chauffeur dans la ville";
                countText.text = "Chauffeur " + (avatarIndex + 1) + " / " + (HumanLibrary.DriverChoices.Length + 1);
                noteText.text = "Personnages 3D : Microsoft Rocketbox (licence MIT). Le chauffeur choisi prend la moto au prochain départ.";
                priceBox.gameObject.SetActive(false);
                upgradeButton.gameObject.SetActive(false);
                foreach (GameObject so in statObjs) so.SetActive(false);
                MenuStyle.SetBigLabel(actionButton, "VALIDER");
                Frame(null);
                return;
            }
            priceBox.gameObject.SetActive(true);
            foreach (GameObject so in statObjs) so.SetActive(true);
            if (entries.Count == 0) return;
            Entry e = entries[index];
            VehicleDef d = e.Def;
            EconomyConfig eco = Db.Economy;
            VehicleStats st = VehicleStatsCalculator.Compute(d, e.Own, eco);
            nameText.text = d.nom;
            bool owned = e.Own != null;
            bool active = owned && profile != null && e.Own.instanceId == profile.vehiculeActif;
            ownedText.text = owned ? (active ? UIKit.Colored("● en service", UIKit.Green) + "  ·  " + e.Own.plaque : "Dans votre garage  ·  " + e.Own.plaque) : CategoryInfo.Label(d.Category);
            vSpeed.text = st.MaxSpeedKmh.ToString("0") + " KM/H";
            UIKit.SetBar(fSpeed, st.MaxSpeedKmh / 180f);
            float acc = Mathf.Clamp01((14f - st.Accel0To50s) / 11f);
            vAccel.text = Mathf.RoundToInt(acc * 100f) + " %";
            UIKit.SetBar(fAccel, acc);
            float brk = Mathf.Clamp01(st.BrakeDecel / 9f);
            vBrake.text = Mathf.RoundToInt(brk * 100f) + " %";
            UIKit.SetBar(fBrake, brk);
            vHandle.text = Mathf.RoundToInt(st.Stability01 * 100f) + " %";
            UIKit.SetBar(fHandle, st.Stability01);
            vTank.text = st.TankL.ToString("0") + " L";
            vCons.text = st.ConsumptionL100.ToString("0.0").Replace('.', ',') + " L/100";
            vSeats.text = d.IsMoto ? "1 + " + d.passagers : (d.passagers + " passagers");
            vState.text = owned ? e.Own.OverallCondition().ToString("0") + " %" : "neuf";
            if (owned && menuSession != null)
            {
                priceBox.color = new Color(0.85f, 0.12f, 0.1f, 1f);
                priceLabel.text = "Revente :";
                priceText.text = Cdf.Format(menuSession.Garage.SellPrice(e.Own));
                noteText.text = "Vendre n'est jamais obligatoire : tous vos véhicules restent au garage.";
                MenuStyle.SetBigLabel(actionButton, active ? "ROULER" : "UTILISER");
            }
            else
            {
                long price = menuSession != null ? menuSession.Garage.Price(d) : GarageSystem.PriceCdf(d, eco);
                priceBox.color = new Color(0.1f, 0.5f, 0.14f, 1f);
                priceLabel.text = "Prix :";
                priceText.text = Cdf.Format(price);
                noteText.text = (string.IsNullOrEmpty(d.inspireDe) ? "" : "Inspiré de : " + d.inspireDe + "\n") + "Le véhicule acheté s'ajoute à votre garage.";
                MenuStyle.SetBigLabel(actionButton, profile == null ? "COMMENCER" : "ACHETER");
            }
            upgradeButton.gameObject.SetActive(owned);
            countText.text = (owned ? "Vos véhicules" : "Modèles à acheter") + "  ·  " + (index + 1) + " / " + entries.Count;
            if (GarageScene.Instance != null) GarageScene.Instance.ShowVehicle(d, e.Own);
            Frame(d);
        }

        private void Action()
        {
            BalulaGame g = BalulaGame.Instance;
            if (driverTab) { SetTab(false); return; }
            if (entries.Count == 0) return;
            Entry e = entries[index];
            if (profile == null)
            {
                // Pas encore de partie : une carrière commence (le véhicule de départ est choisi à l'écran suivant)
                ui.NewGame.Mode = GameMode.Carriere;
                ui.Open(ui.NewGame);
                return;
            }
            if (e.Own != null)
            {
                profile.vehiculeActif = e.Own.instanceId;
                SaveService.Save(profile);
                g.StartGame(profile);
                return;
            }
            long price = menuSession.Garage.Price(e.Def);
            if (!menuSession.Economy.CanAfford(price)) { ui.Toast("Il manque " + Cdf.Format(price - profile.argent) + " pour acheter ce véhicule."); return; }
            VehicleDef d = e.Def;
            ui.Ask("Acheter " + d.nom + " ?", "Prix : " + Cdf.Format(price) + "\nSolde après achat : " + Cdf.Format(profile.argent - price) + "\nLe véhicule sera livré au garage MATULA. Vos autres véhicules restent à vous.", "Acheter", () =>
            {
                string error;
                OwnedVehicle ov = menuSession.Garage.Buy(d.id, out error);
                if (ov == null) { ui.Toast(error); return; }
                SaveService.Save(profile);
                ui.Toast(d.nom + " ajouté à votre garage.");
                BuildEntries();
                for (int i = 0; i < entries.Count; i++) if (entries[i].Own == ov) { index = i; break; }
                header.Money.text = Cdf.Format(profile.argent);
                ShowCurrent();
            });
        }

        private void Upgrade()
        {
            if (entries.Count == 0 || entries[index].Own == null || profile == null) return;
            // Les améliorations se font à l'atelier : la partie reprend avec ce véhicule, le garage s'ouvre
            profile.vehiculeActif = entries[index].Own.instanceId;
            SaveService.Save(profile);
            BalulaGame.Instance.StartGame(profile);
            ui.Garage.Preset(1);
            ui.OpenGarage();
        }

        public override void OnClose()
        {
            HideMainMenu(false);
            if (GarageScene.Instance != null) GarageScene.Instance.SetShowroom(false);
            BalulaGame g = BalulaGame.Instance;
            if (g != null && g.Session == null && ui.MainMenu.IsOpen) ui.MainMenu.Refresh();
        }
    }
}
