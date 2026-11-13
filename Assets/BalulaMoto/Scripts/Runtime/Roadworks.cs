// BALULA MOTO — routes en travaux : la chaussée défoncée (sable, gravier, bosses) est posée à l'import de la carte ;
// ici on ajoute autour du joueur les barrières rouge et blanc, les cônes, les tas de sable et de gravier, la
// pelleteuse, les panneaux « TRAVAUX — RALENTIR », une tranchée et deux ouvriers. La circulation y roule au pas.
using System.Collections.Generic;
using UnityEngine;

namespace BalulaMoto.Runtime
{
    public class Roadworks : MonoBehaviour
    {
        public static Roadworks Instance;
        private readonly List<WorkZone> zones = new List<WorkZone>();
        private readonly List<GameObject> built = new List<GameObject>();
        private float timer;

        private static readonly Color Red = new Color(0.85f, 0.1f, 0.08f);
        private static readonly Color White = new Color(0.95f, 0.95f, 0.93f);
        private static readonly Color Orange = new Color(1f, 0.42f, 0.05f);
        private static readonly Color Yellow = new Color(0.98f, 0.76f, 0.05f);
        private static readonly Color Dark = new Color(0.12f, 0.12f, 0.13f);
        private static readonly Color Sand = new Color(0.78f, 0.64f, 0.42f);
        private static readonly Color Gravel = new Color(0.5f, 0.5f, 0.48f);
        private static readonly Color Soil = new Color(0.32f, 0.22f, 0.15f);

        public int Count { get { return zones.Count; } }

        public static Roadworks Create(Transform parent, ImportedWorld map)
        {
            GameObject g = new GameObject("Chantiers");
            g.transform.SetParent(parent, false);
            Instance = g.AddComponent<Roadworks>();
            if (map != null && map.works != null)
                foreach (WorkZone z in map.works)
                    if (z != null && z.length > 5f && z.dir.sqrMagnitude > 0.5f)
                    {
                        Instance.zones.Add(z);
                        Instance.built.Add(null);
                    }
            return Instance;
        }

        /// <summary>Vitesse maximale de la circulation à cet endroit (au pas dans un chantier).</summary>
        public static float SpeedLimit(Vector3 p)
        {
            if (Instance == null) return float.MaxValue;
            List<WorkZone> zs = Instance.zones;
            for (int i = 0; i < zs.Count; i++)
            {
                WorkZone z = zs[i];
                float dx = p.x - z.center.x, dz = p.z - z.center.z;
                if (dx * dx + dz * dz > 3600f) continue;
                float along = dx * z.dir.x + dz * z.dir.z;
                float across = Mathf.Abs(dx * z.dir.z - dz * z.dir.x);
                if (Mathf.Abs(along) < z.length * 0.5f + 10f && across < z.width * 0.5f + 2f) return 2.4f;
            }
            return float.MaxValue;
        }

        /// <summary>Vrai si le point est dans un chantier (message « route en travaux »).</summary>
        public static bool Inside(Vector3 p) { return SpeedLimit(p) < 100f; }

        private void Update()
        {
            timer -= Time.unscaledDeltaTime;
            if (timer > 0f) return;
            timer = 1f;
            Vector3 pp;
            GameplayDirector d = GameplayDirector.Instance;
            if (d != null && d.Player != null) pp = d.Player.transform.position;
            else if (Camera.main != null) pp = Camera.main.transform.position;
            else return;
            for (int i = 0; i < zones.Count; i++)
            {
                float dist = Vector3.Distance(new Vector3(pp.x, 0f, pp.z), new Vector3(zones[i].center.x, 0f, zones[i].center.z));
                if (built[i] == null && dist < 260f) built[i] = BuildZone(zones[i], i);
                else if (built[i] != null && dist > 360f)
                {
                    Destroy(built[i]);
                    built[i] = null;
                    WorldEnvironment.StreetLamps.RemoveAll(r => r == null);
                }
            }
        }

        private GameObject BuildZone(WorkZone z, int index)
        {
            GameObject rootGo = new GameObject("Chantier_" + (string.IsNullOrEmpty(z.road) ? "rue" : z.road));
            Transform root = rootGo.transform;
            root.SetParent(transform, false);
            root.position = new Vector3(z.center.x, 0f, z.center.z);
            root.rotation = Quaternion.LookRotation(new Vector3(z.dir.x, 0f, z.dir.z).normalized, Vector3.up);
            float hw = z.width * 0.5f, L = z.length, y = Mathf.Max(0.04f, z.center.y);
            System.Random rnd = new System.Random(9000 + index);

            // Barrières rouge et blanc le long des deux bords de la chaussée
            for (float s = -L * 0.5f; s <= L * 0.5f; s += 3.2f)
                foreach (float side in new[] { -1f, 1f })
                    Barrier(root, new Vector3(side * (hw - 0.3f), y, s), 0f);
            // Barrières en travers des accotements aux deux bouts, avec lampes orange (allumées la nuit)
            List<Vector3> lamps = new List<Vector3>();
            foreach (float end in new[] { -1f, 1f })
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 bp = new Vector3(side * (hw + 0.9f), y, end * (L * 0.5f + 1.5f));
                    Barrier(root, bp, 90f);
                    lamps.Add(bp + new Vector3(0f, 1.25f, 0f));
                }
            // Cônes sur la ligne du milieu (entre les deux files)
            for (float s = -L * 0.5f - 6f; s <= L * 0.5f + 6f; s += 4f) Cone(root, new Vector3(0f, y, s));
            // Tas de sable et de gravier qui mordent sur la chaussée : il faut slalomer
            for (int i = 0; i < 4; i++)
            {
                float side = i % 2 == 0 ? 1f : -1f;
                float s = -L * 0.35f + L * 0.7f * i / 3f + (float)rnd.NextDouble() * 2f;
                Pile(root, new Vector3(side * (hw + 0.3f), 0f, s), i % 3 == 1 ? Gravel : Sand, 2.6f + (float)rnd.NextDouble(), 0.9f + (float)rnd.NextDouble() * 0.4f);
            }
            // Tranchée ouverte au bord de la chaussée et terre remuée
            Prims.Box(root, "Tranchee", new Vector3(hw - 1.1f, y + 0.012f, 0f), new Vector3(0.7f, 0.02f, L * 0.8f), new Color(0.08f, 0.06f, 0.05f));
            Prims.Box(root, "TerreRemuee", new Vector3(hw - 1.75f, y + 0.05f, 0f), new Vector3(0.5f, 0.1f, L * 0.8f), Soil);
            // Pelleteuse garée sur l'accotement
            Excavator(root, new Vector3(-(hw + 1.7f), 0f, L * 0.2f));
            // Panneaux aux deux entrées (côté droit, face aux véhicules qui arrivent)
            Sign(root, new Vector3(hw + 1.2f, 0f, -L * 0.5f - 8f), 0f);
            Sign(root, new Vector3(-(hw + 1.2f), 0f, L * 0.5f + 8f), 180f);

            FuelStations.Combine(root);
            foreach (Vector3 lp in lamps)
            {
                GameObject lamp = Prims.Box(root, "LampeChantier", lp, new Vector3(0.16f, 0.16f, 0.16f), new Color(1f, 0.6f, 0.1f));
                WorldEnvironment.StreetLamps.Add(lamp.GetComponent<Renderer>());
            }

            // Ouvriers (personnages réalistes) près de la pelleteuse et du sable
            if (HumanLibrary.Available)
            {
                GameObject w1 = HumanLibrary.Spawn(root, new Vector3(-(hw + 0.4f), 0f, L * 0.2f - 3.2f), "travail", false, 7000 + index * 3, false);
                if (w1 != null) w1.transform.localRotation = Quaternion.Euler(0f, 70f, 0f);
                GameObject w2 = HumanLibrary.Spawn(root, new Vector3(hw + 1.6f, 0f, -L * 0.1f), "travail", false, 7001 + index * 3, false);
                if (w2 != null) w2.transform.localRotation = Quaternion.Euler(0f, -100f, 0f);
            }
            return rootGo;
        }

        private static void Barrier(Transform root, Vector3 p, float yaw)
        {
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            Vector3 along = q * Vector3.forward;
            foreach (float s in new[] { -0.65f, 0.65f })
                Prims.Box(root, "Pied", p + along * s + new Vector3(0f, 0.5f, 0f), new Vector3(0.06f, 1.0f, 0.06f), Dark);
            for (int k = 0; k < 4; k++)
            {
                GameObject b = Prims.Box(root, "Planche", p + along * (-0.525f + 0.35f * k) + new Vector3(0f, 0.85f, 0f), new Vector3(0.05f, 0.24f, 0.35f), k % 2 == 0 ? Red : White);
                b.transform.localRotation = q;
            }
            GameObject col = new GameObject("BarriereCollision");
            col.transform.SetParent(root, false);
            col.transform.localPosition = p + new Vector3(0f, 0.5f, 0f);
            col.transform.localRotation = q;
            col.AddComponent<BoxCollider>().size = new Vector3(0.12f, 1f, 1.4f);
        }

        private static void Cone(Transform root, Vector3 p)
        {
            Prims.Box(root, "ConeSocle", p + new Vector3(0f, 0.02f, 0f), new Vector3(0.38f, 0.04f, 0.38f), Dark);
            Prims.Cyl(root, "Cone", p + new Vector3(0f, 0.3f, 0f), 0.3f, 0.52f, Orange);
            Prims.Cyl(root, "ConeBande", p + new Vector3(0f, 0.36f, 0f), 0.27f, 0.09f, White);
            Prims.Cyl(root, "ConeHaut", p + new Vector3(0f, 0.6f, 0f), 0.14f, 0.12f, Orange);
        }

        private static void Pile(Transform root, Vector3 p, Color c, float diameter, float height)
        {
            Prims.Ball(root, "Tas", p, new Vector3(diameter, height * 2f, diameter * 0.8f), c);
            GameObject col = new GameObject("TasCollision");
            col.transform.SetParent(root, false);
            col.transform.localPosition = p + new Vector3(0f, height * 0.3f, 0f);
            col.AddComponent<BoxCollider>().size = new Vector3(diameter * 0.6f, height * 0.6f, diameter * 0.5f);
        }

        private static void Excavator(Transform root, Vector3 p)
        {
            foreach (float s in new[] { -0.75f, 0.75f })
                Prims.Box(root, "Chenille", p + new Vector3(s, 0.3f, 0f), new Vector3(0.55f, 0.6f, 3.2f), Dark, false, 0.1f);
            Prims.Box(root, "Chassis", p + new Vector3(0f, 0.75f, 0f), new Vector3(1.6f, 0.3f, 2.4f), new Color(0.2f, 0.2f, 0.2f));
            Prims.Box(root, "Caisse", p + new Vector3(0f, 1.35f, -0.4f), new Vector3(2.0f, 0.9f, 2.2f), Yellow, false, 0.4f);
            Prims.Box(root, "Cabine", p + new Vector3(-0.45f, 2.25f, 0.2f), new Vector3(0.95f, 1.0f, 1.0f), Yellow, false, 0.4f);
            Prims.Box(root, "Vitre", p + new Vector3(-0.45f, 2.3f, 0.71f), new Vector3(0.8f, 0.7f, 0.04f), new Color(0.15f, 0.22f, 0.26f), false, 0.9f);
            Prims.Box(root, "Contrepoids", p + new Vector3(0f, 1.3f, -1.55f), new Vector3(1.9f, 0.7f, 0.4f), new Color(0.2f, 0.2f, 0.2f));
            GameObject arm = Prims.Box(root, "Fleche", p + new Vector3(0.45f, 2.3f, 1.55f), new Vector3(0.32f, 0.32f, 2.6f), Yellow);
            arm.transform.localRotation = Quaternion.Euler(-28f, 0f, 0f);
            GameObject stick = Prims.Box(root, "Bras", p + new Vector3(0.45f, 1.9f, 3.0f), new Vector3(0.26f, 0.26f, 1.7f), Yellow);
            stick.transform.localRotation = Quaternion.Euler(55f, 0f, 0f);
            Prims.Box(root, "Godet", p + new Vector3(0.45f, 0.45f, 3.35f), new Vector3(0.9f, 0.5f, 0.6f), Dark);
            GameObject col = new GameObject("PelleteuseCollision");
            col.transform.SetParent(root, false);
            col.transform.localPosition = p + new Vector3(0f, 1.2f, 0.4f);
            col.AddComponent<BoxCollider>().size = new Vector3(2.1f, 2.4f, 4.2f);
        }

        private static void Sign(Transform root, Vector3 p, float yaw)
        {
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            Prims.Box(root, "PoteauPanneau", p + new Vector3(0f, 0.9f, 0f), new Vector3(0.08f, 1.8f, 0.08f), Dark);
            GameObject board = Prims.Box(root, "Panneau", p + new Vector3(0f, 1.75f, 0f), new Vector3(1.3f, 0.9f, 0.05f), Yellow, true);
            board.transform.localRotation = q;
            GameObject t1 = Prims.Label(root, "TRAVAUX", p + q * new Vector3(0f, 1.92f, -0.04f) , 0.045f, Dark, false);
            t1.transform.localRotation = q;
            GameObject t2 = Prims.Label(root, "RALENTIR", p + q * new Vector3(0f, 1.58f, -0.04f), 0.03f, Red, false);
            t2.transform.localRotation = q;
        }
    }
}
