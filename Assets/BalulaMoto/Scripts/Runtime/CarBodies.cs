// BALULA MOTO — carrosseries lisses de la circulation (taxis jaunes, berlines, minibus, bus, pick-up de
// police, camion) : maillages générés par lissage de sections (passages de roue, capot, pare-brise incliné,
// montants, vitres teintées transparentes), partagés entre tous les véhicules du même type.
// Silhouettes génériques inspirées des véhicules courants à Kinshasa ; aucun logo de marque.
using System.Collections.Generic;
using UnityEngine;

namespace BalulaMoto.Runtime
{
    public static partial class VehicleBuilder
    {
        private class BodySpec
        {
            public string Key;
            public float L, W;
            public float Sill = 0.32f;
            public float ArchTop = 0.68f, ArchHalf = 0.4f;
            public float[] WheelZ = new float[0];
            public float NoseLen = 0.45f, TailLen = 0.4f;
            public float NoseDrop = 0.15f, TailDrop = 0.08f;
            public float HoodY = 0.86f, BeltY = 0.92f, DeckY = 0.95f;
            public float WindBaseZ, WindTopZ, RoofEndZ, RearBaseZ;
            public float RoofY = 1.4f, RoofHalfW = 0.62f;
            public float[] Pillars = new float[0];
            public float SideCurve = 7f;
        }

        private static readonly Dictionary<string, Mesh[]> bodyMeshes = new Dictionary<string, Mesh[]>();
        private static readonly Dictionary<string, Material> carMats = new Dictionary<string, Material>();

        // ------------------------------------------------------------------
        // Profil de la caisse
        // ------------------------------------------------------------------
        private static float Smooth01(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

        private static float EndU(BodySpec s, float z, out float tail)
        {
            float front = s.L * 0.5f, rear = -s.L * 0.5f;
            tail = Mathf.Clamp01((rear + s.TailLen - z) / Mathf.Max(0.01f, s.TailLen));
            return Mathf.Clamp01((z - (front - s.NoseLen)) / Mathf.Max(0.01f, s.NoseLen));
        }

        private static float TopAt(BodySpec s, float z)
        {
            float top = s.BeltY;
            if (z > s.WindBaseZ) top = Mathf.Lerp(s.BeltY, s.HoodY, Smooth01((z - s.WindBaseZ) / 0.35f));
            else if (z < s.RearBaseZ) top = Mathf.Lerp(s.BeltY, s.DeckY, Smooth01((s.RearBaseZ - z) / 0.3f));
            float ut, un = EndU(s, z, out ut);
            return top - s.NoseDrop * un * un - s.TailDrop * ut * ut;
        }

        private static float BottomAt(BodySpec s, float z)
        {
            float bottom = s.Sill;
            foreach (float wz in s.WheelZ)
            {
                float d = Mathf.Abs(z - wz) / s.ArchHalf;
                if (d < 1f) bottom = Mathf.Max(bottom, s.Sill + (s.ArchTop - s.Sill) * Mathf.Sqrt(1f - d * d));
            }
            float ut, un = EndU(s, z, out ut);
            return bottom + 0.12f * un * un + 0.1f * ut * ut;
        }

        private static float HalfWidthAt(BodySpec s, float z)
        {
            float ut, un = EndU(s, z, out ut);
            float u = Mathf.Max(un, ut);
            const float k = 2.6f;
            float f = Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Pow(u, k)), 1f / k);
            return s.W * 0.5f * Mathf.Max(0.35f, f);
        }

        private static float RoofAt(BodySpec s, float z, float belt)
        {
            if (z >= s.WindTopZ) return Mathf.Lerp(s.RoofY, belt, Mathf.Clamp01((z - s.WindTopZ) / Mathf.Max(0.01f, s.WindBaseZ - s.WindTopZ)));
            if (z <= s.RoofEndZ) return Mathf.Lerp(s.RoofY, belt, Mathf.Clamp01((s.RoofEndZ - z) / Mathf.Max(0.01f, s.RoofEndZ - s.RearBaseZ)));
            return s.RoofY;
        }

        /// <summary>Point d'une section en super-ellipse (dessus plus plat que le dessous).</summary>
        private static Vector3 SectionPoint(float theta, float hw, float yc, float b, float z, float nTop, float nBottom)
        {
            float c = Mathf.Cos(theta), si = Mathf.Sin(theta);
            float e = 2f / (si >= 0f ? nTop : nBottom);
            float x = hw * Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), e);
            float y = yc + b * Mathf.Sign(si) * Mathf.Pow(Mathf.Abs(si), e);
            return new Vector3(x, y, z);
        }

        /// <summary>Position z de la face avant (ou arrière) de la caisse à l'abscisse x.</summary>
        private static float SurfaceZ(BodySpec s, float x, bool front)
        {
            float step = 0.01f;
            if (front)
            {
                for (float z = s.L * 0.5f; z > 0f; z -= step) if (HalfWidthAt(s, z) >= Mathf.Abs(x)) return z;
                return s.L * 0.5f;
            }
            for (float z = -s.L * 0.5f; z < 0f; z += step) if (HalfWidthAt(s, z) >= Mathf.Abs(x)) return z;
            return -s.L * 0.5f;
        }

        // ------------------------------------------------------------------
        // Maillages
        // ------------------------------------------------------------------
        private static Mesh LowerBodyMesh(BodySpec s)
        {
            int nz = Mathf.Clamp(Mathf.RoundToInt(s.L / 0.09f), 30, 140), m = 28;
            List<Vector3> v = new List<Vector3>(nz * m + 2 * (m + 1));
            List<int> tri = new List<int>();
            for (int i = 0; i < nz; i++)
            {
                float z = Mathf.Lerp(-s.L * 0.5f, s.L * 0.5f, i / (float)(nz - 1));
                float top = TopAt(s, z), bottom = BottomAt(s, z), hw = HalfWidthAt(s, z);
                float yc = (top + bottom) * 0.5f, b = Mathf.Max(0.02f, (top - bottom) * 0.5f);
                for (int j = 0; j < m; j++) v.Add(SectionPoint(2f * Mathf.PI * j / m, hw, yc, b, z, s.SideCurve, 4f));
            }
            for (int i = 0; i < nz - 1; i++)
                for (int j = 0; j < m; j++)
                {
                    int j2 = (j + 1) % m;
                    int a = i * m + j, bb = i * m + j2, c = (i + 1) * m + j, d = (i + 1) * m + j2;
                    tri.Add(a); tri.Add(bb); tri.Add(c);
                    tri.Add(bb); tri.Add(d); tri.Add(c);
                }
            // Faces avant et arrière (sommets dupliqués pour une arête nette)
            for (int end = 0; end < 2; end++)
            {
                int ring = end == 0 ? 0 : nz - 1;
                Vector3 center = Vector3.zero;
                for (int j = 0; j < m; j++) center += v[ring * m + j];
                center /= m;
                int ci = v.Count;
                v.Add(center);
                int first = v.Count;
                for (int j = 0; j < m; j++) v.Add(v[ring * m + j]);
                for (int j = 0; j < m; j++)
                {
                    int p = first + j, q = first + (j + 1) % m;
                    if (end == 1) { tri.Add(ci); tri.Add(p); tri.Add(q); }
                    else { tri.Add(ci); tri.Add(q); tri.Add(p); }
                }
            }
            Mesh mesh = new Mesh { name = "Caisse_" + s.Key };
            mesh.SetVertices(v);
            mesh.SetTriangles(tri, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Habitacle : sous-maillage 0 = vitres, 1 = pavillon et montants (peinture).</summary>
        private static Mesh GreenhouseMesh(BodySpec s)
        {
            float z0 = s.RearBaseZ, z1 = s.WindBaseZ;
            int nz = Mathf.Clamp(Mathf.RoundToInt((z1 - z0) / 0.05f), 12, 240), k = 16;
            List<Vector3> v = new List<Vector3>();
            List<int> glass = new List<int>(), paint = new List<int>();
            float[] zs = new float[nz];
            float[] sins = new float[k + 1];
            float[] coss = new float[k + 1];
            for (int i = 0; i < nz; i++)
            {
                float z = Mathf.Lerp(z0, z1, i / (float)(nz - 1));
                zs[i] = z;
                float belt = TopAt(s, z) - 0.04f;
                float top = Mathf.Max(belt + 0.002f, RoofAt(s, z, belt));
                float hwBase = HalfWidthAt(s, z) * 0.88f;
                float hwRoof = Mathf.Min(s.RoofHalfW, hwBase);
                for (int j = 0; j <= k; j++)
                {
                    float th = Mathf.PI * j / k;
                    float c = Mathf.Cos(th), si = Mathf.Sin(th);
                    sins[j] = si;
                    coss[j] = Mathf.Abs(c);
                    float e = 2f / 6f;
                    float hw = Mathf.Lerp(hwBase, hwRoof, si * si);
                    float x = hw * Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), e);
                    float y = belt + (top - belt) * Mathf.Pow(si, e);
                    v.Add(new Vector3(x, y, z));
                }
            }
            for (int i = 0; i < nz - 1; i++)
            {
                float zm = (zs[i] + zs[i + 1]) * 0.5f;
                bool roofZone = zm <= s.WindTopZ && zm >= s.RoofEndZ;
                bool windZone = zm > s.WindTopZ;
                bool rearZone = zm < s.RoofEndZ;
                bool pillar = false;
                foreach (float pz in s.Pillars) if (Mathf.Abs(zm - pz) < 0.055f) pillar = true;
                for (int j = 0; j < k; j++)
                {
                    float sm = Mathf.Min(sins[j], sins[j + 1]);
                    float cm = (coss[j] + coss[j + 1]) * 0.5f;
                    bool roofBand = sm > 0.9f;
                    bool painted = (roofZone && roofBand) || (windZone && cm > 0.42f && cm < 0.86f) ||
                                   (rearZone && cm > 0.3f && cm < 0.9f) || (pillar && !roofBand);
                    int a = i * (k + 1) + j, bb = a + 1, c = a + (k + 1), d = c + 1;
                    List<int> t = painted ? paint : glass;
                    t.Add(a); t.Add(bb); t.Add(c);
                    t.Add(bb); t.Add(d); t.Add(c);
                }
            }
            Mesh mesh = new Mesh { name = "Habitacle_" + s.Key };
            mesh.SetVertices(v);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(glass, 0);
            mesh.SetTriangles(paint, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh[] BodyMeshes(BodySpec s)
        {
            Mesh[] m;
            if (bodyMeshes.TryGetValue(s.Key, out m) && m[0] != null) return m;
            m = new[] { LowerBodyMesh(s), s.RoofY > s.BeltY + 0.05f ? GreenhouseMesh(s) : null };
            bodyMeshes[s.Key] = m;
            return m;
        }

        // ------------------------------------------------------------------
        // Matériaux : peinture métallisée, vitres teintées transparentes, plastique, chrome
        // ------------------------------------------------------------------
        private static Material Paint(Color c, float metallic = 0.35f, float smooth = 0.78f)
        {
            string key = "P" + ColorUtility.ToHtmlStringRGB(c) + metallic.ToString("0.00") + smooth.ToString("0.00");
            Material m;
            if (carMats.TryGetValue(key, out m) && m != null) return m;
            m = new Material(Mats.Lit(c, smooth));
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            m.enableInstancing = true;
            carMats[key] = m;
            return m;
        }

        private static Material CarGlass()
        {
            Material m;
            if (carMats.TryGetValue("Vitre", out m) && m != null) return m;
            Material baseGlass = Resources.Load<Material>("BalulaMoto/MatGlass");
            if (baseGlass != null)
            {
                m = new Material(baseGlass);
                Color c = new Color(0.08f, 0.11f, 0.14f, 0.5f);
                m.color = c;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            }
            else m = Paint(new Color(0.07f, 0.09f, 0.12f), 0.2f, 0.95f);
            carMats["Vitre"] = m;
            return m;
        }

        private static GameObject MeshPart(Transform parent, string name, Mesh mesh, Material[] mats, Vector3 pos)
        {
            GameObject g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer mr = g.AddComponent<MeshRenderer>();
            mr.sharedMaterials = mats;
            return g;
        }

        // ------------------------------------------------------------------
        // Gabarits par type
        // ------------------------------------------------------------------
        private static BodySpec SpecFor(AIVehicleType type, out float r, out float H)
        {
            BodySpec s = new BodySpec { Key = type.ToString() };
            switch (type)
            {
                case AIVehicleType.MinibusJaune:
                    s.L = 5.3f; s.W = 2.0f; r = 0.36f; H = 2.45f;
                    s.WheelZ = new[] { s.L * 0.5f - 0.78f, -s.L * 0.5f + 1.15f };
                    s.Sill = 0.42f; s.ArchTop = 0.86f; s.ArchHalf = 0.48f;
                    s.NoseLen = 0.5f; s.TailLen = 0.18f; s.NoseDrop = 0.12f; s.TailDrop = 0f;
                    s.HoodY = 1.08f; s.BeltY = 1.24f; s.DeckY = 1.24f;
                    s.WindBaseZ = s.L * 0.5f - 0.5f; s.WindTopZ = s.L * 0.5f - 1.0f; s.RoofEndZ = -s.L * 0.5f + 0.12f; s.RearBaseZ = -s.L * 0.5f + 0.06f;
                    s.RoofY = 2.42f; s.RoofHalfW = 0.92f;
                    s.Pillars = Range(s.WindTopZ - 0.9f, s.RoofEndZ + 0.3f, 1.05f);
                    break;
                case AIVehicleType.MinibusBlanc:
                    s.L = 7.0f; s.W = 2.05f; r = 0.4f; H = 2.6f;
                    s.WheelZ = new[] { s.L * 0.5f - 1.05f, -s.L * 0.5f + 1.6f };
                    s.Sill = 0.45f; s.ArchTop = 0.95f; s.ArchHalf = 0.52f;
                    s.NoseLen = 0.45f; s.TailLen = 0.2f; s.NoseDrop = 0.1f; s.TailDrop = 0f;
                    s.HoodY = 1.15f; s.BeltY = 1.3f; s.DeckY = 1.3f;
                    s.WindBaseZ = s.L * 0.5f - 0.4f; s.WindTopZ = s.L * 0.5f - 0.85f; s.RoofEndZ = -s.L * 0.5f + 0.12f; s.RearBaseZ = -s.L * 0.5f + 0.06f;
                    s.RoofY = 2.58f; s.RoofHalfW = 0.95f;
                    s.Pillars = Range(s.WindTopZ - 0.95f, s.RoofEndZ + 0.3f, 1.1f);
                    break;
                case AIVehicleType.BusTransco:
                    s.L = 11.5f; s.W = 2.5f; r = 0.5f; H = 3.1f;
                    s.WheelZ = new[] { s.L * 0.5f - 2.5f, -s.L * 0.5f + 3.1f };
                    s.Sill = 0.42f; s.ArchTop = 1.12f; s.ArchHalf = 0.64f;
                    s.NoseLen = 0.3f; s.TailLen = 0.25f; s.NoseDrop = 0f; s.TailDrop = 0f;
                    s.HoodY = 1.25f; s.BeltY = 1.3f; s.DeckY = 1.3f;
                    s.WindBaseZ = s.L * 0.5f - 0.04f; s.WindTopZ = s.L * 0.5f - 0.3f; s.RoofEndZ = -s.L * 0.5f + 0.1f; s.RearBaseZ = -s.L * 0.5f + 0.04f;
                    s.RoofY = 3.05f; s.RoofHalfW = 1.2f;
                    s.Pillars = Range(s.WindTopZ - 1.3f, s.RoofEndZ + 0.4f, 1.35f);
                    break;
                case AIVehicleType.PoliceKabasele:
                    s.L = 5.2f; s.W = 1.9f; r = 0.4f; H = 1.95f;
                    s.WheelZ = new[] { s.L * 0.5f - 0.95f, -s.L * 0.5f + 1.25f };
                    s.Sill = 0.55f; s.ArchTop = 0.93f; s.ArchHalf = 0.5f;
                    s.NoseLen = 0.32f; s.TailLen = 0.15f; s.NoseDrop = 0.1f; s.TailDrop = 0f;
                    s.HoodY = 1.3f; s.BeltY = 1.33f; s.DeckY = 1.12f;
                    s.WindBaseZ = s.L * 0.5f - 1.45f; s.WindTopZ = s.WindBaseZ - 0.38f; s.RoofEndZ = s.WindBaseZ - 1.45f; s.RearBaseZ = s.RoofEndZ - 0.06f;
                    s.RoofY = 1.95f; s.RoofHalfW = 0.8f;
                    s.Pillars = new[] { s.WindTopZ - 0.62f };
                    break;
                case AIVehicleType.CamionRemorque:
                    // Cabine avancée seule (la caisse de chargement est ajoutée à part)
                    s.L = 2.35f; s.W = 2.45f; r = 0.5f; H = 3.0f;
                    s.WheelZ = new[] { 0.35f };
                    s.Sill = 0.6f; s.ArchTop = 1.12f; s.ArchHalf = 0.62f;
                    s.NoseLen = 0.28f; s.TailLen = 0.1f; s.NoseDrop = 0.05f; s.TailDrop = 0f;
                    s.HoodY = 1.5f; s.BeltY = 1.55f; s.DeckY = 1.55f;
                    s.WindBaseZ = s.L * 0.5f - 0.08f; s.WindTopZ = s.L * 0.5f - 0.32f; s.RoofEndZ = -s.L * 0.5f + 0.08f; s.RearBaseZ = -s.L * 0.5f + 0.04f;
                    s.RoofY = 2.95f; s.RoofHalfW = 1.1f;
                    s.Pillars = new float[0];
                    break;
                case AIVehicleType.Berline:
                    s.L = 4.55f; s.W = 1.76f; r = 0.31f; H = 1.44f;
                    s.WheelZ = new[] { s.L * 0.5f - 0.9f, -s.L * 0.5f + 1.0f };
                    s.Sill = 0.31f; s.ArchTop = 0.7f; s.ArchHalf = 0.41f;
                    s.HoodY = 0.86f; s.BeltY = 0.94f; s.DeckY = 0.97f;
                    s.WindBaseZ = 0.78f; s.WindTopZ = -0.05f; s.RoofEndZ = -1.0f; s.RearBaseZ = -1.55f;
                    s.RoofY = 1.44f; s.RoofHalfW = 0.64f;
                    s.Pillars = new[] { -0.5f };
                    break;
                default: // TaxiJaune : berline compacte
                    s.L = 4.3f; s.W = 1.69f; r = 0.3f; H = 1.4f;
                    s.WheelZ = new[] { s.L * 0.5f - 0.85f, -s.L * 0.5f + 0.95f };
                    s.Sill = 0.3f; s.ArchTop = 0.68f; s.ArchHalf = 0.4f;
                    s.HoodY = 0.84f; s.BeltY = 0.92f; s.DeckY = 0.95f;
                    s.WindBaseZ = 0.72f; s.WindTopZ = -0.08f; s.RoofEndZ = -0.98f; s.RearBaseZ = -1.48f;
                    s.RoofY = 1.4f; s.RoofHalfW = 0.62f;
                    s.Pillars = new[] { -0.52f };
                    break;
            }
            return s;
        }

        private static float[] Range(float from, float to, float step)
        {
            List<float> l = new List<float>();
            for (float z = from; z > to; z -= step) l.Add(z);
            return l.ToArray();
        }

        // ------------------------------------------------------------------
        // Assemblage d'un véhicule de la circulation (hors motos)
        // ------------------------------------------------------------------
        private static GameObject BuildSmoothTraffic(GameObject root, AIVehicleType type, System.Random rnd, int seed, Color body,
            out float length, List<Renderer> brakeLights, List<Renderer> hazards, List<Transform> wheels)
        {
            Transform t = root.transform;
            float r, H;
            BodySpec s = SpecFor(type, out r, out H);
            bool truck = type == AIVehicleType.CamionRemorque;
            float W = s.W;
            if (type == AIVehicleType.Berline)
            {
                Color[] palette = { new Color(0.85f, 0.86f, 0.88f), new Color(0.1f, 0.1f, 0.11f), new Color(0.95f, 0.95f, 0.95f), new Color(0.12f, 0.18f, 0.35f), new Color(0.55f, 0.08f, 0.08f), new Color(0.45f, 0.47f, 0.5f) };
                body = palette[rnd.Next(palette.Length)];
            }
            float cabZ = 0f, totalL = s.L;
            if (truck) { totalL = 12f; cabZ = totalL * 0.5f - s.L * 0.5f; }
            length = totalL;

            Material paint = Paint(body, type == AIVehicleType.BusTransco || type == AIVehicleType.MinibusBlanc ? 0.15f : 0.35f);
            Material plastic = Paint(new Color(0.09f, 0.09f, 0.1f), 0f, 0.3f);
            Material chrome = Paint(new Color(0.8f, 0.8f, 0.82f), 0.9f, 0.85f);
            Mesh[] meshes = BodyMeshes(s);
            Vector3 bodyPos = new Vector3(0f, 0f, cabZ);
            MeshPart(t, "Caisse", meshes[0], new[] { paint }, bodyPos);
            if (meshes[1] != null) MeshPart(t, "Habitacle", meshes[1], new[] { CarGlass(), paint }, bodyPos);

            // Intérieur sombre visible à travers les vitres (au lieu du dessus de la caisse)
            if (meshes[1] != null)
            {
                float iz0 = s.RearBaseZ + 0.05f, iz1 = s.WindBaseZ - 0.05f;
                MeshPart(t, "Interieur", Prims.Cube, new[] { plastic }, new Vector3(0f, s.BeltY + 0.006f, cabZ + (iz0 + iz1) * 0.5f)).transform.localScale = new Vector3(HalfWidthAt(s, (iz0 + iz1) * 0.5f) * 1.5f, 0.01f, iz1 - iz0);
            }

            // Pare-chocs, calandre, phares, feux
            float frontLowY = BottomAt(s, s.L * 0.5f - 0.05f), frontTopY = TopAt(s, s.L * 0.5f - 0.05f);
            bool bigFront = type == AIVehicleType.BusTransco || type == AIVehicleType.MinibusBlanc || truck;
            Material bumperMat = type == AIVehicleType.TaxiJaune || type == AIVehicleType.Berline ? Paint(ColorUtil.Shade(body, 0.92f)) : plastic;
            MeshPart(t, "PareChocsAV", Prims.Cube, new[] { bumperMat }, new Vector3(0f, frontLowY + 0.08f, s.L * 0.5f + cabZ - 0.06f)).transform.localScale = new Vector3(W * 0.97f, 0.2f, 0.16f);
            if (!truck) MeshPart(t, "PareChocsAR", Prims.Cube, new[] { bumperMat }, new Vector3(0f, BottomAt(s, -s.L * 0.5f + 0.05f) + 0.08f, -s.L * 0.5f + 0.06f)).transform.localScale = new Vector3(W * 0.97f, 0.2f, 0.16f);
            float lampY = Mathf.Lerp(frontLowY, frontTopY, bigFront ? 0.35f : 0.62f);
            MeshPart(t, "Calandre", Prims.Cube, new[] { plastic }, new Vector3(0f, lampY, SurfaceZ(s, W * 0.15f, true) + cabZ - 0.03f)).transform.localScale = new Vector3(W * 0.36f, bigFront ? 0.3f : 0.15f, 0.08f);
            foreach (float sx in new[] { -1f, 1f })
            {
                float hx = sx * W * 0.34f;
                GameObject hl = MeshPart(t, "Phare", Prims.Cube, new[] { Paint(new Color(0.92f, 0.92f, 0.86f), 0.1f, 0.95f) }, new Vector3(hx, lampY, SurfaceZ(s, Mathf.Abs(hx) + 0.1f, true) + cabZ - 0.03f));
                hl.transform.localScale = new Vector3(W * 0.17f, bigFront ? 0.18f : 0.12f, 0.08f);
                float tz = truck ? -totalL * 0.5f + 0.02f : SurfaceZ(s, Mathf.Abs(hx) + 0.1f, false) + 0.03f;
                float tailY = truck ? 0.95f : Mathf.Lerp(BottomAt(s, -s.L * 0.5f + 0.05f), TopAt(s, -s.L * 0.5f + 0.05f), bigFront ? 0.3f : 0.6f);
                brakeLights.Add(Lamp(t, "Stop", new Vector3(sx * W * 0.38f, tailY, tz), new Vector3(W * 0.14f, 0.12f, 0.06f), RedLight));
                hazards.Add(Lamp(t, "Warning", new Vector3(sx * W * 0.45f, tailY + 0.13f, tz), new Vector3(0.07f, 0.07f, 0.05f), ColorUtil.Shade(Amber, 0.3f)));
                hazards.Add(Lamp(t, "WarningAV", new Vector3(sx * W * 0.45f, lampY - (bigFront ? 0.2f : 0.1f), SurfaceZ(s, W * 0.45f, true) + cabZ - 0.02f), new Vector3(0.07f, 0.06f, 0.05f), ColorUtil.Shade(Amber, 0.3f)));
                // Rétroviseurs
                if (s.RoofY > s.BeltY + 0.05f)
                {
                    float mz = s.WindBaseZ + cabZ - (bigFront ? 0.25f : 0.12f);
                    float mx = sx * (HalfWidthAt(s, s.WindBaseZ) + (bigFront ? 0.12f : 0.07f));
                    MeshPart(t, "Retro", Prims.Cube, new[] { bigFront ? plastic : paint }, new Vector3(mx, s.BeltY + (bigFront ? 0.55f : 0.1f), mz)).transform.localScale = bigFront ? new Vector3(0.06f, 0.32f, 0.14f) : new Vector3(0.1f, 0.08f, 0.14f);
                }
            }

            // Roues
            float tyreW = type == AIVehicleType.BusTransco || truck ? 0.3f : 0.21f;
            List<float> axles = new List<float>();
            foreach (float wz in s.WheelZ) axles.Add(wz + cabZ);
            if (truck) { axles.Add(-totalL * 0.5f + 2.0f); axles.Add(-totalL * 0.5f + 3.3f); }
            foreach (float az in axles)
                foreach (float sx in new[] { -1f, 1f })
                    wheels.Add(TrafficWheel(t, new Vector3(sx * (W * 0.5f - tyreW * 0.5f - 0.03f), r, az), r, tyreW));

            // Détails propres à chaque type
            switch (type)
            {
                case AIVehicleType.TaxiJaune:
                {
                    string num = "B " + (1000 + rnd.Next(9000));
                    foreach (float sx in new[] { -1f, 1f })
                    {
                        GameObject l = Prims.Label(t, num, new Vector3(sx * (W * 0.5f + 0.012f), s.Sill + 0.3f, 0.15f), 0.03f, Black, false);
                        l.transform.localRotation = Quaternion.Euler(0f, sx > 0f ? -90f : 90f, 0f);
                    }
                    break;
                }
                case AIVehicleType.MinibusJaune:
                    for (int i = 0; i < 2; i++)
                        foreach (float sx in new[] { -1f, 1f })
                            MeshPart(t, "Bande", Prims.Cube, new[] { Paint(i == 0 ? new Color(0.08f, 0.2f, 0.55f) : Black, 0.2f, 0.6f) }, new Vector3(sx * (W * 0.5f + 0.004f), s.Sill + 0.3f + i * 0.1f, -0.3f)).transform.localScale = new Vector3(0.01f, 0.06f, s.L * 0.72f);
                    if (rnd.Next(2) == 0) RoofLoad(t, s, rnd);
                    {
                        GameObject n = Prims.Label(t, "208", new Vector3(0f, s.BeltY - 0.2f, -s.L * 0.5f - 0.03f), 0.05f, Black, false);
                        n.transform.localRotation = Quaternion.identity;
                    }
                    break;
                case AIVehicleType.BusTransco:
                case AIVehicleType.MinibusBlanc:
                {
                    Color[] stripes = type == AIVehicleType.BusTransco
                        ? new[] { new Color(0f, 0.5f, 1f), new Color(0.97f, 0.8f, 0.1f), new Color(0.85f, 0.1f, 0.1f) }
                        : new[] { new Color(0.9f, 0.2f, 0.2f), new Color(0.98f, 0.6f, 0.1f), new Color(0.98f, 0.85f, 0.1f), new Color(0.2f, 0.7f, 0.3f), new Color(0.2f, 0.4f, 0.9f) };
                    for (int i = 0; i < stripes.Length; i++)
                        foreach (float sx in new[] { -1f, 1f })
                            MeshPart(t, "Bande" + i, Prims.Cube, new[] { Paint(stripes[i], 0.1f, 0.6f) }, new Vector3(sx * (W * 0.5f + 0.006f), s.Sill + 0.3f + i * 0.09f, 0f)).transform.localScale = new Vector3(0.01f, 0.07f, s.L * 0.86f);
                    // Portes côté trottoir (droite)
                    float dz1 = s.L * 0.5f - 1.0f, dz2 = type == AIVehicleType.BusTransco ? -0.6f : -1.2f;
                    foreach (float dz in new[] { dz1, dz2 })
                        MeshPart(t, "Porte", Prims.Cube, new[] { CarGlass() }, new Vector3(W * 0.5f + 0.008f, (s.Sill + s.RoofY - 0.15f) * 0.5f, dz)).transform.localScale = new Vector3(0.01f, s.RoofY - 0.25f - s.Sill, 1.0f);
                    string sign = type == AIVehicleType.BusTransco ? "L01" : "TRANSPORT EN COMMUN";
                    GameObject g = Prims.Label(t, sign, new Vector3(0f, s.RoofY - 0.2f, s.L * 0.5f + 0.03f), 0.035f, new Color(1f, 0.6f, 0.1f), false);
                    g.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    break;
                }
                case AIVehicleType.PoliceKabasele:
                {
                    // Plateau arrière ouvert
                    float bedStart = s.RearBaseZ - 0.08f, bedEnd = -s.L * 0.5f + 0.12f;
                    MeshPart(t, "Plateau", Prims.Cube, new[] { plastic }, new Vector3(0f, s.DeckY + 0.006f, (bedStart + bedEnd) * 0.5f)).transform.localScale = new Vector3(W * 0.78f, 0.01f, bedStart - bedEnd);
                    foreach (float sx in new[] { -1f, 1f })
                    {
                        MeshPart(t, "BandeRouge", Prims.Cube, new[] { Paint(new Color(0.8f, 0.05f, 0.05f), 0.1f, 0.6f) }, new Vector3(sx * (W * 0.5f + 0.006f), s.Sill + 0.35f, 0f)).transform.localScale = new Vector3(0.01f, 0.08f, s.L * 0.85f);
                        MeshPart(t, "BandeJaune", Prims.Cube, new[] { Paint(new Color(0.95f, 0.8f, 0.1f), 0.1f, 0.6f) }, new Vector3(sx * (W * 0.5f + 0.006f), s.Sill + 0.22f, 0f)).transform.localScale = new Vector3(0.01f, 0.06f, s.L * 0.85f);
                        GameObject k = Prims.Label(t, "KABASELE", new Vector3(sx * (W * 0.5f + 0.02f), s.Sill + 0.5f, 0.6f), 0.05f, Color.white, false);
                        k.transform.localRotation = Quaternion.Euler(0f, sx > 0f ? -90f : 90f, 0f);
                    }
                    float barZ = (s.WindTopZ + s.RoofEndZ) * 0.5f;
                    Prims.Box(t, "Rampe", new Vector3(0f, s.RoofY + 0.07f, barZ), new Vector3(W * 0.7f, 0.12f, 0.28f), Color.white);
                    hazards.Add(Lamp(t, "GyroRouge", new Vector3(-W * 0.22f, s.RoofY + 0.15f, barZ), new Vector3(0.3f, 0.08f, 0.24f), new Color(0.6f, 0.05f, 0.05f)));
                    hazards.Add(Lamp(t, "GyroBleu", new Vector3(W * 0.22f, s.RoofY + 0.15f, barZ), new Vector3(0.3f, 0.08f, 0.24f), new Color(0.05f, 0.1f, 0.6f)));
                    GameObject pl = Prims.Label(t, "POLICE", new Vector3(0f, s.RoofY + 0.07f, barZ + 0.15f), 0.03f, Black, false);
                    pl.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    MeshPart(t, "ParBuffle", Prims.Cube, new[] { chrome }, new Vector3(0f, s.Sill + 0.25f, s.L * 0.5f + 0.1f)).transform.localScale = new Vector3(W * 0.8f, 0.45f, 0.06f);
                    break;
                }
                case AIVehicleType.CamionRemorque:
                {
                    float boxFront = cabZ - s.L * 0.5f - 0.15f, boxRear = -totalL * 0.5f + 0.05f;
                    Color cargo = rnd.Next(3) == 0 ? new Color(0.85f, 0.85f, 0.83f) : new Color(0.15f, 0.35f, 0.6f);
                    MeshPart(t, "Chassis", Prims.Cube, new[] { plastic }, new Vector3(0f, r + 0.15f, (boxFront + boxRear) * 0.5f)).transform.localScale = new Vector3(W * 0.7f, 0.3f, boxFront - boxRear);
                    MeshPart(t, "Caisse", Prims.Cube, new[] { Paint(cargo, 0.2f, 0.5f) }, new Vector3(0f, r * 2f + 0.25f + 1.3f, (boxFront + boxRear) * 0.5f)).transform.localScale = new Vector3(W, 2.6f, boxFront - boxRear);
                    foreach (float sx in new[] { -1f, 1f })
                        MeshPart(t, "Nervures", Prims.Cube, new[] { Paint(ColorUtil.Shade(cargo, 0.8f), 0.2f, 0.5f) }, new Vector3(sx * (W * 0.5f + 0.01f), r * 2f + 0.25f + 1.3f, (boxFront + boxRear) * 0.5f)).transform.localScale = new Vector3(0.02f, 2.5f, boxFront - boxRear - 0.3f);
                    break;
                }
            }

            // Conducteur (volant à gauche) et passagers visibles derrière les vitres
            bool hasCabin = s.RoofY > s.BeltY + 0.05f;
            if (hasCabin)
            {
                bool tall = type != AIVehicleType.TaxiJaune && type != AIVehicleType.Berline;
                float hipY = tall ? s.BeltY - 0.3f : s.Sill + 0.25f;
                float driverZ = cabZ + s.WindBaseZ - (type == AIVehicleType.BusTransco ? 0.9f : (tall ? 0.6f : 0.62f));
                GameObject driver = BuildHuman(t, new Vector3(-W * 0.24f, hipY, driverZ), type == AIVehicleType.PoliceKabasele ? "police" : "chauffeur", true, seed + 1, false);
                HumanLibrary.MakeStatic(driver, true);
                int extra = GameSettings.Quality == 0 ? 0 : (type == AIVehicleType.BusTransco ? 3 : (type == AIVehicleType.MinibusJaune || type == AIVehicleType.MinibusBlanc ? 2 : (rnd.Next(10) < 3 ? 1 : 0)));
                for (int i = 0; i < extra; i++)
                {
                    float pz = i == 0 && !tall ? driverZ : driverZ - 0.9f - i * 0.85f;
                    float px = i == 0 && !tall ? W * 0.24f : (i % 2 == 0 ? -1f : 1f) * W * 0.25f;
                    if (pz < cabZ + s.RoofEndZ + 0.2f) break;
                    GameObject p = BuildHuman(t, new Vector3(px, hipY, pz), rnd.Next(2) == 0 ? "pagne" : "civil", true, seed + 7 + i, false);
                    HumanLibrary.MakeStatic(p, true);
                }
            }

            AddTrafficBody(root, new Vector3(W, H - r * 0.5f, totalL), new Vector3(0f, r * 0.5f + (H - r * 0.5f) * 0.5f, 0f));
            return root;
        }

        /// <summary>Galerie de toit chargée (bagages, sacs) des minibus.</summary>
        private static void RoofLoad(Transform t, BodySpec s, System.Random rnd)
        {
            float y = s.RoofY + 0.06f, z0 = s.RoofEndZ + 0.2f, z1 = s.WindTopZ - 0.2f;
            Color rack = new Color(0.12f, 0.12f, 0.12f);
            foreach (float sx in new[] { -1f, 1f }) Prims.Box(t, "Galerie", new Vector3(sx * s.RoofHalfW * 0.9f, y, (z0 + z1) * 0.5f), new Vector3(0.04f, 0.06f, z1 - z0), rack);
            Color[] bags = { new Color(0.55f, 0.4f, 0.25f), new Color(0.2f, 0.3f, 0.6f), new Color(0.6f, 0.15f, 0.12f), new Color(0.75f, 0.7f, 0.55f), new Color(0.15f, 0.45f, 0.2f) };
            int n = 3 + rnd.Next(4);
            for (int i = 0; i < n; i++)
            {
                float z = Mathf.Lerp(z0 + 0.3f, z1 - 0.3f, (float)rnd.NextDouble());
                float x = ((float)rnd.NextDouble() - 0.5f) * s.RoofHalfW;
                Vector3 size = new Vector3(0.4f + (float)rnd.NextDouble() * 0.4f, 0.25f + (float)rnd.NextDouble() * 0.3f, 0.4f + (float)rnd.NextDouble() * 0.5f);
                Prims.Box(t, "Bagage", new Vector3(x, y + size.y * 0.5f, z), size, bags[rnd.Next(bags.Length)]);
            }
        }
    }
}
