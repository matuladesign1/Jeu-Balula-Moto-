// BALULA MOTO — écrans de menu : accueil, nouvelle partie, pause, paramètres, crédits, confirmation.
using System.Collections.Generic;
using BalulaMoto.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace BalulaMoto.Runtime
{
    // =====================================================================
    // Menu principal
    // =====================================================================
    public class MainMenuPanel : UIPanel
    {
        private Button continueButton;
        private Text savesText, worldText;
        private RawImage logo;

        protected override void Build()
        {
            Closable = false;
            Image bg = UIKit.Box(Root, "Fond", new Color(0.02f, 0.03f, 0.06f, 0.72f), false);
            UIKit.Stretch(bg.rectTransform, 0, 0, 0, 0);

            // Colonne gauche : logo et titre
            RectTransform left = UIKit.Rect(Root, "Identite");
            left.anchorMin = new Vector2(0f, 0f);
            left.anchorMax = new Vector2(0.5f, 1f);
            left.offsetMin = new Vector2(60f, 40f);
            left.offsetMax = new Vector2(-20f, -40f);
            logo = new GameObject("Logo", typeof(RectTransform)).AddComponent<RawImage>();
            logo.transform.SetParent(left, false);
            logo.raycastTarget = false;
            UIKit.Place(logo.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(420f, 420f));
            Text title = UIKit.Label(left, "BALULA MOTO", 92, UIKit.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -440f), new Vector2(900f, 110f));
            Text sub = UIKit.Label(left, "Taxi-moto & tricycle — République démocratique du Congo\nMATULA DESIGN", 30, UIKit.Text, TextAnchor.MiddleCenter);
            UIKit.Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -560f), new Vector2(900f, 100f));
            savesText = UIKit.Label(left, "", 24, UIKit.TextDim, TextAnchor.UpperCenter);
            UIKit.Place(savesText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(900f, 80f));
            worldText = UIKit.Label(left, "", 22, UIKit.TextDim, TextAnchor.LowerCenter);
            UIKit.Place(worldText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(900f, 110f));

            // Colonne droite : boutons
            Image col = UIKit.Box(Root, "Boutons", new Color(0.08f, 0.1f, 0.15f, 0.9f));
            col.rectTransform.anchorMin = new Vector2(0.55f, 0f);
            col.rectTransform.anchorMax = new Vector2(1f, 1f);
            col.rectTransform.offsetMin = new Vector2(20f, 40f);
            col.rectTransform.offsetMax = new Vector2(-60f, -40f);
            RectTransform listContent;
            RectTransform view = UIKit.ScrollList(col.transform, out listContent);
            UIKit.Stretch(view, 24, 24, 24, 24);

            Add(listContent, "Nouvelle partie", UIKit.Accent, Color.black, () => { ui.NewGame.Mode = GameMode.Carriere; ui.Open(ui.NewGame); });
            continueButton = Add(listContent, "Continuer", UIKit.Green, Color.white, () => BalulaGame.Instance.ContinueLast());
            Add(listContent, "Mode carrière", UIKit.PanelLight, Color.white, () => BalulaGame.Instance.PlayMode(GameMode.Carriere));
            Add(listContent, "Mode libre", UIKit.PanelLight, Color.white, () => BalulaGame.Instance.PlayMode(GameMode.Libre));
            Add(listContent, "Missions", UIKit.PanelLight, Color.white, () => RequireGame(() => ui.OpenMissions()));
            Add(listContent, "Garage", UIKit.PanelLight, Color.white, () => RequireGame(() => ui.OpenGarage()));
            Add(listContent, "Magasin", UIKit.PanelLight, Color.white, () => RequireGame(() => ui.OpenShop()));
            Add(listContent, "Paramètres", UIKit.PanelLight, Color.white, () => ui.Open(ui.Settings));
            Add(listContent, "Crédits et sources", UIKit.PanelLight, Color.white, () => ui.Open(ui.Credits));
            Add(listContent, "Quitter", UIKit.Red, Color.white, () => BalulaGame.Instance.Quit());
        }

        private static Button Add(RectTransform list, string label, Color bg, Color fg, UnityAction a)
        {
            Button b = UIKit.Button(list, label, bg, fg, 38, a);
            UIKit.Size(b, 104f);
            return b;
        }

        private void RequireGame(System.Action then)
        {
            BalulaGame g = BalulaGame.Instance;
            if (g.Session == null)
            {
                if (!SaveService.Exists(SaveService.LastSlot)) { ui.Toast("Commencez d'abord une nouvelle partie."); return; }
                g.ContinueLast();
            }
            if (g.Session != null) then();
        }

        public override void Refresh()
        {
            BalulaGame g = BalulaGame.Instance;
            if (logo != null && g != null && g.Logo != null) logo.texture = g.Logo;
            if (logo != null) logo.gameObject.SetActive(logo.texture != null);
            continueButton.interactable = SaveService.Exists(SaveService.LastSlot);
            savesText.text = "Carrière : " + SaveService.Summary(SaveService.SlotCareer) + "\nLibre : " + SaveService.Summary(SaveService.SlotFree);
            if (g != null && g.World != null)
            {
                worldText.text = g.World.IsTestTrack
                    ? "Monde actuel : piste d'essai (ce n'est pas Kinshasa). La carte réelle de Kinshasa se génère dans l'éditeur Unity avec l'import OpenStreetMap."
                    : "Monde actuel : " + g.World.Name + "\n" + g.World.Attribution;
                if (g.World.HasMatadi) worldText.text += "\nZone Matadi : maquette jouable du Pont Maréchal (province Kongo Central).";
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
        private Text lang, quality, volume, sensitivity, help;
        private static readonly string[] Languages = { "Francais", "Lingala", "Mixte" };

        protected override void Build()
        {
            Frame("Paramètres", 1300, 860, true);
            UIKit.VList(content, 18f, 10);
            lang = Line("Langue des dialogues", () => Cycle(-1), () => Cycle(1));
            quality = Line("Qualité graphique", () => { GameSettings.Quality = GameSettings.Quality - 1; Apply(); }, () => { GameSettings.Quality = GameSettings.Quality + 1; Apply(); });
            volume = Line("Volume", () => { GameSettings.Volume = GameSettings.Volume - 0.1f; Apply(); }, () => { GameSettings.Volume = GameSettings.Volume + 0.1f; Apply(); });
            sensitivity = Line("Sensibilité caméra", () => { GameSettings.CameraSensitivity = GameSettings.CameraSensitivity - 0.2f; Apply(); }, () => { GameSettings.CameraSensitivity = GameSettings.CameraSensitivity + 0.2f; Apply(); });
            help = Line("Aide au démarrage", () => { GameSettings.ShowHelp = !GameSettings.ShowHelp; Apply(); }, () => { GameSettings.ShowHelp = !GameSettings.ShowHelp; Apply(); });
            Text note = UIKit.Label(content, "Langue : Français, Lingala, ou Mixte (chaque client parle sa langue). Les achats réels, s'ils sont ajoutés un jour, passeront uniquement par la boutique officielle de la plateforme (Google Play / App Store).", 22, UIKit.TextDim, TextAnchor.MiddleLeft);
            UIKit.Size(note, 110f);
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
                "<b>Personnages 3D</b> : avatars et animations « Microsoft Rocketbox » (© 2020 Microsoft, licence MIT — github.com/microsoft/Microsoft-Rocketbox), textures réduites pour téléphone ; foulards, bassines, casques et accessoires créés pour le jeu.\n\n" +
                "<b>Véhicules</b> : les modèles sont inspirés de véhicules réels du marché congolais. Les noms de marques (TVS, Bajaj, Honda, Yamaha, Haojue…) appartiennent à leurs propriétaires ; ce jeu n'est ni affilié ni approuvé par eux. Les caractéristiques indiquées « à vérifier » sont des estimations.\n\n" +
                "<b>Police et amendes</b> : situations et montants fictifs, à but ludique. Le jeu ne représente pas de corruption et n'encourage aucune infraction.\n\n" +
                "<b>Sons</b> : sons de synthèse générés par le jeu. Aucune musique commerciale n'est utilisée.\n\n" +
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
