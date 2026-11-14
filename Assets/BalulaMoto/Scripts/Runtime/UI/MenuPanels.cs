// BALULA MOTO — écrans de menu : accueil, nouvelle partie, pause, paramètres, crédits, confirmation.
using System.Collections.Generic;
using BalulaMoto.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace BalulaMoto.Runtime
{
    // =====================================================================
    // Menu principal (sélection du mode) : cartes de mode à gauche, cadre d'information sur le garage 3D à droite,
    // BOUTIQUE et ROULER en bas — d'après les captures envoyées par MATULA DESIGN, avec peu de boutons
    // =====================================================================
    public class MainMenuPanel : UIPanel
    {
        private MenuStyle.Header header;
        private int selected;
        private readonly Text[] tags = new Text[3];
        private readonly Image[] frames = new Image[3];
        private readonly Image[] tagBacks = new Image[3];
        private Text infoTitle, infoText, vehicleText, worldText, savesText;
        private Button rideButton;
        private float lastClick;
        private int lastCard = -1;
        private static readonly string[] Titles = { "CARRIÈRE", "MODE LIBRE", "GARAGE" };
        private static readonly string[] Subs = { "Courses, réputation et provinces", "Roulez librement dans Kinshasa", "Motos, tricycles et chauffeur" };
        private static readonly string[] Infos =
        {
            "Devenez le meilleur chauffeur de taxi-moto de Kinshasa : prenez des clients, négociez le prix, respectez le code de la route, gagnez votre réputation et achetez de nouvelles motos et de nouveaux tricycles.",
            "Roulez sans objectif dans les rues réelles de Kinshasa, de jour comme de nuit : explorez les boulevards, les avenues et les quartiers à votre rythme.",
            "Choisissez votre véhicule sur le plateau du garage MATULA : caractéristiques, achat de nouveaux modèles, améliorations et chauffeur."
        };

        protected override void Build()
        {
            Closable = false;
            header = MenuStyle.TopBar(Root, "BALULA MOTO", "MATULA DESIGN · taxi-moto et tricycle dans Kinshasa", null);
            // Petits boutons de la barre du haut
            string[] small = { "Paramètres", "Crédits", "Quitter" };
            UnityAction[] acts = { () => ui.Open(ui.Settings), () => ui.Open(ui.Credits), () => BalulaGame.Instance.Quit() };
            for (int i = 0; i < small.Length; i++)
            {
                Button b = UIKit.Button(header.Title.transform.parent.parent, small[i], new Color(1f, 1f, 1f, 0.1f), new Color(0.88f, 0.9f, 0.94f), 22, acts[i]);
                UIKit.Place((RectTransform)b.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-560f - (2 - i) * 172f, -22f), new Vector2(160f, 58f));
            }

            Color[] stripes = { new Color(0.86f, 0.14f, 0.1f), new Color(0.1f, 0.5f, 0.95f), new Color(0.98f, 0.68f, 0.05f) };
            for (int i = 0; i < 3; i++)
            {
                int k = i;
                MenuStyle.ModeCard(Root, Titles[i], Subs[i], stripes[i], new Vector2(48f, -146f - i * 196f), () => Card(k), out tags[i], out frames[i]);
                Transform bg = tags[i].transform.parent.Find("FondEtiquette");
                tagBacks[i] = bg != null ? bg.GetComponent<Image>() : null;
            }
            Button newGame = UIKit.Button(Root, "Nouvelle partie", new Color(1f, 1f, 1f, 0.1f), UIKit.Accent, 24, () => { ui.NewGame.Mode = selected == 1 ? GameMode.Libre : GameMode.Carriere; ui.Open(ui.NewGame); });
            UIKit.Place((RectTransform)newGame.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(48f, -146f - 3 * 196f + 6f), new Vector2(250f, 56f));

            // Cadre d'information : le garage 3D (moto et chauffeur) reste visible au travers
            Image frame = UIKit.Box(Root, "CadreInfo", new Color(1f, 1f, 1f, 0.02f));
            frame.raycastTarget = false;
            frame.rectTransform.anchorMin = Vector2.zero;
            frame.rectTransform.anchorMax = Vector2.one;
            frame.rectTransform.offsetMin = new Vector2(612f, 160f);
            frame.rectTransform.offsetMax = new Vector2(-46f, -132f);
            Outline fo = frame.gameObject.AddComponent<Outline>();
            fo.effectColor = new Color(1f, 1f, 1f, 0.85f);
            fo.effectDistance = new Vector2(3f, 3f);
            infoTitle = UIKit.Label(frame.transform, "", 64, Color.white, TextAnchor.UpperCenter);
            UIKit.Place(infoTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(900f, 90f));
            Outline to = infoTitle.gameObject.AddComponent<Outline>();
            to.effectColor = new Color(0f, 0f, 0f, 0.85f);
            to.effectDistance = new Vector2(3f, -3f);
            Image strip = UIKit.Box(frame.transform, "Info", new Color(0f, 0f, 0f, 0.62f), false);
            strip.raycastTarget = false;
            strip.rectTransform.anchorMin = new Vector2(0f, 0f);
            strip.rectTransform.anchorMax = new Vector2(1f, 0f);
            strip.rectTransform.pivot = new Vector2(0.5f, 0f);
            strip.rectTransform.sizeDelta = new Vector2(0f, 150f);
            strip.rectTransform.anchoredPosition = Vector2.zero;
            Text info = UIKit.Label(strip.transform, "INFO", 34, Color.white, TextAnchor.UpperLeft);
            UIKit.Place(info.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -8f), new Vector2(200f, 44f));
            infoText = UIKit.Label(strip.transform, "", 23, Color.white, TextAnchor.UpperLeft, FontStyle.Bold);
            UIKit.Stretch(infoText.rectTransform, 20, 52, 20, 8);
            vehicleText = UIKit.Label(frame.transform, "", 26, Color.white, TextAnchor.LowerRight, FontStyle.Bold);
            UIKit.Place(vehicleText.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-18f, 160f), new Vector2(760f, 80f));
            Outline vo = vehicleText.gameObject.AddComponent<Outline>();
            vo.effectColor = new Color(0f, 0f, 0f, 0.85f);

            // Boutons du bas : BOUTIQUE (modèles à acheter) et ROULER
            MenuStyle.BigButton(Root, "BOUTIQUE", false, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 14f), new Vector2(400f, 112f), () => ui.OpenShowroom(true));
            rideButton = MenuStyle.BigButton(Root, "ROULER", true, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0f, 14f), new Vector2(430f, 112f), Go);
            savesText = UIKit.Label(Root, "", 19, UIKit.TextDim, TextAnchor.LowerCenter);
            UIKit.Place(savesText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(40f, 74f), new Vector2(900f, 60f));
            worldText = UIKit.Label(Root, "", 17, UIKit.TextDim, TextAnchor.LowerCenter);
            UIKit.Place(worldText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(40f, 18f), new Vector2(900f, 56f));
        }

        private void Card(int k)
        {
            bool twice = k == lastCard && Time.unscaledTime - lastClick < 0.45f;
            lastCard = k;
            lastClick = Time.unscaledTime;
            selected = k;
            ShowSelection();
            if (twice) Go();
        }

        private void ShowSelection()
        {
            for (int i = 0; i < 3; i++) if (frames[i] != null) frames[i].GetComponent<Outline>().effectColor = new Color(1f, 0.82f, 0.1f, i == selected ? 1f : 0f);
            infoTitle.text = Titles[selected];
            infoText.text = Infos[selected];
            MenuStyle.SetBigLabel(rideButton, selected == 2 ? "OUVRIR" : "ROULER");
        }

        private void Go()
        {
            BalulaGame g = BalulaGame.Instance;
            if (selected == 2) { ui.OpenShowroom(false); return; }
            g.PlayMode(selected == 1 ? GameMode.Libre : GameMode.Carriere);
        }

        public override void Refresh()
        {
            BalulaGame g = BalulaGame.Instance;
            bool hasSave = SaveService.Exists(SaveService.LastSlot);
            PlayerProfile p = hasSave ? SaveService.Load(SaveService.LastSlot) : null;
            if (p != null && lastCard < 0) selected = p.Mode == GameMode.Libre ? 1 : 0;
            PlayerProfile career = SaveService.Exists(SaveService.SlotCareer) ? SaveService.Load(SaveService.SlotCareer) : null;
            PlayerProfile free = SaveService.Exists(SaveService.SlotFree) ? SaveService.Load(SaveService.SlotFree) : null;
            SetTag(0, career != null ? "REPRENDRE" : "NOUVEAU", career != null ? new Color(0.1f, 0.55f, 0.15f) : new Color(0.86f, 0.12f, 0.1f));
            SetTag(1, free != null ? "REPRENDRE" : "", new Color(0.1f, 0.55f, 0.15f));
            SetTag(2, "", Color.clear);
            ShowSelection();
            savesText.text = "Carrière : " + SaveService.Summary(SaveService.SlotCareer) + "    ·    Libre : " + SaveService.Summary(SaveService.SlotFree);
            header.Money.text = p != null ? Cdf.Format(p.argent) : "0 FC";
            header.Level.text = p != null ? p.niveau.ToString() : "1";
            header.Rank.text = p != null ? TextUtil.Stars(p.Reputation()) : "";
            string vehicleLine = "";
            if (g != null && g.Db != null)
            {
                OwnedVehicle ov = p != null ? p.FindVehicle(p.vehiculeActif) : null;
                VehicleDef def = ov != null ? g.Db.Vehicle(ov.defId) : null;
                if (def != null && ov != null)
                {
                    VehicleStats st = VehicleStatsCalculator.Compute(def, ov, g.Db.Economy);
                    vehicleLine = def.nom + "\n" + UIKit.Colored(st.MaxSpeedKmh.ToString("0") + " km/h · état " + ov.OverallCondition().ToString("0") + " % · carburant " +
                                  ov.carburantL.ToString("0.0").Replace('.', ',') + " / " + st.TankL.ToString("0") + " L", new Color(0.85f, 0.87f, 0.9f));
                }
                if (GarageScene.Instance != null)
                {
                    GarageScene.Instance.SetShowroom(false);
                    GarageScene.Instance.Show(g.Db, p);
                }
                if (g.Cam != null && GarageScene.Instance != null) g.Cam.SetMenuShot(GarageScene.Instance.CameraPosition, GarageScene.Instance.CameraLook);
            }
            vehicleText.text = vehicleLine;
            if (g != null && g.World != null)
                worldText.text = g.World.IsTestTrack ? "Monde actuel : piste d'essai (ce n'est pas Kinshasa)." : "Carte : " + g.World.Name + " · " + g.World.Attribution;
        }

        private void SetTag(int i, string text, Color bg)
        {
            tags[i].text = text;
            if (tagBacks[i] != null)
            {
                tagBacks[i].gameObject.SetActive(!string.IsNullOrEmpty(text));
                tagBacks[i].color = bg;
            }
        }
    }

    // =====================================================================
    // Choix du chauffeur (le personnage debout dans le garage change en direct)
    // =====================================================================
    public class DriverPanel : UIPanel
    {
        private readonly List<Button> buttons = new List<Button>();

        protected override void Build()
        {
            // Panneau à droite : le garage et le chauffeur restent visibles à gauche
            Image panel = UIKit.Box(Root, "Panneau", new Color(0.06f, 0.07f, 0.1f, 0.94f));
            panel.rectTransform.anchorMin = new Vector2(0.6f, 0f);
            panel.rectTransform.anchorMax = new Vector2(1f, 1f);
            panel.rectTransform.offsetMin = new Vector2(0f, 20f);
            panel.rectTransform.offsetMax = new Vector2(-20f, -130f);
            Text title = UIKit.Label(panel.transform, "CHAUFFEUR", 40, UIKit.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIKit.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -10f), new Vector2(500f, 60f));
            Button close = UIKit.Button(panel.transform, "Valider", UIKit.Green, Color.white, 28, () => ui.Close(this));
            UIKit.Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -12f), new Vector2(200f, 64f));
            RectTransform list;
            RectTransform view = UIKit.ScrollList(panel.transform, out list);
            UIKit.Stretch(view, 18, 90, 18, 18);
            Button random = UIKit.Button(list, "Au hasard (selon la course)", UIKit.PanelLight, Color.white, 26, () => Choose(""));
            UIKit.Size(random, 84f);
            buttons.Add(random);
            foreach (string[] c in HumanLibrary.DriverChoices)
            {
                string id = c[0];
                Button b = UIKit.Button(list, c[1], UIKit.PanelLight, Color.white, 26, () => Choose(id));
                UIKit.Size(b, 84f);
                buttons.Add(b);
            }
            Text note = UIKit.Label(list, "Personnages 3D : Microsoft Rocketbox (licence MIT).", 18, UIKit.TextDim, TextAnchor.MiddleCenter);
            UIKit.Size(note, 50f);
        }

        private void Choose(string id)
        {
            GameSettings.DriverAvatar = id;
            Refresh();
            BalulaGame g = BalulaGame.Instance;
            if (g != null && g.Session == null && GarageScene.Instance != null)
                GarageScene.Instance.Show(g.Db, SaveService.Exists(SaveService.LastSlot) ? SaveService.Load(SaveService.LastSlot) : null);
            if (g != null && g.Session != null) ui.Toast("Le nouveau chauffeur prendra la moto au prochain départ.");
        }

        public override void Refresh()
        {
            string cur = GameSettings.DriverAvatar ?? "";
            for (int i = 0; i < buttons.Count; i++)
            {
                string id = i == 0 ? "" : HumanLibrary.DriverChoices[i - 1][0];
                UIKit.SetSelected(buttons[i], id == cur);
            }
        }
    }

    // =====================================================================
    // Nouvelle partie
    // =====================================================================
    public class NewGamePanel : UIPanel
    {
        public GameMode Mode = GameMode.Carriere;
        private string vehicleId;
        private Button careerButton, freeButton;
        private Text modeText;
        private RectTransform vehicles;
        private InputField nameField;
        private readonly List<Button> vehicleButtons = new List<Button>();
        private readonly List<string> vehicleIds = new List<string>();

        protected override void Build()
        {
            Frame("Nouvelle partie", 1560, 940, true);
            VerticalLayoutGroup v = UIKit.VList(content, 16f, 0);
            v.childAlignment = TextAnchor.UpperCenter;

            RectTransform modes = UIKit.Row(content, 96f, 20f);
            careerButton = UIKit.Button(modes, "Mode carrière", UIKit.PanelLight, Color.white, 34, () => SetMode(GameMode.Carriere));
            freeButton = UIKit.Button(modes, "Mode libre", UIKit.PanelLight, Color.white, 34, () => SetMode(GameMode.Libre));
            modeText = UIKit.Label(content, "", 26, UIKit.TextDim, TextAnchor.MiddleCenter);
            UIKit.Size(modeText, 110f);

            Text pick = UIKit.Label(content, "Véhicule de départ", 30, UIKit.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIKit.Size(pick, 44f);
            vehicles = UIKit.Row(content, 220f, 20f);

            RectTransform nameRow = UIKit.Row(content, 84f, 20f);
            Text nl = UIKit.Label(nameRow, "Nom du chauffeur", 28, UIKit.Text, TextAnchor.MiddleLeft);
            UIKit.Size(nl, 84f, 300f);
            nameField = CreateInput(nameRow, "Chauffeur");

            Button start = UIKit.Button(content, "Commencer", UIKit.Accent, Color.black, 40, Start);
            UIKit.Size(start, 110f);
        }

        public static InputField CreateInput(Transform parent, string value)
        {
            Image bg = UIKit.Box(parent, "Champ", UIKit.PanelLight);
            InputField f = bg.gameObject.AddComponent<InputField>();
            Text t = UIKit.Label(bg.transform, "", 30, Color.white, TextAnchor.MiddleLeft);
            UIKit.Stretch(t.rectTransform, 18, 6, 18, 6);
            t.supportRichText = false;
            Text ph = UIKit.Label(bg.transform, "Votre nom", 30, UIKit.TextDim, TextAnchor.MiddleLeft, FontStyle.Italic);
            UIKit.Stretch(ph.rectTransform, 18, 6, 18, 6);
            f.textComponent = t;
            f.placeholder = ph;
            f.targetGraphic = bg;
            f.characterLimit = 24;
            f.text = value;
            return f;
        }

        private void SetMode(GameMode m)
        {
            Mode = m;
            UIKit.SetSelected(careerButton, m == GameMode.Carriere);
            UIKit.SetSelected(freeButton, m == GameMode.Libre);
            EconomyConfig eco = BalulaGame.Instance.Db.Economy;
            modeText.text = m == GameMode.Carriere
                ? "Carrière : niveau 1, " + Cdf.Format(eco.argentDepartCarriere) + ", véhicule d'occasion, documents à renouveler, provinces à débloquer."
                : "Libre : " + Cdf.Format(eco.argentDepartLibre) + ", véhicule neuf, toutes les provinces ouvertes (dont la zone du Pont Maréchal à Matadi).";
        }

        public override void Refresh()
        {
            SetMode(Mode);
            UIKit.Clear(vehicles);
            vehicleButtons.Clear();
            vehicleIds.Clear();
            GameDatabase db = BalulaGame.Instance.Db;
            foreach (VehicleDef d in db.StartingVehicles())
            {
                string id = d.id;
                string label = "<b>" + d.nom + "</b>\n" + CategoryInfo.Label(d.Category) + "\n" + (d.IsMoto ? "1 passager" : d.passagers + " places (" + d.placesAvant + " devant + " + d.placesArriere + " derrière)") +
                               " · " + d.vitesseMaxKmh.ToString("0") + " km/h";
                Button b = UIKit.Button(vehicles, label, UIKit.PanelLight, Color.white, 28, () => SelectVehicle(id));
                b.GetComponentInChildren<Text>().fontStyle = FontStyle.Normal;
                vehicleButtons.Add(b);
                vehicleIds.Add(id);
            }
            if (vehicleIds.Count > 0 && (vehicleId == null || !vehicleIds.Contains(vehicleId))) vehicleId = vehicleIds[0];
            SelectVehicle(vehicleId);
        }

        private void SelectVehicle(string id)
        {
            vehicleId = id;
            for (int i = 0; i < vehicleButtons.Count; i++) UIKit.SetSelected(vehicleButtons[i], vehicleIds[i] == id);
        }

        private void Start()
        {
            string slot = Mode == GameMode.Libre ? SaveService.SlotFree : SaveService.SlotCareer;
            string name = nameField != null ? nameField.text : "Chauffeur";
            GameMode mode = Mode;
            string vid = vehicleId;
            if (SaveService.Exists(slot))
                ui.Ask("Écraser la partie ?", "Une partie " + (mode == GameMode.Libre ? "libre" : "carrière") + " existe déjà :\n" + SaveService.Summary(slot) + "\n\nLa remplacer par une nouvelle partie ?", "Écraser",
                    () => BalulaGame.Instance.NewGame(mode, vid, name));
            else BalulaGame.Instance.NewGame(mode, vid, name);
        }
    }

    // =====================================================================
    // Pause
    // =====================================================================
    public class PausePanel : UIPanel
    {
        private Text info;

        protected override void Build()
        {
            Frame("Pause", 1000, 1000, true);
            RectTransform list;
            RectTransform view = UIKit.ScrollList(content, out list);
            UIKit.Stretch(view, 0, 0, 0, 0);
            info = UIKit.Label(list, "", 24, UIKit.TextDim, TextAnchor.MiddleCenter);
            UIKit.Size(info, 70f);
            Add(list, "Reprendre", UIKit.Green, () => ui.Close(this));
            Add(list, "Missions", UIKit.PanelLight, () => { ui.Close(this); ui.OpenMissions(); });
            Add(list, "Finances et documents", UIKit.PanelLight, () => { ui.Close(this); ui.OpenFinances(); });
            Add(list, "Provinces et voyages", UIKit.PanelLight, () => { ui.Close(this); ui.OpenProvinces(); });
            Add(list, "Garage", UIKit.PanelLight, () => { ui.Close(this); ui.OpenGarage(); });
            Add(list, "Magasin (livraison au garage)", UIKit.PanelLight, () => { ui.Close(this); ui.OpenShop(); });
            Add(list, "Paramètres", UIKit.PanelLight, () => ui.Open(ui.Settings));
            Add(list, "Sauvegarder", UIKit.PanelLight, () => { BalulaGame.Instance.Save(); ui.Toast("Partie sauvegardée."); });
            Add(list, "Menu principal", UIKit.PanelLight, () => BalulaGame.Instance.ReturnToMenu());
            Add(list, "Quitter le jeu", UIKit.Red, () => BalulaGame.Instance.Quit());
        }

        private static void Add(RectTransform list, string label, Color bg, UnityAction a)
        {
            Button b = UIKit.Button(list, label, bg, Color.white, 34, a);
            UIKit.Size(b, 92f);
        }

        public override void Refresh()
        {
            GameSession s = BalulaGame.Instance.Session;
            if (s == null) { info.text = ""; return; }
            PlayerProfile p = s.Profile;
            info.text = p.nomJoueur + " · Jour " + p.jour + " · " + TextUtil.Clock(p.minuteDuJour) + " · " + Cdf.Format(p.argent) + " · " + p.nbCourses + " courses";
        }
    }

    // =====================================================================
    // Paramètres
    // =====================================================================
    public class SettingsPanel : UIPanel
    {
        private Text lang, quality, volume, music, sensitivity, help, device;
        private static readonly string[] Languages = { "Francais", "Lingala", "Mixte" };

        protected override void Build()
        {
            Frame("Paramètres", 1300, 980, true);
            UIKit.VList(content, 14f, 10);
            lang = Line("Langue des dialogues", () => Cycle(-1), () => Cycle(1));
            quality = Line("Qualité graphique", () => { GameSettings.Quality = GameSettings.Quality - 1; Apply(); }, () => { GameSettings.Quality = GameSettings.Quality + 1; Apply(); });
            volume = Line("Volume général", () => { GameSettings.Volume = GameSettings.Volume - 0.1f; Apply(); }, () => { GameSettings.Volume = GameSettings.Volume + 0.1f; Apply(); });
            music = Line("Musique", () => { GameSettings.MusicVolume = GameSettings.MusicVolume - 0.1f; Apply(); }, () => { GameSettings.MusicVolume = GameSettings.MusicVolume + 0.1f; Apply(); });
            sensitivity = Line("Sensibilité caméra", () => { GameSettings.CameraSensitivity = GameSettings.CameraSensitivity - 0.2f; Apply(); }, () => { GameSettings.CameraSensitivity = GameSettings.CameraSensitivity + 0.2f; Apply(); });
            help = Line("Aide au démarrage", () => { GameSettings.ShowHelp = !GameSettings.ShowHelp; Apply(); }, () => { GameSettings.ShowHelp = !GameSettings.ShowHelp; Apply(); });
            Text note = UIKit.Label(content, "Langue : Français, Lingala, ou Mixte (chaque client parle sa langue). Musique : morceaux originaux du jeu (0 % = coupée). Les achats réels, s'ils sont ajoutés un jour, passeront uniquement par la boutique officielle de la plateforme (Google Play / App Store).", 22, UIKit.TextDim, TextAnchor.MiddleLeft);
            UIKit.Size(note, 96f);
            device = UIKit.Label(content, "", 20, UIKit.TextDim, TextAnchor.MiddleLeft);
            UIKit.Size(device, 60f);
        }

        private Text Line(string label, UnityAction minus, UnityAction plus)
        {
            RectTransform row = UIKit.Row(content, 96f, 16f);
            Text l = UIKit.Label(row, label, 30, UIKit.Text, TextAnchor.MiddleLeft);
            UIKit.Size(l, 96f, 420f);
            Button m = UIKit.Button(row, "−", UIKit.PanelLight, Color.white, 44, minus);
            UIKit.Size(m, 96f, 120f);
            Image box = UIKit.Box(row, "Valeur", UIKit.Bg);
            UIKit.Size(box, 96f, 420f);
            Text value = UIKit.Label(box.transform, "", 30, UIKit.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Stretch(value.rectTransform, 8, 4, 8, 4);
            Button p = UIKit.Button(row, "+", UIKit.PanelLight, Color.white, 44, plus);
            UIKit.Size(p, 96f, 120f);
            return value;
        }

        private void Cycle(int dir)
        {
            int i = System.Array.IndexOf(Languages, GameSettings.Language);
            if (i < 0) i = 2;
            GameSettings.Language = Languages[(i + dir + Languages.Length) % Languages.Length];
            Apply();
        }

        private void Apply()
        {
            GameSettings.Apply();
            BalulaGame g = BalulaGame.Instance;
            if (g != null)
            {
                if (g.Session != null) GameSettings.CopyTo(g.Session.Profile);
                if (g.Cam != null) g.Cam.Sensitivity = GameSettings.CameraSensitivity;
            }
            Refresh();
        }

        public override void Refresh()
        {
            string l = GameSettings.Language;
            lang.text = l == "Francais" ? "Français" : l;
            quality.text = GameSettings.QualityLabel(GameSettings.Quality);
            music.text = GameSettings.MusicVolume <= 0.001f ? "Coupée" : Mathf.RoundToInt(GameSettings.MusicVolume * 100f) + " %";
            device.text = "Appareil : " + SystemInfo.deviceModel + " — " + SystemInfo.graphicsDeviceName + " (" + SystemInfo.graphicsDeviceType + ") — " +
                          SystemInfo.systemMemorySize + " Mo — version " + Application.version + (MusicPlayer.Instance != null && !string.IsNullOrEmpty(MusicPlayer.Instance.NowPlaying) ? " — musique : " + MusicPlayer.Instance.NowPlaying : "");
            volume.text = Mathf.RoundToInt(GameSettings.Volume * 100f) + " %";
            sensitivity.text = GameSettings.CameraSensitivity.ToString("0.0");
            help.text = GameSettings.ShowHelp ? "Oui" : "Non";
        }
    }

    // =====================================================================
    // Crédits, sources et mentions
    // =====================================================================
    public class CreditsPanel : UIPanel
    {
        private Text body;

        protected override void Build()
        {
            Frame("Crédits et sources", 1500, 960, true);
            RectTransform list;
            RectTransform view = UIKit.ScrollList(content, out list);
            UIKit.Stretch(view, 0, 0, 0, 0);
            body = UIKit.Label(list, "", 25, UIKit.Text, TextAnchor.UpperLeft);
            body.verticalOverflow = VerticalWrapMode.Overflow;
            ContentSizeFitter f = body.gameObject.AddComponent<ContentSizeFitter>();
            f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        public override void Refresh()
        {
            BalulaGame g = BalulaGame.Instance;
            string map = g != null && g.World != null && !g.World.IsTestTrack ? g.World.Attribution : "Carte OpenStreetMap non importée (piste d'essai).";
            body.text =
                "<b>BALULA MOTO</b> — conception et production : <b>MATULA DESIGN</b> (Kinshasa, RDC).\n\n" +
                "<b>Cartes</b> : " + map + "\nDonnées OpenStreetMap © les contributeurs d'OpenStreetMap, licence ODbL (openstreetmap.org/copyright).\n\n" +
                "<b>Pont Maréchal (Matadi)</b> : maquette de jeu. Longueur 722 m et travée centrale 520 m d'après des sources publiques ; hauteurs, largeur et relief estimés, à vérifier.\n\n" +
                "<b>Photos de référence</b> : fournies par le créateur (bibliothèque REF-KIN, REF-KCE). Les photos portant un filigrane (CoinAfrique, CnK, Radio Okapi, comptes de réseaux sociaux) restent la propriété de leurs auteurs : elles servent uniquement de référence de modélisation et ne sont pas intégrées au jeu.\n\n" +
                "<b>Personnages 3D</b> : avatars et animations « Microsoft Rocketbox » (© 2020 Microsoft, licence MIT — github.com/microsoft/Microsoft-Rocketbox), textures réduites pour téléphone et teint africain pour tous les personnages (peau des textures assombrie) ; foulards, bassines, casques et accessoires créés pour le jeu.\n\n" +
                "<b>Bus TRANSCO, tricycle et policiers</b> : modèle 3D du bus fourni par MATULA DESIGN ; tricycle (type Bajaj RE) modélisé pour le jeu d'après les photos du créateur ; policiers : avatar Microsoft Rocketbox (licence MIT) habillé d'après la photo du créateur (tenue, béret), fusil modélisé pour le jeu (designs commerciaux respectés, sans logo ni insigne officiel, sans partenariat avec TRANSCO, Bajaj ni la Police nationale) ; murs de parcelles, berges et eau des rivières dessinés pour le jeu d'après les photos du créateur (aucune photo copiée).\n\n" +
                "<b>Monuments</b> : modèles 3D du Stade des Martyrs et de la Tour de l'Échangeur fournis par MATULA DESIGN, posés aux emplacements indiqués par le créateur ; terrains clôturés, arbres, poteaux électriques, lampadaires et plaques de rues générés par le jeu (noms des rues : OpenStreetMap).\n\n" +
                "<b>Affiches publicitaires, stations-service et garage</b> : marques fictives (Unités Mbote, Mayi Malamu, Banque Lisanga, carburant « BALULA ÉNERGIE »…) et textures créées pour le jeu ; toute ressemblance avec une marque réelle serait fortuite. Les parcelles, boutiques et immeubles placés entre les rues sont génériques et ne représentent pas des bâtiments réels.\n\n" +
                "<b>Décors et véhicules fournis en novembre 2026</b> : maison « cottage », maison « fhouse », cinq immeubles, barrières de séparation (repeintes en rouge et blanc), moto centrale (« moto1 »), Honda CB750 et Honda classique : fichiers 3D fournis par MATULA DESIGN, convertis et allégés pour le téléphone (licences des fichiers d'origine à vérifier par le créateur). Mobilier de rue d'après « Source Engine Street Objects » de Leafia dev (licence CC-BY-4.0). Voitures de la circulation : trois modèles génériques dessinés par le jeu, sans logo ni marque. Étals des vendeurs, parkings et poussière créés pour le jeu.\n\n" +
                "<b>Lieux du créateur</b> : positions issues des liens Google Maps fournis par le créateur (coordonnées uniquement, aucune image reprise).\n\n" +
                "<b>Véhicules</b> : les modèles sont inspirés de véhicules réels du marché congolais. Les noms de marques (TVS, Bajaj, Honda, Yamaha, Haojue…) appartiennent à leurs propriétaires ; ce jeu n'est ni affilié ni approuvé par eux. Les caractéristiques indiquées « à vérifier » sont des estimations.\n\n" +
                "<b>Police et amendes</b> : situations et montants fictifs, à but ludique. Le jeu ne représente pas de corruption et n'encourage aucune infraction.\n\n" +
                "<b>Musique</b> : quatre morceaux instrumentaux originaux composés pour le jeu (« Sebene ya Balula », « Nzela Malamu », « Butu ya Kin », « Ndombolo ya Moto »), entièrement générés par programme (guitares, basse, batterie, congas de synthèse) : aucun échantillon, aucune chanson existante, aucune musique commerciale.\n\n" +
                "<b>Sons</b> : moteurs, klaxons et ambiance de rue synthétisés par le jeu.\n\n" +
                "<b>Économie</b> : prix en francs congolais (FC). Taux et prix de référence datés dans les fichiers de données ; ils évoluent et doivent être mis à jour.\n\n" +
                "<b>Achats</b> : aucun paiement réel dans cette version. Tout achat futur passera par la boutique officielle de la plateforme ; le jeu ne demande jamais de carte bancaire.";
        }
    }

    // =====================================================================
    // Confirmation
    // =====================================================================
    public class ConfirmPanel : UIPanel
    {
        private Text message;
        private Button yes;
        private System.Action onYes;

        protected override void Build()
        {
            Frame("Confirmation", 1100, 600, false);
            Closable = true;
            message = UIKit.Label(content, "", 30, UIKit.Text, TextAnchor.MiddleCenter);
            UIKit.Stretch(message.rectTransform, 10, 0, 10, 130);
            Button no = UIKit.Button(content, "Annuler", UIKit.PanelLight, Color.white, 32, () => ui.Close(this));
            UIKit.Place((RectTransform)no.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(20f, 10f), new Vector2(420f, 100f));
            yes = UIKit.Button(content, "Oui", UIKit.Accent, Color.black, 32, () =>
            {
                System.Action a = onYes;
                ui.Close(this);
                if (a != null) a();
            });
            UIKit.Place((RectTransform)yes.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-20f, 10f), new Vector2(420f, 100f));
        }

        public void Setup(string title, string text, string yesLabel, System.Action action)
        {
            if (titleText != null) titleText.text = title;
            message.text = text;
            UIKit.SetButtonText(yes, yesLabel);
            onYes = action;
        }
    }
}
