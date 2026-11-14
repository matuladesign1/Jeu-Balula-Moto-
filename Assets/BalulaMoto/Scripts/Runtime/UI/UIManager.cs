// BALULA MOTO — gestionnaire d'interface : canevas, zone sûre (encoches), panneaux, notifications, bulles de dialogue.
using System.Collections.Generic;
using BalulaMoto.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BalulaMoto.Runtime
{
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance;

        public Canvas Canvas;
        public RectTransform SafeRoot;
        public HudView Hud;

        public MainMenuPanel MainMenu;
        public NewGamePanel NewGame;
        public PausePanel Pause;
        public SettingsPanel Settings;
        public CreditsPanel Credits;
        public MissionsPanel Missions;
        public FinancesPanel Finances;
        public ProvincesPanel Provinces;
        public ShopPanel Shop;
        public GaragePanel Garage;
        public NegotiationPanel Negotiation;
        public RideSummaryPanel RideSummary;
        public FuelPanel Fuel;
        public WorkshopPanel Workshop;
        public EmptyTankPanel EmptyTank;
        public PolicePanel Police;
        public ConfirmPanel Confirm;
        public MapPanel Map;
        public DriverPanel Driver;
        public ShowroomPanel Showroom;

        private readonly List<UIPanel> openModals = new List<UIPanel>();
        private readonly List<UIPanel> all = new List<UIPanel>();
        private RectTransform toastRoot;
        private readonly List<Text> toasts = new List<Text>();
        private readonly List<float> toastTimes = new List<float>();
        private Image commentBox;
        private Text commentText;
        private float commentTimer;
        private Rect lastSafeArea;
        public int LastEscapeFrame = -1;

        public bool AnyModalOpen { get { return openModals.Count > 0; } }
        public GameSession Session { get { return BalulaGame.Instance != null ? BalulaGame.Instance.Session : null; } }
        public GameplayDirector Director { get { return GameplayDirector.Instance; } }

        public static UIManager Create()
        {
            GameObject g = new GameObject("Interface");
            Canvas canvas = g.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            CanvasScaler scaler = g.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.6f;
            g.AddComponent<GraphicRaycaster>();
            UIManager m = g.AddComponent<UIManager>();
            m.Canvas = canvas;
            Instance = m;
            m.Build();
            return m;
        }

        private void Build()
        {
            SafeRoot = UIKit.Rect(transform, "ZoneSure");
            UIKit.Stretch(SafeRoot, 0, 0, 0, 0);
            ApplySafeArea();

            GameObject hudGo = UIKit.Rect(SafeRoot, "HUD").gameObject;
            UIKit.Stretch((RectTransform)hudGo.transform, 0, 0, 0, 0);
            Hud = hudGo.AddComponent<HudView>();
            Hud.Build(this);
            hudGo.SetActive(false);

            MainMenu = Add(new MainMenuPanel());
            NewGame = Add(new NewGamePanel());
            Pause = Add(new PausePanel());
            Settings = Add(new SettingsPanel());
            Credits = Add(new CreditsPanel());
            Missions = Add(new MissionsPanel());
            Finances = Add(new FinancesPanel());
            Provinces = Add(new ProvincesPanel());
            Shop = Add(new ShopPanel());
            Garage = Add(new GaragePanel());
            Negotiation = Add(new NegotiationPanel());
            RideSummary = Add(new RideSummaryPanel());
            Fuel = Add(new FuelPanel());
            Workshop = Add(new WorkshopPanel());
            EmptyTank = Add(new EmptyTankPanel());
            Police = Add(new PolicePanel());
            Confirm = Add(new ConfirmPanel());
            Map = Add(new MapPanel());
            Driver = Add(new DriverPanel());
            Showroom = Add(new ShowroomPanel());

            // Bulle de dialogue (client, police, vendeurs)
            commentBox = UIKit.Box(SafeRoot, "Bulle", new Color(0.05f, 0.06f, 0.1f, 0.88f));
            UIKit.Place(commentBox.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 330f), new Vector2(900f, 120f));
            commentText = UIKit.Label(commentBox.transform, "", 30, UIKit.Text, TextAnchor.MiddleCenter);
            UIKit.Stretch(commentText.rectTransform, 24, 8, 24, 8);
            commentBox.raycastTarget = false;
            commentBox.gameObject.SetActive(false);

            toastRoot = UIKit.Rect(SafeRoot, "Notifications");
            UIKit.Place(toastRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -210f), new Vector2(900f, 400f));
            VerticalLayoutGroup v = UIKit.VList(toastRoot, 8f, 0);
            v.childAlignment = TextAnchor.UpperCenter;
        }

        private T Add<T>(T panel) where T : UIPanel
        {
            panel.Init(this, SafeRoot);
            all.Add(panel);
            return panel;
        }

        private void ApplySafeArea()
        {
            Rect sa = Screen.safeArea;
            if (sa == lastSafeArea || Screen.width <= 0 || Screen.height <= 0) return;
            lastSafeArea = sa;
            SafeRoot.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
            SafeRoot.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
            SafeRoot.offsetMin = Vector2.zero;
            SafeRoot.offsetMax = Vector2.zero;
        }

        // ---------------- Ouverture / fermeture ----------------

        public void Open(UIPanel p)
        {
            if (p == null) return;
            p.Root.gameObject.SetActive(true);
            p.Root.SetAsLastSibling();
            commentBox.transform.SetAsLastSibling();
            toastRoot.SetAsLastSibling();
            p.Refresh();
            if (!p.IsOpen) { UpdateTimeScale(); return; }
            if (p.Modal && !openModals.Contains(p)) openModals.Add(p);
            PlayerControls.ResetTouch();
            UpdateTimeScale();
        }

        public void Close(UIPanel p)
        {
            if (p == null || !p.IsOpen) return;
            p.Root.gameObject.SetActive(false);
            openModals.Remove(p);
            p.OnClose();
            UpdateTimeScale();
        }

        public void CloseAllModals()
        {
            for (int i = openModals.Count - 1; i >= 0; i--) Close(openModals[i]);
        }

        private void UpdateTimeScale()
        {
            bool playing = BalulaGame.Instance != null && BalulaGame.Instance.Playing;
            Time.timeScale = playing && openModals.Count == 0 ? 1f : 0f;
        }

        public void ShowHud(bool show) { Hud.gameObject.SetActive(show); }

        public void ShowMainMenu()
        {
            CloseAllModals();
            ShowHud(false);
            Open(MainMenu);
        }

        // ---------------- Raccourcis utilisés par le jeu ----------------

        public void OpenPause() { if (!AnyModalOpen) Open(Pause); }
        public void OpenMissions() { Open(Missions); }
        public void OpenMap() { if (!Map.IsOpen) Open(Map); }
        public void OpenFinances() { Open(Finances); }
        public void OpenProvinces() { Open(Provinces); }
        public void OpenShop() { Open(Shop); }
        public void OpenGarage() { Open(Garage); }

        /// <summary>Garage du menu (plateau tournant) ; catalog = directement sur les modèles à acheter.</summary>
        public void OpenShowroom(bool catalog)
        {
            Showroom.StartOnCatalog = catalog;
            Open(Showroom);
        }
        public void OpenWorkshop() { Open(Workshop); }
        public void OpenEmptyTank() { if (!EmptyTank.IsOpen) Open(EmptyTank); }
        public void OpenNegotiation() { Open(Negotiation); }

        public void OpenFuel(string vendorId)
        {
            Fuel.VendorId = vendorId;
            Open(Fuel);
        }

        public void OpenPolice(string infractionId, bool fled)
        {
            Police.Setup(infractionId, fled);
            Open(Police);
        }

        public void OpenRideSummary(RideResult r, ClientRequest c, string line)
        {
            RideSummary.Setup(r, c, line);
            Open(RideSummary);
        }

        public void Ask(string title, string message, string yes, System.Action onYes)
        {
            Confirm.Setup(title, message, yes, onYes);
            Open(Confirm);
        }

        // ---------------- Notifications ----------------

        public void Toast(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            Image bg = UIKit.Box(toastRoot, "Notification", new Color(0.08f, 0.1f, 0.16f, 0.92f));
            UIKit.Size(bg, 64f);
            Text t = UIKit.Label(bg.transform, message, 28, UIKit.Text, TextAnchor.MiddleCenter);
            UIKit.Stretch(t.rectTransform, 16, 4, 16, 4);
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = 16;
            t.resizeTextMaxSize = 28;
            bg.raycastTarget = false;
            toasts.Add(t);
            toastTimes.Add(Time.unscaledTime + 4f);
            while (toasts.Count > 4) RemoveToast(0);
        }

        private void RemoveToast(int i)
        {
            if (toasts[i] != null) Destroy(toasts[i].transform.parent.gameObject);
            toasts.RemoveAt(i);
            toastTimes.RemoveAt(i);
        }

        public void ShowComment(string speaker, string text)
        {
            commentText.text = UIKit.Colored(speaker + " : ", UIKit.Accent) + text;
            commentBox.gameObject.SetActive(true);
            commentTimer = 4.5f;
        }

        private void Update()
        {
            ApplySafeArea();
            if (openModals.Count > 0 && PlayerControls.Pressed(GameKey.Menu))
            {
                UIPanel top = openModals[openModals.Count - 1];
                if (top.Closable) { Close(top); LastEscapeFrame = Time.frameCount; }
            }
            for (int i = toasts.Count - 1; i >= 0; i--)
                if (Time.unscaledTime > toastTimes[i]) RemoveToast(i);
            if (commentTimer > 0f)
            {
                commentTimer -= Time.unscaledDeltaTime;
                if (commentTimer <= 0f) commentBox.gameObject.SetActive(false);
            }
        }
    }
}
