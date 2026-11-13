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

        /// <summary>Quad orienté vers « facing » (l'ordre des sommets est inversé si besoin), UV fournis.</summary>
        public void QuadFacing(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud, Vector3 facing)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), facing) < 0f)
            {
                Vector3 t = b; b = d; d = t;
                Vector2 tu = ub; ub = ud; ud = tu;
            }
            int i = V.Count;
            V.Add(a); V.Add(b); V.Add(c); V.Add(d);
            UV.Add(ua); UV.Add(ub); UV.Add(uc); UV.Add(ud);
            T.Add(i); T.Add(i + 1); T.Add(i + 2);
            T.Add(i); T.Add(i + 2); T.Add(i + 3);
        }

        /// <summary>Grille horizontale à sommets partagés (sol découpé en carrés).</summary>
        public void Grid(float x0, float z0, float x1, float z1, int nx, int nz, float y)
        {
            int b = V.Count;
            for (int j = 0; j <= nz; j++)
                for (int i = 0; i <= nx; i++)
                {
                    float x = Mathf.Lerp(x0, x1, i / (float)nx), z = Mathf.Lerp(z0, z1, j / (float)nz);
                    V.Add(new Vector3(x, y, z));
                    UV.Add(new Vector2(x, z) * 0.05f);
                }
            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                {
                    int i00 = b + j * (nx + 1) + i, i10 = i00 + 1, i01 = i00 + nx + 1, i11 = i01 + 1;
                    T.Add(i00); T.Add(i01); T.Add(i11);
                    T.Add(i00); T.Add(i11); T.Add(i10);
                }
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

        // ------------------------------------------------------------------
        // Monuments (modèles 3D fournis par MATULA DESIGN) : terrain clôturé de barreaux de fer, arbres à l'intérieur
        // ------------------------------------------------------------------
        private class Enclosure
        {
            public string Id, Name, Model;
            public Vector2 C, X;           // centre et direction du grand axe du modèle
            public float Hx, Hz;           // demi-emprise du terrain clôturé
            public float Corner;           // rayon des angles (Hx = Hz = Corner : terrain rond)
            public float ModelHx, ModelHz; // demi-emprise du modèle (les arbres restent autour)
            public float Yaw, Sink;
            public string Note, Where;

            public Vector2 Z { get { return new Vector2(-X.y, X.x); } }

            /// <summary>Distance signée au bord du terrain (négative à l'intérieur).</summary>
            public float Signed(Vector2 p)
            {
                Vector2 d = p - C;
                float lx = Mathf.Abs(Vector2.Dot(d, X)) - (Hx - Corner), lz = Mathf.Abs(Vector2.Dot(d, Z)) - (Hz - Corner);
                float outside = new Vector2(Mathf.Max(lx, 0f), Mathf.Max(lz, 0f)).magnitude;
                return outside + Mathf.Min(Mathf.Max(lx, lz), 0f) - Corner;
            }

            /// <summary>Contour du terrain (points tous les « step » mètres environ, sens direct).</summary>
            public List<Vector2> Outline(float step)
            {
                List<Vector2> pts = new List<Vector2>();
                float sx = Hx - Corner, sz = Hz - Corner;
                Vector2[] corners = { new Vector2(sx, sz), new Vector2(-sx, sz), new Vector2(-sx, -sz), new Vector2(sx, -sz) };
                for (int k = 0; k < 4; k++)
                {
                    int n = Mathf.Max(2, Mathf.CeilToInt(Corner * Mathf.PI * 0.5f / step));
                    for (int i = 0; i < n; i++)
                    {
                        float a = (k * 90f + 90f * i / n) * Mathf.Deg2Rad;
                        Vector2 l = corners[k] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Corner;
                        pts.Add(C + X * l.x + Z * l.y);
                    }
                    // Côté droit jusqu'à l'angle suivant
                    Vector2 from = corners[k] + new Vector2(Mathf.Cos((k + 1) * 90f * Mathf.Deg2Rad), Mathf.Sin((k + 1) * 90f * Mathf.Deg2Rad)) * Corner;
                    Vector2 nc = corners[(k + 1) % 4];
                    Vector2 to = nc + new Vector2(Mathf.Cos((k + 1) * 90f * Mathf.Deg2Rad), Mathf.Sin((k + 1) * 90f * Mathf.Deg2Rad)) * Corner;
                    float len = Vector2.Distance(from, to);
                    int m = Mathf.FloorToInt(len / step);
                    for (int i = 0; i < m; i++)
                    {
                        Vector2 l = Vector2.Lerp(from, to, i / (float)Mathf.Max(1, m));
                        if (i == 0) continue;
                        pts.Add(C + X * l.x + Z * l.y);
                    }
                }
                return pts;
            }
        }
        private readonly List<Enclosure> enclosures = new List<Enclosure>();

        /// <summary>Distance du point au bord de la chaussée la plus proche (négative sur une chaussée).</summary>
        private static float RoadClearance(Dictionary<long, List<Vector4>> grid, Dictionary<long, List<float>> hws, Vector2 p)
        {
            float best = 999f;
            int gx = Mathf.FloorToInt(p.x / 50f), gz = Mathf.FloorToInt(p.y / 50f);
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    long key = ((long)(gx + dx) << 32) ^ (uint)(gz + dz);
                    List<Vector4> l;
                    if (!grid.TryGetValue(key, out l)) continue;
                    List<float> h = hws[key];
                    for (int i = 0; i < l.Count; i++)
                    {
                        Vector2 a = new Vector2(l[i].x, l[i].y), b = new Vector2(l[i].z, l[i].w), ab = b - a;
                        float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
                        float d = Vector2.Distance(p, a + ab * t) - h[i];
                        if (d < best) best = d;
                    }
                }
            return best;
        }

        /// <summary>Mur ou barre de p0 à p1 (section rectangulaire : épaisseur le long de n, de y0 à y1), avec dessus.</summary>
        private static void Bar(MeshBatch mb, Vector2 p0, Vector2 p1, Vector2 n, float thick, float y0, float y1)
        {
            Vector2 h = n * (thick * 0.5f);
            Vector3 a0 = V3(p0 + h, y0), a1 = V3(p0 + h, y1), b0 = V3(p1 + h, y0), b1 = V3(p1 + h, y1);
            Vector3 c0 = V3(p0 - h, y0), c1 = V3(p0 - h, y1), d0 = V3(p1 - h, y0), d1 = V3(p1 - h, y1);
            Vector3 n3 = new Vector3(n.x, 0f, n.y);
            Vector2 uv = Vector2.zero;
            mb.QuadFacing(a0, a1, b1, b0, uv, uv, uv, uv, n3);
            mb.QuadFacing(c0, c1, d1, d0, uv, uv, uv, uv, -n3);
            mb.QuadFacing(a1, c1, d1, b1, uv, uv, uv, uv, Vector3.up);
            Vector3 along = V3(p1 - p0, 0f).normalized;
            mb.QuadFacing(a0, a1, c1, c0, uv, uv, uv, uv, -along);
            mb.QuadFacing(b0, b1, d1, d0, uv, uv, uv, uv, along);
        }

        /// <summary>Poteau vertical de section carrée.</summary>
        private static void Post(MeshBatch mb, Vector2 p, float half, float y0, float y1)
        {
            Bar(mb, p - new Vector2(half, 0f), p + new Vector2(half, 0f), new Vector2(0f, 1f), half * 2f, y0, y1);
        }

        /// <summary>Arbre stylisé (tronc + houppier à facettes), comme ceux des parcelles.</summary>
        private static void Tree(MeshBatch trunk, MeshBatch leaves, Vector2 p, float h, float r)
        {
            Post(trunk, p, 0.17f, 0f, h);
            Vector3 c = V3(p, h + r * 0.45f);
            Vector3 rx = new Vector3(r, 0f, 0f), ry = new Vector3(0f, r * 0.8f, 0f), rz = new Vector3(0f, 0f, r);
            Vector2 uv = Vector2.zero;
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        Vector3 a = c + rx * sx, b = c + ry * sy, d = c + rz * sz;
                        Vector3 outward = new Vector3(sx, sy, sz);
                        leaves.QuadFacing(a, b, d, d, uv, uv, uv, uv, outward);
                    }
        }

        /// <summary>Grand axe d'un contour (direction dans le plan), par l'analyse de ses points.</summary>
        private static Vector2 MainAxis(List<Vector2> pts, out Vector2 center)
        {
            Vector2 mn = pts[0], mx = pts[0];
            foreach (Vector2 q in pts) { mn = Vector2.Min(mn, q); mx = Vector2.Max(mx, q); }
            center = (mn + mx) * 0.5f;
            float sxx = 0f, szz = 0f, sxz = 0f;
            foreach (Vector2 q in pts)
            {
                Vector2 d = q - center;
                sxx += d.x * d.x; szz += d.y * d.y; sxz += d.x * d.y;
            }
            float ang = 0.5f * Mathf.Atan2(2f * sxz, sxx - szz);
            return new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
        }

        private void PlanMonuments(OsmData d, GameDatabase db, Dictionary<long, List<Vector4>> carriage, Dictionary<long, List<float>> carriageHw, StringBuilder report)
        {
            enclosures.Clear();
            foreach (LocationDef loc in db.Locations)
            {
                if (!loc.HasCoordinates || (loc.id != "stade-martyrs" && loc.id != "tour-echangeur")) continue;
                double fx, fz;
                GeoProjection.ToLocal(loc.lat, loc.lon, originLat, originLon, out fx, out fz);
                Vector2 p = new Vector2((float)fx, (float)fz);
                string where = "coordonnées de référence";
                if (loc.latModele != 0.0 || loc.lonModele != 0.0)
                {
                    // Repère du créateur : retenu s'il laisse la place du monument (sinon la référence déjà vérifiée)
                    GeoProjection.ToLocal(loc.latModele, loc.lonModele, originLat, originLon, out fx, out fz);
                    Vector2 pin = new Vector2((float)fx, (float)fz);
                    float cPin = RoadClearance(carriage, carriageHw, pin), cRef = RoadClearance(carriage, carriageHw, p);
                    if (loc.id != "tour-echangeur" || cPin >= 24f || cPin >= cRef)
                    {
                        where = "repère du créateur (à " + Vector2.Distance(pin, p).ToString("0") + " m de la référence)";
                        p = pin;
                    }
                    else where = "référence vérifiée (le repère du créateur, à " + Vector2.Distance(pin, p).ToString("0") + " m, tombe à " + cPin.ToString("0") + " m d'une chaussée : pas la place de la tour)";
                }
                if (!InRange(p)) { report.Append("Monument « ").Append(loc.nom).Append(" » : hors de la zone importée, non posé.\n"); continue; }
                Enclosure e = new Enclosure { Id = loc.id, Name = loc.nom, C = p, X = new Vector2(1f, 0f), Where = where };
                if (loc.id == "stade-martyrs")
                {
                    e.Model = "StadeMartyrs";
                    e.ModelHx = 100f; e.ModelHz = 91f; e.Sink = 0.19f;
                    // Orientation et centre réels : contour du stade dans OpenStreetMap (terrain ou enceinte le plus grand à proximité)
                    float bestSize = 0f;
                    foreach (OsmWay w in d.Ways)
                    {
                        string lv, bv;
                        bool stadium = (w.Tags.TryGetValue("leisure", out lv) && (lv == "stadium" || lv == "pitch" || lv == "track")) || (w.Tags.TryGetValue("building", out bv) && (bv == "stadium" || bv == "grandstand"));
                        if (!stadium || w.Refs.Count < 4) continue;
                        bool complete;
                        List<Vector2> pts = Points(d, w, out complete);
                        if (!complete || pts.Count < 4) continue;
                        Vector2 wc;
                        Vector2 axis = MainAxis(pts, out wc);
                        if ((wc - p).sqrMagnitude > 170f * 170f) continue;
                        float size = 0f;
                        foreach (Vector2 q in pts) size = Mathf.Max(size, (q - wc).magnitude);
                        if (size < 40f || size <= bestSize) continue;
                        bestSize = size;
                        e.X = axis;
                        e.C = wc;
                        e.Note = "orientation et centre pris sur le contour OpenStreetMap n° " + w.Id + " (rayon " + size.ToString("0") + " m)";
                    }
                    if (bestSize <= 0f) e.Note = "aucun contour de stade dans OpenStreetMap : orientation est-ouest par défaut, à vérifier";
                    e.Hx = e.ModelHx + 15f; e.Hz = e.ModelHz + 15f; e.Corner = 45f;
                }
                else
                {
                    e.Model = "TourEchangeur";
                    e.ModelHx = 22f; e.ModelHz = 22f;
                    // Terrain rond, aussi grand que le permettent les chaussées de l'échangeur
                    float clear = RoadClearance(carriage, carriageHw, p);
                    float r = Mathf.Clamp(clear - 2.5f, 26f, 46f);
                    e.Hx = e.Hz = e.Corner = r;
                    e.Note = "terrain rond de " + r.ToString("0") + " m de rayon (chaussée la plus proche à " + clear.ToString("0") + " m du centre) ; orientation du modèle non vérifiée";
                }
                // Unity fait tourner l'axe X local vers (cos, -sin) : angle qui aligne le grand axe du modèle sur e.X
                e.Yaw = Mathf.Atan2(-e.X.y, e.X.x) * Mathf.Rad2Deg;
                enclosures.Add(e);
            }
        }

        private bool InEnclosure(List<Vector2> pts, Vector2 center)
        {
            foreach (Enclosure e in enclosures)
            {
                if (e.Signed(center) < 1.5f) return true;
                foreach (Vector2 q in pts) if (e.Signed(q) < 1.5f) return true;
            }
            return false;
        }

        /// <summary>Clôture de barreaux de fer espacés sur un muret, portail, pelouse et arbres autour du monument.</summary>
        private void BuildEnclosure(Enclosure e, Dictionary<string, MeshBatch> decor, Dictionary<string, MeshBatch> areas, List<Vector4> fenceBoxes,
                                    Dictionary<long, List<Vector4>> carriage, Dictionary<long, List<float>> carriageHw, StringBuilder report)
        {
            List<Vector2> outline = e.Outline(2.6f);
            int n = outline.Count;
            MeshBatch iron = Batch(decor, e.Id + "_grille"), wall = Batch(decor, e.Id + "_socle"), trunk = Batch(decor, e.Id + "_tronc"), leaves = Batch(decor, e.Id + "_feuilles");
            // Portail : face à la chaussée la plus proche
            int gate = 0;
            float gateD = float.MaxValue;
            bool[] open = new bool[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 mid = (outline[i] + outline[(i + 1) % n]) * 0.5f;
                float c = RoadClearance(carriage, carriageHw, mid);
                open[i] = c < 1.6f; // une chaussée passe ici : pas de clôture en travers
                if (c >= 1.6f && c < gateD) { gateD = c; gate = i; }
            }
            int gates = 0, opened = 0;
            for (int k = -1; k <= 1; k++) open[(gate + k + n) % n] = true;
            float fenceLen = 0f;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = outline[i], b = outline[(i + 1) % n];
                Vector2 dir = b - a;
                float len = dir.magnitude;
                if (len < 0.2f) continue;
                dir /= len;
                Vector2 nrm = new Vector2(-dir.y, dir.x);
                bool prevOpen = open[(i - 1 + n) % n];
                if (open[i])
                {
                    opened++;
                    // Piliers de part et d'autre d'une ouverture
                    if (!prevOpen) { Post(wall, a, 0.3f, 0f, 2.9f); gates++; }
                    if (!open[(i + 1) % n]) Post(wall, b, 0.3f, 0f, 2.9f);
                    continue;
                }
                fenceLen += len;
                Bar(wall, a, b, nrm, 0.3f, 0f, 0.45f);            // muret
                Post(iron, a, 0.06f, 0.45f, 2.55f);               // poteau
                Bar(iron, a, b, nrm, 0.05f, 0.6f, 0.65f);         // lisse basse
                Bar(iron, a, b, nrm, 0.05f, 2.2f, 2.25f);         // lisse haute
                // Barreaux espacés : on voit les arbres et le monument à travers
                int bars = Mathf.Max(1, Mathf.RoundToInt(len / 0.17f));
                Vector2 uv = Vector2.zero;
                Vector3 n3 = new Vector3(nrm.x, 0f, nrm.y);
                for (int k = 1; k < bars; k++)
                {
                    Vector2 q = Vector2.Lerp(a, b, k / (float)bars);
                    Vector2 w = dir * 0.0125f;
                    Vector3 q0 = V3(q - w, 0.45f), q1 = V3(q - w, 2.42f), q2 = V3(q + w, 2.42f), q3 = V3(q + w, 0.45f);
                    iron.QuadFacing(q0, q1, q2, q3, uv, uv, uv, uv, n3);
                    iron.QuadFacing(q0, q1, q2, q3, uv, uv, uv, uv, -n3);
                }
                Vector2 m2 = (a + b) * 0.5f;
                fenceBoxes.Add(new Vector4(m2.x, m2.y, Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg, len + 0.1f));
            }
            // Pelouse du terrain
            Vector2Int cell = Cell(e.C);
            List<Vector2> lawn = new List<Vector2>(outline);
            if (Triangulator.Area(lawn) < 0f) lawn.Reverse();
            Batch(areas, cell.x + "_" + cell.y + "_vert").Polygon(lawn, 0.016f);
            // Arbres entre la clôture et le monument
            System.Random rnd = new System.Random(e.Id.GetHashCode() & 0xffff);
            int wanted = Mathf.RoundToInt(fenceLen / 9f) + 6, trees = 0;
            for (int attempt = 0; attempt < wanted * 14 && trees < wanted; attempt++)
            {
                float lx = ((float)rnd.NextDouble() * 2f - 1f) * e.Hx, lz = ((float)rnd.NextDouble() * 2f - 1f) * e.Hz;
                Vector2 q = e.C + e.X * lx + e.Z * lz;
                if (e.Signed(q) > -3.2f) continue;
                // Hors de l'emprise du modèle (ellipse) et hors des chaussées
                float ex = lx / (e.ModelHx + 3.5f), ez = lz / (e.ModelHz + 3.5f);
                if (ex * ex + ez * ez < 1f) continue;
                if (RoadClearance(carriage, carriageHw, q) < 2.5f) continue;
                Tree(trunk, leaves, q, 3.2f + (float)rnd.NextDouble() * 2.4f, 2.3f + (float)rnd.NextDouble() * 1.5f);
                trees++;
            }
            report.Append("Monument « ").Append(e.Name).Append(" » : modèle ").Append(e.Model).Append(" posé à (").Append(e.C.x.ToString("0")).Append(", ").Append(e.C.y.ToString("0"))
                  .Append("), angle ").Append(e.Yaw.ToString("0")).Append("° — emplacement : ").Append(e.Where).Append(" ; ").Append(e.Note).Append(". Clôture de barreaux : ").Append(fenceLen.ToString("0")).Append(" m, ")
                  .Append(gates).Append(" ouverture(s), ").Append(trees).Append(" arbres.\n");
        }

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
            List<ImportedRoad> roadAxes = new List<ImportedRoad>();
            int roadPoints = 0;
            List<KeyValuePair<float, ImportedRoad>> trafficCandidates = new List<KeyValuePair<float, ImportedRoad>>();
            List<Vector3> buildingSpots = new List<Vector3>();
            Vector3 spawn = Vector3.zero;
            float spawnHeading = 0f, bestSpawn = float.MaxValue;
            // Ponts (bridge=* dans OpenStreetMap), cours d'eau, monuments, lumières des bâtiments
            List<Vector4> bridgeSegs = new List<Vector4>();
            List<float> bridgeHw = new List<float>();
            int bridgeWays = 0, waterWays = 0, skippedLand = 0, litWindows = 0;
            float bridgeLen = 0f, waterLen = 0f;
            Dictionary<string, MeshBatch> decor = new Dictionary<string, MeshBatch>();
            Dictionary<string, LightMesh> lights = new Dictionary<string, LightMesh>();
            List<Vector4> fenceBoxes = new List<Vector4>();
            List<ImportedRoad> waterAxes = new List<ImportedRoad>();

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
                string bridgeTag;
                bool isBridge = w.Tags.TryGetValue("bridge", out bridgeTag) && bridgeTag != "no";
                if (isBridge) bridgeWays++;
                for (int i = 0; i < pts.Count - 1; i++)
                {
                    Vector2 a = pts[i], b = pts[i + 1];
                    if (!InRange(a) && !InRange(b)) continue;
                    Vector2 dir = b - a;
                    float len = dir.magnitude;
                    if (len < 0.05f) continue;
                    dir /= len;
                    if (isBridge) { bridgeSegs.Add(new Vector4(a.x, a.y, b.x, b.y)); bridgeHw.Add(width * 0.5f); }
                    Vector2 left = new Vector2(-dir.y, dir.x) * (width * 0.5f);
                    Vector2 a2 = i > 0 ? a - dir * Mathf.Min(width * 0.5f, len * 0.5f) : a;
                    Vector2 b2 = i < pts.Count - 2 ? b + dir * Mathf.Min(width * 0.5f, len * 0.5f) : b;
                    Vector2Int c = Cell((a + b) * 0.5f);
                    MeshBatch mb = Batch(roads, c.x + "_" + c.y + "_" + surface);
                    // Morceaux de 20 m au plus : sur téléphone le brouillard est calculé aux sommets, une longue rue
                    // droite (sommets lointains) apparaissait entièrement noyée dans le brouillard, couleur du ciel
                    float segLen = Vector2.Distance(a2, b2);
                    int pieces = Mathf.Max(1, Mathf.CeilToInt(segLen / 20f));
                    for (int k = 0; k < pieces; k++)
                    {
                        Vector2 p0 = Vector2.Lerp(a2, b2, k / (float)pieces), p1 = Vector2.Lerp(a2, b2, (k + 1) / (float)pieces);
                        mb.Quad(V3(p0 + left, y), V3(p1 + left, y), V3(p1 - left, y), V3(p0 - left, y));
                    }
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
                // Portions de la rue dans la zone : axes (parcelles, carte), trottoirs, boucles de circulation
                {
                    List<Vector2> run = new List<Vector2>();
                    string roadName = Name(w.Tags, null);
                    for (int i = 0; i <= pts.Count; i++)
                    {
                        bool inside = i < pts.Count && InRange(pts[i]);
                        if (inside) run.Add(pts[i]);
                        if ((!inside || i == pts.Count) && run.Count >= 2)
                        {
                            if (roadPoints < 250000)
                            {
                                ImportedRoad axis = new ImportedRoad { name = roadName ?? "", width = width, rank = rank, surface = (int)surface };
                                axis.pts.AddRange(run);
                                roadAxes.Add(axis);
                                roadPoints += run.Count;
                                float runLen = 0f;
                                for (int k = 0; k < run.Count - 1; k++) runLen += Vector2.Distance(run[k], run[k + 1]);
                                if (rank >= 3 && hw != "service" && hw != "track" && runLen >= 60f)
                                    trafficCandidates.Add(new KeyValuePair<float, ImportedRoad>(runLen, axis));
                            }
                            if (rank >= 2 && rank <= 8 && hw != "service" && hw != "track" && walkPoints < 160000)
                            {
                                foreach (float side in new[] { 1f, -1f })
                                {
                                    ImportedPath wp = new ImportedPath { name = "Trottoir" };
                                    AddLane(wp.points, run, side * (width * 0.5f + 1.3f));
                                    for (int k = 0; k < wp.points.Count; k++) wp.points[k] = new Vector3(wp.points[k].x, 0f, wp.points[k].z);
                                    if (wp.points.Count >= 2) { walks.Add(wp); walkPoints += wp.points.Count; }
                                }
                            }
                        }
                        if (!inside) run = new List<Vector2>();
                    }
                }
                if (rank >= 5)
                {
                    float total = 0f;
                    for (int i = 0; i < pts.Count - 1; i++) total += Vector2.Distance(pts[i], pts[i + 1]);
                    if (total > 300f) majors.Add(new KeyValuePair<float, OsmWay>(total, w));
                }
            }

            // ---------------- Boucles de circulation (aller-retour sur toutes les rues carrossables) ----------------
            trafficCandidates.Sort((p, q) => q.Key.CompareTo(p.Key));
            for (int k = 0; k < trafficCandidates.Count && paths.Count < 3000; k++)
            {
                ImportedRoad axis = trafficCandidates[k].Value;
                List<Vector2> lane = new List<Vector2>(axis.pts);
                float off = Mathf.Clamp(axis.width * 0.25f, 1.2f, 1.9f);
                bool pavedAxis = axis.surface == (int)SurfaceKind.Goudron || axis.surface == (int)SurfaceKind.GoudronDegrade;
                ImportedPath ip = new ImportedPath { name = string.IsNullOrEmpty(axis.name) ? "Rue" : axis.name, major = axis.rank >= 5 || pavedAxis };
                AddLane(ip.points, lane, off);
                lane.Reverse();
                AddLane(ip.points, lane, off);
                paths.Add(ip);
            }

            // ---------------- Stations-service (emprises calculées avant les bâtiments) ----------------
            // Chaque station du jeu est posée au bord de la rue la plus proche : son terrain reste libre de bâtiments OSM.
            GameDatabase db = DataLoader.LoadAll(new List<string>());
            List<KeyValuePair<string, Vector2>> fuelSpots = new List<KeyValuePair<string, Vector2>>();
            foreach (LocationDef loc in db.Locations)
            {
                if (!loc.HasCoordinates || loc.categorie != "station-service") continue;
                double fx, fz;
                GeoProjection.ToLocal(loc.lat, loc.lon, originLat, originLon, out fx, out fz);
                Vector2 fp = new Vector2((float)fx, (float)fz);
                if (InRange(fp)) fuelSpots.Add(new KeyValuePair<string, Vector2>(loc.id, fp));
            }
            foreach (KeyValuePair<long, OsmNode> kv in d.Nodes)
            {
                string fv;
                if (kv.Value.Tags == null || !kv.Value.Tags.TryGetValue("amenity", out fv) || fv != "fuel" || !InRange(kv.Value.P)) continue;
                fuelSpots.Add(new KeyValuePair<string, Vector2>("osm-n" + kv.Key, kv.Value.P));
            }
            List<ImportedPlace> fuelAreas = new List<ImportedPlace>();
            foreach (OsmWay w in d.Ways)
            {
                string fv;
                if (!w.Tags.TryGetValue("amenity", out fv) || fv != "fuel" || w.Refs.Count < 3) continue;
                bool fcomplete;
                List<Vector2> fpts = Points(d, w, out fcomplete);
                if (fpts.Count < 3) continue;
                Vector2 fc = Vector2.zero;
                foreach (Vector2 q in fpts) fc += q;
                fc /= fpts.Count;
                if (!InRange(fc)) continue;
                fuelSpots.Add(new KeyValuePair<string, Vector2>("osm-w" + w.Id, fc));
                fuelAreas.Add(Place("osm-w" + w.Id, Name(w.Tags, "Station-service"), "carburant", fc, 4, false, true));
            }
            stationLots.Clear();
            duplicateFuel.Clear();
            List<Vector4> lotList = new List<Vector4>();
            foreach (KeyValuePair<string, Vector2> fs in fuelSpots)
            {
                Vector2 lo, ln;
                bool big;
                if (!FuelStations.Plan(roadAxes, fs.Value, out lo, out ln, out big)) continue;
                Vector2 sz = FuelStations.LotSize(big);
                Vector2 center = lo + ln * sz.y * 0.5f;
                bool dup = false;
                foreach (Vector4 l in lotList)
                    if ((new Vector2(l.x, l.y) + new Vector2(l.z, l.w) * FuelStations.LotSize(true).y * 0.5f - center).sqrMagnitude < 38f * 38f) { dup = true; break; }
                if (dup) { duplicateFuel.Add(fs.Key); continue; }
                lotList.Add(new Vector4(lo.x, lo.y, ln.x, ln.y));
                stationLots[fs.Key] = new KeyValuePair<Vector4, bool>(new Vector4(lo.x, lo.y, ln.x, ln.y), big);
            }

            // ---------------- Grille des chaussées (bâtiments posés sur une rue) ----------------
            Dictionary<long, List<Vector4>> carriage = new Dictionary<long, List<Vector4>>();
            Dictionary<long, List<float>> carriageHw = new Dictionary<long, List<float>>();
            foreach (ImportedRoad axis in roadAxes)
            {
                if (axis.rank < 2) continue;
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
                            if (!carriage.TryGetValue(key, out l)) { l = new List<Vector4>(); carriage[key] = l; carriageHw[key] = new List<float>(); }
                            l.Add(new Vector4(a.x, a.y, b.x, b.y));
                            carriageHw[key].Add(axis.width * 0.5f);
                        }
                }
            }

            // ---------------- Monuments : emplacement, terrain clôturé ----------------
            PlanMonuments(d, db, carriage, carriageHw, report);
            foreach (Enclosure e in enclosures) BuildEnclosure(e, decor, areas, fenceBoxes, carriage, carriageHw, report);

            // ---------------- Bords des rues goudronnées : caniveaux, bordures (rouge et blanc sur les boulevards), accotements ----------------
            Dictionary<string, MeshBatch> edges = new Dictionary<string, MeshBatch>();
            // Ponts : parapets de béton et garde-corps des deux côtés, sauf là où une autre chaussée croise
            for (int bi = 0; bi < bridgeSegs.Count; bi++)
            {
                Vector2 a = new Vector2(bridgeSegs[bi].x, bridgeSegs[bi].y), b = new Vector2(bridgeSegs[bi].z, bridgeSegs[bi].w);
                Vector2 dir = b - a;
                float len = dir.magnitude;
                if (len < 1f) continue;
                dir /= len;
                Vector2 nrm = new Vector2(-dir.y, dir.x);
                bridgeLen += len;
                int steps = Mathf.Max(1, Mathf.CeilToInt(len / 2f));
                foreach (float side in new[] { 1f, -1f })
                {
                    Vector2 off = nrm * side * (bridgeHw[bi] + 0.5f);
                    for (int k = 0; k < steps; k++)
                    {
                        Vector2 p0 = Vector2.Lerp(a, b, k / (float)steps) + off, p1 = Vector2.Lerp(a, b, (k + 1) / (float)steps) + off;
                        Vector2 bmid = (p0 + p1) * 0.5f;
                        if (!InRange(bmid) || RoadClearance(carriage, carriageHw, bmid) < 0.35f) continue;
                        Vector2Int pc = Cell(bmid);
                        MeshBatch pb = Batch(edges, pc.x + "_" + pc.y + "_pont");
                        Bar(pb, p0, p1, nrm, 0.28f, 0f, 0.85f);
                        Bar(pb, p0, p1, nrm, 0.08f, 1.02f, 1.1f);
                        if (k % 2 == 0) Post(pb, p0, 0.05f, 0.85f, 1.05f);
                    }
                }
            }
            // Carrefours : points partagés par plusieurs rues (les bordures s'arrêtent avant)
            Dictionary<long, int> nodeUse = new Dictionary<long, int>();
            foreach (ImportedRoad axis in roadAxes)
            {
                if (axis.rank < 2) continue;
                HashSet<long> mine = new HashSet<long>();
                foreach (Vector2 q in axis.pts)
                {
                    long k = PointKey(q);
                    if (!mine.Add(k)) continue;
                    int cnt;
                    nodeUse.TryGetValue(k, out cnt);
                    nodeUse[k] = cnt + 1;
                }
            }
            HashSet<long> junctions = new HashSet<long>();
            foreach (KeyValuePair<long, int> kv in nodeUse) if (kv.Value >= 2) junctions.Add(kv.Key);
            float edgeLength = 0f;
            foreach (ImportedRoad axis in roadAxes) edgeLength += RoadEdges(axis, edges, junctions, lotList);
            int edgeVerts = 0;
            foreach (MeshBatch eb in edges.Values) edgeVerts += eb.V.Count;

            // ---------------- Routes en travaux (difficiles à passer) ----------------
            List<WorkZone> works = ChooseWorks(roadAxes, roads);

            // ---------------- Bâtiments ----------------
            int skippedStations = 0, skippedRoads = 0;
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
                    if (InStationLot(lotList, pts, center)) { skippedStations++; continue; }
                    if (enclosures.Count > 0 && InEnclosure(pts, center)) { skippedLand++; continue; }
                    if (OnCarriageway(carriage, carriageHw, pts, center)) { skippedRoads++; continue; }
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
                    // Courant dans les parcelles : quelques fenêtres éclairées la nuit sur les façades
                    if (litWindows < 140000)
                    {
                        System.Random wr = new System.Random((int)(w.Id % 1000003));
                        LightMesh lm;
                        string lk = c.x + "_" + c.y;
                        if (!lights.TryGetValue(lk, out lm)) { lm = new LightMesh(); lights[lk] = lm; }
                        int floors = Mathf.Clamp(Mathf.FloorToInt(h / 3f), 1, 5);
                        for (int i = 0; i < pts.Count; i++)
                        {
                            Vector2 a = pts[i], b = pts[(i + 1) % pts.Count];
                            float wl = Vector2.Distance(a, b);
                            if (wl < 3.4f || h < 2.6f) continue;
                            Vector2 wd = (b - a) / wl, wo = new Vector2(wd.y, -wd.x);
                            int nw = Mathf.FloorToInt(wl / 3.4f);
                            for (int fl = 0; fl < floors; fl++)
                                for (int k = 0; k < nw; k++)
                                {
                                    if (wr.Next(100) >= 24) continue;
                                    Vector2 q = a + wd * ((k + 0.5f) * wl / nw) + wo * 0.05f;
                                    int tone = wr.Next(10);
                                    byte al = (byte)(110 + wr.Next(146));
                                    Color32 col = tone < 6 ? new Color32(255, 214, 140, al) : (tone < 8 ? new Color32(235, 240, 255, al) : new Color32(150, 190, 255, al));
                                    lm.Panel(V3(q, Mathf.Min(h - 0.9f, fl * 3f + 1.7f)), new Vector3(wd.x, 0f, wd.y), Vector3.up, 0.5f, 0.55f, col);
                                    litWindows++;
                                }
                        }
                    }
                    float br = 0f;
                    foreach (Vector2 p in pts) br = Mathf.Max(br, Vector2.Distance(p, center));
                    buildingSpots.Add(new Vector3(center.x, br, center.y));
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

            // ---------------- Rivières, ruisseaux, canaux (tracés réels d'OpenStreetMap) ----------------
            foreach (OsmWay w in d.Ways)
            {
                string wv;
                if (!w.Tags.TryGetValue("waterway", out wv)) continue;
                float ww;
                switch (wv)
                {
                    case "river": ww = 14f; break;
                    case "canal": ww = 7f; break;
                    case "stream": ww = 4.5f; break;
                    case "drain": ww = 2.6f; break;
                    case "ditch": ww = 1.6f; break;
                    default: continue;
                }
                string tv;
                if (w.Tags.TryGetValue("tunnel", out tv) && tv != "no") continue; // busé, sous la chaussée
                float tw;
                if (w.Tags.TryGetValue("width", out tv) && float.TryParse(tv, NumberStyles.Float, CultureInfo.InvariantCulture, out tw) && tw > 0.5f && tw < 400f) ww = tw;
                bool wcomplete;
                List<Vector2> wp = Points(d, w, out wcomplete);
                if (wp.Count < 2) continue;
                bool anyIn = false;
                ImportedRoad wAxis = new ImportedRoad { name = Name(w.Tags, "") , width = ww, rank = 0 };
                foreach (Vector2 q in wp) if (InRange(q)) wAxis.pts.Add(q);
                if (wAxis.pts.Count >= 2) waterAxes.Add(wAxis);
                for (int i = 0; i < wp.Count - 1; i++)
                {
                    Vector2 a = wp[i], b = wp[i + 1];
                    if (!InRange(a) && !InRange(b)) continue;
                    Vector2 dir = b - a;
                    float len = dir.magnitude;
                    if (len < 0.05f) continue;
                    dir /= len;
                    anyIn = true;
                    waterLen += len;
                    Vector2 left = new Vector2(-dir.y, dir.x) * (ww * 0.5f);
                    Vector2 a2 = a - dir * Mathf.Min(ww * 0.5f, len * 0.5f), b2 = b + dir * Mathf.Min(ww * 0.5f, len * 0.5f);
                    int pieces = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a2, b2) / 20f));
                    Vector2Int wc = Cell((a + b) * 0.5f);
                    MeshBatch wb = Batch(areas, wc.x + "_" + wc.y + "_eau");
                    MeshBatch bank = Batch(areas, wc.x + "_" + wc.y + "_berge");
                    float bw = Mathf.Clamp(ww * 0.18f, 0.8f, 3f);
                    Vector2 outer = left.normalized * (ww * 0.5f + bw);
                    for (int k = 0; k < pieces; k++)
                    {
                        Vector2 p0 = Vector2.Lerp(a2, b2, k / (float)pieces), p1 = Vector2.Lerp(a2, b2, (k + 1) / (float)pieces);
                        wb.Quad(V3(p0 + left, 0.01f), V3(p1 + left, 0.01f), V3(p1 - left, 0.01f), V3(p0 - left, 0.01f));
                        // Berges de boue des deux côtés
                        bank.Quad(V3(p0 + outer, 0.006f), V3(p1 + outer, 0.006f), V3(p1 + left, 0.006f), V3(p0 + left, 0.006f));
                        bank.Quad(V3(p0 - left, 0.006f), V3(p1 - left, 0.006f), V3(p1 - outer, 0.006f), V3(p0 - outer, 0.006f));
                    }
                }
                if (anyIn) waterWays++;
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
                ImportedPlace np = Place("osm-n" + kv.Key, name ?? (stop ? "Arrêt" : "Station-service"), cat ?? (stop ? "arret" : "carburant"), n.P, weight, stop, fuel);
                if (fuel) ApplyStation(np, "osm-n" + kv.Key);
                places.Add(np);
            }
            foreach (ImportedPlace fa in fuelAreas)
            {
                ApplyStation(fa, fa.id);
                if (fa.isFuel) places.Add(fa);
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
            foreach (LocationDef loc in db.Locations)
            {
                if (loc.HasCoordinates)
                {
                    double x, z;
                    GeoProjection.ToLocal(loc.lat, loc.lon, originLat, originLon, out x, out z);
                    Vector2 p = new Vector2((float)x, (float)z);
                    if (!InRange(p)) continue;
                    ImportedPlace ip = Place(loc.id, loc.nom, "repere", p, Mathf.Max(3, loc.poids), loc.arret, loc.categorie == "station-service");
                    if (ip.isFuel) ApplyStation(ip, loc.id);
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
                    bool exact = false;
                    foreach (ImportedPlace ip in places)
                    {
                        string nn = TextUtil.Normalize(ip.name);
                        foreach (string t in terms)
                        {
                            string q = TextUtil.Normalize(t.Trim());
                            if (q.Length <= 3) continue;
                            if (nn == q) { foundPlace = ip; exact = true; break; }
                            if (foundPlace == null && nn.Contains(q)) foundPlace = ip;
                        }
                        if (exact) break;
                    }
                    if (foundPlace != null)
                    {
                        double lat, lon;
                        GeoProjection.ToLatLon(foundPlace.position.x, foundPlace.position.z, originLat, originLon, out lat, out lon);
                        found = foundPlace.name + " (" + lat.ToString("0.00000", CultureInfo.InvariantCulture) + ", " + lon.ToString("0.00000", CultureInfo.InvariantCulture) + ")" +
                                (exact ? " — nom identique, repère affiché « à vérifier »" : " — nom seulement proche, non affiché");
                    }
                    refsReport.Add("• " + loc.nom + " : coordonnées manquantes — " + (found != null ? "SUGGESTION OSM à confirmer : " + found : "aucune correspondance OSM"));
                    // Repère affiché seulement si le nom OSM est identique (un nom proche peut désigner un autre lieu)
                    if (foundPlace != null && exact)
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
            GameObject ground = new GameObject("Sol");
            ground.transform.SetParent(root.transform, false);
            Vector2 mid = (min + max) * 0.5f;
            BoxCollider groundBox = ground.AddComponent<BoxCollider>();
            groundBox.center = new Vector3(mid.x, -0.25f, mid.y);
            groundBox.size = new Vector3(size.x + 400f, 0.5f, size.y + 400f);
            ground.AddComponent<SurfaceZone>().Kind = SurfaceKind.Terre;

            int idx = 0;
            // Terre marron foncé, en dalles de 1 km découpées en carrés de 50 m : une seule face géante était
            // calculée « dans le brouillard » sur téléphone (brouillard aux sommets) et paraissait bleu-gris clair.
            Material soil = Mat(mats, "Sol", shader, new Color(0.24f, 0.165f, 0.11f), matAsset, ref firstMat);
            float gx0 = mid.x - (size.x + 400f) * 0.5f, gx1 = mid.x + (size.x + 400f) * 0.5f;
            float gz0 = mid.y - (size.y + 400f) * 0.5f, gz1 = mid.y + (size.y + 400f) * 0.5f;
            int tiles = 0;
            for (float tx = gx0; tx < gx1 - 1f; tx += 1000f)
                for (float tz = gz0; tz < gz1 - 1f; tz += 1000f)
                {
                    float ex = Mathf.Min(tx + 1000f, gx1), ez = Mathf.Min(tz + 1000f, gz1);
                    MeshBatch gb = new MeshBatch();
                    gb.Grid(tx, tz, ex, ez, Mathf.Max(1, Mathf.CeilToInt((ex - tx) / 50f)), Mathf.Max(1, Mathf.CeilToInt((ez - tz) / 50f)), -0.02f);
                    Emit(root, "SolVisuel_" + tiles++, gb, soil, meshAsset, ref firstMesh, false, ref idx);
                }
            foreach (KeyValuePair<string, MeshBatch> kv in roads)
            {
                string[] parts = kv.Key.Split('_');
                SurfaceKind s = (SurfaceKind)System.Enum.Parse(typeof(SurfaceKind), parts[2]);
                GameObject g = Emit(root, "Routes_" + kv.Key, kv.Value, Mat(mats, "Route" + s, shader, SurfaceColor(s), matAsset, ref firstMat), meshAsset, ref firstMesh, true, ref idx);
                g.AddComponent<SurfaceZone>().Kind = s;
            }
            Texture2D stripes = StripeTexture(folder);
            foreach (KeyValuePair<string, MeshBatch> kv in edges)
            {
                string kind = kv.Key.Substring(kv.Key.LastIndexOf('_') + 1);
                Material em;
                if (kind == "rb")
                {
                    em = Mat(mats, "BordureRougeBlanc", shader, Color.white, matAsset, ref firstMat);
                    if (em.mainTexture != stripes) { em.mainTexture = stripes; EditorUtility.SetDirty(em); }
                }
                else if (kind == "fond") em = Mat(mats, "CaniveauFond", shader, new Color(0.16f, 0.16f, 0.15f), matAsset, ref firstMat);
                else if (kind == "pont") em = Mat(mats, "Pont", shader, new Color(0.72f, 0.72f, 0.69f), matAsset, ref firstMat);
                else if (kind == "accot") em = Mat(mats, "Accotement", shader, new Color(0.47f, 0.35f, 0.24f), matAsset, ref firstMat);
                else em = Mat(mats, "Bordure", shader, new Color(0.62f, 0.62f, 0.6f), matAsset, ref firstMat);
                GameObject eg = Emit(root, "Bords_" + kv.Key, kv.Value, em, meshAsset, ref firstMesh, kind == "fond" || kind == "pont", ref idx);
                if (kind == "fond") eg.AddComponent<SurfaceZone>().Kind = SurfaceKind.Caniveau;
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
                if (kv.Key.EndsWith("berge"))
                {
                    Emit(root, "Berges_" + kv.Key, kv.Value, Mat(mats, "Berge", shader, new Color(0.19f, 0.15f, 0.1f), matAsset, ref firstMat), meshAsset, ref firstMesh, false, ref idx);
                    continue;
                }
                // Eau des rivières de Kinshasa : sombre, gris-vert trouble (même famille que le fleuve, en plus foncé).
                // Plus claire et plus lisse, elle renvoyait le soleil et ressemblait à une allée crème vue d'en haut.
                GameObject ag = Emit(root, (water ? "Eau_" : "Verdure_") + kv.Key, kv.Value, Mat(mats, water ? "Eau" : "Verdure", shader, water ? new Color(0.14f, 0.18f, 0.16f) : new Color(0.3f, 0.48f, 0.2f), matAsset, ref firstMat), meshAsset, ref firstMesh, water, ref idx);
                // L'eau ralentit comme la boue (on ne roule pas dans une rivière comme sur une route)
                if (water) ag.AddComponent<SurfaceZone>().Kind = SurfaceKind.Boue;
            }

            // Clôtures, murets, arbres des monuments
            foreach (KeyValuePair<string, MeshBatch> kv in decor)
            {
                if (kv.Value.V.Count == 0) continue;
                string kind = kv.Key.Substring(kv.Key.LastIndexOf('_') + 1);
                Material dm = kind == "grille" ? Mat(mats, "GrilleFer", shader, new Color(0.08f, 0.09f, 0.1f), matAsset, ref firstMat)
                            : (kind == "socle" ? Mat(mats, "Muret", shader, new Color(0.74f, 0.72f, 0.66f), matAsset, ref firstMat)
                            : (kind == "tronc" ? Mat(mats, "Tronc", shader, new Color(0.33f, 0.23f, 0.14f), matAsset, ref firstMat)
                            : Mat(mats, "Feuillage", shader, new Color(0.17f, 0.4f, 0.14f), matAsset, ref firstMat)));
                Emit(root, "Monument_" + kv.Key, kv.Value, dm, meshAsset, ref firstMesh, false, ref idx);
            }
            if (fenceBoxes.Count > 0)
            {
                GameObject fc = new GameObject("CloturesCollisions");
                fc.transform.SetParent(root.transform, false);
                foreach (Vector4 fb in fenceBoxes)
                {
                    GameObject seg = new GameObject("Cloture");
                    seg.transform.SetParent(fc.transform, false);
                    seg.transform.position = new Vector3(fb.x, 1.25f, fb.y);
                    seg.transform.rotation = Quaternion.Euler(0f, fb.z, 0f);
                    seg.AddComponent<BoxCollider>().size = new Vector3(0.3f, 2.5f, fb.w);
                }
            }
            // Modèles 3D des monuments (convertis par scripts/convertir_monuments.py)
            List<Vector3> clearZones = new List<Vector3>();
            foreach (Enclosure e in enclosures)
            {
                float rr = Mathf.Min(e.Hx, e.Hz) + 4f;
                for (float t = -(e.Hx - Mathf.Min(e.Hx, e.Hz)); t <= e.Hx - Mathf.Min(e.Hx, e.Hz) + 0.01f; t += Mathf.Max(10f, rr * 0.5f))
                {
                    Vector2 zc = e.C + e.X * t;
                    clearZones.Add(new Vector3(zc.x, rr, zc.y));
                }
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(BalulaSetup.Root + "/Modeles/Monuments/" + e.Model + ".obj");
                if (model == null) { report.Append("Modèle absent : ").Append(e.Model).Append(".obj (monument non posé).\n"); continue; }
                GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
                inst.name = "Monument_" + e.Model;
                inst.transform.SetParent(root.transform, false);
                inst.transform.position = new Vector3(e.C.x, -e.Sink, e.C.y);
                inst.transform.rotation = Quaternion.Euler(0f, e.Yaw, 0f);
                int parts = 0, verts = 0;
                foreach (MeshRenderer mr in inst.GetComponentsInChildren<MeshRenderer>())
                {
                    Material[] src = mr.sharedMaterials;
                    for (int i = 0; i < src.Length; i++)
                    {
                        if (src[i] == null) continue;
                        Color mc = src[i].HasProperty("_Color") ? src[i].color : Color.gray;
                        src[i] = Mat(mats, "Monu_" + e.Model + "_" + Safe(src[i].name), shader, mc, matAsset, ref firstMat);
                    }
                    mr.sharedMaterials = src;
                    MeshFilter mf = mr.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                    {
                        verts += mf.sharedMesh.vertexCount;
                        if (mr.GetComponent<Collider>() == null) mr.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                    }
                    parts++;
                }
                Bounds mb = new Bounds(inst.transform.position, Vector3.zero);
                foreach (Renderer r in inst.GetComponentsInChildren<Renderer>()) mb.Encapsulate(r.bounds);
                report.Append("   ").Append(e.Model).Append(" : ").Append(parts).Append(" parties, ").Append(verts).Append(" sommets, dimensions ")
                      .Append(mb.size.x.ToString("0")).Append(" x ").Append(mb.size.z.ToString("0")).Append(" m, hauteur ").Append(mb.size.y.ToString("0")).Append(" m.\n");
            }
            // Fenêtres éclairées la nuit (shader BalulaMoto/Lumiere)
            if (lights.Count > 0)
            {
                Shader ls = AssetDatabase.LoadAssetAtPath<Shader>(BalulaSetup.Root + "/Resources/BalulaMoto/Shaders/Lumiere.shader");
                if (ls != null)
                {
                    Material lmat = new Material(ls) { name = "FenetresNuit" };
                    lmat.enableInstancing = false;
                    AssetDatabase.CreateAsset(lmat, matAsset + "/FenetresNuit.mat");
                    foreach (KeyValuePair<string, LightMesh> kv in lights)
                    {
                        LightMesh lm = kv.Value;
                        if (lm.V.Count == 0) continue;
                        Mesh mesh = new Mesh { name = "Fenetres_" + kv.Key };
                        if (lm.V.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
                        mesh.SetVertices(lm.V);
                        mesh.SetColors(lm.C);
                        mesh.SetUVs(0, lm.UV);
                        mesh.SetTriangles(lm.T, 0);
                        mesh.RecalculateBounds();
                        if (firstMesh) { AssetDatabase.CreateAsset(mesh, meshAsset); firstMesh = false; }
                        else AssetDatabase.AddObjectToAsset(mesh, meshAsset);
                        GameObject lg = new GameObject("Fenetres_" + kv.Key);
                        lg.transform.SetParent(root.transform, false);
                        lg.AddComponent<MeshFilter>().sharedMesh = mesh;
                        MeshRenderer lr = lg.AddComponent<MeshRenderer>();
                        lr.sharedMaterial = lmat;
                        lr.shadowCastingMode = ShadowCastingMode.Off;
                        lr.receiveShadows = false;
                        idx++;
                    }
                }
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
            iw.roads = roadAxes;
            iw.buildingSpots = buildingSpots;
            iw.works = works;
            iw.clearZones = clearZones;
            iw.waterways = waterAxes;
            report.Append("Eau : ").Append(waterWays).Append(" cours d'eau tracés (").Append((waterLen / 1000f).ToString("0.0")).Append(" km). Ponts : ").Append(bridgeWays)
                  .Append(" tronçons marqués « pont » (").Append((bridgeLen / 1000f).ToString("0.00")).Append(" km) avec parapets. Fenêtres éclairées la nuit : ").Append(litWindows)
                  .Append(". Bâtiments OSM écartés des terrains des monuments : ").Append(skippedLand).Append(".\n");
            report.Append("Import ").Append(System.DateTime.Now.ToString("yyyy-MM-dd HH:mm")).Append(" — ").Append(Path.GetFileName(osmPath)).Append('\n');
            report.Append("Origine ").Append(originLat.ToString(CultureInfo.InvariantCulture)).Append(", ").Append(originLon.ToString(CultureInfo.InvariantCulture)).Append(" · rayon ").Append(radius.ToString("0")).Append(" m\n");
            report.Append("Stations-service : ").Append(stationLots.Count).Append(" construites (").Append(duplicateFuel.Count).Append(" doublons fusionnés) ; bâtiments OSM écartés : ")
                  .Append(skippedStations).Append(" sur les stations, ").Append(skippedRoads).Append(" posés sur une chaussée. Bords de rues (caniveaux, bordures) : ")
                  .Append((edgeLength / 1000f).ToString("0")).Append(" km (").Append(edgeVerts).Append(" sommets). Chantiers : ").Append(works.Count).Append(".\n");
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

        // ------------------------------------------------------------------
        // Stations-service, bâtiments en travers des rues, bords de rues, chantiers
        // ------------------------------------------------------------------
        private readonly Dictionary<string, KeyValuePair<Vector4, bool>> stationLots = new Dictionary<string, KeyValuePair<Vector4, bool>>();
        private readonly HashSet<string> duplicateFuel = new HashSet<string>();

        private void ApplyStation(ImportedPlace ip, string key)
        {
            KeyValuePair<Vector4, bool> lot;
            if (duplicateFuel.Contains(key)) { ip.isFuel = false; return; }
            if (!stationLots.TryGetValue(key, out lot)) return;
            ip.stationOrigin = new Vector3(lot.Key.x, 0f, lot.Key.y);
            ip.stationYaw = Mathf.Atan2(lot.Key.z, lot.Key.w) * Mathf.Rad2Deg;
            ip.stationSize = FuelStations.LotSize(lot.Value);
        }

        private static bool InStationLot(List<Vector4> lots, List<Vector2> pts, Vector2 center)
        {
            foreach (Vector4 l in lots)
            {
                Vector2 o = new Vector2(l.x, l.y), n = new Vector2(l.z, l.w);
                Vector2 size = FuelStations.LotSize(true);
                if ((center - (o + n * size.y * 0.5f)).sqrMagnitude > 60f * 60f) continue;
                if (FuelStations.InLot(o, n, size, center, 2f)) return true;
                foreach (Vector2 q in pts) if (FuelStations.InLot(o, n, size, q, 0.5f)) return true;
            }
            return false;
        }

        private static float SegDist(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector2.Distance(p, a + ab * t);
        }

        private static bool SegCross(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            float d1 = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
            float d2 = (b.x - a.x) * (d.y - a.y) - (b.y - a.y) * (d.x - a.x);
            float d3 = (d.x - c.x) * (a.y - c.y) - (d.y - c.y) * (a.x - c.x);
            float d4 = (d.x - c.x) * (b.y - c.y) - (d.y - c.y) * (b.x - c.x);
            return d1 * d2 < 0f && d3 * d4 < 0f;
        }

        /// <summary>Vrai si le bâtiment est posé sur une chaussée (l'axe le traverse, ou il avance de plus d'un mètre
        /// sur la chaussée) : on aurait roulé « dans un tunnel » à travers ses murs.</summary>
        private static bool OnCarriageway(Dictionary<long, List<Vector4>> grid, Dictionary<long, List<float>> hws, List<Vector2> pts, Vector2 center)
        {
            Vector2 mn = pts[0], mx = pts[0];
            foreach (Vector2 q in pts) { mn = Vector2.Min(mn, q); mx = Vector2.Max(mx, q); }
            int x0 = Mathf.FloorToInt((mn.x - 8f) / 50f), x1 = Mathf.FloorToInt((mx.x + 8f) / 50f);
            int z0 = Mathf.FloorToInt((mn.y - 8f) / 50f), z1 = Mathf.FloorToInt((mx.y + 8f) / 50f);
            for (int gz = z0; gz <= z1; gz++)
                for (int gx = x0; gx <= x1; gx++)
                {
                    List<Vector4> l;
                    if (!grid.TryGetValue(((long)gx << 32) ^ (uint)gz, out l)) continue;
                    List<float> hw = hws[((long)gx << 32) ^ (uint)gz];
                    for (int k = 0; k < l.Count; k++)
                    {
                        Vector2 a = new Vector2(l[k].x, l[k].y), b = new Vector2(l[k].z, l[k].w);
                        if (SegDist(center, a, b) < hw[k]) return true;
                        foreach (Vector2 q in pts) if (SegDist(q, a, b) < hw[k] - 1f) return true;
                        for (int i = 0; i < pts.Count; i++)
                            if (SegCross(a, b, pts[i], pts[(i + 1) % pts.Count])) return true;
                    }
                }
            return false;
        }

        /// <summary>Caniveau de béton (bordure, fond sombre, bordure extérieure) et accotement de terre tassée des deux
        /// côtés des rues goudronnées et des grandes avenues ; bordures peintes rouge et blanc sur les boulevards.</summary>
        private static long PointKey(Vector2 p) { return ((long)Mathf.RoundToInt(p.x * 10f) << 32) ^ (uint)Mathf.RoundToInt(p.y * 10f); }

        private float RoadEdges(ImportedRoad axis, Dictionary<string, MeshBatch> edges, HashSet<long> junctions, List<Vector4> lots)
        {
            SurfaceKind s = (SurfaceKind)axis.surface;
            bool paved = s == SurfaceKind.Goudron || s == SurfaceKind.GoudronDegrade;
            if (axis.rank < 3 || (!paved && axis.rank < 5)) return 0f;
            string nn = TextUtil.Normalize(axis.name ?? "");
            bool boulevard = axis.rank >= 8 || nn.Contains("boulevard") || nn.StartsWith("bd ");
            float hw = axis.width * 0.5f, yRoad = 0.03f + axis.rank * 0.004f;
            const float curbW = 0.18f, channel = 0.5f, curbTop = 0.13f, bottom = 0.012f, shoulder = 1.3f;
            float dist = 0f, total = 0f;
            for (int i = 0; i < axis.pts.Count - 1; i++)
            {
                Vector2 a = axis.pts[i], b = axis.pts[i + 1];
                float len = Vector2.Distance(a, b);
                if (len < 0.5f) { dist += len; continue; }
                Vector2 dir = (b - a) / len, nrm = new Vector2(-dir.y, dir.x);
                // Arrêt avant les carrefours (rues qui se croisent) pour ne pas les barrer
                float trimA = junctions.Contains(PointKey(a)) ? Mathf.Min(len * 0.45f, 7f) : 0f;
                float trimB = junctions.Contains(PointKey(b)) ? Mathf.Min(len * 0.45f, 7f) : 0f;
                float usable = len - trimA - trimB;
                if (usable < 0.5f) { dist += len; continue; }
                int pieces = Mathf.Max(1, Mathf.CeilToInt(usable / 30f));
                for (int k = 0; k < pieces; k++)
                {
                    float t0 = trimA + usable * k / pieces, t1 = trimA + usable * (k + 1) / pieces;
                    Vector2 p0 = a + dir * t0, p1 = a + dir * t1;
                    float u0 = (dist + t0) * 0.5f, u1 = (dist + t1) * 0.5f;
                    Vector2Int c = Cell((p0 + p1) * 0.5f);
                    string key = c.x + "_" + c.y + "_";
                    foreach (float side in new[] { 1f, -1f })
                    {
                        Vector2 o = nrm * side;
                        // Pas de caniveau devant une station-service (entrée et sortie des véhicules)
                        Vector2 probe = (p0 + p1) * 0.5f + o * (hw + 1.2f);
                        bool atStation = false;
                        foreach (Vector4 l in lots)
                            if (FuelStations.InLot(new Vector2(l.x, l.y), new Vector2(l.z, l.w), FuelStations.LotSize(true), probe, 3f)) { atStation = true; break; }
                        if (atStation) continue;
                        Vector3 outward = new Vector3(o.x, 0f, o.y);
                        MeshBatch curb = Batch(edges, key + (boulevard ? "rb" : "beton"));
                        MeshBatch concrete = Batch(edges, key + "beton");
                        MeshBatch floor = Batch(edges, key + "fond");
                        MeshBatch earth = Batch(edges, key + "accot");
                        // Bordure côté chaussée : face vers la route, dessus
                        Side(curb, p0, p1, o, hw, yRoad - 0.01f, curbTop, -outward, u0, u1);
                        Top(curb, p0, p1, o, hw, hw + curbW, curbTop, u0, u1);
                        // Caniveau : paroi intérieure, fond sombre, paroi et dessus de la bordure extérieure
                        Side(concrete, p0, p1, o, hw + curbW, bottom, curbTop, outward, u0, u1);
                        Top(floor, p0, p1, o, hw + curbW, hw + curbW + channel, bottom, u0, u1);
                        Side(concrete, p0, p1, o, hw + curbW + channel, bottom, curbTop, -outward, u0, u1);
                        Top(concrete, p0, p1, o, hw + curbW + channel, hw + 2f * curbW + channel, curbTop, u0, u1);
                        // Accotement de terre tassée (terrassé) au-delà du caniveau
                        Top(earth, p0, p1, o, hw + 2f * curbW + channel, hw + 2f * curbW + channel + shoulder, 0.006f, u0, u1);
                        total += t1 - t0;
                    }
                }
                dist += len;
            }
            return total;
        }

        private static void Top(MeshBatch m, Vector2 p0, Vector2 p1, Vector2 o, float r0, float r1, float y, float u0, float u1)
        {
            Vector2 a = p0 + o * r0, b = p1 + o * r0, c = p1 + o * r1, d = p0 + o * r1;
            m.QuadFacing(V3(a, y), V3(b, y), V3(c, y), V3(d, y), new Vector2(u0, 0f), new Vector2(u1, 0f), new Vector2(u1, 1f), new Vector2(u0, 1f), Vector3.up);
        }

        private static void Side(MeshBatch m, Vector2 p0, Vector2 p1, Vector2 o, float r, float y0, float y1, Vector3 facing, float u0, float u1)
        {
            Vector2 a = p0 + o * r, b = p1 + o * r;
            m.QuadFacing(V3(a, y0), V3(b, y0), V3(b, y1), V3(a, y1), new Vector2(u0, 0f), new Vector2(u1, 0f), new Vector2(u1, 1f), new Vector2(u0, 1f), facing);
        }

        /// <summary>Texture 2×1 (rouge, blanc) répétée le long des bordures des boulevards : 1 m rouge, 1 m blanc.</summary>
        private static Texture2D StripeTexture(string folder)
        {
            Texture2D t = new Texture2D(2, 1, TextureFormat.RGBA32, false);
            t.SetPixel(0, 0, new Color(0.82f, 0.08f, 0.08f));
            t.SetPixel(1, 0, new Color(0.96f, 0.96f, 0.94f));
            t.filterMode = FilterMode.Point;
            t.wrapMode = TextureWrapMode.Repeat;
            t.Apply();
            AssetDatabase.CreateAsset(t, folder + "/BordureRougeBlanc.asset");
            return t;
        }

        /// <summary>Choisit des portions de rues goudronnées en travaux (régulièrement réparties) et y pose une
        /// chaussée défoncée (sable, gravier, trous) ; les barrières, cônes, tas de sable et engins sont ajoutés en jeu.</summary>
        private List<WorkZone> ChooseWorks(List<ImportedRoad> axes, Dictionary<string, MeshBatch> roadBatches)
        {
            List<WorkZone> list = new List<WorkZone>();
            System.Random rnd = new System.Random(4171);
            List<KeyValuePair<int, int>> candidates = new List<KeyValuePair<int, int>>();
            for (int r = 0; r < axes.Count; r++)
            {
                ImportedRoad a = axes[r];
                SurfaceKind s = (SurfaceKind)a.surface;
                if (a.rank < 3 || a.rank > 7 || (s != SurfaceKind.Goudron && s != SurfaceKind.GoudronDegrade)) continue;
                for (int i = 0; i < a.pts.Count - 1; i++)
                    if (Vector2.Distance(a.pts[i], a.pts[i + 1]) >= 70f) candidates.Add(new KeyValuePair<int, int>(r, i));
            }
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = rnd.Next(i + 1);
                KeyValuePair<int, int> t = candidates[i]; candidates[i] = candidates[j]; candidates[j] = t;
            }
            foreach (KeyValuePair<int, int> cand in candidates)
            {
                if (list.Count >= 40) break;
                ImportedRoad a = axes[cand.Key];
                Vector2 p = a.pts[cand.Value], q = a.pts[cand.Value + 1];
                Vector2 c = (p + q) * 0.5f;
                if (c.sqrMagnitude < 150f * 150f) continue; // pas devant le point de départ
                bool near = false;
                foreach (WorkZone z in list) if ((new Vector2(z.center.x, z.center.z) - c).sqrMagnitude < 420f * 420f) { near = true; break; }
                if (near) continue;
                Vector2 dir = (q - p).normalized, nrm = new Vector2(-dir.y, dir.x);
                float len = Mathf.Min(48f, Vector2.Distance(p, q) - 16f);
                float hw = a.width * 0.5f, y = 0.03f + a.rank * 0.004f + 0.012f;
                Vector2Int cell = Cell(c);
                MeshBatch mb = Batch(roadBatches, cell.x + "_" + cell.y + "_" + SurfaceKind.Travaux);
                int pieces = Mathf.CeilToInt(len / 8f);
                for (int k = 0; k < pieces; k++)
                {
                    Vector2 p0 = c + dir * (-len * 0.5f + len * k / pieces), p1 = c + dir * (-len * 0.5f + len * (k + 1) / pieces);
                    // Bosses et creux : la chaussée en travaux n'est pas plane
                    float y0 = y + (k % 2 == 0 ? 0.02f : 0f), y1 = y + (k % 2 == 0 ? 0f : 0.02f);
                    mb.Quad(V3(p0 + nrm * hw, y0), V3(p1 + nrm * hw, y1), V3(p1 - nrm * hw, y1), V3(p0 - nrm * hw, y0));
                }
                list.Add(new WorkZone { road = a.name, center = V3(c, y), dir = new Vector3(dir.x, 0f, dir.y), length = len, width = a.width });
            }
            return list;
        }

        private static Color SurfaceColor(SurfaceKind s)
        {
            switch (s)
            {
                case SurfaceKind.Goudron: return new Color(0.22f, 0.22f, 0.24f);
                case SurfaceKind.GoudronDegrade: return new Color(0.3f, 0.29f, 0.28f);
                case SurfaceKind.NidsDePoule: return new Color(0.42f, 0.36f, 0.3f);
                case SurfaceKind.Poussiere: return new Color(0.55f, 0.42f, 0.29f);
                case SurfaceKind.Boue: return new Color(0.22f, 0.15f, 0.1f);
                case SurfaceKind.Travaux: return new Color(0.5f, 0.41f, 0.31f);
                default: return new Color(0.42f, 0.29f, 0.19f);
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
            // Lissé : « _Smoothness » (URP) et « _Glossiness » (Standard). Sans le second, le Standard gardait 0,5 :
            // sol, routes et murs brillaient et reflétaient le ciel (terre éclaircie, reflets bleu-gris).
            float smooth = key.StartsWith("RouteGoudron") ? 0.25f : (key == "Eau" ? 0.35f : (key == "GrilleFer" ? 0.35f : 0.08f));
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
            // Pas d'instanciation GPU : sur certains téléphones Android (PowerVR, Adreno), les objets instanciés ne sont pas dessinés
            m.enableInstancing = false;
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
