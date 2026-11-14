// BALULA MOTO — règles des boulevards (demandes de MATULA DESIGN, novembre 2026) :
//   • le long de tous les boulevards, 10 m libres entre la chaussée et les maisons ou bâtiments ;
//   • boulevard Lumumba : entre le grand boulevard et le petit boulevard parallèle (contre-allée), aucune maison :
//     seulement un espace vert (pelouse et arbres) fermé par une clôture de barreaux de fer noirs.
// Utilisé à l'import de la carte (bâtiments OpenStreetMap écartés) et par la ville générée (CityFill).
// Les rues ne sont pas déplacées : seules les constructions sont retirées de ces bandes.
using System.Collections.Generic;
using BalulaMoto.Core;
using UnityEngine;

namespace BalulaMoto.Runtime
{
    public static class BoulevardRules
    {
        /// <summary>Espace libre entre le bord de la chaussée d'un boulevard et les maisons ou bâtiments.</summary>
        public const float Setback = 10f;
        /// <summary>Trottoir laissé de chaque côté de l'espace vert de Lumumba, hors de la clôture.</summary>
        public const float WalkBand = 2.2f;
        /// <summary>Largeur de bande la plus petite et la plus grande reconnue entre le grand et le petit boulevard.</summary>
        public const float MinGap = 3f, MaxGap = 30f;
        private const float Step = 4f;

        /// <summary>Morceau de la bande entre le grand boulevard Lumumba (côté I) et le petit boulevard (côté O),
        /// entre deux stations successives (0 et 1), largeurs W0 et W1 au niveau de chaque station.</summary>
        public struct Strip
        {
            public Vector2 I0, I1, O0, O1;
            public float W0, W1;
            public int Road;
        }

        public static bool IsBoulevard(ImportedRoad r)
        {
            if (r == null || r.pts == null || r.pts.Count < 2 || r.rank < 1) return false;
            string n = TextUtil.Normalize(r.name ?? "");
            if (n.Length == 0) return false;
            if (n.Contains("boulevard") || n.StartsWith("bd ")) return true;
            // Grands axes que le créateur appelle « boulevards » (ceux des barrières rouges et blanches)
            return r.rank >= 5 && (n.Contains("lumumba") || n.Contains("sendwe") || n.Contains("huilerie") || n.Contains("24 novembre"));
        }

        public static bool IsLumumba(ImportedRoad r)
        {
            return r != null && TextUtil.Normalize(r.name ?? "").Contains("lumumba");
        }

        private static float Cross(Vector2 a, Vector2 b) { return a.x * b.y - a.y * b.x; }
        private static long Key(int x, int z) { return ((long)x << 32) ^ (uint)z; }

        private static float PointSeg(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float l2 = ab.sqrMagnitude;
            float t = l2 < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / l2);
            return Vector2.Distance(p, a + ab * t);
        }

        private static float SegSeg(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
        {
            float d1 = Cross(q2 - q1, p1 - q1), d2 = Cross(q2 - q1, p2 - q1), d3 = Cross(p2 - p1, q1 - p1), d4 = Cross(p2 - p1, q2 - p1);
            if ((d1 > 0f) != (d2 > 0f) && (d3 > 0f) != (d4 > 0f)) return 0f;
            return Mathf.Min(Mathf.Min(PointSeg(p1, q1, q2), PointSeg(p2, q1, q2)), Mathf.Min(PointSeg(q1, p1, p2), PointSeg(q2, p1, p2)));
        }

        /// <summary>Vrai si le point est dans le quadrilatère a-b-c-d (convexe, sens quelconque).</summary>
        public static bool InQuad(Vector2 p, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            float s1 = Cross(b - a, p - a), s2 = Cross(c - b, p - b), s3 = Cross(d - c, p - c), s4 = Cross(a - d, p - d);
            bool neg = s1 < 0f || s2 < 0f || s3 < 0f || s4 < 0f;
            bool pos = s1 > 0f || s2 > 0f || s3 > 0f || s4 > 0f;
            return !(neg && pos);
        }

        /// <summary>Bandes entre le grand boulevard Lumumba et le petit boulevard parallèle, en morceaux de 4 m.
        /// Un rayon part du bord de la chaussée vers l'extérieur ; la première rue touchée doit être parallèle, à
        /// 3-30 m, et être le petit boulevard (même nom et rang plus petit, rue sans nom ou voie de desserte). Une rue
        /// qui traverse la bande (carrefour) l'interrompt ; la seconde chaussée du boulevard (milieu) ne compte pas.</summary>
        public static List<Strip> LumumbaStrips(List<ImportedRoad> roads)
        {
            List<Strip> result = new List<Strip>();
            if (roads == null) return result;
            List<int> mains = new List<int>();
            HashSet<long> near = new HashSet<long>();
            const float G = 50f;
            for (int r = 0; r < roads.Count; r++)
            {
                ImportedRoad rd = roads[r];
                if (rd == null || rd.pts == null || rd.pts.Count < 2 || rd.rank < 5 || !IsLumumba(rd)) continue;
                mains.Add(r);
                foreach (Vector2 p in rd.pts)
                {
                    int cx = Mathf.FloorToInt(p.x / G), cz = Mathf.FloorToInt(p.y / G);
                    for (int dz = -2; dz <= 2; dz++)
                        for (int dx = -2; dx <= 2; dx++) near.Add(Key(cx + dx, cz + dz));
                }
                // Points espacés de plus de 50 m : les cases entre deux points aussi
                for (int i = 0; i < rd.pts.Count - 1; i++)
                {
                    Vector2 a = rd.pts[i], b = rd.pts[i + 1];
                    float len = Vector2.Distance(a, b);
                    for (float t = G; t < len; t += G)
                    {
                        Vector2 p = a + (b - a) * (t / len);
                        int cx = Mathf.FloorToInt(p.x / G), cz = Mathf.FloorToInt(p.y / G);
                        for (int dz = -2; dz <= 2; dz++)
                            for (int dx = -2; dx <= 2; dx++) near.Add(Key(cx + dx, cz + dz));
                    }
                }
            }
            if (mains.Count == 0) return result;
            // Grille des segments de toutes les rues proches du boulevard Lumumba
            Dictionary<long, List<Vector2Int>> grid = new Dictionary<long, List<Vector2Int>>();
            for (int r = 0; r < roads.Count; r++)
            {
                ImportedRoad rd = roads[r];
                if (rd == null || rd.pts == null || rd.rank < 1) continue;
                for (int i = 0; i < rd.pts.Count - 1; i++)
                {
                    Vector2 a = rd.pts[i], b = rd.pts[i + 1];
                    int x0 = Mathf.FloorToInt(Mathf.Min(a.x, b.x) / G), x1 = Mathf.FloorToInt(Mathf.Max(a.x, b.x) / G);
                    int z0 = Mathf.FloorToInt(Mathf.Min(a.y, b.y) / G), z1 = Mathf.FloorToInt(Mathf.Max(a.y, b.y) / G);
                    if ((x1 - x0 + 1) * (z1 - z0 + 1) > 64) continue;
                    for (int z = z0; z <= z1; z++)
                        for (int x = x0; x <= x1; x++)
                        {
                            long k = Key(x, z);
                            if (!near.Contains(k)) continue;
                            List<Vector2Int> l;
                            if (!grid.TryGetValue(k, out l)) { l = new List<Vector2Int>(); grid[k] = l; }
                            l.Add(new Vector2Int(r, i));
                        }
                }
            }
            HashSet<long> seen = new HashSet<long>();
            foreach (int m in mains)
            {
                ImportedRoad main = roads[m];
                float hw = main.width * 0.5f;
                for (int sideI = 0; sideI < 2; sideI++)
                {
                    float side = sideI == 0 ? 1f : -1f;
                    bool have = false;
                    Vector2 lastI = Vector2.zero, lastO = Vector2.zero;
                    float lastW = 0f;
                    for (int i = 0; i < main.pts.Count - 1; i++)
                    {
                        Vector2 a = main.pts[i], b = main.pts[i + 1];
                        float len = Vector2.Distance(a, b);
                        if (len < 0.5f) continue;
                        Vector2 dir = (b - a) / len, nrm = new Vector2(-dir.y, dir.x) * side;
                        int steps = Mathf.Max(1, Mathf.RoundToInt(len / Step));
                        for (int s = 0; s <= steps; s++)
                        {
                            if (s == 0 && have) continue;
                            Vector2 p = a + dir * (len * s / steps);
                            Vector2 inner = p + nrm * hw;
                            float gap;
                            if (Probe(roads, grid, seen, m, main, inner, nrm, dir, out gap))
                            {
                                Vector2 outer = inner + nrm * gap;
                                if (have && Mathf.Abs(gap - lastW) < 5f && (inner - lastI).sqrMagnitude < 7f * 7f && (inner - lastI).sqrMagnitude > 0.25f)
                                    result.Add(new Strip { I0 = lastI, O0 = lastO, I1 = inner, O1 = outer, W0 = lastW, W1 = gap, Road = m });
                                lastI = inner;
                                lastO = outer;
                                lastW = gap;
                                have = true;
                            }
                            else have = false;
                        }
                    }
                }
            }
            return result;
        }

        private static bool Probe(List<ImportedRoad> roads, Dictionary<long, List<Vector2Int>> grid, HashSet<long> seen, int self, ImportedRoad main, Vector2 o, Vector2 n, Vector2 mainDir, out float gap)
        {
            gap = 0f;
            const float G = 50f;
            float reach = MaxGap + 8f;
            Vector2 e = o + n * reach;
            seen.Clear();
            float best = float.MaxValue;
            int bestRoad = -1;
            Vector2 bestDir = Vector2.zero;
            int x0 = Mathf.FloorToInt(Mathf.Min(o.x, e.x) / G), x1 = Mathf.FloorToInt(Mathf.Max(o.x, e.x) / G);
            int z0 = Mathf.FloorToInt(Mathf.Min(o.y, e.y) / G), z1 = Mathf.FloorToInt(Mathf.Max(o.y, e.y) / G);
            for (int z = z0; z <= z1; z++)
                for (int x = x0; x <= x1; x++)
                {
                    List<Vector2Int> l;
                    if (!grid.TryGetValue(Key(x, z), out l)) continue;
                    foreach (Vector2Int si in l)
                    {
                        if (si.x == self) continue;
                        if (!seen.Add(((long)si.x << 24) ^ si.y)) continue;
                        ImportedRoad rd = roads[si.x];
                        Vector2 c = rd.pts[si.y], d = rd.pts[si.y + 1];
                        Vector2 cd = d - c;
                        float cl = cd.magnitude;
                        if (cl < 0.1f) continue;
                        Vector2 cdir = cd / cl;
                        float hwK = rd.width * 0.5f;
                        float den = Cross(n, cdir);
                        float enter;
                        if (Mathf.Abs(den) < 0.35f)
                        {
                            // Rue presque dans l'axe du rayon (rue qui traverse la bande) : elle coupe la bande si elle la touche
                            if (SegSeg(o, e, c, d) > hwK + 0.8f) continue;
                            enter = Mathf.Max(0f, Mathf.Min(Vector2.Dot(c - o, n), Vector2.Dot(d - o, n)) - hwK);
                            if (enter < MaxGap) { best = Mathf.Min(best, enter); if (best == enter) { bestRoad = si.x; bestDir = cdir; } }
                            continue;
                        }
                        Vector2 oc = c - o;
                        float t = Cross(oc, cdir) / den;
                        float u = Cross(oc, n) / den;
                        if (u < -hwK || u > cl + hwK) continue;
                        if (t < -hwK) continue;
                        enter = t - hwK / Mathf.Abs(den);
                        if (enter < best) { best = enter; bestRoad = si.x; bestDir = cdir; }
                    }
                }
            if (bestRoad < 0 || best < MinGap || best > MaxGap) return false;
            ImportedRoad hit = roads[bestRoad];
            if (Mathf.Abs(Vector2.Dot(bestDir, mainDir)) < 0.9f) return false;
            bool lumumba = IsLumumba(hit);
            // Seconde chaussée du grand boulevard (côté milieu) : pas d'espace vert ici (barrières du milieu)
            if (lumumba && hit.rank >= main.rank - 1) return false;
            string hn = TextUtil.Normalize(hit.name ?? "");
            if (!(lumumba || hn.Length == 0 || hit.rank <= 2)) return false;
            gap = best;
            return true;
        }
    }

    /// <summary>Index des boulevards (espace de 10 m) et des bandes vertes de Lumumba, pour les tests rapides.</summary>
    public class BoulevardIndex
    {
        public readonly List<BoulevardRules.Strip> Strips;
        public readonly HashSet<int> Boulevards = new HashSet<int>();
        public readonly float StripLength;
        private readonly List<ImportedRoad> roads;
        private const float G = 25f;
        private readonly Dictionary<long, List<Vector2Int>> bdGrid = new Dictionary<long, List<Vector2Int>>();
        private readonly Dictionary<long, List<int>> stripGrid = new Dictionary<long, List<int>>();

        private static long Key(int x, int z) { return ((long)x << 32) ^ (uint)z; }

        public BoulevardIndex(List<ImportedRoad> roadList)
        {
            roads = roadList ?? new List<ImportedRoad>();
            for (int r = 0; r < roads.Count; r++)
            {
                if (!BoulevardRules.IsBoulevard(roads[r])) continue;
                Boulevards.Add(r);
                ImportedRoad rd = roads[r];
                float pad = rd.width * 0.5f + BoulevardRules.Setback + 2f;
                for (int i = 0; i < rd.pts.Count - 1; i++)
                {
                    Vector2 a = rd.pts[i], b = rd.pts[i + 1];
                    int x0 = Mathf.FloorToInt((Mathf.Min(a.x, b.x) - pad) / G), x1 = Mathf.FloorToInt((Mathf.Max(a.x, b.x) + pad) / G);
                    int z0 = Mathf.FloorToInt((Mathf.Min(a.y, b.y) - pad) / G), z1 = Mathf.FloorToInt((Mathf.Max(a.y, b.y) + pad) / G);
                    if ((x1 - x0 + 1) * (z1 - z0 + 1) > 400) continue;
                    for (int z = z0; z <= z1; z++)
                        for (int x = x0; x <= x1; x++)
                        {
                            List<Vector2Int> l;
                            long k = Key(x, z);
                            if (!bdGrid.TryGetValue(k, out l)) { l = new List<Vector2Int>(); bdGrid[k] = l; }
                            l.Add(new Vector2Int(r, i));
                        }
                }
            }
            Strips = BoulevardRules.LumumbaStrips(roads);
            float total = 0f;
            for (int s = 0; s < Strips.Count; s++)
            {
                BoulevardRules.Strip st = Strips[s];
                total += Vector2.Distance(st.I0, st.I1);
                Vector2 mn = Vector2.Min(Vector2.Min(st.I0, st.I1), Vector2.Min(st.O0, st.O1));
                Vector2 mx = Vector2.Max(Vector2.Max(st.I0, st.I1), Vector2.Max(st.O0, st.O1));
                for (int z = Mathf.FloorToInt(mn.y / G); z <= Mathf.FloorToInt(mx.y / G); z++)
                    for (int x = Mathf.FloorToInt(mn.x / G); x <= Mathf.FloorToInt(mx.x / G); x++)
                    {
                        List<int> l;
                        long k = Key(x, z);
                        if (!stripGrid.TryGetValue(k, out l)) { l = new List<int>(); stripGrid[k] = l; }
                        l.Add(s);
                    }
            }
            StripLength = total;
        }

        /// <summary>Vrai si le point est à moins de 10 m (plus « extra ») du bord de la chaussée d'un boulevard.</summary>
        public bool NearBoulevard(Vector2 p, float extra)
        {
            List<Vector2Int> l;
            if (!bdGrid.TryGetValue(Key(Mathf.FloorToInt(p.x / G), Mathf.FloorToInt(p.y / G)), out l)) return false;
            foreach (Vector2Int si in l)
            {
                ImportedRoad rd = roads[si.x];
                Vector2 a = rd.pts[si.y], b = rd.pts[si.y + 1];
                Vector2 ab = b - a;
                float l2 = ab.sqrMagnitude;
                float t = l2 < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / l2);
                if (Vector2.Distance(p, a + ab * t) < rd.width * 0.5f + BoulevardRules.Setback + extra) return true;
            }
            return false;
        }

        /// <summary>Vrai si le point est dans la bande entre le grand et le petit boulevard Lumumba.</summary>
        public bool InStrip(Vector2 p)
        {
            List<int> l;
            if (!stripGrid.TryGetValue(Key(Mathf.FloorToInt(p.x / G), Mathf.FloorToInt(p.y / G)), out l)) return false;
            foreach (int s in l)
            {
                BoulevardRules.Strip st = Strips[s];
                if (BoulevardRules.InQuad(p, st.I0, st.I1, st.O1, st.O0)) return true;
            }
            return false;
        }

        /// <summary>0 : rien ; 1 : bâtiment à moins de 10 m d'un boulevard ; 2 : bâtiment dans la bande verte de Lumumba.</summary>
        public int Conflict(List<Vector2> poly, Vector2 center)
        {
            if (InStrip(center)) return 2;
            foreach (Vector2 p in poly) if (InStrip(p)) return 2;
            if (NearBoulevard(center, 0f)) return 1;
            foreach (Vector2 p in poly) if (NearBoulevard(p, 0f)) return 1;
            return 0;
        }
    }
}
