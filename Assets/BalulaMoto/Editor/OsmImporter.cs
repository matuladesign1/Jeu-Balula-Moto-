// BALULA MOTO — import d'une carte OpenStreetMap (.osm) : routes (largeur et revêtement), bâtiments,
// eau et espaces verts, arrêts, stations, lieux nommés, boucles de circulation.
// Les avenues ne sont jamais déplacées ni « embellies » : la géométrie OSM est reprise telle quelle.
// Données © les contributeurs d'OpenStreetMap, licence ODbL : l'attribution est affichée dans les crédits du jeu.
#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using BalulaMoto.Core;
using BalulaMoto.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace BalulaMoto.EditorTools
{
    // =====================================================================
    // Lecture du fichier .osm (XML)
    // =====================================================================
    public class OsmNode { public double Lat, Lon; public Dictionary<string, string> Tags; public Vector2 P; }
    public class OsmWay { public long Id; public readonly List<long> Refs = new List<long>(); public readonly Dictionary<string, string> Tags = new Dictionary<string, string>(); }
    public class OsmMember { public string Type, Role; public long Ref; }
    public class OsmRelation { public long Id; public readonly List<OsmMember> Members = new List<OsmMember>(); public readonly Dictionary<string, string> Tags = new Dictionary<string, string>(); }

    public class OsmData
    {
        public readonly Dictionary<long, OsmNode> Nodes = new Dictionary<long, OsmNode>();
        public readonly List<OsmWay> Ways = new List<OsmWay>();
        public readonly Dictionary<long, OsmWay> WayById = new Dictionary<long, OsmWay>();
        public readonly List<OsmRelation> Relations = new List<OsmRelation>();
    }

    public static class OsmParser
    {
        public static OsmData Parse(string path)
        {
            OsmData d = new OsmData();
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.IgnoreComments = true;
            settings.IgnoreWhitespace = true;
            settings.DtdProcessing = DtdProcessing.Ignore;
            using (XmlReader r = XmlReader.Create(path, settings))
            {
                OsmNode node = null;
                OsmWay way = null;
                OsmRelation rel = null;
                while (r.Read())
                {
                    if (r.NodeType == XmlNodeType.Element)
                    {
                        bool empty = r.IsEmptyElement;
                        switch (r.Name)
                        {
                            case "node":
                                node = new OsmNode { Lat = Num(r.GetAttribute("lat")), Lon = Num(r.GetAttribute("lon")) };
                                d.Nodes[Id(r.GetAttribute("id"))] = node;
                                way = null; rel = null;
                                if (empty) node = null;
                                break;
                            case "way":
                                way = new OsmWay { Id = Id(r.GetAttribute("id")) };
                                d.Ways.Add(way);
                                d.WayById[way.Id] = way;
                                node = null; rel = null;
                                if (empty) way = null;
                                break;
                            case "relation":
                                rel = new OsmRelation { Id = Id(r.GetAttribute("id")) };
                                d.Relations.Add(rel);
                                node = null; way = null;
                                if (empty) rel = null;
                                break;
                            case "nd":
                                if (way != null) way.Refs.Add(Id(r.GetAttribute("ref")));
                                break;
                            case "member":
                                if (rel != null) rel.Members.Add(new OsmMember { Type = r.GetAttribute("type"), Ref = Id(r.GetAttribute("ref")), Role = r.GetAttribute("role") ?? "" });
                                break;
                            case "tag":
                                string k = r.GetAttribute("k"), v = r.GetAttribute("v") ?? "";
                                if (k == null) break;
                                if (node != null) { if (node.Tags == null) node.Tags = new Dictionary<string, string>(); node.Tags[k] = v; }
                                else if (way != null) way.Tags[k] = v;
                                else if (rel != null) rel.Tags[k] = v;
                                break;
                        }
                    }
                    else if (r.NodeType == XmlNodeType.EndElement)
                    {
                        if (r.Name == "node") node = null;
                        else if (r.Name == "way") way = null;
                        else if (r.Name == "relation") rel = null;
                    }
                }
            }
            return d;
        }

        private static double Num(string s) { return string.IsNullOrEmpty(s) ? 0.0 : double.Parse(s, CultureInfo.InvariantCulture); }
        private static long Id(string s) { return string.IsNullOrEmpty(s) ? 0L : long.Parse(s, CultureInfo.InvariantCulture); }
    }

    // =====================================================================
    // Triangulation de polygones simples (oreilles)
    // =====================================================================
    public static class Triangulator
    {
        /// <summary>Retourne des triangles orientés pour une face visible du dessus (plan XZ, normale +Y).</summary>
        public static List<int> Triangulate(IList<Vector2> pts)
        {
            List<int> result = new List<int>();
            int n = pts.Count;
            if (n < 3) return result;
            List<int> V = new List<int>(n);
            if (Area(pts) > 0f) for (int i = 0; i < n; i++) V.Add(i);
            else for (int i = n - 1; i >= 0; i--) V.Add(i);
            int nv = n, count = 2 * nv, v = nv - 1;
            while (nv > 2)
            {
                if (count-- <= 0) break;
                int u = v; if (nv <= u) u = 0;
                v = u + 1; if (nv <= v) v = 0;
                int w = v + 1; if (nv <= w) w = 0;
                if (Snip(pts, u, v, w, nv, V))
                {
                    result.Add(V[u]); result.Add(V[w]); result.Add(V[v]);
                    V.RemoveAt(v);
                    nv--;
                    count = 2 * nv;
                }
            }
            return result;
        }

        public static float Area(IList<Vector2> p)
        {
            float a = 0f;
            for (int i = 0, j = p.Count - 1; i < p.Count; j = i++) a += p[j].x * p[i].y - p[i].x * p[j].y;
            return a * 0.5f;
        }

        private static bool Snip(IList<Vector2> p, int u, int v, int w, int n, List<int> V)
        {
            Vector2 A = p[V[u]], B = p[V[v]], C = p[V[w]];
            if (1e-7f > (B.x - A.x) * (C.y - A.y) - (B.y - A.y) * (C.x - A.x)) return false;
            for (int k = 0; k < n; k++)
            {
                if (k == u || k == v || k == w) continue;
                if (Inside(A, B, C, p[V[k]])) return false;
            }
            return true;
        }

        private static bool Inside(Vector2 A, Vector2 B, Vector2 C, Vector2 P)
        {
            float c1 = (C.x - B.x) * (P.y - B.y) - (C.y - B.y) * (P.x - B.x);
            float c2 = (A.x - C.x) * (P.y - C.y) - (A.y - C.y) * (P.x - C.x);
            float c3 = (B.x - A.x) * (P.y - A.y) - (B.y - A.y) * (P.x - A.x);
            return c1 >= 0f && c2 >= 0f && c3 >= 0f;
        }
    }

    // =====================================================================
    // Accumulateur de maillage par lot (zone × matériau)
    // =====================================================================
    public class MeshBatch
    {
        public readonly List<Vector3> V = new List<Vector3>();
        public readonly List<int> T = new List<int>();
        public readonly List<Vector2> UV = new List<Vector2>();

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int i = V.Count;
            V.Add(a); V.Add(b); V.Add(c); V.Add(d);
            UV.Add(new Vector2(0, 0)); UV.Add(new Vector2(0, 1)); UV.Add(new Vector2(1, 1)); UV.Add(new Vector2(1, 0));
            T.Add(i); T.Add(i + 1); T.Add(i + 2);
            T.Add(i); T.Add(i + 2); T.Add(i + 3);
        }

        public void Polygon(IList<Vector2> pts, float y)
        {
            List<int> tri = Triangulator.Triangulate(pts);
            int b = V.Count;
            foreach (Vector2 p in pts) { V.Add(new Vector3(p.x, y, p.y)); UV.Add(p * 0.1f); }
            for (int i = 0; i < tri.Count; i++) T.Add(b + tri[i]);
        }

        public Mesh Build(string name)
        {
            Mesh m = new Mesh { name = name };
            if (V.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(V);
            m.SetUVs(0, UV);
            m.SetTriangles(T, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }

    // =====================================================================
    // Fenêtre d'import
    // =====================================================================
    public class OsmImporterWindow : EditorWindow
    {
        private string osmPath = "";
        private string worldName = "Kinshasa";
        private double originLat = -4.32194, originLon = 15.31194;
        private float radius = 2500f;
        private float chunk = 400f;
        private bool buildings = true, kinshasaDirtHeuristic = true;
        private int maxBuildings = 8000;
        private Vector2 scroll;

        [MenuItem("BALULA MOTO/4. Importer une carte OpenStreetMap…", priority = 20)]
        public static void Open() { GetWindow<OsmImporterWindow>("Import OSM").minSize = new Vector2(460, 520); }

        /// <summary>Import sans fenêtre (compilation automatique) : la carte est ajoutée à la scène ouverte.</summary>
        public static string ImportBatch(string path, string name, double lat, double lon, float importRadius, int buildingLimit)
        {
            OsmImporterWindow w = CreateInstance<OsmImporterWindow>();
            try
            {
                w.osmPath = path;
                w.worldName = name;
                w.originLat = lat;
                w.originLon = lon;
                w.radius = importRadius;
                w.maxBuildings = buildingLimit;
                w.batch = true;
                w.Import();
                return w.lastReport;
            }
            finally { DestroyImmediate(w); }
        }

        private bool batch;
        private string lastReport = "";

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.HelpBox(
                "1. Sur openstreetmap.org, zoomez sur le quartier voulu (ex. Gombe, Kalamu, Victoire), cliquez « Exporter », puis téléchargez le fichier .osm.\n" +
                "   Pour une zone plus grande, utilisez un extrait OSM (Geofabrik, BBBike) découpé au format .osm.\n" +
                "2. Choisissez le fichier, vérifiez le point d'origine (centre de la carte) et le rayon, puis « Importer ».\n" +
                "3. Enregistrez la scène : le jeu utilisera cette carte à la place de la piste d'essai.\n\n" +
                "Licence : © les contributeurs d'OpenStreetMap (ODbL). Ne pas utiliser d'images satellites protégées.", MessageType.Info);
            EditorGUILayout.BeginHorizontal();
            osmPath = EditorGUILayout.TextField("Fichier .osm", osmPath);
            if (GUILayout.Button("…", GUILayout.Width(30)))
            {
                string p = EditorUtility.OpenFilePanel("Fichier OpenStreetMap", "", "osm");
                if (!string.IsNullOrEmpty(p)) osmPath = p;
            }
            EditorGUILayout.EndHorizontal();
            worldName = EditorGUILayout.TextField("Nom de la carte", worldName);
            originLat = EditorGUILayout.DoubleField("Origine — latitude", originLat);
            originLon = EditorGUILayout.DoubleField("Origine — longitude", originLon);
            radius = EditorGUILayout.Slider("Rayon importé (m)", radius, 300f, 8000f);
            chunk = EditorGUILayout.Slider("Taille des blocs (m)", chunk, 150f, 1000f);
            buildings = EditorGUILayout.Toggle("Bâtiments", buildings);
            maxBuildings = EditorGUILayout.IntSlider("Bâtiments max", maxBuildings, 500, 30000);
            kinshasaDirtHeuristic = EditorGUILayout.Toggle(new GUIContent("Rues sans revêtement = terre", "À Kinshasa, beaucoup de rues résidentielles sans étiquette « surface » sont en terre. Désactivez si la carte est mieux renseignée."), kinshasaDirtHeuristic);
            EditorGUILayout.HelpBox("Conseil téléphone : un rayon de 1,5 à 3 km reste fluide. Au-delà, prévoyez des zones séparées.", MessageType.None);
            GUI.enabled = File.Exists(osmPath);
            if (GUILayout.Button("Importer dans la scène", GUILayout.Height(36))) Import();
            GUI.enabled = true;
            EditorGUILayout.EndScrollView();
        }

        // ------------------------------------------------------------------
        private static readonly Dictionary<string, float> RoadWidth = new Dictionary<string, float>
        {
            { "motorway", 14f }, { "trunk", 12f }, { "primary", 11f }, { "secondary", 9f }, { "tertiary", 8f },
            { "unclassified", 6.5f }, { "residential", 6f }, { "living_street", 5f }, { "service", 4f }, { "track", 3.5f },
            { "road", 6f }, { "motorway_link", 7f }, { "trunk_link", 7f }, { "primary_link", 7f }, { "secondary_link", 6.5f }, { "tertiary_link", 6f }
        };

        private static readonly Dictionary<string, int> RoadRank = new Dictionary<string, int>
        {
            { "motorway", 9 }, { "trunk", 8 }, { "primary", 7 }, { "secondary", 6 }, { "tertiary", 5 }, { "unclassified", 3 },
            { "residential", 3 }, { "living_street", 2 }, { "service", 1 }, { "track", 1 }, { "road", 2 }
        };

        private static readonly Color[] WallColors =
        {
            new Color(0.92f, 0.88f, 0.76f), new Color(0.78f, 0.84f, 0.9f), new Color(0.95f, 0.76f, 0.62f),
            new Color(0.9f, 0.9f, 0.88f), new Color(0.98f, 0.9f, 0.6f), new Color(0.75f, 0.85f, 0.7f)
        };

        private void Import()
        {
            try
            {
                EditorUtility.DisplayProgressBar("Import OSM", "Lecture du fichier…", 0.05f);
                OsmData d = OsmParser.Parse(osmPath);
                foreach (OsmNode n in d.Nodes.Values)
                {
                    double x, z;
                    GeoProjection.ToLocal(n.Lat, n.Lon, originLat, originLon, out x, out z);
                    n.P = new Vector2((float)x, (float)z);
                }
                EditorUtility.DisplayProgressBar("Import OSM", "Construction de la carte…", 0.3f);
                Build(d);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                lastReport = "Erreur d'import OSM : " + e.Message;
                if (!batch) EditorUtility.DisplayDialog("Import OSM", "Erreur : " + e.Message, "OK");
            }
            finally { EditorUtility.ClearProgressBar(); }
        }

        private bool InRange(Vector2 p) { return p.sqrMagnitude <= radius * radius; }

        private List<Vector2> Points(OsmData d, OsmWay w, out bool complete)
        {
            List<Vector2> pts = new List<Vector2>(w.Refs.Count);
            complete = true;
            foreach (long id in w.Refs)
            {
                OsmNode n;
                if (d.Nodes.TryGetValue(id, out n)) pts.Add(n.P);
                else complete = false;
            }
            return pts;
        }

        private Vector2Int Cell(Vector2 p) { return new Vector2Int(Mathf.FloorToInt(p.x / chunk), Mathf.FloorToInt(p.y / chunk)); }

        private static MeshBatch Batch(Dictionary<string, MeshBatch> map, string key)
        {
            MeshBatch b;
            if (!map.TryGetValue(key, out b)) { b = new MeshBatch(); map[key] = b; }
            return b;
        }

        private void Build(OsmData d)
        {
            Dictionary<string, MeshBatch> roads = new Dictionary<string, MeshBatch>();
            Dictionary<string, MeshBatch> houses = new Dictionary<string, MeshBatch>();
            Dictionary<string, MeshBatch> areas = new Dictionary<string, MeshBatch>();
            List<ImportedPlace> places = new List<ImportedPlace>();
            List<ImportedPath> paths = new List<ImportedPath>();
            StringBuilder report = new StringBuilder();
            int roadCount = 0, buildingCount = 0, areaCount = 0;
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            List<KeyValuePair<float, OsmWay>> majors = new List<KeyValuePair<float, OsmWay>>();
            List<ImportedPath> walks = new List<ImportedPath>();
            int walkPoints = 0;
            Vector3 spawn = Vector3.zero;
            float spawnHeading = 0f, bestSpawn = float.MaxValue;

            // ---------------- Routes ----------------
            foreach (OsmWay w in d.Ways)
            {
                string hw;
                if (!w.Tags.TryGetValue("highway", out hw) || !RoadWidth.ContainsKey(hw)) continue;
                bool complete;
                List<Vector2> pts = Points(d, w, out complete);
                if (pts.Count < 2) continue;
                bool any = false;
                foreach (Vector2 p in pts) if (InRange(p)) { any = true; break; }
                if (!any) continue;
                float width = RoadWidth[hw];
                string lanes;
                int nl;
                if (w.Tags.TryGetValue("lanes", out lanes) && int.TryParse(lanes, out nl) && nl > 0) width = Mathf.Max(width, nl * 3.2f + 1f);
                SurfaceKind surface = Surface(w, hw);
                int rank;
                if (!RoadRank.TryGetValue(hw.Replace("_link", ""), out rank)) rank = 2;
                float y = 0.03f + rank * 0.004f;
                for (int i = 0; i < pts.Count - 1; i++)
                {
                    Vector2 a = pts[i], b = pts[i + 1];
                    if (!InRange(a) && !InRange(b)) continue;
                    Vector2 dir = b - a;
                    float len = dir.magnitude;
                    if (len < 0.05f) continue;
                    dir /= len;
                    Vector2 left = new Vector2(-dir.y, dir.x) * (width * 0.5f);
                    Vector2 a2 = i > 0 ? a - dir * Mathf.Min(width * 0.5f, len * 0.5f) : a;
                    Vector2 b2 = i < pts.Count - 2 ? b + dir * Mathf.Min(width * 0.5f, len * 0.5f) : b;
                    Vector2Int c = Cell((a + b) * 0.5f);
                    MeshBatch mb = Batch(roads, c.x + "_" + c.y + "_" + surface);
                    mb.Quad(V3(a2 + left, y), V3(b2 + left, y), V3(b2 - left, y), V3(a2 - left, y));
                    min = Vector2.Min(min, Vector2.Min(a, b));
                    max = Vector2.Max(max, Vector2.Max(a, b));
                    if (rank >= 3 && a.sqrMagnitude < bestSpawn)
                    {
                        bestSpawn = a.sqrMagnitude;
                        Vector2 right = -left.normalized * Mathf.Min(1.8f, width * 0.25f);
                        spawn = V3(a + right + dir * 2f, y + 0.6f);
                        spawnHeading = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
                    }
                }
                roadCount++;
                // Trottoirs des deux côtés (rues ordinaires et avenues ; pas d'autoroutes ni de pistes)
                if (rank >= 2 && rank <= 8 && hw != "service" && hw != "track" && walkPoints < 70000)
                {
                    List<Vector2> run = new List<Vector2>();
                    for (int i = 0; i <= pts.Count; i++)
                    {
                        bool inside = i < pts.Count && InRange(pts[i]);
                        if (inside) run.Add(pts[i]);
                        if ((!inside || i == pts.Count) && run.Count >= 2)
                        {
                            foreach (float side in new[] { 1f, -1f })
                            {
                                ImportedPath wp = new ImportedPath { name = "Trottoir" };
                                AddLane(wp.points, run, side * (width * 0.5f + 1.3f));
                                for (int k = 0; k < wp.points.Count; k++) wp.points[k] = new Vector3(wp.points[k].x, 0f, wp.points[k].z);
                                if (wp.points.Count >= 2) { walks.Add(wp); walkPoints += wp.points.Count; }
                            }
                        }
                        if (!inside) run.Clear();
                    }
                }
                if (rank >= 5)
                {
                    float total = 0f;
                    for (int i = 0; i < pts.Count - 1; i++) total += Vector2.Distance(pts[i], pts[i + 1]);
                    if (total > 300f) majors.Add(new KeyValuePair<float, OsmWay>(total, w));
                }
            }

            // ---------------- Boucles de circulation (aller-retour sur les grandes avenues) ----------------
            majors.Sort((p, q) => q.Key.CompareTo(p.Key));
            for (int k = 0; k < majors.Count && paths.Count < 14; k++)
            {
                bool complete;
                List<Vector2> pts = Points(d, majors[k].Value, out complete);
                List<Vector2> inside = pts.FindAll(InRange);
                if (inside.Count < 2) continue;
                ImportedPath ip = new ImportedPath { name = Name(majors[k].Value.Tags, "Avenue " + majors[k].Value.Id) };
                AddLane(ip.points, inside, 1.8f);
                inside.Reverse();
                AddLane(ip.points, inside, 1.8f);
                paths.Add(ip);
            }

            // ---------------- Bâtiments ----------------
            if (buildings)
            {
                System.Random rnd = new System.Random(243);
                // Les bâtiments les plus proches du centre d'abord (la limite garde le cœur de la carte complet)
                List<KeyValuePair<float, OsmWay>> byDistance = new List<KeyValuePair<float, OsmWay>>();
                foreach (OsmWay w in d.Ways)
                {
                    if (!w.Tags.ContainsKey("building") || w.Refs.Count < 4) continue;
                    OsmNode n0;
                    if (!d.Nodes.TryGetValue(w.Refs[0], out n0)) continue;
                    byDistance.Add(new KeyValuePair<float, OsmWay>(n0.P.sqrMagnitude, w));
                }
                byDistance.Sort((p, q) => p.Key.CompareTo(q.Key));
                foreach (KeyValuePair<float, OsmWay> kvb in byDistance)
                {
                    OsmWay w = kvb.Value;
                    if (buildingCount >= maxBuildings) break;
                    bool complete;
                    List<Vector2> pts = Points(d, w, out complete);
                    if (!complete || pts.Count < 4) continue;
                    if (pts[0] == pts[pts.Count - 1]) pts.RemoveAt(pts.Count - 1);
                    Vector2 center = Vector2.zero;
                    foreach (Vector2 p in pts) center += p;
                    center /= pts.Count;
                    if (!InRange(center)) continue;
                    if (Triangulator.Area(pts) < 0f) pts.Reverse();
                    float h = Height(w, rnd);
                    Vector2Int c = Cell(center);
                    int color = (int)(Mathf.Abs((float)(w.Id % 997)) % WallColors.Length);
                    MeshBatch walls = Batch(houses, c.x + "_" + c.y + "_mur" + color);
                    for (int i = 0; i < pts.Count; i++)
                    {
                        Vector2 a = pts[i], b = pts[(i + 1) % pts.Count];
                        walls.Quad(V3(a, 0f), V3(a, h), V3(b, h), V3(b, 0f));
                    }
                    Batch(houses, c.x + "_" + c.y + "_toit").Polygon(pts, h);
                    buildingCount++;
                    string bname = Name(w.Tags, null);
                    if (bname != null) places.Add(Place("osm-b" + w.Id, bname, "batiment", center, 2, false, false));
                }
            }

            // ---------------- Eau, verdure ----------------
            foreach (OsmWay w in d.Ways) AddArea(d, w, w.Tags, areas, ref areaCount);
            foreach (OsmRelation rel in d.Relations)
            {
                if (!IsArea(rel.Tags)) continue;
                foreach (OsmMember m in rel.Members)
                {
                    OsmWay w;
                    if (m.Type == "way" && m.Role == "outer" && d.WayById.TryGetValue(m.Ref, out w)) AddArea(d, w, rel.Tags, areas, ref areaCount);
                }
            }

            // ---------------- Points d'intérêt ----------------
            List<KeyValuePair<string, Vector2>> districts = new List<KeyValuePair<string, Vector2>>();
            foreach (KeyValuePair<long, OsmNode> kv in d.Nodes)
            {
                OsmNode n = kv.Value;
                if (n.Tags == null || !InRange(n.P)) continue;
                string place;
                string name = Name(n.Tags, null);
                if (name != null && n.Tags.TryGetValue("place", out place) && (place == "suburb" || place == "neighbourhood" || place == "quarter" || place == "town"))
                    districts.Add(new KeyValuePair<string, Vector2>(name, n.P));
            }
            foreach (KeyValuePair<long, OsmNode> kv in d.Nodes)
            {
                OsmNode n = kv.Value;
                if (n.Tags == null || !InRange(n.P)) continue;
                string v;
                string name = Name(n.Tags, null);
                bool stop = (n.Tags.TryGetValue("highway", out v) && v == "bus_stop") || (n.Tags.TryGetValue("public_transport", out v) && (v == "platform" || v == "stop_position"));
                bool fuel = n.Tags.TryGetValue("amenity", out v) && v == "fuel";
                string cat = Category(n.Tags);
                if (!stop && !fuel && (cat == null || name == null)) continue;
                int weight = cat == "marche" ? 10 : (cat == "gare" ? 8 : (cat == "sante" || cat == "commerce" ? 6 : (cat == "ecole" || cat == "stade" ? 5 : 3)));
                places.Add(Place("osm-n" + kv.Key, name ?? (stop ? "Arrêt" : "Station-service"), cat ?? (stop ? "arret" : "carburant"), n.P, weight, stop, fuel));
            }
            foreach (ImportedPlace p in places)
            {
                float best = 3000f * 3000f;
                p.district = worldName;
                foreach (KeyValuePair<string, Vector2> dk in districts)
                {
                    float dd = (dk.Value - new Vector2(p.position.x, p.position.z)).sqrMagnitude;
                    if (dd < best) { best = dd; p.district = dk.Key; }
                }
            }

            // ---------------- Lieux de référence du créateur (prioritaires, jamais modifiés) ----------------
            List<string> refsReport = new List<string>();
            List<ImportedPlace> suggested = new List<ImportedPlace>();
            GameDatabase db = DataLoader.LoadAll(new List<string>());
            foreach (LocationDef loc in db.Locations)
            {
                if (loc.HasCoordinates)
                {
                    double x, z;
                    GeoProjection.ToLocal(loc.lat, loc.lon, originLat, originLon, out x, out z);
                    Vector2 p = new Vector2((float)x, (float)z);
                    if (!InRange(p)) continue;
                    ImportedPlace ip = Place(loc.id, loc.nom, "repere", p, Mathf.Max(3, loc.poids), loc.arret, false);
                    ip.district = string.IsNullOrEmpty(loc.commune) ? worldName : loc.commune;
                    ip.isLandmark = true;
                    ip.status = loc.statutCoordonnees;
                    places.Add(ip);
                    refsReport.Add("• " + loc.nom + " : position du créateur (" + loc.statutCoordonnees + ")");
                }
                else if (!string.IsNullOrEmpty(loc.rechercheOsm))
                {
                    string[] terms = loc.rechercheOsm.Split('|');
                    string found = null;
                    ImportedPlace foundPlace = null;
                    foreach (ImportedPlace ip in places)
                    {
                        string nn = TextUtil.Normalize(ip.name);
                        foreach (string t in terms)
                        {
                            string q = TextUtil.Normalize(t.Trim());
                            if (q.Length > 3 && nn.Contains(q))
                            {
                                double lat, lon;
                                GeoProjection.ToLatLon(ip.position.x, ip.position.z, originLat, originLon, out lat, out lon);
                                found = ip.name + " (" + lat.ToString("0.00000", CultureInfo.InvariantCulture) + ", " + lon.ToString("0.00000", CultureInfo.InvariantCulture) + ")";
                                foundPlace = ip;
                                break;
                            }
                        }
                        if (found != null) break;
                    }
                    refsReport.Add("• " + loc.nom + " : coordonnées manquantes — " + (found != null ? "SUGGESTION OSM à confirmer : " + found : "aucune correspondance OSM"));
                    if (foundPlace != null)
                    {
                        // Repère affiché à l'emplacement OSM correspondant au nom, clairement marqué « à vérifier »
                        ImportedPlace ip = Place(loc.id, loc.nom, "repere", new Vector2(foundPlace.position.x, foundPlace.position.z), Mathf.Max(3, loc.poids), loc.arret, false);
                        ip.district = string.IsNullOrEmpty(loc.commune) ? worldName : loc.commune;
                        ip.isLandmark = true;
                        ip.status = "Position proposée par OpenStreetMap (« " + foundPlace.name + " ») — à vérifier par le créateur";
                        suggested.Add(ip);
                    }
                }
            }

            places.AddRange(suggested);

            // ---------------- Scène ----------------
            string safe = Safe(worldName);
            string folder = BalulaSetup.Root + "/Generated/" + safe;
            if (AssetDatabase.IsValidFolder(folder)) AssetDatabase.DeleteAsset(folder);
            BalulaSetup.EnsureFolder(folder);
            string meshAsset = folder + "/Maillages.asset";
            string matAsset = folder;

            GameObject old = GameObject.Find("Carte_" + safe);
            if (old != null) Undo.DestroyObjectImmediate(old);
            GameObject root = new GameObject("Carte_" + safe);
            Undo.RegisterCreatedObjectUndo(root, "Import OSM");

            Material litBase = AssetDatabase.LoadAssetAtPath<Material>(BalulaSetup.MaterialsFolder + "/MatLit.mat");
            Shader shader = litBase != null ? litBase.shader : Shader.Find(GraphicsSettings.currentRenderPipeline != null ? "Universal Render Pipeline/Lit" : "Standard");
            Dictionary<string, Material> mats = new Dictionary<string, Material>();
            bool firstMesh = true, firstMat = true;

            // Sol (terre) sous toute la carte
            Vector2 size = (max - min);
            if (size.x <= 0f || size.y <= 0f) { min = new Vector2(-radius, -radius); max = new Vector2(radius, radius); size = max - min; }
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Sol";
            ground.transform.SetParent(root.transform, false);
            Vector2 mid = (min + max) * 0.5f;
            ground.transform.localPosition = new Vector3(mid.x, -0.25f, mid.y);
            ground.transform.localScale = new Vector3(size.x + 400f, 0.5f, size.y + 400f);
            ground.GetComponent<Renderer>().sharedMaterial = Mat(mats, "Sol", shader, new Color(0.6f, 0.47f, 0.33f), matAsset, ref firstMat);
            ground.AddComponent<SurfaceZone>().Kind = SurfaceKind.Terre;

            int idx = 0;
            foreach (KeyValuePair<string, MeshBatch> kv in roads)
            {
                string[] parts = kv.Key.Split('_');
                SurfaceKind s = (SurfaceKind)System.Enum.Parse(typeof(SurfaceKind), parts[2]);
                GameObject g = Emit(root, "Routes_" + kv.Key, kv.Value, Mat(mats, "Route" + s, shader, SurfaceColor(s), matAsset, ref firstMat), meshAsset, ref firstMesh, true, ref idx);
                g.AddComponent<SurfaceZone>().Kind = s;
            }
            foreach (KeyValuePair<string, MeshBatch> kv in houses)
            {
                bool roof = kv.Key.EndsWith("toit");
                Color c = roof ? new Color(0.55f, 0.32f, 0.2f) : WallColors[int.Parse(kv.Key.Substring(kv.Key.LastIndexOf("mur") + 3))];
                Emit(root, "Batiments_" + kv.Key, kv.Value, Mat(mats, roof ? "Toit" : "Mur" + kv.Key.Substring(kv.Key.LastIndexOf("mur") + 3), shader, c, matAsset, ref firstMat), meshAsset, ref firstMesh, !roof, ref idx);
            }
            foreach (KeyValuePair<string, MeshBatch> kv in areas)
            {
                bool water = kv.Key.EndsWith("eau");
                Emit(root, (water ? "Eau_" : "Verdure_") + kv.Key, kv.Value, Mat(mats, water ? "Eau" : "Verdure", shader, water ? new Color(0.25f, 0.36f, 0.34f) : new Color(0.3f, 0.48f, 0.2f), matAsset, ref firstMat), meshAsset, ref firstMesh, false, ref idx);
            }

            ImportedWorld iw = root.AddComponent<ImportedWorld>();
            iw.worldName = worldName;
            iw.originLat = originLat;
            iw.originLon = originLon;
            iw.spawnPosition = bestSpawn < float.MaxValue ? spawn : new Vector3(0f, 0.6f, 0f);
            iw.spawnHeading = spawnHeading;
            iw.halfSize = new Vector2(Mathf.Max(Mathf.Abs(min.x), Mathf.Abs(max.x)), Mathf.Max(Mathf.Abs(min.y), Mathf.Abs(max.y)));
            iw.places = places;
            iw.trafficPaths = paths;
            iw.walkPaths = walks;
            report.Append("Import ").Append(System.DateTime.Now.ToString("yyyy-MM-dd HH:mm")).Append(" — ").Append(Path.GetFileName(osmPath)).Append('\n');
            report.Append("Origine ").Append(originLat.ToString(CultureInfo.InvariantCulture)).Append(", ").Append(originLon.ToString(CultureInfo.InvariantCulture)).Append(" · rayon ").Append(radius.ToString("0")).Append(" m\n");
            report.Append(roadCount).Append(" routes, ").Append(buildingCount).Append(" bâtiments, ").Append(areaCount).Append(" surfaces (eau/verdure), ").Append(places.Count).Append(" lieux, ").Append(paths.Count).Append(" boucles de circulation, ").Append(walks.Count).Append(" trottoirs.\n");
            report.Append("Lieux de référence :\n").Append(string.Join("\n", refsReport.ToArray()));
            iw.report = report.ToString();
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(root.scene);
            Selection.activeGameObject = root;
            Debug.Log("[BALULA] " + iw.report);
            lastReport = iw.report;
            if (batch) return;
            EditorUtility.DisplayDialog("Import OSM terminé", report.ToString().Length > 1500 ? report.ToString().Substring(0, 1500) + "…\n(rapport complet dans le composant ImportedWorld)" : report.ToString(), "OK");
        }

        // ------------------------------------------------------------------
        private static Vector3 V3(Vector2 p, float y) { return new Vector3(p.x, y, p.y); }

        private static void AddLane(List<Vector3> output, List<Vector2> pts, float offset)
        {
            for (int i = 0; i < pts.Count; i++)
            {
                Vector2 dir = i < pts.Count - 1 ? pts[i + 1] - pts[i] : pts[i] - pts[i - 1];
                if (dir.sqrMagnitude < 1e-4f) continue;
                dir.Normalize();
                Vector2 right = new Vector2(dir.y, -dir.x) * offset;
                output.Add(V3(pts[i] + right, 0.06f));
            }
        }

        private SurfaceKind Surface(OsmWay w, string hw)
        {
            string s, sm;
            w.Tags.TryGetValue("smoothness", out sm);
            bool bad = sm == "bad" || sm == "very_bad" || sm == "horrible" || sm == "very_horrible";
            if (w.Tags.TryGetValue("surface", out s))
            {
                switch (s)
                {
                    case "asphalt": case "paved": case "concrete": case "concrete:plates": case "paving_stones": case "sett":
                        return bad ? SurfaceKind.GoudronDegrade : SurfaceKind.Goudron;
                    case "sand": return SurfaceKind.Poussiere;
                    case "mud": return SurfaceKind.Boue;
                    default: return bad ? SurfaceKind.NidsDePoule : SurfaceKind.Terre;
                }
            }
            if (hw == "track") return SurfaceKind.Terre;
            if (kinshasaDirtHeuristic && (hw == "residential" || hw == "unclassified" || hw == "service" || hw == "living_street" || hw == "road")) return SurfaceKind.Terre;
            return bad ? SurfaceKind.GoudronDegrade : SurfaceKind.Goudron;
        }

        private static Color SurfaceColor(SurfaceKind s)
        {
            switch (s)
            {
                case SurfaceKind.Goudron: return new Color(0.22f, 0.22f, 0.24f);
                case SurfaceKind.GoudronDegrade: return new Color(0.3f, 0.29f, 0.28f);
                case SurfaceKind.NidsDePoule: return new Color(0.42f, 0.36f, 0.3f);
                case SurfaceKind.Poussiere: return new Color(0.76f, 0.63f, 0.46f);
                case SurfaceKind.Boue: return new Color(0.33f, 0.23f, 0.15f);
                default: return new Color(0.62f, 0.45f, 0.3f);
            }
        }

        private static float Height(OsmWay w, System.Random rnd)
        {
            string v;
            float f;
            if (w.Tags.TryGetValue("height", out v) && float.TryParse(v.Replace("m", "").Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out f) && f > 1f) return f;
            if (w.Tags.TryGetValue("building:levels", out v) && float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out f) && f > 0f) return f * 3.1f + 0.4f;
            return 3.2f + (float)rnd.NextDouble() * 1.6f;
        }

        private static bool IsArea(Dictionary<string, string> t)
        {
            string v;
            if (t.TryGetValue("natural", out v) && (v == "water" || v == "wood" || v == "scrub" || v == "grassland")) return true;
            if (t.TryGetValue("waterway", out v) && v == "riverbank") return true;
            if (t.TryGetValue("landuse", out v) && (v == "grass" || v == "forest" || v == "meadow" || v == "recreation_ground" || v == "cemetery")) return true;
            if (t.TryGetValue("leisure", out v) && (v == "park" || v == "pitch" || v == "garden")) return true;
            return false;
        }

        private void AddArea(OsmData d, OsmWay w, Dictionary<string, string> tags, Dictionary<string, MeshBatch> areas, ref int count)
        {
            if (!IsArea(tags) || w.Refs.Count < 4 || w.Refs[0] != w.Refs[w.Refs.Count - 1]) return;
            bool complete;
            List<Vector2> pts = Points(d, w, out complete);
            if (!complete || pts.Count < 4) return;
            pts.RemoveAt(pts.Count - 1);
            bool near = false;
            foreach (Vector2 p in pts) if (InRange(p)) { near = true; break; }
            if (!near || pts.Count > 6000) return;
            string v;
            bool water = (tags.TryGetValue("natural", out v) && v == "water") || tags.ContainsKey("waterway");
            Vector2Int c = Cell(pts[0]);
            Batch(areas, c.x + "_" + c.y + "_" + (water ? "eau" : "vert")).Polygon(pts, water ? 0.008f : 0.012f);
            count++;
        }

        private static string Category(Dictionary<string, string> t)
        {
            string v;
            if (t.TryGetValue("amenity", out v))
            {
                switch (v)
                {
                    case "marketplace": return "marche";
                    case "hospital": case "clinic": case "pharmacy": return "sante";
                    case "school": case "university": case "college": return "ecole";
                    case "place_of_worship": return "eglise";
                    case "bank": case "bureau_de_change": return "banque";
                    case "bus_station": return "gare";
                    case "police": return "police";
                    case "restaurant": case "bar": return "restaurant";
                }
            }
            if (t.TryGetValue("shop", out v) && (v == "supermarket" || v == "mall")) return "commerce";
            if (t.TryGetValue("railway", out v) && v == "station") return "gare";
            if (t.TryGetValue("leisure", out v) && v == "stadium") return "stade";
            if (t.TryGetValue("tourism", out v) && (v == "hotel" || v == "attraction" || v == "museum")) return "tourisme";
            if (t.TryGetValue("aeroway", out v) && v == "aerodrome") return "aeroport";
            return null;
        }

        private static string Name(Dictionary<string, string> t, string fallback)
        {
            string v;
            if (t != null && (t.TryGetValue("name:fr", out v) || t.TryGetValue("name", out v)) && !string.IsNullOrEmpty(v)) return v;
            return fallback;
        }

        private static ImportedPlace Place(string id, string name, string category, Vector2 p, int weight, bool stop, bool fuel)
        {
            return new ImportedPlace { id = id, name = name, category = category, position = new Vector3(p.x, 0f, p.y), weight = weight, isStop = stop, isFuel = fuel, status = "osm" };
        }

        private static string Safe(string s)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char ch in s) sb.Append(char.IsLetterOrDigit(ch) ? ch : '_');
            return sb.Length > 0 ? sb.ToString() : "Carte";
        }

        private static Material Mat(Dictionary<string, Material> mats, string key, Shader shader, Color c, string assetPath, ref bool first)
        {
            Material m;
            if (mats.TryGetValue(key, out m)) return m;
            m = new Material(shader) { name = key };
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", key.StartsWith("RouteGoudron") ? 0.25f : 0.08f);
            m.enableInstancing = true;
            AssetDatabase.CreateAsset(m, assetPath + "/" + key + ".mat");
            first = false;
            mats[key] = m;
            return m;
        }

        private static GameObject Emit(GameObject root, string name, MeshBatch batch, Material mat, string assetPath, ref bool first, bool collider, ref int idx)
        {
            Mesh mesh = batch.Build(name);
            if (first) { AssetDatabase.CreateAsset(mesh, assetPath); first = false; }
            else AssetDatabase.AddObjectToAsset(mesh, assetPath);
            GameObject g = new GameObject(name);
            g.transform.SetParent(root.transform, false);
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer mr = g.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            if (collider) g.AddComponent<MeshCollider>().sharedMesh = mesh;
            GameObjectUtility.SetStaticEditorFlags(g, StaticEditorFlags.BatchingStatic);
            idx++;
            return g;
        }
    }
}
#endif
