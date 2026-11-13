// BALULA MOTO — lumières de la nuit : ampoules, fenêtres, lampadaires, phares et flaques de lumière au sol.
// Tout s'allume par une seule valeur globale (_BalulaNight) lue par les shaders BalulaMoto/Lumiere et BalulaMoto/Halo.
using System.Collections.Generic;
using UnityEngine;

namespace BalulaMoto.Runtime
{
    public static class Night
    {
        /// <summary>0 = plein jour, 1 = nuit (éclairage public allumé).</summary>
        public static float Level { get; private set; }
        public static bool Dark { get { return Level > 0.5f; } }
        /// <summary>Calque des petits détails (plaques de rue, fils) : la caméra ne les dessine que de près.</summary>
        public const int DetailLayer = 9;

        private static readonly int NightId = Shader.PropertyToID("_BalulaNight");
        private static Material lightMat, haloMat, lampMat, beamMat;
        private static bool missing;
        private static Mesh beamMesh, tailMesh;

        public static void Set(float level)
        {
            Level = Mathf.Clamp01(level);
            Shader.SetGlobalFloat(NightId, Level);
        }

        private static Material Make(string shaderName, string fallback)
        {
            Shader sh = Resources.Load<Shader>("BalulaMoto/Shaders/" + shaderName);
            if (sh == null) sh = Shader.Find("BalulaMoto/" + shaderName);
            if (sh == null || !sh.isSupported)
            {
                if (!missing) Debug.LogWarning("[BalulaMoto] Shader BalulaMoto/" + shaderName + " absent : lumières de nuit simplifiées.");
                missing = true;
                return null;
            }
            Material m = new Material(sh) { name = shaderName };
            m.enableInstancing = false;
            return m;
        }

        /// <summary>Ampoules et fenêtres (couleur aux sommets).</summary>
        public static Material LightMaterial { get { if (lightMat == null) lightMat = Make("Lumiere", null); return lightMat; } }
        /// <summary>Flaques de lumière au sol (additif).</summary>
        public static Material HaloMaterial { get { if (haloMat == null) haloMat = Make("Halo", null); return haloMat; } }
        /// <summary>Phares des véhicules (maillages sans couleur aux sommets).</summary>
        public static Material LampMaterial
        {
            get
            {
                if (lampMat == null)
                {
                    lampMat = Make("Lumiere", null);
                    if (lampMat != null) { lampMat.name = "Phare"; lampMat.SetColor("_Color", new Color(1f, 0.96f, 0.82f)); lampMat.SetFloat("_Jour", 0.75f); lampMat.SetFloat("_Nuit", 2.2f); }
                }
                return lampMat;
            }
        }
        public static Material BeamMaterial
        {
            get
            {
                if (beamMat == null)
                {
                    beamMat = Make("Halo", null);
                    if (beamMat != null) { beamMat.name = "Faisceau"; beamMat.SetFloat("_Force", 0.5f); }
                }
                return beamMat;
            }
        }

        private static Mesh Disc(string name, float halfW, float z0, float z1, Color32 c)
        {
            Mesh m = new Mesh { name = name };
            m.SetVertices(new List<Vector3> { new Vector3(-halfW, 0f, z0), new Vector3(-halfW, 0f, z1), new Vector3(halfW, 0f, z1), new Vector3(halfW, 0f, z0) });
            m.SetUVs(0, new List<Vector2> { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) });
            m.SetColors(new List<Color32> { c, c, c, c });
            m.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Faisceau des phares posé sur la chaussée devant un véhicule (visible la nuit seulement).</summary>
        public static Renderer AddBeam(Transform vehicle, float frontZ, float width, bool moto)
        {
            if (BeamMaterial == null) return null;
            if (beamMesh == null) beamMesh = Disc("Faisceau", 0.5f, 0f, 1f, new Color32(255, 240, 200, 255));
            GameObject g = new GameObject("Faisceau");
            g.transform.SetParent(vehicle, false);
            g.transform.localPosition = new Vector3(0f, 0.09f, frontZ - 1.5f);
            g.transform.localScale = new Vector3(moto ? 5f : width + 5f, 1f, moto ? 13f : 17f);
            g.AddComponent<MeshFilter>().sharedMesh = beamMesh;
            MeshRenderer r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = BeamMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.enabled = Dark;
            return r;
        }

        /// <summary>Petite lueur rouge derrière un véhicule la nuit.</summary>
        public static Renderer AddTailGlow(Transform vehicle, float rearZ, float width)
        {
            if (BeamMaterial == null) return null;
            if (tailMesh == null) tailMesh = Disc("LueurArriere", 0.5f, 0f, 1f, new Color32(150, 14, 8, 255));
            GameObject g = new GameObject("LueurArriere");
            g.transform.SetParent(vehicle, false);
            g.transform.localPosition = new Vector3(0f, 0.09f, rearZ - 3.2f);
            g.transform.localScale = new Vector3(width + 2.5f, 1f, 4.2f);
            g.AddComponent<MeshFilter>().sharedMesh = tailMesh;
            MeshRenderer r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = BeamMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.enabled = Dark;
            return r;
        }
    }

    /// <summary>Maillage de lumières (boîtes et panneaux colorés aux sommets) ou de flaques de lumière.</summary>
    public class LightMesh
    {
        public readonly List<Vector3> V = new List<Vector3>();
        public readonly List<Color32> C = new List<Color32>();
        public readonly List<Vector2> UV = new List<Vector2>();
        public readonly List<int> T = new List<int>();

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color32 col)
        {
            int i = V.Count;
            V.Add(a); V.Add(b); V.Add(c); V.Add(d);
            C.Add(col); C.Add(col); C.Add(col); C.Add(col);
            UV.Add(new Vector2(0f, 0f)); UV.Add(new Vector2(0f, 1f)); UV.Add(new Vector2(1f, 1f)); UV.Add(new Vector2(1f, 0f));
            T.Add(i); T.Add(i + 1); T.Add(i + 2); T.Add(i); T.Add(i + 2); T.Add(i + 3);
        }

        /// <summary>Panneau plat (fenêtre, enseigne) ; le shader le dessine des deux côtés.</summary>
        public void Panel(Vector3 center, Vector3 right, Vector3 up, float halfW, float halfH, Color32 col)
        {
            Vector3 a = center - right * halfW - up * halfH, b = center - right * halfW + up * halfH, c = center + right * halfW + up * halfH, d = center + right * halfW - up * halfH;
            Quad(a, b, c, d, col);
        }

        public void Box(Vector3 center, Vector3 half, Color32 col)
        {
            Vector3 x = Vector3.right * half.x, y = Vector3.up * half.y, z = Vector3.forward * half.z;
            Quad(center - x - y - z, center - x + y - z, center + x + y - z, center + x - y - z, col);
            Quad(center + x - y + z, center + x + y + z, center - x + y + z, center - x - y + z, col);
            Quad(center - x - y + z, center - x + y + z, center - x + y - z, center - x - y - z, col);
            Quad(center + x - y - z, center + x + y - z, center + x + y + z, center + x - y + z, col);
            Quad(center - x - y + z, center - x - y - z, center + x - y - z, center + x - y + z, col);
            Quad(center - x + y - z, center - x + y + z, center + x + y + z, center + x + y - z, col);
        }

        /// <summary>Flaque de lumière horizontale (carré dont le shader fait un disque).</summary>
        public void Pool(Vector3 center, float radius, Color32 col)
        {
            Quad(center + new Vector3(-radius, 0f, -radius), center + new Vector3(-radius, 0f, radius), center + new Vector3(radius, 0f, radius), center + new Vector3(radius, 0f, -radius), col);
        }

        public GameObject Emit(Transform parent, string name, Material mat)
        {
            if (V.Count == 0 || mat == null) return null;
            Mesh mesh = new Mesh { name = name };
            if (V.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(V);
            mesh.SetColors(C);
            mesh.SetUVs(0, UV);
            mesh.SetTriangles(T, 0);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            GameObject g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            g.AddComponent<MeshCleanup>().Target = mesh;
            return g;
        }
    }
}
