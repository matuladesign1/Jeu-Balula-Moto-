// BALULA MOTO — objets du monde : surfaces, points d'interaction, arrêts et clients, marqueurs, police, carte importée.
using System.Collections.Generic;
using BalulaMoto.Core;
using UnityEngine;

namespace BalulaMoto.Runtime
{
    /// <summary>Type de chaussée d'un collisionneur (lu par les roues).</summary>
    public class SurfaceZone : MonoBehaviour
    {
        public SurfaceKind Kind = SurfaceKind.Goudron;
    }

    public enum InteractionKind { StationService, PointVente, Kadafi, Atelier, Garage, Concession }

    public class InteractionPoint : MonoBehaviour
    {
        public static readonly List<InteractionPoint> All = new List<InteractionPoint>();
        public InteractionKind Kind;
        public string VendorId;
        public string Label;
        public float Radius = 8f;

        private void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        private void OnDisable() { All.Remove(this); }

        public string ActionLabel()
        {
            switch (Kind)
            {
                case InteractionKind.StationService:
                case InteractionKind.PointVente:
                case InteractionKind.Kadafi: return "Faire le plein — " + Label;
                case InteractionKind.Atelier: return "Entrer à l'atelier";
                case InteractionKind.Garage: return "Entrer au garage";
                case InteractionKind.Concession: return "Voir le magasin de véhicules";
            }
            return Label;
        }

        public static InteractionPoint Create(Transform parent, InteractionKind kind, string vendorId, string label, Vector3 pos, float radius)
        {
            GameObject g = new GameObject("Interaction_" + kind);
            g.transform.SetParent(parent, false);
            g.transform.position = pos;
            InteractionPoint ip = g.AddComponent<InteractionPoint>();
            ip.Kind = kind;
            ip.VendorId = vendorId;
            ip.Label = label;
            ip.Radius = radius;
            // Repère au sol (cercle jaune) pour le retrouver facilement
            GameObject ring = Prims.Cyl(g.transform, "Repere", new Vector3(0f, 0.05f, 0f), radius * 0.9f, 0.02f, new Color(1f, 0.8f, 0.1f));
            ring.GetComponent<Renderer>().sharedMaterial = Mats.Unlit(new Color(1f, 0.82f, 0.15f));
            return ip;
        }
    }

    /// <summary>Client qui attend à un arrêt (fait signe quand le joueur approche).</summary>
    public class WaitingClient : MonoBehaviour
    {
        public ClientRequest Request;
        public ClientStop Stop;
        public HumanFigure Figure;
        public float Lifetime;

        private void Update()
        {
            Lifetime -= Time.deltaTime;
            if (Figure == null) return;
            GameplayDirector d = GameplayDirector.Instance;
            bool hail = d != null && d.Player != null && d.State == RideState.Libre &&
                        (d.Player.transform.position - transform.position).sqrMagnitude < 35f * 35f;
            Figure.Hailing = hail;
            if (hail)
            {
                Vector3 look = d.Player.transform.position - transform.position;
                look.y = 0f;
                if (look.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look), Time.deltaTime * 3f);
            }
        }
    }

    /// <summary>Arrêt de transport : des clients y apparaissent selon l'heure et la réputation.</summary>
    public class ClientStop : MonoBehaviour
    {
        public static readonly List<ClientStop> All = new List<ClientStop>();
        public PlaceRef Place;
        public readonly List<WaitingClient> Clients = new List<WaitingClient>();
        public int MaxClients = 2;
        public float Timer;

        private void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        private void OnDisable() { All.Remove(this); }

        public static ClientStop Create(Transform parent, PlaceRef place, bool shelter)
        {
            GameObject g = new GameObject("Arret_" + place.Name);
            g.transform.SetParent(parent, false);
            g.transform.position = WorldGeo.ToWorld(place, 0f);
            ClientStop s = g.AddComponent<ClientStop>();
            s.Place = place;
            s.Timer = Random.Range(1f, 8f);
            if (shelter)
            {
                Transform t = g.transform;
                Color blue = new Color(0.1f, 0.35f, 0.75f);
                Prims.Box(t, "PoteauA", new Vector3(-1.4f, 1.2f, 0.6f), new Vector3(0.08f, 2.4f, 0.08f), Color.gray);
                Prims.Box(t, "PoteauB", new Vector3(1.4f, 1.2f, 0.6f), new Vector3(0.08f, 2.4f, 0.08f), Color.gray);
                Prims.Box(t, "Toit", new Vector3(0f, 2.45f, 0.3f), new Vector3(3.2f, 0.08f, 1.4f), blue);
                Prims.Box(t, "Banc", new Vector3(0f, 0.45f, 0.7f), new Vector3(2.4f, 0.08f, 0.4f), new Color(0.4f, 0.3f, 0.2f));
                GameObject l = Prims.Label(t, "ARRÊT\n" + place.Name, new Vector3(0f, 2.9f, 0.3f), 0.05f, Color.white, true);
                l.transform.localRotation = Quaternion.identity;
            }
            return s;
        }

        public void Cleanup()
        {
            for (int i = Clients.Count - 1; i >= 0; i--)
            {
                WaitingClient c = Clients[i];
                if (c == null) { Clients.RemoveAt(i); continue; }
                if (c.Lifetime <= 0f) { Destroy(c.gameObject); Clients.RemoveAt(i); }
            }
        }

        public WaitingClient AddClient(ClientRequest request, int seed)
        {
            Vector3 offset = new Vector3((Clients.Count == 0 ? -0.8f : 0.8f), 0f, 1.2f);
            GameObject h = VehicleBuilder.BuildHuman(transform, offset, request.Outfit, false, seed, false);
            WaitingClient wc = h.AddComponent<WaitingClient>();
            wc.Request = request;
            wc.Stop = this;
            wc.Figure = h.GetComponent<HumanFigure>();
            wc.Lifetime = Random.Range(90f, 200f);
            if (request.CargoKg > 0f)
                Prims.Box(h.transform, "Colis", new Vector3(0.45f, 0.25f, 0f), new Vector3(0.5f, 0.5f, 0.4f), new Color(0.55f, 0.4f, 0.25f));
            Clients.Add(wc);
            return wc;
        }

        public void RemoveClient(WaitingClient c)
        {
            Clients.Remove(c);
            if (c != null) Destroy(c.gameObject);
        }
    }

    /// <summary>Colonne lumineuse au-dessus de la destination.</summary>
    public class DestinationMarker : MonoBehaviour
    {
        private Transform beam;
        private float t;

        public static DestinationMarker Create(Vector3 position, Color color, string label)
        {
            GameObject g = new GameObject("Destination");
            g.transform.position = position;
            DestinationMarker m = g.AddComponent<DestinationMarker>();
            GameObject b = Prims.Cyl(g.transform, "Colonne", new Vector3(0f, 15f, 0f), 2.2f, 30f, color);
            b.GetComponent<Renderer>().sharedMaterial = Mats.Unlit(color);
            Prims.NoShadows(b);
            m.beam = b.transform;
            GameObject ring = Prims.Cyl(g.transform, "Anneau", new Vector3(0f, 0.06f, 0f), 12f, 0.03f, color);
            ring.GetComponent<Renderer>().sharedMaterial = Mats.Unlit(color);
            Prims.Label(g.transform, label, new Vector3(0f, 6f, 0f), 0.12f, Color.white, true);
            return m;
        }

        private void Update()
        {
            t += Time.deltaTime;
            if (beam != null) beam.localScale = new Vector3(2.2f + Mathf.Sin(t * 3f) * 0.3f, 15f, 2.2f + Mathf.Sin(t * 3f) * 0.3f);
            Camera c = Camera.main;
            if (c != null)
            {
                Transform label = transform.Find("Texte");
                if (label != null) label.rotation = Quaternion.LookRotation(label.position - c.transform.position);
            }
        }
    }

    /// <summary>Contrôle routier fictif avec le bus Kabasele et deux agents.</summary>
    public class PoliceCheckpoint : MonoBehaviour
    {
        public float Radius = 120f;
        public Transform Agent;
        public AIVehicle Bus;
        public Vector3 BusHome;
        public Quaternion BusHomeRotation;

        public static PoliceCheckpoint Create(Transform parent, Vector3 position, float heading, TrafficManager traffic)
        {
            GameObject g = new GameObject("ControleRoutier");
            g.transform.SetParent(parent, false);
            g.transform.position = position;
            g.transform.rotation = Quaternion.Euler(0f, heading, 0f);
            PoliceCheckpoint cp = g.AddComponent<PoliceCheckpoint>();
            GameObject a = VehicleBuilder.BuildHuman(g.transform, new Vector3(0f, 0f, 0f), "police", false, 901, false);
            cp.Agent = a.transform;
            VehicleBuilder.BuildHuman(g.transform, new Vector3(1.2f, 0f, -0.8f), "roulage", false, 902, false);
            for (int i = 0; i < 3; i++)
                Prims.Cyl(g.transform, "Cone", new Vector3(-1.5f - i * 1.6f, 0.35f, 1.2f), 0.35f, 0.7f, new Color(1f, 0.4f, 0.05f));
            if (traffic != null)
            {
                cp.Bus = traffic.Spawn(AIVehicleType.PoliceKabasele, null, 0f, 0f);
                cp.Bus.Special = true;
                cp.BusHome = position + g.transform.rotation * new Vector3(4f, 0f, -6f);
                cp.BusHomeRotation = g.transform.rotation;
                cp.Bus.transform.SetPositionAndRotation(cp.BusHome, cp.BusHomeRotation);
            }
            return cp;
        }

        public void ReturnBus()
        {
            if (Bus == null) return;
            Bus.Pursuing = false;
            Bus.PursuitTarget = null;
            Bus.Path = null;
            Bus.Speed = 0f;
            Bus.Sirens = false;
            Bus.transform.SetPositionAndRotation(BusHome, BusHomeRotation);
        }
    }

    /// <summary>Repère d'un lieu de référence (statut de vérification visible).</summary>
    public class LandmarkPin : MonoBehaviour
    {
        public string LocationId;
        public string Status;

        public static LandmarkPin Create(Transform parent, string id, string name, string status, Vector3 position)
        {
            GameObject g = new GameObject("Repere_" + id);
            g.transform.SetParent(parent, false);
            g.transform.position = position;
            LandmarkPin p = g.AddComponent<LandmarkPin>();
            p.LocationId = id;
            p.Status = status;
            Color c = status == "verifiee" ? new Color(0.2f, 0.85f, 0.35f) : new Color(1f, 0.6f, 0.1f);
            Prims.Cyl(g.transform, "Mat", new Vector3(0f, 6f, 0f), 0.4f, 12f, c);
            Prims.Label(g.transform, name + "\n(" + (status == "verifiee" ? "position vérifiée" : "position à vérifier") + ")", new Vector3(0f, 13.5f, 0f), 0.1f, Color.white, true);
            return p;
        }
    }

    [System.Serializable]
    public class ImportedPlace
    {
        public string id;
        public string name;
        public string category;
        public string district;
        public Vector3 position;
        public bool isStop;
        public bool isFuel;
        public bool isLandmark;
        public int weight;
        public string status;
    }

    /// <summary>Boucle de circulation générée depuis une grande avenue OpenStreetMap (aller puis retour).</summary>
    [System.Serializable]
    public class ImportedPath
    {
        public string name;
        public List<Vector3> points = new List<Vector3>();
    }

    /// <summary>
    /// Carte réelle générée par l'outil d'import OpenStreetMap (menu BALULA MOTO). Quand ce composant
    /// est présent dans la scène, le jeu l'utilise à la place de la piste d'essai.
    /// </summary>
    public class ImportedWorld : MonoBehaviour
    {
        public string worldName = "Kinshasa";
        public double originLat;
        public double originLon;
        public Vector3 spawnPosition;
        public float spawnHeading;
        /// <summary>Demi-étendue de la carte importée (m), pour placer les autres zones à l'écart.</summary>
        public Vector2 halfSize = new Vector2(3000f, 3000f);
        public string attribution = "© les contributeurs d'OpenStreetMap — données sous licence ODbL";
        public List<ImportedPlace> places = new List<ImportedPlace>();
        public List<ImportedPath> trafficPaths = new List<ImportedPath>();
        /// <summary>Trottoirs (bords des rues OpenStreetMap) utilisés par la foule.</summary>
        public List<ImportedPath> walkPaths = new List<ImportedPath>();
        [TextArea(3, 12)] public string report;
    }
}
