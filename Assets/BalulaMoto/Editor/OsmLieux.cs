// BALULA MOTO — lieux aménagés de la carte importée (demandes de MATULA DESIGN, novembre 2026) :
//   • ronds-points (junction=roundabout d'OpenStreetMap) : plus aucune maison dedans ; îlot de pelouse bordé, arbres ;
//   • esplanades de l'Échangeur de Limete et du Stade des Martyrs : contour déduit des rues qui les bordent (les rues ne
//     sont pas déplacées), longue clôture de barreaux de fer noirs, pavés partout, pelouses dessinées et arbres ; au
//     stade, terrains de football municipaux sans clôture (terrains d'OSM, sinon emplacements choisis par le jeu, à
//     vérifier) et un terrain de basket ; policiers et joueurs posés en jeu (LieuxAnimes) ;
//   • cité Motel Fikin : contour déduit des rues « Motel Fikin » d'OSM (à vérifier), clôture de béton blanc et de
//     barreaux noirs, entrées contrôlées par la police (barrière levée en jeu : on peut y déposer les clients), écoles
//     génériques et maisons (ville générée) à l'intérieur.
// Aucune image satellite n'est utilisée : seulement les données d'OpenStreetMap.
#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using BalulaMoto.Core;
using BalulaMoto.Runtime;
using UnityEngine;

namespace BalulaMoto.EditorTools
{
    public partial class OsmImporterWindow
    {
        private readonly Dictionary<long, List<Vector4>> lieuRoads = new Dictionary<long, List<Vector4>>();
        private readonly Dictionary<long, List<float>> lieuHw = new Dictionary<long, List<float>>();
        private readonly List<Vector3> treeSpots = new List<Vector3>();
        private readonly List<ImportedArea> lieux = new List<ImportedArea>();
        /// <summary>Ronds-points : x, z = centre ; y = rayon de l'îlot central.</summary>
        private readonly List<Vector3> roundabouts = new List<Vector3>();
        private readonly List<List<Vector2>> waterPolys = new List<List<Vector2>>();
        private readonly StringBuilder lieuxReport = new StringBuilder();
        private List<Vector2> citePoly;

        private struct Lot
        {
            public Vector2 C, X;
            public float Hx, Hz;
            public Vector2 Z { get { return new Vector2(-X.y, X.x); } }
            public Vector2 P(float lx, float lz) { return C + X * lx + Z * lz; }
        }

        // ------------------------------------------------------------------
        // Outils de géométrie
        // ------------------------------------------------------------------
        private static bool InPoly(List<Vector2> poly, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                Vector2 a = poly[i], b = poly[j];
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside;
        }

        /// <summary>Distance signée au contour (négative à l'intérieur).</summary>
        private static float SignedPoly(List<Vector2> poly, Vector2 p)
        {
            float d = float.MaxValue;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++) d = Mathf.Min(d, SegDist(p, poly[j], poly[i]));
            return InPoly(poly, p) ? -d : d;
        }

        private static bool LotOverlap(Lot a, Lot b, float margin)
        {
            Vector2 d = b.C - a.C;
            Vector2[] axes = { a.X, a.Z, b.X, b.Z };
            foreach (Vector2 n in axes)
            {
                float ra = a.Hx * Mathf.Abs(Vector2.Dot(a.X, n)) + a.Hz * Mathf.Abs(Vector2.Dot(a.Z, n));
                float rb = b.Hx * Mathf.Abs(Vector2.Dot(b.X, n)) + b.Hz * Mathf.Abs(Vector2.Dot(b.Z, n));
                if (Mathf.Abs(Vector2.Dot(d, n)) > ra + rb + margin) return false;
            }
            return true;
        }

        /// <summary>Points du contour tous les « step » mètres environ (chaque côté découpé régulièrement).</summary>
        private static List<Vector2> Resample(List<Vector2> poly, float step)
        {
            List<Vector2> r = new List<Vector2>();
            for (int i = 0; i < poly.Count; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % poly.Count];
                int m = Mathf.Max(1, Mathf.RoundToInt(Vector2.Distance(a, b) / step));
                for (int k = 0; k < m; k++) r.Add(Vector2.Lerp(a, b, k / (float)m));
            }
            return r;
        }

        private static List<Vector2> Circle(Vector2 c, float r, int n)
        {
            List<Vector2> p = new List<Vector2>(n);
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                p.Add(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
            }
            return p;
        }

        /// <summary>Polygone horizontal en petits triangles (brouillard calculé aux sommets sur téléphone).</summary>
        private static void FinePolygon(MeshBatch mb, List<Vector2> pts, float y, float maxEdge)
        {
            List<int> tri = Triangulator.Triangulate(pts);
            for (int t = 0; t + 2 < tri.Count; t += 3) SplitTri(mb, pts[tri[t]], pts[tri[t + 1]], pts[tri[t + 2]], y, maxEdge, 0);
        }

        private static void SplitTri(MeshBatch mb, Vector2 a, Vector2 b, Vector2 c, float y, float maxEdge, int depth)
        {
            float ab = (b - a).sqrMagnitude, bc = (c - b).sqrMagnitude, ca = (a - c).sqrMagnitude;
            float m = Mathf.Max(ab, Mathf.Max(bc, ca));
            if (m <= maxEdge * maxEdge || depth > 12)
            {
                int i = mb.V.Count;
                mb.V.Add(V3(a, y)); mb.V.Add(V3(b, y)); mb.V.Add(V3(c, y));
                mb.UV.Add(a * 0.1f); mb.UV.Add(b * 0.1f); mb.UV.Add(c * 0.1f);
                mb.T.Add(i); mb.T.Add(i + 1); mb.T.Add(i + 2);
                return;
            }
            if (m == ab) { Vector2 p = (a + b) * 0.5f; SplitTri(mb, a, p, c, y, maxEdge, depth + 1); SplitTri(mb, p, b, c, y, maxEdge, depth + 1); }
            else if (m == bc) { Vector2 p = (b + c) * 0.5f; SplitTri(mb, a, b, p, y, maxEdge, depth + 1); SplitTri(mb, a, p, c, y, maxEdge, depth + 1); }
            else { Vector2 p = (c + a) * 0.5f; SplitTri(mb, a, b, p, y, maxEdge, depth + 1); SplitTri(mb, p, b, c, y, maxEdge, depth + 1); }
        }

        /// <summary>Bordure basse autour d'un polygone (sens direct) : dessus et face extérieure.</summary>
        private static void RingCurb(MeshBatch m, List<Vector2> pts, float w, float h)
        {
            if (Triangulator.Area(pts) < 0f) pts.Reverse();
            Vector2 uv = Vector2.zero;
            for (int i = 0; i < pts.Count; i++)
            {
                Vector2 a = pts[i], b = pts[(i + 1) % pts.Count];
                Vector2 d = b - a;
                if (d.sqrMagnitude < 1e-4f) continue;
                d.Normalize();
                Vector2 outN = new Vector2(d.y, -d.x);
                Vector2 ai = a - outN * w, bi = b - outN * w;
                m.QuadFacing(V3(a, h), V3(b, h), V3(bi, h), V3(ai, h), uv, uv, uv, uv, Vector3.up);
                m.QuadFacing(V3(a, -0.02f), V3(b, -0.02f), V3(b, h), V3(a, h), uv, uv, uv, uv, new Vector3(outN.x, 0f, outN.y));
            }
        }

        /// <summary>Trait blanc peint au sol.</summary>
        private static void LineSeg(MeshBatch m, Vector2 a, Vector2 b, float w, float y)
        {
            Vector2 d = b - a;
            if (d.sqrMagnitude < 1e-4f) return;
            d.Normalize();
            Vector2 n = new Vector2(-d.y, d.x) * (w * 0.5f);
            Vector2 uv = Vector2.zero;
            m.QuadFacing(V3(a + n, y), V3(b + n, y), V3(b - n, y), V3(a - n, y), uv, uv, uv, uv, Vector3.up);
        }

        // ------------------------------------------------------------------
        // Grille de toutes les rues (dessertes comprises)
        // ------------------------------------------------------------------
        private void IndexAllRoads(List<ImportedRoad> axes)
        {
            lieuRoads.Clear();
            lieuHw.Clear();
            foreach (ImportedRoad axis in axes)
            {
                for (int i = 0; i < axis.pts.Count - 1; i++)
                {
                    Vector2 a = axis.pts[i], b = axis.pts[i + 1];
                    int x0 = Mathf.FloorToInt(Mathf.Min(a.x, b.x) / 50f), x1 = Mathf.FloorToInt(Mathf.Max(a.x, b.x) / 50f);
                    int z0 = Mathf.FloorToInt(Mathf.Min(a.y, b.y) / 50f), z1 = Mathf.FloorToInt(Mathf.Max(a.y, b.y) / 50f);
                    if ((x1 - x0 + 1) * (z1 - z0 + 1) > 400) continue;
                    for (int gz = z0; gz <= z1; gz++)
                        for (int gx = x0; gx <= x1; gx++)
                        {
                            long key = ((long)gx << 32) ^ (uint)gz;
                            List<Vector4> l;
                            if (!lieuRoads.TryGetValue(key, out l)) { l = new List<Vector4>(); lieuRoads[key] = l; lieuHw[key] = new List<float>(); }
                            l.Add(new Vector4(a.x, a.y, b.x, b.y));
                            lieuHw[key].Add(axis.width * 0.5f);
                        }
                }
            }
        }

        /// <summary>Distance au bord de la rue la plus proche (dessertes comprises), négative sur une chaussée.</summary>
        private float Clearance(Vector2 p) { return RoadClearance(lieuRoads, lieuHw, p); }

        private void PrepareLieux(OsmData d, List<ImportedRoad> axes)
        {
            treeSpots.Clear();
            lieux.Clear();
            roundabouts.Clear();
            waterPolys.Clear();
            lieuxReport.Length = 0;
            citePoly = null;
            IndexAllRoads(axes);
            foreach (OsmWay w in d.Ways)
            {
                string v;
                if (!w.Tags.TryGetValue("natural", out v) || v != "water" || w.Refs.Count < 4) continue;
                bool complete;
                List<Vector2> pts = Points(d, w, out complete);
                if (complete && pts.Count >= 4 && InRange(pts[0])) waterPolys.Add(pts);
            }
        }

        private bool InWaterPoly(Vector2 p, float margin)
        {
            foreach (List<Vector2> w in waterPolys)
            {
                if ((w[0] - p).sqrMagnitude > 400f * 400f) continue;
                if (SignedPoly(w, p) < margin) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------
        // Ronds-points
        // ------------------------------------------------------------------
        private static int FindRoot(int[] parent, int i)
        {
            while (parent[i] != i) { parent[i] = parent[parent[i]]; i = parent[i]; }
            return i;
        }

        private void PlanRoundabouts(OsmData d)
        {
            List<OsmWay> rw = new List<OsmWay>();
            foreach (OsmWay w in d.Ways)
            {
                string j, hw;
                if (w.Tags.TryGetValue("junction", out j) && (j == "roundabout" || j == "circular") && w.Tags.TryGetValue("highway", out hw) && RoadWidth.ContainsKey(hw)) rw.Add(w);
            }
            int n = rw.Count;
            if (n == 0) return;
            int[] parent = new int[n];
            for (int i = 0; i < n; i++) parent[i] = i;
            Dictionary<long, int> nodeWay = new Dictionary<long, int>();
            for (int i = 0; i < n; i++)
                foreach (long id in rw[i].Refs)
                {
                    int o;
                    if (nodeWay.TryGetValue(id, out o)) parent[FindRoot(parent, i)] = FindRoot(parent, o);
                    else nodeWay[id] = i;
                }
            Dictionary<int, List<int>> groups = new Dictionary<int, List<int>>();
            for (int i = 0; i < n; i++)
            {
                int r = FindRoot(parent, i);
                List<int> l;
                if (!groups.TryGetValue(r, out l)) { l = new List<int>(); groups[r] = l; }
                l.Add(i);
            }
            foreach (List<int> g in groups.Values)
            {
                HashSet<long> seen = new HashSet<long>();
                List<Vector2> pts = new List<Vector2>();
                float hw = 0f;
                foreach (int i in g)
                {
                    string hv;
                    if (rw[i].Tags.TryGetValue("highway", out hv)) hw = Mathf.Max(hw, RoadWidth[hv] * 0.5f);
                    foreach (long id in rw[i].Refs)
                    {
                        OsmNode nd;
                        if (seen.Add(id) && d.Nodes.TryGetValue(id, out nd)) pts.Add(nd.P);
                    }
                }
                if (pts.Count < 5) continue;
                Vector2 c = Vector2.zero;
                foreach (Vector2 p in pts) c += p;
                c /= pts.Count;
                if (!InRange(c)) continue;
                float mean = 0f;
                foreach (Vector2 p in pts) mean += Vector2.Distance(p, c);
                mean /= pts.Count;
                float dev = 0f;
                foreach (Vector2 p in pts) dev += Mathf.Abs(Vector2.Distance(p, c) - mean);
                dev /= pts.Count;
                if (dev > mean * 0.3f) continue;
                // Anneau complet (pas un simple arc)
                List<float> ang = new List<float>();
                foreach (Vector2 p in pts) ang.Add(Mathf.Atan2(p.y - c.y, p.x - c.x));
                ang.Sort();
                float gap = ang[0] + Mathf.PI * 2f - ang[ang.Count - 1];
                for (int k = 1; k < ang.Count; k++) gap = Mathf.Max(gap, ang[k] - ang[k - 1]);
                if (gap > 1.9f) continue;
                float inner = mean - hw - 0.4f;
                if (inner < 2.5f) continue;
                roundabouts.Add(new Vector3(c.x, inner, c.y));
            }
        }

        /// <summary>Vrai si un bâtiment est dans l'îlot d'un rond-point.</summary>
        private bool InRoundabout(List<Vector2> pts, Vector2 center)
        {
            foreach (Vector3 r in roundabouts)
            {
                Vector2 c = new Vector2(r.x, r.z);
                if ((center - c).sqrMagnitude > (r.y + 60f) * (r.y + 60f)) continue;
                if (Vector2.Distance(center, c) < r.y + 2f) return true;
                foreach (Vector2 p in pts) if (Vector2.Distance(p, c) < r.y) return true;
            }
            return false;
        }

        /// <summary>Îlot des ronds-points : pelouse, bordure blanche, arbres.</summary>
        private void BuildRoundabouts(Dictionary<string, MeshBatch> decor, Dictionary<string, MeshBatch> areas, List<Vector3> clearZones)
        {
            foreach (Vector3 r in roundabouts)
            {
                Vector2 c = new Vector2(r.x, r.z);
                float inner = r.y;
                Vector2Int cell = Cell(c);
                int seg = Mathf.Clamp(Mathf.RoundToInt(inner * 2.2f), 16, 48);
                List<Vector2> lawn = Circle(c, Mathf.Max(1f, inner - 0.3f), seg);
                Batch(areas, cell.x + "_" + cell.y + "_vert").Polygon(lawn, 0.02f);
                RingCurb(Batch(decor, "rondpoint_" + cell.x + "_" + cell.y + "_bordure"), Circle(c, inner, seg), 0.3f, 0.2f);
                clearZones.Add(new Vector3(c.x, inner + 0.6f, c.y));
                // Arbres : un au milieu, une couronne si l'îlot est grand
                System.Random rnd = new System.Random(Mathf.RoundToInt(c.x * 7f + c.y * 13f) & 0xffff);
                if (inner >= 4f) treeSpots.Add(new Vector3(c.x, Mathf.Min(8.5f, 4.5f + inner * 0.25f), c.y));
                if (inner >= 11f)
                {
                    float rr = inner * 0.62f;
                    int count = Mathf.Clamp(Mathf.FloorToInt(2f * Mathf.PI * rr / 10f), 4, 14);
                    float off = (float)rnd.NextDouble() * 6.28f;
                    for (int k = 0; k < count; k++)
                    {
                        float a = off + k * Mathf.PI * 2f / count;
                        Vector2 q = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr;
                        treeSpots.Add(new Vector3(q.x, 5f + (float)rnd.NextDouble() * 2.2f, q.y));
                    }
                }
            }
            if (roundabouts.Count > 0) lieuxReport.Append("Ronds-points (OSM junction=roundabout) : ").Append(roundabouts.Count).Append(" îlots en pelouse avec arbres, aucune maison dedans.\n");
        }

        // ------------------------------------------------------------------
        // Région délimitée par les rues (grille de 2 m) : remplissage, fermeture, contour
        // ------------------------------------------------------------------
        private static byte[] Dilate(byte[] src, int n, int r)
        {
            byte[] dst = new byte[n * n];
            List<Vector2Int> offs = new List<Vector2Int>();
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                    if (dx * dx + dy * dy <= r * r) offs.Add(new Vector2Int(dx, dy));
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    if (src[j * n + i] == 0) continue;
                    foreach (Vector2Int o in offs)
                    {
                        int x = i + o.x, y = j + o.y;
                        if (x >= 0 && y >= 0 && x < n && y < n) dst[y * n + x] = 1;
                    }
                }
            return dst;
        }

        private static byte[] Erode(byte[] src, int n, int r)
        {
            byte[] inv = new byte[n * n];
            for (int k = 0; k < n * n; k++) inv[k] = (byte)(src[k] == 0 ? 1 : 0);
            // Hors de la grille : vide (l'érosion ronge les bords)
            byte[] grown = Dilate(inv, n, r);
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                    if (i < r || j < r || i >= n - r || j >= n - r) grown[j * n + i] = 1;
            byte[] dst = new byte[n * n];
            for (int k = 0; k < n * n; k++) dst[k] = (byte)(grown[k] == 0 ? 1 : 0);
            return dst;
        }

        private static void Flood(byte[] reg, byte[] g, int n, Queue<int> q)
        {
            while (q.Count > 0)
            {
                int id = q.Dequeue();
                int i = id % n, j = id / n;
                if (i + 1 < n) Push(reg, g, j * n + i + 1, q);
                if (i > 0) Push(reg, g, j * n + i - 1, q);
                if (j + 1 < n) Push(reg, g, (j + 1) * n + i, q);
                if (j > 0) Push(reg, g, (j - 1) * n + i, q);
            }
        }

        private static void Push(byte[] reg, byte[] g, int id, Queue<int> q)
        {
            if (reg[id] != 0 || (g != null && g[id] != 0)) return;
            reg[id] = 1;
            q.Enqueue(id);
        }

        private List<Vector2> RegionPolygon(Vector2 center, float half, float cell, List<ImportedRoad> axes, System.Predicate<ImportedRoad> blocks,
                                            List<Vector2> seeds, System.Predicate<Vector2> allowed, int closeR, out float leak, out float area)
        {
            leak = 1f;
            area = 0f;
            int n = Mathf.Clamp(Mathf.CeilToInt(2f * half / cell), 8, 900);
            Vector2 org = center - new Vector2(half, half);
            byte[] g = new byte[n * n];
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                    if (!allowed(org + new Vector2((i + 0.5f) * cell, (j + 0.5f) * cell))) g[j * n + i] = 2;
            foreach (ImportedRoad r in axes)
            {
                if (r == null || r.pts == null || !blocks(r)) continue;
                float rad = r.width * 0.5f + Mathf.Max(2.5f, BoulevardRules.SidewalkOuter(r) + 0.6f);
                for (int k = 0; k < r.pts.Count - 1; k++)
                {
                    Vector2 a = r.pts[k], b = r.pts[k + 1];
                    int i0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.x, b.x) - rad - org.x) / cell));
                    int i1 = Mathf.Min(n - 1, Mathf.FloorToInt((Mathf.Max(a.x, b.x) + rad - org.x) / cell));
                    int j0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.y, b.y) - rad - org.y) / cell));
                    int j1 = Mathf.Min(n - 1, Mathf.FloorToInt((Mathf.Max(a.y, b.y) + rad - org.y) / cell));
                    for (int j = j0; j <= j1; j++)
                        for (int i = i0; i <= i1; i++)
                        {
                            if (g[j * n + i] != 0) continue;
                            if (SegDist(org + new Vector2((i + 0.5f) * cell, (j + 0.5f) * cell), a, b) <= rad) g[j * n + i] = 1;
                        }
                }
            }
            byte[] reg = new byte[n * n];
            Queue<int> q = new Queue<int>();
            List<int> seedIds = new List<int>();
            foreach (Vector2 s in seeds)
            {
                int i = Mathf.FloorToInt((s.x - org.x) / cell), j = Mathf.FloorToInt((s.y - org.y) / cell);
                if (i < 0 || j < 0 || i >= n || j >= n) continue;
                int id = j * n + i;
                if (g[id] != 0) continue;
                seedIds.Add(id);
                if (reg[id] != 0) continue;
                reg[id] = 1;
                q.Enqueue(id);
            }
            if (seedIds.Count == 0) return null;
            Flood(reg, g, n, q);
            // Fuite : part du bord de la région qui touche la limite de la zone permise (et non une rue)
            int edgeOut = 0, edgeAll = 0;
            for (int id = 0; id < n * n; id++)
            {
                if (reg[id] == 0) continue;
                int i = id % n, j = id / n;
                for (int k = 0; k < 4; k++)
                {
                    int ni = i + (k == 0 ? 1 : (k == 1 ? -1 : 0)), nj = j + (k == 2 ? 1 : (k == 3 ? -1 : 0));
                    bool outW = ni < 0 || nj < 0 || ni >= n || nj >= n;
                    if (!outW && reg[nj * n + ni] != 0) continue;
                    edgeAll++;
                    if (outW || g[nj * n + ni] == 2) edgeOut++;
                }
            }
            if (edgeAll == 0) return null;
            leak = edgeOut / (float)edgeAll;
            // Fermeture : comble les encoches étroites (bouts de rue, ruelles en cul-de-sac)
            if (closeR > 0)
            {
                reg = Erode(Dilate(reg, n, closeR), n, closeR);
                for (int id = 0; id < n * n; id++) if (g[id] == 2) reg[id] = 0;
            }
            // Partie reliée aux graines seulement
            byte[] keep = new byte[n * n];
            Queue<int> q2 = new Queue<int>();
            byte[] notReg = new byte[n * n];
            for (int id = 0; id < n * n; id++) notReg[id] = (byte)(reg[id] == 0 ? 1 : 0);
            foreach (int id in seedIds)
                if (reg[id] != 0 && keep[id] == 0) { keep[id] = 1; q2.Enqueue(id); }
            Flood(keep, notReg, n, q2);
            // Trous bouchés : tout ce qui n'est pas relié au bord de la grille par l'extérieur
            byte[] outside = new byte[n * n];
            Queue<int> q3 = new Queue<int>();
            for (int k = 0; k < n; k++)
            {
                int[] border = { k, (n - 1) * n + k, k * n, k * n + n - 1 };
                foreach (int id in border)
                    if (keep[id] == 0 && outside[id] == 0) { outside[id] = 1; q3.Enqueue(id); }
            }
            Flood(outside, keep, n, q3);
            int count = 0;
            for (int id = 0; id < n * n; id++)
            {
                reg[id] = (byte)(outside[id] == 0 ? 1 : 0);
                if (reg[id] != 0) count++;
            }
            area = count * cell * cell;
            if (count < 4) return null;
            return Trace(reg, n, org, cell);
        }

        /// <summary>Contour extérieur de la région (sens direct) : arêtes entre cellules pleines et vides, chaînées, simplifiées.</summary>
        private static List<Vector2> Trace(byte[] reg, int n, Vector2 org, float cell)
        {
            Dictionary<long, List<long>> outgoing = new Dictionary<long, List<long>>();
            System.Func<int, int, bool> R = (i, j) => i >= 0 && j >= 0 && i < n && j < n && reg[j * n + i] != 0;
            System.Action<int, int, int, int> add = (i0, j0, i1, j1) =>
            {
                long a = ((long)i0 << 32) | (uint)j0, b = ((long)i1 << 32) | (uint)j1;
                List<long> l;
                if (!outgoing.TryGetValue(a, out l)) { l = new List<long>(2); outgoing[a] = l; }
                l.Add(b);
            };
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    if (!R(i, j)) continue;
                    if (!R(i, j - 1)) add(i, j, i + 1, j);
                    if (!R(i + 1, j)) add(i + 1, j, i + 1, j + 1);
                    if (!R(i, j + 1)) add(i + 1, j + 1, i, j + 1);
                    if (!R(i - 1, j)) add(i, j + 1, i, j);
                }
            List<long> best = null;
            int guard = 0;
            while (outgoing.Count > 0 && guard++ < 10000)
            {
                long v0 = 0;
                foreach (long k in outgoing.Keys) { v0 = k; break; }
                List<long> loop = new List<long> { v0 };
                long prev = v0, cur = Pop(outgoing, v0, 0, 0);
                int steps = 0;
                while (cur != v0 && steps++ < 4 * n * n)
                {
                    loop.Add(cur);
                    int dx = (int)(cur >> 32) - (int)(prev >> 32), dy = (int)(uint)cur - (int)(uint)prev;
                    long next = Pop(outgoing, cur, dx, dy);
                    if (next == long.MinValue) break;
                    prev = cur;
                    cur = next;
                }
                if (best == null || loop.Count > best.Count) best = loop;
            }
            if (best == null || best.Count < 4) return null;
            List<Vector2> pts = new List<Vector2>(best.Count);
            foreach (long v in best) pts.Add(org + new Vector2((int)(v >> 32) * cell, (int)(uint)v * cell));
            List<Vector2> simple = SimplifyClosed(pts, cell * 1.15f);
            if (Triangulator.Area(simple) < 0f) simple.Reverse();
            return simple;
        }

        /// <summary>Retire et renvoie l'arête qui part du sommet ; au pincement, celle qui tourne le plus à gauche.</summary>
        private static long Pop(Dictionary<long, List<long>> outgoing, long v, int dx, int dy)
        {
            List<long> l;
            if (!outgoing.TryGetValue(v, out l) || l.Count == 0) return long.MinValue;
            int bestK = 0;
            if (l.Count > 1 && (dx != 0 || dy != 0))
            {
                int bestScore = int.MinValue;
                for (int k = 0; k < l.Count; k++)
                {
                    int ox = (int)(l[k] >> 32) - (int)(v >> 32), oy = (int)(uint)l[k] - (int)(uint)v;
                    int cross = dx * oy - dy * ox, dot = dx * ox + dy * oy;
                    int score = cross > 0 ? 2 : (cross == 0 && dot > 0 ? 1 : 0);
                    if (score > bestScore) { bestScore = score; bestK = k; }
                }
            }
            long r = l[bestK];
            l.RemoveAt(bestK);
            if (l.Count == 0) outgoing.Remove(v);
            return r;
        }

        private static List<Vector2> SimplifyClosed(List<Vector2> pts, float tol)
        {
            if (pts.Count < 8) return pts;
            int far = 0;
            float fd = -1f;
            for (int i = 0; i < pts.Count; i++)
            {
                float d = (pts[i] - pts[0]).sqrMagnitude;
                if (d > fd) { fd = d; far = i; }
            }
            List<Vector2> a = pts.GetRange(0, far + 1);
            List<Vector2> b = pts.GetRange(far, pts.Count - far);
            b.Add(pts[0]);
            List<Vector2> ra = Rdp(a, tol), rb = Rdp(b, tol);
            ra.RemoveAt(ra.Count - 1);
            rb.RemoveAt(rb.Count - 1);
            ra.AddRange(rb);
            return ra;
        }

        private static List<Vector2> Rdp(List<Vector2> pts, float tol)
        {
            bool[] keep = new bool[pts.Count];
            keep[0] = keep[pts.Count - 1] = true;
            Stack<Vector2Int> st = new Stack<Vector2Int>();
            st.Push(new Vector2Int(0, pts.Count - 1));
            while (st.Count > 0)
            {
                Vector2Int s = st.Pop();
                float dm = 0f;
                int idx = -1;
                for (int i = s.x + 1; i < s.y; i++)
                {
                    float d = SegDist(pts[i], pts[s.x], pts[s.y]);
                    if (d > dm) { dm = d; idx = i; }
                }
                if (idx >= 0 && dm > tol)
                {
                    keep[idx] = true;
                    st.Push(new Vector2Int(s.x, idx));
                    st.Push(new Vector2Int(idx, s.y));
                }
            }
            List<Vector2> r = new List<Vector2>();
            for (int i = 0; i < pts.Count; i++) if (keep[i]) r.Add(pts[i]);
            return r;
        }

        // ------------------------------------------------------------------
        // Esplanades des monuments
        // ------------------------------------------------------------------
        private static bool KeepInEsplanade(Dictionary<string, string> t)
        {
            string v;
            if (t.ContainsKey("leisure") || t.ContainsKey("sport") || t.ContainsKey("amenity")) return true;
            if (t.TryGetValue("building", out v) && (v == "sports_hall" || v == "civic" || v == "public" || v == "government" || v == "church" || v == "school")) return true;
            return false;
        }

        /// <summary>Bâtiment OSM gardé sur l'esplanade du stade (salle de sport, équipement public), hors du modèle.</summary>
        private bool KeepBuilding(Dictionary<string, string> tags, Vector2 center)
        {
            if (!KeepInEsplanade(tags)) return false;
            foreach (Enclosure e in enclosures)
            {
                if (e.Poly == null || e.Id != "stade-martyrs") continue;
                Vector2 dm = center - e.M;
                float ex = Vector2.Dot(dm, e.X) / (e.ModelHx + 10f), ez = Vector2.Dot(dm, e.Z) / (e.ModelHz + 10f);
                if (ex * ex + ez * ez < 1f) return false;
                if (InPoly(e.Poly, center)) return true;
            }
            return false;
        }

        /// <summary>Contour de l'esplanade de chaque monument : la région entourée par les rues (rues publiques ; les
        /// dessertes et parkings restent dedans), limitée autour du monument. Sinon, l'ancien terrain agrandi.</summary>
        private void PlanEsplanades(List<ImportedRoad> axes, Dictionary<long, List<Vector4>> carriage, Dictionary<long, List<float>> carriageHw)
        {
            foreach (Enclosure e in enclosures)
            {
                e.M = e.C;
                bool stade = e.Id == "stade-martyrs";
                float half = stade ? 470f : 160f, cap = stade ? 430f : 118f;
                Vector2 c0 = e.C;
                List<Vector2> seeds = new List<Vector2> { c0 };
                for (int k = 0; k < 16; k++)
                {
                    float a = k * 22.5f * Mathf.Deg2Rad;
                    seeds.Add(c0 + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (stade ? 70f : 18f));
                }
                float leak, area;
                List<Vector2> poly = RegionPolygon(c0, half, 2f, axes, r => r.rank >= 3, seeds, p => (p - c0).sqrMagnitude <= cap * cap, 3, out leak, out area);
                float minArea = stade ? 52000f : 4000f;
                string how;
                if (poly != null && poly.Count >= 4 && leak <= 0.42f && area >= minArea)
                    how = "contour déduit des rues qui l'entourent (" + (area / 10000f).ToString("0.0") + " ha, " + poly.Count + " sommets)";
                else
                {
                    // Secours : terrain du monument agrandi (stade) ou cercle aussi grand que le permettent les chaussées
                    if (stade) { e.Hx += 30f; e.Hz += 30f; e.Corner = Mathf.Min(e.Hx, e.Hz) * 0.5f; poly = e.Outline(3f); }
                    else
                    {
                        float clear = RoadClearance(carriage, carriageHw, c0);
                        float r = Mathf.Clamp(clear - 4f, 26f, 96f);
                        poly = Circle(c0, r, 72);
                    }
                    if (Triangulator.Area(poly) < 0f) poly.Reverse();
                    how = "contour de secours (région des rues " + (poly == null ? "absente" : "fuite " + (leak * 100f).ToString("0") + " %") + ", à vérifier)";
                }
                e.Poly = poly;
                e.Clear = 6f;
                e.Note = (e.Note ?? "") + " ; esplanade : " + how;
                ImportedArea ea = new ImportedArea { kind = "esplanade", name = e.Name, c = e.M, x = e.X, osm = true };
                ea.pts.AddRange(poly);
                lieux.Add(ea);
            }
        }

        /// <summary>Esplanade d'un monument : longue clôture de barreaux noirs, pavés, terrains (stade), pelouses
        /// dessinées, arbres, postes de police aux entrées.</summary>
        private void BuildEsplanade(OsmData d, Enclosure e, Dictionary<string, MeshBatch> decor, Dictionary<string, MeshBatch> areas, List<Vector4> fenceBoxes, StringBuilder report)
        {
            List<Vector2> poly = e.Poly;
            bool stade = e.Id == "stade-martyrs";
            // 1) Clôture de longs barreaux de fer noirs sur un muret, ouverte là où passe une rue
            List<Vector2> fencePts = Resample(poly, 2.6f);
            int n = fencePts.Count;
            bool[] open = new bool[n];
            for (int i = 0; i < n; i++) open[i] = Clearance((fencePts[i] + fencePts[(i + 1) % n]) * 0.5f) < 1.4f;
            bool[] wide = (bool[])open.Clone();
            for (int i = 0; i < n; i++) if (open[i]) { wide[(i + 1) % n] = true; wide[(i - 1 + n) % n] = true; }
            open = wide;
            MeshBatch iron = Batch(decor, e.Id + "_grille"), wall = Batch(decor, e.Id + "_socle");
            float fenceLen = 0f;
            int gates = 0;
            Vector2 uv = Vector2.zero;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = fencePts[i], b = fencePts[(i + 1) % n];
                Vector2 dir = b - a;
                float len = dir.magnitude;
                if (len < 0.2f) continue;
                dir /= len;
                Vector2 nrm = new Vector2(-dir.y, dir.x);
                bool prevOpen = open[(i - 1 + n) % n];
                if (open[i])
                {
                    if (!prevOpen)
                    {
                        Post(wall, a, 0.32f, 0f, 3.1f);
                        gates++;
                        // Policiers en faction à l'entrée : un dedans, un dehors (posés en jeu)
                        Vector2 inward = InPoly(poly, a + nrm * 3f) ? nrm : -nrm;
                        Vector2 g0 = a - dir * 1.5f;
                        lieux.Add(new ImportedArea { kind = "police", name = e.Name, c = g0 + inward * 3.5f, x = -inward, osm = false });
                        lieux.Add(new ImportedArea { kind = "police", name = e.Name, c = g0 - inward * 3.5f, x = inward, osm = false });
                    }
                    if (!open[(i + 1) % n]) Post(wall, b, 0.32f, 0f, 3.1f);
                    continue;
                }
                fenceLen += len;
                Bar(wall, a, b, nrm, 0.32f, 0f, 0.5f);           // muret
                Post(iron, a, 0.06f, 0.5f, 2.95f);               // poteau
                Bar(iron, a, b, nrm, 0.05f, 0.66f, 0.71f);       // lisse basse
                Bar(iron, a, b, nrm, 0.05f, 2.5f, 2.55f);        // lisse haute
                int bars = Mathf.Max(1, Mathf.RoundToInt(len / 0.16f));
                Vector3 n3 = new Vector3(nrm.x, 0f, nrm.y);
                for (int k = 1; k < bars; k++)
                {
                    Vector2 q = Vector2.Lerp(a, b, k / (float)bars);
                    Vector2 w = dir * 0.013f;
                    // Longs barreaux pointus (2,9 m)
                    Vector3 q0 = V3(q - w, 0.5f), q1 = V3(q - w, 2.82f), q2 = V3(q + w, 2.82f), q3 = V3(q + w, 0.5f);
                    iron.QuadFacing(q0, q1, q2, q3, uv, uv, uv, uv, n3);
                    iron.QuadFacing(q0, q1, q2, q3, uv, uv, uv, uv, -n3);
                    Vector3 tip = V3(q, 2.95f);
                    iron.QuadFacing(q1, tip, q2, q2, uv, uv, uv, uv, n3);
                    iron.QuadFacing(q1, tip, q2, q2, uv, uv, uv, uv, -n3);
                }
                Vector2 m2 = (a + b) * 0.5f;
                fenceBoxes.Add(new Vector4(m2.x, m2.y, Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg, len + 0.1f));
            }
            // 2) Pavés sur toute l'esplanade (sous les rues de desserte, la pelouse et les terrains)
            Vector2Int cell = Cell(e.M);
            FinePolygon(Batch(areas, cell.x + "_" + cell.y + "_pave"), poly, 0.013f, 22f);
            // 3) Ce qu'il faut éviter : rues, modèle du monument, bâtiments gardés, eau
            List<Lot> taken = new List<Lot>();
            List<Vector3> blds = new List<Vector3>();
            foreach (OsmWay w in d.Ways)
            {
                if (!w.Tags.ContainsKey("building") || w.Refs.Count < 4) continue;
                bool complete;
                List<Vector2> bp = Points(d, w, out complete);
                if (!complete || bp.Count < 4) continue;
                Vector2 bc = Vector2.zero;
                foreach (Vector2 p in bp) bc += p;
                bc /= bp.Count;
                if ((bc - e.M).sqrMagnitude > 520f * 520f || !InPoly(poly, bc)) continue;
                float br = 0f;
                foreach (Vector2 p in bp) br = Mathf.Max(br, Vector2.Distance(p, bc));
                blds.Add(new Vector3(bc.x, br, bc.y));
            }
            System.Func<Vector2, float, bool> free = (p, margin) =>
            {
                if (SignedPoly(poly, p) > -3.5f) return false;
                if (Clearance(p) < margin) return false;
                Vector2 dm = p - e.M;
                float ex = Vector2.Dot(dm, e.X) / (e.ModelHx + 8f), ez = Vector2.Dot(dm, e.Z) / (e.ModelHz + 8f);
                if (ex * ex + ez * ez < 1f) return false;
                foreach (Vector3 b in blds) if ((new Vector2(b.x, b.z) - p).sqrMagnitude < (b.y + 3f) * (b.y + 3f)) return false;
                if (InWaterPoly(p, 2.5f)) return false;
                return true;
            };
            System.Func<Lot, float, bool> lotFree = (lot, margin) =>
            {
                foreach (Lot t in taken) if (LotOverlap(lot, t, 3f)) return false;
                for (float lx = -1f; lx <= 1.001f; lx += 0.5f)
                    for (float lz = -1f; lz <= 1.001f; lz += 0.5f)
                        if (!free(lot.P(lx * lot.Hx, lz * lot.Hz), margin)) return false;
                return true;
            };
            int pitches = 0, osmPitches = 0, courts = 0, lawns = 0, trees = 0;
            // 4) Stade : terrains de football municipaux (OSM d'abord) et terrain de basket, sans clôture
            if (stade)
            {
                foreach (OsmWay w in d.Ways)
                {
                    string lv;
                    if (!w.Tags.TryGetValue("leisure", out lv) || lv != "pitch" || w.Refs.Count < 4) continue;
                    bool complete;
                    List<Vector2> pp = Points(d, w, out complete);
                    if (!complete || pp.Count < 4) continue;
                    Vector2 pc;
                    Vector2 ax = MainAxis(pp, out pc);
                    if ((pc - e.M).sqrMagnitude > 500f * 500f || !InPoly(poly, pc)) continue;
                    Vector2 dm = pc - e.M;
                    float ex = Vector2.Dot(dm, e.X) / (e.ModelHx + 4f), ez = Vector2.Dot(dm, e.Z) / (e.ModelHz + 4f);
                    if (ex * ex + ez * ez < 1f) continue; // terrain du stade lui-même (dans le modèle)
                    Vector2 az = new Vector2(-ax.y, ax.x);
                    float hx = 0f, hz = 0f;
                    foreach (Vector2 p in pp) { hx = Mathf.Max(hx, Mathf.Abs(Vector2.Dot(p - pc, ax))); hz = Mathf.Max(hz, Mathf.Abs(Vector2.Dot(p - pc, az))); }
                    if (hz > hx) { float t = hx; hx = hz; hz = t; Vector2 tv = ax; ax = az; az = new Vector2(-tv.y, tv.x); }
                    if (hx < 8f || hz < 5f) continue;
                    string sv, surf;
                    w.Tags.TryGetValue("sport", out sv);
                    w.Tags.TryGetValue("surface", out surf);
                    bool basket = sv != null && sv.Contains("basketball") && !sv.Contains("soccer");
                    Lot lot = new Lot { C = pc, X = ax, Hx = hx, Hz = hz };
                    string kind = basket ? "court" : (surf == "artificial_turf" || surf == "grass" ? "turf" : "terre");
                    DrawPitch(decor, e.Id + "_p" + pitches, lot, kind, basket);
                    lieux.Add(new ImportedArea { kind = basket ? "basket" : "foot", name = "Terrain OSM n° " + w.Id, c = pc, x = ax, hx = hx, hz = hz, osm = true });
                    taken.Add(lot);
                    pitches++;
                    osmPitches++;
                    if (basket) courts++;
                }
                // Petits terrains municipaux et terrain de basket choisis par le jeu dans les espaces libres
                Vector2[] sizes = { new Vector2(21f, 13f), new Vector2(21f, 13f), new Vector2(14f, 7.5f) };
                Vector2 bmin = poly[0], bmax = poly[0];
                foreach (Vector2 p in poly) { bmin = Vector2.Min(bmin, p); bmax = Vector2.Max(bmax, p); }
                for (int si = 0; si < sizes.Length; si++)
                {
                    bool basket = si == 2;
                    if (basket && courts > 0) continue;
                    Lot bestLot = new Lot();
                    float bestD = float.MaxValue;
                    for (int pass = 0; pass < 2; pass++)
                    {
                        Vector2 ax = pass == 0 ? e.X : e.Z;
                        for (float gx = bmin.x; gx <= bmax.x; gx += 6f)
                            for (float gz = bmin.y; gz <= bmax.y; gz += 6f)
                            {
                                Lot lot = new Lot { C = new Vector2(gx, gz), X = ax, Hx = sizes[si].x, Hz = sizes[si].y };
                                float dd = (lot.C - e.M).sqrMagnitude;
                                if (dd >= bestD || !lotFree(lot, 3f)) continue;
                                bestD = dd;
                                bestLot = lot;
                            }
                    }
                    if (bestD == float.MaxValue) continue;
                    DrawPitch(decor, e.Id + "_p" + pitches, bestLot, basket ? "court" : "terre", basket);
                    lieux.Add(new ImportedArea { kind = basket ? "basket" : "foot", name = basket ? "Terrain de basket" : "Terrain municipal", c = bestLot.C, x = bestLot.X, hx = bestLot.Hx, hz = bestLot.Hz, osm = false });
                    taken.Add(bestLot);
                    pitches++;
                    if (basket) courts++;
                }
            }
            // 5) Pelouses dessinées (parterres bordés de blanc) et arbres
            MeshBatch curb = Batch(decor, e.Id + "_bordure");
            System.Random rnd = new System.Random(e.Id.GetHashCode() & 0xffff);
            if (stade)
            {
                Vector2 bmin = poly[0], bmax = poly[0];
                foreach (Vector2 p in poly) { bmin = Vector2.Min(bmin, p); bmax = Vector2.Max(bmax, p); }
                float ext = Mathf.Max(bmax.x - bmin.x, bmax.y - bmin.y);
                int row = 0;
                for (float lz = -ext; lz <= ext; lz += 15f, row++)
                    for (float lx = -ext + (row % 2) * 11f; lx <= ext; lx += 22f)
                    {
                        Vector2 c = e.M + e.X * lx + e.Z * lz;
                        bool round = ((row + Mathf.RoundToInt(lx / 22f)) % 3) == 0;
                        Lot lot = new Lot { C = c, X = e.X, Hx = round ? 5.5f : 7f, Hz = round ? 5.5f : 4f };
                        if (!lotFree(lot, 2.5f)) continue;
                        List<Vector2> shape = round ? Circle(c, 5.2f, 20) : Chamfer(lot, 1.4f);
                        Vector2Int lc = Cell(c);
                        Batch(areas, lc.x + "_" + lc.y + "_vert").Polygon(shape, 0.045f);
                        RingCurb(curb, round ? Circle(c, 5.4f, 20) : Chamfer(new Lot { C = c, X = e.X, Hx = lot.Hx + 0.2f, Hz = lot.Hz + 0.2f }, 1.5f), 0.16f, 0.14f);
                        taken.Add(lot);
                        lawns++;
                        if (round) { treeSpots.Add(new Vector3(c.x, 6f + (float)rnd.NextDouble() * 2f, c.y)); trees++; }
                        else
                        {
                            for (int s = -1; s <= 1; s += 2)
                            {
                                Vector2 tq = c + e.X * (s * (lot.Hx - 2.2f));
                                treeSpots.Add(new Vector3(tq.x, 5.2f + (float)rnd.NextDouble() * 2.2f, tq.y));
                                trees++;
                            }
                        }
                    }
            }
            else
            {
                // Échangeur : place pavée autour de la tour, couronne de pelouse coupée par des allées, pétales de pelouse
                float rIn = Mathf.Max(e.ModelHx + 8f, 28f);
                for (int s = 0; s < 8; s++)
                {
                    List<Vector2> sec = Sector(e.M, rIn, rIn + 9f, s * 45f + 4f, s * 45f + 41f, 6);
                    if (!SectorFree(sec, free)) continue;
                    Vector2Int lc = Cell(e.M);
                    Batch(areas, lc.x + "_" + lc.y + "_vert").Polygon(sec, 0.045f);
                    RingCurb(curb, new List<Vector2>(sec), 0.16f, 0.14f);
                    lawns++;
                }
                float rMid = rIn + 13f;
                for (int s = 0; s < 12; s++)
                {
                    float a0 = s * 30f + 5f, a1 = s * 30f + 25f;
                    for (float rOut = rMid + 26f; rOut >= rMid + 8f; rOut -= 4f)
                    {
                        List<Vector2> sec = Sector(e.M, rMid + 4f, rOut, a0, a1, 6);
                        if (!SectorFree(sec, free)) continue;
                        Vector2 sc = Vector2.zero;
                        foreach (Vector2 p in sec) sc += p;
                        sc /= sec.Count;
                        Vector2Int lc = Cell(sc);
                        Batch(areas, lc.x + "_" + lc.y + "_vert").Polygon(sec, 0.045f);
                        RingCurb(curb, new List<Vector2>(sec), 0.16f, 0.14f);
                        lawns++;
                        treeSpots.Add(new Vector3(sc.x, 5.5f + (float)rnd.NextDouble() * 2.5f, sc.y));
                        trees++;
                        break;
                    }
                }
                // Arbres le long de l'allée circulaire entre la couronne et les pétales
                for (int k = 0; k < 24; k++)
                {
                    float a = (k * 15f + 7.5f) * Mathf.Deg2Rad;
                    Vector2 q = e.M + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (rIn + 11f);
                    if (k % 2 == 1 || !free(q, 2.5f)) continue;
                    treeSpots.Add(new Vector3(q.x, 5f + (float)rnd.NextDouble() * 2f, q.y));
                    trees++;
                }
                // La foule fait la fête sur la place autour de la tour et sur les allées
                lieux.Add(new ImportedArea { kind = "fete", name = e.Name, c = e.M, hx = rIn - 3f, hz = e.ModelHx + 3f, osm = false });
                for (int k = 0; k < 4; k++)
                {
                    float a = (k * 90f + 22.5f) * Mathf.Deg2Rad;
                    Vector2 q = e.M + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (rIn + 11f);
                    if (free(q, 2f)) lieux.Add(new ImportedArea { kind = "fete", name = e.Name, c = q, hx = 5f, hz = 0f, osm = false });
                }
            }
            // 6) Arbres le long de la clôture, à l'intérieur
            for (int i = 0; i < n; i += 5)
            {
                Vector2 a = fencePts[i], b = fencePts[(i + 1) % n];
                Vector2 dir = (b - a).normalized;
                Vector2 nrm = new Vector2(-dir.y, dir.x);
                Vector2 q = (a + b) * 0.5f + (InPoly(poly, (a + b) * 0.5f + nrm * 4f) ? nrm : -nrm) * 4.5f;
                bool ok = free(q, 2.5f);
                foreach (Lot t in taken) if (ok && LotOverlap(new Lot { C = q, X = e.X, Hx = 1.5f, Hz = 1.5f }, t, 1f)) ok = false;
                if (!ok) continue;
                treeSpots.Add(new Vector3(q.x, 5.5f + (float)rnd.NextDouble() * 2.5f, q.y));
                trees++;
            }
            report.Append("Monument « ").Append(e.Name).Append(" » : modèle ").Append(e.Model).Append(" posé à (").Append(e.M.x.ToString("0")).Append(", ").Append(e.M.y.ToString("0"))
                  .Append("), angle ").Append(e.Yaw.ToString("0")).Append("° — emplacement : ").Append(e.Where).Append(" ; ").Append(e.Note).Append(". Esplanade pavée clôturée de longs barreaux noirs : ")
                  .Append(fenceLen.ToString("0")).Append(" m, ").Append(gates).Append(" entrée(s), ").Append(lawns).Append(" pelouses dessinées, ").Append(trees).Append(" arbres");
            if (stade) report.Append(", ").Append(pitches).Append(" terrains sans clôture (dont ").Append(osmPitches).Append(" d'OpenStreetMap ; les autres : emplacement choisi par le jeu, à vérifier), ").Append(courts).Append(" terrain(s) de basket");
            report.Append(".\n");
        }

        private static List<Vector2> Chamfer(Lot l, float c)
        {
            return new List<Vector2>
            {
                l.P(-l.Hx + c, -l.Hz), l.P(l.Hx - c, -l.Hz), l.P(l.Hx, -l.Hz + c), l.P(l.Hx, l.Hz - c),
                l.P(l.Hx - c, l.Hz), l.P(-l.Hx + c, l.Hz), l.P(-l.Hx, l.Hz - c), l.P(-l.Hx, -l.Hz + c)
            };
        }

        /// <summary>Secteur d'anneau (pétale de pelouse) entre les rayons r0 et r1, angles a0 à a1 (degrés).</summary>
        private static List<Vector2> Sector(Vector2 c, float r0, float r1, float a0, float a1, int steps)
        {
            List<Vector2> p = new List<Vector2>();
            for (int i = 0; i <= steps; i++)
            {
                float a = Mathf.Lerp(a0, a1, i / (float)steps) * Mathf.Deg2Rad;
                p.Add(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r1);
            }
            for (int i = steps; i >= 0; i--)
            {
                float a = Mathf.Lerp(a0, a1, i / (float)steps) * Mathf.Deg2Rad;
                p.Add(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r0);
            }
            return p;
        }

        private static bool SectorFree(List<Vector2> sec, System.Func<Vector2, float, bool> free)
        {
            foreach (Vector2 p in sec) if (!free(p, 2.5f)) return false;
            Vector2 c = Vector2.zero;
            foreach (Vector2 p in sec) c += p;
            return free(c / sec.Count, 2.5f);
        }

        /// <summary>Terrain de sport sans clôture : surface (terre battue, gazon synthétique ou béton), lignes blanches, buts
        /// ou paniers de basket.</summary>
        private void DrawPitch(Dictionary<string, MeshBatch> decor, string id, Lot lot, string surface, bool basket)
        {
            MeshBatch surf = Batch(decor, id + "_" + surface), line = Batch(decor, id + "_ligne");
            Vector2 uv = Vector2.zero;
            int nx = Mathf.Max(1, Mathf.CeilToInt(lot.Hx * 2f / 18f)), nz = Mathf.Max(1, Mathf.CeilToInt(lot.Hz * 2f / 18f));
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    float x0 = -lot.Hx + 2f * lot.Hx * i / nx, x1 = -lot.Hx + 2f * lot.Hx * (i + 1) / nx;
                    float z0 = -lot.Hz + 2f * lot.Hz * j / nz, z1 = -lot.Hz + 2f * lot.Hz * (j + 1) / nz;
                    surf.QuadFacing(V3(lot.P(x0 - 1f, z0 - 1f), 0.05f), V3(lot.P(x1 + 1f, z0 - 1f), 0.05f), V3(lot.P(x1 + 1f, z1 + 1f), 0.05f), V3(lot.P(x0 - 1f, z1 + 1f), 0.05f), uv, uv, uv, uv, Vector3.up);
                }
            const float W = 0.12f, Y = 0.062f;
            float hx = lot.Hx, hz = lot.Hz;
            Vector2 c00 = lot.P(-hx, -hz), c10 = lot.P(hx, -hz), c11 = lot.P(hx, hz), c01 = lot.P(-hx, hz);
            LineSeg(line, c00, c10, W, Y); LineSeg(line, c10, c11, W, Y); LineSeg(line, c11, c01, W, Y); LineSeg(line, c01, c00, W, Y);
            LineSeg(line, lot.P(0f, -hz), lot.P(0f, hz), W, Y);
            float cr = basket ? 1.8f : Mathf.Min(9.15f, hz * 0.32f);
            List<Vector2> circ = Circle(lot.C, cr, 28);
            for (int i = 0; i < circ.Count; i++) LineSeg(line, circ[i], circ[(i + 1) % circ.Count], W, Y);
            for (int s = -1; s <= 1; s += 2)
            {
                if (basket)
                {
                    // Raquette, poteau, panneau et cercle orange
                    float kd = Mathf.Min(5.8f, hx * 0.4f), kw = Mathf.Min(2.45f, hz * 0.33f);
                    LineSeg(line, lot.P(s * hx, -kw), lot.P(s * (hx - kd), -kw), W, Y);
                    LineSeg(line, lot.P(s * hx, kw), lot.P(s * (hx - kd), kw), W, Y);
                    LineSeg(line, lot.P(s * (hx - kd), -kw), lot.P(s * (hx - kd), kw), W, Y);
                    MeshBatch pole = Batch(decor, id + "_poteau"), rim = Batch(decor, id + "_panier"), board = Batch(decor, id + "_planche");
                    Vector2 pp = lot.P(s * (hx + 0.7f), 0f);
                    Post(pole, pp, 0.08f, 0f, 3.35f);
                    Bar(pole, pp, lot.P(s * (hx - 1.1f), 0f), lot.Z, 0.12f, 3.2f, 3.32f);
                    Vector2 bc = lot.P(s * (hx - 1.15f), 0f);
                    Bar(board, bc - lot.Z * 0.9f, bc + lot.Z * 0.9f, lot.X, 0.06f, 2.9f, 3.95f);
                    Vector2 rc = lot.P(s * (hx - 1.45f), 0f);
                    Vector2 rx = lot.X * 0.23f, rz = lot.Z * 0.23f;
                    Bar(rim, rc - rx - rz, rc - rx + rz, lot.X, 0.03f, 3.03f, 3.06f);
                    Bar(rim, rc + rx - rz, rc + rx + rz, lot.X, 0.03f, 3.03f, 3.06f);
                    Bar(rim, rc - rx - rz, rc + rx - rz, lot.Z, 0.03f, 3.03f, 3.06f);
                    Bar(rim, rc - rx + rz, rc + rx + rz, lot.Z, 0.03f, 3.03f, 3.06f);
                }
                else
                {
                    // Surface de réparation et buts (poteaux et barre blancs)
                    float bd = Mathf.Min(16.5f, hx * 0.3f), bw = Mathf.Min(20f, hz * 0.62f);
                    LineSeg(line, lot.P(s * hx, -bw), lot.P(s * (hx - bd), -bw), W, Y);
                    LineSeg(line, lot.P(s * hx, bw), lot.P(s * (hx - bd), bw), W, Y);
                    LineSeg(line, lot.P(s * (hx - bd), -bw), lot.P(s * (hx - bd), bw), W, Y);
                    float gw = hz >= 25f ? 3.66f : 2.5f, gh = hz >= 25f ? 2.44f : 2f;
                    MeshBatch goal = Batch(decor, id + "_planche");
                    Vector2 g0 = lot.P(s * hx, -gw), g1 = lot.P(s * hx, gw);
                    Post(goal, g0, 0.06f, 0f, gh);
                    Post(goal, g1, 0.06f, 0f, gh);
                    Bar(goal, g0, g1, lot.X, 0.12f, gh - 0.12f, gh);
                }
            }
        }

        // ------------------------------------------------------------------
        // Cité Motel Fikin
        // ------------------------------------------------------------------
        private void BuildCite(List<ImportedRoad> axes, List<ImportedPlace> places, Dictionary<string, MeshBatch> decor, List<Vector4> fenceBoxes, StringBuilder report)
        {
            List<ImportedRoad> streets = new List<ImportedRoad>();
            foreach (ImportedRoad r in axes) if (TextUtil.Normalize(r.name ?? "") == "motel fikin") streets.Add(r);
            if (streets.Count == 0) { report.Append("Cité Motel Fikin : aucune rue « Motel Fikin » dans la zone importée — cité non construite (à vérifier).\n"); return; }
            List<Vector2> seeds = new List<Vector2>();
            Vector2 sum = Vector2.zero;
            int cnt = 0;
            foreach (ImportedRoad r in streets)
                for (int i = 0; i < r.pts.Count - 1; i++)
                {
                    Vector2 a = r.pts[i], b = r.pts[i + 1];
                    int m = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / 2f));
                    for (int k = 0; k <= m; k++) { Vector2 p = Vector2.Lerp(a, b, k / (float)m); seeds.Add(p); sum += p; cnt++; }
                }
            Vector2 c0 = sum / Mathf.Max(1, cnt);
            const float buffer = 42f;
            System.Predicate<Vector2> allowed = p =>
            {
                foreach (ImportedRoad r in streets)
                    for (int i = 0; i < r.pts.Count - 1; i++)
                        if (SegDist(p, r.pts[i], r.pts[i + 1]) <= buffer) return true;
                return false;
            };
            System.Predicate<ImportedRoad> blocks = r =>
            {
                if (r.rank < 3 || string.IsNullOrEmpty(r.name)) return false;
                return TextUtil.Normalize(r.name) != "motel fikin";
            };
            float leak, area;
            List<Vector2> poly = RegionPolygon(c0, 420f, 2f, axes, blocks, seeds, allowed, 4, out leak, out area);
            if (poly == null || area < 3000f) { report.Append("Cité Motel Fikin : contour introuvable autour des rues « Motel Fikin » — cité non construite (à vérifier).\n"); return; }
            citePoly = poly;
            ImportedArea ca = new ImportedArea { kind = "cite", name = "Cité Motel Fikin", c = c0, osm = true };
            ca.pts.AddRange(poly);
            lieux.Add(ca);
            // Clôture : muret de béton blanc, barreaux de fer noirs, piliers blancs ; entrées ouvertes là où passe une rue
            List<Vector2> fp = Resample(poly, 2.6f);
            int n = fp.Count;
            bool[] open = new bool[n];
            for (int i = 0; i < n; i++) open[i] = Clearance((fp[i] + fp[(i + 1) % n]) * 0.5f) < 1.1f;
            bool[] wide = (bool[])open.Clone();
            for (int i = 0; i < n; i++) if (open[i]) { wide[(i + 1) % n] = true; wide[(i - 1 + n) % n] = true; }
            open = wide;
            MeshBatch iron = Batch(decor, "cite_grille"), white = Batch(decor, "cite_blanc");
            Vector2 uv = Vector2.zero;
            float fenceLen = 0f;
            int gates = 0;
            int runStart = -1;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = fp[i], b = fp[(i + 1) % n];
                Vector2 dir = b - a;
                float len = dir.magnitude;
                if (len < 0.2f) continue;
                dir /= len;
                Vector2 nrm = new Vector2(-dir.y, dir.x);
                bool prevOpen = open[(i - 1 + n) % n];
                if (open[i])
                {
                    if (!prevOpen) { Post(white, a, 0.34f, 0f, 3.0f); Post(white, a, 0.4f, 3.0f, 3.12f); runStart = i; }
                    if (!open[(i + 1) % n])
                    {
                        Post(white, b, 0.34f, 0f, 3.0f);
                        Post(white, b, 0.4f, 3.0f, 3.12f);
                        if (runStart >= 0)
                        {
                            // Entrée contrôlée : milieu du passage et direction de la rue qui entre
                            Vector2 g0 = fp[runStart], g1 = b;
                            Vector2 gc = (g0 + g1) * 0.5f;
                            int gateRank;
                            Vector2 rd = NearestRoadDir(axes, gc, out gateRank);
                            // Barrière et policiers seulement sur les vraies rues (pas sur une desserte ou une piste)
                            if (gateRank < 3) { runStart = -1; continue; }
                            Vector2 inward = InPoly(poly, gc + rd * 6f) ? rd : -rd;
                            lieux.Add(new ImportedArea { kind = "porte", name = "Entrée de la cité Motel Fikin", c = gc, x = inward, hx = Vector2.Distance(g0, g1) * 0.5f, osm = false });
                            gates++;
                        }
                        runStart = -1;
                    }
                    continue;
                }
                fenceLen += len;
                Bar(white, a, b, nrm, 0.3f, 0f, 0.75f);           // muret de béton blanc
                Bar(white, a, b, nrm, 0.36f, 0.75f, 0.82f);       // chaperon
                Post(iron, a, 0.05f, 0.82f, 2.4f);
                Bar(iron, a, b, nrm, 0.05f, 0.95f, 1.0f);
                Bar(iron, a, b, nrm, 0.05f, 2.24f, 2.29f);
                int bars = Mathf.Max(1, Mathf.RoundToInt(len / 0.15f));
                Vector3 n3 = new Vector3(nrm.x, 0f, nrm.y);
                for (int k = 1; k < bars; k++)
                {
                    Vector2 q = Vector2.Lerp(a, b, k / (float)bars);
                    Vector2 w = dir * 0.012f;
                    Vector3 q0 = V3(q - w, 0.82f), q1 = V3(q - w, 2.36f), q2 = V3(q + w, 2.36f), q3 = V3(q + w, 0.82f);
                    iron.QuadFacing(q0, q1, q2, q3, uv, uv, uv, uv, n3);
                    iron.QuadFacing(q0, q1, q2, q3, uv, uv, uv, uv, -n3);
                }
                if (i % 2 == 0) { Post(white, a, 0.22f, 0f, 2.6f); Post(white, a, 0.27f, 2.6f, 2.7f); }
                Vector2 m2 = (a + b) * 0.5f;
                fenceBoxes.Add(new Vector4(m2.x, m2.y, Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg, len + 0.1f));
            }
            // Écoles (génériques) : grands terrains libres au bord des rues de la cité
            int schools = 0;
            List<Lot> chosen = new List<Lot>();
            foreach (ImportedRoad r in streets)
            {
                for (int i = 0; i < r.pts.Count - 1 && schools < 2; i++)
                {
                    Vector2 a = r.pts[i], b = r.pts[i + 1];
                    float len = Vector2.Distance(a, b);
                    if (len < 20f) continue;
                    Vector2 dir = (b - a) / len, nrm = new Vector2(-dir.y, dir.x);
                    for (float t = 16f; t <= len - 16f && schools < 2; t += 6f)
                        for (int s = -1; s <= 1 && schools < 2; s += 2)
                        {
                            Vector2 inward = nrm * s;
                            Lot lot = new Lot { C = a + dir * t + inward * (r.width * 0.5f + 3.2f + 10f), X = dir * s, Hx = 15f, Hz = 10f };
                            bool ok = true;
                            foreach (Lot o in chosen) if ((o.C - lot.C).sqrMagnitude < 80f * 80f) ok = false;
                            for (float lx = -1f; lx <= 1.001f && ok; lx += 0.5f)
                                for (float lz = -1f; lz <= 1.001f && ok; lz += 0.5f)
                                {
                                    Vector2 p = lot.C + dir * (lx * lot.Hx) + inward * (lz * lot.Hz);
                                    if (SignedPoly(poly, p) > -2.5f || Clearance(p) < 0.8f) ok = false;
                                }
                            if (!ok) continue;
                            chosen.Add(lot);
                            lieux.Add(new ImportedArea { kind = "ecole", name = "École", c = lot.C, x = dir * s, hx = lot.Hx, hz = lot.Hz, osm = false });
                            schools++;
                        }
                }
            }
            // Destination des clients à l'intérieur de la cité : point de rue le plus proche du centre du contour
            Vector2 pc = Vector2.zero;
            foreach (Vector2 p in poly) pc += p;
            pc /= poly.Count;
            Vector2 best = seeds[0];
            foreach (Vector2 p in seeds) if ((p - pc).sqrMagnitude < (best - pc).sqrMagnitude) best = p;
            ImportedPlace ip = Place("cite-motel-fikin", "Cité Motel Fikin", "quartier", best, 6, false, false);
            ip.status = "Rues « Motel Fikin » d'OpenStreetMap ; limites de la cité déduites des rues, à vérifier";
            places.Add(ip);
            report.Append("Cité Motel Fikin : contour déduit de ").Append(streets.Count).Append(" rues « Motel Fikin » d'OpenStreetMap (≈ ").Append((area / 10000f).ToString("0.0"))
                  .Append(" ha, limites à vérifier) ; clôture de béton blanc et barreaux noirs : ").Append(fenceLen.ToString("0")).Append(" m, ").Append(gates)
                  .Append(" entrée(s) contrôlée(s) par la police (barrière levée à l'approche) ; ").Append(schools)
                  .Append(" école(s) générique(s) (emplacement choisi par le jeu, aucune école de la cité dans OSM) ; destination des clients à l'intérieur.\n");
        }

        private static Vector2 NearestRoadDir(List<ImportedRoad> axes, Vector2 p, out int rank)
        {
            rank = 0;
            float best = float.MaxValue;
            Vector2 dir = Vector2.right;
            foreach (ImportedRoad r in axes)
            {
                if (r.pts.Count < 2 || (r.pts[0] - p).sqrMagnitude > 1500f * 1500f) continue;
                for (int i = 0; i < r.pts.Count - 1; i++)
                {
                    float d = SegDist(p, r.pts[i], r.pts[i + 1]);
                    if (d >= best) continue;
                    Vector2 v = r.pts[i + 1] - r.pts[i];
                    if (v.sqrMagnitude < 0.01f) continue;
                    best = d;
                    dir = v.normalized;
                    rank = r.rank;
                }
            }
            return dir;
        }
    }
}
#endif
