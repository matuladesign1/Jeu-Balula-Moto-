// BALULA MOTO — cours d'eau en tranchée de la carte importée (rivières, ruisseaux, canaux, caniveaux d'OpenStreetMap).
// Comme sur la photo de rivière envoyée par MATULA DESIGN : eau turquoise plus bas que le sol, berges en pente couvertes
// d'herbe et de terre (parois de béton pour les canaux et les caniveaux), bande d'herbe le long du bord. Les maisons sont
// écartées des berges, un pont est construit partout où une rue croise l'eau (trottoirs, parapets, tablier) et des
// barrières invisibles empêchent de rouler dans le lit. Les tracés OpenStreetMap ne sont pas déplacés.
//
// Rendu : le sol (shader BalulaMoto/SolTrou) n'est pas dessiné là où le masque de la rivière (BalulaMoto/RiviereMasque)
// a marqué le stencil ; la tranchée (berges, eau, murs de fond) est posée sous le sol et se voit par ce « trou ».
#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using BalulaMoto.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BalulaMoto.EditorTools
{
    public partial class OsmImporterWindow
    {
        private const float GroundY = -0.02f, WaterY = -1.35f;
        // Ponts : trottoir de chaque côté de la chaussée (la foule y marche), parapet de béton, dessous du tablier
        private const float BridgeWalk = 1.8f, BridgeParapet = 0.3f, BridgeUnder = -0.9f;
        private const string RiverShaders = "Assets/BalulaMoto/Resources/BalulaMoto/Shaders/";
        private const string RiverTextures = "Assets/BalulaMoto/Resources/BalulaMoto/Textures/";

        private class Channel
        {
            public string Name = "", Kind = "";
            public float W, Bank;          // largeur de l'eau, largeur nominale d'une berge
            public bool Concrete;          // parois de béton (canal, caniveau) au lieu d'herbe et de terre
            public List<Vector2> P = new List<Vector2>();
            public float[] S, Hc;          // abscisse le long du tracé ; demi-largeur au sommet des berges
            public Vector2[] N, T;         // normale (onglet aux coudes) et tangente à chaque point
        }

        private class Crossing
        {
            public int Road, Ci, Si;
            public Vector2 X, U, N;        // croisement, direction et normale de la rue
            public float Hw, YRoad;
            public bool Big;               // vrai pont (parapet et garde-corps) ; sinon simple dalle à bordure
            public float T0a, T1a, T0b, T1b; // étendue le long de la rue, côté +N puis côté -N
        }

        private class RiverChunk
        {
            public readonly MeshBatch Mask = new MeshBatch(), Water = new MeshBatch(), Bank = new MeshBatch(), Concrete = new MeshBatch();
        }

        private readonly List<Channel> channels = new List<Channel>();
        private readonly Dictionary<long, List<long>> channelGrid = new Dictionary<long, List<long>>();
        private readonly List<Crossing> crossings = new List<Crossing>();
        private readonly Dictionary<long, List<int>> crossingGrid = new Dictionary<long, List<int>>();
        private readonly Dictionary<long, List<Vector4>> allRoads = new Dictionary<long, List<Vector4>>();
        private readonly Dictionary<long, List<float>> allRoadsHw = new Dictionary<long, List<float>>();
        private Shader solShader, maskShader, waterShader;
        private bool trenchMode;
        private int riverRemoved, riverShallow, riverBig;
        private float riverKm, riverWallKm;
        private List<Vector4> riverSegs = new List<Vector4>();
        private List<float> riverSegHw = new List<float>();

        private static long GKey(int gx, int gz) { return ((long)gx << 32) ^ (uint)gz; }
        private static long GKey(Vector2 p) { return GKey(Mathf.FloorToInt(p.x / 50f), Mathf.FloorToInt(p.y / 50f)); }
        private static float Cross2(Vector2 a, Vector2 b) { return a.x * b.y - a.y * b.x; }

        private void LoadRiverAssets()
        {
            solShader = AssetDatabase.LoadAssetAtPath<Shader>(RiverShaders + "SolTrou.shader");
            maskShader = AssetDatabase.LoadAssetAtPath<Shader>(RiverShaders + "RiviereMasque.shader");
            waterShader = AssetDatabase.LoadAssetAtPath<Shader>(RiverShaders + "Eau.shader");
            trenchMode = solShader != null && maskShader != null && waterShader != null;
        }

        /// <summary>Intersection de deux segments (bouts compris).</summary>
        private static bool SegHit(Vector2 a, Vector2 b, Vector2 p, Vector2 q, out Vector2 x)
        {
            x = Vector2.zero;
            Vector2 r = b - a, s = q - p;
            float den = Cross2(r, s);
            if (Mathf.Abs(den) < 1e-6f) return false;
            float t = Cross2(p - a, s) / den, u = Cross2(p - a, r) / den;
            if (t < -1e-4f || t > 1.0001f || u < -1e-4f || u > 1.0001f) return false;
            x = a + r * t;
            return true;
        }

        private static void AddEnd(Dictionary<long, List<int>> ends, long key, int i)
        {
            List<int> l;
            if (!ends.TryGetValue(key, out l)) { l = new List<int>(); ends[key] = l; }
            l.Add(i);
        }

        /// <summary>Tracés des cours d'eau, largeur des berges (réduite près des rues qui longent l'eau : les rues ne sont
        /// jamais déplacées), croisements avec les rues (ponts).</summary>
        private void PlanChannels(OsmData d, List<ImportedRoad> roadAxes)
        {
            channels.Clear(); channelGrid.Clear(); crossings.Clear(); crossingGrid.Clear(); allRoads.Clear(); allRoadsHw.Clear();
            riverRemoved = 0; riverShallow = 0; riverBig = 0; riverKm = 0f; riverWallKm = 0f;
            if (!trenchMode) return;

            // 1) Tracés OpenStreetMap (portions dans la zone de la carte)
            foreach (OsmWay w in d.Ways)
            {
                string wv;
                if (!w.Tags.TryGetValue("waterway", out wv)) continue;
                float ww, bank;
                bool concrete;
                switch (wv)
                {
                    case "river": ww = 14f; bank = 4.2f; concrete = false; break;
                    case "canal": ww = 7f; bank = 1.0f; concrete = true; break;
                    case "stream": ww = 4.5f; bank = 2.0f; concrete = false; break;
                    case "drain": ww = 2.6f; bank = 0.55f; concrete = true; break;
                    case "ditch": ww = 1.6f; bank = 0.8f; concrete = false; break;
                    default: continue;
                }
                string tv;
                if (w.Tags.TryGetValue("tunnel", out tv) && tv != "no") continue; // busé, sous la chaussée
                float tw;
                if (w.Tags.TryGetValue("width", out tv) && float.TryParse(tv, NumberStyles.Float, CultureInfo.InvariantCulture, out tw) && tw > 0.5f && tw < 400f) ww = tw;
                if (wv == "river") bank = Mathf.Clamp(ww * 0.3f, 3f, 7f);
                bool complete;
                List<Vector2> wp = Points(d, w, out complete);
                Channel cur = null;
                for (int i = 0; i < wp.Count - 1; i++)
                {
                    Vector2 a = wp[i], b = wp[i + 1];
                    if ((!InRange(a) && !InRange(b)) || (b - a).sqrMagnitude < 0.01f) { cur = null; continue; }
                    if (cur == null)
                    {
                        cur = new Channel { Name = Name(w.Tags, ""), Kind = wv, W = ww, Bank = bank, Concrete = concrete };
                        channels.Add(cur);
                        cur.P.Add(a);
                    }
                    cur.P.Add(b);
                }
            }
            channels.RemoveAll(c => c.P.Count < 2);

            // 2) Tronçons bout à bout de même nature : un seul tracé (berges continues aux raccords)
            bool merged = true;
            for (int guard = 0; merged && guard < 30; guard++)
            {
                merged = false;
                Dictionary<long, List<int>> ends = new Dictionary<long, List<int>>();
                for (int i = 0; i < channels.Count; i++)
                {
                    AddEnd(ends, PointKey(channels[i].P[0]), i);
                    AddEnd(ends, PointKey(channels[i].P[channels[i].P.Count - 1]), i);
                }
                foreach (KeyValuePair<long, List<int>> kv in ends)
                {
                    if (kv.Value.Count != 2 || kv.Value[0] == kv.Value[1]) continue;
                    Channel a = channels[kv.Value[0]], b = channels[kv.Value[1]];
                    if (a.P.Count < 2 || b.P.Count < 2 || a.Kind != b.Kind || Mathf.Abs(a.W - b.W) > 0.01f) continue;
                    if (PointKey(a.P[0]) == kv.Key) a.P.Reverse();
                    if (PointKey(b.P[b.P.Count - 1]) == kv.Key) b.P.Reverse();
                    if (PointKey(a.P[a.P.Count - 1]) != kv.Key || PointKey(b.P[0]) != kv.Key) continue;
                    for (int k = 1; k < b.P.Count; k++) a.P.Add(b.P[k]);
                    b.P.Clear();
                    if (string.IsNullOrEmpty(a.Name)) a.Name = b.Name;
                    merged = true;
                }
                channels.RemoveAll(c => c.P.Count < 2);
            }

            // 3) Points tous les 8 m au plus, abscisses, normales, grille de recherche (cases de 50 m)
            for (int ci = 0; ci < channels.Count; ci++)
            {
                Channel c = channels[ci];
                List<Vector2> dense = new List<Vector2> { c.P[0] };
                for (int i = 0; i < c.P.Count - 1; i++)
                {
                    int n = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(c.P[i], c.P[i + 1]) / 8f));
                    for (int k = 1; k <= n; k++) dense.Add(Vector2.Lerp(c.P[i], c.P[i + 1], k / (float)n));
                }
                c.P = dense;
                int m = c.P.Count;
                c.S = new float[m]; c.Hc = new float[m]; c.N = new Vector2[m]; c.T = new Vector2[m];
                for (int k = 1; k < m; k++) c.S[k] = c.S[k - 1] + Vector2.Distance(c.P[k - 1], c.P[k]);
                riverKm += c.S[m - 1] / 1000f;
                for (int k = 0; k < m; k++)
                {
                    c.Hc[k] = c.W * 0.5f + c.Bank;
                    Vector2 dPrev = k > 0 ? c.P[k] - c.P[k - 1] : c.P[1] - c.P[0];
                    Vector2 dNext = k < m - 1 ? c.P[k + 1] - c.P[k] : dPrev;
                    dPrev.Normalize(); dNext.Normalize();
                    Vector2 t = dPrev + dNext;
                    if (t.sqrMagnitude < 1e-4f) t = dNext;
                    t.Normalize();
                    c.T[k] = t;
                    Vector2 nrm = new Vector2(-t.y, t.x);
                    float cosHalf = Mathf.Abs(Vector2.Dot(nrm, new Vector2(-dNext.y, dNext.x)));
                    c.N[k] = nrm / Mathf.Max(0.6f, cosHalf);
                }
                float reach = c.W * 0.5f + c.Bank + 2.5f;
                for (int i = 0; i < m - 1; i++)
                {
                    Vector2 a = c.P[i], b = c.P[i + 1];
                    int x0 = Mathf.FloorToInt((Mathf.Min(a.x, b.x) - reach) / 50f), x1 = Mathf.FloorToInt((Mathf.Max(a.x, b.x) + reach) / 50f);
                    int z0 = Mathf.FloorToInt((Mathf.Min(a.y, b.y) - reach) / 50f), z1 = Mathf.FloorToInt((Mathf.Max(a.y, b.y) + reach) / 50f);
                    for (int gz = z0; gz <= z1; gz++)
                        for (int gx = x0; gx <= x1; gx++)
                        {
                            List<long> l;
                            if (!channelGrid.TryGetValue(GKey(gx, gz), out l)) { l = new List<long>(); channelGrid[GKey(gx, gz)] = l; }
                            l.Add(((long)ci << 32) | (uint)i);
                        }
                }
            }

            // 4) Toutes les rues dessinées (pistes et voies de service comprises)
            Dictionary<long, List<long>> roadIds = new Dictionary<long, List<long>>();
            for (int r = 0; r < roadAxes.Count; r++)
            {
                ImportedRoad ax = roadAxes[r];
                for (int i = 0; i < ax.pts.Count - 1; i++)
                {
                    Vector2 a = ax.pts[i], b = ax.pts[i + 1];
                    int x0 = Mathf.FloorToInt(Mathf.Min(a.x, b.x) / 50f), x1 = Mathf.FloorToInt(Mathf.Max(a.x, b.x) / 50f);
                    int z0 = Mathf.FloorToInt(Mathf.Min(a.y, b.y) / 50f), z1 = Mathf.FloorToInt(Mathf.Max(a.y, b.y) / 50f);
                    if ((x1 - x0 + 1) * (z1 - z0 + 1) > 400) continue;
                    for (int gz = z0; gz <= z1; gz++)
                        for (int gx = x0; gx <= x1; gx++)
                        {
                            long key = GKey(gx, gz);
                            List<long> l;
                            if (!roadIds.TryGetValue(key, out l)) { l = new List<long>(); roadIds[key] = l; allRoads[key] = new List<Vector4>(); allRoadsHw[key] = new List<float>(); }
                            l.Add(((long)r << 32) | (uint)i);
                            allRoads[key].Add(new Vector4(a.x, a.y, b.x, b.y));
                            allRoadsHw[key].Add(ax.width * 0.5f);
                        }
                }
            }

            // 5) Croisements rue × cours d'eau : un pont à chacun
            Dictionary<long, List<Vector2>> crossAt = new Dictionary<long, List<Vector2>>();
            for (int r = 0; r < roadAxes.Count; r++)
            {
                ImportedRoad ax = roadAxes[r];
                for (int i = 0; i < ax.pts.Count - 1; i++)
                {
                    Vector2 a = ax.pts[i], b = ax.pts[i + 1];
                    float len = Vector2.Distance(a, b);
                    if (len < 0.2f) continue;
                    Vector2 u = (b - a) / len;
                    int x0 = Mathf.FloorToInt(Mathf.Min(a.x, b.x) / 50f), x1 = Mathf.FloorToInt(Mathf.Max(a.x, b.x) / 50f);
                    int z0 = Mathf.FloorToInt(Mathf.Min(a.y, b.y) / 50f), z1 = Mathf.FloorToInt(Mathf.Max(a.y, b.y) / 50f);
                    if ((x1 - x0 + 1) * (z1 - z0 + 1) > 400) continue;
                    for (int gz = z0; gz <= z1; gz++)
                        for (int gx = x0; gx <= x1; gx++)
                        {
                            List<long> l;
                            if (!channelGrid.TryGetValue(GKey(gx, gz), out l)) continue;
                            foreach (long e in l)
                            {
                                int ci = (int)(e >> 32), si = (int)(e & 0xffffffffL);
                                Channel c = channels[ci];
                                Vector2 x;
                                if (!SegHit(a, b, c.P[si], c.P[si + 1], out x)) continue;
                                long pair = ((long)ci << 32) | (uint)r;
                                List<Vector2> xs;
                                if (!crossAt.TryGetValue(pair, out xs)) { xs = new List<Vector2>(); crossAt[pair] = xs; }
                                xs.Add(x);
                                // Même rue déjà pontée tout près (trouvée dans une autre case, ou confluent sous le pont)
                                Crossing same = null;
                                foreach (Crossing o in crossings)
                                    if (o.Road == r && (o.X - x).sqrMagnitude < 9f) { same = o; break; }
                                if (same != null)
                                {
                                    if (c.W > channels[same.Ci].W) { same.Ci = ci; same.Si = si; same.X = x; same.Big = c.W >= 3f; }
                                    continue;
                                }
                                Vector2 v = (c.P[si + 1] - c.P[si]).normalized;
                                if (Mathf.Abs(Cross2(u, v)) < 0.3f) { riverShallow++; continue; } // rue presque parallèle à l'eau
                                crossings.Add(new Crossing { Road = r, Ci = ci, Si = si, X = x, U = u, N = new Vector2(-u.y, u.x), Hw = ax.width * 0.5f, YRoad = 0.03f + ax.rank * 0.004f, Big = c.W >= 3f });
                            }
                        }
                }
            }

            // 6) Berges plus étroites là où une rue longe l'eau (on garde la place de la rue et de son trottoir)
            for (int ci = 0; ci < channels.Count; ci++)
            {
                Channel c = channels[ci];
                float hn = c.W * 0.5f + c.Bank, hmin = c.W * 0.5f + 0.35f;
                int m = c.P.Count;
                for (int k = 0; k < m; k++)
                {
                    Vector2 p = c.P[k];
                    float best = hn;
                    int gx = Mathf.FloorToInt(p.x / 50f), gz = Mathf.FloorToInt(p.y / 50f);
                    for (int dz = -1; dz <= 1; dz++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            List<long> l;
                            if (!roadIds.TryGetValue(GKey(gx + dx, gz + dz), out l)) continue;
                            foreach (long e in l)
                            {
                                int r = (int)(e >> 32), i = (int)(e & 0xffffffffL);
                                ImportedRoad ax = roadAxes[r];
                                List<Vector2> xs;
                                if (crossAt.TryGetValue(((long)ci << 32) | (uint)r, out xs))
                                {
                                    // Cette rue croise l'eau tout près : c'est un pont, pas une rue qui longe la berge
                                    bool nearX = false;
                                    foreach (Vector2 xx in xs) if ((xx - p).sqrMagnitude < (hn + 25f) * (hn + 25f)) { nearX = true; break; }
                                    if (nearX) continue;
                                }
                                float margin = ax.rank >= 2 ? 2.0f : 0.6f;
                                float lim = SegDist(p, ax.pts[i], ax.pts[i + 1]) - ax.width * 0.5f - margin;
                                if (lim < best) best = lim;
                            }
                        }
                    c.Hc[k] = Mathf.Max(best, hmin);
                }
                for (int k = 1; k < m; k++) c.Hc[k] = Mathf.Min(c.Hc[k], c.Hc[k - 1] + 0.6f * (c.S[k] - c.S[k - 1]));
                for (int k = m - 2; k >= 0; k--) c.Hc[k] = Mathf.Min(c.Hc[k], c.Hc[k + 1] + 0.6f * (c.S[k + 1] - c.S[k]));
            }

            // 7) Étendue de chaque pont : la chaussée et ses trottoirs passent au-dessus de l'emprise (+ 1 m d'appui)
            for (int xi = 0; xi < crossings.Count; xi++)
            {
                Crossing x = crossings[xi];
                Channel c = channels[x.Ci];
                Vector2 p = c.P[x.Si], q = c.P[x.Si + 1];
                Vector2 v = (q - p).normalized, mn = new Vector2(-v.y, v.x);
                float f = Mathf.Clamp01(Vector2.Dot(x.X - p, q - p) / Mathf.Max((q - p).sqrMagnitude, 1e-6f));
                float hc = Mathf.Lerp(c.Hc[x.Si], c.Hc[x.Si + 1], f) + 1f;
                float um = Vector2.Dot(x.U, mn), nm = Vector2.Dot(x.N, mn);
                float sOut = x.Hw + BridgeWalk + BridgeParapet;
                Extent(hc, um, nm, x.Hw, sOut, out x.T0a, out x.T1a);
                Extent(hc, um, nm, -sOut, -x.Hw, out x.T0b, out x.T1b);
                if (x.Big) riverBig++;
                float reach = Mathf.Max(Mathf.Max(Mathf.Abs(x.T0a), Mathf.Abs(x.T1a)), Mathf.Max(Mathf.Abs(x.T0b), Mathf.Abs(x.T1b))) + sOut + 2f;
                int x0 = Mathf.FloorToInt((x.X.x - reach) / 50f), x1 = Mathf.FloorToInt((x.X.x + reach) / 50f);
                int z0 = Mathf.FloorToInt((x.X.y - reach) / 50f), z1 = Mathf.FloorToInt((x.X.y + reach) / 50f);
                for (int gz = z0; gz <= z1; gz++)
                    for (int gx = x0; gx <= x1; gx++)
                    {
                        List<int> l;
                        if (!crossingGrid.TryGetValue(GKey(gx, gz), out l)) { l = new List<int>(); crossingGrid[GKey(gx, gz)] = l; }
                        l.Add(xi);
                    }
            }
        }

        /// <summary>Étendue (le long de la rue) où une bande latérale [s0, s1] passe au-dessus de l'emprise |t·um + s·nm| ≤ hc.</summary>
        private static void Extent(float hc, float um, float nm, float s0, float s1, out float t0, out float t1)
        {
            float a = (-hc - s0 * nm) / um, b = (hc - s0 * nm) / um, c = (-hc - s1 * nm) / um, d = (hc - s1 * nm) / um;
            t0 = Mathf.Min(Mathf.Min(a, b), Mathf.Min(c, d));
            t1 = Mathf.Max(Mathf.Max(a, b), Mathf.Max(c, d));
        }

        /// <summary>Vrai si le point est dans l'emprise d'un cours d'eau (eau + berges), élargie de « margin » (≤ 2 m).</summary>
        private bool InCorridor(Vector2 p, float margin)
        {
            List<long> l;
            if (channels.Count == 0 || !channelGrid.TryGetValue(GKey(p), out l)) return false;
            foreach (long e in l)
            {
                Channel c = channels[(int)(e >> 32)];
                int si = (int)(e & 0xffffffffL);
                if (SegDist(p, c.P[si], c.P[si + 1]) < Mathf.Max(c.Hc[si], c.Hc[si + 1]) + margin) return true;
            }
            return false;
        }

        /// <summary>Distance au bord d'un autre cours d'eau que « self » (négative dedans) : bord de l'eau ou sommet des berges.</summary>
        private float OtherChannel(Vector2 p, int self, bool water)
        {
            float best = 999f;
            List<long> l;
            if (!channelGrid.TryGetValue(GKey(p), out l)) return best;
            foreach (long e in l)
            {
                int ci = (int)(e >> 32), si = (int)(e & 0xffffffffL);
                if (ci == self) continue;
                Channel c = channels[ci];
                float edge = water ? c.W * 0.5f + 0.3f : Mathf.Max(c.Hc[si], c.Hc[si + 1]);
                float dd = SegDist(p, c.P[si], c.P[si + 1]) - edge;
                if (dd < best) best = dd;
            }
            return best;
        }

        /// <summary>Vrai si le point est sur le tablier d'un pont construit sur un cours d'eau (chaussée ou trottoirs).</summary>
        private bool InDeck(Vector2 p, float margin)
        {
            List<int> l;
            if (!crossingGrid.TryGetValue(GKey(p), out l)) return false;
            foreach (int xi in l)
            {
                Crossing x = crossings[xi];
                Vector2 dd = p - x.X;
                float t = Vector2.Dot(dd, x.U), s = Vector2.Dot(dd, x.N);
                if (Mathf.Abs(s) > x.Hw + BridgeWalk + BridgeParapet + margin) continue;
                float t0 = s >= 0f ? x.T0a : x.T0b, t1 = s >= 0f ? x.T1a : x.T1b;
                if (t >= t0 - margin && t <= t1 + margin) return true;
            }
            return false;
        }

        /// <summary>Vrai si un cours d'eau passe à moins de 12 m du segment (bords de rue découpés plus fin près de l'eau).</summary>
        private bool NearWater(Vector2 a, Vector2 b)
        {
            if (channels.Count == 0) return false;
            int x0 = Mathf.FloorToInt((Mathf.Min(a.x, b.x) - 12f) / 50f), x1 = Mathf.FloorToInt((Mathf.Max(a.x, b.x) + 12f) / 50f);
            int z0 = Mathf.FloorToInt((Mathf.Min(a.y, b.y) - 12f) / 50f), z1 = Mathf.FloorToInt((Mathf.Max(a.y, b.y) + 12f) / 50f);
            if ((x1 - x0 + 1) * (z1 - z0 + 1) > 400) return true;
            for (int gz = z0; gz <= z1; gz++)
                for (int gx = x0; gx <= x1; gx++)
                    if (channelGrid.ContainsKey(GKey(gx, gz))) return true;
            return false;
        }

        /// <summary>Vrai si le bâtiment est sur un cours d'eau ou sur ses berges (demande du créateur : aucune maison ni
        /// clôture au bord de la rivière ; les maisons restent le long du tracé, en retrait).</summary>
        private bool RiverHit(List<Vector2> pts, Vector2 center)
        {
            if (channels.Count == 0) return false;
            Vector2 mn = pts[0], mx = pts[0];
            foreach (Vector2 q in pts) { mn = Vector2.Min(mn, q); mx = Vector2.Max(mx, q); }
            int x0 = Mathf.FloorToInt((mn.x - 2f) / 50f), x1 = Mathf.FloorToInt((mx.x + 2f) / 50f);
            int z0 = Mathf.FloorToInt((mn.y - 2f) / 50f), z1 = Mathf.FloorToInt((mx.y + 2f) / 50f);
            if ((x1 - x0 + 1) * (z1 - z0 + 1) > 100) return false;
            for (int gz = z0; gz <= z1; gz++)
                for (int gx = x0; gx <= x1; gx++)
                {
                    List<long> l;
                    if (!channelGrid.TryGetValue(GKey(gx, gz), out l)) continue;
                    foreach (long e in l)
                    {
                        Channel c = channels[(int)(e >> 32)];
                        int si = (int)(e & 0xffffffffL);
                        Vector2 a = c.P[si], b = c.P[si + 1];
                        float lim = Mathf.Max(c.Hc[si], c.Hc[si + 1]) + 1.5f;
                        if (SegDist(center, a, b) < lim) return true;
                        foreach (Vector2 q in pts) if (SegDist(q, a, b) < lim) return true;
                        for (int i = 0; i < pts.Count; i++)
                            if (SegCross(a, b, pts[i], pts[(i + 1) % pts.Count])) return true;
                    }
                }
            return false;
        }

        /// <summary>Un côté de pont le long de la chaussée, de p0 à p1 (points de l'axe) : bordure, trottoir, parapet et
        /// garde-corps (ou simple bordure sur un caniveau), face extérieure du tablier.</summary>
        private void BridgeSide(Dictionary<string, MeshBatch> edges, Vector2 p0, Vector2 p1, Vector2 o, float hw, float yRoad, bool big, bool parapet, bool post, float u0, float u1)
        {
            Vector2Int c = Cell((p0 + p1) * 0.5f);
            MeshBatch deck = Batch(edges, c.x + "_" + c.y + "_pont"), under = Batch(edges, c.x + "_" + c.y + "_pontb");
            Vector3 out3 = new Vector3(o.x, 0f, o.y);
            float yTop = yRoad + 0.15f;
            Side(deck, p0, p1, o, hw, yRoad - 0.01f, yTop, -out3, u0, u1);
            Top(deck, p0, p1, o, hw, hw + BridgeWalk, yTop, u0, u1);
            Side(under, p0, p1, o, hw + BridgeWalk + BridgeParapet, BridgeUnder, yTop, out3, u0, u1);
            if (!parapet) return;
            Vector2 q0 = p0 + o * (hw + BridgeWalk + BridgeParapet * 0.5f), q1 = p1 + o * (hw + BridgeWalk + BridgeParapet * 0.5f);
            if (big)
            {
                Bar(deck, q0, q1, o, BridgeParapet, 0f, 0.85f);
                Bar(deck, q0, q1, o, 0.08f, 1.02f, 1.1f);
                if (post) Post(deck, q0, 0.05f, 0.85f, 1.05f);
            }
            else Bar(deck, q0, q1, o, BridgeParapet, 0f, 0.5f);
        }

        /// <summary>Ponts sur les cours d'eau : obligatoires partout où une rue croise l'eau.</summary>
        private void BuildRiverBridges(Dictionary<string, MeshBatch> edges, Dictionary<long, List<Vector4>> carriage, Dictionary<long, List<float>> carriageHw)
        {
            Vector2 uv = Vector2.zero;
            foreach (Crossing x in crossings)
            {
                float sOut = x.Hw + BridgeWalk + BridgeParapet, yTop = x.YRoad + 0.15f;
                Vector3 along = new Vector3(x.U.x, 0f, x.U.y);
                foreach (float side in new[] { 1f, -1f })
                {
                    float t0 = side > 0f ? x.T0a : x.T0b, t1 = side > 0f ? x.T1a : x.T1b;
                    Vector2 o = x.N * side, a = x.X + x.U * t0, b = x.X + x.U * t1;
                    int steps = Mathf.Max(1, Mathf.CeilToInt((t1 - t0) / 2f));
                    for (int k = 0; k < steps; k++)
                    {
                        Vector2 p0 = Vector2.Lerp(a, b, k / (float)steps), p1 = Vector2.Lerp(a, b, (k + 1) / (float)steps);
                        Vector2 mid = (p0 + p1) * 0.5f + o * (x.Hw + BridgeWalk + BridgeParapet * 0.5f);
                        bool parapet = RoadClearance(carriage, carriageHw, mid) >= 0.35f;
                        BridgeSide(edges, p0, p1, o, x.Hw, x.YRoad, x.Big, parapet, k % 2 == 0, (t0 + (t1 - t0) * k / steps) * 0.5f, (t0 + (t1 - t0) * (k + 1) / steps) * 0.5f);
                    }
                    // Bouts du trottoir
                    Vector2Int cc = Cell(a);
                    MeshBatch deck = Batch(edges, cc.x + "_" + cc.y + "_pont");
                    deck.QuadFacing(V3(a + o * x.Hw, 0f), V3(a + o * x.Hw, yTop), V3(a + o * sOut, yTop), V3(a + o * sOut, 0f), uv, uv, uv, uv, -along);
                    deck.QuadFacing(V3(b + o * x.Hw, 0f), V3(b + o * x.Hw, yTop), V3(b + o * sOut, yTop), V3(b + o * sOut, 0f), uv, uv, uv, uv, along);
                }
                // Dessous du tablier
                Vector2 a0 = x.X + x.U * x.T0a + x.N * sOut, a1 = x.X + x.U * x.T1a + x.N * sOut;
                Vector2 b0 = x.X + x.U * x.T0b - x.N * sOut, b1 = x.X + x.U * x.T1b - x.N * sOut;
                Vector2Int xc = Cell(x.X);
                Batch(edges, xc.x + "_" + xc.y + "_pontb").QuadFacing(V3(a0, BridgeUnder), V3(a1, BridgeUnder), V3(b1, BridgeUnder), V3(b0, BridgeUnder), uv, uv, uv, uv, Vector3.down);
            }
        }

        private void RiverProfile(Channel c, float h, out float rMid, out float yMid, out float rBot, out float yBot)
        {
            float half = c.W * 0.5f;
            rBot = half - 0.3f;
            yBot = WaterY - 0.12f;
            if (c.Concrete) { rMid = (h + rBot) * 0.5f; yMid = (GroundY + yBot) * 0.5f; }
            else { rMid = h - 0.55f * (h - half); yMid = GroundY - 0.45f * (GroundY - WaterY); }
        }

        /// <summary>Tranchées (masque, eau, berges, murs de fond), bandes d'herbe, barrières invisibles ; emprises pour le jeu.</summary>
        private void EmitRivers(GameObject root, Dictionary<string, MeshBatch> areas, Dictionary<string, Material> mats, Shader shader, string matAsset, string meshAsset, ref bool firstMesh, ref bool firstMat, ref int idx)
        {
            riverSegs = new List<Vector4>();
            riverSegHw = new List<float>();
            if (!trenchMode || channels.Count == 0) return;
            Dictionary<string, RiverChunk> chunks = new Dictionary<string, RiverChunk>();
            Dictionary<string, MeshBatch> walls = new Dictionary<string, MeshBatch>();
            for (int ci = 0; ci < channels.Count; ci++)
            {
                Channel c = channels[ci];
                int n = c.P.Count;
                bool capStart = OtherChannel(c.P[0], ci, false) > -0.5f, capEnd = OtherChannel(c.P[n - 1], ci, false) > -0.5f;
                float half = c.W * 0.5f, we = half + 0.3f, uScale = c.Concrete ? 0.5f : 0.25f;
                for (int k = 0; k < n - 1; k++)
                {
                    Vector2 pa = c.P[k], pb = c.P[k + 1];
                    float ha = c.Hc[k], hb = c.Hc[k + 1];
                    riverSegs.Add(new Vector4(pa.x, pa.y, pb.x, pb.y));
                    riverSegHw.Add(Mathf.Max(ha, hb));
                    Vector2Int cell = Cell((pa + pb) * 0.5f);
                    string key = cell.x + "_" + cell.y;
                    RiverChunk ch;
                    if (!chunks.TryGetValue(key, out ch)) { ch = new RiverChunk(); chunks[key] = ch; }
                    // Masque : un peu en retrait des bords et des murs de fond (le sol recouvre le raccord)
                    Vector2 ma = pa, mb = pb;
                    if (k == 0 && capStart) ma += c.T[0] * 0.12f;
                    if (k == n - 2 && capEnd) mb -= c.T[n - 1] * 0.12f;
                    ch.Mask.Quad(V3(ma + c.N[k] * (ha - 0.12f), GroundY), V3(mb + c.N[k + 1] * (hb - 0.12f), GroundY), V3(mb - c.N[k + 1] * (hb - 0.12f), GroundY), V3(ma - c.N[k] * (ha - 0.12f), GroundY));
                    // Eau (coordonnées de texture = position dans le monde : les confluents se raccordent sans couture)
                    Vector2 wa0 = pa + c.N[k] * we, wa1 = pa - c.N[k] * we, wb0 = pb + c.N[k + 1] * we, wb1 = pb - c.N[k + 1] * we;
                    ch.Water.QuadFacing(V3(wa0, WaterY), V3(wb0, WaterY), V3(wb1, WaterY), V3(wa1, WaterY), wa0 / 6f, wb0 / 6f, wb1 / 6f, wa1 / 6f, Vector3.up);
                    MeshBatch bank = c.Concrete ? ch.Concrete : ch.Bank;
                    float ua = c.S[k] * uScale, ub = c.S[k + 1] * uScale;
                    float rMa, yMa, rBa, yBa, rMb, yMb, rBb, yBb;
                    RiverProfile(c, ha, out rMa, out yMa, out rBa, out yBa);
                    RiverProfile(c, hb, out rMb, out yMb, out rBb, out yBb);
                    foreach (float side in new[] { 1f, -1f })
                    {
                        Vector2 na = c.N[k] * side, nb = c.N[k + 1] * side;
                        Vector3 topA = V3(pa + na * ha, GroundY), topB = V3(pb + nb * hb, GroundY);
                        Vector3 midA = V3(pa + na * rMa, yMa), midB = V3(pb + nb * rMb, yMb);
                        Vector3 botA = V3(pa + na * rBa, yBa), botB = V3(pb + nb * rBb, yBb);
                        // Berge : pas à l'intérieur de l'eau d'un autre cours d'eau (confluent ouvert)
                        Vector2 cTop = ((pa + na * ha) + (pb + nb * hb) + (pa + na * rMa) + (pb + nb * rMb)) * 0.25f;
                        Vector2 cBot = ((pa + na * rMa) + (pb + nb * rMb) + (pa + na * rBa) + (pb + nb * rBb)) * 0.25f;
                        if (OtherChannel(cTop, ci, true) >= 0f)
                            bank.QuadFacing(topA, topB, midB, midA, new Vector2(ua, 1f), new Vector2(ub, 1f), new Vector2(ub, 0.5f), new Vector2(ua, 0.5f), Vector3.up);
                        if (OtherChannel(cBot, ci, true) >= 0f)
                            bank.QuadFacing(midA, midB, botB, botA, new Vector2(ua, 0.5f), new Vector2(ub, 0.5f), new Vector2(ub, 0f), new Vector2(ua, 0f), Vector3.up);
                        Vector2 e0a = pa + na * (ha - 0.15f), e0b = pb + nb * (hb - 0.15f);
                        if (c.Concrete)
                        {
                            // Rebord de béton au sommet de la paroi
                            Vector2 e1a = pa + na * (ha + 0.3f), e1b = pb + nb * (hb + 0.3f);
                            if (OtherChannel((e0a + e1b) * 0.5f, ci, false) >= -0.2f)
                                ch.Concrete.QuadFacing(V3(e0a, 0.02f), V3(e0b, 0.02f), V3(e1b, 0.02f), V3(e1a, 0.02f), new Vector2(ua, 1f), new Vector2(ub, 1f), new Vector2(ub, 0.9f), new Vector2(ua, 0.9f), Vector3.up);
                        }
                        else
                        {
                            // Bande d'herbe le long du sommet de la berge (comme sur la photo)
                            Vector2 e1a = pa + na * (ha + 1.4f), e1b = pb + nb * (hb + 1.4f);
                            Batch(areas, key + "_vert").QuadFacing(V3(e0a, 0.012f), V3(e0b, 0.012f), V3(e1b, 0.012f), V3(e1a, 0.012f), e0a * 0.1f, e0b * 0.1f, e1b * 0.1f, e1a * 0.1f, Vector3.up);
                        }
                        // Barrière invisible le long de la berge, sauf sur les chaussées, les ponts et dans un autre cours d'eau
                        Vector2 wa = pa + na * (ha + 0.1f), wb = pb + nb * (hb + 0.1f);
                        float wl = Vector2.Distance(wa, wb);
                        int pieces = Mathf.Max(1, Mathf.CeilToInt(wl / 2f)), run = -1;
                        for (int j = 0; j <= pieces; j++)
                        {
                            bool keep = false;
                            if (j < pieces)
                            {
                                Vector2 q = Vector2.Lerp(wa, wb, (j + 0.5f) / pieces);
                                keep = RoadClearance(allRoads, allRoadsHw, q) >= 0.3f && !InDeck(q, 0.05f) && OtherChannel(q, ci, false) >= -1f;
                            }
                            if (keep && run < 0) run = j;
                            if (!keep && run >= 0)
                            {
                                Vector2 r0 = Vector2.Lerp(wa, wb, run / (float)pieces), r1 = Vector2.Lerp(wa, wb, j / (float)pieces);
                                Vector2 dir = (r1 - r0).normalized;
                                MeshBatch wm;
                                if (!walls.TryGetValue(key, out wm)) { wm = new MeshBatch(); walls[key] = wm; }
                                Bar(wm, r0, r1, new Vector2(-dir.y, dir.x), 0.5f, -0.3f, 2.2f);
                                riverWallKm += Vector2.Distance(r0, r1) / 1000f;
                                run = -1;
                            }
                        }
                    }
                    // Murs de fond aux extrémités (sauf dans un autre cours d'eau : confluent)
                    if (k == 0 && capStart) RiverCap(ch.Concrete, c, 0, new Vector3(c.T[0].x, 0f, c.T[0].y));
                    if (k == n - 2 && capEnd) RiverCap(ch.Concrete, c, n - 1, new Vector3(-c.T[n - 1].x, 0f, -c.T[n - 1].y));
                }
            }

            Material maskMat = new Material(maskShader) { name = "RiviereMasque" };
            maskMat.enableInstancing = false;
            AssetDatabase.CreateAsset(maskMat, matAsset + "/RiviereMasque.mat");
            Material waterMat = new Material(waterShader) { name = "EauRiviere" };
            waterMat.enableInstancing = false;
            Texture2D ripples = AssetDatabase.LoadAssetAtPath<Texture2D>(RiverTextures + "eau_ondes.png");
            if (ripples != null) waterMat.SetTexture("_Ripple", ripples);
            AssetDatabase.CreateAsset(waterMat, matAsset + "/EauRiviere.mat");
            Material bankMat = Mat(mats, "BergeHerbe", shader, Color.white, matAsset, ref firstMat);
            Texture2D bankTex = AssetDatabase.LoadAssetAtPath<Texture2D>(RiverTextures + "berge.png");
            if (bankTex != null) { bankMat.mainTexture = bankTex; EditorUtility.SetDirty(bankMat); }
            else { bankMat.color = new Color(0.33f, 0.42f, 0.2f); EditorUtility.SetDirty(bankMat); }
            Material concreteMat = Mat(mats, "BetonCanal", shader, Color.white, matAsset, ref firstMat);
            Texture2D concreteTex = AssetDatabase.LoadAssetAtPath<Texture2D>(RiverTextures + "beton_canal.png");
            if (concreteTex != null) { concreteMat.mainTexture = concreteTex; EditorUtility.SetDirty(concreteMat); }
            else { concreteMat.color = new Color(0.55f, 0.55f, 0.52f); EditorUtility.SetDirty(concreteMat); }

            foreach (KeyValuePair<string, RiverChunk> kv in chunks)
            {
                // Un seul objet par bloc : le masque et la tranchée s'affichent et se cachent toujours ensemble
                MeshBatch[] parts = { kv.Value.Mask, kv.Value.Water, kv.Value.Bank, kv.Value.Concrete };
                Material[] pm = { maskMat, waterMat, bankMat, concreteMat };
                List<Vector3> v = new List<Vector3>();
                List<Vector2> uv = new List<Vector2>();
                List<List<int>> subs = new List<List<int>>();
                List<Material> used = new List<Material>();
                for (int i = 0; i < parts.Length; i++)
                {
                    if (parts[i].T.Count == 0) continue;
                    int off = v.Count;
                    v.AddRange(parts[i].V);
                    uv.AddRange(parts[i].UV);
                    List<int> t = new List<int>(parts[i].T.Count);
                    foreach (int ti in parts[i].T) t.Add(ti + off);
                    subs.Add(t);
                    used.Add(pm[i]);
                }
                if (subs.Count == 0) continue;
                Mesh mesh = new Mesh { name = "Riviere_" + kv.Key };
                if (v.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(v);
                mesh.SetUVs(0, uv);
                mesh.subMeshCount = subs.Count;
                for (int i = 0; i < subs.Count; i++) mesh.SetTriangles(subs[i], i);
                mesh.RecalculateNormals();
                mesh.RecalculateTangents();
                mesh.RecalculateBounds();
                if (firstMesh) { AssetDatabase.CreateAsset(mesh, meshAsset); firstMesh = false; }
                else AssetDatabase.AddObjectToAsset(mesh, meshAsset);
                GameObject g = new GameObject("Riviere_" + kv.Key);
                g.transform.SetParent(root.transform, false);
                g.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer mr = g.AddComponent<MeshRenderer>();
                mr.sharedMaterials = used.ToArray();
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                idx++;
            }
            if (walls.Count > 0)
            {
                GameObject wr = new GameObject("RiviereBarrieres");
                wr.transform.SetParent(root.transform, false);
                foreach (KeyValuePair<string, MeshBatch> kv in walls)
                {
                    Mesh mesh = kv.Value.Build("BarriereRiviere_" + kv.Key);
                    if (firstMesh) { AssetDatabase.CreateAsset(mesh, meshAsset); firstMesh = false; }
                    else AssetDatabase.AddObjectToAsset(mesh, meshAsset);
                    GameObject g = new GameObject("Barriere_" + kv.Key);
                    g.transform.SetParent(wr.transform, false);
                    g.layer = 2; // « Ignore Raycast » : la caméra ne s'y cogne pas
                    g.AddComponent<MeshCollider>().sharedMesh = mesh;
                }
            }
        }

        /// <summary>Mur de fond d'une tranchée : remplit la coupe (sommet, milieu, bas des deux berges) jusqu'au niveau du sol.</summary>
        private void RiverCap(MeshBatch m, Channel c, int k, Vector3 facing)
        {
            float h = c.Hc[k], rMid, yMid, rBot, yBot;
            RiverProfile(c, h, out rMid, out yMid, out rBot, out yBot);
            Vector2 p = c.P[k], n = c.N[k].normalized;
            float[] rs = { h, rMid, rBot, -rBot, -rMid, -h };
            float[] ys = { GroundY, yMid, yBot, yBot, yMid, GroundY };
            Vector3 center = V3(p, GroundY);
            Vector2 uc = new Vector2(0f, 1f);
            for (int i = 0; i < 5; i++)
            {
                Vector3 a = V3(p + n * rs[i], ys[i]), b = V3(p + n * rs[i + 1], ys[i + 1]);
                Vector2 ua = new Vector2(rs[i] * 0.5f, Mathf.InverseLerp(yBot, GroundY, ys[i])), ub = new Vector2(rs[i + 1] * 0.5f, Mathf.InverseLerp(yBot, GroundY, ys[i + 1]));
                m.QuadFacing(center, a, b, b, uc, ua, ub, ub, facing);
            }
        }

        private string RiverReport()
        {
            if (!trenchMode) return "Rivières : shaders de tranchée absents, anciens rubans plats conservés.\n";
            return "Rivières en tranchée : " + channels.Count + " tracés (" + riverKm.ToString("0.0") + " km), " + crossings.Count + " ponts construits aux croisements avec les rues (dont " + riverBig +
                   " sur rivière, ruisseau ou canal ; " + riverShallow + " croisements trop rasants laissés sans pont), " + riverRemoved + " bâtiments OSM retirés des berges, " +
                   riverWallKm.ToString("0.0") + " km de barrières invisibles.\n";
        }
    }
}
#endif
