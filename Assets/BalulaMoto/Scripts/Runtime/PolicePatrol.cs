// BALULA MOTO — policiers à pied aux carrefours de toute la ville (comme la police de circulation routière à Kinshasa).
// Agent : personnage réaliste animé, comme les autres habitants (avatar Rocketbox « Police_Male_01 », licence MIT),
// habillé d'après les indications de MATULA DESIGN : tenue bleu de nuit, inscription « POLICE » blanche bordée de bleu
// sur la poche gauche, béret noir, ceinturon et rangers noirs (scripts/police_congolaise.py), fusil d'assaut de type Kalachnikov porté en bandoulière dans le dos
// (Resources/BalulaMoto/Accessoires/FusilAK, scripts/generer_fusil.py). Aucun insigne officiel n'est reproduit.
// Si les personnages réalistes manquent, le policier dessiné d'après le script Blender de MATULA DESIGN
// (Docs/ModelesSources/Police_Officer_RDC_Blender_Export.py, PoliceOfficerModel) sert de secours.
// Les postes sont construits autour du joueur seulement (téléphones) ; un agent qui voit une infraction siffle,
// le joueur se range, l'agent vient au véhicule et dresse l'amende.
using System.Collections.Generic;
using UnityEngine;

namespace BalulaMoto.Runtime
{
    /// <summary>Maillage du policier (deux niveaux de détail), construit une fois et partagé par tous les agents.</summary>
    public static class PoliceOfficerModel
    {
        // Couleurs du script (valeurs linéaires de Blender, converties en sRGB pour Unity)
        private static readonly Color[] Palette =
        {
            new Color(0.172f, 0.259f, 0.412f), // 0 uniforme bleu marine
            new Color(0.205f, 0.304f, 0.477f), // 1 panneaux bleu
            new Color(0.112f, 0.128f, 0.160f), // 2 cuir noir (rangers, ceinturon)
            new Color(0.110f, 0.120f, 0.150f), // 3 béret en laine noire
            new Color(0.400f, 0.250f, 0.170f), // 4 peau brun foncé
            new Color(0.480f, 0.310f, 0.210f), // 5 reflets de la peau
            new Color(0.530f, 0.560f, 0.590f), // 6 métal (boutons, boucle)
            new Color(0.720f, 0.620f, 0.380f), // 7 insignes en laiton
            new Color(0.900f, 0.910f, 0.900f), // 8 lettres blanches
            new Color(0.170f, 0.140f, 0.110f), // 9 yeux
            new Color(0.140f, 0.160f, 0.180f)  // 10 arme de service (noir mat)
        };
        private const int Navy = 0, Navy2 = 1, Black = 2, Beret = 3, Skin = 4, SkinHi = 5, Metal = 6, Badge = 7, White = 8, Eye = 9, Gun = 10;
        private const float S = 0.835f;          // 2,18 m (volumes du script) -> 1,82 m
        private const float Foot = 0.0375f * S;  // dessous des semelles posé au sol

        private static Mesh lod0, lod1;
        private static Material mat;

        public static Material Material
        {
            get
            {
                if (mat != null) return mat;
                Texture2D t = new Texture2D(16, 1, TextureFormat.RGBA32, false);
                t.filterMode = FilterMode.Point;
                t.wrapMode = TextureWrapMode.Clamp;
                Color[] px = new Color[16];
                for (int i = 0; i < 16; i++) px[i] = i < Palette.Length ? Palette[i] : Color.gray;
                t.SetPixels(px);
                t.Apply(false, true);
                mat = new Material(Mats.Lit(Color.white, 0.3f)) { name = "Policier" };
                mat.color = Color.white;
                mat.mainTexture = t;
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", t);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
                mat.enableInstancing = false;
                return mat;
            }
        }

        public static Mesh Lod0 { get { if (lod0 == null) lod0 = Build(false); return lod0; } }
        public static Mesh Lod1 { get { if (lod1 == null) lod1 = Build(true); return lod1; } }

        // ------------------------------------------------------------------
        // Construction (coordonnées du script : X droite, Y profondeur, Z haut ; le personnage regarde vers -Y)
        // ------------------------------------------------------------------
        private class B
        {
            public readonly List<Vector3> V = new List<Vector3>();
            public readonly List<Vector3> N = new List<Vector3>();
            public readonly List<Vector2> UV = new List<Vector2>();
            public readonly List<int> T = new List<int>();
            public bool Low;

            // Blender (x, y, z) -> Unity (x, z, -y) : le personnage regarde vers +Z, à l'échelle, pieds au sol
            public static Vector3 P(float x, float y, float z) { return new Vector3(x * S, z * S - Foot, -y * S); }
            private static Vector2 Uv(int c) { return new Vector2((c + 0.5f) / 16f, 0.5f); }

            private void Tri(int a, int b, int c, Vector3 outward)
            {
                Vector3 n = Vector3.Cross(V[b] - V[a], V[c] - V[a]);
                if (n.sqrMagnitude < 1e-14f) return;
                if (Vector3.Dot(n, outward) < 0f) { T.Add(a); T.Add(c); T.Add(b); }
                else { T.Add(a); T.Add(b); T.Add(c); }
            }

            private int Add(Vector3 p, Vector3 n, int color)
            {
                V.Add(p);
                N.Add(n);
                UV.Add(Uv(color));
                return V.Count - 1;
            }

            /// <summary>Sphère UV du script (rayon 1) mise à l'échelle (sx, sy, sz).</summary>
            public void Sphere(float x, float y, float z, float sx, float sy, float sz, int color, int segs = 20, int rings = 12)
            {
                Vector3 c = P(x, y, z);
                Vector3 r = new Vector3(sx, sz, sy) * S;
                if (Low)
                {
                    if (Mathf.Max(r.x, Mathf.Max(r.y, r.z)) < 0.03f) return; // petits détails invisibles de loin
                    segs = Mathf.Max(6, segs / 3);
                    rings = Mathf.Max(4, rings / 3);
                }
                else
                {
                    // Allégé pour le téléphone (même forme, moins de facettes)
                    segs = Mathf.Max(8, Mathf.RoundToInt(segs * 0.6f));
                    rings = Mathf.Max(6, Mathf.RoundToInt(rings * 0.6f));
                }
                int start = V.Count;
                for (int i = 0; i <= rings; i++)
                {
                    float th = Mathf.PI * i / rings;
                    for (int j = 0; j <= segs; j++)
                    {
                        float ph = 2f * Mathf.PI * j / segs;
                        Vector3 u = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph));
                        Vector3 p = c + Vector3.Scale(u, r);
                        Vector3 n = new Vector3(u.x / r.x, u.y / r.y, u.z / r.z).normalized;
                        Add(p, n, color);
                    }
                }
                int w = segs + 1;
                for (int i = 0; i < rings; i++)
                    for (int j = 0; j < segs; j++)
                    {
                        int a = start + i * w + j, b = a + w, cc = b + 1, d = a + 1;
                        Vector3 o1 = (V[a] + V[b] + V[cc]) / 3f - c, o2 = (V[a] + V[cc] + V[d]) / 3f - c;
                        Tri(a, b, cc, o1);
                        Tri(a, cc, d, o2);
                    }
            }

            /// <summary>Cube du script (taille 1) aux dimensions (dx, dy, dz).</summary>
            public void Box(float x, float y, float z, float dx, float dy, float dz, int color)
            {
                Vector3 c = P(x, y, z);
                Vector3 h = new Vector3(dx, dz, dy) * (S * 0.5f);
                if (Low && Mathf.Max(h.x, Mathf.Max(h.y, h.z)) < 0.025f) return;
                Vector3[] axes = { Vector3.right, Vector3.up, Vector3.forward };
                for (int k = 0; k < 3; k++)
                    for (int sgn = -1; sgn <= 1; sgn += 2)
                    {
                        Vector3 n = axes[k] * sgn;
                        Vector3 a1 = axes[(k + 1) % 3], a2 = axes[(k + 2) % 3];
                        Vector3 fc = c + Vector3.Scale(n, h);
                        Vector3 e1 = Vector3.Scale(a1, h), e2 = Vector3.Scale(a2, h);
                        int i0 = Add(fc - e1 - e2, n, color), i1 = Add(fc + e1 - e2, n, color), i2 = Add(fc + e1 + e2, n, color), i3 = Add(fc - e1 + e2, n, color);
                        Tri(i0, i1, i2, n);
                        Tri(i0, i2, i3, n);
                    }
            }

            /// <summary>Cône tronqué du script entre deux points (rayon r1 en a, r2 en b).</summary>
            public void Cone(float ax, float ay, float az, float bx, float by, float bz, float r1, float r2, int color, int sides = 16)
            {
                Vector3 A = P(ax, ay, az), Bp = P(bx, by, bz);
                r1 *= S;
                r2 *= S;
                sides = Low ? 6 : Mathf.Max(8, Mathf.RoundToInt(sides * 0.75f));
                Vector3 axis = Bp - A;
                float len = axis.magnitude;
                if (len < 1e-5f) return;
                axis /= len;
                Vector3 u = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                Vector3 v = Vector3.Cross(axis, u);
                int start = V.Count;
                for (int k = 0; k <= sides; k++)
                {
                    float an = 2f * Mathf.PI * k / sides;
                    Vector3 rad = u * Mathf.Cos(an) + v * Mathf.Sin(an);
                    Vector3 n = (rad * len + axis * (r1 - r2)).normalized;
                    Add(A + rad * r1, n, color);
                    Add(Bp + rad * r2, n, color);
                }
                for (int k = 0; k < sides; k++)
                {
                    int a = start + k * 2, b = a + 1, c2 = a + 3, d = a + 2;
                    Vector3 mid = ((V[a] + V[c2]) * 0.5f) - (A + Bp) * 0.5f;
                    Tri(a, b, c2, mid);
                    Tri(a, c2, d, mid);
                }
                // Bouchons
                for (int e = 0; e < 2; e++)
                {
                    Vector3 cen = e == 0 ? A : Bp;
                    float rr = e == 0 ? r1 : r2;
                    Vector3 n = e == 0 ? -axis : axis;
                    int ci = Add(cen, n, color);
                    int ring = V.Count;
                    for (int k = 0; k <= sides; k++)
                    {
                        float an = 2f * Mathf.PI * k / sides;
                        Add(cen + (u * Mathf.Cos(an) + v * Mathf.Sin(an)) * rr, n, color);
                    }
                    for (int k = 0; k < sides; k++) Tri(ci, ring + k, ring + k + 1, n);
                }
            }
        }

        private static Mesh Build(bool low)
        {
            B b = new B { Low = low };
            // Rangers et jambes
            foreach (float x in new[] { -0.115f, 0.115f })
            {
                b.Box(x, -0.035f, 0.075f, 0.19f, 0.36f, 0.075f, Black);
                b.Sphere(x, -0.105f, 0.16f, 0.095f, 0.17f, 0.09f, Black);
                b.Sphere(x, 0.015f, 0.29f, 0.085f, 0.105f, 0.18f, Black);
                b.Cone(x, 0.015f, 0.43f, x, 0.015f, 0.86f, 0.085f, 0.115f, Navy, 20);
                b.Sphere(x, 0f, 0.88f, 0.105f, 0.105f, 0.12f, Navy2);
                b.Cone(x, 0.015f, 0.91f, x * 0.92f, 0f, 1.18f, 0.115f, 0.15f, Navy, 20);
                // Poche cargo sur la cuisse
                b.Box(x * 1.75f, -0.105f, 0.79f, 0.105f, 0.055f, 0.17f, Navy2);
                b.Box(x * 1.75f, -0.14f, 0.87f, 0.11f, 0.025f, 0.045f, Navy);
            }
            // Bassin et veste
            b.Sphere(0f, 0.015f, 1.19f, 0.205f, 0.135f, 0.17f, Navy);
            b.Sphere(0f, 0.015f, 1.48f, 0.255f, 0.145f, 0.34f, Navy);
            b.Box(0f, -0.135f, 1.49f, 0.045f, 0.025f, 0.34f, Navy2);
            foreach (float z in new[] { 1.35f, 1.43f, 1.51f, 1.59f, 1.67f }) b.Sphere(0f, -0.153f, z, 0.012f, 0.008f, 0.012f, Metal, 12, 8);
            // Épaules et épaulettes
            foreach (float x in new[] { -0.265f, 0.265f })
            {
                b.Sphere(x, 0f, 1.69f, 0.12f, 0.145f, 0.12f, Navy);
                b.Box(x, -0.005f, 1.775f, 0.17f, 0.095f, 0.035f, Navy2);
                b.Sphere(x, -0.06f, 1.78f, 0.012f, 0.008f, 0.012f, Metal, 12, 8);
            }
            // Bras : gauche le long du corps, droit plié pour tenir l'arme devant
            b.Cone(-0.29f, 0f, 1.68f, -0.36f, -0.005f, 1.39f, 0.095f, 0.075f, Navy, 20);
            b.Sphere(-0.36f, -0.005f, 1.39f, 0.075f, 0.08f, 0.08f, Navy);
            b.Cone(-0.36f, -0.005f, 1.39f, -0.39f, -0.025f, 1.13f, 0.073f, 0.055f, Navy, 20);
            b.Sphere(-0.39f, -0.045f, 1.08f, 0.052f, 0.045f, 0.085f, Skin);
            b.Cone(0.29f, 0f, 1.68f, 0.39f, -0.015f, 1.48f, 0.095f, 0.075f, Navy, 20);
            b.Sphere(0.39f, -0.015f, 1.48f, 0.078f, 0.08f, 0.08f, Navy);
            b.Cone(0.39f, -0.015f, 1.48f, 0.30f, -0.22f, 1.36f, 0.073f, 0.055f, Navy, 20);
            b.Sphere(0.27f, -0.255f, 1.34f, 0.06f, 0.052f, 0.075f, Skin);
            // Cou et tête
            b.Cone(0f, 0.015f, 1.76f, 0f, 0.015f, 1.88f, 0.075f, 0.075f, Skin, 20);
            b.Sphere(0f, -0.005f, 1.99f, 0.135f, 0.125f, 0.17f, Skin, 32, 20);
            b.Sphere(0f, -0.025f, 1.91f, 0.112f, 0.11f, 0.09f, Skin);
            b.Sphere(0f, -0.132f, 1.985f, 0.027f, 0.035f, 0.05f, SkinHi);
            foreach (float x in new[] { -0.052f, 0.052f })
            {
                b.Sphere(x, -0.005f, 1.99f, 0.027f, 0.025f, 0.045f, Skin);
                b.Sphere(x, -0.121f, 2.025f, 0.018f, 0.009f, 0.012f, Eye, 12, 8);
                b.Sphere(x, -0.125f, 2.047f, 0.03f, 0.012f, 0.009f, SkinHi, 12, 8);
            }
            b.Sphere(0f, -0.13f, 1.94f, 0.035f, 0.012f, 0.009f, SkinHi);
            b.Sphere(0f, -0.105f, 1.895f, 0.045f, 0.02f, 0.018f, SkinHi);
            // Béret noir, bandeau et insigne de la police
            b.Sphere(0f, 0f, 2.155f, 0.16f, 0.145f, 0.065f, Beret, 32, 16);
            b.Sphere(0f, -0.005f, 2.125f, 0.15f, 0.14f, 0.025f, Black, 32, 12);
            b.Sphere(0f, -0.143f, 2.145f, 0.026f, 0.008f, 0.032f, Badge, 16, 10);
            b.Sphere(0f, -0.152f, 2.145f, 0.009f, 0.005f, 0.012f, White, 12, 8);
            // Poches de poitrine, rabats, boutons, écusson POLICE, insignes de manche
            foreach (float x in new[] { -0.13f, 0.13f })
            {
                b.Box(x, -0.145f, 1.53f, 0.105f, 0.035f, 0.105f, Navy2);
                b.Box(x, -0.17f, 1.59f, 0.112f, 0.022f, 0.035f, Navy);
                b.Sphere(x, -0.185f, 1.585f, 0.009f, 0.006f, 0.009f, Metal, 10, 8);
            }
            b.Box(0.13f, -0.17f, 1.665f, 0.105f, 0.018f, 0.035f, Black);
            foreach (float x in new[] { -0.365f, 0.365f }) b.Box(x, -0.082f, 1.52f, 0.055f, 0.018f, 0.075f, Badge);
            // Ceinturon, boucle, pochette radio, étui
            b.Box(0f, 0f, 1.255f, 0.46f, 0.31f, 0.065f, Black);
            b.Box(0f, -0.165f, 1.255f, 0.075f, 0.025f, 0.045f, Metal);
            b.Box(-0.19f, -0.17f, 1.27f, 0.075f, 0.065f, 0.11f, Black);
            b.Box(0.22f, -0.14f, 1.22f, 0.09f, 0.075f, 0.15f, Black);
            // Arme de service tenue en main droite (accessoire visuel, sans fonction)
            b.Box(0.25f, -0.36f, 1.405f, 0.19f, 0.045f, 0.045f, Gun);
            b.Box(0.25f, -0.355f, 1.37f, 0.15f, 0.04f, 0.035f, Gun);
            b.Cone(0.29f, -0.35f, 1.37f, 0.31f, -0.30f, 1.27f, 0.026f, 0.035f, Gun, 12);
            b.Box(0.145f, -0.36f, 1.405f, 0.025f, 0.035f, 0.035f, Black);
            b.Sphere(0.255f, -0.385f, 1.355f, 0.035f, 0.008f, 0.022f, Metal, 16, 8);

            Mesh m = new Mesh { name = low ? "Policier_LOD1" : "Policier_LOD0" };
            m.SetVertices(b.V);
            m.SetNormals(b.N);
            m.SetUVs(0, b.UV);
            m.SetTriangles(b.T, 0);
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Un agent debout (pieds au point donné, regard selon la rotation).</summary>
        public static GameObject Create(Transform parent, Vector3 pos, Quaternion rot)
        {
            GameObject g = new GameObject("Policier");
            g.transform.SetParent(parent, false);
            g.transform.SetPositionAndRotation(pos, rot);
            GameObject a = new GameObject("Detail");
            a.transform.SetParent(g.transform, false);
            a.AddComponent<MeshFilter>().sharedMesh = Lod0;
            MeshRenderer r0 = a.AddComponent<MeshRenderer>();
            r0.sharedMaterial = Material;
            GameObject c = new GameObject("Loin");
            c.transform.SetParent(g.transform, false);
            c.AddComponent<MeshFilter>().sharedMesh = Lod1;
            MeshRenderer r1 = c.AddComponent<MeshRenderer>();
            r1.sharedMaterial = Material;
            r1.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            LODGroup lg = g.AddComponent<LODGroup>();
            lg.SetLODs(new[] { new LOD(0.07f, new Renderer[] { r0 }), new LOD(0.012f, new Renderer[] { r1 }) });
            lg.RecalculateBounds();
            return g;
        }

        public static int Triangles(bool low) { return (low ? Lod1 : Lod0).triangles.Length / 3; }
    }

    /// <summary>Agent de police réaliste (avatar animé en tenue de la police congolaise, fusil en bandoulière).</summary>
    public static class PoliceAgent
    {
        /// <summary>Vrai si les agents sont des personnages réalistes (sinon : policier dessiné de secours).</summary>
        public static bool Realistic { get { return HumanLibrary.Available && HumanLibrary.Load("Police").Length > 0; } }

        /// <summary>Un agent debout (pieds au point donné, regard selon la rotation).</summary>
        public static GameObject Create(Transform parent, Vector3 pos, Quaternion rot, int seed)
        {
            GameObject g = Realistic ? HumanLibrary.Spawn(parent, Vector3.zero, "police", false, seed, false) : null;
            if (g == null) return PoliceOfficerModel.Create(parent, pos, rot);
            g.name = "Policier";
            g.transform.SetPositionAndRotation(pos, rot);
            HumanAvatar av = g.GetComponent<HumanAvatar>();
            if (av != null)
            {
                // en faction : attente, bras le long du corps, de temps en temps une autre attitude
                av.Play(seed % 3 == 0 ? HumanAction.Wait : HumanAction.Idle, 0f, true);
                if (av.Anim != null) av.Anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                PoliceKit.Equip(av);
            }
            return g;
        }

        public static void SetAction(Transform officer, HumanAction a)
        {
            if (officer == null) return;
            HumanAvatar av = officer.GetComponent<HumanAvatar>();
            if (av != null) av.Play(a, 0.25f);
        }
    }

    /// <summary>Fusil d'assaut de type Kalachnikov porté en bandoulière dans le dos de l'agent (accessoire, sans tir).</summary>
    public static class PoliceKit
    {
        private static GameObject rifle;
        private static bool tried;
        private static Material metal, wood, strap;

        private static GameObject Rifle
        {
            get
            {
                if (!tried)
                {
                    tried = true;
                    rifle = Resources.Load<GameObject>("BalulaMoto/Accessoires/FusilAK");
                    if (rifle == null) Debug.LogWarning("[BalulaMoto] Modèle du fusil absent : fusil simplifié.");
                }
                return rifle;
            }
        }

        private static Material Mat(string n)
        {
            if (metal == null)
            {
                metal = Mats.Lit(new Color(0.11f, 0.115f, 0.12f), 0.55f);
                wood = Mats.Lit(new Color(0.42f, 0.21f, 0.1f), 0.35f);
                strap = Mats.Lit(new Color(0.16f, 0.15f, 0.12f), 0.15f);
            }
            if (n.Contains("Bois")) return wood;
            if (n.Contains("Bretelle")) return strap;
            return metal;
        }

        public static void Equip(HumanAvatar av)
        {
            if (av == null) return;
            Transform spine = av.Spine2 != null ? av.Spine2 : av.Spine1;
            if (spine == null) return;
            Transform root = av.transform;
            GameObject g = new GameObject("FusilEnBandouliere");
            // dans le dos, en travers : canon vers le haut au-dessus de l'épaule droite, crosse vers la hanche gauche
            float a = 38f * Mathf.Deg2Rad;
            Vector3 barrel = root.right * Mathf.Sin(a) + root.up * Mathf.Cos(a);
            Vector3 top = -root.right * Mathf.Cos(a) + root.up * Mathf.Sin(a);
            g.transform.SetPositionAndRotation(spine.position - root.up * 0.08f - root.forward * 0.165f, Quaternion.LookRotation(barrel, top));
            List<Renderer> rs = new List<Renderer>();
            GameObject model = Rifle;
            if (model != null)
            {
                Matrix4x4 toModel = model.transform.worldToLocalMatrix;
                foreach (MeshFilter src in model.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (src.sharedMesh == null) continue;
                    GameObject part = new GameObject(src.name);
                    part.transform.SetParent(g.transform, false);
                    Matrix4x4 m = toModel * src.transform.localToWorldMatrix;
                    part.transform.localPosition = m.GetColumn(3);
                    part.transform.localRotation = m.rotation;
                    part.transform.localScale = m.lossyScale;
                    part.AddComponent<MeshFilter>().sharedMesh = src.sharedMesh;
                    MeshRenderer mr = part.AddComponent<MeshRenderer>();
                    MeshRenderer smr = src.GetComponent<MeshRenderer>();
                    mr.sharedMaterial = Mat(smr != null && smr.sharedMaterial != null ? smr.sharedMaterial.name : src.name);
                    rs.Add(mr);
                }
            }
            else
            {
                // secours : silhouette simplifiée (carcasse, canon, chargeur, crosse en bois)
                rs.Add(Part(g.transform, Prims.Cube, new Vector3(0f, 0f, 0f), new Vector3(0.04f, 0.06f, 0.24f), Mat("Metal")));
                rs.Add(Part(g.transform, Prims.Cube, new Vector3(0f, 0f, 0.3f), new Vector3(0.018f, 0.018f, 0.36f), Mat("Metal")));
                rs.Add(Part(g.transform, Prims.Cube, new Vector3(0f, -0.12f, 0.09f), new Vector3(0.026f, 0.18f, 0.05f), Mat("Metal")));
                rs.Add(Part(g.transform, Prims.Cube, new Vector3(0f, -0.03f, -0.27f), new Vector3(0.036f, 0.09f, 0.28f), Mat("Bois")));
            }
            g.transform.SetParent(spine, true);
            // le fusil suit les niveaux de détail de l'agent (pas de fusil flottant quand l'agent n'est plus dessiné)
            LODGroup lg = av.GetComponent<LODGroup>();
            if (lg != null && rs.Count > 0)
            {
                LOD[] lods = lg.GetLODs();
                for (int i = 0; i < lods.Length; i++)
                {
                    List<Renderer> l = new List<Renderer>(lods[i].renderers);
                    l.AddRange(rs);
                    lods[i].renderers = l.ToArray();
                }
                lg.SetLODs(lods);
            }
        }

        private static Renderer Part(Transform parent, Mesh mesh, Vector3 pos, Vector3 size, Material mat)
        {
            GameObject p = new GameObject("Piece");
            p.transform.SetParent(parent, false);
            p.transform.localPosition = pos;
            p.transform.localScale = size;
            p.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer mr = p.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            return mr;
        }
    }

    /// <summary>Postes de police aux carrefours, construits autour du joueur ; agents qui arrêtent les contrevenants.</summary>
    public class PolicePatrol : MonoBehaviour
    {
        public static PolicePatrol Instance;
        /// <summary>Distance à laquelle un agent voit une infraction.</summary>
        public const float SightRadius = 75f;

        private class Post
        {
            public Vector3 Junction;
            public Vector3 P1, P2;
            public Quaternion R1, R2;
            public bool Two;
            public GameObject G1, G2;
        }

        private readonly List<Post> posts = new List<Post>();
        private readonly Dictionary<long, List<int>> grid = new Dictionary<long, List<int>>();
        private readonly List<int> built = new List<int>();
        private const float Cell = 200f, BuildDist = 180f, DropDist = 250f;
        private float timer;
        // Agent qui s'avance vers le véhicule arrêté, puis retourne à son poste
        private Transform moving;
        private Vector3 moveFrom, moveTo, home;
        private Quaternion rotFrom, rotTo, homeRot;
        private float moveT = 1f, moveDur = 1.2f;
        private bool returning;

        public int PostCount { get { return posts.Count; } }
        public int ActiveOfficers
        {
            get
            {
                int n = 0;
                foreach (int i in built) n += (posts[i].G1 != null ? 1 : 0) + (posts[i].G2 != null ? 1 : 0);
                return n;
            }
        }

        private static long Key(int x, int z) { return ((long)x << 32) ^ (uint)z; }

        public static PolicePatrol Create(Transform parent, ImportedWorld map)
        {
            GameObject g = new GameObject("Police");
            g.transform.SetParent(parent, false);
            Instance = g.AddComponent<PolicePatrol>();
            if (map != null) Instance.Plan(map);
            return Instance;
        }

        /// <summary>Carrefours entre rues réelles d'OpenStreetMap ; un poste tous les 200 m environ, les grands carrefours d'abord.</summary>
        private void Plan(ImportedWorld map)
        {
            Dictionary<long, List<int>> nodes = new Dictionary<long, List<int>>();
            Dictionary<long, Vector2> nodePos = new Dictionary<long, Vector2>();
            for (int r = 0; r < map.roads.Count; r++)
            {
                ImportedRoad rd = map.roads[r];
                if (rd.rank < 2 || rd.pts.Count < 2) continue;
                foreach (Vector2 q in rd.pts)
                {
                    long k = Key(Mathf.RoundToInt(q.x * 2f), Mathf.RoundToInt(q.y * 2f));
                    List<int> l;
                    if (!nodes.TryGetValue(k, out l)) { l = new List<int>(); nodes[k] = l; nodePos[k] = q; }
                    if (!l.Contains(r)) l.Add(r);
                }
            }
            List<KeyValuePair<float, long>> cand = new List<KeyValuePair<float, long>>();
            foreach (KeyValuePair<long, List<int>> kv in nodes)
            {
                if (kv.Value.Count < 2) continue;
                int r1 = 0, r2 = 0;
                foreach (int ri in kv.Value)
                {
                    int rk = map.roads[ri].rank;
                    if (rk > r1) { r2 = r1; r1 = rk; } else if (rk > r2) r2 = rk;
                }
                if (r1 < 3) continue;
                cand.Add(new KeyValuePair<float, long>(r1 * 10f + r2, kv.Key));
            }
            cand.Sort((a, b) => b.Key.CompareTo(a.Key));
            List<Vector2> kept = new List<Vector2>();
            foreach (KeyValuePair<float, long> c in cand)
            {
                if (posts.Count >= 600) break;
                Vector2 j = nodePos[c.Value];
                bool close = false;
                foreach (Vector2 k in kept) if ((k - j).sqrMagnitude < 200f * 200f) { close = true; break; }
                if (close) continue;
                // Rue principale (la plus large) et rue qui la croise
                List<int> rs = nodes[c.Value];
                int ra = rs[0], rb = rs[1];
                foreach (int ri in rs) if (map.roads[ri].rank > map.roads[ra].rank || (map.roads[ri].rank == map.roads[ra].rank && map.roads[ri].width > map.roads[ra].width)) ra = ri;
                foreach (int ri in rs) if (ri != ra) { rb = ri; break; }
                Vector2 da = DirAt(map.roads[ra], j), db = DirAt(map.roads[rb], j);
                if (da.sqrMagnitude < 0.5f || db.sqrMagnitude < 0.5f) continue;
                float hwa = map.roads[ra].width * 0.5f, hwb = map.roads[rb].width * 0.5f;
                Vector2 na = new Vector2(-da.y, da.x);
                // Coin du carrefour, sur le bord de la rue principale, avant la rue qui croise
                float sinA = Mathf.Abs(da.x * db.y - da.y * db.x);
                float back = (hwb + 2.6f) / Mathf.Max(0.35f, sinA);
                Vector2 p1 = j + na * (hwa + 1.5f) - da * back;
                Vector2 p2 = j - na * (hwa + 1.5f) + da * back;
                if (!ClearOf(map, rs, p1) || !ClearOf(map, rs, p2)) continue;
                Vector3 J = new Vector3(j.x, 0f, j.y);
                Post post = new Post { Junction = J, P1 = new Vector3(p1.x, 0f, p1.y), P2 = new Vector3(p2.x, 0f, p2.y) };
                post.R1 = Quaternion.LookRotation(Flat(J - post.P1));
                post.R2 = Quaternion.LookRotation(Flat(J - post.P2));
                post.Two = (c.Value & 0x7fffffffL) % 3 == 0 || map.roads[ra].rank >= 5;
                int idx = posts.Count;
                posts.Add(post);
                kept.Add(j);
                long gk = Key(Mathf.FloorToInt(j.x / Cell), Mathf.FloorToInt(j.y / Cell));
                List<int> gl;
                if (!grid.TryGetValue(gk, out gl)) { gl = new List<int>(); grid[gk] = gl; }
                gl.Add(idx);
            }
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-4f ? v.normalized : Vector3.forward;
        }

        private static Vector2 DirAt(ImportedRoad rd, Vector2 j)
        {
            int best = 0;
            float bd = float.MaxValue;
            for (int i = 0; i < rd.pts.Count; i++)
            {
                float d = (rd.pts[i] - j).sqrMagnitude;
                if (d < bd) { bd = d; best = i; }
            }
            Vector2 a = rd.pts[Mathf.Max(0, best - 1)], b = rd.pts[Mathf.Min(rd.pts.Count - 1, best + 1)];
            Vector2 d2 = b - a;
            return d2.sqrMagnitude > 1e-4f ? d2.normalized : Vector2.zero;
        }

        /// <summary>L'agent ne doit pas se tenir sur la chaussée d'une des rues du carrefour.</summary>
        private static bool ClearOf(ImportedWorld map, List<int> roads, Vector2 p)
        {
            foreach (int ri in roads)
            {
                ImportedRoad rd = map.roads[ri];
                for (int i = 0; i < rd.pts.Count - 1; i++)
                {
                    Vector2 a = rd.pts[i], b = rd.pts[i + 1], ab = b - a;
                    float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
                    if (Vector2.Distance(p, a + ab * t) < rd.width * 0.5f + 0.6f) return false;
                }
            }
            return true;
        }

        private static bool PlayerPos(out Vector3 p)
        {
            GameplayDirector d = GameplayDirector.Instance;
            if (d != null && d.Player != null) { p = d.Player.transform.position; return true; }
            Camera c = Camera.main;
            if (c != null) { p = c.transform.position; p.y = 0f; return true; }
            p = Vector3.zero;
            return false;
        }

        private static float GroundAt(Vector3 p)
        {
            RaycastHit hit;
            int mask = ~(1 << RaycastVehicle.PlayerLayer);
            if (Physics.Raycast(new Vector3(p.x, 4f, p.z), Vector3.down, out hit, 8f, mask, QueryTriggerInteraction.Ignore) && hit.point.y < 0.6f)
                return hit.point.y;
            return 0.02f;
        }

        private void Update()
        {
            // Agent qui s'avance vers le véhicule (ou retourne à son poste)
            if (moving != null && moveT < 1f)
            {
                moveT = Mathf.Min(1f, moveT + Time.unscaledDeltaTime / moveDur);
                float e = moveT * moveT * (3f - 2f * moveT);
                moving.position = Vector3.Lerp(moveFrom, moveTo, e);
                // l'agent se tourne d'abord vers où il va, puis vers le conducteur (ou son poste) à l'arrivée
                Vector3 dir = moveTo - moveFrom;
                dir.y = 0f;
                Quaternion walk = dir.sqrMagnitude > 0.04f ? Quaternion.LookRotation(dir) : rotTo;
                moving.rotation = moveT < 0.8f ? Quaternion.Slerp(rotFrom, walk, Mathf.Clamp01(moveT * 5f)) : Quaternion.Slerp(walk, rotTo, (moveT - 0.8f) / 0.2f);
                if (moveT >= 1f) PoliceAgent.SetAction(moving, returning ? HumanAction.Idle : HumanAction.Talk);
            }
            timer -= Time.unscaledDeltaTime;
            if (timer > 0f) return;
            timer = 0.5f;
            Vector3 pp;
            if (!PlayerPos(out pp)) return;
            // Postes trop loin : retirés (sauf l'agent occupé à dresser une amende)
            for (int k = built.Count - 1; k >= 0; k--)
            {
                Post p = posts[built[k]];
                if ((p.Junction - pp).sqrMagnitude < DropDist * DropDist) continue;
                if (p.G1 != null && p.G1.transform != moving) Destroy(p.G1);
                if (p.G2 != null && p.G2.transform != moving) Destroy(p.G2);
                if ((p.G1 == null || p.G1.transform != moving) && (p.G2 == null || p.G2.transform != moving))
                {
                    p.G1 = null;
                    p.G2 = null;
                    built.RemoveAt(k);
                }
            }
            // Postes proches : agents en place
            int cx = Mathf.FloorToInt(pp.x / Cell), cz = Mathf.FloorToInt(pp.z / Cell);
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    List<int> l;
                    if (!grid.TryGetValue(Key(cx + dx, cz + dz), out l)) continue;
                    foreach (int i in l)
                    {
                        Post p = posts[i];
                        if (p.G1 != null || (p.Junction - pp).sqrMagnitude > BuildDist * BuildDist) continue;
                        p.G1 = PoliceAgent.Create(transform, new Vector3(p.P1.x, GroundAt(p.P1), p.P1.z), p.R1, i * 2 + 1);
                        if (p.Two) p.G2 = PoliceAgent.Create(transform, new Vector3(p.P2.x, GroundAt(p.P2), p.P2.z), p.R2, i * 2 + 2);
                        built.Add(i);
                    }
                }
        }

        /// <summary>Agent le plus proche du point (dans le rayon), ou null.</summary>
        public Transform NearestOfficer(Vector3 p, float radius)
        {
            Transform best = null;
            float bd = radius * radius;
            foreach (int i in built)
            {
                Post post = posts[i];
                for (int k = 0; k < 2; k++)
                {
                    GameObject g = k == 0 ? post.G1 : post.G2;
                    if (g == null) continue;
                    Vector3 d = g.transform.position - p;
                    d.y = 0f;
                    if (d.sqrMagnitude < bd) { bd = d.sqrMagnitude; best = g.transform; }
                }
            }
            return best;
        }

        /// <summary>Vrai si le véhicule (rectangle au sol) touche un agent.</summary>
        public bool Bump(Vector3 center, Vector3 fwd, float halfW, float halfL, out Transform who)
        {
            who = null;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) return false;
            fwd.Normalize();
            Vector3 right = new Vector3(fwd.z, 0f, -fwd.x);
            foreach (int i in built)
            {
                Post post = posts[i];
                for (int k = 0; k < 2; k++)
                {
                    GameObject g = k == 0 ? post.G1 : post.G2;
                    if (g == null || g.transform == moving) continue;
                    Vector3 d = g.transform.position - center;
                    if (Mathf.Abs(Vector3.Dot(d, fwd)) < halfL + 0.25f && Mathf.Abs(Vector3.Dot(d, right)) < halfW + 0.3f)
                    {
                        who = g.transform;
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>L'agent vient se placer à côté du véhicule arrêté (côté gauche, face au conducteur).</summary>
        public void Approach(Transform officer, Transform vehicle)
        {
            if (officer == null || vehicle == null) return;
            if (moving != null && moving != officer) Release();
            moving = officer;
            home = officer.position;
            homeRot = officer.rotation;
            Vector3 target = vehicle.position - vehicle.right * 1.7f + vehicle.forward * 0.4f;
            target.y = GroundAt(target);
            moveFrom = officer.position;
            rotFrom = officer.rotation;
            moveTo = target;
            rotTo = Quaternion.LookRotation(Flat(vehicle.position - target));
            moveDur = Mathf.Clamp(Vector3.Distance(moveFrom, moveTo) / 2.4f, 1.2f, 3f);
            moveT = 0f;
            returning = false;
            PoliceAgent.SetAction(officer, HumanAction.Walk);
        }

        /// <summary>Fin du contrôle : l'agent retourne à son poste.</summary>
        public void Release()
        {
            if (moving == null) return;
            moveFrom = moving.position;
            rotFrom = moving.rotation;
            moveTo = home;
            rotTo = homeRot;
            moveDur = Mathf.Clamp(Vector3.Distance(moveFrom, moveTo) / 1.6f, 1.2f, 4f);
            moveT = 0f;
            returning = true;
            PoliceAgent.SetAction(moving, HumanAction.Walk);
            Transform m = moving;
            // Le suivi reste actif le temps du retour ; l'agent n'est plus « occupé »
            StartCoroutine(Forget(m));
        }

        private System.Collections.IEnumerator Forget(Transform m)
        {
            yield return new WaitForSecondsRealtime(moveDur + 0.1f);
            if (moving == m) moving = null;
        }

        /// <summary>Position du poste le plus proche (pour les photos de contrôle).</summary>
        public bool NearestPost(Vector3 p, out Vector3 junction, out Vector3 officerSpot)
        {
            junction = officerSpot = Vector3.zero;
            float bd = float.MaxValue;
            bool found = false;
            foreach (Post post in posts)
            {
                float d = (post.Junction - p).sqrMagnitude;
                if (d < bd) { bd = d; junction = post.Junction; officerSpot = post.P1; found = true; }
            }
            return found;
        }
    }
}
