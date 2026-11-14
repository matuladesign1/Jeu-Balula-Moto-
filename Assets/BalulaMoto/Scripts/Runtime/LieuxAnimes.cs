// BALULA MOTO — vie des lieux aménagés (demandes de MATULA DESIGN, novembre 2026) :
//   • policiers en faction aux entrées des esplanades (dedans et dehors) ;
//   • entrées de la cité Motel Fikin : barrière rouge et blanc tenue par deux policiers, qui se lève à l'approche du
//     joueur (on peut entrer déposer les clients) ;
//   • joueurs de football et de basket sur les terrains du Stade des Martyrs (ballon, course) ;
//   • foule qui fête et se promène sur la place de l'Échangeur de Limete et dans les espaces verts du boulevard Lumumba.
// Les personnages ne sont créés que près du joueur, avec un plafond (téléphones).
using System.Collections.Generic;
using UnityEngine;

namespace BalulaMoto.Runtime
{
    public class LieuxAnimes : MonoBehaviour
    {
        public static LieuxAnimes Instance;

        public class Site
        {
            public string Kind;
            public Vector3 C;
            public Vector3 X = Vector3.right, Z = Vector3.forward;
            public float Hx, Hz;
            public List<Vector4> Lawns;
            public List<Vector2> LawnDirs;
            public GameObject Root;
            public List<Agent> Agents;
            public Transform Ball, Arm;
            public Vector3 BallFrom, BallTo;
            public float BallT, BallDur, Lift;
        }

        public class Agent
        {
            public Transform T;
            public HumanAvatar Av;
            public Vector3 Home, Target;
            public float Timer, Speed;
            public int Mode; // 0 : sur place ; 1 : promeneur ; 2 : joueur
            public bool Moving;
        }

        public readonly List<Site> Sites = new List<Site>();
        private readonly List<Site> lawnSites = new List<Site>();
        private readonly List<Vector4> lawnBuf = new List<Vector4>();
        private readonly List<Vector2> dirBuf = new List<Vector2>();
        private float scan;
        private int humans, cap;
        private static readonly HumanAction[] Party = { HumanAction.Wave, HumanAction.Talk, HumanAction.Idle2, HumanAction.Wave, HumanAction.Phone, HumanAction.Talk };

        public int ActiveHumans { get { return humans; } }

        public static LieuxAnimes Create(Transform parent, ImportedWorld map)
        {
            GameObject g = new GameObject("LieuxAnimes");
            g.transform.SetParent(parent, false);
            Instance = g.AddComponent<LieuxAnimes>();
            Instance.Init(map);
            return Instance;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Init(ImportedWorld map)
        {
            cap = GameSettings.Quality == 0 ? 20 : (GameSettings.Quality == 1 ? 34 : 46);
            if (map == null || map.areas == null) return;
            foreach (ImportedArea a in map.areas)
            {
                if (a == null) continue;
                if (a.kind != "police" && a.kind != "porte" && a.kind != "foot" && a.kind != "basket" && a.kind != "fete") continue;
                Vector2 x = a.x.sqrMagnitude > 0.01f ? a.x.normalized : Vector2.right;
                Sites.Add(new Site
                {
                    Kind = a.kind, C = new Vector3(a.c.x, 0f, a.c.y), X = new Vector3(x.x, 0f, x.y), Z = new Vector3(-x.y, 0f, x.x), Hx = a.hx, Hz = a.hz
                });
            }
        }

        /// <summary>Site le plus proche d'un genre donné (photos de contrôle).</summary>
        public Site Nearest(string kind, Vector3 from)
        {
            Site best = null;
            foreach (Site s in Sites)
                if (s.Kind == kind && (best == null || (s.C - from).sqrMagnitude < (best.C - from).sqrMagnitude)) best = s;
            return best;
        }

        public int Count(string kind)
        {
            int n = 0;
            foreach (Site s in Sites) if (s.Kind == kind) n++;
            return n;
        }

        private static bool PlayerPos(out Vector3 p)
        {
            TrafficManager tm = TrafficManager.Instance;
            if (tm != null && tm.Player != null) { p = tm.Player.transform.position; return true; }
            Camera c = Camera.main;
            if (c != null) { p = c.transform.position; p.y = 0f; return true; }
            p = Vector3.zero;
            return false;
        }

        private void Update()
        {
            Vector3 pp;
            if (!PlayerPos(out pp)) return;
            scan -= Time.unscaledDeltaTime;
            if (scan <= 0f)
            {
                scan = 0.7f;
                Refresh(pp);
            }
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            foreach (Site s in Sites) if (s.Root != null) Animate(s, pp, dt);
            foreach (Site s in lawnSites) if (s.Root != null) Animate(s, pp, dt);
        }

        private void Refresh(Vector3 pp)
        {
            foreach (Site s in Sites)
            {
                float d2 = (s.C - pp).sqrMagnitude;
                float reach = s.Kind == "foot" ? 170f + s.Hx : 150f;
                if (s.Root == null && d2 < reach * reach) Activate(s);
                else if (s.Root != null && d2 > (reach + 45f) * (reach + 45f)) Deactivate(s);
            }
            // Espaces verts du boulevard Lumumba : groupes de promeneurs tous les 25 m environ, près du joueur
            for (int i = lawnSites.Count - 1; i >= 0; i--)
                if ((lawnSites[i].C - pp).sqrMagnitude > 170f * 170f) { Deactivate(lawnSites[i]); lawnSites.RemoveAt(i); }
            CityFill cf = CityFill.Instance;
            if (cf == null || !HumanLibrary.Available) return;
            lawnBuf.Clear();
            dirBuf.Clear();
            if (cf.LawnsNear(new Vector2(pp.x, pp.z), 125f, lawnBuf, dirBuf) == 0) return;
            for (int i = 0; i < lawnBuf.Count && lawnSites.Count < 9; i++)
            {
                Vector3 c = new Vector3(lawnBuf[i].x, 0.07f, lawnBuf[i].y);
                bool close = false;
                foreach (Site o in lawnSites) if ((o.C - c).sqrMagnitude < 24f * 24f) { close = true; break; }
                if (close || humans + 3 > cap) continue;
                Site s = new Site { Kind = "pelouse", C = c, Lawns = new List<Vector4>(), LawnDirs = new List<Vector2>() };
                for (int k = 0; k < lawnBuf.Count; k++)
                    if ((new Vector2(lawnBuf[k].x, lawnBuf[k].y) - new Vector2(c.x, c.z)).sqrMagnitude < 13f * 13f) { s.Lawns.Add(lawnBuf[k]); s.LawnDirs.Add(dirBuf[k]); }
                lawnSites.Add(s);
                Activate(s);
            }
        }

        private void Deactivate(Site s)
        {
            if (s.Root == null) return;
            if (s.Agents != null) humans -= s.Agents.Count;
            Destroy(s.Root);
            s.Root = null;
            s.Agents = null;
            s.Ball = null;
            s.Arm = null;
        }

        private Agent Human(Site s, Vector3 pos, Vector3 look, string outfit, int seed, int mode, HumanAction idle)
        {
            if (!HumanLibrary.Available || humans >= cap) return null;
            GameObject g = HumanLibrary.Spawn(s.Root.transform, Vector3.zero, outfit, false, seed, false);
            if (g == null) return null;
            if (look.sqrMagnitude < 0.01f) look = Vector3.forward;
            g.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look));
            HumanAvatar av = g.GetComponent<HumanAvatar>();
            if (av != null)
            {
                av.Play(idle, 0f, true);
                if (av.Anim != null) av.Anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            }
            Agent a = new Agent { T = g.transform, Av = av, Home = pos, Target = pos, Mode = mode, Timer = (seed % 7) * 0.4f, Speed = mode == 2 ? 3.1f + (seed % 5) * 0.25f : 0.95f + (seed % 4) * 0.1f };
            s.Agents.Add(a);
            humans++;
            return a;
        }

        private static GameObject Block(Transform parent, Vector3 localPos, Vector3 size, Color c, bool solid)
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localPos;
            g.transform.localScale = size;
            g.GetComponent<Renderer>().sharedMaterial = Mats.Lit(c, 0.2f);
            if (!solid) Destroy(g.GetComponent<Collider>());
            return g;
        }

        private void Activate(Site s)
        {
            s.Root = new GameObject("Lieu_" + s.Kind);
            s.Root.transform.SetParent(transform, false);
            s.Agents = new List<Agent>();
            int seed = Mathf.RoundToInt(s.C.x * 3f + s.C.z * 5f) & 0x7fff;
            System.Random rnd = new System.Random(seed);
            switch (s.Kind)
            {
                case "police":
                {
                    GameObject o = PoliceAgent.Create(s.Root.transform, s.C, Quaternion.LookRotation(s.X), seed);
                    s.Agents.Add(new Agent { T = o.transform, Av = o.GetComponent<HumanAvatar>(), Home = s.C, Target = s.C });
                    humans++;
                    break;
                }
                case "porte":
                {
                    // Repère de l'entrée : X = rue qui entre dans la cité ; la barrière est en travers
                    Transform gate = new GameObject("Barriere").transform;
                    gate.SetParent(s.Root.transform, false);
                    gate.SetPositionAndRotation(s.C, Quaternion.LookRotation(s.X));
                    float hw = Mathf.Clamp(s.Hx, 2.6f, 7f);
                    Block(gate, new Vector3(-hw, 0.55f, 0f), new Vector3(0.3f, 1.1f, 0.3f), new Color(0.92f, 0.92f, 0.9f), true);
                    Block(gate, new Vector3(hw, 0.5f, 0f), new Vector3(0.16f, 1f, 0.16f), new Color(0.92f, 0.92f, 0.9f), false);
                    s.Arm = new GameObject("Bras").transform;
                    s.Arm.SetParent(gate, false);
                    s.Arm.localPosition = new Vector3(-hw, 1.02f, 0f);
                    int pieces = Mathf.Max(4, Mathf.RoundToInt(hw * 2f / 0.9f));
                    float pl = hw * 2f / pieces;
                    for (int i = 0; i < pieces; i++)
                        Block(s.Arm, new Vector3((i + 0.5f) * pl, 0f, 0f), new Vector3(pl, 0.11f, 0.08f), i % 2 == 0 ? new Color(0.85f, 0.1f, 0.08f) : new Color(0.96f, 0.96f, 0.94f), false);
                    // Guérite blanche et panneau « CONTRÔLE POLICE »
                    Block(gate, new Vector3(-hw - 1.5f, 1.15f, -1.2f), new Vector3(1.5f, 2.3f, 1.5f), new Color(0.93f, 0.93f, 0.9f), true);
                    Block(gate, new Vector3(-hw - 1.5f, 2.36f, -1.2f), new Vector3(1.8f, 0.12f, 1.8f), new Color(0.1f, 0.14f, 0.3f), false);
                    Block(gate, new Vector3(hw + 0.9f, 1.75f, -0.4f), new Vector3(1.5f, 0.5f, 0.05f), new Color(0.1f, 0.14f, 0.3f), false);
                    Block(gate, new Vector3(hw + 0.9f, 0.75f, -0.4f), new Vector3(0.08f, 1.5f, 0.08f), new Color(0.3f, 0.3f, 0.32f), false);
                    GameObject l = Prims.Label(gate, "CONTRÔLE POLICE", new Vector3(hw + 0.9f, 1.75f, -0.44f), 0.028f, Color.white, false);
                    l.transform.localRotation = Quaternion.LookRotation(Vector3.forward);
                    // Deux policiers : un de chaque côté du passage, tournés vers la rue d'arrivée
                    for (int k = 0; k < 2; k++)
                    {
                        Vector3 p = s.C + s.Z * ((k == 0 ? -1f : 1f) * (hw + 0.8f)) - s.X * (k == 0 ? 1.4f : 0.6f);
                        GameObject o = PoliceAgent.Create(s.Root.transform, p, Quaternion.LookRotation(-s.X), seed + k);
                        s.Agents.Add(new Agent { T = o.transform, Av = o.GetComponent<HumanAvatar>(), Home = p, Target = p });
                        humans++;
                    }
                    break;
                }
                case "foot":
                case "basket":
                {
                    bool basket = s.Kind == "basket";
                    int n = basket ? 6 : (s.Hx > 30f ? 12 : 8);
                    for (int i = 0; i < n; i++)
                    {
                        float fx = ((i % 4) + 0.5f) / 4f * 2f - 1f, fz = (((i / 4) % 3) + 0.5f) / 3f * 2f - 1f;
                        Vector3 home = s.C + s.X * (fx * s.Hx * 0.8f) + s.Z * (fz * s.Hz * 0.75f);
                        Human(s, home, s.C - home, i % 5 == 4 ? "uniforme" : "chauffeur", seed + i * 13, 2, HumanAction.Idle);
                    }
                    GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Destroy(ball.GetComponent<Collider>());
                    ball.transform.SetParent(s.Root.transform, false);
                    ball.transform.localScale = Vector3.one * (basket ? 0.25f : 0.23f);
                    ball.GetComponent<Renderer>().sharedMaterial = Mats.Lit(basket ? new Color(0.85f, 0.4f, 0.1f) : new Color(0.96f, 0.96f, 0.96f), 0.3f);
                    s.Ball = ball.transform;
                    s.BallFrom = s.BallTo = s.C;
                    s.BallT = 1f;
                    s.BallDur = 1f;
                    break;
                }
                case "fete":
                {
                    float r1 = Mathf.Max(3f, s.Hx), r0 = Mathf.Min(s.Hz, r1 - 2f);
                    int n = Mathf.Clamp(Mathf.RoundToInt(r1 * 0.55f), 4, 14);
                    for (int i = 0; i < n; i++)
                    {
                        float a = (float)rnd.NextDouble() * 6.283f, rr = Mathf.Lerp(Mathf.Max(0f, r0), r1, (float)rnd.NextDouble());
                        Vector3 p = s.C + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * rr;
                        bool walker = i % 3 == 2;
                        Human(s, p, s.C - p + new Vector3((float)rnd.NextDouble() - 0.5f, 0f, (float)rnd.NextDouble() - 0.5f) * 3f, i % 4 == 1 ? "pagne" : "civil", seed + i * 7, walker ? 1 : 0, Party[i % Party.Length]);
                    }
                    break;
                }
                case "pelouse":
                {
                    for (int i = 0; i < 3; i++)
                    {
                        Vector3 p = LawnPoint(s, rnd);
                        Human(s, p, new Vector3((float)rnd.NextDouble() - 0.5f, 0f, (float)rnd.NextDouble() - 0.5f), i == 1 ? "pagne" : "civil", seed + i * 11, i == 0 ? 0 : 1, Party[(seed + i) % Party.Length]);
                    }
                    break;
                }
            }
        }

        private static Vector3 LawnPoint(Site s, System.Random rnd)
        {
            if (s.Lawns == null || s.Lawns.Count == 0) return s.C;
            int k = rnd.Next(s.Lawns.Count);
            Vector4 l = s.Lawns[k];
            Vector2 d = s.LawnDirs[k], z = new Vector2(-d.y, d.x);
            Vector2 p = new Vector2(l.x, l.y) + d * (((float)rnd.NextDouble() * 2f - 1f) * l.z) + z * (((float)rnd.NextDouble() * 2f - 1f) * Mathf.Max(0f, l.w - 0.9f));
            return new Vector3(p.x, 0.07f, p.y);
        }

        private void Animate(Site s, Vector3 pp, float dt)
        {
            if (s.Arm != null)
            {
                // La barrière se lève quand le joueur approche : on peut entrer déposer les clients
                bool near = (pp - s.C).sqrMagnitude < 17f * 17f;
                s.Lift = Mathf.MoveTowards(s.Lift, near ? 1f : 0f, dt * 0.9f);
                s.Arm.localRotation = Quaternion.Euler(0f, 0f, s.Lift * 82f);
                if (s.Agents != null && s.Agents.Count > 0 && s.Agents[0].Av != null) s.Agents[0].Av.Play(near ? HumanAction.Wave : HumanAction.Idle, 0.3f);
            }
            if (s.Agents == null) return;
            if (s.Ball != null)
            {
                s.BallT += dt / Mathf.Max(0.2f, s.BallDur);
                if (s.BallT >= 1f)
                {
                    s.BallFrom = s.BallTo;
                    s.BallTo = s.C + s.X * (Random.Range(-0.85f, 0.85f) * s.Hx) + s.Z * (Random.Range(-0.8f, 0.8f) * s.Hz);
                    s.BallDur = Mathf.Max(0.8f, Vector3.Distance(s.BallFrom, s.BallTo) / 9f) + Random.Range(0.4f, 1.3f);
                    s.BallT = 0f;
                }
                float t = Mathf.Clamp01(s.BallT * 1.35f);
                Vector3 bp = Vector3.Lerp(s.BallFrom, s.BallTo, t);
                bp.y = 0.14f + Mathf.Sin(t * Mathf.PI) * Mathf.Min(2.2f, Vector3.Distance(s.BallFrom, s.BallTo) * 0.09f);
                s.Ball.position = bp;
            }
            for (int i = 0; i < s.Agents.Count; i++)
            {
                Agent a = s.Agents[i];
                if (a.T == null || a.Mode == 0) continue;
                if (a.Mode == 2)
                {
                    // Joueur : entre sa place et le ballon (les plus proches courent au ballon)
                    Vector3 ball = s.BallTo;
                    float w = (a.Home - ball).sqrMagnitude < 14f * 14f ? 0.85f : 0.3f;
                    a.Target = Vector3.Lerp(a.Home, ball, w) + s.X * ((i % 3 - 1) * 0.9f) + s.Z * ((i % 2) * 1.1f - 0.5f);
                }
                else
                {
                    a.Timer -= dt;
                    if (a.Timer <= 0f)
                    {
                        a.Timer = 5f + (i % 4) * 2.5f;
                        if (s.Kind == "pelouse") a.Target = LawnPoint(s, new System.Random(Mathf.RoundToInt(Time.time * 10f) + i * 31));
                        else
                        {
                            float ang = Random.value * 6.283f, rr = Mathf.Lerp(Mathf.Max(0f, Mathf.Min(s.Hz, s.Hx - 2f)), Mathf.Max(3f, s.Hx), Random.value);
                            a.Target = s.C + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * rr;
                        }
                    }
                }
                Vector3 to = a.Target - a.T.position;
                to.y = 0f;
                float dist = to.magnitude;
                bool move = dist > (a.Moving ? 0.35f : 1.2f);
                if (move)
                {
                    Vector3 dir = to / dist;
                    a.T.position += dir * Mathf.Min(dist, a.Speed * dt);
                    a.T.rotation = Quaternion.Slerp(a.T.rotation, Quaternion.LookRotation(dir), dt * 7f);
                }
                if (move != a.Moving && a.Av != null)
                {
                    a.Moving = move;
                    a.Av.Play(move ? HumanAction.Walk : (a.Mode == 2 ? HumanAction.Idle : Party[i % Party.Length]), 0.2f);
                    // Course des joueurs : marche accélérée
                    a.Av.SetPlaybackSpeed(move && a.Mode == 2 ? 2.1f : 1f);
                }
            }
        }
    }
}
