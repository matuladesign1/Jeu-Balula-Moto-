// BALULA MOTO — véhicules construits à partir des modèles fournis par MATULA DESIGN.
// Bus TRANSCO : fichier Resources/BalulaMoto/Vehicules/BusTransco.obj, converti du FBX d'origine par
// scripts/convertir_bus.py (avant vers +Z, roues au sol, roues/phares/feux séparés). Design commercial respecté,
// sans prétendre à un partenariat officiel avec TRANSCO.
// Tricycle Bajaj RE : fichier Resources/BalulaMoto/Vehicules/BajajRE.obj, construit par scripts/generer_bajaj_re.py
// d'après le script Blender de MATULA DESIGN (hauteur ramenée à celle du vrai Bajaj RE, intérieur aménagé pour le
// chauffeur et les passagers). Tricycle du joueur « Bajaj RE » et tous les tricycles de la circulation. Design
// commercial respecté, sans prétendre à un partenariat officiel avec Bajaj.
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
        // Tricycle Bajaj RE de MATULA DESIGN
        // ==================================================================
        private static GameObject bajajModel;
        private static bool bajajTried;

        /// <summary>Modèle du Bajaj RE (null s'il manque : le tricycle dessiné par le jeu sert alors de secours).</summary>
        public static GameObject BajajModel
        {
            get
            {
                if (!bajajTried)
                {
                    bajajTried = true;
                    bajajModel = Resources.Load<GameObject>("BalulaMoto/Vehicules/BajajRE");
                    if (bajajModel == null) Debug.LogWarning("[BalulaMoto] Modèle du Bajaj RE absent : tricycle dessiné par le jeu.");
                }
                return bajajModel;
            }
        }

        // Places dans le modèle (hanches sur l'assise) : banquette avant (chauffeur au milieu, 2 places à côté de lui,
        // comme à Kinshasa), banquette arrière (3 places)
        private static readonly Vector3 BajajDriverSeat = new Vector3(0f, 0.6f, -0.075f);
        private static readonly Vector3[] BajajFrontSeats = { new Vector3(-0.39f, 0.6f, -0.075f), new Vector3(0.39f, 0.6f, -0.075f) };
        private static readonly Vector3[] BajajRearSeats = { new Vector3(-0.33f, 0.61f, -0.655f), new Vector3(0f, 0.61f, -0.655f), new Vector3(0.33f, 0.61f, -0.655f) };
        private static readonly Vector3 BajajHandlebar = new Vector3(0f, 1.04f, 0.36f);
        private static readonly Vector3 BajajWiperPivot = new Vector3(0f, 1.036f, 0.64f);
        private const float BajajWheelRadius = 0.24f;
        // Couleurs du script Blender (bleu de référence, bâche crème), converties pour Unity
        public static readonly Color BajajBlue = new Color(0.128f, 0.506f, 0.809f);
        public static readonly Color BajajCanvas = new Color(0.887f, 0.872f, 0.835f);

        private class BajajParts
        {
            public readonly Dictionary<string, Renderer> Lamps = new Dictionary<string, Renderer>();
            public readonly List<Transform> Wheels = new List<Transform>();   // avant, arrière gauche, arrière droite
            public Transform Wiper;
            public Vector3 PlatePos;
            public bool HasPlate;
        }

        private static Material MapBajajMaterial(Material src, Color body, Color canvas, Color rim)
        {
            string n = src != null ? src.name : "";
            if (n.Contains("Body_Blue")) return Paint(body, 0.25f, 0.72f);
            if (n.Contains("Canopy")) return Paint(canvas, 0f, 0.2f);
            if (n.Contains("Glass")) return CarGlass();
            if (n.Contains("Tyre")) return Paint(new Color(0.08f, 0.085f, 0.09f), 0f, 0.1f);
            if (n.Contains("Metal")) return Paint(rim, 0.78f, 0.75f);
            if (n.Contains("Seat")) return Paint(new Color(0.79f, 0.78f, 0.76f), 0f, 0.15f);
            if (n.Contains("Plate")) return Paint(new Color(1f, 0.83f, 0.13f), 0f, 0.6f);
            if (n.Contains("Headlamp")) return Paint(new Color(0.91f, 0.93f, 0.91f), 0.1f, 0.9f);
            if (n.Contains("Tail")) return Mats.Lit(RedLight, 0.5f);
            if (n.Contains("Amber")) return Mats.Lit(ColorUtil.Shade(Amber, 0.3f), 0.5f);
            if (n.Contains("Bumper")) return Paint(new Color(0.21f, 0.22f, 0.25f), 0.1f, 0.6f);
            return src != null ? Paint(src.color, 0.1f, 0.5f) : Paint(new Color(0.21f, 0.22f, 0.25f), 0.1f, 0.6f);
        }

        /// <summary>Pose les pièces du modèle sous « parent » : caisse, roues (pivot au moyeu), feux, essuie-glace (pivot).</summary>
        private static BajajParts PlaceBajaj(GameObject model, Transform parent, Color body, Color canvas, Color rim)
        {
            BajajParts res = new BajajParts();
            Matrix4x4 toModel = model.transform.worldToLocalMatrix;
            Dictionary<string, Transform> wheelPivots = new Dictionary<string, Transform>();
            foreach (MeshFilter src in model.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = src.sharedMesh;
                if (mesh == null) continue;
                MeshRenderer smr = src.GetComponent<MeshRenderer>();
                Material[] srcMats = smr != null ? smr.sharedMaterials : new Material[0];
                Material[] mats = new Material[Mathf.Max(1, mesh.subMeshCount)];
                for (int i = 0; i < mats.Length; i++) mats[i] = MapBajajMaterial(i < srcMats.Length ? srcMats[i] : null, body, canvas, rim);
                Matrix4x4 m = toModel * src.transform.localToWorldMatrix;
                string n = src.name;
                if (n.StartsWith("Roue_") && n.LastIndexOf('_') > 5)
                {
                    // Roue : pivot au moyeu, elle tourne autour de l'axe X ; pneu (et enjoliveur) puis jante (moyeu)
                    string key = n.Substring(0, n.LastIndexOf('_'));
                    Transform pivot;
                    if (!wheelPivots.TryGetValue(key, out pivot))
                    {
                        GameObject pg = new GameObject(key);
                        pg.transform.SetParent(parent, false);
                        pg.transform.localPosition = m.MultiplyPoint3x4(mesh.bounds.center);
                        pivot = pg.transform;
                        wheelPivots[key] = pivot;
                    }
                    GameObject part = MeshPart(pivot, n.EndsWith("Jante") ? "Jante" : "Pneu", mesh, mats, Vector3.zero);
                    ApplyMatrix(part.transform, Matrix4x4.Translate(-pivot.localPosition) * m);
                    continue;
                }
                if (n.StartsWith("Essuie"))
                {
                    GameObject pv = new GameObject("EssuieGlace");
                    pv.transform.SetParent(parent, false);
                    pv.transform.localPosition = BajajWiperPivot;
                    GameObject part = MeshPart(pv.transform, "Balai", mesh, mats, Vector3.zero);
                    ApplyMatrix(part.transform, Matrix4x4.Translate(-BajajWiperPivot) * m);
                    res.Wiper = pv.transform;
                    continue;
                }
                if (n.StartsWith("Phare") || n.StartsWith("Cligno") || n.StartsWith("Feu"))
                {
                    // Les phares s'appellent « Phare » : la circulation les reconnaît et les allume la nuit
                    GameObject lp = MeshPart(parent, n.StartsWith("Phare") ? "Phare" : n, mesh, mats, Vector3.zero);
                    ApplyMatrix(lp.transform, m);
                    res.Lamps[n] = lp.GetComponent<Renderer>();
                    continue;
                }
                if (n.StartsWith("Plaque"))
                {
                    res.PlatePos = m.MultiplyPoint3x4(mesh.bounds.center);
                    res.HasPlate = true;
                }
                GameObject bp = MeshPart(parent, n, mesh, mats, Vector3.zero);
                ApplyMatrix(bp.transform, m);
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

        /// <summary>Guidon (le pivot de direction tourne avec le volant de la physique), colonne cachée dans la caisse avant.</summary>
        private static Transform BajajHandlebarPivot(Transform parent)
        {
            GameObject pivot = new GameObject("Direction");
            pivot.transform.SetParent(parent, false);
            pivot.transform.localPosition = BajajHandlebar;
            Prims.Tube(pivot.transform, "Guidon", new Vector3(-0.34f, 0f, 0f), new Vector3(0.34f, 0f, 0f), 0.025f, Black);
            Prims.Tube(pivot.transform, "Colonne", new Vector3(0f, -0.3f, 0.08f), Vector3.zero, 0.03f, Black);
            Prims.Tube(pivot.transform, "PoigneeG", new Vector3(-0.36f, 0f, 0f), new Vector3(-0.25f, 0f, 0f), 0.034f, new Color(0.12f, 0.12f, 0.13f));
            Prims.Tube(pivot.transform, "PoigneeD", new Vector3(0.25f, 0f, 0f), new Vector3(0.36f, 0f, 0f), 0.034f, new Color(0.12f, 0.12f, 0.13f));
            return pivot.transform;
        }

        /// <summary>Bajaj RE du joueur à partir du modèle fourni. Faux si le modèle est absent (tricycle dessiné en secours).</summary>
        private static bool BuildModelTricycle(VehicleRig rig, VehicleDef d, Color main, Color second, Color rim, bool withDriver)
        {
            GameObject model = BajajModel;
            if (model == null) return false;
            Transform b = rig.Body;
            // Peinture d'origine (bleu et crème du modèle) ; l'ancienne teinte par défaut (noir et jaune) compte comme d'origine
            string mh = ColorUtility.ToHtmlStringRGB(main), sh = ColorUtility.ToHtmlStringRGB(second);
            if (mh == "151515") main = BajajBlue;
            if (sh == "F2B705") second = BajajCanvas;
            BajajParts p = PlaceBajaj(model, b, main, second, rim);
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
            if (p.Wiper != null) p.Wiper.gameObject.AddComponent<Essuieglace>();

            // Places : banquette avant (2 à côté du chauffeur) puis banquette arrière (3)
            for (int i = 0; i < d.placesAvant && i < BajajFrontSeats.Length; i++) rig.Seats.Add(Anchor(b, "PlaceAvant" + i, BajajFrontSeats[i]));
            for (int i = 0; i < d.placesArriere && i < BajajRearSeats.Length; i++) rig.Seats.Add(Anchor(b, "Place", BajajRearSeats[i]));
            rig.CargoAnchor = Anchor(b, "Bagages", new Vector3(0f, 0.68f, -0.655f));

            Transform pivot = BajajHandlebarPivot(b);
            rig.SteerPivot = pivot;
            Renderer lamp;
            if ((lamp = BajajLamp(p, "Phare_G")) != null) rig.HeadLights.Add(lamp);
            if ((lamp = BajajLamp(p, "Phare_D")) != null) rig.HeadLights.Add(lamp);
            rig.HeadLamp = HeadLamp(b, new Vector3(0f, 0.56f, 1.26f));
            if ((lamp = BajajLamp(p, "Feu_ARG")) != null) rig.TailLights.Add(lamp);
            if ((lamp = BajajLamp(p, "Feu_ARD")) != null) rig.TailLights.Add(lamp);
            if ((lamp = BajajLamp(p, "Cligno_AVG")) != null) rig.IndicatorsLeft.Add(lamp);
            if ((lamp = BajajLamp(p, "Cligno_ARG")) != null) rig.IndicatorsLeft.Add(lamp);
            if ((lamp = BajajLamp(p, "Cligno_AVD")) != null) rig.IndicatorsRight.Add(lamp);
            if ((lamp = BajajLamp(p, "Cligno_ARD")) != null) rig.IndicatorsRight.Add(lamp);
            // Plaque d'immatriculation du joueur posée sur la plaque jaune du modèle, autocollant sur le flanc arrière
            if (p.HasPlate) rig.PlateAnchor = Anchor(b, "PlaqueJoueur", p.PlatePos + new Vector3(0f, 0f, -0.02f));
            rig.StickerAnchor = Anchor(b, "Autocollant", new Vector3(0.675f, 0.72f, -0.62f));

            rig.CameraFirstPerson = Anchor(b, "CameraPremierePersonne", new Vector3(0f, 1.36f, -0.02f));
            rig.CameraDashboard = Anchor(b, "CameraTableauDeBord", new Vector3(0f, 1.2f, 0.3f));
            if (withDriver)
            {
                rig.Driver = BuildDriver(b, BajajDriverSeat, 11, false);
                SetArmsForward(rig.Driver, 60f);
                HumanLibrary.SetupRider(rig.Driver, Anchor(pivot, "MainGauche", new Vector3(-0.3f, 0f, -0.02f)), Anchor(pivot, "MainDroite", new Vector3(0.3f, 0f, -0.02f)), null, null, 4f);
            }
            return true;
        }

        /// <summary>Tricycle de la circulation : Bajaj RE de MATULA DESIGN (couleurs du modèle), chauffeur, parfois des passagers.</summary>
        private static GameObject BuildModelTricycleTraffic(GameObject root, System.Random rnd, int seed, out float length, List<Renderer> brakeLights, List<Renderer> hazards, List<Transform> wheels)
        {
            length = 2.75f;
            GameObject model = BajajModel;
            if (model == null) return null;
            Transform t = root.transform;
            BajajParts p = PlaceBajaj(model, t, BajajBlue, BajajCanvas, Silver);
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
            HumanLibrary.SetupRider(drv, Anchor(pivot, "MainGauche", new Vector3(-0.3f, 0f, -0.02f)), Anchor(pivot, "MainDroite", new Vector3(0.3f, 0f, -0.02f)),
                Anchor(t, "PiedGauche", new Vector3(-0.15f, 0.37f, 0.25f)), Anchor(t, "PiedDroit", new Vector3(0.15f, 0.37f, 0.25f)), 4f);
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
            // Caisse : 1,32 m de large, 2,75 m de long, du bas de caisse au toit (1,70 m)
            AddTrafficBody(root, new Vector3(1.32f, 1.62f, 2.72f), new Vector3(0f, 0.89f, -0.07f));
            return root;
        }
    }

    /// <summary>Essuie-glace du tricycle : balaie le pare-brise quand il pleut (comme l'animation du modèle), rangé sinon.</summary>
    public class Essuieglace : MonoBehaviour
    {
        public float Amplitude = 22f;
        private float phase;
        private float angle = 22f;

        private void Update()
        {
            BalulaGame g = BalulaGame.Instance;
            bool rain = g != null && g.Env != null && g.Env.IsRaining;
            if (rain)
            {
                phase += Time.deltaTime * 0.8f;
                angle = Amplitude * Mathf.Cos(phase * Mathf.PI * 2f);
            }
            else
            {
                phase = 0f;
                angle = Mathf.MoveTowards(angle, Amplitude, Time.deltaTime * 90f);
            }
            transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
    }
}
