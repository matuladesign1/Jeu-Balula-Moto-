// BALULA MOTO — allège les véhicules de la circulation pour pouvoir en afficher beaucoup sur téléphone :
// carrosserie fusionnée en un maillage par matériau, une seule pièce par roue, feux regroupés, phares et
// faisceaux de nuit. Sans cela, chaque véhicule comptait une trentaine d'objets à dessiner.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BalulaMoto.Runtime
{
    public static class TrafficOptimizer
    {
        private static Material wheelMat;
        private static readonly Dictionary<string, Mesh> wheelMeshes = new Dictionary<string, Mesh>();

        private static Material WheelMaterial
        {
            get
            {
                if (wheelMat != null) return wheelMat;
                Texture2D t = new Texture2D(2, 1, TextureFormat.RGBA32, false);
                t.filterMode = FilterMode.Point;
                t.wrapMode = TextureWrapMode.Clamp;
                t.SetPixels(new[] { new Color(0.06f, 0.06f, 0.065f), new Color(0.62f, 0.63f, 0.65f) });
                t.Apply(false, true);
                wheelMat = new Material(Mats.Lit(Color.white, 0.3f));
                wheelMat.name = "Roue";
                wheelMat.color = Color.white;
                wheelMat.mainTexture = t;
                if (wheelMat.HasProperty("_BaseMap")) wheelMat.SetTexture("_BaseMap", t);
                if (wheelMat.HasProperty("_BaseColor")) wheelMat.SetColor("_BaseColor", Color.white);
                wheelMat.enableInstancing = false;
                return wheelMat;
            }
        }

        public static void Optimize(AIVehicle v)
        {
            if (v == null) return;
            Transform root = v.transform;
            HashSet<Transform> skip = new HashSet<Transform>();
            List<Renderer> detail = new List<Renderer>();

            // Roues : pneu + jante en une seule pièce (une texture de deux couleurs)
            foreach (Transform w in v.Wheels)
            {
                if (w == null) continue;
                skip.Add(w);
                Renderer wr = MergeWheel(w);
                if (wr != null) detail.Add(wr);
            }

            // Feux : stops et clignotants regroupés ; phares lumineux la nuit
            List<Renderer> heads = new List<Renderer>();
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                if (r != null && r.name == "Phare" && r.GetComponent<MeshFilter>() != null) heads.Add(r);
            foreach (Renderer r in heads) skip.Add(r.transform);
            foreach (Renderer r in v.BrakeLights) if (r != null) skip.Add(r.transform);
            foreach (Renderer r in v.Hazards) if (r != null) skip.Add(r.transform);
            Renderer head = MergeGroup(root, heads, "Phares");
            if (head != null && Night.LampMaterial != null) head.sharedMaterial = Night.LampMaterial;
            v.HeadLights = head;
            Renderer stop = MergeGroup(root, v.BrakeLights, "Stops");
            if (stop != null) { v.BrakeLights = new List<Renderer> { stop }; }
            if (!v.Sirens && v.Type != AIVehicleType.PoliceKabasele)
            {
                Renderer hz = MergeGroup(root, v.Hazards, "Clignotants");
                if (hz != null) { v.Hazards = new List<Renderer> { hz }; detail.Add(hz); }
            }
            else foreach (Renderer r in v.Hazards) if (r != null) detail.Add(r);

            // Carrosserie : tout le reste, fusionné par matériau (les personnages gardent leurs propres maillages)
            Dictionary<Material, List<CombineInstance>> groups = new Dictionary<Material, List<CombineInstance>>();
            Matrix4x4 toLocal = root.worldToLocalMatrix;
            List<GameObject> empty = new List<GameObject>();
            foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf == null || mf.transform == root) continue;
                MeshRenderer mr = mf.GetComponent<MeshRenderer>();
                if (mr == null || mf.sharedMesh == null || mr.sharedMaterial == null || !mf.sharedMesh.isReadable) continue;
                if (mf.GetComponent<TextMesh>() != null) continue;
                if (Skipped(mf.transform, root, skip)) continue;
                if (mf.GetComponentInParent<HumanAvatar>() != null || mf.GetComponentInParent<LODGroup>() != null) continue;
                if (mf.name == "Faisceau" || mf.name == "LueurArriere") continue;
                List<CombineInstance> list;
                if (!groups.TryGetValue(mr.sharedMaterial, out list)) { list = new List<CombineInstance>(); groups[mr.sharedMaterial] = list; }
                list.Add(new CombineInstance { mesh = mf.sharedMesh, transform = toLocal * mf.transform.localToWorldMatrix });
                if (mf.GetComponent<Collider>() == null && mf.transform.childCount == 0) empty.Add(mf.gameObject);
                else { Object.Destroy(mr); Object.Destroy(mf); }
            }
            foreach (GameObject g in empty) Object.Destroy(g);
            foreach (KeyValuePair<Material, List<CombineInstance>> kv in groups)
            {
                Mesh mesh = new Mesh { name = "Carrosserie_" + kv.Key.name };
                int verts = 0;
                foreach (CombineInstance ci in kv.Value) verts += ci.mesh.vertexCount;
                if (verts > 65000) mesh.indexFormat = IndexFormat.UInt32;
                mesh.CombineMeshes(kv.Value.ToArray(), true, true);
                mesh.RecalculateBounds();
                GameObject go = new GameObject("Carrosserie_" + kv.Key.name);
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = kv.Key;
                go.AddComponent<MeshCleanup>().Target = mesh;
            }

            // Inscriptions : dessinées de près seulement
            foreach (TextMesh tm in root.GetComponentsInChildren<TextMesh>(true))
            {
                Renderer tr = tm.GetComponent<Renderer>();
                if (tr != null) detail.Add(tr);
            }
            v.Detail = detail.ToArray();

            // Faisceau des phares et lueur des feux arrière (la nuit)
            bool moto = v.Type == AIVehicleType.MotoTaxi;
            float width = moto ? 0.8f : (v.Type == AIVehicleType.TricycleTaxi ? 1.3f : 1.9f);
            v.Beam = Night.AddBeam(root, v.Length * 0.5f, width, moto);
            v.TailGlow = Night.AddTailGlow(root, -v.Length * 0.5f, width);
        }

        private static bool Skipped(Transform t, Transform root, HashSet<Transform> skip)
        {
            while (t != null && t != root)
            {
                if (skip.Contains(t)) return true;
                t = t.parent;
            }
            return false;
        }

        private static Renderer MergeWheel(Transform wheel)
        {
            MeshFilter[] parts = wheel.GetComponentsInChildren<MeshFilter>(true);
            if (parts.Length == 0) return null;
            string key = "";
            foreach (MeshFilter mf in parts)
                key += (mf.sharedMesh != null ? mf.sharedMesh.GetInstanceID() : 0) + "/" + mf.transform.localScale.ToString("F3") + mf.transform.localRotation.eulerAngles.ToString("F0") + ";";
            Mesh mesh;
            if (!wheelMeshes.TryGetValue(key, out mesh) || mesh == null)
            {
                List<CombineInstance> list = new List<CombineInstance>();
                List<Vector2> uv = new List<Vector2>();
                Matrix4x4 toLocal = wheel.worldToLocalMatrix;
                foreach (MeshFilter mf in parts)
                {
                    if (mf.sharedMesh == null || !mf.sharedMesh.isReadable) return null;
                    list.Add(new CombineInstance { mesh = mf.sharedMesh, transform = toLocal * mf.transform.localToWorldMatrix });
                    bool rim = mf.name == "Jante";
                    for (int i = 0; i < mf.sharedMesh.vertexCount; i++) uv.Add(new Vector2(rim ? 0.75f : 0.25f, 0.5f));
                }
                mesh = new Mesh { name = "Roue" };
                mesh.CombineMeshes(list.ToArray(), true, true);
                mesh.SetUVs(0, uv);
                mesh.RecalculateBounds();
                wheelMeshes[key] = mesh;
            }
            foreach (MeshFilter mf in parts) if (mf.transform != wheel) Object.Destroy(mf.gameObject);
            MeshFilter own = wheel.GetComponent<MeshFilter>();
            if (own == null) own = wheel.gameObject.AddComponent<MeshFilter>();
            own.sharedMesh = mesh;
            MeshRenderer r = wheel.GetComponent<MeshRenderer>();
            if (r == null) r = wheel.gameObject.AddComponent<MeshRenderer>();
            r.sharedMaterial = WheelMaterial;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return r;
        }

        /// <summary>Regroupe plusieurs feux de même matériau en un seul objet (une seule ampoule à allumer).</summary>
        private static Renderer MergeGroup(Transform root, List<Renderer> lamps, string name)
        {
            if (lamps == null || lamps.Count == 0) return null;
            List<CombineInstance> list = new List<CombineInstance>();
            Matrix4x4 toLocal = root.worldToLocalMatrix;
            Material mat = null;
            foreach (Renderer r in lamps)
            {
                if (r == null) continue;
                MeshFilter mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null || !mf.sharedMesh.isReadable) return lamps[0];
                if (mat == null) mat = r.sharedMaterial;
                list.Add(new CombineInstance { mesh = mf.sharedMesh, transform = toLocal * mf.transform.localToWorldMatrix });
            }
            if (list.Count == 0) return null;
            Mesh mesh = new Mesh { name = name };
            mesh.CombineMeshes(list.ToArray(), true, true);
            mesh.RecalculateBounds();
            foreach (Renderer r in lamps) if (r != null) Object.Destroy(r.gameObject);
            GameObject go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer nr = go.AddComponent<MeshRenderer>();
            nr.sharedMaterial = mat;
            nr.shadowCastingMode = ShadowCastingMode.Off;
            go.AddComponent<MeshCleanup>().Target = mesh;
            return nr;
        }
    }
}
