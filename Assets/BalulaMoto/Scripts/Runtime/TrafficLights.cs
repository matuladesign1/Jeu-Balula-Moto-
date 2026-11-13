// BALULA MOTO — feux tricolores fonctionnels et robot roulage synchronisé (référence REF-KIN-014).
using System.Collections.Generic;
using UnityEngine;

namespace BalulaMoto.Runtime
{
    public enum LightAxis { NordSud, EstOuest }
    public enum LightColor { Rouge, Orange, Vert }

    public class StopLine
    {
        public Vector3 Center;
        public Vector3 Travel;     // sens de circulation qui doit s'arrêter avant la ligne
        public float HalfWidth;
        public LightAxis Axis;
    }

    public class TrafficLightController : MonoBehaviour
    {
        public float GreenTime = 16f, AmberTime = 3f, AllRedTime = 1.5f;
        public readonly List<StopLine> StopLines = new List<StopLine>();
        private readonly List<TrafficLightHead> heads = new List<TrafficLightHead>();
        private float clock;

        public LightAxis GreenAxis { get; private set; }
        public float PhaseProgress { get; private set; }

        public LightColor ColorFor(LightAxis axis)
        {
            float cycle = GreenTime + AmberTime + AllRedTime;
            float t = clock % (cycle * 2f);
            LightAxis active = t < cycle ? LightAxis.NordSud : LightAxis.EstOuest;
            float local = t < cycle ? t : t - cycle;
            GreenAxis = active;
            PhaseProgress = local / cycle;
            if (axis != active) return LightColor.Rouge;
            if (local < GreenTime) return LightColor.Vert;
            if (local < GreenTime + AmberTime) return LightColor.Orange;
            return LightColor.Rouge;
        }

        public bool CanGo(LightAxis axis) { return ColorFor(axis) == LightColor.Vert; }

        public void Register(TrafficLightHead head) { heads.Add(head); }

        public void AddStopLine(Vector3 center, Vector3 travel, float halfWidth, LightAxis axis)
        {
            StopLines.Add(new StopLine { Center = center, Travel = travel.normalized, HalfWidth = halfWidth, Axis = axis });
        }

        /// <summary>Vrai si le segment prev→cur franchit une ligne d'arrêt au rouge, dans le sens concerné.</summary>
        public bool CrossedOnRed(Vector3 prev, Vector3 cur)
        {
            foreach (StopLine s in StopLines)
            {
                float a = Vector3.Dot(prev - s.Center, s.Travel);
                float b = Vector3.Dot(cur - s.Center, s.Travel);
                if (a < 0f && b >= 0f)
                {
                    Vector3 side = Vector3.Cross(Vector3.up, s.Travel);
                    if (Mathf.Abs(Vector3.Dot(cur - s.Center, side)) <= s.HalfWidth && ColorFor(s.Axis) == LightColor.Rouge) return true;
                }
            }
            return false;
        }

        private void Update()
        {
            clock += Time.deltaTime;
            LightColor ns = ColorFor(LightAxis.NordSud);
            LightColor eo = ColorFor(LightAxis.EstOuest);
            foreach (TrafficLightHead h in heads) h.Show(h.Axis == LightAxis.NordSud ? ns : eo);
        }
    }

    public class TrafficLightHead : MonoBehaviour
    {
        public LightAxis Axis;
        public Renderer Red, Amber, Green;
        private LightColor shown = (LightColor)(-1);

        public void Show(LightColor c)
        {
            if (c == shown) return;
            shown = c;
            Mats.SetLamp(Red, new Color(1f, 0.1f, 0.05f), c == LightColor.Rouge, 3f);
            Mats.SetLamp(Amber, new Color(1f, 0.6f, 0.05f), c == LightColor.Orange, 3f);
            Mats.SetLamp(Green, new Color(0.1f, 1f, 0.3f), c == LightColor.Vert, 3f);
        }

        /// <summary>Poteau orange à potence avec tête de feux (référence REF-KIN-009).</summary>
        public static TrafficLightHead Build(Transform parent, Vector3 pos, float yaw, LightAxis axis, TrafficLightController ctrl, float armLength)
        {
            GameObject root = new GameObject("Feu_" + axis);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = pos;
            root.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Color orange = new Color(0.92f, 0.45f, 0.08f);
            Prims.Box(root.transform, "Mat", new Vector3(0f, 2.8f, 0f), new Vector3(0.18f, 5.6f, 0.18f), orange, true);
            Prims.Box(root.transform, "Socle", new Vector3(0f, 0.3f, 0f), new Vector3(0.5f, 0.6f, 0.5f), new Color(0.15f, 0.15f, 0.15f));
            Prims.Box(root.transform, "Potence", new Vector3(-armLength * 0.5f, 5.4f, 0f), new Vector3(armLength, 0.14f, 0.14f), orange);
            TrafficLightHead head = root.AddComponent<TrafficLightHead>();
            head.Axis = axis;
            Vector3 hp = new Vector3(-armLength + 0.3f, 4.75f, 0f);
            Prims.Box(root.transform, "Boitier", hp, new Vector3(0.42f, 1.15f, 0.3f), new Color(0.1f, 0.1f, 0.12f));
            head.Red = Prims.Ball(root.transform, "Rouge", hp + new Vector3(0f, 0.36f, 0.16f), new Vector3(0.24f, 0.24f, 0.08f), Color.black).GetComponent<Renderer>();
            head.Amber = Prims.Ball(root.transform, "Orange", hp + new Vector3(0f, 0f, 0.16f), new Vector3(0.24f, 0.24f, 0.08f), Color.black).GetComponent<Renderer>();
            head.Green = Prims.Ball(root.transform, "Vert", hp + new Vector3(0f, -0.36f, 0.16f), new Vector3(0.24f, 0.24f, 0.08f), Color.black).GetComponent<Renderer>();
            ctrl.Register(head);
            return head;
        }
    }

    /// <summary>
    /// Robot roulage : pivote vers l'axe qui a le vert, lève les bras pour arrêter l'autre axe,
    /// et son torse affiche la couleur du feu (comme les robots installés à Kinshasa).
    /// </summary>
    public class RobotRoulage : MonoBehaviour
    {
        public TrafficLightController Controller;
        public Transform Torso, ArmLeft, ArmRight;
        public Renderer Chest;
        private LightColor last = (LightColor)(-1);

        public static RobotRoulage Build(Transform parent, Vector3 pos, TrafficLightController ctrl)
        {
            GameObject root = new GameObject("RobotRoulage");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = pos;
            Transform t = root.transform;
            Color red = new Color(0.8f, 0.08f, 0.08f), white = new Color(0.95f, 0.95f, 0.95f);
            Color metal = new Color(0.62f, 0.64f, 0.67f);
            // Socle tronconique à chevrons rouge et blanc
            for (int i = 0; i < 6; i++)
                Prims.Box(t, "Socle" + i, new Vector3(0f, 0.18f + i * 0.32f, 0f), new Vector3(2.0f - i * 0.12f, 0.32f, 2.0f - i * 0.12f), i % 2 == 0 ? red : white, true);
            // Mâts rayés et auvent
            for (int k = 0; k < 4; k++)
            {
                float sx = k % 2 == 0 ? -0.7f : 0.7f, sz = k < 2 ? -0.7f : 0.7f;
                for (int s = 0; s < 8; s++)
                    Prims.Box(t, "Mat", new Vector3(sx, 2.15f + s * 0.45f, sz), new Vector3(0.09f, 0.45f, 0.09f), s % 2 == 0 ? red : white);
            }
            Prims.Box(t, "Auvent", new Vector3(0f, 5.85f, 0f), new Vector3(1.8f, 0.08f, 1.8f), new Color(0.85f, 0.85f, 0.88f));
            Prims.Box(t, "PanneauSolaire", new Vector3(0f, 5.95f, 0f), new Vector3(1.4f, 0.04f, 1.0f), new Color(0.1f, 0.15f, 0.3f));
            RobotRoulage robot = root.AddComponent<RobotRoulage>();
            robot.Controller = ctrl;
            GameObject torso = new GameObject("Torse");
            torso.transform.SetParent(t, false);
            torso.transform.localPosition = new Vector3(0f, 2.1f, 0f);
            robot.Torso = torso.transform;
            Transform tr = torso.transform;
            Prims.Box(tr, "Jambes", new Vector3(0f, 0.7f, 0f), new Vector3(0.6f, 1.4f, 0.4f), metal, false, 0.7f);
            Prims.Box(tr, "Corps", new Vector3(0f, 1.95f, 0f), new Vector3(0.8f, 1.1f, 0.5f), metal, false, 0.7f);
            robot.Chest = Prims.Box(tr, "Ecran", new Vector3(0f, 2.0f, 0.26f), new Vector3(0.4f, 0.3f, 0.02f), Color.black).GetComponent<Renderer>();
            // Écharpe aux couleurs du drapeau de la RDC (bleu ciel, diagonale rouge bordée de jaune)
            Prims.BoxRot(tr, "Echarpe", new Vector3(0f, 1.8f, 0f), new Vector3(0.84f, 0.55f, 0.54f), new Vector3(0f, 0f, 0f), new Color(0f, 0.5f, 1f));
            Prims.BoxRot(tr, "EcharpeRouge", new Vector3(0f, 1.8f, 0.275f), new Vector3(0.95f, 0.1f, 0.01f), new Vector3(0f, 0f, 30f), new Color(0.85f, 0.1f, 0.1f));
            Prims.Box(tr, "Etoile", new Vector3(-0.25f, 1.98f, 0.276f), new Vector3(0.1f, 0.1f, 0.01f), new Color(0.98f, 0.85f, 0.1f));
            Prims.Box(tr, "Tete", new Vector3(0f, 2.8f, 0f), new Vector3(0.45f, 0.45f, 0.42f), metal, false, 0.7f);
            Prims.Box(tr, "Yeux", new Vector3(0f, 2.85f, 0.215f), new Vector3(0.3f, 0.07f, 0.01f), new Color(0.05f, 0.05f, 0.05f));
            robot.ArmLeft = Arm(tr, new Vector3(-0.5f, 2.4f, 0f), metal);
            robot.ArmRight = Arm(tr, new Vector3(0.5f, 2.4f, 0f), metal);
            return robot;
        }

        private static Transform Arm(Transform parent, Vector3 shoulder, Color c)
        {
            GameObject p = new GameObject("Bras");
            p.transform.SetParent(parent, false);
            p.transform.localPosition = shoulder;
            Prims.Box(p.transform, "Segment", new Vector3(0f, -0.5f, 0f), new Vector3(0.16f, 1.0f, 0.16f), c, false, 0.7f);
            Prims.Box(p.transform, "Main", new Vector3(0f, -1.05f, 0f), new Vector3(0.2f, 0.16f, 0.2f), new Color(0.2f, 0.2f, 0.2f));
            return p.transform;
        }

        private void Update()
        {
            if (Controller == null) return;
            LightColor ns = Controller.ColorFor(LightAxis.NordSud);
            LightAxis green = Controller.GreenAxis;
            // Le robot fait face à l'axe arrêté (bras levés vers lui), de profil pour l'axe qui circule
            float yaw = green == LightAxis.NordSud ? 90f : 0f;
            Torso.localRotation = Quaternion.Slerp(Torso.localRotation, Quaternion.Euler(0f, yaw, 0f), Time.deltaTime * 2f);
            LightColor active = Controller.ColorFor(green);
            float armAngle = active == LightColor.Vert ? -90f : (active == LightColor.Orange ? -150f : -170f);
            // Rotation négative autour de Z = bras gauche vers l'extérieur ; positive = bras droit vers l'extérieur
            ArmLeft.localRotation = Quaternion.Slerp(ArmLeft.localRotation, Quaternion.Euler(0f, 0f, armAngle), Time.deltaTime * 3f);
            ArmRight.localRotation = Quaternion.Slerp(ArmRight.localRotation, Quaternion.Euler(0f, 0f, -armAngle), Time.deltaTime * 3f);
            LightColor shown = green == LightAxis.NordSud ? ns : Controller.ColorFor(LightAxis.EstOuest);
            if (shown != last)
            {
                last = shown;
                Color c = shown == LightColor.Vert ? new Color(0.1f, 1f, 0.3f) : (shown == LightColor.Orange ? new Color(1f, 0.6f, 0.05f) : new Color(1f, 0.1f, 0.05f));
                Mats.SetLamp(Chest, c, true, 3f);
            }
        }
    }
}
