// BALULA MOTO — ville pleine : parcelles clôturées (murs, portails, maisons à toit de tôle ou en béton, arbres),
// boutiques et petits immeubles au bord des avenues, panneaux publicitaires, générés le long de toutes les rues
// autour du joueur, et activation des morceaux de la carte importée selon la distance (téléphones).
// Tout est solide (collisions par boîtes). Les parcelles évitent les rues, les bâtiments réels d'OpenStreetMap,
// les arrêts, stations et lieux du créateur : ce sont des constructions génériques, jamais des bâtiments identifiés.
using System.Collections.Generic;
using UnityEngine;

namespace BalulaMoto.Runtime
{
    public partial class CityFill : MonoBehaviour
    {
        public static CityFill Instance;

        private struct Seg { public int Road; public int Index; }
        /// <summary>Segment de rue proche : Clear = espace à laisser libre devant les maisons (10 m sur les boulevards).</summary>
        private struct Sg { public Vector2 A, B; public float Hw, Clear; }
        private readonly Dictionary<long, List<Sg>> waterGrid = new Dictionary<long, List<Sg>>();
        private List<Sg> curWater;

        /// <summary>Vrai si le point est sur un cours d'eau ou ses berges (case en cours de construction).</summary>
        private bool InWater(Vector2 p, float margin)
        {
            if (curWater == null) return false;
            foreach (Sg s in curWater) if (PointSeg(p, s.A, s.B) < s.Hw + margin) return true;
            return false;
        }
        private struct Obb { public Vector2 C, X, Z; public float Hx, Hz; }
        private struct Keep { public Vector2 C; public float R; }

        private class Cell
        {
            public GameObject Go;
            public Mesh Mesh;
            public int Parcels;
        }

        private ImportedWorld map;
        // Plaques de rue aux carrefours (noms réels d'OpenStreetMap), rangées par case
        private struct SignSpot { public Vector2 P, DirA, DirB; public string A, B; public float HwA, HwB; }
        private readonly Dictionary<long, List<SignSpot>> signGrid = new Dictionary<long, List<SignSpot>>();
        // Lumières de la case en cours de construction (ampoules, fenêtres) et flaques de lumière au sol
        private LightMesh curLum, curHalo;
        public int SignCount { get; private set; }
        public int PoleCount { get; private set; }
        private const float CellSize = 100f;
        private readonly Dictionary<long, List<Seg>> roadGrid = new Dictionary<long, List<Seg>>();
        // Rues à moins de 70 m d'une case : celles dont les parcelles (jusqu'à 3 rangées) peuvent tomber dans la case
        private readonly Dictionary<long, List<Seg>> genGrid = new Dictionary<long, List<Seg>>();
        private const float GenReach = 70f;
        private readonly Dictionary<long, List<Vector3>> buildingGrid = new Dictionary<long, List<Vector3>>();
        private readonly Dictionary<long, Cell> cells = new Dictionary<long, Cell>();
        private readonly Dictionary<long, List<Obb>> placedByCell = new Dictionary<long, List<Obb>>();
        private readonly List<long> toUnload = new List<long>();
        private readonly List<Keep> keep = new List<Keep>();
        private bool keepReady, warm;
        private float loadRadius = 230f;
        private Material atlasMat, wallMat, wireMat;
        private readonly List<Material> adMats = new List<Material>();
        private Material postMat;
        private float scanTimer;
        private readonly List<KeyValuePair<Renderer, Bounds>> chunks = new List<KeyValuePair<Renderer, Bounds>>();
        private float chunkTimer;
        private Vector3 lastPos = new Vector3(1e6f, 0f, 1e6f);

        public int LoadedCells { get { return cells.Count; } }
        public int AdCount { get { return adMats.Count; } }

        // ------------------------------------------------------------------
        // Palette (atlas de couleurs unies : une seule texture et un seul matériau pour toutes les parcelles)
        // ------------------------------------------------------------------
        private static readonly Color[] Palette =
        {
            new Color(0.93f, 0.92f, 0.88f), new Color(0.93f, 0.86f, 0.68f), new Color(0.86f, 0.66f, 0.4f), new Color(0.63f, 0.77f, 0.87f),
            new Color(0.92f, 0.73f, 0.72f), new Color(0.73f, 0.84f, 0.66f), new Color(0.12f, 0.3f, 0.7f), new Color(0.1f, 0.45f, 0.25f),
            new Color(0.08f, 0.08f, 0.09f), new Color(0.65f, 0.1f, 0.1f), new Color(0.55f, 0.32f, 0.2f), new Color(0.6f, 0.62f, 0.64f),
            new Color(0.7f, 0.7f, 0.68f), new Color(0.07f, 0.09f, 0.12f), new Color(0.2f, 0.45f, 0.15f), new Color(0.35f, 0.25f, 0.15f),
            new Color(0.8f, 0.8f, 0.78f), new Color(0.62f, 0.5f, 0.38f), new Color(0.97f, 0.97f, 0.95f), new Color(0.88f, 0.5f, 0.12f),
            new Color(0.45f, 0.45f, 0.47f), new Color(0.27f, 0.55f, 0.2f), new Color(0.95f, 0.78f, 0.1f), new Color(0.82f, 0.2f, 0.12f),
            new Color(0.3f, 0.52f, 0.17f), new Color(0.36f, 0.58f, 0.2f), new Color(0.035f, 0.035f, 0.04f)
        };
        private const int WallFirst = 0, WallCount = 6, GateFirst = 6, GateCount = 4, RoofRust = 10, RoofGray = 11, Concrete = 12,
            Window = 13, Leaves = 14, Trunk = 15, Cap = 16, Shutter = 20, Leaves2 = 21, Lawn = 24, Lawn2 = 25, Iron = 26;
        private static readonly int[] SignColors = { 22, 23, 6, 7, 19, 9, 18 };
        private const int AtlasCols = 8;

        public static CityFill Create(Transform parent, ImportedWorld importedMap)
        {
            GameObject g = new GameObject("VillePleine");
            g.transform.SetParent(parent, false);
            Instance = g.AddComponent<CityFill>();
            Instance.Init(importedMap);
            return Instance;
        }

        /// <summary>Vrai si un point (rayon r) tombe dans une parcelle ou un panneau déjà construit.</summary>
        public static bool IsBlocked(Vector3 p, float r)
        {
            return Instance != null && Instance.Blocked(p, r);
        }

        private void OnDestroy()
        {
            foreach (KeyValuePair<long, Cell> kv in cells) if (kv.Value.Mesh != null) Destroy(kv.Value.Mesh);
            if (Instance == this) Instance = null;
        }

        private void Init(ImportedWorld importedMap)
        {
            map = importedMap;
            loadRadius = GameSettings.Quality == 0 ? 160f : (GameSettings.Quality == 1 ? 230f : 300f);
            atlasMat = BuildAtlasMaterial();
            // Murs de clôture bruts (textures dessinées par scripts/generer_textures_murs.py, d'après les photos de Kinshasa)
            Texture2D wallTex = Resources.Load<Texture2D>("BalulaMoto/Textures/murs_parcelles");
            if (wallTex != null)
            {
                wallMat = new Material(Mats.Lit(Color.white, 0.06f)) { name = "MursParcelles" };
                wallMat.color = Color.white;
                wallMat.mainTexture = wallTex;
                if (wallMat.HasProperty("_BaseMap")) wallMat.SetTexture("_BaseMap", wallTex);
                if (wallMat.HasProperty("_BaseColor")) wallMat.SetColor("_BaseColor", Color.white);
                wallMat.enableInstancing = false;
            }
            Texture2D wireTex = Resources.Load<Texture2D>("BalulaMoto/Textures/barbele");
            Shader wireShader = Resources.Load<Shader>("BalulaMoto/Shaders/Barbele");
            if (wireShader == null) wireShader = Shader.Find("BalulaMoto/Barbele");
            if (wireTex != null && wireShader != null && wireShader.isSupported)
            {
                wireMat = new Material(wireShader) { name = "Barbele" };
                wireMat.mainTexture = wireTex;
                wireMat.enableInstancing = false;
            }
            postMat = Mats.Lit(new Color(0.4f, 0.4f, 0.42f), 0.5f);
            for (int i = 0; i < 24; i++)
            {
                Texture2D tex = Resources.Load<Texture2D>("BalulaMoto/Pubs/pub_" + i);
                if (tex == null) break;
                Material m = new Material(Mats.Lit(Color.white, 0.35f));
                m.color = Color.white;
                m.mainTexture = tex;
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
                adMats.Add(m);
            }
            if (map == null) return;
            for (int r = 0; r < map.roads.Count; r++)
            {
                List<Vector2> pts = map.roads[r].pts;
                for (int i = 0; i < pts.Count - 1; i++)
                {
                    Vector2 a = pts[i], b = pts[i + 1];
                    float len = Vector2.Distance(a, b);
                    int steps = Mathf.Max(1, Mathf.CeilToInt(len / (CellSize * 0.25f)));
                    long lastKey = long.MinValue;
                    for (int k = 0; k <= steps; k++)
                    {
                        // Le long d'un segment droit, on ne revient jamais dans une case quittée
                        long key = Key(Vector2.Lerp(a, b, k / (float)steps));
                        if (key == lastKey) continue;
                        lastKey = key;
                        List<Seg> l;
                        if (!roadGrid.TryGetValue(key, out l)) { l = new List<Seg>(); roadGrid[key] = l; }
                        l.Add(new Seg { Road = r, Index = i });
                    }
                    if (map.roads[r].rank <= 1) continue;
                    int x0 = Mathf.FloorToInt((Mathf.Min(a.x, b.x) - GenReach) / CellSize), x1 = Mathf.FloorToInt((Mathf.Max(a.x, b.x) + GenReach) / CellSize);
                    int z0 = Mathf.FloorToInt((Mathf.Min(a.y, b.y) - GenReach) / CellSize), z1 = Mathf.FloorToInt((Mathf.Max(a.y, b.y) + GenReach) / CellSize);
                    for (int gz = z0; gz <= z1; gz++)
                        for (int gx = x0; gx <= x1; gx++)
                        {
                            // Distance du segment au carré de la case (approchée par le centre et la demi-diagonale)
                            Vector2 cc = new Vector2((gx + 0.5f) * CellSize, (gz + 0.5f) * CellSize);
                            if (PointSeg(cc, a, b) > GenReach + CellSize * 0.7072f) continue;
                            long gk = Key(gx, gz);
                            List<Seg> gl;
                            if (!genGrid.TryGetValue(gk, out gl)) { gl = new List<Seg>(); genGrid[gk] = gl; }
                            gl.Add(new Seg { Road = r, Index = i });
                        }
                }
            }
            BuildSigns();
            // Modèles de MATULA DESIGN (maisons, immeubles, barrières, mobilier, étals, parkings : CityFillModels.cs)
            InitModels();
            PlanModels();
            PlanBoulevards();
            // Emprise des cours d'eau (eau + berges) : ni parcelle, ni poteau, ni panneau dedans
            if (map.waterSegs != null && map.waterHw != null)
                for (int i = 0; i < map.waterSegs.Count && i < map.waterHw.Count; i++)
                {
                    Vector4 w = map.waterSegs[i];
                    float hw = map.waterHw[i] + 16f;
                    int x0 = Mathf.FloorToInt((Mathf.Min(w.x, w.z) - hw) / CellSize), x1 = Mathf.FloorToInt((Mathf.Max(w.x, w.z) + hw) / CellSize);
                    int z0 = Mathf.FloorToInt((Mathf.Min(w.y, w.w) - hw) / CellSize), z1 = Mathf.FloorToInt((Mathf.Max(w.y, w.w) + hw) / CellSize);
                    for (int gz = z0; gz <= z1; gz++)
                        for (int gx = x0; gx <= x1; gx++)
                        {
                            List<Sg> l;
                            if (!waterGrid.TryGetValue(Key(gx, gz), out l)) { l = new List<Sg>(); waterGrid[Key(gx, gz)] = l; }
                            l.Add(new Sg { A = new Vector2(w.x, w.y), B = new Vector2(w.z, w.w), Hw = map.waterHw[i] });
                        }
                }
            foreach (Vector3 b in map.buildingSpots)
            {
                long key = Key(new Vector2(b.x, b.z));
                List<Vector3> l;
                if (!buildingGrid.TryGetValue(key, out l)) { l = new List<Vector3>(); buildingGrid[key] = l; }
                l.Add(b);
            }
            // Morceaux de la carte importée (routes, bâtiments, surfaces) : dessinés seulement près du joueur.
            // On coupe l'affichage, pas les collisions (aucun recalcul physique en roulant).
            foreach (Transform child in map.transform)
            {
                Renderer rd = child.GetComponent<Renderer>();
                if (rd == null) continue;
                // Le sol reste toujours dessiné (sinon le ciel apparaîtrait sous l'horizon au loin)
                if (child.name.StartsWith("Sol")) continue;
                Bounds bd = rd.bounds;
                if (bd.size.x > 1500f || bd.size.z > 1500f) continue;
                chunks.Add(new KeyValuePair<Renderer, Bounds>(rd, bd));
            }
        }

        private static long PKey(Vector2 p)
        {
            return ((long)Mathf.RoundToInt(p.x * 4f) << 32) ^ (uint)Mathf.RoundToInt(p.y * 4f);
        }

        /// <summary>Abrège le type de voie pour tenir sur une plaque (« Avenue Kasa-Vubu » → « Av. Kasa-Vubu »).</summary>
        public static string ShortName(string n)
        {
            if (string.IsNullOrEmpty(n)) return n;
            string[][] rep = { new[] { "Avenue ", "Av. " }, new[] { "Boulevard ", "Bd " }, new[] { "avenue ", "Av. " }, new[] { "boulevard ", "Bd " } };
            foreach (string[] r in rep) if (n.StartsWith(r[0])) return r[1] + n.Substring(r[0].Length);
            return n;
        }

        /// <summary>Carrefours entre deux rues portant des noms différents : une plaque bleue par rue.</summary>
        private void BuildSigns()
        {
            Dictionary<long, List<Seg>> nodes = new Dictionary<long, List<Seg>>();
            for (int r = 0; r < map.roads.Count; r++)
            {
                ImportedRoad road = map.roads[r];
                if (road.rank < 2 || string.IsNullOrEmpty(road.name)) continue;
                for (int i = 0; i < road.pts.Count; i++)
                {
                    long k = PKey(road.pts[i]);
                    List<Seg> l;
                    if (!nodes.TryGetValue(k, out l)) { l = new List<Seg>(); nodes[k] = l; }
                    l.Add(new Seg { Road = r, Index = i });
                }
            }
            foreach (KeyValuePair<long, List<Seg>> kv in nodes)
            {
                if (kv.Value.Count < 2) continue;
                Seg a = kv.Value[0], b = a;
                bool found = false;
                foreach (Seg o in kv.Value)
                    if (map.roads[o.Road].name != map.roads[a.Road].name) { b = o; found = true; break; }
                if (!found) continue;
                ImportedRoad ra = map.roads[a.Road], rb = map.roads[b.Road];
                SignSpot sp = new SignSpot { P = ra.pts[a.Index], A = ra.name, B = rb.name, HwA = ra.width * 0.5f, HwB = rb.width * 0.5f, DirA = DirAt(ra, a.Index), DirB = DirAt(rb, b.Index) };
                if (Mathf.Abs(Cross(sp.DirA, sp.DirB)) < 0.35f) continue; // rues presque parallèles : pas un vrai coin
                long cell = Key(sp.P);
                List<SignSpot> list;
                if (!signGrid.TryGetValue(cell, out list)) { list = new List<SignSpot>(); signGrid[cell] = list; }
                bool dup = false;
                foreach (SignSpot o in list)
                    if ((o.P - sp.P).sqrMagnitude < 35f * 35f && ((o.A == sp.A && o.B == sp.B) || (o.A == sp.B && o.B == sp.A))) { dup = true; break; }
                if (!dup) list.Add(sp);
            }
        }

        private static Vector2 DirAt(ImportedRoad r, int i)
        {
            Vector2 d = r.pts[Mathf.Min(i + 1, r.pts.Count - 1)] - r.pts[Mathf.Max(i - 1, 0)];
            return d.sqrMagnitude > 1e-6f ? d.normalized : Vector2.right;
        }

        private static bool OnRoad(Vector2 p, List<Sg> near, float margin)
        {
            foreach (Sg sg in near) if (PointSeg(p, sg.A, sg.B) < sg.Hw + margin) return true;
            return false;
        }

        /// <summary>Poteaux électriques avec fils et lampadaire, le long de toutes les rues de la case.</summary>
        private void StreetFurniture(Rect bounds, List<int> roadsHere, List<Sg> near, MeshData md, GameObject go)
        {
            const float Step = 42f;
            GameObject col = null;
            foreach (int ri in roadsHere)
            {
                ImportedRoad road = map.roads[ri];
                if (road.rank < 2 || road.pts.Count < 2) continue;
                float[] cum = Cum(ri);
                float total = cum[cum.Length - 1];
                if (total < 25f) continue;
                float side = (Hash(ri, 3, 1, 9) & 1) == 0 ? 1f : -1f;
                bool avenue = road.rank >= 5;
                float off = road.width * 0.5f + 1.75f;
                float h = avenue ? 9f : 7.6f;
                int poleColor = avenue ? Concrete : Trunk;
                for (float sd = Step * 0.5f; sd < total; sd += Step)
                {
                    Vector2 p2, dir;
                    At(ri, cum, sd, out p2, out dir);
                    Vector2 nrm = new Vector2(-dir.y, dir.x) * side;
                    Vector2 q = p2 + nrm * off;
                    if (!bounds.Contains(q)) continue;
                    if (OnRoad(q, near, 0.7f) || InWater(q, 0.2f)) continue;
                    bool blocked = false;
                    foreach (Keep k in keep) if ((k.C - q).sqrMagnitude < k.R * k.R) { blocked = true; break; }
                    if (blocked) continue;
                    Vector3 b = new Vector3(q.x, 0f, q.y), r = new Vector3(dir.x, 0f, dir.y), f = new Vector3(nrm.x, 0f, nrm.y), u = Vector3.up;
                    md.Box(b + u * (h * 0.5f), r, u, f, new Vector3(0.11f, h * 0.5f, 0.11f), poleColor, poleColor);
                    md.Box(b + u * (h - 0.45f), r, u, f, new Vector3(0.05f, 0.05f, 0.85f), Trunk, Trunk);
                    // Bras et lampe au-dessus de la chaussée
                    float arm = Mathf.Min(2.6f, off - 0.4f);
                    md.Box(b + u * (h - 1.5f) - f * (arm * 0.5f), r, u, f, new Vector3(0.04f, 0.04f, arm * 0.5f), 20, 20);
                    Vector3 lamp = b + u * (h - 1.58f) - f * arm;
                    if (curLum != null) curLum.Box(lamp, new Vector3(0.16f, 0.06f, 0.3f), new Color32(255, 218, 150, 255));
                    if (curHalo != null)
                    {
                        float rad = avenue ? 15f : 11f;
                        Vector3 g = b - f * (arm + 1.2f);
                        curHalo.Pool(new Vector3(g.x, 0.1f, g.z), rad, avenue ? new Color32(255, 205, 140, 255) : new Color32(230, 180, 115, 255));
                    }
                    // Fils jusqu'au poteau suivant (deux fils, légère flèche)
                    if (sd + Step < total)
                    {
                        Vector2 p3, dir3;
                        At(ri, cum, sd + Step, out p3, out dir3);
                        Vector2 q3 = p3 + new Vector2(-dir3.y, dir3.x) * side * off;
                        Vector3 b3 = new Vector3(q3.x, 0f, q3.y), f3 = new Vector3(-dir3.y, 0f, dir3.x) * side;
                        foreach (float w in new[] { -0.75f, 0.75f })
                        {
                            Vector3 w0 = b + u * (h - 0.38f) + f * w, w1 = b3 + u * (h - 0.38f) + f3 * w;
                            Vector3 mid = (w0 + w1) * 0.5f - u * 0.55f;
                            Wire(md, w0, mid);
                            Wire(md, mid, w1);
                        }
                    }
                    if (col == null) { col = new GameObject("Poteaux"); col.transform.SetParent(go.transform, false); }
                    AddBox(col, b + u * 1.5f, new Vector3(0.3f, 3f, 0.3f));
                    PoleCount++;
                }
            }
        }

        private static void Wire(MeshData md, Vector3 a, Vector3 b)
        {
            Vector3 t = Vector3.up * 0.03f;
            md.Quad(a - t, a + t, b + t, b - t, 8);
            md.Quad(b - t, b + t, a + t, a - t, 8);
        }

        /// <summary>Plaques bleues à lettres blanches au coin des rues (un poteau, une plaque par rue).</summary>
        private void Signs(long cellKey, List<Sg> near, MeshData md, Transform parent)
        {
            List<SignSpot> list;
            if (!signGrid.TryGetValue(cellKey, out list)) return;
            foreach (SignSpot sp in list)
            {
                // Coin du carrefour : en retrait des deux chaussées ; on essaie les quatre coins
                Vector2 q = Vector2.zero;
                bool ok = false;
                foreach (Vector2 sg in new[] { new Vector2(1f, 1f), new Vector2(-1f, 1f), new Vector2(1f, -1f), new Vector2(-1f, -1f) })
                {
                    // Décomposition dans le repère (non orthogonal) des deux rues
                    float det = Cross(sp.DirA, sp.DirB);
                    float da = sg.x * (sp.HwB + 1.3f) / Mathf.Abs(det), db = sg.y * (sp.HwA + 1.3f) / Mathf.Abs(det);
                    q = sp.P + sp.DirA * da + sp.DirB * db;
                    if ((q - sp.P).sqrMagnitude > 28f * 28f) continue;
                    if (OnRoad(q, near, 0.5f) || InWater(q, 0.3f)) continue;
                    ok = true;
                    break;
                }
                if (!ok) continue;
                Vector3 b = new Vector3(q.x, 0f, q.y), u = Vector3.up;
                md.Box(b + u * 1.45f, Vector3.right, u, Vector3.forward, new Vector3(0.035f, 1.45f, 0.035f), 20, 20);
                Plate(md, parent, b + u * 2.68f, sp.DirA, sp.A);
                Plate(md, parent, b + u * 2.36f, sp.DirB, sp.B);
                SignCount++;
            }
        }

        private void Plate(MeshData md, Transform parent, Vector3 center, Vector2 dir2, string name)
        {
            string text = ShortName(name);
            if (text.Length > 26) text = text.Substring(0, 25) + "…";
            Vector3 r = new Vector3(dir2.x, 0f, dir2.y), f = new Vector3(-dir2.y, 0f, dir2.x), u = Vector3.up;
            float hw = Mathf.Clamp(0.05f * text.Length + 0.12f, 0.35f, 1.5f);
            // La plaque part du poteau (en drapeau) : le poteau ne coupe pas le nom
            center += r * (hw + 0.07f);
            md.Box(center, r, u, f, new Vector3(hw + 0.03f, 0.15f, 0.012f), 18, 18);   // liseré blanc
            md.Box(center, r, u, f, new Vector3(hw, 0.125f, 0.016f), 6, 6);            // plaque bleue
            md.Panel(center - u * 0.15f - f * 0.001f, r, f, hw + 0.03f, 0.012f, 18);
            GameObject l = Prims.Label(parent, text, center, 0.0185f, Color.white, true);
            l.name = "Plaque";
            l.transform.rotation = Quaternion.LookRotation(f, u);
            foreach (Transform face in l.transform)
            {
                face.localPosition = face.localRotation * new Vector3(0f, 0f, -0.02f);
                face.gameObject.layer = Night.DetailLayer;
            }
            l.layer = Night.DetailLayer;
        }

        private void CollectKeepClear()
        {
            keepReady = true;
            if (map != null && map.clearZones != null)
                foreach (Vector3 z in map.clearZones) keep.Add(new Keep { C = new Vector2(z.x, z.z), R = z.y });
            if (map != null)
                foreach (ImportedPlace p in map.places)
                {
                    float r = p.isLandmark ? 18f : (p.isFuel ? 14f : (p.isStop ? 6f : (p.category == "marche" ? 25f : 0f)));
                    if (r > 0f) keep.Add(new Keep { C = new Vector2(p.position.x, p.position.z), R = r });
                }
            foreach (InteractionPoint ip in InteractionPoint.All)
                if (ip != null) keep.Add(new Keep { C = new Vector2(ip.transform.position.x, ip.transform.position.z), R = ip.Radius + 6f });
            foreach (FuelStations.Zone z in FuelStations.Zones) keep.Add(new Keep { C = z.C, R = z.R + 2f });
            foreach (ClientStop cs in ClientStop.All)
                if (cs != null) keep.Add(new Keep { C = new Vector2(cs.transform.position.x, cs.transform.position.z), R = 6f });
            foreach (LandmarkPin lp in FindObjectsByType<LandmarkPin>(FindObjectsSortMode.None))
                keep.Add(new Keep { C = new Vector2(lp.transform.position.x, lp.transform.position.z), R = 18f });
        }

        private static Material BuildAtlasMaterial()
        {
            int px = 16, rows = (Palette.Length + AtlasCols - 1) / AtlasCols;
            Texture2D t = new Texture2D(AtlasCols * px, rows * px, TextureFormat.RGBA32, false);
            t.filterMode = FilterMode.Point;
            t.wrapMode = TextureWrapMode.Clamp;
            Color[] pixels = new Color[t.width * t.height];
            for (int i = 0; i < Palette.Length; i++)
            {
                int cx = (i % AtlasCols) * px, cy = (i / AtlasCols) * px;
                for (int y = 0; y < px; y++)
                    for (int x = 0; x < px; x++)
                        pixels[(cy + y) * t.width + cx + x] = Palette[i];
            }
            t.SetPixels(pixels);
            t.Apply(false, true);
            Material m = new Material(Mats.Lit(Color.white, 0.12f));
            m.color = Color.white;
            m.mainTexture = t;
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", t);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
            return m;
        }

        private static Vector2 Swatch(int i)
        {
            int rows = (Palette.Length + AtlasCols - 1) / AtlasCols;
            return new Vector2(((i % AtlasCols) + 0.5f) / AtlasCols, ((i / AtlasCols) + 0.5f) / rows);
        }

        private static long Key(Vector2 p) { return Key(Mathf.FloorToInt(p.x / CellSize), Mathf.FloorToInt(p.y / CellSize)); }
        private static long Key(int cx, int cz) { return ((long)cx << 32) ^ (uint)cz; }

        private static bool PlayerPos(out Vector3 p)
        {
            TrafficManager tm = TrafficManager.Instance;
            if (tm != null && tm.Player != null) { p = tm.Player.transform.position; return true; }
            Camera c = Camera.main;
            if (c != null) { p = c.transform.position; return true; }
            p = Vector3.zero;
            return false;
        }

        private void Update()
        {
            if (map == null) return;
            Vector3 pp;
            if (!PlayerPos(out pp)) return;
            if (!keepReady) CollectKeepClear();
            // Saut de position (début de partie, retour du menu, voyage) : quartier reconstruit d'un coup
            if ((pp - lastPos).sqrMagnitude > 300f * 300f) { warm = false; chunkTimer = 0f; }
            lastPos = pp;

            chunkTimer -= Time.unscaledDeltaTime;
            if (chunkTimer <= 0f)
            {
                chunkTimer = 0.5f;
                float vis = GameSettings.Quality == 0 ? 450f : 700f;
                foreach (KeyValuePair<Renderer, Bounds> kv in chunks)
                {
                    if (kv.Key == null) continue;
                    Bounds b = kv.Value;
                    float dx = Mathf.Max(0f, Mathf.Abs(pp.x - b.center.x) - b.extents.x);
                    float dz = Mathf.Max(0f, Mathf.Abs(pp.z - b.center.z) - b.extents.z);
                    bool on = dx * dx + dz * dz < vis * vis;
                    if (kv.Key.enabled != on) kv.Key.enabled = on;
                }
            }

            if (!warm)
            {
                // Premier passage : le quartier autour du joueur est construit d'un coup (pas de ville qui « pousse »)
                warm = true;
                for (int i = 0; i < 14; i++) if (!BuildNearest(pp, 140f)) break;
            }

            scanTimer -= Time.unscaledDeltaTime;
            if (scanTimer > 0f) return;
            scanTimer = 0.08f;
            BuildNearest(pp, loadRadius);

            toUnload.Clear();
            foreach (KeyValuePair<long, Cell> kv in cells)
            {
                int cx = (int)(kv.Key >> 32), cz = (int)(uint)(kv.Key & 0xffffffffL);
                Vector2 center = new Vector2((cx + 0.5f) * CellSize, (cz + 0.5f) * CellSize);
                if (Vector2.Distance(center, new Vector2(pp.x, pp.z)) > loadRadius + CellSize * 1.8f) toUnload.Add(kv.Key);
            }
            foreach (long k in toUnload)
            {
                Cell c = cells[k];
                if (c.Go != null) Destroy(c.Go);
                if (c.Mesh != null) Destroy(c.Mesh);
                cells.Remove(k);
                placedByCell.Remove(k);
                stallsByCell.Remove(k);
            }
        }

        private bool BuildNearest(Vector3 pp, float radius)
        {
            int pcx = Mathf.FloorToInt(pp.x / CellSize), pcz = Mathf.FloorToInt(pp.z / CellSize);
            int r = Mathf.CeilToInt(radius / CellSize) + 1;
            long best = long.MinValue;
            float bestD = float.MaxValue;
            int bx = 0, bz = 0;
            for (int dz = -r; dz <= r; dz++)
                for (int dx = -r; dx <= r; dx++)
                {
                    int cx = pcx + dx, cz = pcz + dz;
                    long key = Key(cx, cz);
                    if (cells.ContainsKey(key)) continue;
                    // Distance du joueur au carré de la case (pas à son centre)
                    float ddx = Mathf.Max(0f, Mathf.Abs(pp.x - (cx + 0.5f) * CellSize) - CellSize * 0.5f);
                    float ddz = Mathf.Max(0f, Mathf.Abs(pp.z - (cz + 0.5f) * CellSize) - CellSize * 0.5f);
                    float d = Mathf.Sqrt(ddx * ddx + ddz * ddz);
                    if (d > radius || d >= bestD) continue;
                    bestD = d;
                    best = key;
                    bx = cx;
                    bz = cz;
                }
            if (best == long.MinValue) return false;
            cells[best] = BuildCell(bx, bz);
            return true;
        }

        private bool Blocked(Vector3 p, float r)
        {
            int cx = Mathf.FloorToInt(p.x / CellSize), cz = Mathf.FloorToInt(p.z / CellSize);
            Vector2 q = new Vector2(p.x, p.z);
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    List<Obb> l;
                    if (!placedByCell.TryGetValue(Key(cx + dx, cz + dz), out l)) continue;
                    foreach (Obb o in l) if (RectCircle(o, q, r)) return true;
                }
            return InLawn(q, r, cx, cz);
        }

        // ------------------------------------------------------------------
        // Géométrie 2D
        // ------------------------------------------------------------------
        private static bool RectCircle(Obb o, Vector2 p, float r)
        {
            Vector2 d = p - o.C;
            float lx = Vector2.Dot(d, o.X), lz = Vector2.Dot(d, o.Z);
            float cx = Mathf.Clamp(lx, -o.Hx, o.Hx), cz = Mathf.Clamp(lz, -o.Hz, o.Hz);
            float ex = lx - cx, ez = lz - cz;
            return ex * ex + ez * ez < r * r;
        }

        private static bool ObbOverlap(Obb a, Obb b, float margin)
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

        private static float PointSeg(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector2.Distance(p, a + ab * t);
        }

        private static float Cross(Vector2 a, Vector2 b) { return a.x * b.y - a.y * b.x; }

        private static bool SegIntersect(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
        {
            float d1 = Cross(q2 - q1, p1 - q1), d2 = Cross(q2 - q1, p2 - q1);
            float d3 = Cross(p2 - p1, q1 - p1), d4 = Cross(p2 - p1, q2 - p1);
            return ((d1 > 0f) != (d2 > 0f)) && ((d3 > 0f) != (d4 > 0f));
        }

        private static float SegSeg(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
        {
            if (SegIntersect(p1, p2, q1, q2)) return 0f;
            return Mathf.Min(Mathf.Min(PointSeg(p1, q1, q2), PointSeg(p2, q1, q2)), Mathf.Min(PointSeg(q1, p1, p2), PointSeg(q2, p1, p2)));
        }

        /// <summary>Distance exacte entre un rectangle orienté et un segment.</summary>
        private static float RectSeg(Obb o, Vector2 a, Vector2 b)
        {
            Vector2 la = new Vector2(Vector2.Dot(a - o.C, o.X), Vector2.Dot(a - o.C, o.Z));
            Vector2 lb = new Vector2(Vector2.Dot(b - o.C, o.X), Vector2.Dot(b - o.C, o.Z));
            if ((Mathf.Abs(la.x) <= o.Hx && Mathf.Abs(la.y) <= o.Hz) || (Mathf.Abs(lb.x) <= o.Hx && Mathf.Abs(lb.y) <= o.Hz)) return 0f;
            Vector2 c0 = new Vector2(-o.Hx, -o.Hz), c1 = new Vector2(o.Hx, -o.Hz), c2 = new Vector2(o.Hx, o.Hz), c3 = new Vector2(-o.Hx, o.Hz);
            float d = SegSeg(la, lb, c0, c1);
            d = Mathf.Min(d, SegSeg(la, lb, c1, c2));
            d = Mathf.Min(d, SegSeg(la, lb, c2, c3));
            return Mathf.Min(d, SegSeg(la, lb, c3, c0));
        }

        private bool Fits(Obb o, List<Sg> roads, List<Vector3> blds, float roadMargin, int cx, int cz, bool building = false)
        {
            float diag = Mathf.Sqrt(o.Hx * o.Hx + o.Hz * o.Hz);
            foreach (Sg s in roads)
            {
                // Maisons et bâtiments : 10 m libres devant les boulevards
                float m = building ? Mathf.Max(roadMargin, s.Clear) : roadMargin;
                if (PointSeg(o.C, s.A, s.B) > s.Hw + m + diag) continue;
                if (RectSeg(o, s.A, s.B) < s.Hw + m) return false;
            }
            // Bande verte du boulevard Lumumba (entre le grand et le petit boulevard) : rien n'y est construit
            if (InGreen(o, cx, cz)) return false;
            foreach (Vector3 b in blds)
                if (RectCircle(o, new Vector2(b.x, b.z), b.y + 0.6f)) return false;
            // Aucune parcelle sur une rivière ni sur ses berges : les maisons restent le long du tracé, en retrait
            if (curWater != null)
                foreach (Sg s in curWater)
                {
                    if (PointSeg(o.C, s.A, s.B) > s.Hw + 2f + diag) continue;
                    if (RectSeg(o, s.A, s.B) < s.Hw + 2f) return false;
                }
            foreach (Keep k in keep)
            {
                float dx = k.C.x - o.C.x, dz = k.C.y - o.C.y, lim = k.R + diag;
                if (dx * dx + dz * dz > lim * lim) continue;
                if (RectCircle(o, k.C, k.R)) return false;
            }
            for (int dz2 = -1; dz2 <= 1; dz2++)
                for (int dx2 = -1; dx2 <= 1; dx2++)
                {
                    List<Obb> l;
                    if (!placedByCell.TryGetValue(Key(cx + dx2, cz + dz2), out l)) continue;
                    foreach (Obb p in l)
                    {
                        float ddx = p.C.x - o.C.x, ddz = p.C.y - o.C.y, lim = diag + p.Hx + p.Hz;
                        if (ddx * ddx + ddz * ddz > lim * lim) continue;
                        if (ObbOverlap(o, p, 0.15f)) return false;
                    }
                }
            return true;
        }

        // ------------------------------------------------------------------
        // Construction d'une case de 100 m
        // ------------------------------------------------------------------
        private class MeshData
        {
            public readonly List<Vector3> V = new List<Vector3>();
            public readonly List<Vector3> N = new List<Vector3>();
            public readonly List<Vector2> UV = new List<Vector2>();
            public readonly List<int> T = new List<int>();
            /// <summary>Murs de clôture texturés (parpaings, ciment, chaux, inscriptions) : 2e matériau.</summary>
            public readonly List<int> TWall = new List<int>();
            /// <summary>Fil de fer barbelé au-dessus des murs : 3e matériau (découpe, visible des deux côtés).</summary>
            public readonly List<int> TWire = new List<int>();

            /// <summary>Quadrilatère texturé a-b-c-d (UV donnés), tourné vers la normale attendue, dans un sous-maillage.</summary>
            public void TexQuad(List<int> tris, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud, Vector3 normal)
            {
                int i = V.Count;
                V.Add(a); V.Add(b); V.Add(c); V.Add(d);
                N.Add(normal); N.Add(normal); N.Add(normal); N.Add(normal);
                UV.Add(ua); UV.Add(ub); UV.Add(uc); UV.Add(ud);
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) >= 0f)
                {
                    tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
                    tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
                }
                else
                {
                    tris.Add(i); tris.Add(i + 2); tris.Add(i + 1);
                    tris.Add(i); tris.Add(i + 3); tris.Add(i + 2);
                }
            }

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, int color)
            {
                Vector3 n = Vector3.Cross(b - a, c - a);
                if (n.sqrMagnitude < 1e-10f) return;
                n.Normalize();
                int i = V.Count;
                Vector2 uv = Swatch(color);
                V.Add(a); V.Add(b); V.Add(c); V.Add(d);
                N.Add(n); N.Add(n); N.Add(n); N.Add(n);
                UV.Add(uv); UV.Add(uv); UV.Add(uv); UV.Add(uv);
                T.Add(i); T.Add(i + 1); T.Add(i + 2);
                T.Add(i); T.Add(i + 2); T.Add(i + 3);
            }

            public void Tri(Vector3 a, Vector3 b, Vector3 c, int color)
            {
                Vector3 n = Vector3.Cross(b - a, c - a);
                if (n.sqrMagnitude < 1e-10f) return;
                n.Normalize();
                int i = V.Count;
                Vector2 uv = Swatch(color);
                V.Add(a); V.Add(b); V.Add(c);
                N.Add(n); N.Add(n); N.Add(n);
                UV.Add(uv); UV.Add(uv); UV.Add(uv);
                T.Add(i); T.Add(i + 1); T.Add(i + 2);
            }

            /// <summary>Boîte orientée (repère right/up/forward), sans face inférieure.</summary>
            public void Box(Vector3 c, Vector3 r, Vector3 u, Vector3 f, Vector3 half, int color, int topColor)
            {
                Vector3 R = r * half.x, U = u * half.y, F = f * half.z;
                Face(c + R, F, U, color);
                Face(c - R, U, F, color);
                Face(c + U, R, F, topColor);
                Face(c + F, U, R, color);
                Face(c - F, R, U, color);
            }

            // Face centrée en p : sommets p-A-B, p-A+B, p+A+B, p+A-B ; normale = B × A (vers l'extérieur)
            private void Face(Vector3 p, Vector3 A, Vector3 B, int color)
            {
                Quad(p - A - B, p - A + B, p + A + B, p + A - B, color);
            }

            /// <summary>Panneau plat (fenêtre, porte, enseigne) : visible du côté de la normale haut × droite
            /// (pour une façade tournée vers -avant d'un repère direct, passer droite = r).</summary>
            public void Panel(Vector3 center, Vector3 right, Vector3 up, float halfW, float halfH, int color)
            {
                Vector3 R = right * halfW, U = up * halfH;
                Quad(center - R - U, center - R + U, center + R + U, center + R - U, color);
            }

            public void Octa(Vector3 c, Vector3 r, int color)
            {
                for (int sx = -1; sx <= 1; sx += 2)
                    for (int sy = -1; sy <= 1; sy += 2)
                        for (int sz = -1; sz <= 1; sz += 2)
                        {
                            Vector3 a = c + new Vector3(r.x * sx, 0f, 0f), b = c + new Vector3(0f, r.y * sy, 0f), d = c + new Vector3(0f, 0f, r.z * sz);
                            if (sx * sy * sz > 0) Tri(a, b, d, color);
                            else Tri(a, d, b, color);
                        }
            }
        }

        private static int Hash(int a, int b, int c, int d)
        {
            unchecked
            {
                int h = (int)2166136261;
                h = (h ^ a) * 16777619;
                h = (h ^ b) * 16777619;
                h = (h ^ c) * 16777619;
                h = (h ^ d) * 16777619;
                return h & 0x7fffffff;
            }
        }

        private readonly Dictionary<int, float[]> cumCache = new Dictionary<int, float[]>();

        /// <summary>Longueurs cumulées d'une rue (mises en cache).</summary>
        private float[] Cum(int road)
        {
            float[] c;
            if (cumCache.TryGetValue(road, out c)) return c;
            List<Vector2> pts = map.roads[road].pts;
            c = new float[pts.Count];
            for (int i = 1; i < pts.Count; i++) c[i] = c[i - 1] + Vector2.Distance(pts[i - 1], pts[i]);
            cumCache[road] = c;
            return c;
        }

        /// <summary>Point et direction à l'abscisse s le long d'une rue.</summary>
        private void At(int road, float[] cum, float s, out Vector2 p, out Vector2 dir)
        {
            List<Vector2> pts = map.roads[road].pts;
            int lo = 0, hi = cum.Length - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (cum[mid] <= s) lo = mid; else hi = mid;
            }
            Vector2 a = pts[lo], b = pts[lo + 1];
            float len = Mathf.Max(1e-4f, cum[lo + 1] - cum[lo]);
            dir = (b - a) / len;
            p = a + (b - a) * Mathf.Clamp01((s - cum[lo]) / len);
        }

        private Cell BuildCell(int cx, int cz)
        {
            Cell cell = new Cell();
            long cellKey = Key(cx, cz);
            GameObject go = new GameObject("Case_" + cx + "_" + cz);
            go.transform.SetParent(transform, false);
            cell.Go = go;
            List<Obb> placed = new List<Obb>();
            placedByCell[cellKey] = placed;
            List<Seg> segs;
            if (!genGrid.TryGetValue(cellKey, out segs) || segs.Count == 0) return cell;

            Rect bounds = new Rect(cx * CellSize, cz * CellSize, CellSize, CellSize);
            List<Sg> near = new List<Sg>();
            HashSet<long> nearSeen = new HashSet<long>();
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    List<Seg> l;
                    if (!roadGrid.TryGetValue(Key(cx + dx, cz + dz), out l)) continue;
                    foreach (Seg s in l)
                    {
                        if (!nearSeen.Add(((long)s.Road << 24) ^ s.Index)) continue;
                        ImportedRoad rd = map.roads[s.Road];
                        near.Add(new Sg { A = rd.pts[s.Index], B = rd.pts[s.Index + 1], Hw = rd.width * 0.5f, Clear = IsBoulevardRoad(s.Road) ? BoulevardRules.Setback : 0f });
                    }
                }
            curWater = null;
            {
                List<Sg> wl;
                if (waterGrid.TryGetValue(cellKey, out wl)) curWater = wl;
            }
            List<Vector3> blds = new List<Vector3>();
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    List<Vector3> l;
                    if (buildingGrid.TryGetValue(Key(cx + dx, cz + dz), out l)) blds.AddRange(l);
                }

            MeshData md = new MeshData();
            curLum = new LightMesh();
            curHalo = GameSettings.Quality == 0 ? null : new LightMesh();
            // 1) Panneaux publicitaires sur les avenues (une chance par segment, tirage stable)
            List<int> roadsHere = new List<int>();
            HashSet<int> roadSeen = new HashSet<int>();
            HashSet<long> segSeen = new HashSet<long>();
            foreach (Seg s in segs)
            {
                if (roadSeen.Add(s.Road)) roadsHere.Add(s.Road);
                if (!segSeen.Add(((long)s.Road << 24) ^ s.Index)) continue;
                ImportedRoad road = map.roads[s.Road];
                if (road.rank < 5 || adMats.Count == 0) continue;
                Vector2 a = road.pts[s.Index], b = road.pts[s.Index + 1];
                float len = Vector2.Distance(a, b);
                if (len <= 40f) continue;
                Vector2 dir = (b - a) / len, nrm = new Vector2(-dir.y, dir.x);
                System.Random ar = new System.Random(Hash(s.Road, s.Index, 7, 7));
                if (ar.Next(3) != 0) continue;
                float t = (float)ar.NextDouble() * 0.6f + 0.2f;
                float side = ar.Next(2) == 0 ? 1f : -1f;
                Vector2 bp = a + dir * (len * t) + nrm * side * (road.width * 0.5f + 3.4f);
                Obb bo = new Obb { C = bp, X = dir, Z = nrm, Hx = 3.4f, Hz = 0.8f };
                if (bounds.Contains(bp) && Fits(bo, near, blds, 1.2f, cx, cz))
                {
                    Billboard(go.transform, bp, dir * -side, ar);
                    placed.Add(bo);
                }
            }

            // 2) Parcelles en rangées le long de chaque rue, sur toute sa longueur (tirage stable par rue)
            foreach (int ri in roadsHere)
            {
                ImportedRoad road = map.roads[ri];
                if (road.rank <= 1 || road.pts.Count < 2) continue;
                float[] cum = Cum(ri);
                float total = cum[cum.Length - 1];
                if (total < 10f) continue;
                float half = road.width * 0.5f;
                bool avenue = road.rank >= 5;
                for (int sideI = 0; sideI < 2; sideI++)
                {
                    float side = sideI == 0 ? 1f : -1f;
                    for (int row = 0; row < 3; row++)
                    {
                        System.Random rr = new System.Random(Hash(ri, sideI, row, 1));
                        float along = 1.5f + (float)rr.NextDouble() * 4f;
                        // Boulevards : 10 m libres entre la chaussée et la première rangée de maisons
                        float back = half + (IsBoulevardRoad(ri) ? BoulevardRules.Setback + 0.4f : 3.0f) + row * 19.2f;
                        int k = 0;
                        while (along < total - 4f)
                        {
                            float w = 11f + (float)rr.NextDouble() * 7f;
                            float d = 14f + (float)rr.NextDouble() * 8f;
                            if (avenue && row == 0) d = 12f + (float)rr.NextDouble() * 6f;
                            // Modèle de MATULA DESIGN (cottage, maison, immeuble) ou parking : la parcelle prend ses dimensions
                            int model = PickModel(ri, road, row, rr);
                            bool parking = false;
                            if (model >= 0) ModelLot(model, rr, out w, out d);
                            else if (row == 0 && Goudron(road) && parkedCars.Count > 0 && rr.Next(100) < 7)
                            {
                                parking = true;
                                w = 12f + (float)rr.NextDouble() * 5f;
                                d = 7.6f;
                            }
                            int seed = Hash(ri, sideI * 8 + row, k++, 3);
                            float start = along;
                            along += w + 0.3f + (float)rr.NextDouble() * 1.2f;
                            if (start + w > total + 2f) break;
                            Vector2 p, dir;
                            At(ri, cum, start + w * 0.5f, out p, out dir);
                            Vector2 inward = new Vector2(-dir.y, dir.x) * side;
                            // La case propriétaire est celle du centre « normal » : tirage identique d'une case à l'autre
                            if (!bounds.Contains(p + inward * (back + d * 0.5f))) continue;
                            for (int attempt = 0; attempt < 3; attempt++)
                            {
                                // Modèles et parkings : dimensions fixes (pas de parcelle raccourcie)
                                if ((model >= 0 || parking) && attempt > 0) break;
                                float depth = d - attempt * 3f;
                                if (depth < 10f && model < 0 && !parking) break;
                                Vector2 c = p + inward * (back + attempt * 4f + depth * 0.5f);
                                Obb o = new Obb { C = c, X = dir, Z = inward, Hx = w * 0.5f, Hz = depth * 0.5f };
                                if (!Fits(o, near, blds, 0.8f, cx, cz, true)) continue;
                                placed.Add(o);
                                System.Random pr = new System.Random(seed);
                                // Repère direct (droite, haut, avant) : droite × haut = avant (vers l'intérieur de la parcelle)
                                Vector2 xr = dir * side;
                                if (model >= 0) ModelParcel(md, go.transform, c, xr, inward, w, depth, model, pr);
                                else if (parking) ParkingParcel(md, go.transform, c, xr, inward, w, depth, pr);
                                else if (avenue && row == 0 && attempt == 0 && pr.Next(10) < 7) ShopParcel(md, go.transform, c, xr, inward, w, depth, road.rank, pr);
                                else Parcel(md, go.transform, c, xr, inward, w, depth, pr);
                                cell.Parcels++;
                                // Étal de vendeur devant la parcelle (trottoir, contre le mur de façade)
                                if (row == 0 && attempt == 0 && pr.Next(100) < (Goudron(road) ? 16 : 10))
                                {
                                    Vector2 sp = p + inward * (half + 2.2f) + dir * (((float)pr.NextDouble() - 0.5f) * (w - 3f));
                                    if (!OnRoad(sp, near, 1.0f) && !InWater(sp, 0.5f)) Stall(md, cellKey, sp, -inward, pr);
                                }
                                break;
                            }
                        }
                    }
                }
            }

            // 3) Remplissage des trous : petites parcelles orientées comme la rue la plus proche
            if (near.Count > 0)
            {
                System.Random fr = new System.Random(Hash(cx, cz, 99, 5));
                for (int gz = 0; gz < 17; gz++)
                    for (int gx = 0; gx < 17; gx++)
                    {
                        Vector2 q = new Vector2(bounds.xMin + 3f + gx * 5.8f + (float)fr.NextDouble() * 1.5f, bounds.yMin + 3f + gz * 5.8f + (float)fr.NextDouble() * 1.5f);
                        bool covered = false;
                        foreach (Obb o in placed)
                        {
                            Vector2 dd = q - o.C;
                            if (Mathf.Abs(Vector2.Dot(dd, o.X)) <= o.Hx + 1f && Mathf.Abs(Vector2.Dot(dd, o.Z)) <= o.Hz + 1f) { covered = true; break; }
                        }
                        if (covered) continue;
                        float bestD = float.MaxValue;
                        Sg best = default(Sg);
                        foreach (Sg sg in near)
                        {
                            float dist = PointSeg(q, sg.A, sg.B) - sg.Hw;
                            if (dist < bestD) { bestD = dist; best = sg; }
                        }
                        if (bestD < 4f || bestD > 70f) continue;
                        Vector2 dv = best.B - best.A;
                        if (dv.sqrMagnitude < 1e-4f) continue;
                        dv.Normalize();
                        Vector2 nv = new Vector2(-dv.y, dv.x);
                        // L'avant de la petite parcelle regarde la rue la plus proche
                        if (Vector2.Dot(q - best.A, nv) > 0f) nv = -nv;
                        Vector2 inward = -nv;
                        int sizeIndex = 0;
                        foreach (Vector2 size in new[] { new Vector2(13f, 15f), new Vector2(11f, 12f), new Vector2(9f, 10f) })
                        {
                            Obb o = new Obb { C = q, X = dv, Z = inward, Hx = size.x * 0.5f, Hz = size.y * 0.5f };
                            sizeIndex++;
                            if (!Fits(o, near, blds, 0.8f, cx, cz, true)) continue;
                            placed.Add(o);
                            System.Random pr = new System.Random(Hash(cx, cz, gx * 32 + gz, sizeIndex));
                            float sd = Vector2.Dot(new Vector2(-dv.y, dv.x), inward) > 0f ? 1f : -1f;
                            Parcel(md, go.transform, q, dv * sd, inward, size.x, size.y, pr);
                            cell.Parcels++;
                            break;
                        }
                    }
            }

            // 3) Barrières des boulevards, mobilier urbain, étals aux carrefours (modèles de MATULA DESIGN)
            Medians(cx, cz, bounds, go);
            GreenStrips(md, go.transform, cellKey);
            RoadItems(bounds, roadsHere, near, blds, cx, cz, placed, go);
            CornerStalls(cellKey, near, blds, cx, cz, placed, md);

            // 4) Poteaux électriques, fils, lampadaires et plaques de rue
            StreetFurniture(bounds, roadsHere, near, md, go);
            Signs(cellKey, near, md, go.transform);
            curLum.Emit(go.transform, "Lumieres", Night.LightMaterial);
            if (curHalo != null) curHalo.Emit(go.transform, "Halos", Night.HaloMaterial);
            curLum = null;
            curHalo = null;
            // Modèles de la case fusionnés par matériau
            if (batch.Instances > 0) batch.Emit(go.transform, "Modeles", GameSettings.Quality > 0);

            if (md.V.Count > 0)
            {
                Mesh mesh = new Mesh { name = "Parcelles_" + cx + "_" + cz };
                if (md.V.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(md.V);
                mesh.SetNormals(md.N);
                mesh.SetUVs(0, md.UV);
                bool walls = wallMat != null && md.TWall.Count > 0, wire = wireMat != null && md.TWire.Count > 0;
                mesh.subMeshCount = 1 + (walls ? 1 : 0) + (wire ? 1 : 0);
                mesh.SetTriangles(md.T, 0);
                int sub = 1;
                if (walls) mesh.SetTriangles(md.TWall, sub++);
                if (wire) mesh.SetTriangles(md.TWire, sub);
                mesh.RecalculateBounds();
                mesh.UploadMeshData(true);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer mr = go.AddComponent<MeshRenderer>();
                List<Material> cellMats = new List<Material> { atlasMat };
                if (walls) cellMats.Add(wallMat);
                if (wire) cellMats.Add(wireMat);
                mr.sharedMaterials = cellMats.ToArray();
                cell.Mesh = mesh;
            }
            return cell;
        }

        // ------------------------------------------------------------------
        // Murs de clôture bruts : bandes de la texture murs_parcelles (4 m de mur x 2,2 m de haut chacune)
        //   0 parpaings bruts, 1 ciment gris sale, 2 parpaings sombres, 3 chaux sale (répétées en longueur)
        //   4-7 les mêmes avec une inscription peinte (« Cette parcelle n'est pas à vendre »), sans répétition
        // ------------------------------------------------------------------
        private const float WallTile = 4f, WallTexH = 2.2f, WallBands = 8f;

        private static Vector2 WallUV(int band, float u, float h, float height)
        {
            float inset = 3f / 2048f;
            float v0 = 1f - (band + 1) / WallBands + inset, v1 = 1f - band / WallBands - inset;
            return new Vector2(u, Mathf.Lerp(v0, v1, Mathf.Clamp01(h / Mathf.Max(height, 0.01f))));
        }

        /// <summary>Face verticale d'un mur, de a à b au sol, hauteur h ; la texture se lit de gauche à droite pour qui
        /// regarde la face (normale n) ; u0 = départ dans la bande.</summary>
        private void WallFace(MeshData md, Vector3 a, Vector3 b, float h, Vector3 n, int band, float u0, float uScale)
        {
            Vector3 viewRight = Vector3.Cross(Vector3.up, -n);
            // Gauche du spectateur d'abord
            if (Vector3.Dot(b - a, viewRight) < 0f) { Vector3 t = a; a = b; b = t; }
            float len = Vector3.Distance(a, b);
            float u1 = u0 + len * uScale;
            Vector3 up = Vector3.up * h;
            md.TexQuad(md.TWall, a, a + up, b + up, b, WallUV(band, u0, 0f, h), WallUV(band, u0, h, h), WallUV(band, u1, h, h), WallUV(band, u1, 0f, h), n);
        }

        /// <summary>Mur de clôture texturé posé au sol : centre bas c, le long de « along », face extérieure tournée vers
        /// « outward ». Inscription peinte côté extérieur si graffiti ≥ 0 ; barbelé au-dessus si wire.</summary>
        private void FenceWall(MeshData md, Vector3 c, Vector3 along, Vector3 outward, float halfLen, float H, float T, int variant, int graffiti, bool wire, float phase)
        {
            if (wallMat == null)
            {
                // Texture absente : ancien mur de couleur unie
                Vector3 fwd = Vector3.Cross(along, Vector3.up);
                md.Box(c + Vector3.up * (H * 0.5f), along, Vector3.up, fwd, new Vector3(halfLen, H * 0.5f, T * 0.5f), WallFirst + variant, Cap);
                return;
            }
            Vector3 o = outward * (T * 0.5f), a = c - along * halfLen, b = c + along * halfLen;
            float k = 1f / WallTile;
            // Face extérieure : inscription au milieu (4 m) si le mur est assez long, mur ordinaire de part et d'autre
            if (graffiti >= 0 && halfLen * 2f >= 3.2f)
            {
                float panel = Mathf.Min(halfLen * 2f, WallTile);
                Vector3 pa = c - along * (panel * 0.5f), pb = c + along * (panel * 0.5f);
                WallFace(md, pa + o, pb + o, H, outward, graffiti, 0f, 1f / panel);
                if (halfLen * 2f > panel + 0.05f)
                {
                    WallFace(md, a + o, pa + o, H, outward, variant, phase, k);
                    WallFace(md, pb + o, b + o, H, outward, variant, phase + 0.37f, k);
                }
            }
            else WallFace(md, a + o, b + o, H, outward, variant, phase, k);
            // Face intérieure (cour) et bouts du mur
            WallFace(md, a - o, b - o, H, -outward, variant, phase + 0.5f, k);
            WallFace(md, a + o, a - o, H, -along, variant, phase, k);
            WallFace(md, b - o, b + o, H, along, variant, phase, k);
            // Chaperon en ciment
            Vector3 u = Vector3.up * H;
            if (Vector3.Cross(-2f * o, (b - a) - o).y >= 0f) md.Quad(a + o + u, a - o + u, b - o + u, b + o + u, Cap);
            else md.Quad(a + o + u, b + o + u, b - o + u, a - o + u, Cap);
            if (wire && wireMat != null)
            {
                // Concertina de barbelé tendue sur le chaperon (0,5 m), visible des deux côtés
                Vector3 w0 = Vector3.up * (H + 0.02f), w1 = Vector3.up * (H + 0.52f);
                float u1 = halfLen * 2f / 2f;
                md.TexQuad(md.TWire, a + w0, a + w1, b + w1, b + w0, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(u1, 1f), new Vector2(u1, 0f), outward);
            }
        }

        // ------------------------------------------------------------------
        // Parcelle résidentielle : murs de clôture, portail côté rue, maison au fond, parfois un arbre
        // ------------------------------------------------------------------
        private void Parcel(MeshData md, Transform parent, Vector2 c2, Vector2 x2, Vector2 z2, float w, float d, System.Random rnd)
        {
            Vector3 c = new Vector3(c2.x, 0f, c2.y);
            Vector3 r = new Vector3(x2.x, 0f, x2.y), f = new Vector3(z2.x, 0f, z2.y), u = Vector3.up;
            int gate = GateFirst + rnd.Next(GateCount);
            const float H = 2.2f, T = 0.2f;
            float hw = w * 0.5f, hd = d * 0.5f;
            // Murs bruts comme à Kinshasa : une variante par parcelle, inscription peinte côté rue sur 40 % des parcelles,
            // barbelé sur un tiers des clôtures
            int variant = rnd.Next(4);
            bool painted = rnd.Next(100) < 40;
            bool wire = rnd.Next(100) < 33;
            float phase = (float)rnd.NextDouble();
            // Portail : ouverture de 3 m dans le mur de façade (côté rue = -f)
            float gw = 3f, gx = Mathf.Clamp(((float)rnd.NextDouble() - 0.5f) * (w - 5f), -hw + 1.5f, hw - 1.5f - gw);
            float leftLen = gx + hw, rightLen = hw - (gx + gw);
            Vector3 front = c - f * hd;
            bool leftPainted = painted && leftLen >= rightLen;
            if (leftLen > 0.1f) FenceWall(md, front + r * (-hw + leftLen * 0.5f), r, -f, leftLen * 0.5f, H, T, variant, leftPainted ? 4 + variant : -1, wire, phase);
            if (rightLen > 0.1f) FenceWall(md, front + r * (hw - rightLen * 0.5f), r, -f, rightLen * 0.5f, H, T, variant, painted && !leftPainted ? 4 + variant : -1, wire, phase + 0.21f);
            md.Box(front + r * (gx + gw * 0.5f) + u * 1.0f, r, u, f, new Vector3(gw * 0.5f, 1.0f, 0.04f), gate, gate);
            FenceWall(md, c + f * hd, r, f, hw, H, T, variant, -1, wire, phase + 0.43f);
            FenceWall(md, c - r * hw, f, -r, hd, H, T, variant, -1, wire, phase + 0.61f);
            FenceWall(md, c + r * hw, f, r, hd, H, T, variant, -1, wire, phase + 0.83f);

            // Ampoule au portail (côté rue)
            if (curLum != null)
            {
                Vector3 gb = front + r * (gx + gw + 0.25f) + u * (H + 0.12f) - f * 0.05f;
                curLum.Box(gb, new Vector3(0.08f, 0.08f, 0.08f), new Color32(255, 230, 180, 200));
                if (curHalo != null) curHalo.Pool(new Vector3(gb.x, 0.09f, gb.z) - f * 1.4f, 3.6f, new Color32(150, 125, 85, 255));
            }
            // Maison au fond de la parcelle
            bool storey2 = rnd.Next(5) == 0;
            float mw = w * (0.55f + (float)rnd.NextDouble() * 0.2f), mdp = d * (0.38f + (float)rnd.NextDouble() * 0.14f);
            float mh = storey2 ? 6.2f : 3.1f;
            Vector3 hc = c + f * (hd - mdp * 0.5f - 1.4f) + r * (((float)rnd.NextDouble() - 0.5f) * (w - mw - 1f));
            int houseColor = WallFirst + rnd.Next(WallCount);
            md.Box(hc + u * (mh * 0.5f), r, u, f, new Vector3(mw * 0.5f, mh * 0.5f, mdp * 0.5f), houseColor, Concrete);
            // Fenêtres et porte sur la façade (côté cour = -f)
            Vector3 face = hc - f * (mdp * 0.5f + 0.02f);
            int nwin = Mathf.Max(1, Mathf.FloorToInt(mw / 3.2f));
            for (int fl = 0; fl < (storey2 ? 2 : 1); fl++)
                for (int i = 0; i < nwin; i++)
                {
                    float wx = -mw * 0.5f + (i + 0.5f) * mw / nwin;
                    bool door = fl == 0 && i == nwin / 2;
                    float wy = fl * 3.1f + (door ? 1.05f : 1.6f), whalf = door ? 1.05f : 0.6f;
                    md.Panel(face + r * wx + u * wy, r, u, door ? 0.5f : 0.55f, whalf, door ? Trunk : Window);
                    // Courant dans la parcelle : fenêtres éclairées la nuit (pas toutes, et pas toutes au même moment)
                    if (curLum != null && !door && rnd.Next(100) < 62)
                        curLum.Panel(face - f * 0.025f + r * wx + u * wy, r, u, 0.5f, 0.55f, WindowLight(rnd));
                    if (curLum != null && door)
                    {
                        // Ampoule au-dessus de la porte, et sa lumière dans la cour
                        Vector3 bulb = face - f * 0.12f + r * wx + u * 2.35f;
                        curLum.Box(bulb, new Vector3(0.07f, 0.07f, 0.07f), new Color32(255, 235, 190, 255));
                        if (curHalo != null) curHalo.Pool(new Vector3(bulb.x, 0.09f, bulb.z) - f * 1.6f, 4.2f, new Color32(190, 160, 110, 255));
                    }
                }
            if (storey2)
            {
                // Toit-terrasse en béton avec acrotère
                md.Box(hc + u * (mh + 0.25f), r, u, f, new Vector3(mw * 0.5f + 0.05f, 0.25f, mdp * 0.5f + 0.05f), Concrete, Cap);
            }
            else
            {
                // Toit de tôle à deux pans (faîtage parallèle à la rue)
                int roof = rnd.Next(3) == 0 ? RoofGray : RoofRust;
                float ox = mw * 0.5f + 0.35f, oz = mdp * 0.5f + 0.45f, rise = 0.9f;
                Vector3 b0 = hc + u * mh;
                Vector3 eFL = b0 - r * ox - f * oz, eFR = b0 + r * ox - f * oz, eBL = b0 - r * ox + f * oz, eBR = b0 + r * ox + f * oz;
                Vector3 rL = b0 - r * ox + u * rise, rR = b0 + r * ox + u * rise;
                md.Quad(eFL, rL, rR, eFR, roof);
                md.Quad(eBR, rR, rL, eBL, roof);
                // Dessous des débords (visible de la rue)
                md.Quad(eFL, eFR, rR, rL, RoofGray);
                md.Quad(eBR, eBL, rL, rR, RoofGray);
                // Pignons (sous le toit, couleur de la maison)
                Vector3 gL = hc + u * mh - r * (mw * 0.5f), gR = hc + u * mh + r * (mw * 0.5f);
                md.Tri(gL - f * (mdp * 0.5f), gL + f * (mdp * 0.5f), gL + u * rise, houseColor);
                md.Tri(gR + f * (mdp * 0.5f), gR - f * (mdp * 0.5f), gR + u * rise, houseColor);
            }
            // Arbre dans la cour (manguier stylisé)
            if (rnd.Next(10) < 4)
            {
                Vector3 tp = c - f * (hd - 3.2f) + r * ((rnd.Next(2) == 0 ? -1f : 1f) * (hw - 2.5f));
                float th = 2.6f + (float)rnd.NextDouble() * 1.6f;
                md.Box(tp + u * (th * 0.5f), r, u, f, new Vector3(0.16f, th * 0.5f, 0.16f), Trunk, Trunk);
                float cr = 1.8f + (float)rnd.NextDouble() * 1.2f;
                md.Octa(tp + u * (th + cr * 0.5f), new Vector3(cr, cr * 0.75f, cr), rnd.Next(2) == 0 ? Leaves : Leaves2);
            }

            // Collisions (murs et maison) : la moto ne traverse plus les parcelles
            GameObject col = ParcelCollider(parent, c, f);
            AddBox(col, new Vector3(0f, H * 0.5f, -hd), new Vector3(w, H, T + 0.1f));
            AddBox(col, new Vector3(0f, H * 0.5f, hd), new Vector3(w, H, T));
            AddBox(col, new Vector3(-hw, H * 0.5f, 0f), new Vector3(T, H, d));
            AddBox(col, new Vector3(hw, H * 0.5f, 0f), new Vector3(T, H, d));
            Vector3 localHouse = col.transform.InverseTransformPoint(hc);
            AddBox(col, new Vector3(localHouse.x, mh * 0.5f, localHouse.z), new Vector3(mw, mh, mdp));
        }

        // ------------------------------------------------------------------
        // Boutique ou petit immeuble au bord d'une avenue (façade sur le trottoir, cour fermée derrière)
        // ------------------------------------------------------------------
        private void ShopParcel(MeshData md, Transform parent, Vector2 c2, Vector2 x2, Vector2 z2, float w, float d, int rank, System.Random rnd)
        {
            Vector3 c = new Vector3(c2.x, 0f, c2.y);
            Vector3 r = new Vector3(x2.x, 0f, x2.y), f = new Vector3(z2.x, 0f, z2.y), u = Vector3.up;
            const float H = 2.2f, T = 0.2f;
            float hw = w * 0.5f, hd = d * 0.5f;
            int floors = 1;
            int roll = rnd.Next(100);
            if (roll < 30) floors = 2;
            if (rank >= 6 && roll < 15) floors = 3 + rnd.Next(3);
            float fh = 3.3f, bh = floors * fh;
            float bd = Mathf.Min(d - 3f, 6f + (float)rnd.NextDouble() * 3f + (floors > 2 ? 3f : 0f));
            float bw = w - 0.2f;
            int color = WallFirst + rnd.Next(WallCount);
            Vector3 bc = c - f * (hd - bd * 0.5f);
            md.Box(bc + u * (bh * 0.5f), r, u, f, new Vector3(bw * 0.5f, bh * 0.5f, bd * 0.5f), color, Concrete);
            if (curLum != null)
            {
                // Boutique éclairée la nuit : enseigne lumineuse sur la façade et fenêtres des étages
                System.Random lr = new System.Random(Hash(Mathf.RoundToInt(c2.x * 3f), Mathf.RoundToInt(c2.y * 3f), floors, 5));
                Vector3 sf = bc - f * (bd * 0.5f + 0.06f);
                curLum.Panel(sf + u * 2.95f, r, u, bw * 0.38f, 0.07f, new Color32(255, 240, 215, 255));
                if (curHalo != null) curHalo.Pool(new Vector3(sf.x, 0.09f, sf.z) - f * 2.2f, 6.5f, new Color32(200, 175, 130, 255));
                for (int fl = 1; fl < floors; fl++)
                    for (float wx = -bw * 0.5f + 1.6f; wx < bw * 0.5f - 1f; wx += 3f)
                        if (lr.Next(100) < 55) curLum.Panel(sf + r * wx + u * (fl * fh + 1.7f), r, u, 0.55f, 0.6f, WindowLight(lr));
            }
            // Acrotère
            md.Box(bc + u * (bh + 0.2f), r, u, f, new Vector3(bw * 0.5f, 0.2f, bd * 0.5f), Concrete, Cap);
            Vector3 face = bc - f * (bd * 0.5f + 0.02f);
            // Rez-de-chaussée : rideaux métalliques et enseigne colorée
            int shops = Mathf.Max(1, Mathf.RoundToInt(bw / 4.5f));
            int sign = SignColors[rnd.Next(SignColors.Length)];
            for (int i = 0; i < shops; i++)
            {
                float sx = -bw * 0.5f + (i + 0.5f) * bw / shops;
                bool open = rnd.Next(3) > 0;
                md.Panel(face + r * sx + u * 1.25f, r, u, bw / shops * 0.5f - 0.35f, 1.25f, open ? Window : Shutter);
            }
            md.Panel(face - f * 0.01f + u * 2.95f, r, u, bw * 0.5f - 0.1f, 0.38f, sign);
            // Étages : fenêtres
            int nwin = Mathf.Max(1, Mathf.FloorToInt(bw / 3f));
            for (int fl = 1; fl < floors; fl++)
                for (int i = 0; i < nwin; i++)
                {
                    float wx = -bw * 0.5f + (i + 0.5f) * bw / nwin;
                    md.Panel(face + r * wx + u * (fl * fh + 1.7f), r, u, 0.6f, 0.75f, Window);
                }
            // Cour fermée derrière la boutique
            float yard = d - bd;
            if (yard > 1f)
            {
                int variant = rnd.Next(4);
                bool wire = rnd.Next(100) < 45;
                float phase = (float)rnd.NextDouble();
                Vector3 yc = c + f * (hd - yard * 0.5f);
                FenceWall(md, c + f * hd, r, f, hw, H, T, variant, -1, wire, phase);
                FenceWall(md, yc - r * hw, f, -r, yard * 0.5f, H, T, variant, -1, wire, phase + 0.3f);
                FenceWall(md, yc + r * hw, f, r, yard * 0.5f, H, T, variant, -1, wire, phase + 0.6f);
            }
            GameObject col = ParcelCollider(parent, c, f);
            AddBox(col, new Vector3(0f, bh * 0.5f, -hd + bd * 0.5f), new Vector3(bw, bh, bd));
            if (yard > 1f)
            {
                AddBox(col, new Vector3(0f, H * 0.5f, hd), new Vector3(w, H, T));
                AddBox(col, new Vector3(-hw, H * 0.5f, hd - yard * 0.5f), new Vector3(T, H, yard));
                AddBox(col, new Vector3(hw, H * 0.5f, hd - yard * 0.5f), new Vector3(T, H, yard));
            }
        }

        private static Color32 WindowLight(System.Random rnd)
        {
            int k = rnd.Next(10);
            byte a = (byte)(110 + rnd.Next(146));
            if (k < 6) return new Color32(255, 214, 140, a);   // ampoule chaude
            if (k < 8) return new Color32(235, 240, 255, a);   // tube blanc
            return new Color32(150, 190, 255, a);              // télévision
        }

        private static GameObject ParcelCollider(Transform parent, Vector3 c, Vector3 f)
        {
            GameObject col = new GameObject("Parcelle");
            col.transform.SetParent(parent, false);
            col.transform.position = c;
            col.transform.rotation = Quaternion.LookRotation(f, Vector3.up);
            return col;
        }

        private static void AddBox(GameObject g, Vector3 center, Vector3 size)
        {
            BoxCollider b = g.AddComponent<BoxCollider>();
            b.center = center;
            b.size = size;
        }

        /// <summary>Panneau publicitaire double face sur deux poteaux, tourné vers la circulation.</summary>
        private void Billboard(Transform parent, Vector2 p2, Vector2 facing2, System.Random rnd)
        {
            GameObject g = new GameObject("PanneauPub");
            g.transform.SetParent(parent, false);
            g.transform.position = new Vector3(p2.x, 0f, p2.y);
            Vector3 face = new Vector3(facing2.x, 0f, facing2.y);
            face = Quaternion.Euler(0f, rnd.Next(2) == 0 ? 25f : -25f, 0f) * face;
            g.transform.rotation = Quaternion.LookRotation(face, Vector3.up);
            Transform t = g.transform;
            float bw = 6f, bh = 3f, by = 3.2f + bh * 0.5f;
            foreach (float sx in new[] { -1.8f, 1.8f })
            {
                GameObject post = Prims.Box(t, "Poteau", new Vector3(sx, (by - bh * 0.5f) * 0.5f + 0.1f, -0.1f), new Vector3(0.22f, by - bh * 0.5f + 0.2f, 0.22f), Color.gray);
                post.GetComponent<Renderer>().sharedMaterial = postMat;
            }
            // Cadre derrière les affiches (affiche avant à z = 0, affiche arrière à z = -0.2)
            GameObject frame = Prims.Box(t, "Cadre", new Vector3(0f, by, -0.1f), new Vector3(bw + 0.3f, bh + 0.3f, 0.16f), Color.gray);
            frame.GetComponent<Renderer>().sharedMaterial = postMat;
            BoxCollider bc = g.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, by * 0.5f, -0.1f);
            bc.size = new Vector3(4f, by, 0.4f);
            int first = rnd.Next(adMats.Count);
            AdQuad(t, new Vector3(0f, by, 0.005f), bw, bh, adMats[first], true);
            AdQuad(t, new Vector3(0f, by, -0.205f), bw, bh, adMats[(first + 1 + rnd.Next(Mathf.Max(1, adMats.Count - 1))) % adMats.Count], false);
        }

        private static void AdQuad(Transform t, Vector3 pos, float bw, float bh, Material mat, bool front)
        {
            GameObject ad = new GameObject(front ? "Affiche" : "AfficheDos");
            ad.transform.SetParent(t, false);
            ad.transform.localPosition = pos;
            Mesh q = new Mesh { name = "Affiche" };
            q.vertices = new[] { new Vector3(-bw * 0.5f, -bh * 0.5f, 0f), new Vector3(-bw * 0.5f, bh * 0.5f, 0f), new Vector3(bw * 0.5f, bh * 0.5f, 0f), new Vector3(bw * 0.5f, -bh * 0.5f, 0f) };
            if (front)
            {
                // Face visible vers +z local (vers la circulation) ; vue de face, la gauche de l'image est en +x
                q.triangles = new[] { 0, 2, 1, 0, 3, 2 };
                q.uv = new[] { new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f) };
            }
            else
            {
                q.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                q.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
            }
            q.RecalculateNormals();
            q.RecalculateBounds();
            ad.AddComponent<MeshFilter>().sharedMesh = q;
            ad.AddComponent<MeshRenderer>().sharedMaterial = mat;
            ad.AddComponent<MeshCleanup>().Target = q;
        }
    }

    /// <summary>Détruit un maillage créé à la volée quand son objet disparaît (évite les fuites mémoire).</summary>
    public class MeshCleanup : MonoBehaviour
    {
        public Mesh Target;
        private void OnDestroy() { if (Target != null) Destroy(Target); }
    }
}
