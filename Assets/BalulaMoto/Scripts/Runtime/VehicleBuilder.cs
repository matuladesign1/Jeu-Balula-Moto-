// BALULA MOTO — construction des véhicules et personnages.
// Ce sont des MAQUETTES VOLUMÉTRIQUES aux proportions réelles (étape « blockout ») : elles seront remplacées
// par les modèles Blender réalistes. Pour brancher un modèle, renseignez "prefabRessource" dans vehicles.json
// (préfabriqué placé dans un dossier Resources, avec un composant VehicleRig rempli).
using System.Collections.Generic;
using BalulaMoto.Core;
using UnityEngine;

namespace BalulaMoto.Runtime
{
    public enum AIVehicleType { TaxiJaune, MinibusJaune, BusTransco, MinibusBlanc, PoliceKabasele, CamionRemorque, MotoTaxi, Berline }

    /// <summary>Personnage (maquette) : membres articulés pour les gestes simples.</summary>
    public class HumanFigure : MonoBehaviour
    {
        public Transform ArmLeft, ArmRight, LegLeft, LegRight, Torso;
        public bool Hailing;
        public bool Walking;
        public bool Seated;
        /// <summary>Personnage réaliste (Rocketbox) : les états ci-dessus pilotent ses animations.</summary>
        public HumanAvatar Avatar;
        public HumanAction IdleAction = HumanAction.Idle;
        private float phase;

        private void Update()
        {
            if (Avatar != null)
            {
                HumanAction want = Seated ? HumanAction.Sit : (Hailing ? HumanAction.Wave : (Walking ? HumanAction.Walk : IdleAction));
                Avatar.Play(want);
                return;
            }
            if (Seated) return;
            phase += Time.deltaTime * (Walking ? 7f : 2f);
            float swing = Walking ? Mathf.Sin(phase) * 28f : 0f;
            if (LegLeft != null) LegLeft.localRotation = Quaternion.Euler(swing, 0f, 0f);
            if (LegRight != null) LegRight.localRotation = Quaternion.Euler(-swing, 0f, 0f);
            if (ArmLeft != null) ArmLeft.localRotation = Quaternion.Euler(-swing * 0.8f, 0f, 0f);
            if (ArmRight != null)
            {
                float target = Hailing ? -150f + Mathf.Sin(phase * 2.5f) * 18f : swing * 0.8f;
                ArmRight.localRotation = Quaternion.Slerp(ArmRight.localRotation, Quaternion.Euler(target, 0f, Hailing ? -12f : 0f), Time.deltaTime * 8f);
            }
        }
    }

    public static partial class VehicleBuilder
    {
        /// <summary>Carrosseries lisses pour la circulation (désactivable en cas de problème d'affichage).</summary>
        public static bool UseSmoothBodies = true;

        private static readonly Color Black = new Color(0.07f, 0.07f, 0.08f);
        private static readonly Color Chrome = new Color(0.78f, 0.79f, 0.8f);
        private static readonly Color Silver = new Color(0.62f, 0.64f, 0.66f);
        private static readonly Color Glass = new Color(0.12f, 0.16f, 0.2f);
        private static readonly Color Amber = new Color(1f, 0.55f, 0.05f);
        private static readonly Color RedLight = new Color(0.55f, 0.05f, 0.04f);

        private static readonly string[] SkinTones = { "#3B2219", "#4A2C20", "#5C3A26", "#6B4430", "#7A4E36", "#8D5A3B" };
        private static readonly string[] Prints = { "#E65100", "#2E7D32", "#1565C0", "#AD1457", "#F9A825", "#6A1B9A", "#00838F", "#C62828" };

        // ==================================================================
        // Véhicule du joueur
        // ==================================================================

        public static RaycastVehicle BuildPlayer(VehicleDef def, OwnedVehicle ov, VehicleStats stats, Vector3 position, Quaternion rotation)
        {
            GameObject root = new GameObject("Joueur_" + def.id);
            root.transform.SetPositionAndRotation(position, rotation);
            Rigidbody rb = root.AddComponent<Rigidbody>();
            VehicleRig rig = null;

            if (!string.IsNullOrEmpty(def.prefabRessource))
            {
                GameObject prefab = Resources.Load<GameObject>(def.prefabRessource);
                if (prefab != null)
                {
                    GameObject model = Object.Instantiate(prefab, root.transform, false);
                    rig = model.GetComponentInChildren<VehicleRig>();
                    if (rig == null) Debug.LogWarning("[BALULA] Le modèle " + def.prefabRessource + " n'a pas de VehicleRig : maquette utilisée.");
                    if (rig == null) Object.Destroy(model);
                }
            }
            if (rig == null) rig = BuildVisual(root.transform, def, ov, true);

            // Collisionneur de caisse (au-dessus des roues : ce sont les rayons qui portent le véhicule)
            BoxCollider box = root.AddComponent<BoxCollider>();
            if (def.Kind == VehicleKind.Moto)
            {
                box.center = new Vector3(0f, 1.0f, 0f);
                box.size = new Vector3(0.6f, 1.15f, def.longueurM * 0.92f);
            }
            else
            {
                float bottom = 0.34f;
                box.center = new Vector3(0f, (bottom + def.hauteurM) * 0.5f, 0f);
                box.size = new Vector3(def.largeurM * 0.95f, def.hauteurM - bottom, def.longueurM * 0.95f);
            }

            RaycastVehicle v = root.AddComponent<RaycastVehicle>();
            v.Setup(def, stats, rig);
            Prims.SetLayerRecursive(root, RaycastVehicle.PlayerLayer);
            return v;
        }

        /// <summary>Modèle visuel seul (aperçu 3D du magasin).</summary>
        public static GameObject BuildPreview(VehicleDef def, OwnedVehicle ov)
        {
            GameObject root = new GameObject("Apercu_" + def.id);
            BuildVisual(root.transform, def, ov, false);
            return root;
        }

        public static VehicleRig BuildVisual(Transform root, VehicleDef def, OwnedVehicle ov, bool withDriver)
        {
            VehicleRig rig = root.gameObject.AddComponent<VehicleRig>();
            GameObject body = new GameObject("Carrosserie");
            body.transform.SetParent(root, false);
            rig.Body = body.transform;
            Color main = ColorUtil.Parse(ov != null ? ov.couleurPrincipale : def.couleurPrincipale, ColorUtil.Parse(def.couleurPrincipale, Color.blue));
            Color second = ColorUtil.Parse(ov != null ? ov.couleurSecondaire : def.couleurSecondaire, ColorUtil.Parse(def.couleurSecondaire, Black));
            Color rim = RimColor(ov);
            if (def.Kind == VehicleKind.Moto) BuildMoto(rig, def, ov, main, second, rim, withDriver);
            else BuildTricycle(rig, def, ov, main, second, rim, withDriver);
            string plate = ov != null && !string.IsNullOrEmpty(ov.plaque) ? ov.plaque : "BLM-0000-KN";
            AddPlate(rig.Body, plate, def.Kind == VehicleKind.Moto ? new Vector3(0f, 0.7f, -def.longueurM * 0.5f + 0.04f) : new Vector3(0f, 0.55f, -def.longueurM * 0.5f - 0.02f));
            if (ov != null && !string.IsNullOrEmpty(ov.autocollant) && ov.autocollant != "Aucun")
                AddSticker(rig.Body, ov.autocollant, def);
            return rig;
        }

        private static Color RimColor(OwnedVehicle ov)
        {
            if (ov == null || string.IsNullOrEmpty(ov.jantes)) return Silver;
            if (ov.jantes.Contains("noir")) return Black;
            if (ov.jantes.Contains("doré")) return new Color(0.83f, 0.66f, 0.2f);
            if (ov.jantes.Contains("chrom")) return Chrome;
            return Silver;
        }

        private static Transform Wheel(VehicleRig rig, Transform parent, string name, Vector3 pos, float radius, float width, Color rim)
        {
            GameObject w = new GameObject(name);
            w.transform.SetParent(parent, false);
            w.transform.localPosition = pos;
            Prims.CylX(w.transform, "Pneu", Vector3.zero, radius * 2f, width, Black);
            Prims.CylX(w.transform, "Jante", Vector3.zero, radius * 1.42f, width * 1.05f, rim);
            Prims.Box(w.transform, "Rayon", Vector3.zero, new Vector3(width * 1.1f, radius * 1.3f, 0.04f), rim);
            Prims.Box(w.transform, "Rayon2", Vector3.zero, new Vector3(width * 1.1f, 0.04f, radius * 1.3f), rim);
            rig.Wheels.Add(w.transform);
            return w.transform;
        }

        private static Renderer Lamp(Transform parent, string name, Vector3 pos, Vector3 size, Color off)
        {
            GameObject g = Prims.Box(parent, name, pos, size, off);
            return g.GetComponent<Renderer>();
        }

        private static void AddPlate(Transform body, string text, Vector3 pos)
        {
            // Un TextMesh se lit du côté -Z : la plaque arrière est donc lisible sans rotation.
            Prims.Box(body, "Plaque", pos, new Vector3(0.2f, 0.11f, 0.012f), Color.white);
            Prims.Label(body, text, pos + new Vector3(0f, 0f, -0.01f), 0.011f, Black, false);
        }

        private static void AddSticker(Transform body, string sticker, VehicleDef def)
        {
            float x = def.Kind == VehicleKind.Moto ? 0.18f : def.largeurM * 0.5f + 0.03f;
            float y = def.Kind == VehicleKind.Moto ? 0.88f : 0.95f;
            float z = def.Kind == VehicleKind.Moto ? 0.28f : -0.3f;
            GameObject l = Prims.Label(body, sticker, new Vector3(x, y, z), 0.012f, Color.white, false);
            l.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            GameObject r = Prims.Label(body, sticker, new Vector3(-x, y, z), 0.012f, Color.white, false);
            r.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        }

        // ------------------------------------------------------------------
        // Moto (proportions issues de la fiche du modèle)
        // ------------------------------------------------------------------
        private static void BuildMoto(VehicleRig rig, VehicleDef d, OwnedVehicle ov, Color main, Color second, Color rim, bool withDriver)
        {
            Transform b = rig.Body;
            float r = d.rayonRoueM, wb = d.empattementM, L = d.longueurM;
            bool sport = d.style == "sport", trail = d.style == "trail", longSeat = d.style == "boxer" || d.passagers >= 2;
            float zf = wb * 0.5f, zr = -wb * 0.5f;
            Wheel(rig, b, "RoueAV", new Vector3(0f, r, zf), r, trail ? 0.09f : 0.1f, rim);
            Wheel(rig, b, "RoueAR", new Vector3(0f, r, zr), r, 0.11f, rim);

            // Cadre, moteur, échappement
            Vector3 head = new Vector3(0f, 0.95f + (trail ? 0.08f : 0f), zf - 0.14f);
            Prims.Tube(b, "Cadre", head, new Vector3(0f, 0.45f, -0.08f), 0.06f, Black);
            Prims.Tube(b, "CadreBas", new Vector3(0f, 0.45f, -0.08f), new Vector3(0f, 0.86f, -0.55f), 0.05f, Black);
            Prims.Tube(b, "BrasOscillantG", new Vector3(0.09f, 0.42f, -0.08f), new Vector3(0.09f, r, zr), 0.05f, Black);
            Prims.Tube(b, "BrasOscillantD", new Vector3(-0.09f, 0.42f, -0.08f), new Vector3(-0.09f, r, zr), 0.05f, Black);
            Prims.Box(b, "Moteur", new Vector3(0f, 0.43f, 0.04f), new Vector3(0.27f, 0.3f, 0.38f), Silver, false, 0.5f);
            Prims.Box(b, "Culasse", new Vector3(0f, 0.62f, 0.14f), new Vector3(0.2f, 0.16f, 0.2f), Silver, false, 0.5f);
            Prims.Box(b, "Carter", new Vector3(0.1f, 0.36f, -0.02f), new Vector3(0.1f, 0.2f, 0.3f), new Color(0.5f, 0.5f, 0.52f));
            Prims.Tube(b, "Echappement", new Vector3(-0.1f, 0.42f, 0.2f), new Vector3(-0.17f, 0.4f, -0.45f), 0.05f, new Color(0.2f, 0.2f, 0.2f));
            Prims.Tube(b, "Silencieux", new Vector3(-0.17f, 0.42f, -0.4f), new Vector3(-0.18f, 0.5f, -0.85f), 0.09f, Chrome);
            Prims.Box(b, "GardeChaine", new Vector3(0.13f, 0.36f, -0.42f), new Vector3(0.03f, 0.12f, 0.62f), Black);
            Prims.Tube(b, "AmortisseurG", new Vector3(0.12f, r + 0.06f, zr + 0.06f), new Vector3(0.12f, 0.86f, -0.42f), 0.05f, Chrome);
            Prims.Tube(b, "AmortisseurD", new Vector3(-0.12f, r + 0.06f, zr + 0.06f), new Vector3(-0.12f, 0.86f, -0.42f), 0.05f, Chrome);

            // Réservoir, flancs, selle
            Prims.Box(b, "Reservoir", new Vector3(0f, 0.9f, 0.26f), new Vector3(sport ? 0.38f : 0.33f, 0.24f, 0.5f), main, false, 0.6f);
            Prims.Box(b, "BandeHaute", new Vector3(0f, 0.95f, 0.24f), new Vector3(sport ? 0.385f : 0.335f, 0.035f, 0.36f), second, false, 0.6f);
            Prims.Box(b, "BandeBasse", new Vector3(0f, 0.85f, 0.2f), new Vector3(sport ? 0.385f : 0.335f, 0.025f, 0.28f), Color.white, false, 0.6f);
            Prims.Box(b, "Flanc", new Vector3(0f, 0.72f, -0.22f), new Vector3(0.26f, 0.16f, 0.3f), main, false, 0.6f);
            float seatLen = longSeat ? 0.95f : 0.78f;
            Color seatColor = ov != null && ov.selle == "Marron" ? new Color(0.36f, 0.22f, 0.12f) : (ov != null && ov.selle == "Rouge" ? new Color(0.55f, 0.08f, 0.08f) : Black);
            Prims.Box(b, "Selle", new Vector3(0f, 0.88f, -0.24f - (seatLen - 0.78f) * 0.5f), new Vector3(0.3f, 0.1f, seatLen), seatColor, false, 0.3f);
            if (!sport)
            {
                Prims.Box(b, "PorteBagages", new Vector3(0f, 0.94f, -L * 0.5f + 0.32f), new Vector3(0.3f, 0.03f, 0.38f), Black);
                Prims.Tube(b, "PoigneePassager", new Vector3(0.16f, 0.93f, -0.3f), new Vector3(0.16f, 0.95f, -L * 0.5f + 0.15f), 0.03f, Black);
                Prims.Tube(b, "PoigneePassager2", new Vector3(-0.16f, 0.93f, -0.3f), new Vector3(-0.16f, 0.95f, -L * 0.5f + 0.15f), 0.03f, Black);
            }
            Prims.Box(b, "GardeBoueAR", new Vector3(0f, r * 2f + 0.04f, zr - 0.12f), new Vector3(0.14f, 0.03f, 0.5f), Black);
            rig.TailLights.Add(Lamp(b, "FeuArriere", new Vector3(0f, 0.86f, -L * 0.5f + 0.08f), new Vector3(0.13f, 0.07f, 0.05f), RedLight));
            rig.IndicatorsLeft.Add(Lamp(b, "ClignoARG", new Vector3(-0.15f, 0.82f, -L * 0.5f + 0.12f), new Vector3(0.05f, 0.04f, 0.06f), ColorUtil.Shade(Amber, 0.3f)));
            rig.IndicatorsRight.Add(Lamp(b, "ClignoARD", new Vector3(0.15f, 0.82f, -L * 0.5f + 0.12f), new Vector3(0.05f, 0.04f, 0.06f), ColorUtil.Shade(Amber, 0.3f)));

            // Direction : fourche, phare, guidon, rétroviseurs
            GameObject pivot = new GameObject("Direction");
            pivot.transform.SetParent(b, false);
            pivot.transform.localPosition = head;
            rig.SteerPivot = pivot.transform;
            Transform p = pivot.transform;
            Vector3 axle = new Vector3(0f, r - head.y, zf - head.z);
            Prims.Tube(p, "FourcheG", new Vector3(0.09f, 0f, -0.02f), axle + new Vector3(0.09f, 0f, 0f), 0.045f, Chrome);
            Prims.Tube(p, "FourcheD", new Vector3(-0.09f, 0f, -0.02f), axle + new Vector3(-0.09f, 0f, 0f), 0.045f, Chrome);
            Prims.Box(p, "GardeBoueAV", axle + new Vector3(0f, r + (trail ? 0.18f : 0.04f), 0.02f), new Vector3(0.13f, 0.03f, 0.42f), trail ? Color.white : main, false, 0.6f);
            Prims.Box(p, "Carenage", new Vector3(0f, 0.1f, 0.14f), new Vector3(0.27f, 0.24f, 0.16f), main, false, 0.6f);
            rig.HeadLights.Add(Lamp(p, "Phare", new Vector3(0f, 0.1f, 0.23f), new Vector3(0.17f, 0.12f, 0.03f), new Color(0.6f, 0.6f, 0.55f)));
            Prims.BoxRot(p, "SauteVent", new Vector3(0f, 0.28f, 0.18f), new Vector3(0.22f, 0.13f, 0.015f), new Vector3(-20f, 0f, 0f), Glass);
            rig.IndicatorsLeft.Add(Lamp(p, "ClignoAVG", new Vector3(-0.17f, 0.06f, 0.18f), new Vector3(0.05f, 0.04f, 0.06f), ColorUtil.Shade(Amber, 0.3f)));
            rig.IndicatorsRight.Add(Lamp(p, "ClignoAVD", new Vector3(0.17f, 0.06f, 0.18f), new Vector3(0.05f, 0.04f, 0.06f), ColorUtil.Shade(Amber, 0.3f)));
            Prims.Tube(p, "Guidon", new Vector3(-0.36f, 0.2f, -0.06f), new Vector3(0.36f, 0.2f, -0.06f), 0.03f, Black);
            Prims.Tube(p, "TigeRetroG", new Vector3(-0.25f, 0.2f, -0.05f), new Vector3(-0.3f, 0.46f, 0f), 0.015f, Black);
            Prims.Tube(p, "TigeRetroD", new Vector3(0.25f, 0.2f, -0.05f), new Vector3(0.3f, 0.46f, 0f), 0.015f, Black);
            Prims.Box(p, "RetroG", new Vector3(-0.3f, 0.48f, 0f), new Vector3(0.1f, 0.07f, 0.02f), Black);
            Prims.Box(p, "RetroD", new Vector3(0.3f, 0.48f, 0f), new Vector3(0.1f, 0.07f, 0.02f), Black);
            // Glaces des rétroviseurs (côté pilote, brillantes : visibles en vue 1re personne)
            Prims.Box(p, "GlaceRetroG", new Vector3(-0.3f, 0.48f, -0.012f), new Vector3(0.085f, 0.055f, 0.004f), new Color(0.74f, 0.82f, 0.9f), false, 0.95f);
            Prims.Box(p, "GlaceRetroD", new Vector3(0.3f, 0.48f, -0.012f), new Vector3(0.085f, 0.055f, 0.004f), new Color(0.74f, 0.82f, 0.9f), false, 0.95f);
            rig.HeadLamp = HeadLamp(p, new Vector3(0f, 0.1f, 0.3f));

            rig.CameraFirstPerson = Anchor(b, "CameraPremierePersonne", new Vector3(0f, 1.58f, -0.08f));
            rig.CameraDashboard = Anchor(b, "CameraTableauDeBord", new Vector3(0f, 1.4f, 0.05f));
            rig.Seats.Add(Anchor(b, "PlacePassager1", new Vector3(0f, 0.93f, -0.5f)));
            if (d.passagers >= 2) rig.Seats.Add(Anchor(b, "PlacePassager2", new Vector3(0f, 0.95f, -0.74f)));
            rig.CargoAnchor = Anchor(b, "Bagages", new Vector3(0f, 0.98f, -L * 0.5f + 0.3f));
            if (withDriver)
            {
                rig.Driver = BuildDriver(b, new Vector3(0f, 0.88f, -0.12f), 7, true);
                SetArmsForward(rig.Driver, 52f);
                HumanLibrary.SetupRider(rig.Driver, Anchor(p, "MainGauche", new Vector3(-0.3f, 0.2f, -0.07f)), Anchor(p, "MainDroite", new Vector3(0.3f, 0.2f, -0.07f)),
                    Anchor(b, "PiedGauche", new Vector3(-0.17f, 0.4f, 0.02f)), Anchor(b, "PiedDroit", new Vector3(0.17f, 0.4f, 0.02f)), 10f);
            }
        }

        // ------------------------------------------------------------------
        // Tricycle (5 places, 8 places, cargo)
        // ------------------------------------------------------------------
        private static void BuildTricycle(VehicleRig rig, VehicleDef d, OwnedVehicle ov, Color main, Color second, Color rim, bool withDriver)
        {
            Transform b = rig.Body;
            float r = d.rayonRoueM, wb = d.empattementM, L = d.longueurM, W = d.largeurM, H = d.hauteurM;
            float track = Mathf.Max(0.9f, d.voieArriereM) * 0.5f;
            bool cargo = d.style == "tricycle_cargo";
            bool bajaj = d.style == "tricycle_bajaj";
            float front = L * 0.5f;

            Wheel(rig, b, "RoueAV", new Vector3(0f, r, wb * 0.55f), r, 0.12f, rim);
            Wheel(rig, b, "RoueARG", new Vector3(-track, r, -wb * 0.45f), r, 0.13f, rim);
            Wheel(rig, b, "RoueARD", new Vector3(track, r, -wb * 0.45f), r, 0.13f, rim);

            Prims.Box(b, "Plancher", new Vector3(0f, 0.4f, -0.05f), new Vector3(W * 0.9f, 0.1f, L * 0.86f), Black);
            Prims.Box(b, "CapotAvant", new Vector3(0f, 0.82f, front - 0.3f), new Vector3(W * 0.56f, 0.86f, 0.42f), main, false, 0.55f);
            Prims.Box(b, "GardeBoueAV", new Vector3(0f, r * 2f + 0.05f, wb * 0.55f), new Vector3(0.18f, 0.04f, 0.42f), main, false, 0.55f);
            Prims.BoxRot(b, "PareBrise", new Vector3(0f, 1.42f, front - 0.5f), new Vector3(W * 0.78f, 0.55f, 0.03f), new Vector3(-12f, 0f, 0f), Glass);
            Prims.Box(b, "TableauDeBord", new Vector3(0f, 1.08f, front - 0.68f), new Vector3(W * 0.6f, 0.14f, 0.24f), Black);

            float cabinStart = front - 0.55f;
            float cabinEnd = cargo ? front - 1.35f : -front + 0.05f;
            float cabinLen = cabinStart - cabinEnd;
            float cabinMid = (cabinStart + cabinEnd) * 0.5f;
            Color roofColor = bajaj ? Black : main;
            Prims.Box(b, "Toit", new Vector3(0f, H - 0.03f, cabinMid + 0.25f), new Vector3(W * 0.98f, 0.07f, cabinLen + 0.6f), roofColor, false, 0.5f);
            foreach (float sx in new[] { -1f, 1f })
            {
                Prims.Box(b, "MontantAV", new Vector3(sx * W * 0.46f, (0.45f + H) * 0.5f, cabinStart), new Vector3(0.05f, H - 0.45f, 0.05f), Black);
                Prims.Box(b, "MontantAR", new Vector3(sx * W * 0.47f, (0.45f + H) * 0.5f, cabinEnd), new Vector3(0.05f, H - 0.45f, 0.05f), Black);
            }

            if (cargo)
            {
                float bedLen = L - (front - cabinEnd) - 0.1f;
                float bedMid = cabinEnd - bedLen * 0.5f;
                Prims.Box(b, "Benne", new Vector3(0f, 0.72f, bedMid), new Vector3(W, 0.08f, bedLen), second);
                Prims.Box(b, "RidelleG", new Vector3(-W * 0.5f, 0.92f, bedMid), new Vector3(0.05f, 0.38f, bedLen), main);
                Prims.Box(b, "RidelleD", new Vector3(W * 0.5f, 0.92f, bedMid), new Vector3(0.05f, 0.38f, bedLen), main);
                Prims.Box(b, "RidelleAR", new Vector3(0f, 0.92f, bedMid - bedLen * 0.5f), new Vector3(W, 0.38f, 0.05f), main);
                rig.CargoAnchor = Anchor(b, "Chargement", new Vector3(0f, 0.8f, bedMid));
                Prims.Box(b, "CabineArriere", new Vector3(0f, 1.1f, cabinEnd + 0.02f), new Vector3(W * 0.95f, 1.2f, 0.05f), main);
            }
            else
            {
                Prims.Box(b, "BasDeCaisse", new Vector3(0f, 0.68f, cabinMid - 0.15f), new Vector3(W * 0.97f, 0.5f, cabinLen - 0.3f), second, false, 0.45f);
                if (bajaj)
                {
                    Prims.Box(b, "BacheArriere", new Vector3(0f, H - 0.5f, cabinEnd), new Vector3(W * 0.96f, 0.85f, 0.04f), Black);
                    Prims.Box(b, "FaceAvantJaune", new Vector3(0f, 1.22f, front - 0.08f), new Vector3(W * 0.62f, 0.3f, 0.12f), second, false, 0.55f);
                }
                else
                {
                    foreach (float sx in new[] { -1f, 1f })
                    {
                        Prims.Box(b, "PanneauHaut", new Vector3(sx * W * 0.485f, H - 0.18f, cabinMid - 0.1f), new Vector3(0.04f, 0.22f, cabinLen - 0.4f), main, false, 0.55f);
                        Prims.Box(b, "Vitre", new Vector3(sx * W * 0.49f, H - 0.55f, cabinMid - 0.35f), new Vector3(0.03f, 0.48f, cabinLen * 0.45f), Glass);
                        Prims.Box(b, "Porte", new Vector3(sx * W * 0.495f, 0.95f, cabinMid + 0.25f), new Vector3(0.04f, 0.95f, 0.62f), Black);
                    }
                    Prims.Box(b, "PanneauArriere", new Vector3(0f, (0.45f + H) * 0.5f, cabinEnd), new Vector3(W * 0.97f, H - 0.45f, 0.05f), main, false, 0.55f);
                    Prims.Box(b, "VitreArriere", new Vector3(0f, H - 0.45f, cabinEnd - 0.03f), new Vector3(W * 0.5f, 0.3f, 0.02f), Glass);
                }
                // Banquettes arrière
                int rows = d.placesArriere > 3 ? 2 : 1;
                for (int row = 0; row < rows; row++)
                {
                    float z = rows == 1 ? cabinEnd + 0.35f : (row == 0 ? cabinEnd + 0.3f : cabinMid + 0.15f);
                    Prims.Box(b, "Banquette" + row, new Vector3(0f, 0.82f, z), new Vector3(W * 0.82f, 0.12f, 0.42f), new Color(0.35f, 0.22f, 0.13f));
                    Prims.Box(b, "Dossier" + row, new Vector3(0f, 1.1f, row == 1 ? z + 0.22f : z - 0.22f), new Vector3(W * 0.82f, 0.45f, 0.08f), new Color(0.35f, 0.22f, 0.13f));
                    for (int s = 0; s < 3 && rig.Seats.Count < d.placesAvant + d.placesArriere; s++)
                    {
                        Transform seat = Anchor(b, "Place", new Vector3((s - 1) * W * 0.28f, 0.88f, z));
                        if (row == 1) seat.localRotation = Quaternion.Euler(0f, 180f, 0f);
                        rig.Seats.Add(seat);
                    }
                }
                rig.CargoAnchor = Anchor(b, "Bagages", new Vector3(0f, 0.95f, cabinEnd + 0.1f));
            }

            // Poste de conduite (2 places à côté du chauffeur selon la configuration demandée)
            float driverZ = front - 1.05f;
            Prims.Box(b, "SiegeChauffeur", new Vector3(0f, 0.86f, driverZ), new Vector3(W * 0.8f, 0.12f, 0.42f), Black);
            List<Transform> frontSeats = new List<Transform>();
            for (int i = 0; i < d.placesAvant; i++)
                frontSeats.Add(Anchor(b, "PlaceAvant" + i, new Vector3((i % 2 == 0 ? -1f : 1f) * W * 0.3f, 0.92f, driverZ)));
            rig.Seats.InsertRange(0, frontSeats);

            GameObject pivot = new GameObject("Direction");
            pivot.transform.SetParent(b, false);
            pivot.transform.localPosition = new Vector3(0f, 1.12f, front - 0.78f);
            rig.SteerPivot = pivot.transform;
            Prims.Tube(pivot.transform, "Guidon", new Vector3(-0.34f, 0f, 0f), new Vector3(0.34f, 0f, 0f), 0.03f, Black);

            if (bajaj)
            {
                rig.HeadLights.Add(Lamp(b, "PhareG", new Vector3(-0.3f, 0.95f, front - 0.08f), new Vector3(0.16f, 0.16f, 0.04f), new Color(0.6f, 0.6f, 0.55f)));
                rig.HeadLights.Add(Lamp(b, "PhareD", new Vector3(0.3f, 0.95f, front - 0.08f), new Vector3(0.16f, 0.16f, 0.04f), new Color(0.6f, 0.6f, 0.55f)));
            }
            else rig.HeadLights.Add(Lamp(b, "Phare", new Vector3(0f, 1.0f, front - 0.08f), new Vector3(0.2f, 0.16f, 0.04f), new Color(0.6f, 0.6f, 0.55f)));
            rig.HeadLamp = HeadLamp(b, new Vector3(0f, 1.0f, front));
            rig.TailLights.Add(Lamp(b, "FeuARG", new Vector3(-W * 0.42f, 0.72f, -front - 0.01f), new Vector3(0.12f, 0.1f, 0.03f), RedLight));
            rig.TailLights.Add(Lamp(b, "FeuARD", new Vector3(W * 0.42f, 0.72f, -front - 0.01f), new Vector3(0.12f, 0.1f, 0.03f), RedLight));
            rig.IndicatorsLeft.Add(Lamp(b, "ClignoAVG", new Vector3(-W * 0.28f, 1.15f, front - 0.08f), new Vector3(0.06f, 0.05f, 0.05f), ColorUtil.Shade(Amber, 0.3f)));
            rig.IndicatorsRight.Add(Lamp(b, "ClignoAVD", new Vector3(W * 0.28f, 1.15f, front - 0.08f), new Vector3(0.06f, 0.05f, 0.05f), ColorUtil.Shade(Amber, 0.3f)));
            rig.IndicatorsLeft.Add(Lamp(b, "ClignoARG", new Vector3(-W * 0.42f, 0.86f, -front - 0.01f), new Vector3(0.08f, 0.06f, 0.03f), ColorUtil.Shade(Amber, 0.3f)));
            rig.IndicatorsRight.Add(Lamp(b, "ClignoARD", new Vector3(W * 0.42f, 0.86f, -front - 0.01f), new Vector3(0.08f, 0.06f, 0.03f), ColorUtil.Shade(Amber, 0.3f)));
            Prims.Box(b, "RetroG", new Vector3(-W * 0.55f, 1.35f, front - 0.5f), new Vector3(0.12f, 0.1f, 0.02f), Black);
            Prims.Box(b, "RetroD", new Vector3(W * 0.55f, 1.35f, front - 0.5f), new Vector3(0.12f, 0.1f, 0.02f), Black);

            rig.CameraFirstPerson = Anchor(b, "CameraPremierePersonne", new Vector3(0f, 1.62f, driverZ + 0.05f));
            rig.CameraDashboard = Anchor(b, "CameraTableauDeBord", new Vector3(0f, 1.38f, front - 0.75f));
            if (withDriver)
            {
                rig.Driver = BuildDriver(b, new Vector3(0f, 0.92f, driverZ), 11, false);
                SetArmsForward(rig.Driver, 60f);
                HumanLibrary.SetupRider(rig.Driver, Anchor(pivot.transform, "MainGauche", new Vector3(-0.3f, 0f, -0.02f)), Anchor(pivot.transform, "MainDroite", new Vector3(0.3f, 0f, -0.02f)), null, null, 4f);
            }
        }

        private static Transform Anchor(Transform parent, string name, Vector3 pos)
        {
            GameObject g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            return g.transform;
        }

        private static Light HeadLamp(Transform parent, Vector3 pos)
        {
            GameObject g = new GameObject("Projecteur");
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localRotation = Quaternion.Euler(9f, 0f, 0f);
            Light l = g.AddComponent<Light>();
            l.type = LightType.Spot;
            l.range = 45f;
            l.spotAngle = 70f;
            l.intensity = 3f;
            l.color = new Color(1f, 0.95f, 0.82f);
            l.shadows = LightShadows.None;
            l.enabled = false;
            return l;
        }

        private static void SetArmsForward(GameObject human, float angle)
        {
            HumanFigure f = human != null ? human.GetComponent<HumanFigure>() : null;
            if (f == null) return;
            if (f.ArmLeft != null) f.ArmLeft.localRotation = Quaternion.Euler(-angle, 0f, 0f);
            if (f.ArmRight != null) f.ArmRight.localRotation = Quaternion.Euler(-angle, 0f, 0f);
        }

        // ==================================================================
        // Personnages (maquettes) — tenues : pagne, costume, etudiant, uniforme, travail, civil,
        // blouse, police, roulage, mecano, chauffeur, vendeur
        // ==================================================================

        /// <summary>Chauffeur du joueur : l'avatar choisi dans le menu « Chauffeur » s'il existe.</summary>
        private static GameObject BuildDriver(Transform parent, Vector3 localPos, int seed, bool helmet)
        {
            GameObject chosen = HumanLibrary.FindPrefab(GameSettings.DriverAvatar);
            if (chosen != null) return HumanLibrary.Instantiate(chosen, parent, localPos, "chauffeur", true, seed, helmet);
            return BuildHuman(parent, localPos, "chauffeur", true, seed, helmet);
        }

        public static GameObject BuildHuman(Transform parent, Vector3 localPos, string outfit, bool seated, int seed, bool helmet)
        {
            // Personnages réalistes (Rocketbox) quand ils sont disponibles, sinon maquettes en volumes simples
            GameObject real = HumanLibrary.Spawn(parent, localPos, outfit, seated, seed, helmet);
            if (real != null) return real;
            GameObject custom = TryCustomHuman(parent, localPos, seed, seated);
            if (custom != null) return custom;

            System.Random rnd = new System.Random(seed * 7919 + 17);
            GameObject root = new GameObject("Personnage_" + outfit);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPos;
            HumanFigure fig = root.AddComponent<HumanFigure>();
            fig.Seated = seated;
            Color skin = ColorUtil.Hex(SkinTones[rnd.Next(SkinTones.Length)]);
            Color top, bottom, headwear = Color.clear;
            bool skirt = false;
            switch (outfit)
            {
                case "pagne":
                    top = ColorUtil.Hex(Prints[rnd.Next(Prints.Length)]);
                    bottom = ColorUtil.Hex(Prints[rnd.Next(Prints.Length)]);
                    headwear = top; skirt = true; break;
                case "costume": top = new Color(0.12f, 0.13f, 0.18f); bottom = top; break;
                case "uniforme": top = Color.white; bottom = new Color(0.08f, 0.14f, 0.35f); break;
                case "etudiant": top = ColorUtil.Hex(Prints[rnd.Next(Prints.Length)]); bottom = new Color(0.15f, 0.2f, 0.35f); break;
                case "travail": top = new Color(0.9f, 0.45f, 0.1f); bottom = new Color(0.2f, 0.25f, 0.35f); break;
                case "mecano": top = new Color(0.12f, 0.25f, 0.5f); bottom = top; break;
                case "blouse": top = new Color(0.95f, 0.95f, 0.95f); bottom = new Color(0.3f, 0.3f, 0.35f); break;
                case "police": top = new Color(0.1f, 0.16f, 0.32f); bottom = top; headwear = new Color(0.08f, 0.1f, 0.2f); break;
                case "roulage": top = new Color(0.95f, 0.8f, 0.05f); bottom = new Color(0.1f, 0.16f, 0.32f); headwear = Color.white; break;
                case "chauffeur": top = new Color(0.95f, 0.55f, 0.08f); bottom = new Color(0.18f, 0.2f, 0.28f); break;
                default: top = ColorUtil.Hex(Prints[rnd.Next(Prints.Length)]); bottom = new Color(0.2f, 0.22f, 0.3f); break;
            }
            float hipY = seated ? 0f : 0.86f;
            Transform t = root.transform;
            // Jambes (pivot à la hanche)
            fig.LegLeft = Limb(t, "JambeG", new Vector3(-0.1f, hipY, 0f), new Vector3(0.14f, 0.84f, 0.15f), bottom);
            fig.LegRight = Limb(t, "JambeD", new Vector3(0.1f, hipY, 0f), new Vector3(0.14f, 0.84f, 0.15f), bottom);
            if (seated)
            {
                fig.LegLeft.localRotation = Quaternion.Euler(-80f, 0f, 0f);
                fig.LegRight.localRotation = Quaternion.Euler(-80f, 0f, 0f);
            }
            if (skirt && !seated) Prims.Box(t, "Pagne", new Vector3(0f, hipY - 0.35f, 0f), new Vector3(0.4f, 0.75f, 0.26f), bottom);
            fig.Torso = Prims.Box(t, "Torse", new Vector3(0f, hipY + 0.31f, 0f), new Vector3(0.42f, 0.62f, 0.24f), top).transform;
            if (outfit == "costume") Prims.Box(t, "Chemise", new Vector3(0f, hipY + 0.5f, 0.121f), new Vector3(0.12f, 0.22f, 0.01f), Color.white);
            if (outfit == "roulage") Prims.Box(t, "Gilet", new Vector3(0f, hipY + 0.31f, 0f), new Vector3(0.44f, 0.5f, 0.26f), new Color(1f, 0.85f, 0f));
            Prims.Box(t, "Cou", new Vector3(0f, hipY + 0.66f, 0f), new Vector3(0.1f, 0.08f, 0.1f), skin);
            Prims.Ball(t, "Tete", new Vector3(0f, hipY + 0.8f, 0f), new Vector3(0.21f, 0.25f, 0.23f), skin);
            if (helmet) Prims.Ball(t, "Casque", new Vector3(0f, hipY + 0.84f, -0.01f), new Vector3(0.29f, 0.28f, 0.31f), new Color(0.08f, 0.08f, 0.1f));
            else if (headwear.a > 0f) Prims.Box(t, "Coiffe", new Vector3(0f, hipY + 0.93f, 0f), new Vector3(0.24f, 0.1f, 0.25f), headwear);
            fig.ArmLeft = Limb(t, "BrasG", new Vector3(-0.27f, hipY + 0.58f, 0f), new Vector3(0.1f, 0.6f, 0.11f), outfit == "pagne" ? skin : top);
            fig.ArmRight = Limb(t, "BrasD", new Vector3(0.27f, hipY + 0.58f, 0f), new Vector3(0.1f, 0.6f, 0.11f), outfit == "pagne" ? skin : top);
            Prims.NoShadows(root);
            return root;
        }

        /// <summary>Si des personnages réalistes sont placés dans Resources/BalulaModels/Humains, ils sont utilisés.</summary>
        private static GameObject TryCustomHuman(Transform parent, Vector3 localPos, int seed, bool seated)
        {
            if (seated) return null;
            GameObject[] prefabs = Resources.LoadAll<GameObject>("BalulaModels/Humains");
            if (prefabs == null || prefabs.Length == 0) return null;
            GameObject g = Object.Instantiate(prefabs[Mathf.Abs(seed) % prefabs.Length], parent, false);
            g.transform.localPosition = localPos;
            if (g.GetComponent<HumanFigure>() == null) g.AddComponent<HumanFigure>();
            return g;
        }

        private static Transform Limb(Transform parent, string name, Vector3 pivot, Vector3 size, Color c)
        {
            GameObject p = new GameObject(name);
            p.transform.SetParent(parent, false);
            p.transform.localPosition = pivot;
            Prims.Box(p.transform, "Segment", new Vector3(0f, -size.y * 0.5f, 0f), size, c);
            return p.transform;
        }

        // ==================================================================
        // Véhicules de la circulation (maquettes)
        // ==================================================================

        public static GameObject BuildTraffic(Transform parent, AIVehicleType type, int seed, out float length, out List<Renderer> brakeLights, out List<Renderer> hazards, out List<Transform> wheels)
        {
            System.Random rnd = new System.Random(seed);
            GameObject root = new GameObject("PNJ_" + type);
            root.transform.SetParent(parent, false);
            Transform t = root.transform;
            brakeLights = new List<Renderer>();
            hazards = new List<Renderer>();
            wheels = new List<Transform>();
            float L, W, H, r;
            Color body;
            switch (type)
            {
                case AIVehicleType.TaxiJaune: L = 3.8f; W = 1.66f; H = 1.48f; r = 0.3f; body = new Color(0.98f, 0.8f, 0.05f); break;
                case AIVehicleType.MinibusJaune: L = 5.3f; W = 2.0f; H = 2.45f; r = 0.36f; body = new Color(0.97f, 0.78f, 0.1f); break;
                case AIVehicleType.BusTransco: L = 11.5f; W = 2.5f; H = 3.1f; r = 0.5f; body = new Color(0.96f, 0.96f, 0.96f); break;
                case AIVehicleType.MinibusBlanc: L = 7.0f; W = 2.05f; H = 2.6f; r = 0.4f; body = new Color(0.95f, 0.95f, 0.95f); break;
                case AIVehicleType.PoliceKabasele: L = 5.6f; W = 2.0f; H = 2.55f; r = 0.38f; body = new Color(0.08f, 0.11f, 0.24f); break;
                case AIVehicleType.CamionRemorque: L = 12f; W = 2.5f; H = 3.4f; r = 0.5f; body = new Color(0.55f, 0.1f, 0.1f); break;
                case AIVehicleType.MotoTaxi: L = 2.0f; W = 0.75f; H = 1.6f; r = 0.3f; body = ColorUtil.Hex(Prints[rnd.Next(Prints.Length)]); break;
                default: L = 4.4f; W = 1.75f; H = 1.45f; r = 0.32f; body = rnd.Next(2) == 0 ? new Color(0.85f, 0.86f, 0.88f) : new Color(0.15f, 0.16f, 0.18f); break;
            }
            length = L;

            if (type == AIVehicleType.MotoTaxi)
            {
                wheels.Add(TrafficWheel(t, new Vector3(0f, r, 0.65f), r, 0.1f));
                wheels.Add(TrafficWheel(t, new Vector3(0f, r, -0.65f), r, 0.1f));
                // Moto-taxi : cadre, moteur, réservoir, selle longue, fourche, guidon, phare
                Prims.Tube(t, "Cadre", new Vector3(0f, 1.0f, 0.5f), new Vector3(0f, 0.45f, -0.05f), 0.06f, Black);
                Prims.Box(t, "Moteur", new Vector3(0f, 0.45f, 0.05f), new Vector3(0.27f, 0.3f, 0.38f), Silver, false, 0.5f);
                Prims.Tube(t, "Echappement", new Vector3(-0.12f, 0.42f, 0.15f), new Vector3(-0.18f, 0.5f, -0.75f), 0.08f, Chrome);
                Prims.Pill(t, "Reservoir", new Vector3(0f, 0.92f, 0.28f), new Vector3(0.34f, 0.24f, 0.5f), body);
                Prims.Box(t, "Flanc", new Vector3(0f, 0.74f, -0.2f), new Vector3(0.26f, 0.18f, 0.34f), body, false, 0.6f);
                Prims.Box(t, "Selle", new Vector3(0f, 0.9f, -0.3f), new Vector3(0.3f, 0.1f, 0.9f), Black);
                Prims.Box(t, "PorteBagages", new Vector3(0f, 0.95f, -0.82f), new Vector3(0.3f, 0.03f, 0.3f), Black);
                Prims.Tube(t, "FourcheG", new Vector3(0.09f, 1.0f, 0.55f), new Vector3(0.09f, r, 0.65f), 0.045f, Chrome);
                Prims.Tube(t, "FourcheD", new Vector3(-0.09f, 1.0f, 0.55f), new Vector3(-0.09f, r, 0.65f), 0.045f, Chrome);
                Prims.Tube(t, "Guidon", new Vector3(-0.36f, 1.12f, 0.5f), new Vector3(0.36f, 1.12f, 0.5f), 0.03f, Black);
                Prims.Box(t, "Carenage", new Vector3(0f, 1.05f, 0.62f), new Vector3(0.26f, 0.22f, 0.15f), body, false, 0.6f);
                Prims.Box(t, "Phare", new Vector3(0f, 1.05f, 0.7f), new Vector3(0.16f, 0.11f, 0.03f), new Color(0.9f, 0.9f, 0.85f));
                Prims.Box(t, "GardeBoueAV", new Vector3(0f, r * 2f + 0.03f, 0.66f), new Vector3(0.13f, 0.03f, 0.4f), body, false, 0.6f);
                GameObject rider = BuildHuman(t, new Vector3(0f, 0.92f, -0.05f), "chauffeur", true, seed, rnd.Next(3) == 0);
                HumanLibrary.SetupRider(rider, Anchor(t, "MainGauche", new Vector3(-0.3f, 1.12f, 0.48f)), Anchor(t, "MainDroite", new Vector3(0.3f, 1.12f, 0.48f)),
                    Anchor(t, "PiedGauche", new Vector3(-0.17f, 0.42f, 0.08f)), Anchor(t, "PiedDroit", new Vector3(0.17f, 0.42f, 0.08f)), 10f);
                HumanLibrary.MakeStatic(rider, false);
                if (rnd.Next(2) == 0)
                {
                    GameObject pass = BuildHuman(t, new Vector3(0f, 0.95f, -0.45f), rnd.Next(2) == 0 ? "pagne" : "civil", true, seed + 3, false);
                    HumanLibrary.SetupRider(pass, null, null, Anchor(t, "PiedPassagerG", new Vector3(-0.2f, 0.45f, -0.36f)), Anchor(t, "PiedPassagerD", new Vector3(0.2f, 0.45f, -0.36f)), 0f);
                    HumanLibrary.MakeStatic(pass, false);
                }
                brakeLights.Add(Lamp(t, "Stop", new Vector3(0f, 0.85f, -0.95f), new Vector3(0.12f, 0.06f, 0.04f), RedLight));
                AddTrafficBody(root, new Vector3(0.6f, 1.6f, 2.0f), new Vector3(0f, 0.8f, 0f));
                return root;
            }

            // Carrosseries lisses (CarBodies.cs) ; les volumes simples ci-dessous restent en secours
            if (UseSmoothBodies) return BuildSmoothTraffic(root, type, rnd, seed, body, out length, brakeLights, hazards, wheels);

            float lowerH = type == AIVehicleType.TaxiJaune || type == AIVehicleType.Berline ? 0.62f : H * 0.55f;
            Prims.Box(t, "Caisse", new Vector3(0f, r + lowerH * 0.5f, 0f), new Vector3(W, lowerH, L), body, false, 0.45f);
            if (type == AIVehicleType.TaxiJaune || type == AIVehicleType.Berline)
            {
                Prims.Box(t, "Habitacle", new Vector3(0f, r + lowerH + 0.25f, -0.15f), new Vector3(W * 0.9f, 0.5f, L * 0.55f), Glass, false, 0.8f);
                Prims.Box(t, "Pavillon", new Vector3(0f, r + lowerH + 0.52f, -0.15f), new Vector3(W * 0.88f, 0.05f, L * 0.5f), body, false, 0.45f);
                if (type == AIVehicleType.TaxiJaune)
                {
                    string num = "B " + (1000 + rnd.Next(9000));
                    GameObject l = Prims.Label(t, num, new Vector3(W * 0.5f + 0.01f, r + 0.35f, 0.2f), 0.03f, Black, false);
                    l.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
                    GameObject l2 = Prims.Label(t, num, new Vector3(-W * 0.5f - 0.01f, r + 0.35f, 0.2f), 0.03f, Black, false);
                    l2.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                }
            }
            else
            {
                float upperH = H - r - lowerH;
                Prims.Box(t, "Haut", new Vector3(0f, r + lowerH + upperH * 0.5f, 0f), new Vector3(W, upperH, L), body, false, 0.45f);
                Prims.Box(t, "VitresG", new Vector3(-W * 0.5f - 0.005f, r + lowerH + upperH * 0.45f, 0.1f), new Vector3(0.01f, upperH * 0.55f, L * 0.82f), Glass, false, 0.8f);
                Prims.Box(t, "VitresD", new Vector3(W * 0.5f + 0.005f, r + lowerH + upperH * 0.45f, 0.1f), new Vector3(0.01f, upperH * 0.55f, L * 0.82f), Glass, false, 0.8f);
                Prims.Box(t, "ParebriseBus", new Vector3(0f, r + lowerH + upperH * 0.45f, L * 0.5f + 0.005f), new Vector3(W * 0.9f, upperH * 0.6f, 0.01f), Glass, false, 0.8f);
                if (type == AIVehicleType.BusTransco || type == AIVehicleType.MinibusBlanc)
                {
                    Color[] stripes = type == AIVehicleType.BusTransco
                        ? new[] { new Color(0f, 0.5f, 1f), new Color(0.97f, 0.8f, 0.1f), new Color(0.85f, 0.1f, 0.1f) }
                        : new[] { new Color(0.9f, 0.2f, 0.2f), new Color(0.98f, 0.6f, 0.1f), new Color(0.98f, 0.85f, 0.1f), new Color(0.2f, 0.7f, 0.3f), new Color(0.2f, 0.4f, 0.9f) };
                    for (int i = 0; i < stripes.Length; i++)
                    {
                        float y = r + lowerH * 0.35f + i * 0.09f;
                        Prims.Box(t, "Bande" + i, new Vector3(-W * 0.5f - 0.008f, y, 0f), new Vector3(0.01f, 0.07f, L * 0.9f), stripes[i]);
                        Prims.Box(t, "BandeD" + i, new Vector3(W * 0.5f + 0.008f, y, 0f), new Vector3(0.01f, 0.07f, L * 0.9f), stripes[i]);
                    }
                    string sign = type == AIVehicleType.BusTransco ? "L01" : "TRANSPORT EN COMMUN";
                    GameObject s = Prims.Label(t, sign, new Vector3(0f, H - 0.2f, L * 0.5f + 0.02f), 0.035f, new Color(1f, 0.6f, 0.1f), false);
                    s.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                }
                if (type == AIVehicleType.MinibusJaune)
                {
                    for (int i = 0; i < 4; i++)
                        Prims.Box(t, "Rouille" + i, new Vector3((rnd.Next(2) == 0 ? -1f : 1f) * (W * 0.5f + 0.006f), r + 0.15f + (float)rnd.NextDouble() * lowerH, ((float)rnd.NextDouble() - 0.5f) * L * 0.8f), new Vector3(0.01f, 0.15f, 0.25f), new Color(0.45f, 0.25f, 0.1f));
                    GameObject n = Prims.Label(t, "208", new Vector3(0f, r + lowerH * 0.7f, -L * 0.5f - 0.02f), 0.05f, Black, false);
                    n.transform.localRotation = Quaternion.identity;
                }
                if (type == AIVehicleType.PoliceKabasele)
                {
                    Prims.Box(t, "BandeRouge", new Vector3(-W * 0.5f - 0.008f, r + lowerH * 0.8f, 0f), new Vector3(0.01f, 0.08f, L * 0.92f), new Color(0.8f, 0.05f, 0.05f));
                    Prims.Box(t, "BandeRougeD", new Vector3(W * 0.5f + 0.008f, r + lowerH * 0.8f, 0f), new Vector3(0.01f, 0.08f, L * 0.92f), new Color(0.8f, 0.05f, 0.05f));
                    Prims.Box(t, "BandeJaune", new Vector3(-W * 0.5f - 0.008f, r + 0.12f, 0f), new Vector3(0.01f, 0.06f, L * 0.92f), new Color(0.95f, 0.8f, 0.1f));
                    Prims.Box(t, "BandeJauneD", new Vector3(W * 0.5f + 0.008f, r + 0.12f, 0f), new Vector3(0.01f, 0.06f, L * 0.92f), new Color(0.95f, 0.8f, 0.1f));
                    GameObject k1 = Prims.Label(t, "KABASELE", new Vector3(-W * 0.5f - 0.02f, r + lowerH * 0.45f, 0f), 0.06f, Color.white, false);
                    k1.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                    GameObject k2 = Prims.Label(t, "KABASELE", new Vector3(W * 0.5f + 0.02f, r + lowerH * 0.45f, 0f), 0.06f, Color.white, false);
                    k2.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
                    Prims.Box(t, "Rampe", new Vector3(0f, H + 0.08f, L * 0.3f), new Vector3(W * 0.7f, 0.14f, 0.3f), Color.white);
                    hazards.Add(Lamp(t, "GyroRouge", new Vector3(-W * 0.22f, H + 0.17f, L * 0.3f), new Vector3(0.3f, 0.08f, 0.25f), new Color(0.6f, 0.05f, 0.05f)));
                    hazards.Add(Lamp(t, "GyroBleu", new Vector3(W * 0.22f, H + 0.17f, L * 0.3f), new Vector3(0.3f, 0.08f, 0.25f), new Color(0.05f, 0.1f, 0.6f)));
                    GameObject pl = Prims.Label(t, "POLICE", new Vector3(0f, H + 0.08f, L * 0.3f + 0.16f), 0.03f, Black, false);
                    pl.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    Prims.Box(t, "ParBuffle", new Vector3(0f, r + 0.4f, L * 0.5f + 0.12f), new Vector3(W * 0.8f, 0.45f, 0.06f), Chrome, false, 0.7f);
                }
                if (type == AIVehicleType.CamionRemorque)
                {
                    Prims.Box(t, "Conteneur", new Vector3(0f, r + lowerH + 1.0f, -1.2f), new Vector3(W, 2.0f, L * 0.72f), new Color(0.15f, 0.35f, 0.6f));
                }
            }
            // Roues et feux
            float zFront = L * 0.5f - Mathf.Max(0.7f, L * 0.18f), zRear = -L * 0.5f + Mathf.Max(0.8f, L * 0.2f);
            foreach (float sx in new[] { -1f, 1f })
            {
                wheels.Add(TrafficWheel(t, new Vector3(sx * W * 0.45f, r, zFront), r, 0.22f));
                wheels.Add(TrafficWheel(t, new Vector3(sx * W * 0.45f, r, zRear), r, 0.24f));
                brakeLights.Add(Lamp(t, "Stop", new Vector3(sx * W * 0.4f, r + lowerH * 0.6f, -L * 0.5f - 0.02f), new Vector3(0.18f, 0.12f, 0.03f), RedLight));
                hazards.Add(Lamp(t, "Warning", new Vector3(sx * W * 0.47f, r + lowerH * 0.85f, -L * 0.5f - 0.02f), new Vector3(0.08f, 0.08f, 0.03f), ColorUtil.Shade(Amber, 0.3f)));
                Prims.Box(t, "Phare", new Vector3(sx * W * 0.36f, r + lowerH * 0.55f, L * 0.5f + 0.01f), new Vector3(0.2f, 0.13f, 0.02f), new Color(0.85f, 0.85f, 0.8f));
            }
            if (type != AIVehicleType.CamionRemorque)
                BuildHuman(t, new Vector3(-W * 0.22f, r + lowerH * 0.55f, L * 0.5f - (type == AIVehicleType.BusTransco ? 1.0f : 1.4f)), type == AIVehicleType.PoliceKabasele ? "police" : "civil", true, seed + 1, false);
            AddTrafficBody(root, new Vector3(W, H - r * 0.5f, L), new Vector3(0f, r * 0.5f + (H - r * 0.5f) * 0.5f, 0f));
            return root;
        }

        private static Transform TrafficWheel(Transform parent, Vector3 pos, float r, float w)
        {
            GameObject g = new GameObject("Roue");
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            Prims.CylX(g.transform, "Pneu", Vector3.zero, r * 2f, w, Black);
            Prims.CylX(g.transform, "Jante", Vector3.zero, r * 1.2f, w * 1.05f, Silver);
            return g.transform;
        }

        private static void AddTrafficBody(GameObject root, Vector3 size, Vector3 center)
        {
            BoxCollider c = root.AddComponent<BoxCollider>();
            c.size = size;
            c.center = center;
            Rigidbody rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }
    }
}
