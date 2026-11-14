// BALULA MOTO — véhicules construits à partir des modèles fournis par MATULA DESIGN.
// Bus TRANSCO : fichier Resources/BalulaMoto/Vehicules/BusTransco.obj, converti du FBX d'origine par
// scripts/convertir_bus.py (avant vers +Z, roues au sol, roues/phares/feux séparés). Design commercial respecté,
// sans prétendre à un partenariat officiel avec TRANSCO.
// Tricycle taxi (type Bajaj RE) : fichier Resources/BalulaMoto/Vehicules/BajajRE.obj, modélisé par
// scripts/generer_tricycle.py d'après les photos de référence de MATULA DESIGN (version bleue à bâche blanche, versions
// blanche et rouge à bâche noire). Tricycle du joueur « Bajaj RE » et tous les tricycles de la circulation. Design
// commercial respecté, sans logo et sans prétendre à un partenariat officiel avec Bajaj.
using System.Collections.Generic;
using BalulaMoto.Core;
using UnityEngine;

namespace BalulaMoto.Runtime
{
    public static partial class VehicleBuilder
    {
        private static GameObject transcoModel;
        private static bool transcoTried;

        /// <summary>Modèle du bus TRANSCO (null s'il manque : le bus dessiné par le jeu sert alors de secours).</summary>
        public static GameObject TranscoModel
        {
            get
            {
                if (!transcoTried)
                {
                    transcoTried = true;
                    transcoModel = Resources.Load<GameObject>("BalulaMoto/Vehicules/BusTransco");
                    if (transcoModel == null) Debug.LogWarning("[BalulaMoto] Modèle du bus TRANSCO absent : bus dessiné par le jeu.");
                }
                return transcoModel;
            }
        }

        private static Material MapTranscoMaterial(Material src, Material white, Material glass, Material dark, Material chrome, Material rubber)
        {
            string n = src != null ? src.name : "";
            if (n.Contains("Paint")) return white;
            if (n.Contains("Glass")) return glass;
            if (n.Contains("Rubber")) return rubber;
            if (n.Contains("Silver")) return chrome;
            if (n.Contains("Stripe_Blue")) return Paint(new Color(0.02f, 0.2f, 0.72f), 0.1f, 0.6f);
            if (n.Contains("Stripe_Red")) return Paint(new Color(0.82f, 0.025f, 0.035f), 0.1f, 0.6f);
            if (n.Contains("Stripe_Yellow")) return Paint(new Color(0.98f, 0.76f, 0.02f), 0.1f, 0.6f);
            if (n.Contains("Light")) return Paint(new Color(0.85f, 0.88f, 0.9f), 0.1f, 0.9f);
            if (n.Contains("Dark")) return dark;
            return src != null ? Paint(src.color, 0.1f, 0.5f) : dark;
        }

        private static void ApplyMatrix(Transform t, Matrix4x4 m)
        {
            t.localPosition = m.GetColumn(3);
            t.localRotation = m.rotation;
            t.localScale = m.lossyScale;
        }

        /// <summary>Bus TRANSCO à partir du modèle fourni. Retourne null si le modèle est absent.</summary>
        private static GameObject BuildModelBus(GameObject root, out float length, List<Renderer> brakeLights, List<Renderer> hazards, List<Transform> wheels)
        {
            length = 11.5f;
            GameObject model = TranscoModel;
            if (model == null) return null;
            Transform t = root.transform;
            Matrix4x4 toModel = model.transform.worldToLocalMatrix;
            Material white = Paint(new Color(0.9f, 0.91f, 0.9f), 0.15f);
            Material glass = CarGlass();
            Material dark = Paint(new Color(0.07f, 0.08f, 0.09f), 0f, 0.3f);
            Material chrome = Paint(new Color(0.78f, 0.8f, 0.82f), 0.9f, 0.85f);
            Material rubber = Paint(new Color(0.03f, 0.03f, 0.035f), 0f, 0.2f);
            Dictionary<string, Transform> pivots = new Dictionary<string, Transform>();
            int parts = 0;
            foreach (MeshFilter src in model.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = src.sharedMesh;
                if (mesh == null) continue;
                MeshRenderer smr = src.GetComponent<MeshRenderer>();
                Material[] srcMats = smr != null ? smr.sharedMaterials : new Material[0];
                Material[] mats = new Material[Mathf.Max(1, mesh.subMeshCount)];
                for (int i = 0; i < mats.Length; i++) mats[i] = MapTranscoMaterial(i < srcMats.Length ? srcMats[i] : null, white, glass, dark, chrome, rubber);
                Matrix4x4 m = toModel * src.transform.localToWorldMatrix;
                string n = src.name;
                parts++;
                if (n.StartsWith("Roue_") && n.Length > 8)
                {
                    // Roue : pivot au moyeu (elle tourne autour de l'axe X, la largeur du bus) ; pneu et jante dedans
                    string key = n.Substring(5, 3);
                    Transform pivot;
                    if (!pivots.TryGetValue(key, out pivot))
                    {
                        GameObject pg = new GameObject("Roue");
                        pg.transform.SetParent(t, false);
                        pg.transform.localPosition = m.MultiplyPoint3x4(mesh.bounds.center);
                        pivot = pg.transform;
                        pivots[key] = pivot;
                        wheels.Add(pivot);
                    }
                    bool rim = n.EndsWith("Jante");
                    GameObject part = MeshPart(pivot, rim ? "Jante" : "Pneu", mesh, mats, Vector3.zero);
                    ApplyMatrix(part.transform, Matrix4x4.Translate(-pivot.localPosition) * m);
                    continue;
                }
                if (n.StartsWith("Phare"))
                {
                    GameObject hl = MeshPart(t, "Phare", mesh, new[] { Paint(new Color(0.92f, 0.92f, 0.86f), 0.1f, 0.95f) }, Vector3.zero);
                    ApplyMatrix(hl.transform, m);
                    continue;
                }
                if (n.StartsWith("Stop"))
                {
                    GameObject st = MeshPart(t, "Stop", mesh, new[] { Mats.Lit(RedLight, 0.5f) }, Vector3.zero);
                    ApplyMatrix(st.transform, m);
                    brakeLights.Add(st.GetComponent<Renderer>());
                    continue;
                }
                if (n.StartsWith("Cligno"))
                {
                    GameObject cl = MeshPart(t, "Warning", mesh, new[] { Mats.Lit(ColorUtil.Shade(Amber, 0.3f), 0.5f) }, Vector3.zero);
                    ApplyMatrix(cl.transform, m);
                    hazards.Add(cl.GetComponent<Renderer>());
                    continue;
                }
                GameObject body = MeshPart(t, n, mesh, mats, Vector3.zero);
                ApplyMatrix(body.transform, m);
            }
            if (parts == 0 || wheels.Count == 0)
            {
                // Modèle incomplet : on retire ce qui a été posé, le bus dessiné par le jeu prend le relais
                for (int i = t.childCount - 1; i >= 0; i--) Object.Destroy(t.GetChild(i).gameObject);
                brakeLights.Clear();
                hazards.Clear();
                wheels.Clear();
                return null;
            }
            // Caisse : 11,5 m de long, 2,5 m de large, du bas de caisse (0,3 m) au toit (3,4 m)
            AddTrafficBody(root, new Vector3(2.5f, 3.1f, 11.45f), new Vector3(0f, 1.86f, 0f));
            return root;
        }

        // ==================================================================
        // Tricycle taxi (type Bajaj RE) modélisé d'après les photos de MATULA DESIGN
        // ==================================================================
        private static GameObject bajajModel;
        private static bool bajajTried;

        /// <summary>Modèle du tricycle (null s'il manque : le tricycle dessiné par le jeu sert alors de secours).</summary>
        public static GameObject BajajModel
        {
            get
            {
                if (!bajajTried)
                {
                    bajajTried = true;
                    bajajModel = Resources.Load<GameObject>("BalulaMoto/Vehicules/BajajRE");
                    if (bajajModel == null) Debug.LogWarning("[BalulaMoto] Modèle du tricycle absent : tricycle dessiné par le jeu.");
                }
                return bajajModel;
            }
        }

        // Places dans le modèle (hanches sur l'assise, à 0,60 m) : banquette avant (chauffeur au milieu, 2 places à côté de
        // lui, comme à Kinshasa), banquette arrière (3 places) au-dessus du moteur
        private static readonly Vector3 BajajDriverSeat = new Vector3(0f, 0.6f, -0.04f);
        private static readonly Vector3[] BajajFrontSeats = { new Vector3(-0.37f, 0.6f, -0.04f), new Vector3(0.37f, 0.6f, -0.04f) };
        private static readonly Vector3[] BajajRearSeats = { new Vector3(-0.36f, 0.6f, -0.86f), new Vector3(0f, 0.6f, -0.86f), new Vector3(0.36f, 0.6f, -0.86f) };
        private static readonly Vector3 BajajHandlebar = new Vector3(0f, 1.0f, 0.45f);
        // Essuie-glace monté en haut du pare-brise (axe perpendiculaire à la vitre, inclinée de 12,8°)
        private static readonly Vector3 BajajWiperPivot = new Vector3(-0.18f, 1.474f, 0.795f);
        private static readonly Quaternion BajajWiperTilt = Quaternion.Euler(-12.8f, 0f, 0f);
        // Fourche avant (garde-boue, bras) : tourne autour de l'axe vertical de la roue avant
        private static readonly Vector3 BajajFrontAxle = new Vector3(0f, 0.24f, 1.1f);
        private const float BajajWheelRadius = 0.24f;
        // Couleurs des photos : bleu à bâche blanche ; versions blanche et rouge à bâche noire
        public static readonly Color BajajBlue = new Color(0.11f, 0.53f, 0.86f);
        public static readonly Color BajajCanvas = new Color(0.89f, 0.875f, 0.85f);
        private static readonly Color BajajWhite = new Color(0.92f, 0.92f, 0.91f);
        private static readonly Color BajajRed = new Color(0.8f, 0.04f, 0.035f);
        private static readonly Color BajajBlackCanvas = new Color(0.1f, 0.1f, 0.11f);
        private static readonly Color BajajSeatLight = new Color(0.9f, 0.89f, 0.87f);
        private static readonly Color BajajSeatDark = new Color(0.12f, 0.12f, 0.13f);

        private class BajajParts
        {
            public readonly Dictionary<string, Renderer> Lamps = new Dictionary<string, Renderer>();
            public readonly List<Transform> Wheels = new List<Transform>();   // avant, arrière gauche, arrière droite
            public Transform Wiper;
            public Transform Fork;
            public Vector3 PlatePos;
            public bool HasPlate;
        }

        private static Material MapBajajMaterial(Material src, Color body, Color canvas, Color seat, Color rim)
        {
            string n = src != null ? src.name : "";
            if (n.Contains("Carrosserie")) return Paint(body, 0.25f, 0.72f);
            if (n.Contains("Bache")) return Paint(canvas, 0f, 0.2f);
            if (n.Contains("Siege")) return Paint(seat, 0f, 0.25f);
            if (n.Contains("Vitre")) return CarGlass();
            if (n.Contains("Pneu")) return Paint(new Color(0.08f, 0.085f, 0.09f), 0f, 0.1f);
            if (n.Contains("Chrome")) return Paint(rim, 0.78f, 0.75f);
            if (n.Contains("Phare")) return Paint(new Color(0.93f, 0.94f, 0.95f), 0.1f, 0.92f);
            if (n.Contains("Feu_Rouge")) return Mats.Lit(RedLight, 0.5f);
            if (n.Contains("Orange")) return Mats.Lit(ColorUtil.Shade(Amber, 0.3f), 0.5f);
            if (n.Contains("Plaque")) return Paint(new Color(1f, 0.83f, 0.13f), 0f, 0.6f);
            if (n.Contains("Cadran")) return Paint(new Color(0.93f, 0.93f, 0.9f), 0f, 0.7f);
            if (n.Contains("Tapis")) return Paint(new Color(0.05f, 0.05f, 0.055f), 0f, 0.1f);
            if (n.Contains("Grille")) return Paint(new Color(0.1f, 0.105f, 0.11f), 0.1f, 0.3f);
            return Paint(new Color(0.075f, 0.08f, 0.085f), 0.05f, 0.45f);   // plastique noir
        }

        /// <summary>Pose les pièces du modèle sous « parent » : caisse, roues (pivot au moyeu), fourche (pivot de direction),
        /// feux, essuie-glace (pivot dans le plan du pare-brise).</summary>
        private static BajajParts PlaceBajaj(GameObject model, Transform parent, Color body, Color canvas, Color seat, Color rim)
        {
            BajajParts res = new BajajParts();
            Matrix4x4 toModel = model.transform.worldToLocalMatrix;
            Dictionary<string, Transform> wheelPivots = new Dictionary<string, Transform>();
            Dictionary<string, Material> matCache = new Dictionary<string, Material>();
            foreach (MeshFilter src in model.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = src.sharedMesh;
                if (mesh == null) continue;
                MeshRenderer smr = src.GetComponent<MeshRenderer>();
                Material[] srcMats = smr != null ? smr.sharedMaterials : new Material[0];
                Material[] mats = new Material[Mathf.Max(1, mesh.subMeshCount)];
                for (int i = 0; i < mats.Length; i++)
                {
                    Material sm = i < srcMats.Length ? srcMats[i] : null;
                    string key = sm != null ? sm.name : "";
                    Material m;
                    if (!matCache.TryGetValue(key, out m))
                    {
                        m = MapBajajMaterial(sm, body, canvas, seat, rim);
                        matCache[key] = m;
                    }
                    mats[i] = m;
                }
                Matrix4x4 mm = toModel * src.transform.localToWorldMatrix;
                string n = src.name;
                if (n.StartsWith("Roue_") && n.LastIndexOf('_') > 5)
                {
                    // Roue : pivot au moyeu, elle tourne autour de l'axe X ; pneu puis jante
                    string key = n.Substring(0, n.LastIndexOf('_'));
                    Transform pivot;
                    if (!wheelPivots.TryGetValue(key, out pivot))
                    {
                        Vector3 c = mm.MultiplyPoint3x4(mesh.bounds.center);
                        GameObject pg = new GameObject(key);
                        pg.transform.SetParent(parent, false);
                        pg.transform.localPosition = new Vector3(c.x, BajajWheelRadius, c.z);
                        pivot = pg.transform;
                        wheelPivots[key] = pivot;
                    }
                    GameObject part = MeshPart(pivot, n.EndsWith("Jante") ? "Jante" : "Pneu", mesh, mats, Vector3.zero);
                    ApplyMatrix(part.transform, Matrix4x4.Translate(-pivot.localPosition) * mm);
                    continue;
                }
                if (n.StartsWith("Fourche"))
                {
                    if (res.Fork == null)
                    {
                        GameObject fg = new GameObject("FourcheAvant");
                        fg.transform.SetParent(parent, false);
                        fg.transform.localPosition = BajajFrontAxle;
                        res.Fork = fg.transform;
                    }
                    GameObject part = MeshPart(res.Fork, n, mesh, mats, Vector3.zero);
                    ApplyMatrix(part.transform, Matrix4x4.Translate(-BajajFrontAxle) * mm);
                    continue;
                }
                if (n.StartsWith("Essuie"))
                {
                    GameObject pv = new GameObject("EssuieGlace");
                    pv.transform.SetParent(parent, false);
                    pv.transform.localPosition = BajajWiperPivot;
                    pv.transform.localRotation = BajajWiperTilt;
                    GameObject part = MeshPart(pv.transform, "Balai", mesh, mats, Vector3.zero);
                    ApplyMatrix(part.transform, Matrix4x4.Rotate(Quaternion.Inverse(BajajWiperTilt)) * Matrix4x4.Translate(-BajajWiperPivot) * mm);
                    res.Wiper = pv.transform;
                    continue;
                }
                if (n.StartsWith("Phare") || n.StartsWith("Cligno") || n.StartsWith("Feu"))
                {
                    // Les phares s'appellent « Phare » : la circulation les reconnaît et les allume la nuit
                    GameObject lp = MeshPart(parent, n.StartsWith("Phare") ? "Phare" : n, mesh, mats, Vector3.zero);
                    ApplyMatrix(lp.transform, mm);
                    res.Lamps[n] = lp.GetComponent<Renderer>();
                    continue;
                }
                if (n.StartsWith("Plaque"))
                {
                    res.PlatePos = mm.MultiplyPoint3x4(mesh.bounds.center);
                    res.HasPlate = true;
                }
                GameObject bp = MeshPart(parent, n, mesh, mats, Vector3.zero);
                ApplyMatrix(bp.transform, mm);
            }
            foreach (string k in new[] { "Roue_AV", "Roue_ARG", "Roue_ARD" })
            {
                Transform w;
                if (wheelPivots.TryGetValue(k, out w)) res.Wheels.Add(w);
            }
            return res;
        }

        private static Renderer BajajLamp(BajajParts p, string name)
        {
            Renderer r;
            return p.Lamps.TryGetValue(name, out r) ? r : null;
        }

        /// <summary>Guidon de type moto (le pivot de direction tourne avec la physique), colonne qui rentre dans la planche de bord.</summary>
        private static Transform BajajHandlebarPivot(Transform parent)
        {
            GameObject pivot = new GameObject("Direction");
            pivot.transform.SetParent(parent, false);
            pivot.transform.localPosition = BajajHandlebar;
            Prims.Tube(pivot.transform, "Guidon", new Vector3(-0.33f, 0.03f, -0.02f), new Vector3(0.33f, 0.03f, -0.02f), 0.022f, Black);
            Prims.Tube(pivot.transform, "Potence", new Vector3(0f, 0.03f, -0.02f), new Vector3(0f, -0.1f, 0.2f), 0.03f, Black);
            Prims.Tube(pivot.transform, "PoigneeG", new Vector3(-0.37f, 0.03f, -0.02f), new Vector3(-0.25f, 0.03f, -0.02f), 0.032f, new Color(0.12f, 0.12f, 0.13f));
            Prims.Tube(pivot.transform, "PoigneeD", new Vector3(0.25f, 0.03f, -0.02f), new Vector3(0.37f, 0.03f, -0.02f), 0.032f, new Color(0.12f, 0.12f, 0.13f));
            Prims.Tube(pivot.transform, "Levier", new Vector3(0.22f, 0.045f, 0.0f), new Vector3(0.33f, 0.04f, 0.05f), 0.007f, new Color(0.7f, 0.72f, 0.74f));
            return pivot.transform;
        }

        private static bool IsDark(Color c) { return c.r * 0.3f + c.g * 0.59f + c.b * 0.11f < 0.35f; }

        /// <summary>Tricycle du joueur à partir du modèle. Faux si le modèle est absent (tricycle dessiné en secours).</summary>
        private static bool BuildModelTricycle(VehicleRig rig, VehicleDef d, Color main, Color second, Color rim, bool withDriver)
        {
            GameObject model = BajajModel;
            if (model == null) return false;
            Transform b = rig.Body;
            // Peinture d'origine (bleu et bâche blanche) ; l'ancienne teinte par défaut (noir et jaune) compte comme d'origine
            string mh = ColorUtility.ToHtmlStringRGB(main), sh = ColorUtility.ToHtmlStringRGB(second);
            if (mh == "151515") main = BajajBlue;
            if (sh == "F2B705") second = BajajCanvas;
            BajajParts p = PlaceBajaj(model, b, main, second, IsDark(second) ? BajajSeatDark : BajajSeatLight, rim);
            if (p.Wheels.Count < 3)
            {
                for (int i = b.childCount - 1; i >= 0; i--) Object.Destroy(b.GetChild(i).gameObject);
                return false;
            }
            // Roues aux emplacements de la physique (empattement, voie, rayon de la fiche du véhicule)
            float r = d.rayonRoueM, wb = d.empattementM;
            float track = Mathf.Max(0.9f, d.voieArriereM) * 0.5f;
            Vector3[] anchors = { new Vector3(0f, r, wb * 0.55f), new Vector3(-track, r, -wb * 0.45f), new Vector3(track, r, -wb * 0.45f) };
            for (int i = 0; i < 3; i++)
            {
                Transform w = p.Wheels[i];
                w.localPosition = anchors[i];
                w.localScale = Vector3.one * (r / BajajWheelRadius);
                rig.Wheels.Add(w);
            }
            if (p.Fork != null)
            {
                // Garde-boue et bras avant : suivent le braquage de la roue avant
                p.Fork.localPosition = new Vector3(anchors[0].x, BajajFrontAxle.y, anchors[0].z);
                p.Fork.gameObject.AddComponent<SuitBraquage>().Wheel = p.Wheels[0];
            }
            if (p.Wiper != null) p.Wiper.gameObject.AddComponent<Essuieglace>();

            // Places : banquette avant (2 à côté du chauffeur) puis banquette arrière (3)
            for (int i = 0; i < d.placesAvant && i < BajajFrontSeats.Length; i++) rig.Seats.Add(Anchor(b, "PlaceAvant" + i, BajajFrontSeats[i]));
            for (int i = 0; i < d.placesArriere && i < BajajRearSeats.Length; i++) rig.Seats.Add(Anchor(b, "Place", BajajRearSeats[i]));
            rig.CargoAnchor = Anchor(b, "Bagages", new Vector3(0f, 0.68f, -0.86f));

            Transform pivot = BajajHandlebarPivot(b);
            rig.SteerPivot = pivot;
            Renderer lamp;
            if ((lamp = BajajLamp(p, "Phare_G")) != null) rig.HeadLights.Add(lamp);
            if ((lamp = BajajLamp(p, "Phare_D")) != null) rig.HeadLights.Add(lamp);
            rig.HeadLamp = HeadLamp(b, new Vector3(0f, 0.69f, 1.4f));
            if ((lamp = BajajLamp(p, "Feu_ARG")) != null) rig.TailLights.Add(lamp);
            if ((lamp = BajajLamp(p, "Feu_ARD")) != null) rig.TailLights.Add(lamp);
            if ((lamp = BajajLamp(p, "Cligno_AVG")) != null) rig.IndicatorsLeft.Add(lamp);
            if ((lamp = BajajLamp(p, "Cligno_ARG")) != null) rig.IndicatorsLeft.Add(lamp);
            if ((lamp = BajajLamp(p, "Cligno_AVD")) != null) rig.IndicatorsRight.Add(lamp);
            if ((lamp = BajajLamp(p, "Cligno_ARD")) != null) rig.IndicatorsRight.Add(lamp);
            // Plaque d'immatriculation du joueur posée sur la plaque jaune du modèle, autocollant sur le flanc arrière droit
            if (p.HasPlate) rig.PlateAnchor = Anchor(b, "PlaqueJoueur", p.PlatePos + new Vector3(0f, 0f, -0.012f));
            rig.StickerAnchor = Anchor(b, "Autocollant", new Vector3(0.672f, 0.7f, -0.78f));

            rig.CameraFirstPerson = Anchor(b, "CameraPremierePersonne", new Vector3(0f, 1.37f, 0.03f));
            rig.CameraDashboard = Anchor(b, "CameraTableauDeBord", new Vector3(0f, 1.22f, 0.3f));
            if (withDriver)
            {
                rig.Driver = BuildDriver(b, BajajDriverSeat, 11, false);
                SetArmsForward(rig.Driver, 60f);
                HumanLibrary.SetupRider(rig.Driver, Anchor(pivot, "MainGauche", new Vector3(-0.3f, 0.03f, -0.02f)), Anchor(pivot, "MainDroite", new Vector3(0.3f, 0.03f, -0.02f)), null, null, 4f);
            }
            return true;
        }

        /// <summary>Tricycle de la circulation : bleu à bâche blanche (le plus courant), blanc ou rouge à bâche noire ;
        /// chauffeur, parfois des passagers.</summary>
        private static GameObject BuildModelTricycleTraffic(GameObject root, System.Random rnd, int seed, out float length, List<Renderer> brakeLights, List<Renderer> hazards, List<Transform> wheels)
        {
            length = 2.75f;
            GameObject model = BajajModel;
            if (model == null) return null;
            Transform t = root.transform;
            int v = rnd.Next(100);
            Color body = v < 55 ? BajajBlue : (v < 78 ? BajajWhite : BajajRed);
            Color canvas = v < 55 ? BajajCanvas : BajajBlackCanvas;
            BajajParts p = PlaceBajaj(model, t, body, canvas, v < 55 ? BajajSeatLight : BajajSeatDark, Silver);
            if (p.Wheels.Count < 3)
            {
                for (int i = t.childCount - 1; i >= 0; i--) Object.Destroy(t.GetChild(i).gameObject);
                return null;
            }
            wheels.AddRange(p.Wheels);
            foreach (KeyValuePair<string, Renderer> kv in p.Lamps)
            {
                if (kv.Key.StartsWith("Feu")) brakeLights.Add(kv.Value);
                else if (kv.Key.StartsWith("Cligno")) hazards.Add(kv.Value);
            }
            Transform pivot = BajajHandlebarPivot(t);
            GameObject drv = BuildHuman(t, BajajDriverSeat, "chauffeur", true, seed, false);
            HumanLibrary.SetupRider(drv, Anchor(pivot, "MainGauche", new Vector3(-0.3f, 0.03f, -0.02f)), Anchor(pivot, "MainDroite", new Vector3(0.3f, 0.03f, -0.02f)),
                Anchor(t, "PiedGauche", new Vector3(-0.16f, 0.37f, 0.42f)), Anchor(t, "PiedDroit", new Vector3(0.16f, 0.37f, 0.42f)), 4f);
            HumanLibrary.MakeStatic(drv, true);
            if (GameSettings.Quality > 0)
            {
                int n = rnd.Next(3);   // 0, 1 ou 2 passagers à l'arrière
                for (int i = 0; i < n; i++)
                {
                    Vector3 seat = BajajRearSeats[(i * 2 + rnd.Next(2)) % BajajRearSeats.Length];
                    GameObject ps = BuildHuman(t, seat, rnd.Next(2) == 0 ? "pagne" : "civil", true, seed + 5 + i, false);
                    HumanLibrary.MakeStatic(ps, true);
                }
            }
            // Caisse : 1,32 m de large, 2,74 m de long, du bas de caisse au toit (1,71 m)
            AddTrafficBody(root, new Vector3(1.32f, 1.62f, 2.7f), new Vector3(0f, 0.9f, 0f));
            return root;
        }
    }

    /// <summary>Essuie-glace monté en haut du pare-brise : balaie la vitre quand il pleut, revient au repos sinon.
    /// Le balai est modélisé au repos (penché vers la droite) ; il pivote dans le plan de la vitre vers la gauche.</summary>
    public class Essuieglace : MonoBehaviour
    {
        public float Sweep = -66f;
        private float phase;
        private float angle;
        private Quaternion baseRot;

        private void Awake() { baseRot = transform.localRotation; }

        private void Update()
        {
            BalulaGame g = BalulaGame.Instance;
            bool rain = g != null && g.Env != null && g.Env.IsRaining;
            if (rain)
            {
                phase += Time.deltaTime * 0.8f;
                angle = Sweep * 0.5f * (1f - Mathf.Cos(phase * Mathf.PI * 2f));
            }
            else
            {
                phase = 0f;
                angle = Mathf.MoveTowards(angle, 0f, Time.deltaTime * 120f);
            }
            transform.localRotation = baseRot * Quaternion.Euler(0f, 0f, angle);
        }
    }

    /// <summary>Garde-boue et bras avant du tricycle : tournent avec la roue avant (braquage lu sur la roue).</summary>
    public class SuitBraquage : MonoBehaviour
    {
        public Transform Wheel;

        private void LateUpdate()
        {
            if (Wheel == null) return;
            Vector3 r = Wheel.localRotation * Vector3.right;
            float steer = Mathf.Atan2(-r.z, r.x) * Mathf.Rad2Deg;
            transform.localRotation = Quaternion.Euler(0f, steer, 0f);
        }
    }
}
