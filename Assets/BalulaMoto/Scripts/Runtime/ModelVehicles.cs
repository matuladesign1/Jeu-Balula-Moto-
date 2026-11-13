// BALULA MOTO — véhicules de la circulation construits à partir des modèles fournis par MATULA DESIGN.
// Bus TRANSCO : fichier Resources/BalulaMoto/Vehicules/BusTransco.obj, converti du FBX d'origine par
// scripts/convertir_bus.py (avant vers +Z, roues au sol, roues/phares/feux séparés). Design commercial respecté,
// sans prétendre à un partenariat officiel avec TRANSCO.
using System.Collections.Generic;
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
    }
}
