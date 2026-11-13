// BALULA MOTO — plan de la ville (mini-carte et carte plein écran) : les rues réelles importées d'OpenStreetMap
// (ou la piste d'essai) sont dessinées une fois dans une texture au chargement, avec les bâtiments en gris.
// Les repères (clients, arrêts, stations, garages, lieux du créateur, destination) sont ajoutés par l'interface.
using System.Collections.Generic;
using BalulaMoto.Core;
using UnityEngine;

namespace BalulaMoto.Runtime
{
    public enum MapIconKind { Client, Arret, Carburant, Garage, Atelier, Concession, Lieu, Destination, Police }

    public struct MapIcon
    {
        public MapIconKind Kind;
        public Vector3 Position;
        public string Label;
    }

    public static class CityMap
    {
        public static Texture2D Texture { get; private set; }
        public static float MinX { get; private set; }
        public static float MinZ { get; private set; }
        public static float Size { get; private set; }
        public static bool Ready { get { return Texture != null; } }
        public static string Name = "Kinshasa";

        private static readonly Color32 Ground = new Color32(34, 40, 46, 255);
        private static readonly Color32 BuildingCol = new Color32(58, 64, 72, 255);
        private static readonly Color32 MinorRoad = new Color32(150, 156, 164, 255);
        private static readonly Color32 Street = new Color32(206, 210, 216, 255);
        private static readonly Color32 Avenue = new Color32(250, 204, 72, 255);
        private static readonly Color32 Highway = new Color32(255, 150, 60, 255);
        private static readonly Color32 Border = new Color32(20, 22, 26, 255);

        public static Color IconColor(MapIconKind k)
        {
            switch (k)
            {
                case MapIconKind.Client: return new Color(0.2f, 0.85f, 0.3f);
                case MapIconKind.Arret: return new Color(0.15f, 0.5f, 1f);
                case MapIconKind.Carburant: return new Color(1f, 0.55f, 0.05f);
                case MapIconKind.Garage: return new Color(0.75f, 0.35f, 1f);
                case MapIconKind.Atelier: return new Color(0.55f, 0.6f, 0.68f);
                case MapIconKind.Concession: return new Color(0f, 0.75f, 0.75f);
                case MapIconKind.Lieu: return new Color(1f, 0.85f, 0.1f);
                case MapIconKind.Destination: return new Color(0.95f, 0.15f, 0.15f);
                case MapIconKind.Police: return new Color(0.1f, 0.2f, 0.55f);
            }
            return Color.white;
        }

        public static string IconLetter(MapIconKind k)
        {
            switch (k)
            {
                case MapIconKind.Client: return "C";
                case MapIconKind.Arret: return "A";
                case MapIconKind.Carburant: return "E";
                case MapIconKind.Garage: return "G";
                case MapIconKind.Atelier: return "M";
                case MapIconKind.Concession: return "V";
                case MapIconKind.Lieu: return "L";
                case MapIconKind.Destination: return "D";
                case MapIconKind.Police: return "P";
            }
            return "?";
        }

        public static string IconName(MapIconKind k)
        {
            switch (k)
            {
                case MapIconKind.Client: return "Client qui attend";
                case MapIconKind.Arret: return "Arrêt";
                case MapIconKind.Carburant: return "Station-service / carburant";
                case MapIconKind.Garage: return "Garage";
                case MapIconKind.Atelier: return "Atelier mécanique";
                case MapIconKind.Concession: return "Magasin de véhicules";
                case MapIconKind.Lieu: return "Lieu de référence";
                case MapIconKind.Destination: return "Destination du client";
                case MapIconKind.Police: return "Contrôle de police";
            }
            return "";
        }

        public static Vector2 ToUV(Vector3 world)
        {
            return new Vector2((world.x - MinX) / Size, (world.z - MinZ) / Size);
        }

        /// <summary>Dessine le plan. Avec une carte importée : ses rues et ses bâtiments ; sinon les boucles de circulation.</summary>
        public static void Build(ImportedWorld map, WorldContext world)
        {
            if (Texture != null) Object.Destroy(Texture);
            Texture = null;
            int res = GameSettings.Quality == 0 ? 1024 : (GameSettings.Quality == 1 ? 1536 : 2048);
            float half;
            if (map != null)
            {
                half = Mathf.Max(map.halfSize.x, map.halfSize.y) + 60f;
                Name = string.IsNullOrEmpty(map.worldName) ? "Kinshasa" : map.worldName;
            }
            else
            {
                half = 200f;
                if (world != null)
                    foreach (TrafficPath tp in world.TrafficPaths)
                        foreach (Vector3 p in tp.Points) half = Mathf.Max(half, Mathf.Abs(p.x) + 60f, Mathf.Abs(p.z) + 60f);
                Name = world != null ? world.Name : "Carte";
            }
            MinX = -half;
            MinZ = -half;
            Size = half * 2f;
            float mpp = Size / res;
            Color32[] px = new Color32[res * res];
            for (int i = 0; i < px.Length; i++) px[i] = Ground;

            if (map != null)
            {
                foreach (Vector3 b in map.buildingSpots)
                    Disc(px, res, (b.x - MinX) / mpp, (b.z - MinZ) / mpp, Mathf.Max(0.6f, b.y * 0.75f / mpp), BuildingCol);
                // Rues : d'abord les petites, puis les grandes par-dessus (bordure sombre puis remplissage)
                for (int pass = 0; pass < 3; pass++)
                    foreach (ImportedRoad r in map.roads)
                    {
                        int tier = r.rank >= 5 ? 2 : (r.rank >= 3 ? 1 : 0);
                        if (tier != pass) continue;
                        float w = Mathf.Clamp(r.width / mpp * 1.3f, tier == 2 ? 2.2f : (tier == 1 ? 1.4f : 0.9f), 6f);
                        Color32 c = r.rank >= 8 ? Highway : (tier == 2 ? Avenue : (tier == 1 ? Street : MinorRoad));
                        if (tier == 2) Polyline(px, res, r.pts, mpp, w * 0.5f + 0.9f, Border);
                        Polyline(px, res, r.pts, mpp, w * 0.5f, c);
                    }
            }
            else if (world != null)
            {
                foreach (TrafficPath tp in world.TrafficPaths)
                {
                    List<Vector2> pts = new List<Vector2>(tp.Points.Count);
                    foreach (Vector3 p in tp.Points) pts.Add(new Vector2(p.x, p.z));
                    Polyline(px, res, pts, mpp, Mathf.Max(1.5f, 4f / mpp), Street);
                }
            }

            Texture2D t = new Texture2D(res, res, TextureFormat.RGBA32, true);
            t.name = "PlanVille";
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Trilinear;
            t.anisoLevel = 2;
            t.SetPixels32(px);
            t.Apply(true, true);
            Texture = t;
        }

        private static void Polyline(Color32[] px, int res, List<Vector2> pts, float mpp, float radius, Color32 c)
        {
            for (int i = 0; i < pts.Count - 1; i++)
            {
                float ax = (pts[i].x - MinX) / mpp, ay = (pts[i].y - MinZ) / mpp;
                float bx = (pts[i + 1].x - MinX) / mpp, by = (pts[i + 1].y - MinZ) / mpp;
                float len = Mathf.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
                int steps = Mathf.Max(1, Mathf.CeilToInt(len / Mathf.Max(0.5f, radius * 0.6f)));
                for (int k = 0; k <= steps; k++)
                {
                    float t = k / (float)steps;
                    Disc(px, res, ax + (bx - ax) * t, ay + (by - ay) * t, radius, c);
                }
            }
        }

        private static void Disc(Color32[] px, int res, float cx, float cy, float r, Color32 c)
        {
            int x0 = Mathf.FloorToInt(cx - r), x1 = Mathf.CeilToInt(cx + r);
            int y0 = Mathf.FloorToInt(cy - r), y1 = Mathf.CeilToInt(cy + r);
            if (x1 < 0 || y1 < 0 || x0 >= res || y0 >= res) return;
            x0 = Mathf.Max(0, x0); y0 = Mathf.Max(0, y0);
            x1 = Mathf.Min(res - 1, x1); y1 = Mathf.Min(res - 1, y1);
            float rr = Mathf.Max(0.36f, r * r);
            for (int y = y0; y <= y1; y++)
            {
                float dy = y + 0.5f - cy;
                int row = y * res;
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x + 0.5f - cx;
                    if (dx * dx + dy * dy <= rr) px[row + x] = c;
                }
            }
        }

        // ------------------------------------------------------------------
        // Repères à afficher (calculés à la demande par la mini-carte et la carte)
        // ------------------------------------------------------------------
        private static readonly List<LandmarkPin> pins = new List<LandmarkPin>();
        private static readonly List<PoliceCheckpoint> checkpoints = new List<PoliceCheckpoint>();
        private static float pinsTime = -100f;

        public static void CollectIcons(List<MapIcon> output, GameplayDirector g)
        {
            output.Clear();
            if (Time.unscaledTime - pinsTime > 10f)
            {
                pinsTime = Time.unscaledTime;
                pins.Clear();
                pins.AddRange(Object.FindObjectsByType<LandmarkPin>(FindObjectsSortMode.None));
                checkpoints.Clear();
                checkpoints.AddRange(Object.FindObjectsByType<PoliceCheckpoint>(FindObjectsSortMode.None));
            }
            foreach (ClientStop s in ClientStop.All)
            {
                if (s == null) continue;
                bool waiting = false;
                foreach (WaitingClient c in s.Clients) if (c != null) { waiting = true; break; }
                output.Add(new MapIcon { Kind = waiting ? MapIconKind.Client : MapIconKind.Arret, Position = s.transform.position, Label = s.Place != null ? s.Place.Name : "Arrêt" });
            }
            foreach (InteractionPoint ip in InteractionPoint.All)
            {
                if (ip == null) continue;
                MapIconKind k;
                switch (ip.Kind)
                {
                    case InteractionKind.Garage: k = MapIconKind.Garage; break;
                    case InteractionKind.Atelier: k = MapIconKind.Atelier; break;
                    case InteractionKind.Concession: k = MapIconKind.Concession; break;
                    default: k = MapIconKind.Carburant; break;
                }
                output.Add(new MapIcon { Kind = k, Position = ip.transform.position, Label = ip.Label });
            }
            foreach (LandmarkPin p in pins)
                if (p != null) output.Add(new MapIcon { Kind = MapIconKind.Lieu, Position = p.transform.position, Label = PinName(p) });
            foreach (PoliceCheckpoint pc in checkpoints)
                if (pc != null && pc.gameObject.activeInHierarchy) output.Add(new MapIcon { Kind = MapIconKind.Police, Position = pc.transform.position, Label = "Contrôle" });
            if (g != null && g.State == RideState.EnCourse && g.Client != null)
                output.Add(new MapIcon { Kind = MapIconKind.Destination, Position = WorldGeo.ToWorld(g.Client.Destination, 0f), Label = g.Client.Destination.Name });
        }

        private static readonly Dictionary<LandmarkPin, string> pinNames = new Dictionary<LandmarkPin, string>();

        private static string PinName(LandmarkPin p)
        {
            string n;
            if (pinNames.TryGetValue(p, out n)) return n;
            n = p.LocationId;
            TextMesh tm = p.GetComponentInChildren<TextMesh>();
            if (tm != null && !string.IsNullOrEmpty(tm.text)) n = tm.text.Split('\n')[0];
            pinNames[p] = n;
            return n;
        }
    }
}
