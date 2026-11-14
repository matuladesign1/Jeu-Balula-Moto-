// BALULA MOTO — modèles de MATULA DESIGN posés par la ville pleine (novembre 2026) :
//   • cottage (Cottage_FREE) sur plusieurs rues goudronnées, beaucoup plus le long du boulevard Lumumba ;
//   • maison « fhouse » dans presque tous les quartiers, avenues et rues de terre (jamais sur le goudron) ;
//   • cinq immeubles (5Building_FBX) le long de presque toutes les rues goudronnées et de quelques rues ;
//   • barrières de béton rouge et blanc au milieu des boulevards Lumumba, Sendwe, Huilerie et 24 Novembre ;
//   • mobilier urbain (bancs, fûts, bennes, glissières) sur quelques rues goudronnées ;
//   • étals de vendeurs (table, marchandises, parasol ou auvent, cartons) : 2 ou 3 à chaque carrefour et devant des parcelles ;
//   • parkings (voitures génériques et motos garées) le long des rues goudronnées.
// Toutes ces constructions sont génériques : aucune n'identifie un bâtiment réel de Kinshasa.
using System.Collections.Generic;
using BalulaMoto.Core;
using UnityEngine;

namespace BalulaMoto.Runtime
{
    public partial class CityFill
    {
        /// <summary>Étal de vendeur : position au sol et direction du client (vers la rue).</summary>
        public struct StallSpot { public Vector3 P, Face; public int Seed; }

        private readonly Dictionary<long, List<StallSpot>> stallsByCell = new Dictionary<long, List<StallSpot>>();
        private readonly DecorBatch batch = new DecorBatch();
        private readonly Dictionary<long, List<Vector3>> junctionGrid = new Dictionary<long, List<Vector3>>();
        private readonly Dictionary<long, List<Vector4>> junctionDirs = new Dictionary<long, List<Vector4>>();
        private readonly HashSet<int> medianRoads = new HashSet<int>();
        private readonly HashSet<int> lumumbaRoads = new HashSet<int>();
        private DecorModel cottage, maisonF, barrierA, barrierB;
        private readonly DecorModel[] towers = new DecorModel[5];
        private DecorModel bench, drum, dumpster, cone, jersey, awning, boxes;
        private readonly List<DecorModel> parkedCars = new List<DecorModel>();
        private readonly List<DecorModel> parkedMotos = new List<DecorModel>();
        private bool modelsReady;
        /// <summary>Derniers emplacements construits (photos de contrôle) : position au sol et direction de la rue.</summary>
        public struct Spot { public Vector3 P, Face; public string Kind; }
        public static readonly List<Spot> RecentSpots = new List<Spot>();
        private static void Note(string kind, Vector3 p, Vector3 face)
        {
            RecentSpots.Add(new Spot { P = p, Face = face, Kind = kind });
            if (RecentSpots.Count > 400) RecentSpots.RemoveRange(0, 100);
        }
        public int ModelHouses { get; private set; }
        public int Towers { get; private set; }
        public int Barriers { get; private set; }
        public int Stalls { get; private set; }
        public int ParkedVehicles { get; private set; }

        // Échelles des modèles (les maisons d'origine font 12 à 16 m : un peu réduites pour tenir dans les parcelles)
        private static readonly Vector3 CottageScale = new Vector3(0.85f, 0.85f, 0.85f);
        private static readonly Vector3 MaisonScale = new Vector3(0.8f, 0.9f, 0.8f);

        private static bool Paved(ImportedRoad r)
        {
            return r.surface == (int)SurfaceKind.Goudron || r.surface == (int)SurfaceKind.GoudronDegrade;
        }

        /// <summary>Rue goudronnée ou grande avenue (« goudron »).</summary>
        private static bool Goudron(ImportedRoad r) { return Paved(r) || r.rank >= 5; }

        private void InitModels()
        {
            if (modelsReady) return;
            modelsReady = true;
            cottage = DecorModels.Get("Decor/Cottage");
            maisonF = DecorModels.Get("Decor/MaisonF");
            barrierA = DecorModels.Get("Decor/Barriere_A");
            barrierB = DecorModels.Get("Decor/Barriere_B");
            for (int i = 0; i < 5; i++) towers[i] = DecorModels.Get("Decor/Immeuble_" + (i + 1));
            bench = DecorModels.Get("Decor/Rue_Banc");
            drum = DecorModels.Get("Decor/Rue_Fut");
            dumpster = DecorModels.Get("Decor/Rue_Benne");
            cone = DecorModels.Get("Decor/Rue_Cone");
            jersey = DecorModels.Get("Decor/Rue_Glissiere");
            awning = DecorModels.Get("Decor/Rue_Auvent");
            boxes = DecorModels.Get("Decor/Rue_Cartons");
            foreach (string c in VehicleBuilder.CarModels) { DecorModel m = DecorModels.Get("Vehicules/" + c); if (m != null) parkedCars.Add(m); }
            foreach (string c in new[] { "HondaClassique_LOD", "HondaCB750_LOD", "MotoCentrale_LOD" }) { DecorModel m = DecorModels.Get("Vehicules/" + c); if (m != null) parkedMotos.Add(m); }
        }

        /// <summary>Carrefours (toutes les rues) et boulevards à terre-plein (rangés par case).</summary>
        private void PlanModels()
        {
            Dictionary<long, List<Seg>> nodes = new Dictionary<long, List<Seg>>();
            for (int r = 0; r < map.roads.Count; r++)
            {
                ImportedRoad road = map.roads[r];
                if (road.rank < 2) continue;
                string n = TextUtil.Normalize(road.name ?? "");
                bool big = road.rank >= 5 || n.Contains("boulevard") || n.StartsWith("bd ") || Paved(road);
                if (big && (n.Contains("lumumba") || n.Contains("sendwe") || n.Contains("huilerie") || n.Contains("24 novembre")))
                    medianRoads.Add(r);
                if (n.Contains("lumumba")) lumumbaRoads.Add(r);
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
                Seg s0 = kv.Value[0];
                Vector2 p = map.roads[s0.Road].pts[s0.Index];
                // Directions des rues qui partent du carrefour (deux par rue traversante)
                List<Vector4> dirs = new List<Vector4>();
                foreach (Seg s in kv.Value)
                {
                    ImportedRoad rd = map.roads[s.Road];
                    float hw = rd.width * 0.5f;
                    if (s.Index > 0) { Vector2 d = (rd.pts[s.Index - 1] - p); if (d.sqrMagnitude > 0.01f) { d.Normalize(); dirs.Add(new Vector4(d.x, d.y, hw, s.Road)); } }
                    if (s.Index < rd.pts.Count - 1) { Vector2 d = (rd.pts[s.Index + 1] - p); if (d.sqrMagnitude > 0.01f) { d.Normalize(); dirs.Add(new Vector4(d.x, d.y, hw, s.Road)); } }
                }
                if (dirs.Count < 3) continue;
                long cell = Key(p);
                List<Vector3> jl;
                if (!junctionGrid.TryGetValue(cell, out jl)) { jl = new List<Vector3>(); junctionGrid[cell] = jl; }
                bool dup = false;
                foreach (Vector3 o in jl) if ((new Vector2(o.x, o.z) - p).sqrMagnitude < 9f) { dup = true; break; }
                if (dup) continue;
                jl.Add(new Vector3(p.x, jl.Count, p.y));
                junctionDirs[((long)Mathf.RoundToInt(p.x * 4f) << 32) ^ (uint)Mathf.RoundToInt(p.y * 4f)] = dirs;
            }
        }

        // ------------------------------------------------------------------
        // Choix du modèle d'une parcelle en rangée
        // ------------------------------------------------------------------
        /// <summary>-1 : parcelle dessinée par le jeu ; 0 : cottage ; 1 : maison fhouse ; 2..6 : immeubles.</summary>
        private int PickModel(int ri, ImportedRoad road, int row, System.Random rnd)
        {
            if (!modelsReady) return -1;
            bool goudron = Goudron(road);
            int roll = rnd.Next(1000);
            if (goudron)
            {
                bool lumumba = lumumbaRoads.Contains(ri);
                if (row == 0 && roll < (lumumba ? 140 : 170))
                {
                    int t = rnd.Next(5);
                    return towers[t] != null ? 2 + t : -1;
                }
                roll = rnd.Next(1000);
                if (row <= 1 && cottage != null && roll < (lumumba ? 420 : 130)) return 0;
                return -1;
            }
            // Rues de terre (quartiers, avenues) : maisons fhouse dans presque tous les quartiers, quelques immeubles
            if (row == 0 && roll < 30) { int t = rnd.Next(5); return towers[t] != null ? 2 + t : -1; }
            roll = rnd.Next(1000);
            if (maisonF != null && roll < 230) return 1;
            return -1;
        }

        private DecorModel ModelOf(int kind) { return kind == 0 ? cottage : (kind == 1 ? maisonF : towers[kind - 2]); }

        private Vector3 ScaleOf(int kind) { return kind == 0 ? CottageScale : (kind == 1 ? MaisonScale : Vector3.one); }

        /// <summary>Largeur (le long de la rue) et profondeur d'une parcelle qui accueille le modèle.</summary>
        private void ModelLot(int kind, System.Random rnd, out float w, out float d)
        {
            DecorModel m = ModelOf(kind);
            Vector3 s = ScaleOf(kind);
            float fw = m.Bounds.size.x * s.x, fd = m.Bounds.size.z * s.z;
            if (kind >= 2) { w = fw + 2.5f + (float)rnd.NextDouble() * 2f; d = fd + 3f + (float)rnd.NextDouble() * 2f; }
            else { w = fw + 3f + (float)rnd.NextDouble() * 2.5f; d = fd + 6.5f + (float)rnd.NextDouble() * 2.5f; }
        }

        /// <summary>Parcelle portant un modèle : clôture et portail (maisons) ou immeuble sur rue avec cour fermée.</summary>
        private void ModelParcel(MeshData md, Transform parent, Vector2 c2, Vector2 x2, Vector2 z2, float w, float d, int kind, System.Random rnd)
        {
            DecorModel m = ModelOf(kind);
            Vector3 s = ScaleOf(kind);
            Vector3 c = new Vector3(c2.x, 0f, c2.y);
            Vector3 r = new Vector3(x2.x, 0f, x2.y), f = new Vector3(z2.x, 0f, z2.y), u = Vector3.up;
            const float H = 2.2f, T = 0.2f;
            float hw = w * 0.5f, hd = d * 0.5f;
            float fw = m.Bounds.size.x * s.x, fd = m.Bounds.size.z * s.z;
            GameObject col = ParcelCollider(parent, c, f);
            Vector3 hc;
            if (kind >= 2)
            {
                // Immeuble : façade près du trottoir (1,5 m), cour fermée derrière
                hc = c - f * (hd - fd * 0.5f - 1.5f);
                int variant = rnd.Next(4);
                FenceWall(md, c + f * hd, r, f, hw, H, T, variant, -1, rnd.Next(2) == 0, (float)rnd.NextDouble());
                float yard = d - fd - 1.5f;
                if (yard > 1f)
                {
                    Vector3 yc = c + f * (hd - yard * 0.5f);
                    FenceWall(md, yc - r * hw, f, -r, yard * 0.5f, H, T, variant, -1, false, 0.3f);
                    FenceWall(md, yc + r * hw, f, r, yard * 0.5f, H, T, variant, -1, false, 0.6f);
                    AddBox(col, new Vector3(-hw, H * 0.5f, hd - yard * 0.5f), new Vector3(T, H, yard));
                    AddBox(col, new Vector3(hw, H * 0.5f, hd - yard * 0.5f), new Vector3(T, H, yard));
                }
                AddBox(col, new Vector3(0f, H * 0.5f, hd), new Vector3(w, H, T));
                Towers++;
                // Lumières des bureaux la nuit (quelques fenêtres)
                if (curLum != null)
                {
                    float top = m.Bounds.size.y;
                    Vector3 face = hc - f * (fd * 0.5f + 0.05f);
                    for (float y = 4.8f; y < top - 2f; y += 3.2f)
                        for (float x = -fw * 0.5f + 1.6f; x < fw * 0.5f - 1f; x += 3.2f)
                            if (rnd.Next(100) < 30) curLum.Panel(face + r * x + u * y, r, u, 1.1f, 0.9f, WindowLight(rnd));
                }
            }
            else
            {
                // Maison : clôture, portail côté rue, maison au fond de la cour
                int gate = GateFirst + rnd.Next(GateCount);
                int variant = rnd.Next(4);
                bool wire = rnd.Next(100) < 30;
                float phase = (float)rnd.NextDouble();
                float gw = 3f, gx = Mathf.Clamp(((float)rnd.NextDouble() - 0.5f) * (w - 5f), -hw + 1.5f, hw - 1.5f - gw);
                float leftLen = gx + hw, rightLen = hw - (gx + gw);
                Vector3 front = c - f * hd;
                bool painted = rnd.Next(100) < 35;
                if (leftLen > 0.1f) FenceWall(md, front + r * (-hw + leftLen * 0.5f), r, -f, leftLen * 0.5f, H, T, variant, painted ? 4 + variant : -1, wire, phase);
                if (rightLen > 0.1f) FenceWall(md, front + r * (hw - rightLen * 0.5f), r, -f, rightLen * 0.5f, H, T, variant, -1, wire, phase + 0.21f);
                md.Box(front + r * (gx + gw * 0.5f) + u * 1.0f, r, u, f, new Vector3(gw * 0.5f, 1.0f, 0.04f), gate, gate);
                FenceWall(md, c + f * hd, r, f, hw, H, T, variant, -1, wire, phase + 0.43f);
                FenceWall(md, c - r * hw, f, -r, hd, H, T, variant, -1, wire, phase + 0.61f);
                FenceWall(md, c + r * hw, f, r, hd, H, T, variant, -1, wire, phase + 0.83f);
                AddBox(col, new Vector3(0f, H * 0.5f, -hd), new Vector3(w, H, T + 0.1f));
                AddBox(col, new Vector3(0f, H * 0.5f, hd), new Vector3(w, H, T));
                AddBox(col, new Vector3(-hw, H * 0.5f, 0f), new Vector3(T, H, d));
                AddBox(col, new Vector3(hw, H * 0.5f, 0f), new Vector3(T, H, d));
                hc = c + f * (hd - fd * 0.5f - 1.4f) + r * (((float)rnd.NextDouble() - 0.5f) * Mathf.Max(0f, w - fw - 1f));
                if (curLum != null)
                {
                    Vector3 gb = front + r * (gx + gw + 0.25f) + u * (H + 0.12f) - f * 0.05f;
                    curLum.Box(gb, new Vector3(0.08f, 0.08f, 0.08f), new Color32(255, 230, 180, 200));
                    if (curHalo != null) curHalo.Pool(new Vector3(gb.x, 0.09f, gb.z) - f * 1.4f, 3.6f, new Color32(150, 125, 85, 255));
                }
                ModelHouses++;
            }
            // Le modèle regarde la rue (sa façade est du côté -Z du fichier)
            Quaternion rot = Quaternion.LookRotation(f, Vector3.up);
            Vector3 pos = hc - rot * new Vector3(m.Bounds.center.x * s.x, 0f, m.Bounds.center.z * s.z);
            batch.Add(m, Matrix4x4.TRS(pos, rot, s));
            Note(kind == 0 ? "cottage" : (kind == 1 ? "maison" : "immeuble"), hc, -f);
            Vector3 lh = col.transform.InverseTransformPoint(hc);
            AddBox(col, new Vector3(lh.x, m.Bounds.size.y * s.y * 0.5f, lh.z), new Vector3(fw * 0.92f, m.Bounds.size.y * s.y, fd * 0.92f));
        }

        // ------------------------------------------------------------------
        // Barrières rouge et blanc au milieu des boulevards
        // ------------------------------------------------------------------
        private void Medians(int cx, int cz, Rect bounds, GameObject go)
        {
            if (barrierA == null || medianRoads.Count == 0) return;
            const float Len = 3.75f;
            HashSet<int> done = new HashSet<int>();
            List<Seg> segs;
            if (!roadGrid.TryGetValue(Key(cx, cz), out segs)) return;
            GameObject col = null;
            foreach (Seg sg in segs)
            {
                if (!medianRoads.Contains(sg.Road) || !done.Add(sg.Road)) continue;
                ImportedRoad road = map.roads[sg.Road];
                string name = road.name ?? "";
                float[] cum = Cum(sg.Road);
                float total = cum[cum.Length - 1];
                for (float sd = Len * 0.5f; sd < total; sd += Len)
                {
                    Vector2 p, dir;
                    At(sg.Road, cum, sd, out p, out dir);
                    if (!bounds.Contains(p)) continue;
                    // Chaussées doubles (deux voies du même nom à moins de 30 m) : la barrière va au milieu du terre-plein,
                    // posée une seule fois (par la voie d'indice le plus petit)
                    Vector2 nrm = new Vector2(-dir.y, dir.x);
                    int twin;
                    Vector2 twinPt;
                    Vector2 q = p;
                    if (Twin(sg.Road, name, p, nrm, out twin, out twinPt))
                    {
                        if (twin < sg.Road) continue;
                        q = (p + twinPt) * 0.5f;
                    }
                    // Pas de barrière dans un carrefour : passage libre de 6 m au-delà de la rue qui croise
                    if (NearCrossing(sg.Road, name, q, 6f)) continue;
                    bool a = ((int)(sd / Len)) % 2 == 0;
                    Quaternion rot = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.y), Vector3.up);
                    Vector3 pos = new Vector3(q.x, 0.02f, q.y);
                    batch.Add(a || barrierB == null ? barrierA : barrierB, Matrix4x4.TRS(pos, rot, Vector3.one));
                    if (col == null) { col = new GameObject("Barrieres"); col.transform.SetParent(go.transform, false); }
                    GameObject b = new GameObject("Barriere");
                    b.transform.SetParent(col.transform, false);
                    b.transform.SetPositionAndRotation(pos, rot);
                    AddBox(b, new Vector3(0f, 0.55f, 0f), new Vector3(0.55f, 1.1f, Len - 0.05f));
                    Barriers++;
                    if (Barriers % 8 == 0) Note("barriere", pos, new Vector3(-dir.y, 0f, dir.x));
                }
            }
        }

        /// <summary>Autre voie du même boulevard, parallèle, entre 5 et 30 m (chaussées séparées).</summary>
        private bool Twin(int road, string name, Vector2 p, Vector2 nrm, out int twin, out Vector2 pt)
        {
            twin = -1;
            pt = p;
            float best = 30f;
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    List<Seg> l;
                    if (!roadGrid.TryGetValue(Key(Mathf.FloorToInt(p.x / CellSize) + dx, Mathf.FloorToInt(p.y / CellSize) + dz), out l)) continue;
                    foreach (Seg s in l)
                    {
                        if (s.Road == road || map.roads[s.Road].name != name) continue;
                        Vector2 a = map.roads[s.Road].pts[s.Index], b = map.roads[s.Road].pts[s.Index + 1];
                        Vector2 ab = b - a;
                        if (ab.sqrMagnitude < 0.01f) continue;
                        // parallèle (moins de 25°)
                        if (Mathf.Abs(Vector2.Dot(ab.normalized, new Vector2(nrm.y, -nrm.x))) < 0.9f) continue;
                        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                        Vector2 q = a + ab * t;
                        float dist = Vector2.Distance(p, q);
                        if (dist < 5f || dist >= best) continue;
                        best = dist;
                        twin = s.Road;
                        pt = q;
                    }
                }
            return twin >= 0;
        }

        /// <summary>Vrai si une rue qui croise (autre nom, ou même nom mais non parallèle) passe près du point.</summary>
        private bool NearCrossing(int road, string name, Vector2 p, float margin)
        {
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    List<Seg> l;
                    if (!roadGrid.TryGetValue(Key(Mathf.FloorToInt(p.x / CellSize) + dx, Mathf.FloorToInt(p.y / CellSize) + dz), out l)) continue;
                    foreach (Seg s in l)
                    {
                        if (s.Road == road) continue;
                        ImportedRoad rd = map.roads[s.Road];
                        if (rd.rank < 2 || rd.name == name) continue;
                        Vector2 a = rd.pts[s.Index], b = rd.pts[s.Index + 1];
                        if (PointSeg(p, a, b) < rd.width * 0.5f + margin) return true;
                    }
                }
            return false;
        }

        // ------------------------------------------------------------------
        // Mobilier urbain sur quelques rues goudronnées (côté opposé aux poteaux électriques)
        // ------------------------------------------------------------------
        private void RoadItems(Rect bounds, List<int> roadsHere, List<Sg> near, List<Vector3> blds, int cx, int cz, List<Obb> placed, GameObject go)
        {
            if (bench == null) return;
            GameObject col = null;
            foreach (int ri in roadsHere)
            {
                ImportedRoad road = map.roads[ri];
                if (!Paved(road) || road.rank < 3 || road.pts.Count < 2) continue;
                // « Quelques goudrons » : environ deux rues goudronnées sur trois
                if (Hash(ri, 17, 3, 1) % 3 == 0) continue;
                float[] cum = Cum(ri);
                float total = cum[cum.Length - 1];
                float poleSide = (Hash(ri, 3, 1, 9) & 1) == 0 ? 1f : -1f;
                float side = -poleSide;
                float off = road.width * 0.5f + 2.45f;
                System.Random rr = new System.Random(Hash(ri, 23, 5, 7));
                for (float sd = 12f + (float)rr.NextDouble() * 10f; sd < total - 5f; sd += 26f + (float)rr.NextDouble() * 22f)
                {
                    Vector2 p, dir;
                    At(ri, cum, sd, out p, out dir);
                    Vector2 nrm = new Vector2(-dir.y, dir.x) * side;
                    Vector2 q = p + nrm * off;
                    if (!bounds.Contains(q)) continue;
                    int k = rr.Next(100);
                    DecorModel m = k < 42 ? bench : (k < 66 ? drum : (k < 78 && road.rank >= 5 ? dumpster : (k < 90 ? jersey : cone)));
                    if (m == null) continue;
                    float ext = Mathf.Max(m.Bounds.extents.x, m.Bounds.extents.z);
                    Vector2 qq = q + nrm * (ext * 0.5f);
                    Obb o = new Obb { C = qq, X = dir, Z = nrm, Hx = m.Bounds.extents.x + 0.1f, Hz = m.Bounds.extents.z + 0.1f };
                    if (!Fits(o, near, blds, 0.3f, cx, cz)) continue;
                    placed.Add(o);
                    // L'objet regarde la rue (sa face avant est vers +Z du fichier)
                    Quaternion rot = Quaternion.LookRotation(new Vector3(-nrm.x, 0f, -nrm.y), Vector3.up);
                    Vector3 pos = new Vector3(qq.x, 0.02f, qq.y) - rot * new Vector3(m.Bounds.center.x, 0f, m.Bounds.center.z);
                    batch.Add(m, Matrix4x4.TRS(pos, rot, Vector3.one));
                    if (col == null) { col = new GameObject("Mobilier"); col.transform.SetParent(go.transform, false); }
                    GameObject b = new GameObject("Objet");
                    b.transform.SetParent(col.transform, false);
                    b.transform.SetPositionAndRotation(new Vector3(qq.x, 0f, qq.y), rot);
                    AddBox(b, new Vector3(0f, m.Bounds.size.y * 0.5f, 0f), new Vector3(m.Bounds.size.x, m.Bounds.size.y, m.Bounds.size.z));
                    Note("mobilier", new Vector3(qq.x, 0f, qq.y), new Vector3(-nrm.x, 0f, -nrm.y));
                }
            }
        }

        // ------------------------------------------------------------------
        // Étals de vendeurs : table, marchandises, parasol ou auvent, cartons
        // ------------------------------------------------------------------
        private static readonly int[] GoodsColors = { 22, 23, 19, 9, 21, 14, 3, 4, 2, 6, 7, 18 };

        private void Stall(MeshData md, long cellKey, Vector2 p2, Vector2 face2, System.Random rnd)
        {
            Vector3 c = new Vector3(p2.x, 0f, p2.y);
            Vector3 f = new Vector3(face2.x, 0f, face2.y).normalized;   // vers le client (la rue)
            Vector3 r = Vector3.Cross(Vector3.up, f), u = Vector3.up;
            float tw = 1.2f + (float)rnd.NextDouble() * 0.5f, td = 0.65f, th = 0.78f;
            int wood = rnd.Next(3) == 0 ? 20 : Trunk;
            // Table : plateau et quatre pieds
            md.Box(c + u * th, r, u, f, new Vector3(tw * 0.5f, 0.025f, td * 0.5f), wood, wood);
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                    md.Box(c + r * (sx * (tw * 0.5f - 0.05f)) + f * (sz * (td * 0.5f - 0.05f)) + u * (th * 0.5f), r, u, f, new Vector3(0.025f, th * 0.5f, 0.025f), wood, wood);
            // Nappe ou pagne qui pend devant
            md.Panel(c + f * (td * 0.5f + 0.012f) + u * (th - 0.18f), r, u, tw * 0.5f, 0.17f, GoodsColors[rnd.Next(GoodsColors.Length)]);
            // Marchandises : tas de fruits, bouteilles, paquets, savons… en petits volumes colorés
            int kind = rnd.Next(4);
            int n = 5 + rnd.Next(5);
            for (int i = 0; i < n; i++)
            {
                float x = ((i + 0.5f) / n - 0.5f) * (tw - 0.2f);
                float z = ((float)rnd.NextDouble() - 0.5f) * (td - 0.2f);
                int col = GoodsColors[(kind * 3 + i) % GoodsColors.Length];
                Vector3 b = c + r * x + f * z + u * (th + 0.025f);
                switch (kind)
                {
                    case 0: md.Octa(b + u * 0.07f, new Vector3(0.09f, 0.07f, 0.09f), col); break;                                   // fruits
                    case 1: md.Box(b + u * 0.12f, r, u, f, new Vector3(0.035f, 0.12f, 0.035f), col, col); break;                   // bouteilles
                    case 2: md.Box(b + u * 0.05f, r, u, f, new Vector3(0.08f, 0.05f, 0.06f), col, col); break;                     // paquets
                    default: md.Box(b + u * 0.03f, r, u, f, new Vector3(0.06f, 0.03f, 0.04f), col, col); md.Octa(b + u * 0.09f, new Vector3(0.05f, 0.04f, 0.05f), col); break;
                }
            }
            // Tabouret du vendeur, derrière la table
            md.Box(c - f * 0.75f + u * 0.22f, r, u, f, new Vector3(0.16f, 0.22f, 0.16f), 15, 15);
            // Parasol coloré ou auvent rayé
            int shade = rnd.Next(3);
            if (shade == 0)
            {
                Vector3 pole = c - f * 0.2f + r * (tw * 0.5f - 0.1f);
                md.Box(pole + u * 1.15f, r, u, f, new Vector3(0.02f, 1.15f, 0.02f), 20, 20);
                md.Octa(c - f * 0.1f + u * 2.25f, new Vector3(1.05f, 0.22f, 1.05f), GoodsColors[rnd.Next(GoodsColors.Length)]);
            }
            else if (shade == 1 && awning != null)
            {
                foreach (float sx in new[] { -1f, 1f }) md.Box(c + r * (sx * (tw * 0.5f + 0.05f)) - f * 0.3f + u * 1.05f, r, u, f, new Vector3(0.025f, 1.05f, 0.025f), 20, 20);
                Quaternion rot = Quaternion.LookRotation(f, Vector3.up);
                float sc = (tw + 0.3f) / Mathf.Max(0.5f, awning.Bounds.size.x);
                batch.Add(awning, Matrix4x4.TRS(c + u * 1.55f - f * 0.05f, rot, new Vector3(sc, 1f, 1f)));
            }
            // Cartons de marchandises par terre
            if (boxes != null && rnd.Next(2) == 0)
            {
                Quaternion rb = Quaternion.LookRotation(f, Vector3.up) * Quaternion.Euler(0f, rnd.Next(60) - 30, 0f);
                batch.Add(boxes, Matrix4x4.TRS(c - f * 0.55f + r * (tw * 0.5f + 0.3f), rb, Vector3.one * 1.6f));
            }
            List<StallSpot> l;
            if (!stallsByCell.TryGetValue(cellKey, out l)) { l = new List<StallSpot>(); stallsByCell[cellKey] = l; }
            l.Add(new StallSpot { P = c - f * 0.72f, Face = f, Seed = rnd.Next() });
            Stalls++;
            Note("etal", c, f);
        }

        /// <summary>2 ou 3 étals aux coins de chaque carrefour de la case.</summary>
        private void CornerStalls(long cellKey, List<Sg> near, List<Vector3> blds, int cx, int cz, List<Obb> placed, MeshData md)
        {
            List<Vector3> js;
            if (!junctionGrid.TryGetValue(cellKey, out js)) return;
            foreach (Vector3 j3 in js)
            {
                Vector2 j = new Vector2(j3.x, j3.z);
                List<Vector4> dirs;
                if (!junctionDirs.TryGetValue(((long)Mathf.RoundToInt(j.x * 4f) << 32) ^ (uint)Mathf.RoundToInt(j.y * 4f), out dirs)) continue;
                // Directions triées autour du carrefour : un coin entre deux rues voisines
                dirs.Sort((a, b) => Mathf.Atan2(a.y, a.x).CompareTo(Mathf.Atan2(b.y, b.x)));
                System.Random rnd = new System.Random(Hash(Mathf.RoundToInt(j.x), Mathf.RoundToInt(j.y), 41, 3));
                int want = 2 + rnd.Next(2), made = 0;
                int startK = rnd.Next(dirs.Count);
                for (int t = 0; t < dirs.Count && made < want; t++)
                {
                    Vector4 a = dirs[(startK + t) % dirs.Count], b = dirs[(startK + t + 1) % dirs.Count];
                    Vector2 da = new Vector2(a.x, a.y), db = new Vector2(b.x, b.y);
                    float ang = Vector2.SignedAngle(da, db);
                    if (ang < 0f) ang += 360f;
                    if (ang < 35f || ang > 200f) continue;
                    Vector2 bis = (da + db);
                    bis = bis.sqrMagnitude > 1e-4f ? bis.normalized : new Vector2(-da.y, da.x);
                    float hw = Mathf.Max(a.z, b.z);
                    float dist = Mathf.Clamp((hw + 2.4f) / Mathf.Max(0.3f, Mathf.Sin(ang * 0.5f * Mathf.Deg2Rad)), 4f, 16f);
                    Vector2 q = j + bis * dist;
                    Obb o = new Obb { C = q, X = new Vector2(-bis.y, bis.x), Z = bis, Hx = 1.2f, Hz = 1.1f };
                    if (!Fits(o, near, blds, 0.5f, cx, cz)) continue;
                    placed.Add(o);
                    Stall(md, cellKey, q, -bis, rnd);
                    made++;
                }
            }
        }

        // ------------------------------------------------------------------
        // Parkings : voitures génériques et motos garées dans une cour ouverte
        // ------------------------------------------------------------------
        private void ParkingParcel(MeshData md, Transform parent, Vector2 c2, Vector2 x2, Vector2 z2, float w, float d, System.Random rnd)
        {
            Vector3 c = new Vector3(c2.x, 0f, c2.y);
            Vector3 r = new Vector3(x2.x, 0f, x2.y), f = new Vector3(z2.x, 0f, z2.y), u = Vector3.up;
            float hw = w * 0.5f, hd = d * 0.5f;
            const float H = 1.2f, T = 0.2f;
            // Sol en béton et lignes de places
            md.Quad(c - r * hw - f * hd + u * 0.035f, c - r * hw + f * hd + u * 0.035f, c + r * hw + f * hd + u * 0.035f, c + r * hw - f * hd + u * 0.035f, Concrete);
            int slots = Mathf.Max(2, Mathf.FloorToInt((w - 1f) / 2.8f));
            float sw = (w - 1f) / slots;
            for (int i = 0; i <= slots; i++)
            {
                Vector3 lp = c - r * (hw - 0.5f - i * sw) + f * (hd - 3f) + u * 0.04f;
                md.Box(lp, r, u, f, new Vector3(0.05f, 0.005f, 2.3f), Cap, Cap);
            }
            // Muret bas sur trois côtés (entrée côté rue)
            int variant = rnd.Next(4);
            FenceWall(md, c + f * hd, r, f, hw, H, T, variant, -1, false, 0f);
            FenceWall(md, c - r * hw, f, -r, hd, H, T, variant, -1, false, 0.3f);
            FenceWall(md, c + r * hw, f, r, hd, H, T, variant, -1, false, 0.6f);
            GameObject col = ParcelCollider(parent, c, f);
            AddBox(col, new Vector3(0f, H * 0.5f, hd), new Vector3(w, H, T));
            AddBox(col, new Vector3(-hw, H * 0.5f, 0f), new Vector3(T, H, d));
            AddBox(col, new Vector3(hw, H * 0.5f, 0f), new Vector3(T, H, d));
            Note("parking", c, -f);
            for (int i = 0; i < slots; i++)
            {
                if (rnd.Next(100) < 22) continue;   // places libres
                Vector3 sc = c - r * (hw - 0.5f - (i + 0.5f) * sw) + f * (hd - 3f);
                bool moto = parkedMotos.Count > 0 && (parkedCars.Count == 0 || rnd.Next(100) < 35);
                // Garé en marche avant (nez vers le fond) ou en marche arrière (nez vers la rue)
                Vector3 nose = rnd.Next(3) == 0 ? -f : f;
                Quaternion rot = Quaternion.LookRotation(nose, Vector3.up);
                if (moto)
                {
                    DecorModel m = parkedMotos[rnd.Next(parkedMotos.Count)];
                    int pi = rnd.Next(64);
                    for (int k = 0; k < 2; k++)
                    {
                        Vector3 mp = sc + r * ((k - 0.5f) * 1.0f);
                        if (k == 1 && rnd.Next(2) == 0) break;
                        batch.Add(m, Matrix4x4.TRS(mp, rot * Quaternion.Euler(0f, 0f, 5f), Vector3.one), VehicleBuilder.ParkedMotoPaint(pi + k));
                        AddBox(col, col.transform.InverseTransformPoint(mp) + new Vector3(0f, 0.55f, 0f), new Vector3(0.6f, 1.1f, 2.0f));
                        ParkedVehicles++;
                    }
                }
                else
                {
                    DecorModel m = parkedCars[rnd.Next(parkedCars.Count)];
                    batch.Add(m, Matrix4x4.TRS(sc, rot, Vector3.one), VehicleBuilder.ParkedPaint(rnd.Next(64)));
                    Vector3 lc = col.transform.InverseTransformPoint(sc);
                    AddBox(col, lc + new Vector3(0f, m.Bounds.size.y * 0.5f + 0.1f, 0f), new Vector3(Mathf.Min(m.Bounds.size.x, sw - 0.2f), m.Bounds.size.y - 0.2f, m.Bounds.size.z));
                    ParkedVehicles++;
                }
            }
        }

        // ------------------------------------------------------------------
        // Requêtes (foule : vendeurs aux étals)
        // ------------------------------------------------------------------
        /// <summary>Étals construits à moins de « radius » mètres du point.</summary>
        public static void StallsNear(Vector3 p, float radius, List<StallSpot> result)
        {
            result.Clear();
            if (Instance == null) return;
            int cx = Mathf.FloorToInt(p.x / CellSize), cz = Mathf.FloorToInt(p.z / CellSize);
            int k = Mathf.CeilToInt(radius / CellSize);
            for (int dz = -k; dz <= k; dz++)
                for (int dx = -k; dx <= k; dx++)
                {
                    List<StallSpot> l;
                    if (!Instance.stallsByCell.TryGetValue(Key(cx + dx, cz + dz), out l)) continue;
                    foreach (StallSpot s in l) if ((s.P - p).sqrMagnitude < radius * radius) result.Add(s);
                }
        }

        /// <summary>Distance du point au bord de la chaussée la plus proche (négative sur la chaussée), direction qui
        /// s'en éloigne et revêtement de cette rue (goudron ou terre). Faux si aucune rue à moins de 30 m.</summary>
        public static bool NearestCarriageway(Vector3 p, out float edgeDistance, out Vector3 away, out bool paved)
        {
            edgeDistance = 999f;
            away = Vector3.zero;
            paved = false;
            if (Instance == null || Instance.map == null) return false;
            CityFill cf = Instance;
            Vector2 q = new Vector2(p.x, p.z);
            int cx = Mathf.FloorToInt(q.x / CellSize), cz = Mathf.FloorToInt(q.y / CellSize);
            float best = 30f;
            bool found = false;
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    List<Seg> l;
                    if (!cf.roadGrid.TryGetValue(Key(cx + dx, cz + dz), out l)) continue;
                    foreach (Seg s in l)
                    {
                        ImportedRoad rd = cf.map.roads[s.Road];
                        if (rd.rank < 2) continue;
                        Vector2 a = rd.pts[s.Index], b = rd.pts[s.Index + 1], ab = b - a;
                        float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(q - a, ab) / ab.sqrMagnitude) : 0f;
                        Vector2 c = a + ab * t;
                        float d = Vector2.Distance(q, c) - rd.width * 0.5f;
                        if (d >= best) continue;
                        best = d;
                        found = true;
                        Vector2 aw = q - c;
                        if (aw.sqrMagnitude < 1e-4f) aw = new Vector2(-ab.y, ab.x);
                        aw.Normalize();
                        away = new Vector3(aw.x, 0f, aw.y);
                        paved = Paved(rd);
                    }
                }
            edgeDistance = best;
            return found;
        }
    }
}
