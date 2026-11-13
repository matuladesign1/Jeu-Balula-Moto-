// BALULA MOTO — mondes jouables :
// 1) PISTE D'ESSAI : terrain de test des mécaniques (ce n'est PAS une représentation de Kinshasa) ;
// 2) CARTE IMPORTÉE : Kinshasa réelle générée depuis OpenStreetMap par l'outil d'import (menu BALULA MOTO).
using System.Collections.Generic;
using BalulaMoto.Core;
using UnityEngine;

namespace BalulaMoto.Runtime
{
    public class WorldContext
    {
        public string Name;
        public bool IsTestTrack;
        public string Attribution = "";
        public readonly List<PlaceRef> Places = new List<PlaceRef>();
        public Vector3 SpawnPosition;
        public Quaternion SpawnRotation = Quaternion.identity;
        public Vector3 MenuCenter;
        public TrafficLightController Junction;
        public PoliceCheckpoint Checkpoint;
        public readonly List<TrafficPath> TrafficPaths = new List<TrafficPath>();
        public readonly List<TrafficPath> WalkPaths = new List<TrafficPath>();
        public TrafficPath FollowPath;
        public float SpeedLimitKmh = 60f;

        // Zone de Matadi (Pont Maréchal), accessible quand le Kongo Central est débloqué
        public bool HasMatadi;
        public Vector3 MatadiSpawn;
        public Quaternion MatadiSpawnRotation = Quaternion.identity;
        public Vector3 MatadiCenter;

        /// <summary>Lieux d'une même zone : un client ne demande jamais une destination inaccessible par la route.</summary>
        public List<PlaceRef> ZonePlaces(string zone)
        {
            List<PlaceRef> list = new List<PlaceRef>();
            foreach (PlaceRef p in Places) if (p.Zone == zone) list.Add(p);
            return list;
        }

        public string ZoneAt(Vector3 pos)
        {
            return HasMatadi && (new Vector2(pos.x - MatadiCenter.x, pos.z - MatadiCenter.z)).sqrMagnitude < 1500f * 1500f ? "matadi" : "kinshasa";
        }

        public PlaceRef NearestPlace(Vector3 pos, float maxDistance)
        {
            PlaceRef best = null;
            double bestD = maxDistance * maxDistance;
            foreach (PlaceRef p in Places)
            {
                double dx = p.X - pos.x, dz = p.Z - pos.z;
                double d = dx * dx + dz * dz;
                if (d < bestD) { bestD = d; best = p; }
            }
            return best;
        }
    }

    public static class TestTrackBuilder
    {
        private static readonly Color Asphalt = new Color(0.22f, 0.22f, 0.24f);
        private static readonly Color AsphaltWorn = new Color(0.3f, 0.29f, 0.28f);
        private static readonly Color Dirt = new Color(0.62f, 0.45f, 0.3f);
        private static readonly Color Mud = new Color(0.33f, 0.23f, 0.15f);
        private static readonly Color Dust = new Color(0.76f, 0.63f, 0.46f);
        private static readonly Color Ground = new Color(0.58f, 0.46f, 0.32f);
        private static readonly Color Red = new Color(0.78f, 0.1f, 0.1f);
        private static readonly Color White = new Color(0.94f, 0.94f, 0.94f);

        private static Transform decor;
        private static System.Random rnd;
        private static readonly List<Vector3> reserved = new List<Vector3>();

        public static WorldContext Build(Transform root, TrafficManager traffic)
        {
            rnd = new System.Random(42);
            reserved.Clear();
            WorldContext w = new WorldContext { Name = "Piste d'essai BALULA MOTO", IsTestTrack = true, SpeedLimitKmh = 60f };
            GameObject decorGo = new GameObject("Decor");
            decorGo.transform.SetParent(root, false);
            decor = decorGo.transform;

            // Terrain (terre battue)
            GameObject ground = Prims.Box(root, "Sol", new Vector3(0f, -0.25f, 0f), new Vector3(900f, 0.5f, 900f), Ground, true);
            ground.AddComponent<SurfaceZone>().Kind = SurfaceKind.Terre;

            // --- Routes
            // Boulevard nord à 2 × 2 voies avec séparateur rouge et blanc (inspiré du profil du bd Lumumba, REF-KIN-008)
            Road(root, new Vector3(-312f, 0f, 200f), new Vector3(312f, 0f, 200f), 17f, SurfaceKind.Goudron, Asphalt, 0.03f, true);
            for (float x = -296f; x <= 296f; x += 2f)
                if (Mathf.Abs(x) > 11f) Prims.Box(decor, "Separateur", new Vector3(x, 0.35f, 200f), new Vector3(2f, 0.7f, 0.6f), ((int)((x + 400f) / 2f)) % 2 == 0 ? Red : White, true);
            for (float x = -306f; x <= 306f; x += 4f)
            {
                Color c = ((int)((x + 400f) / 4f)) % 2 == 0 ? Red : White;
                Prims.Box(decor, "Bordure", new Vector3(x, 0.08f, 191.3f), new Vector3(4f, 0.16f, 0.3f), c);
                Prims.Box(decor, "Bordure", new Vector3(x, 0.08f, 208.7f), new Vector3(4f, 0.16f, 0.3f), c);
            }
            // Route est : goudron dégradé puis nids-de-poule
            Road(root, new Vector3(300f, 0f, 60f), new Vector3(300f, 0f, 208f), 9f, SurfaceKind.GoudronDegrade, AsphaltWorn, 0.03f, false);
            Road(root, new Vector3(300f, 0f, -80f), new Vector3(300f, 0f, 60f), 9f, SurfaceKind.NidsDePoule, AsphaltWorn, 0.03f, false);
            for (int i = 0; i < 40; i++)
                Prims.Box(decor, "NidDePoule", new Vector3(300f + (float)(rnd.NextDouble() - 0.5) * 7f, 0.031f, -78f + (float)rnd.NextDouble() * 136f), new Vector3(0.6f + (float)rnd.NextDouble(), 0.01f, 0.5f + (float)rnd.NextDouble()), new Color(0.12f, 0.1f, 0.08f));
            Road(root, new Vector3(300f, 0f, -208f), new Vector3(300f, 0f, -80f), 9f, SurfaceKind.GoudronDegrade, AsphaltWorn, 0.03f, false);
            // Route sud : terre, boue, poussière + caniveau ouvert (REF-KIN-017)
            Road(root, new Vector3(120f, 0f, -200f), new Vector3(304f, 0f, -200f), 9f, SurfaceKind.Terre, Dirt, 0.02f, false);
            GameObject mud = Road(root, new Vector3(30f, 0f, -200f), new Vector3(120f, 0f, -200f), 9f, SurfaceKind.Boue, Mud, 0.02f, false);
            Material mudMat = mud.GetComponent<Renderer>().material;
            WorldEnvironment.WetMaterials.Add(mudMat);
            Road(root, new Vector3(-120f, 0f, -200f), new Vector3(30f, 0f, -200f), 9f, SurfaceKind.Poussiere, Dust, 0.02f, false);
            Road(root, new Vector3(-304f, 0f, -200f), new Vector3(-120f, 0f, -200f), 9f, SurfaceKind.Terre, Dirt, 0.02f, false);
            GameObject drain = Road(root, new Vector3(-296f, 0f, -205.3f), new Vector3(296f, 0f, -205.3f), 1.4f, SurfaceKind.Caniveau, new Color(0.15f, 0.15f, 0.15f), -0.04f, false);
            drain.name = "Caniveau";
            Prims.Box(decor, "BordCaniveau", new Vector3(0f, 0.05f, -206.1f), new Vector3(592f, 0.2f, 0.2f), new Color(0.55f, 0.55f, 0.55f), true);
            // Route ouest : goudron avec ralentisseurs et zone de travaux
            Road(root, new Vector3(-300f, 0f, -208f), new Vector3(-300f, 0f, 208f), 9f, SurfaceKind.Goudron, Asphalt, 0.03f, true);
            SpeedBump(root, new Vector3(-300f, 0f, 140f), 9f);
            SpeedBump(root, new Vector3(-300f, 0f, -140f), 9f);
            GameObject works = Road(root, new Vector3(-300f, 0f, -70f), new Vector3(-300f, 0f, -20f), 9.2f, SurfaceKind.Travaux, new Color(0.45f, 0.4f, 0.35f), 0.035f, false);
            works.name = "ZoneDeTravaux";
            for (float z = -68f; z <= -22f; z += 3f)
                Prims.Cyl(decor, "Cone", new Vector3(-298.5f, 0.35f, z), 0.35f, 0.7f, new Color(1f, 0.4f, 0.05f), true);
            Prims.Box(decor, "PanneauTravaux", new Vector3(-295f, 1.2f, -16f), new Vector3(1.6f, 1.2f, 0.08f), new Color(1f, 0.8f, 0.05f), true);
            Prims.Label(decor, "TRAVAUX", new Vector3(-295f, 1.2f, -16.1f), 0.05f, Color.black, true);
            // Avenues centrales (croisement à feux)
            Road(root, new Vector3(0f, 0f, -204f), new Vector3(0f, 0f, 191f), 10f, SurfaceKind.Goudron, Asphalt, 0.04f, true);
            Road(root, new Vector3(-295f, 0f, 0f), new Vector3(295f, 0f, 0f), 10f, SurfaceKind.Goudron, Asphalt, 0.04f, true);

            // --- Carrefour à feux, robot roulage, passages piétons
            GameObject jgo = new GameObject("Carrefour");
            jgo.transform.SetParent(root, false);
            TrafficLightController junction = jgo.AddComponent<TrafficLightController>();
            w.Junction = junction;
            Vector3[] dirs = { Vector3.forward, Vector3.back, Vector3.right, Vector3.left };
            LightAxis[] axes = { LightAxis.NordSud, LightAxis.NordSud, LightAxis.EstOuest, LightAxis.EstOuest };
            for (int i = 0; i < 4; i++)
            {
                Vector3 d = dirs[i];
                Vector3 r = Vector3.Cross(Vector3.up, d);
                Vector3 pole = -d * 10.5f - r * 6.5f;
                TrafficLightHead.Build(jgo.transform, pole, Quaternion.LookRotation(-d).eulerAngles.y, axes[i], junction, 9f);
                junction.AddStopLine(-d * 8f + r * 2.5f, d, 5f, axes[i]);
                Vector3 stopBar = -d * 8f + r * 2.5f;
                Prims.BoxRot(decor, "LigneArret", new Vector3(stopBar.x, 0.05f, stopBar.z), new Vector3(4.6f, 0.01f, 0.4f), new Vector3(0f, Quaternion.LookRotation(d).eulerAngles.y, 0f), White);
                for (int k = -4; k <= 4; k++)
                {
                    Vector3 zc = -d * 10f + r * k * 1.1f;
                    Prims.BoxRot(decor, "PassagePieton", new Vector3(zc.x, 0.05f, zc.z), new Vector3(0.6f, 0.01f, 2.6f), new Vector3(0f, Quaternion.LookRotation(d).eulerAngles.y, 0f), White);
                }
            }
            RobotRoulage.Build(jgo.transform, new Vector3(10f, 0f, 10f), junction);
            Reserve(Vector3.zero, 22f);

            // --- Lieux de la piste (noms de test, pas de vrais lieux de Kinshasa)
            AddPlace(w, root, "essai-rond-point", "Rond-point central", "Piste d'essai", "arret", new Vector3(12f, 0f, -12f), 8, true);
            AddPlace(w, root, "essai-marche", "Marché d'essai", "Piste d'essai", "marche", new Vector3(-200f, 0f, 211f), 10, true);
            AddPlace(w, root, "essai-terminus-est", "Terminus Est", "Piste d'essai", "arret", new Vector3(307f, 0f, 140f), 7, true);
            AddPlace(w, root, "essai-ecole", "École d'essai", "Piste d'essai", "ecole", new Vector3(200f, 0f, -207f), 6, true);
            AddPlace(w, root, "essai-eglise", "Église d'essai", "Piste d'essai", "eglise", new Vector3(-307f, 0f, 80f), 6, true);
            AddPlace(w, root, "essai-centrale-sud", "Avenue Centrale Sud", "Piste d'essai", "arret", new Vector3(-7f, 0f, -120f), 6, true);
            AddPlace(w, root, "essai-ouest", "Quartier Ouest", "Piste d'essai", "quartier", new Vector3(-150f, 0f, 7f), 7, true);
            AddPlace(w, root, "essai-est", "Quartier Est", "Piste d'essai", "quartier", new Vector3(150f, 0f, -7f), 7, true);
            AddPlace(w, root, "essai-centrale-nord", "Avenue Centrale Nord", "Piste d'essai", "arret", new Vector3(7f, 0f, 120f), 6, true);
            AddPlace(w, root, "essai-hopital", "Hôpital d'essai", "Piste d'essai", "sante", new Vector3(293f, 0f, 60f), 5, false);
            AddPlace(w, root, "essai-cite-sud", "Cité Sud", "Piste d'essai", "quartier", new Vector3(-80f, 0f, -211f), 6, false);
            AddPlace(w, root, "essai-pharmacie", "Pharmacie d'essai", "Piste d'essai", "sante", new Vector3(-120f, 0f, 189f), 4, false);
            Landmark(root, "HÔPITAL D'ESSAI", new Vector3(278f, 0f, 60f), new Vector3(16f, 7f, 22f), White, 90f);
            Landmark(root, "PHARMACIE D'ESSAI", new Vector3(-120f, 0f, 180f), new Vector3(10f, 4f, 8f), new Color(0.3f, 0.7f, 0.4f), 0f);
            Landmark(root, "ÉCOLE D'ESSAI", new Vector3(200f, 0f, -222f), new Vector3(30f, 4f, 12f), new Color(0.95f, 0.9f, 0.7f), 0f);
            Landmark(root, "ÉGLISE D'ESSAI", new Vector3(-322f, 0f, 80f), new Vector3(14f, 9f, 24f), White, 90f);

            // --- Services
            Station(root, w, new Vector3(150f, 0f, 224f));
            InteractionPoint.Create(root, InteractionKind.StationService, "station", "Station-service", new Vector3(150f, 0f, 213f), 9f);
            Reserve(new Vector3(150f, 0f, 220f), 18f);
            Kiosk(root, new Vector3(-150f, 0f, -214f), "POINT DE VENTE CARBURANT", new Color(0.1f, 0.4f, 0.75f));
            InteractionPoint.Create(root, InteractionKind.PointVente, "point_vente", "Point de vente carburant", new Vector3(-150f, 0f, -211f), 7f);
            KadafiStand(root, new Vector3(309.5f, 0f, -40f));
            InteractionPoint.Create(root, InteractionKind.Kadafi, "kadafi", "Kadafi", new Vector3(306.5f, 0f, -40f), 6f);
            Shed(root, new Vector3(-316f, 0f, -100f), "ATELIER MÉCANIQUE");
            InteractionPoint.Create(root, InteractionKind.Atelier, "", "Atelier mécanique", new Vector3(-307f, 0f, -100f), 8f);
            AddPlace(w, root, "essai-atelier", "Atelier mécanique", "Piste d'essai", "atelier", new Vector3(-307f, 0f, -100f), 2, false);
            Landmark(root, "GARAGE MATULA", new Vector3(-262f, 0f, 176f), new Vector3(16f, 6f, 12f), new Color(0.15f, 0.2f, 0.3f), 0f);
            InteractionPoint.Create(root, InteractionKind.Garage, "", "Garage MATULA", new Vector3(-262f, 0f, 187f), 8f);
            Landmark(root, "CONCESSION BALULA — MOTOS & TRICYCLES", new Vector3(60f, 0f, 226f), new Vector3(24f, 6f, 12f), new Color(0.95f, 0.75f, 0.05f), 180f);
            InteractionPoint.Create(root, InteractionKind.Concession, "", "Concession", new Vector3(60f, 0f, 213f), 9f);
            Reserve(new Vector3(60f, 0f, 222f), 18f);
            w.SpawnPosition = new Vector3(-262f, 0f, 196f);
            w.SpawnRotation = Quaternion.Euler(0f, 90f, 0f);
            w.MenuCenter = new Vector3(-150f, 0f, 150f);

            // Panneau d'accueil (honnêteté sur la nature du lieu)
            Prims.Box(decor, "PanneauAccueil", new Vector3(-232f, 4f, 214f), new Vector3(16f, 6f, 0.3f), new Color(0.05f, 0.25f, 0.55f), true);
            Prims.Box(decor, "PiedA", new Vector3(-238f, 1f, 214.4f), new Vector3(0.3f, 2f, 0.3f), Color.gray, true);
            Prims.Box(decor, "PiedB", new Vector3(-226f, 1f, 214.4f), new Vector3(0.3f, 2f, 0.3f), Color.gray, true);
            GameObject sign = Prims.Label(decor, "PISTE D'ESSAI — BALULA MOTO\nEnvironnement de test des mécaniques (ce n'est pas Kinshasa).\nLa carte réelle se génère avec l'import OpenStreetMap.", new Vector3(-232f, 4f, 213.8f), 0.12f, Color.white, false);
            sign.transform.localRotation = Quaternion.identity;

            // Passerelle piétonne en arc au-dessus du boulevard (REF-KIN-009)
            Footbridge(root, 80f);
            // Panneaux publicitaires fictifs
            Billboard(root, new Vector3(-60f, 0f, 216f), "BALULA TÉLÉCOM\nLa 4G partout à Kin", new Color(0.85f, 0.1f, 0.15f));
            Billboard(root, new Vector3(230f, 0f, 216f), "EAU KIN PURE\nNaturellement fraîche", new Color(0.1f, 0.45f, 0.85f));
            Billboard(root, new Vector3(-150f, 0f, -224f), "MAMAN MADO WAX\nPagnes et tissus", new Color(0.9f, 0.5f, 0.05f));

            // Lampadaires, palmiers, bâtiments
            for (float x = -290f; x <= 290f; x += 40f)
            {
                StreetLamp(root, new Vector3(x, 0f, 190.5f), 0f);
                StreetLamp(root, new Vector3(x + 20f, 0f, 209.5f), 180f);
                if (Mathf.Abs(x - 80f) > 15f) Palm(root, new Vector3(x + 10f, 0f, 188.8f));
            }
            for (float z = -180f; z <= 180f; z += 45f)
            {
                StreetLamp(root, new Vector3(6.2f, 0f, z), 90f);
                StreetLamp(root, new Vector3(-6.2f, 0f, z + 22f), -90f);
            }
            PopulateBuildings(root);
            for (int i = 0; i < 70; i++)
            {
                Vector3 p = new Vector3((float)(rnd.NextDouble() - 0.5) * 560f, 0f, (float)(rnd.NextDouble() - 0.5) * 360f);
                if (Mathf.Abs(p.x) < 14f || Mathf.Abs(p.z) < 14f || IsReserved(p, 12f)) continue;
                if (rnd.Next(3) == 0) Palm(root, p); else Tree(root, p);
            }

            // --- Police
            w.Checkpoint = PoliceCheckpoint.Create(root, new Vector3(40f, 0f, -8f), 0f, traffic);

            // --- Circulation (conduite à droite)
            BuildTrafficPaths(w, junction);
            w.WalkPaths.Add(TrafficPath.Loop("TrottoirExterieur", new List<Vector3> { new Vector3(-312f, 0f, 212f), new Vector3(312f, 0f, 212f), new Vector3(312f, 0f, -212f), new Vector3(-312f, 0f, -212f) }, 4f));
            w.WalkPaths.Add(TrafficPath.Loop("TrottoirInterieur", new List<Vector3> { new Vector3(-293f, 0f, 189f), new Vector3(-293f, 0f, -193f), new Vector3(293f, 0f, -193f), new Vector3(293f, 0f, 189f) }, 4f));

            // Optimisation mobile prévue : fusionner les maillages du décor dans l'éditeur (voir feuille de route).
            return w;
        }

        private static void BuildTrafficPaths(WorldContext w, TrafficLightController j)
        {
            // A : boucle extérieure sens horaire (voies intérieures) ; B : sens inverse (voies extérieures)
            w.TrafficPaths.Add(TrafficPath.Loop("BoucleHoraire", new List<Vector3> { new Vector3(-297.75f, 0f, 196f), new Vector3(297.75f, 0f, 196f), new Vector3(297.75f, 0f, -197.75f), new Vector3(-297.75f, 0f, -197.75f) }, 9f));
            w.TrafficPaths.Add(TrafficPath.Loop("BoucleAntihoraire", new List<Vector3> { new Vector3(-302.25f, 0f, 204f), new Vector3(-302.25f, 0f, -202.25f), new Vector3(302.25f, 0f, -202.25f), new Vector3(302.25f, 0f, 204f) }, 9f));
            // C : nord par l'avenue centrale (traverse le carrefour)
            TrafficPath c = TrafficPath.Loop("TraverseeNord", new List<Vector3> { new Vector3(2.5f, 0f, -202.25f), new Vector3(2.5f, 0f, 204f), new Vector3(-302.25f, 0f, 204f), new Vector3(-302.25f, 0f, -202.25f) }, 9f);
            c.AddStop(new Vector3(2.5f, 0f, -8f), LightAxis.NordSud, j);
            w.TrafficPaths.Add(c);
            w.FollowPath = c;
            // D : est par l'avenue transversale
            TrafficPath d = TrafficPath.Loop("TraverseeEst", new List<Vector3> { new Vector3(-297.75f, 0f, -2.5f), new Vector3(297.75f, 0f, -2.5f), new Vector3(297.75f, 0f, -197.75f), new Vector3(-297.75f, 0f, -197.75f) }, 9f);
            d.AddStop(new Vector3(-8f, 0f, -2.5f), LightAxis.EstOuest, j);
            w.TrafficPaths.Add(d);
            // E : sud par l'avenue centrale
            TrafficPath e = TrafficPath.Loop("TraverseeSud", new List<Vector3> { new Vector3(-2.5f, 0f, 204f), new Vector3(-2.5f, 0f, -202.25f), new Vector3(302.25f, 0f, -202.25f), new Vector3(302.25f, 0f, 204f) }, 9f);
            e.AddStop(new Vector3(-2.5f, 0f, 8f), LightAxis.NordSud, j);
            w.TrafficPaths.Add(e);
            // F : ouest par l'avenue transversale
            TrafficPath f = TrafficPath.Loop("TraverseeOuest", new List<Vector3> { new Vector3(297.75f, 0f, 2.5f), new Vector3(-297.75f, 0f, 2.5f), new Vector3(-297.75f, 0f, 196f), new Vector3(297.75f, 0f, 196f) }, 9f);
            f.AddStop(new Vector3(8f, 0f, 2.5f), LightAxis.EstOuest, j);
            w.TrafficPaths.Add(f);
        }

        // ------------------------------------------------------------------
        // Éléments de construction
        // ------------------------------------------------------------------

        private static GameObject Road(Transform parent, Vector3 a, Vector3 b, float width, SurfaceKind kind, Color color, float topY, bool markings)
        {
            Vector3 dir = b - a;
            float len = dir.magnitude;
            GameObject g = Prims.Box(parent, "Route_" + kind, (a + b) * 0.5f + new Vector3(0f, topY - 0.05f, 0f), new Vector3(width, 0.1f, len), color, true, kind == SurfaceKind.Goudron ? 0.18f : 0.05f);
            g.transform.rotation = Quaternion.LookRotation(dir.normalized);
            g.AddComponent<SurfaceZone>().Kind = kind;
            if (kind == SurfaceKind.Goudron || kind == SurfaceKind.GoudronDegrade)
            {
                Material m = g.GetComponent<Renderer>().sharedMaterial;
                if (!WorldEnvironment.WetMaterials.Contains(m)) WorldEnvironment.WetMaterials.Add(m);
            }
            if (markings)
            {
                Vector3 n = dir.normalized;
                for (float s = 3f; s < len - 3f; s += 9f)
                {
                    Vector3 p = a + n * s;
                    GameObject mk = Prims.Box(decor, "Marquage", new Vector3(p.x, topY + 0.006f, p.z), new Vector3(0.15f, 0.01f, 3f), White);
                    mk.transform.rotation = Quaternion.LookRotation(n);
                }
            }
            return g;
        }

        private static void SpeedBump(Transform parent, Vector3 pos, float width)
        {
            Mesh m = Prims.Prism(new[] { new Vector2(0.02f, -0.9f), new Vector2(0.11f, -0.3f), new Vector2(0.12f, 0.3f), new Vector2(0.02f, 0.9f) }, width);
            GameObject g = Prims.MeshObject(parent, "Ralentisseur", m, Mats.Lit(new Color(0.85f, 0.75f, 0.1f)));
            g.transform.position = pos + new Vector3(0f, 0.02f, 0f);
            MeshCollider mc = g.AddComponent<MeshCollider>();
            mc.sharedMesh = m;
            g.AddComponent<SurfaceZone>().Kind = SurfaceKind.Ralentisseur;
            Prims.Box(decor, "PanneauRalentisseur", pos + new Vector3(5.5f, 1.2f, -12f), new Vector3(0.7f, 0.7f, 0.05f), White, true);
        }

        private static void AddPlace(WorldContext w, Transform root, string id, string name, string district, string category, Vector3 pos, int weight, bool stop)
        {
            PlaceRef p = WorldGeo.Place(id, name, district, category, pos, weight, stop);
            w.Places.Add(p);
            Reserve(pos, 14f);
            if (stop) ClientStop.Create(root, p, true);
        }

        private static void Reserve(Vector3 p, float radius) { reserved.Add(new Vector3(p.x, radius, p.z)); }

        private static bool IsReserved(Vector3 p, float margin)
        {
            foreach (Vector3 r in reserved)
            {
                float dx = p.x - r.x, dz = p.z - r.z;
                if (dx * dx + dz * dz < (r.y + margin) * (r.y + margin)) return true;
            }
            return false;
        }

        private static void Landmark(Transform root, string label, Vector3 pos, Vector3 size, Color color, float yaw)
        {
            GameObject b = Prims.Box(decor, "Batiment_" + label, pos + new Vector3(0f, size.y * 0.5f, 0f), size, color, true);
            b.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            Prims.Box(decor, "Toit", pos + new Vector3(0f, size.y + 0.15f, 0f), new Vector3(size.x + 0.6f, 0.3f, size.z + 0.6f), new Color(0.35f, 0.35f, 0.38f)).transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            GameObject l = Prims.Label(root, label, pos + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, size.y * 0.75f, size.z * 0.5f + 0.1f), 0.12f, Color.white, true);
            l.transform.rotation = Quaternion.Euler(0f, yaw + 180f, 0f);
            Reserve(pos, Mathf.Max(size.x, size.z) * 0.6f + 4f);
        }

        private static void Station(Transform root, WorldContext w, Vector3 pos)
        {
            Prims.Box(decor, "Auvent", pos + new Vector3(0f, 5.2f, 0f), new Vector3(18f, 0.5f, 10f), White, false);
            Prims.Box(decor, "BandeauStation", pos + new Vector3(0f, 5.2f, -5.05f), new Vector3(18f, 0.6f, 0.1f), new Color(0.05f, 0.45f, 0.2f));
            for (int i = 0; i < 4; i++)
            {
                float x = -6f + i * 4f;
                Prims.Box(decor, "Pilier", pos + new Vector3(x, 2.5f, 0f), new Vector3(0.4f, 5f, 0.4f), White, true);
                Prims.Box(decor, "Pompe", pos + new Vector3(x + 1.2f, 0.8f, 0f), new Vector3(0.6f, 1.6f, 0.5f), new Color(0.9f, 0.1f, 0.1f), true);
            }
            Prims.Box(decor, "Boutique", pos + new Vector3(0f, 2f, 9f), new Vector3(12f, 4f, 6f), new Color(0.9f, 0.9f, 0.85f), true);
            Prims.Box(decor, "Totem", pos + new Vector3(11f, 3.5f, -4f), new Vector3(1.6f, 7f, 0.4f), new Color(0.05f, 0.45f, 0.2f), true);
            Prims.Label(decor, "STATION\nEssence\n2 640 FC/L", pos + new Vector3(11f, 4.5f, -4.3f), 0.07f, Color.white, true);
        }

        private static void Kiosk(Transform root, Vector3 pos, string label, Color color)
        {
            Prims.Box(decor, "Kiosque", pos + new Vector3(0f, 1.4f, 0f), new Vector3(5f, 2.8f, 3f), color, true);
            Prims.Box(decor, "Fut", pos + new Vector3(3.2f, 0.5f, 0f), new Vector3(0.6f, 1f, 0.6f), new Color(0.2f, 0.3f, 0.5f), true);
            Prims.Label(decor, label, pos + new Vector3(0f, 3.2f, 1.6f), 0.06f, Color.white, true);
            Reserve(pos, 8f);
        }

        /// <summary>Vendeur de carburant au détail (« Kadafi ») : table, bouteilles et bidons jaunes.</summary>
        private static void KadafiStand(Transform root, Vector3 pos)
        {
            Prims.Box(decor, "Table", pos + new Vector3(0f, 0.75f, 0f), new Vector3(1.0f, 0.06f, 1.8f), new Color(0.45f, 0.3f, 0.18f), true);
            for (int i = 0; i < 8; i++)
                Prims.Box(decor, "Bouteille", pos + new Vector3(-0.3f + (i % 2) * 0.6f, 0.95f, -0.75f + (i / 2) * 0.45f), new Vector3(0.09f, 0.3f, 0.09f), new Color(0.95f, 0.75f, 0.2f, 1f));
            for (int i = 0; i < 3; i++)
                Prims.Box(decor, "Bidon", pos + new Vector3(0.9f, 0.3f, -0.6f + i * 0.6f), new Vector3(0.3f, 0.6f, 0.45f), new Color(0.95f, 0.8f, 0.05f), true);
            Prims.Box(decor, "Parasol", pos + new Vector3(0f, 2.3f, 0f), new Vector3(2.6f, 0.08f, 2.6f), new Color(0.1f, 0.5f, 0.9f));
            Prims.Box(decor, "Manche", pos + new Vector3(0f, 1.2f, 0f), new Vector3(0.05f, 2.3f, 0.05f), Color.gray);
            VehicleBuilder.BuildHuman(decor, pos + new Vector3(1.4f, 0f, 1.2f), "civil", false, 333, false);
            Prims.Label(decor, "ESSENCE", pos + new Vector3(0f, 1.6f, 0f), 0.05f, Color.white, true);
            Reserve(pos, 6f);
        }

        private static void Shed(Transform root, Vector3 pos, string label)
        {
            Prims.Box(decor, "Hangar", pos + new Vector3(-3f, 2f, 0f), new Vector3(6f, 4f, 10f), new Color(0.55f, 0.5f, 0.45f), true);
            Prims.Box(decor, "Toles", pos + new Vector3(-2f, 4.2f, 0f), new Vector3(9f, 0.15f, 12f), new Color(0.45f, 0.3f, 0.2f));
            for (int i = 0; i < 5; i++)
                Prims.CylX(decor, "Pneu", pos + new Vector3(1.5f, 0.3f + i * 0.22f, 3.5f), 0.6f, 0.2f, new Color(0.08f, 0.08f, 0.08f));
            VehicleBuilder.BuildHuman(decor, pos + new Vector3(1.5f, 0f, -1f), "mecano", false, 444, false);
            Prims.Label(decor, label, pos + new Vector3(0.2f, 3.4f, 0f), 0.08f, Color.white, true).transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            Reserve(pos, 10f);
        }

        private static void Footbridge(Transform root, float x)
        {
            Color steel = new Color(0.35f, 0.38f, 0.42f);
            Prims.Box(decor, "Tablier", new Vector3(x, 6.2f, 200f), new Vector3(2.6f, 0.3f, 26f), steel, true);
            foreach (float z in new[] { 186.5f, 213.5f })
            {
                Prims.Box(decor, "Pile", new Vector3(x, 3.1f, z), new Vector3(1f, 6.2f, 1f), steel, true);
                for (int s = 0; s < 10; s++)
                    Prims.Box(decor, "Marche", new Vector3(x + 1.8f + s * 0.6f, 0.3f + s * 0.6f, z), new Vector3(0.6f, 0.2f, 2.2f), steel, true);
            }
            for (int i = 0; i <= 12; i++)
            {
                float t = i / 12f;
                float z = Mathf.Lerp(187f, 213f, t);
                float y = 6.4f + Mathf.Sin(t * Mathf.PI) * 3.2f;
                Prims.Box(decor, "Arc", new Vector3(x - 1.2f, y, z), new Vector3(0.25f, 0.25f, 2.3f), steel);
                Prims.Box(decor, "Arc", new Vector3(x + 1.2f, y, z), new Vector3(0.25f, 0.25f, 2.3f), steel);
            }
            Prims.Box(decor, "Banderole", new Vector3(x - 1.3f, 8.1f, 200f), new Vector3(0.08f, 1.6f, 14f), new Color(0.85f, 0.08f, 0.1f));
            Prims.Box(decor, "Banderole", new Vector3(x + 1.3f, 8.1f, 200f), new Vector3(0.08f, 1.6f, 14f), new Color(0.85f, 0.08f, 0.1f));
            Prims.Label(decor, "KIN LA BELLE — BALULA MOTO", new Vector3(x - 1.36f, 8.1f, 200f), 0.15f, Color.white, false).transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            Prims.Label(decor, "KIN LA BELLE — BALULA MOTO", new Vector3(x + 1.36f, 8.1f, 200f), 0.15f, Color.white, false).transform.rotation = Quaternion.Euler(0f, -90f, 0f);
            Reserve(new Vector3(x, 0f, 213f), 10f);
            Reserve(new Vector3(x, 0f, 187f), 10f);
        }

        private static void Billboard(Transform root, Vector3 pos, string text, Color color)
        {
            Prims.Box(decor, "PiedPanneau", pos + new Vector3(0f, 3f, 0f), new Vector3(0.4f, 6f, 0.4f), Color.gray, true);
            Prims.Box(decor, "Panneau", pos + new Vector3(0f, 7.5f, 0f), new Vector3(10f, 4f, 0.3f), color);
            Prims.Label(decor, text, pos + new Vector3(0f, 7.5f, -0.2f), 0.13f, Color.white, false);
            Prims.Label(decor, text, pos + new Vector3(0f, 7.5f, 0.2f), 0.13f, Color.white, false).transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            Reserve(pos, 7f);
        }

        private static void StreetLamp(Transform root, Vector3 pos, float yaw)
        {
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            Prims.Box(decor, "Mat", pos + new Vector3(0f, 4.5f, 0f), new Vector3(0.18f, 9f, 0.18f), new Color(0.7f, 0.72f, 0.75f), true);
            Prims.Box(decor, "Crosse", pos + q * new Vector3(0f, 8.9f, 1.2f), new Vector3(0.12f, 0.12f, 2.4f), new Color(0.7f, 0.72f, 0.75f)).transform.rotation = q;
            GameObject lamp = Prims.Box(root, "Lampe", pos + q * new Vector3(0f, 8.75f, 2.3f), new Vector3(0.5f, 0.15f, 0.8f), new Color(0.3f, 0.28f, 0.22f));
            lamp.transform.rotation = q;
            WorldEnvironment.StreetLamps.Add(lamp.GetComponent<Renderer>());
        }

        private static void Palm(Transform root, Vector3 pos)
        {
            float h = 6f + (float)rnd.NextDouble() * 4f;
            Prims.Box(decor, "Stipe", pos + new Vector3(0f, h * 0.5f, 0f), new Vector3(0.35f, h, 0.35f), new Color(0.45f, 0.36f, 0.25f), true);
            for (int i = 0; i < 6; i++)
                Prims.BoxRot(decor, "Palme", pos + new Vector3(0f, h, 0f) + Quaternion.Euler(0f, i * 60f, 0f) * new Vector3(0f, -0.3f, 1.4f), new Vector3(0.5f, 0.06f, 3f), new Vector3(25f, i * 60f, 0f), new Color(0.2f, 0.5f, 0.15f));
        }

        private static void Tree(Transform root, Vector3 pos)
        {
            float h = 3f + (float)rnd.NextDouble() * 2f;
            Prims.Box(decor, "Tronc", pos + new Vector3(0f, h * 0.5f, 0f), new Vector3(0.45f, h, 0.45f), new Color(0.38f, 0.28f, 0.2f), true);
            Prims.Ball(decor, "Feuillage", pos + new Vector3(0f, h + 1.5f, 0f), new Vector3(5f, 3.8f, 5f), new Color(0.18f, 0.42f, 0.14f));
        }

        private static readonly string[] Shops = { "ALIMENTATION", "PHARMACIE", "SALON DE COIFFURE", "MULTISERVICES", "QUINCAILLERIE", "BOUTIQUE WAX", "CABINE TÉLÉPHONIQUE", "DÉPÔT DE BOISSONS", "COUTURE", "RESTAURANT MALEWA" };
        private static readonly Color[] Walls = { new Color(0.92f, 0.88f, 0.75f), new Color(0.65f, 0.8f, 0.9f), new Color(0.95f, 0.72f, 0.6f), new Color(0.98f, 0.9f, 0.55f), new Color(0.9f, 0.9f, 0.9f), new Color(0.75f, 0.75f, 0.72f), new Color(0.7f, 0.85f, 0.65f) };
        private static readonly Color[] Roofs = { new Color(0.55f, 0.3f, 0.18f), new Color(0.2f, 0.35f, 0.6f), new Color(0.6f, 0.15f, 0.12f), new Color(0.5f, 0.52f, 0.55f) };

        private static void PopulateBuildings(Transform root)
        {
            // Rangées de maisons et boutiques le long des voies (côtés extérieurs et avenues centrales)
            for (float x = -290f; x <= 290f; x += 17f)
            {
                House(root, new Vector3(x, 0f, 226f), 180f);
                House(root, new Vector3(x, 0f, -226f), 0f);
            }
            for (float z = -185f; z <= 185f; z += 17f)
            {
                House(root, new Vector3(326f, 0f, z), -90f);
                House(root, new Vector3(-326f, 0f, z), 90f);
            }
            for (float z = -185f; z <= 175f; z += 18f)
            {
                House(root, new Vector3(18f, 0f, z), -90f);
                House(root, new Vector3(-18f, 0f, z), 90f);
            }
            for (float x = -280f; x <= 280f; x += 18f)
            {
                House(root, new Vector3(x, 0f, 18f), 180f);
                House(root, new Vector3(x, 0f, -18f), 0f);
            }
        }

        private static void House(Transform root, Vector3 pos, float yaw)
        {
            if (IsReserved(pos, 6f) || Mathf.Abs(pos.x) < 14f && Mathf.Abs(pos.z) < 14f) return;
            if (rnd.Next(5) == 0) return;
            float w = 7f + (float)rnd.NextDouble() * 6f, d = 6f + (float)rnd.NextDouble() * 4f;
            float h = rnd.Next(4) == 0 ? 7f : 3.6f;
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            Color wall = Walls[rnd.Next(Walls.Length)];
            Prims.Box(decor, "Maison", pos + new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h, d), wall, true).transform.rotation = q;
            Prims.Box(decor, "Tole", pos + new Vector3(0f, h + 0.12f, 0f), new Vector3(w + 0.8f, 0.2f, d + 0.8f), Roofs[rnd.Next(Roofs.Length)]).transform.rotation = q;
            Prims.Box(decor, "Porte", pos + q * new Vector3(0f, 1.05f, d * 0.5f + 0.02f), new Vector3(1.1f, 2.1f, 0.06f), new Color(0.3f, 0.2f, 0.12f)).transform.rotation = q;
            Prims.Box(decor, "Fenetre", pos + q * new Vector3(-w * 0.3f, 1.6f, d * 0.5f + 0.02f), new Vector3(1.2f, 1f, 0.06f), new Color(0.15f, 0.2f, 0.25f)).transform.rotation = q;
            if (rnd.Next(3) == 0)
            {
                Vector3 sp = pos + q * new Vector3(0f, Mathf.Min(h, 3.6f) - 0.4f, d * 0.5f + 0.15f);
                Prims.Box(decor, "Enseigne", sp, new Vector3(w * 0.8f, 0.7f, 0.1f), Roofs[rnd.Next(Roofs.Length)]).transform.rotation = q;
                Prims.Label(decor, Shops[rnd.Next(Shops.Length)], sp + q * new Vector3(0f, 0f, 0.07f), 0.05f, Color.white, false).transform.rotation = q * Quaternion.Euler(0f, 180f, 0f);
            }
        }
    }

    /// <summary>Construit le contexte de jeu à partir d'une carte importée d'OpenStreetMap.</summary>
    public static class ImportedWorldAdapter
    {
        private static long StationPrice()
        {
            BalulaGame g = BalulaGame.Instance;
            if (g == null || g.Db == null) return 0;
            BalulaMoto.Core.FuelVendorDef v = g.Db.FuelVendor("station");
            return v != null ? v.prixLitre : 0;
        }

        public static WorldContext Build(ImportedWorld map, Transform root, TrafficManager traffic)
        {
            WorldContext w = new WorldContext { Name = map.worldName, IsTestTrack = false, Attribution = map.attribution, SpeedLimitKmh = 60f };
            int stops = 0;
            foreach (ImportedPlace p in map.places)
            {
                PlaceRef pr = WorldGeo.Place(p.id, p.name, p.district, p.category, p.position, Mathf.Max(1, p.weight), p.isStop);
                w.Places.Add(pr);
                if (p.isStop && stops < 80) { ClientStop.Create(root, pr, false); stops++; }
                if (p.isFuel)
                {
                    // Vraie station (auvent, pompes, boutique, totem) au bord de la rue ; « faire le plein » sous l'auvent
                    Vector3 service = FuelStations.Build(root, p, map, StationPrice());
                    InteractionPoint.Create(root, InteractionKind.StationService, "station", p.name, service, 10f, false);
                }
                if (p.isLandmark) LandmarkPin.Create(root, p.id, p.name, p.status, p.position);
            }
            // Arrêts informels (comme à Kinshasa : devant les marchés, gares, hôpitaux, écoles, églises, magasins),
            // posés sur le trottoir réel le plus proche (rues OpenStreetMap), jamais à moins de 120 m d'un autre arrêt
            if (stops < 70 && map.walkPaths.Count > 0)
            {
                Dictionary<long, List<Vector3>> walkGrid = new Dictionary<long, List<Vector3>>();
                foreach (ImportedPath wp in map.walkPaths)
                    foreach (Vector3 q in wp.points)
                    {
                        long k = ((long)Mathf.FloorToInt(q.x / 50f) << 32) ^ (uint)Mathf.FloorToInt(q.z / 50f);
                        List<Vector3> l;
                        if (!walkGrid.TryGetValue(k, out l)) { l = new List<Vector3>(); walkGrid[k] = l; }
                        l.Add(q);
                    }
                List<ImportedPlace> candidates = new List<ImportedPlace>();
                foreach (ImportedPlace p in map.places)
                {
                    if (p.isStop || p.isFuel || p.isLandmark) continue;
                    if (p.category == "marche" || p.category == "gare" || p.category == "sante" || p.category == "ecole" || p.category == "eglise" || p.category == "commerce")
                        candidates.Add(p);
                }
                candidates.Sort((a, b) => b.weight.CompareTo(a.weight));
                foreach (ImportedPlace p in candidates)
                {
                    if (stops >= 90) break;
                    int cx = Mathf.FloorToInt(p.position.x / 50f), cz = Mathf.FloorToInt(p.position.z / 50f);
                    Vector3 best = Vector3.zero;
                    float bestD = 70f * 70f;
                    bool found = false;
                    for (int dz = -1; dz <= 1; dz++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            List<Vector3> l;
                            if (!walkGrid.TryGetValue(((long)(cx + dx) << 32) ^ (uint)(cz + dz), out l)) continue;
                            foreach (Vector3 q in l)
                            {
                                float d2 = (q.x - p.position.x) * (q.x - p.position.x) + (q.z - p.position.z) * (q.z - p.position.z);
                                if (d2 < bestD) { bestD = d2; best = q; found = true; }
                            }
                        }
                    if (!found) continue;
                    bool close = false;
                    foreach (ClientStop cs in ClientStop.All)
                        if ((cs.transform.position - best).sqrMagnitude < 120f * 120f) { close = true; break; }
                    if (close) continue;
                    PlaceRef stopRef = WorldGeo.Place("arret-" + p.id, "Arrêt " + p.name, p.district, "arret", new Vector3(best.x, 0f, best.z), Mathf.Max(1, p.weight), true);
                    ClientStop.Create(root, stopRef, false);
                    stops++;
                }
            }
            if (stops == 0)
                foreach (PlaceRef pr in w.Places)
                    if (stops++ < 40) ClientStop.Create(root, pr, false);
            foreach (ImportedPath ip in map.trafficPaths)
            {
                if (ip.points.Count < 4) continue;
                TrafficPath tp = new TrafficPath { Name = ip.name, Major = ip.major };
                tp.Points.AddRange(ip.points);
                tp.Build();
                w.TrafficPaths.Add(tp);
            }
            // Chaussées goudronnées : brillantes sous la pluie (copies de matériaux propres à la partie)
            foreach (SurfaceZone z in map.GetComponentsInChildren<SurfaceZone>())
            {
                if (z.Kind != SurfaceKind.Goudron && z.Kind != SurfaceKind.GoudronDegrade) continue;
                Renderer r = z.GetComponent<Renderer>();
                if (r != null) WorldEnvironment.WetMaterials.Add(r.material);
            }
            Quaternion rot = Quaternion.Euler(0f, map.spawnHeading, 0f);
            Vector3 s = map.spawnPosition;
            w.SpawnPosition = s;
            w.SpawnRotation = rot;
            w.MenuCenter = s;
            // Services de départ autour du point d'apparition (à déplacer vers leurs vrais emplacements)
            InteractionPoint.Create(root, InteractionKind.Garage, "", "Garage MATULA", s + rot * new Vector3(-8f, 0f, -6f), 8f);
            InteractionPoint.Create(root, InteractionKind.Atelier, "", "Atelier mécanique", s + rot * new Vector3(8f, 0f, 25f), 8f);
            InteractionPoint.Create(root, InteractionKind.Kadafi, "kadafi", "Kadafi", s + rot * new Vector3(-8f, 0f, 40f), 6f);
            InteractionPoint.Create(root, InteractionKind.Concession, "", "Concession", s + rot * new Vector3(8f, 0f, -25f), 9f);
            return w;
        }
    }
}
