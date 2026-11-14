// BALULA MOTO — voitures génériques de la circulation et des parkings (scripts/generer_voitures.py) : grand coupé
// sportif, petite citadine à hayon, SUV compact. Aucune marque ni logo (MATULA DESIGN a choisi des voitures génériques
// à la place des modèles de marque reçus). Pièces lues : Caisse_* (matériau Peinture repeint au hasard), Roue_XXX_Pneu/
// _Jante, Phare_G/D, Stop_G/D, Cligno_*.
using System.Collections.Generic;
using UnityEngine;

namespace BalulaMoto.Runtime
{
    public static partial class VehicleBuilder
    {
        public static readonly string[] CarModels = { "Voiture_Coupe", "Voiture_Citadine", "Voiture_SUV" };

        /// <summary>Teintes courantes des voitures à Kinshasa (blanc, gris, noir, bleu nuit, rouge, beige…).</summary>
        public static readonly Color[] CarPaints =
        {
            new Color(0.93f, 0.93f, 0.92f), new Color(0.93f, 0.93f, 0.92f), new Color(0.72f, 0.73f, 0.75f), new Color(0.07f, 0.07f, 0.08f),
            new Color(0.08f, 0.12f, 0.26f), new Color(0.6f, 0.06f, 0.05f), new Color(0.78f, 0.7f, 0.55f), new Color(0.2f, 0.32f, 0.22f),
            new Color(0.45f, 0.47f, 0.5f), new Color(0.85f, 0.65f, 0.1f)
        };

        private static Material CarPart(string matName, Color body)
        {
            switch (matName)
            {
                case "Peinture": return Paint(body, 0.35f, 0.78f);
                case "Vitre": return Paint(new Color(0.06f, 0.08f, 0.1f), 0.2f, 0.93f);
                case "Noir": return Paint(new Color(0.05f, 0.05f, 0.055f), 0f, 0.3f);
                case "Chrome": return Paint(new Color(0.78f, 0.8f, 0.82f), 0.9f, 0.85f);
                case "Plaque": return Paint(new Color(0.95f, 0.95f, 0.93f), 0f, 0.3f);
                case "Pneu": return Paint(new Color(0.04f, 0.04f, 0.045f), 0f, 0.2f);
                case "Jante": return Paint(new Color(0.7f, 0.71f, 0.73f), 0.6f, 0.65f);
                case "Phare_Verre": return Paint(new Color(0.88f, 0.89f, 0.88f), 0.1f, 0.95f);
                case "Feu_Rouge": return Mats.Lit(RedLight, 0.5f);
                case "Orange": return Mats.Lit(ColorUtil.Shade(Amber, 0.3f), 0.5f);
            }
            return Paint(body, 0.2f, 0.5f);
        }

        /// <summary>Voiture générique de la circulation. Retourne null si le modèle est absent.</summary>
        private static GameObject BuildModelCar(GameObject root, string modelName, Color body, out float length, List<Renderer> brakeLights, List<Renderer> hazards, List<Transform> wheels)
        {
            length = 4.4f;
            GameObject model = MotoModel(modelName);
            if (model == null) return null;
            Transform t = root.transform;
            Matrix4x4 toModel = model.transform.worldToLocalMatrix;
            Dictionary<string, Transform> pivots = new Dictionary<string, Transform>();
            Bounds all = new Bounds();
            bool first = true;
            foreach (MeshFilter src in model.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = src.sharedMesh;
                if (mesh == null) continue;
                MeshRenderer smr = src.GetComponent<MeshRenderer>();
                Material[] srcMats = smr != null ? smr.sharedMaterials : new Material[0];
                Material[] mats = new Material[Mathf.Max(1, mesh.subMeshCount)];
                for (int i = 0; i < mats.Length; i++) mats[i] = CarPart(i < srcMats.Length && srcMats[i] != null ? srcMats[i].name : "", body);
                Matrix4x4 m = toModel * src.transform.localToWorldMatrix;
                Bounds wb = DecorModels.TransformBounds(mesh.bounds, m);
                if (first) { all = wb; first = false; } else all.Encapsulate(wb);
                string n = src.name;
                if (n.StartsWith("Roue_") && n.Length > 8)
                {
                    string key = n.Substring(5, 3);
                    Transform pivot;
                    if (!pivots.TryGetValue(key, out pivot))
                    {
                        pivot = Anchor(t, "Roue", Vector3.zero);
                        pivots[key] = pivot;
                        wheels.Add(pivot);
                    }
                    // Le pivot est placé au moyeu une fois toutes les pièces de la roue connues (ci-dessous)
                    GameObject part = MeshPart(pivot, n.EndsWith("Jante") ? "Jante" : "Pneu", mesh, mats, Vector3.zero);
                    ApplyMatrix(part.transform, m);
                    continue;
                }
                if (n.StartsWith("Phare")) { ApplyMatrix(MeshPart(t, "Phare", mesh, mats, Vector3.zero).transform, m); continue; }
                if (n.StartsWith("Stop"))
                {
                    GameObject st = MeshPart(t, "Stop", mesh, mats, Vector3.zero);
                    ApplyMatrix(st.transform, m);
                    brakeLights.Add(st.GetComponent<Renderer>());
                    continue;
                }
                if (n.StartsWith("Cligno"))
                {
                    GameObject cl = MeshPart(t, "Warning", mesh, mats, Vector3.zero);
                    ApplyMatrix(cl.transform, m);
                    hazards.Add(cl.GetComponent<Renderer>());
                    continue;
                }
                ApplyMatrix(MeshPart(t, n, mesh, mats, Vector3.zero).transform, m);
            }
            if (wheels.Count == 0)
            {
                for (int i = t.childCount - 1; i >= 0; i--) Object.Destroy(t.GetChild(i).gameObject);
                brakeLights.Clear();
                hazards.Clear();
                return null;
            }
            // Pivots des roues au centre de leurs pièces (la circulation fait tourner les roues autour de l'axe X)
            foreach (Transform pivot in pivots.Values)
            {
                Bounds rb = new Bounds();
                bool f2 = true;
                foreach (MeshFilter mf in pivot.GetComponentsInChildren<MeshFilter>())
                {
                    Bounds b = DecorModels.TransformBounds(mf.sharedMesh.bounds, Matrix4x4.TRS(mf.transform.localPosition, mf.transform.localRotation, mf.transform.localScale));
                    if (f2) { rb = b; f2 = false; } else rb.Encapsulate(b);
                }
                Vector3 c = rb.center;
                pivot.localPosition = c;
                foreach (Transform ch in pivot) ch.localPosition -= c;
            }
            length = all.size.z;
            float bottom = 0.3f;
            AddTrafficBody(root, new Vector3(all.size.x * 0.95f, all.max.y - bottom, all.size.z * 0.97f), new Vector3(0f, (all.max.y + bottom) * 0.5f, all.center.z));
            return root;
        }

        public static bool IsCarModelType(AIVehicleType t)
        {
            return t == AIVehicleType.VoitureCoupe || t == AIVehicleType.VoitureCitadine || t == AIVehicleType.VoitureSUV;
        }

        public static string CarModelFor(AIVehicleType t)
        {
            return t == AIVehicleType.VoitureCoupe ? CarModels[0] : (t == AIVehicleType.VoitureCitadine ? CarModels[1] : CarModels[2]);
        }

        // ==================================================================
        // Véhicules garés (parkings, bords de route) : modèles posés sans pilote, fusionnés avec la case de ville
        // ==================================================================
        private static readonly Dictionary<string, Material> parkedPaints = new Dictionary<string, Material>();

        /// <summary>Peinture d'un véhicule garé (une couleur de la liste, matériau partagé).</summary>
        public static Material ParkedPaint(int index)
        {
            Color c = CarPaints[Mathf.Abs(index) % CarPaints.Length];
            string key = ColorUtility.ToHtmlStringRGB(c);
            Material m;
            if (parkedPaints.TryGetValue(key, out m) && m != null) return m;
            m = Paint(c, 0.35f, 0.75f);
            parkedPaints[key] = m;
            return m;
        }

        /// <summary>Peinture d'une moto garée (couleurs vives des motos-taxis).</summary>
        public static Material ParkedMotoPaint(int index)
        {
            Color c = index % 3 == 0 ? new Color(0.62f, 0.03f, 0.03f) : ColorUtil.Hex(Prints[Mathf.Abs(index) % Prints.Length]);
            string key = "M" + ColorUtility.ToHtmlStringRGB(c);
            Material m;
            if (parkedPaints.TryGetValue(key, out m) && m != null) return m;
            m = Paint(c, 0.3f, 0.8f);
            parkedPaints[key] = m;
            return m;
        }
    }
}
