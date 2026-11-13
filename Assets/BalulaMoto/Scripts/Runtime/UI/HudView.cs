// BALULA MOTO — HUD de conduite (disposition inspirée des simulateurs de tricycle fournis en exemple) :
// jauge de carburant en haut à gauche, pause en haut à droite, flèches de direction rondes en bas à gauche,
// pédales frein / accélérateur en bas à droite, klaxon rond à droite, compteur et bouton d'action au centre.
using System.Collections.Generic;
using System.Text;
using BalulaMoto.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BalulaMoto.Runtime
{
    public class HudView : MonoBehaviour
    {
        private UIManager ui;
        private Text moneyText, levelText, repText, clockText, rideText, speedText, gearText, fuelText, condText, warnText, missionsText, actionText, helpText, brakeText, brake2Text;
        private RectTransform xpFill, condFill, arrow;
        private readonly List<Image> fuelSegments = new List<Image>();
        private GameObject ridePanel, helpPanel;
        private Button actionButton;
        private Image lightsImg, indLeftImg, indRightImg, hazardImg;
        private bool steerLeft, steerRight, kindSet, lastMoto, helpDone;
        private float refreshTimer, helpTimer = 22f;

        private static readonly Color ControlBg = new Color(0.08f, 0.1f, 0.14f, 0.55f);
        private static readonly Color Pedal = new Color(0.98f, 0.82f, 0.12f, 0.92f);
        private static readonly Color PedalGas = new Color(0.55f, 0.85f, 0.18f, 0.92f);
        private static readonly Color Amber = new Color(1f, 0.6f, 0.05f);

        private static GameplayDirector G { get { return GameplayDirector.Instance; } }

        public void Build(UIManager manager)
        {
            ui = manager;
            RectTransform root = (RectTransform)transform;

            // Zone de glissement plein écran (derrière les commandes) : regarder autour / caméra libre
            Image drag = UIKit.Box(root, "ZoneCamera", new Color(0f, 0f, 0f, 0f), false);
            UIKit.Stretch(drag.rectTransform, 0, 0, 0, 0);
            DragZone dz = drag.gameObject.AddComponent<DragZone>();
            dz.Dragged = d => { if (G != null && G.Cam != null) G.Cam.AddOrbit(d * (1080f / Mathf.Max(1, Screen.height))); };
            dz.Scrolled = s => { if (G != null && G.Cam != null) G.Cam.Zoom(s * 1.5f); };

            BuildTopLeft(root);
            BuildTopCenter(root);
            BuildTopRight(root);
            BuildMissions(root);
            BuildSpeedo(root);
            BuildDriving(root);
            BuildHelp(root);
        }

        // ------------------------------------------------------------------
        // Construction
        // ------------------------------------------------------------------

        private void BuildTopLeft(RectTransform root)
        {
            Image info = UIKit.Box(root, "Infos", UIKit.Dark);
            info.raycastTarget = false;
            UIKit.Place(info.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -20f), new Vector2(480f, 200f));
            RectTransform r = info.rectTransform;

            // Jauge de carburant segmentée (comme dans les exemples)
            Text pump = UIKit.Label(r, "CARBURANT", 20, UIKit.TextDim, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIKit.TopLeft(pump.rectTransform, 20, 12, 150, 30);
            for (int i = 0; i < 12; i++)
            {
                Image seg = UIKit.Box(r, "Segment", new Color(1f, 1f, 1f, 0.15f), false);
                seg.raycastTarget = false;
                UIKit.TopLeft(seg.rectTransform, 170 + i * 24, 14, 18, 28);
                fuelSegments.Add(seg);
            }
            fuelText = UIKit.Label(r, "", 20, UIKit.Text, TextAnchor.MiddleRight);
            UIKit.TopLeft(fuelText.rectTransform, 170, 44, 290, 24);

            moneyText = UIKit.Label(r, "", 44, UIKit.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIKit.TopLeft(moneyText.rectTransform, 20, 66, 450, 56);
            levelText = UIKit.Label(r, "", 24, UIKit.Text, TextAnchor.MiddleLeft);
            UIKit.TopLeft(levelText.rectTransform, 20, 122, 450, 30);
            RectTransform bar = UIKit.Bar(r, new Color(1f, 1f, 1f, 0.12f), UIKit.Blue, out xpFill);
            UIKit.TopLeft(bar, 20, 154, 440, 10);
            repText = UIKit.Label(r, "", 22, UIKit.TextDim, TextAnchor.MiddleLeft);
            UIKit.TopLeft(repText.rectTransform, 20, 166, 450, 30);
        }

        private void BuildTopCenter(RectTransform root)
        {
            Image clock = UIKit.Box(root, "Horloge", UIKit.Dark);
            clock.raycastTarget = false;
            UIKit.Place(clock.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(620f, 60f));
            clockText = UIKit.Label(clock.transform, "", 28, UIKit.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Stretch(clockText.rectTransform, 10, 0, 10, 0);

            Image ride = UIKit.Box(root, "Course", new Color(0.45f, 0.32f, 0f, 0.88f));
            ride.raycastTarget = false;
            UIKit.Place(ride.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -88f), new Vector2(860f, 104f));
            ridePanel = ride.gameObject;
            Text arrowText = UIKit.Label(ride.transform, "▲", 70, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            arrow = arrowText.rectTransform;
            UIKit.Place(arrow, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(60f, 0f), new Vector2(90f, 90f));
            rideText = UIKit.Label(ride.transform, "", 26, Color.white, TextAnchor.MiddleLeft);
            UIKit.Stretch(rideText.rectTransform, 120, 6, 20, 6);
            ridePanel.SetActive(false);
        }

        private void BuildTopRight(RectTransform root)
        {
            RoundButton(root, "II", new Vector2(1f, 1f), new Vector2(-20f, -20f), 110f, UIKit.Dark, 46, () => ui.OpenPause());
            RoundButton(root, "Missions", new Vector2(1f, 1f), new Vector2(-140f, -26f), 98f, UIKit.Dark, 20, () => ui.OpenMissions());
            RoundButton(root, "Caméra", new Vector2(1f, 1f), new Vector2(-250f, -26f), 98f, UIKit.Dark, 20, () => { if (G != null) G.CycleCamera(); });
        }

        private void BuildMissions(RectTransform root)
        {
            Image mis = UIKit.Box(root, "SuiviMissions", new Color(0.03f, 0.03f, 0.05f, 0.5f));
            mis.raycastTarget = false;
            UIKit.Place(mis.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -232f), new Vector2(480f, 170f));
            missionsText = UIKit.Label(mis.transform, "", 21, UIKit.Text, TextAnchor.UpperLeft);
            UIKit.Stretch(missionsText.rectTransform, 18, 10, 14, 8);
        }

        private void BuildSpeedo(RectTransform root)
        {
            Image sp = UIKit.Box(root, "Compteur", UIKit.Dark);
            sp.raycastTarget = false;
            UIKit.Place(sp.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(600f, 150f));
            RectTransform r = sp.rectTransform;
            speedText = UIKit.Label(r, "0", 78, Color.white, TextAnchor.MiddleRight, FontStyle.Bold);
            UIKit.TopLeft(speedText.rectTransform, 8, 0, 200, 96);
            Text unit = UIKit.Label(r, "km/h", 24, UIKit.TextDim, TextAnchor.MiddleLeft);
            UIKit.TopLeft(unit.rectTransform, 214, 44, 90, 36);
            gearText = UIKit.Label(r, "", 24, UIKit.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIKit.TopLeft(gearText.rectTransform, 20, 100, 280, 40);
            condText = UIKit.Label(r, "", 22, UIKit.Text, TextAnchor.MiddleLeft);
            UIKit.TopLeft(condText.rectTransform, 320, 12, 264, 30);
            RectTransform cb = UIKit.Bar(r, new Color(1f, 1f, 1f, 0.12f), UIKit.Green, out condFill);
            UIKit.TopLeft(cb, 320, 46, 260, 16);
            warnText = UIKit.Label(r, "", 20, new Color(1f, 0.45f, 0.4f), TextAnchor.UpperLeft);
            UIKit.TopLeft(warnText.rectTransform, 320, 70, 270, 76);

            actionButton = UIKit.Button(root, "Action", UIKit.Accent, Color.black, 32, () => { if (G != null) G.OnAction(); });
            UIKit.Place((RectTransform)actionButton.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 176f), new Vector2(700f, 92f));
            actionText = actionButton.GetComponentInChildren<Text>();
            actionButton.gameObject.SetActive(false);
        }

        private void BuildDriving(RectTransform root)
        {
            // Direction : deux boutons ronds en bas à gauche
            Text t;
            TouchButton left = HoldDisc(root, "◄", new Vector2(0f, 0f), new Vector2(150f, 150f), 220f, out t);
            left.Changed = p => { steerLeft = p; UpdateSteer(); };
            TouchButton right = HoldDisc(root, "►", new Vector2(0f, 0f), new Vector2(400f, 150f), 220f, out t);
            right.Changed = p => { steerRight = p; UpdateSteer(); };

            // Clignotants, détresse, phares au-dessus de la direction
            indLeftImg = SmallButton(root, "◄ Clign.", new Vector2(30f, 290f), () => { if (G != null) G.ToggleIndicator(-1); });
            hazardImg = SmallButton(root, "Détresse", new Vector2(152f, 290f), () => { if (G != null) G.ToggleHazards(); });
            indRightImg = SmallButton(root, "Clign. ►", new Vector2(274f, 290f), () => { if (G != null) G.ToggleIndicator(1); });
            lightsImg = SmallButton(root, "Phares", new Vector2(396f, 290f), () => { if (G != null) G.ToggleLights(); });

            // Pédales en bas à droite : frein (large) puis accélérateur (haut)
            TouchButton gas = Pedal3D(root, "GAZ", new Vector2(-40f, 40f), new Vector2(150f, 300f), PedalGas, out t);
            gas.Changed = p => { PlayerControls.TouchThrottle = p ? 1f : 0f; };
            TouchButton brake = Pedal3D(root, "FREIN", new Vector2(-220f, 40f), new Vector2(230f, 170f), Pedal, out brakeText);
            brake.Changed = p => { PlayerControls.TouchBrake = p ? 1f : 0f; };
            TouchButton brake2 = Pedal3D(root, "FREIN AR", new Vector2(-220f, 230f), new Vector2(230f, 100f), new Color(0.95f, 0.6f, 0.15f, 0.9f), out brake2Text);
            brake2.Changed = p =>
            {
                if (G == null || G.Player == null) return;
                if (G.Player.Kind == VehicleKind.Moto) PlayerControls.TouchBrakeRear = p ? 1f : 0f;
                else if (p) G.ToggleReverse();
            };

            // Klaxon rond à droite
            Image hornImg = UIKit.Disc(root, "Klaxon", ControlBg);
            UIKit.Place(hornImg.rectTransform, new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-115f, 430f), new Vector2(130f, 130f));
            Text hl = UIKit.Label(hornImg.transform, "KLAXON", 22, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Stretch(hl.rectTransform, 6, 6, 6, 6);
            TouchButton horn = hornImg.gameObject.AddComponent<TouchButton>();
            horn.Changed = p => { PlayerControls.TouchHorn = p; };
        }

        private void BuildHelp(RectTransform root)
        {
            Image help = UIKit.Box(root, "Aide", new Color(0.03f, 0.04f, 0.07f, 0.82f));
            help.raycastTarget = false;
            UIKit.Place(help.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(1000f, 170f));
            helpText = UIKit.Label(help.transform,
                "Trouvez un client qui fait signe à un arrêt, arrêtez-vous près de lui et touchez le bouton jaune d'action.\n" +
                "Négociez le prix, puis suivez la flèche jusqu'à la destination. Surveillez le carburant et l'état du véhicule.",
                24, UIKit.Text, TextAnchor.MiddleCenter);
            UIKit.Stretch(helpText.rectTransform, 24, 10, 24, 10);
            helpPanel = help.gameObject;
        }

        // ------------------------------------------------------------------
        // Petits constructeurs
        // ------------------------------------------------------------------

        private static Button RoundButton(RectTransform root, string label, Vector2 anchor, Vector2 pos, float size, Color bg, int font, UnityEngine.Events.UnityAction onClick)
        {
            Image img = UIKit.Disc(root, "Bouton_" + label, bg);
            UIKit.Place(img.rectTransform, anchor, anchor, pos, new Vector2(size, size));
            Button b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            Text t = UIKit.Label(img.transform, label, font, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Stretch(t.rectTransform, 8, 8, 8, 8);
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = 12;
            t.resizeTextMaxSize = font;
            b.onClick.AddListener(onClick);
            return b;
        }

        private static TouchButton HoldDisc(RectTransform root, string label, Vector2 anchor, Vector2 center, float size, out Text text)
        {
            Image img = UIKit.Disc(root, "Direction_" + label, ControlBg);
            UIKit.Place(img.rectTransform, anchor, new Vector2(0.5f, 0.5f), center, new Vector2(size, size));
            Image ring = UIKit.Disc(img.transform, "Anneau", new Color(0.55f, 0.85f, 0.18f, 0.35f));
            ring.raycastTarget = false;
            UIKit.Stretch(ring.rectTransform, 18, 18, 18, 18);
            text = UIKit.Label(img.transform, label, 90, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Stretch(text.rectTransform, 0, 0, 0, 0);
            return img.gameObject.AddComponent<TouchButton>();
        }

        /// <summary>Pédale : plaque arrondie avec stries horizontales (frein large, accélérateur haut).</summary>
        private static TouchButton Pedal3D(RectTransform root, string label, Vector2 pos, Vector2 size, Color color, out Text text)
        {
            Image img = UIKit.Box(root, "Pedale_" + label, color);
            UIKit.Place(img.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), pos, size);
            int stripes = Mathf.Max(3, Mathf.RoundToInt(size.y / 34f));
            for (int i = 0; i < stripes; i++)
            {
                Image s = UIKit.Box(img.transform, "Strie", new Color(0f, 0f, 0f, 0.28f), false);
                s.raycastTarget = false;
                float h = size.y / (stripes * 2f + 1f);
                UIKit.Place(s.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, h * (1 + i * 2)), new Vector2(size.x * 0.72f, h));
            }
            text = UIKit.Label(img.transform, label, 26, Color.black, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Stretch(text.rectTransform, 4, 4, 4, 4);
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 12;
            text.resizeTextMaxSize = 26;
            return img.gameObject.AddComponent<TouchButton>();
        }

        private static Image SmallButton(RectTransform root, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick)
        {
            Button b = UIKit.Button(root, label, UIKit.PanelLight, Color.white, 20, onClick);
            UIKit.Place((RectTransform)b.transform, Vector2.zero, Vector2.zero, pos, new Vector2(112f, 70f));
            return b.GetComponent<Image>();
        }

        private void UpdateSteer()
        {
            PlayerControls.TouchSteer = (steerRight ? 1f : 0f) - (steerLeft ? 1f : 0f);
        }

        private void ConfigureKind(bool moto)
        {
            kindSet = true;
            lastMoto = moto;
            brakeText.text = moto ? "FREIN AV" : "FREIN";
            brake2Text.text = moto ? "FREIN AR" : "MARCHE AR.";
            PlayerControls.ResetTouch();
        }

        // ------------------------------------------------------------------
        // Mise à jour
        // ------------------------------------------------------------------

        private void Update()
        {
            GameplayDirector g = G;
            if (g == null || g.Session == null || g.Player == null) return;
            bool moto = g.Player.Kind == VehicleKind.Moto;
            if (!kindSet || moto != lastMoto) ConfigureKind(moto);
            UpdateArrow(g);
            if (!helpDone)
            {
                helpTimer -= Time.deltaTime;
                if (helpTimer <= 0f || g.State != RideState.Libre || !GameSettings.ShowHelp) { helpDone = true; helpPanel.SetActive(false); }
            }
            refreshTimer -= Time.unscaledDeltaTime;
            if (refreshTimer > 0f) return;
            refreshTimer = 0.12f;
            Refresh(g);
        }

        public void ResetHelp()
        {
            helpDone = false;
            helpTimer = 22f;
            if (helpPanel != null) helpPanel.SetActive(GameSettings.ShowHelp);
        }

        private void UpdateArrow(GameplayDirector g)
        {
            if (g.State != RideState.EnCourse || g.Client == null) return;
            Vector3 dest = WorldGeo.ToWorld(g.Client.Destination, 0f);
            Transform reference = g.Cam != null ? g.Cam.transform : g.Player.transform;
            Vector3 fwd = Vector3.ProjectOnPlane(reference.forward, Vector3.up);
            Vector3 dir = dest - g.Player.transform.position;
            dir.y = 0f;
            if (fwd.sqrMagnitude < 0.001f || dir.sqrMagnitude < 0.01f) return;
            float angle = Vector3.SignedAngle(fwd, dir, Vector3.up);
            arrow.localEulerAngles = new Vector3(0f, 0f, -angle);
        }

        private void Refresh(GameplayDirector g)
        {
            GameSession s = g.Session;
            PlayerProfile p = s.Profile;
            RaycastVehicle v = g.Player;
            OwnedVehicle ov = s.ActiveVehicle;

            moneyText.text = Cdf.Format(p.argent);
            levelText.text = "Niveau " + p.niveau + "   ·   " + (p.Mode == GameMode.Libre ? "Mode libre" : "Carrière") + "   ·   " + (g.World != null && g.World.ZoneAt(v.transform.position) == MatadiBuilder.Zone ? "Matadi" : g.World != null ? g.World.Name : "");
            UIKit.SetBar(xpFill, s.Career.LevelProgress01);
            repText.text = "Réputation " + TextUtil.Stars(p.Reputation()) + "  " + TextUtil.Rating(p.Reputation()) + "  (" + p.notes.Count + " avis)";

            string weather = g.Env != null ? WorldEnvironment.WeatherLabel(g.Env.Weather) : "";
            clockText.text = "Jour " + p.jour + "  ·  " + TextUtil.Clock(p.minuteDuJour) + "  ·  " + GameSession.PhaseLabel(s.Phase) +
                             (weather.Length > 0 ? "  ·  " + weather : "") + (s.IsRushHour ? "  ·  Heure de pointe" : "");

            // Course en cours
            if (g.State == RideState.EnCourse && g.Client != null)
            {
                if (!ridePanel.activeSelf) ridePanel.SetActive(true);
                ClientRequest c = g.Client;
                Vector3 d = WorldGeo.ToWorld(c.Destination, 0f) - v.transform.position;
                d.y = 0f;
                rideText.text = "<b>" + c.Name + "</b>  →  " + c.Destination.Name + "  (" + c.Destination.District + ")\n" +
                                "Prix convenu " + Cdf.Format(g.AgreedPrice) + "  ·  " + UIFormat.Distance(d.magnitude) +
                                (c.Passengers > 1 ? "  ·  " + c.Passengers + " passagers" : "") +
                                (c.CargoKg > 0f ? "  ·  " + c.CargoKg.ToString("0") + " kg" : "");
            }
            else if (ridePanel.activeSelf) ridePanel.SetActive(false);

            // Compteur
            speedText.text = Mathf.RoundToInt(Mathf.Abs(v.SpeedKmh)).ToString();
            if (v.Crashed) gearText.text = "Véhicule à terre";
            else if (v.EngineCut) gearText.text = v.PushMode ? "Vous poussez" : "Panne sèche";
            else if (v.ReverseGear) gearText.text = "Marche arrière";
            else gearText.text = v.Gear <= 0 ? "Point mort" : "Rapport " + v.Gear;

            if (ov != null)
            {
                float frac = s.Fuel.Fraction(ov, g.Stats);
                int lit = Mathf.CeilToInt(frac * fuelSegments.Count - 0.01f);
                for (int i = 0; i < fuelSegments.Count; i++)
                {
                    float t = i / (float)(fuelSegments.Count - 1);
                    Color c = t < 0.25f ? UIKit.Red : (t < 0.5f ? new Color(1f, 0.75f, 0.1f) : new Color(0.45f, 0.85f, 0.2f));
                    fuelSegments[i].color = i < lit ? c : new Color(1f, 1f, 1f, 0.13f);
                }
                fuelText.text = ov.carburantL.ToString("0.0").Replace('.', ',') + " L / " + (g.Stats != null ? g.Stats.TankL.ToString("0") : "?") + " L";
                float cond = ov.OverallCondition();
                condText.text = "État du véhicule  " + cond.ToString("0") + " %";
                UIKit.SetBar(condFill, cond / 100f);
                condFill.GetComponent<Image>().color = cond < 40f ? UIKit.Red : (cond < 65f ? Amber : UIKit.Green);
                List<string> w = VehicleWear.Warnings(ov, s.Db.Economy);
                if (!s.Police.DocumentsValid) w.Insert(0, "Documents expirés");
                if (p.rechercheParPolice) w.Insert(0, "Recherché par la police");
                warnText.text = string.Join(" · ", w.ToArray());
            }

            // Bouton d'action contextuel
            bool show = !string.IsNullOrEmpty(g.ActionLabel);
            if (actionButton.gameObject.activeSelf != show) actionButton.gameObject.SetActive(show);
            if (show) actionText.text = g.ActionLabel;

            // Voyants
            bool blink = Mathf.Repeat(Time.unscaledTime, 0.8f) < 0.4f;
            lightsImg.color = v.HeadlightsOn ? UIKit.Accent : UIKit.PanelLight;
            indLeftImg.color = (v.Indicator == -1 || v.Indicator == 2) && blink ? Amber : UIKit.PanelLight;
            indRightImg.color = (v.Indicator == 1 || v.Indicator == 2) && blink ? Amber : UIKit.PanelLight;
            hazardImg.color = v.Indicator == 2 ? UIKit.Red : UIKit.PanelLight;

            // Missions actives
            List<MissionProgress> act = s.Missions.Active();
            if (act.Count == 0) missionsText.text = UIKit.Colored("Aucune mission active", UIKit.TextDim) + "\nTouchez « Missions » pour en accepter.";
            else
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(UIKit.Colored("MISSIONS", UIKit.Accent)).Append('\n');
                foreach (MissionProgress mp in act)
                {
                    MissionDef m = s.Db.Mission(mp.id);
                    if (m == null) continue;
                    sb.Append("• ").Append(m.titre).Append("  ").Append(UIKit.Colored(UIFormat.MissionProgressText(m, mp), UIKit.TextDim)).Append('\n');
                }
                missionsText.text = sb.ToString();
            }
        }
    }
}
