// BALULA MOTO — foule : piétons réalistes sur les trottoirs autour du joueur (bulle de densité qui suit le
// joueur), groupes qui discutent, vendeuses avec bassine sur la tête ou accroupies devant leur marchandise,
// téléphones, parapluies sous la pluie, écart quand une moto arrive trop près. Densité selon l'heure.
using System.Collections.Generic;
using UnityEngine;

namespace BalulaMoto.Runtime
{
    public class CrowdSystem : MonoBehaviour
    {
        public static CrowdSystem Instance;

        private class Lane
        {
            public Vector3[] P;
        }

        private class Ped
        {
            public GameObject Go;
            public HumanAvatar Av;
            public int Lane, Seg;
            public float T;
            public int Dir = 1;
            public float Speed, Side, BaseSide, Dodge;
            public float Timer;
            public bool Standing, Static, Slow;
            public float GroundY, GroundTimer;
            /// <summary>Bousculé par le joueur : immobile le temps de tomber et de se relever.</summary>
            public float Stun;
        }

        // Personnes bousculées : écart, chute vers l'extérieur, puis elles se relèvent
        private class Shove
        {
            public Transform T;
            public Vector3 From, Push, Axis;
            public Quaternion Rot;
            public float Time;
        }
        private readonly List<Shove> shoves = new List<Shove>();
        /// <summary>Personnes bousculées par le joueur depuis le début de la partie.</summary>
        public int Bumped { get; private set; }

        private readonly List<Lane> lanes = new List<Lane>();
        private readonly Dictionary<long, List<Vector2Int>> grid = new Dictionary<long, List<Vector2Int>>();
        private readonly List<Ped> peds = new List<Ped>();
        private const float CellSize = 40f;
        private const float SpawnMin = 28f, SpawnMax = 80f, DespawnDist = 100f;
        private int target = 28;
        private float spawnTimer;
        private int checkIndex;
        private bool raining;
        private readonly System.Random rnd = new System.Random(4242);
        private Vector3 lastPlayer;
        private float playerSpeed;
        private int seedCounter = 1000;

        public int Count { get { return peds.Count; } }

        // ------------------------------------------------------------------
        // Foules aux arrêts : beaucoup de personnes des deux côtés de la rue. Elles sont posées puis figées
        // (maillage fixe, aucune animation à calculer) pour rester légères sur téléphone.
        // ------------------------------------------------------------------
        private class StopGroup
        {
            public ClientStop Stop;
            public Vector3 Axis, Dir, Normal;
            public float HalfWidth;
            public readonly List<GameObject> People = new List<GameObject>();
            public int Target;
            public float Refill;
        }
        private static ImportedWorld mapRef;
        private readonly Dictionary<ClientStop, StopGroup> stopGroups = new Dictionary<ClientStop, StopGroup>();
        private readonly List<ClientStop> stopRemove = new List<ClientStop>();
        private float stopTimer;
        public int StopPeople { get; private set; }

        /// <summary>Personnes par côté de rue à un arrêt, selon la qualité graphique.</summary>
        public static int PerSide { get { return GameSettings.Quality == 0 ? 10 : (GameSettings.Quality == 1 ? 18 : 25); } }

        private void UpdateStops(Vector3 pp)
        {
            stopTimer -= Time.unscaledDeltaTime;
            bool scan = stopTimer <= 0f;
            if (scan)
            {
                stopTimer = 0.5f;
                foreach (ClientStop cs in ClientStop.All)
                {
                    if (cs == null || stopGroups.ContainsKey(cs)) continue;
                    if ((cs.transform.position - pp).sqrMagnitude > 95f * 95f) continue;
                    if (stopGroups.Count >= 4) break;
                    StopGroup g = MakeGroup(cs);
                    if (g != null) stopGroups[cs] = g;
                }
                stopRemove.Clear();
                foreach (KeyValuePair<ClientStop, StopGroup> kv in stopGroups)
                    if (kv.Key == null || (kv.Key.transform.position - pp).sqrMagnitude > 135f * 135f) stopRemove.Add(kv.Key);
                foreach (ClientStop cs in stopRemove)
                {
                    foreach (GameObject go in stopGroups[cs].People) if (go != null) { Destroy(go); StopPeople--; }
                    stopGroups.Remove(cs);
                }
            }
            // Remplissage progressif : deux personnes par image au plus (pas d'à-coup)
            int budget = 2;
            foreach (KeyValuePair<ClientStop, StopGroup> kv in stopGroups)
            {
                StopGroup g = kv.Value;
                if (g.Refill > 0f) { g.Refill -= Time.deltaTime; continue; }
                while (budget > 0 && g.People.Count < g.Target)
                {
                    budget--;
                    AddStopPerson(g);
                }
                if (budget <= 0) break;
            }
        }

        private StopGroup MakeGroup(ClientStop cs)
        {
            Vector3 p = cs.transform.position;
            StopGroup g = new StopGroup { Stop = cs, Target = PerSide * 2 };
            // Rue la plus proche de l'arrêt : la foule se tient le long de ses deux bords
            float best = 45f * 45f;
            bool found = false;
            if (mapRef != null)
            {
                Vector2 p2 = new Vector2(p.x, p.z);
                foreach (ImportedRoad r in mapRef.roads)
                {
                    if (r.rank < 2) continue;
                    for (int i = 0; i < r.pts.Count - 1; i++)
                    {
                        Vector2 a = r.pts[i], b = r.pts[i + 1];
                        if (Mathf.Abs(a.x - p2.x) > 260f || Mathf.Abs(a.y - p2.y) > 260f) continue;
                        Vector2 ab = b - a;
                        float len2 = ab.sqrMagnitude;
                        if (len2 < 0.01f) continue;
                        float t = Mathf.Clamp01(Vector2.Dot(p2 - a, ab) / len2);
                        Vector2 q = a + ab * t;
                        float d = (q - p2).sqrMagnitude - r.rank * 4f;
                        if (d >= best) continue;
                        best = d;
                        found = true;
                        Vector2 dir = ab / Mathf.Sqrt(len2);
                        g.Axis = new Vector3(q.x, 0f, q.y);
                        g.Dir = new Vector3(dir.x, 0f, dir.y);
                        g.Normal = new Vector3(-dir.y, 0f, dir.x);
                        g.HalfWidth = r.width * 0.5f;
                    }
                }
            }
            if (!found)
            {
                // Piste d'essai ou arrêt loin de toute rue importée : petit groupe autour de l'arrêt
                g.Axis = p;
                g.Dir = cs.transform.right;
                g.Normal = cs.transform.forward;
                g.HalfWidth = 0.5f;
                g.Target = Mathf.Min(g.Target, 12);
            }
            return g;
        }

        private void AddStopPerson(StopGroup g)
        {
            int index = g.People.Count;
            float side = index % 2 == 0 ? 1f : -1f;
            Vector3 pos = Vector3.zero;
            bool ok = false;
            for (int attempt = 0; attempt < 6 && !ok; attempt++)
            {
                // Attroupement autour de l'arrêt (plus dense au milieu), sur 1 à 4 m de profondeur
                float along = (((float)rnd.NextDouble() + (float)rnd.NextDouble()) - 1f) * 13f;
                float lateral = g.HalfWidth + 1.0f + (float)rnd.NextDouble() * (2.6f + attempt * 0.4f);
                pos = g.Axis + g.Dir * along + g.Normal * side * lateral;
                if (CityFill.IsBlocked(pos, 0.35f)) continue;
                ok = true;
                foreach (GameObject o in g.People)
                    if (o != null && (o.transform.position - pos).sqrMagnitude < 0.55f * 0.55f) { ok = false; break; }
            }
            if (!ok) { g.People.Add(null); return; }
            string outfit;
            bool woman;
            GameObject prefab = PickWalker(out outfit, out woman);
            if (outfit == "police") { prefab = Pick("Hommes"); outfit = "civil"; }
            if (prefab == null) { g.People.Add(null); return; }
            pos.y = Ground(pos, 0f);
            GameObject go = HumanLibrary.Instantiate(prefab, transform, transform.InverseTransformPoint(pos), woman && rnd.Next(10) < 6 ? "pagne" : outfit, false, seedCounter++, false);
            go.name = "Arret_" + go.name;
            go.transform.position = pos;
            // La plupart regardent la rue (on guette le taxi), quelques-uns discutent ou regardent le téléphone
            Vector3 look = -g.Normal * side;
            float turn = ((float)rnd.NextDouble() - 0.5f) * 110f;
            go.transform.rotation = Quaternion.AngleAxis(turn, Vector3.up) * Quaternion.LookRotation(look);
            HumanFigure f = go.GetComponent<HumanFigure>();
            if (f != null) f.enabled = false;
            HumanAvatar av = go.GetComponent<HumanAvatar>();
            if (av != null)
            {
                int k = rnd.Next(100);
                HumanAction a = k < 34 ? HumanAction.Wait : (k < 58 ? HumanAction.Idle : (k < 76 ? HumanAction.Idle2 : (k < 88 ? HumanAction.Phone : (k < 95 ? HumanAction.Talk : HumanAction.Wave))));
                if (raining && rnd.Next(3) == 0) a = HumanAction.Umbrella;
                // La pose doit être calculée même hors du champ de la caméra, sinon le personnage serait figé bras en croix
                if (av.Anim != null) av.Anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                av.Play(a, 0f, true);
                if (a == HumanAction.Umbrella) HumanProps.HandProp(av, true);
                else if (a == HumanAction.Phone) HumanProps.HandProp(av, false);
                else if (woman && !av.Child && rnd.Next(100) < 14) HumanProps.Basin(av, rnd);
            }
            HumanLibrary.MakeStatic(go, false);
            g.People.Add(go);
            StopPeople++;
        }

        /// <summary>Un taxi ou un bus s'arrête : les personnes les plus proches montent (elles quittent le groupe)
        /// et quelques passagers descendent et s'en vont à pied. Le groupe se reforme peu à peu.</summary>
        public int Board(Vector3 vehiclePos, int count)
        {
            int boarded = 0;
            foreach (KeyValuePair<ClientStop, StopGroup> kv in stopGroups)
            {
                StopGroup g = kv.Value;
                if ((g.Axis - vehiclePos).sqrMagnitude > 30f * 30f) continue;
                for (int n = 0; n < count; n++)
                {
                    int bestI = -1;
                    float bestD = 12f * 12f;
                    for (int i = 0; i < g.People.Count; i++)
                    {
                        if (g.People[i] == null) continue;
                        float d = (g.People[i].transform.position - vehiclePos).sqrMagnitude;
                        if (d < bestD) { bestD = d; bestI = i; }
                    }
                    if (bestI < 0) break;
                    Destroy(g.People[bestI]);
                    g.People.RemoveAt(bestI);
                    StopPeople--;
                    boarded++;
                }
                g.People.RemoveAll(o => o == null);
                g.Refill = 6f + (float)rnd.NextDouble() * 8f;
                break;
            }
            // Passagers qui descendent : des marcheurs partent du trottoir le plus proche
            int alight = rnd.Next(0, 3);
            for (int n = 0; n < alight && peds.Count < target + 6; n++)
            {
                List<Vector2Int> cell;
                if (!grid.TryGetValue(KeyAt(vehiclePos), out cell) || cell.Count == 0) break;
                Vector2Int ls = cell[0];
                float bd = float.MaxValue;
                foreach (Vector2Int c in cell)
                {
                    float d = (lanes[c.x].P[c.y] - vehiclePos).sqrMagnitude;
                    if (d < bd) { bd = d; ls = c; }
                }
                if (bd > 18f * 18f) break;
                Lane l = lanes[ls.x];
                Vector3 a = l.P[ls.y], b = l.P[ls.y + 1];
                Vector3 ab = b - a;
                float t = ab.sqrMagnitude > 0.01f ? Mathf.Clamp01(Vector3.Dot(vehiclePos - a, ab) / ab.sqrMagnitude) : 0f;
                SpawnWalker(ls, t, Vector3.Lerp(a, b, t));
            }
            return boarded;
        }

        public static CrowdSystem Create(Transform parent, List<List<Vector3>> polylines)
        {
            GameObject g = new GameObject("Foule");
            g.transform.SetParent(parent, false);
            Instance = g.AddComponent<CrowdSystem>();
            Instance.Build(polylines);
            Instance.SetTimeOfDay(9f, false);
            return Instance;
        }

        /// <summary>Trottoirs : chemins piétons importés d'OpenStreetMap, sinon trottoirs de la carte, sinon bords des avenues.</summary>
        public static List<List<Vector3>> CollectLanes(WorldContext w, ImportedWorld map)
        {
            List<List<Vector3>> list = new List<List<Vector3>>();
            mapRef = map;
            if (map != null && map.walkPaths != null)
                foreach (ImportedPath p in map.walkPaths)
                    if (p != null && p.points != null && p.points.Count >= 2) list.Add(new List<Vector3>(p.points));
            if (list.Count == 0 && w != null)
            {
                foreach (TrafficPath tp in w.WalkPaths)
                {
                    if (tp.Points.Count < 2) continue;
                    List<Vector3> l = new List<Vector3>(tp.Points);
                    l.Add(tp.Points[0]);
                    list.Add(l);
                }
                if (list.Count < 6)
                    foreach (TrafficPath tp in w.TrafficPaths)
                        if (tp.Points.Count >= 3) list.Add(Offset(tp.Points, 5.2f));
            }
            return list;
        }

        private static List<Vector3> Offset(List<Vector3> pts, float offset)
        {
            List<Vector3> o = new List<Vector3>(pts.Count + 1);
            int n = pts.Count;
            for (int i = 0; i <= n; i++)
            {
                Vector3 prev = pts[(i - 1 + n) % n], next = pts[(i + 1) % n];
                Vector3 d = next - prev;
                d.y = 0f;
                if (d.sqrMagnitude < 1e-6f) continue;
                d.Normalize();
                Vector3 right = new Vector3(d.z, 0f, -d.x);
                Vector3 p = pts[i % n] + right * offset;
                p.y = 0f;
                o.Add(p);
            }
            return o;
        }

        private static long Key(int cx, int cz) { return ((long)cx << 32) ^ (uint)cz; }
        private static long KeyAt(Vector3 p) { return Key(Mathf.FloorToInt(p.x / CellSize), Mathf.FloorToInt(p.z / CellSize)); }

        private void Build(List<List<Vector3>> polylines)
        {
            if (polylines == null) return;
            foreach (List<Vector3> pl in polylines)
            {
                if (pl == null || pl.Count < 2) continue;
                int li = lanes.Count;
                lanes.Add(new Lane { P = pl.ToArray() });
                for (int s = 0; s < pl.Count - 1; s++)
                {
                    Vector3 a = pl[s], b = pl[s + 1];
                    float len = Vector3.Distance(a, b);
                    if (len < 0.5f) continue;
                    int steps = Mathf.Max(1, Mathf.CeilToInt(len / (CellSize * 0.5f)));
                    long lastKey = long.MinValue;
                    for (int k = 0; k <= steps; k++)
                    {
                        long key = KeyAt(Vector3.Lerp(a, b, k / (float)steps));
                        if (key == lastKey) continue;
                        lastKey = key;
                        List<Vector2Int> cell;
                        if (!grid.TryGetValue(key, out cell)) { cell = new List<Vector2Int>(); grid[key] = cell; }
                        cell.Add(new Vector2Int(li, s));
                    }
                }
            }
        }

        /// <summary>Plus de monde le matin et en fin d'après-midi, peu la nuit, moins sous la pluie.</summary>
        public void SetTimeOfDay(float hour, bool rain)
        {
            raining = rain;
            int q = GameSettings.Quality;
            float baseCount = q == 0 ? 14f : (q == 1 ? 28f : 46f);
            float f = 1f;
            if (hour >= 21f || hour < 5f) f = 0.3f;
            else if (hour < 6.5f || hour >= 19f) f = 0.6f;
            else if (hour < 9f) f = 1.15f;
            else if (hour >= 16f) f = 1.2f;
            if (rain) f *= 0.6f;
            target = Mathf.Max(4, Mathf.RoundToInt(baseCount * f));
        }

        private bool PlayerPos(out Vector3 p)
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
            if (lanes.Count == 0 || !HumanLibrary.Available) return;
            Vector3 pp;
            if (!PlayerPos(out pp)) return;
            float dt = Time.deltaTime;
            if (dt > 0f) playerSpeed = Mathf.Lerp(playerSpeed, (pp - lastPlayer).magnitude / dt, 0.2f);
            lastPlayer = pp;

            UpdateStops(pp);
            UpdateShoves(dt);
            spawnTimer -= Time.unscaledDeltaTime;
            if (spawnTimer <= 0f && peds.Count < target)
            {
                spawnTimer = 0.12f;
                TrySpawn(pp);
            }
            for (int k = 0; k < 3 && peds.Count > 0; k++)
            {
                checkIndex = (checkIndex + 1) % peds.Count;
                Ped p = peds[checkIndex];
                if (p.Go == null) { peds.RemoveAt(checkIndex); continue; }
                float d2 = (p.Go.transform.position - pp).sqrMagnitude;
                if (d2 > DespawnDist * DespawnDist)
                {
                    if (p.Static || peds.Count > target) Remove(checkIndex);
                    else Relocate(p, pp);
                }
            }
            if (dt <= 0f) return;
            for (int i = 0; i < peds.Count; i++)
            {
                Ped p = peds[i];
                if (p.Go == null) continue;
                if (p.Stun > 0f) { p.Stun -= dt; continue; }
                if (p.Static) continue;
                if (p.Standing)
                {
                    p.Timer -= dt;
                    if (p.Timer <= 0f) StartWalking(p);
                    continue;
                }
                Walk(p, dt, pp);
            }
        }

        // ------------------------------------------------------------------
        // Piétons bousculés par le véhicule du joueur
        // ------------------------------------------------------------------
        private static bool Touch(Transform t, Vector3 c, Vector3 f, Vector3 r, float hw, float hl)
        {
            Vector3 d = t.position - c;
            return Mathf.Abs(Vector3.Dot(d, f)) < hl + 0.25f && Mathf.Abs(Vector3.Dot(d, r)) < hw + 0.3f;
        }

        private bool IsShoved(Transform t)
        {
            for (int i = 0; i < shoves.Count; i++) if (shoves[i].T == t) return true;
            return false;
        }

        private Vector3 StartShove(Transform t, Vector3 vehCenter, Vector3 vehRight)
        {
            Vector3 side = t.position - vehCenter;
            side.y = 0f;
            Vector3 dir = vehRight * (Vector3.Dot(side, vehRight) >= 0f ? 1f : -1f);
            float dist = CityFill.IsBlocked(t.position + dir * 1.1f, 0.3f) ? 0.4f : 1.1f;
            shoves.Add(new Shove { T = t, From = t.position, Push = dir * dist, Rot = t.rotation, Axis = Vector3.Cross(Vector3.up, dir) });
            Bumped++;
            return dir * dist;
        }

        private void UpdateShoves(float dt)
        {
            for (int i = shoves.Count - 1; i >= 0; i--)
            {
                Shove sh = shoves[i];
                if (sh.T == null) { shoves.RemoveAt(i); continue; }
                sh.Time += dt;
                float t = sh.Time;
                float push = Mathf.Clamp01(t / 0.25f);
                push = 1f - (1f - push) * (1f - push);
                // Chute en 0,3 s, au sol jusqu'à 1,4 s, debout à 2,1 s
                float tilt = t < 0.3f ? t / 0.3f : (t < 1.4f ? 1f : Mathf.Clamp01(1f - (t - 1.4f) / 0.7f));
                sh.T.position = sh.From + sh.Push * push;
                sh.T.rotation = Quaternion.AngleAxis(78f * tilt, sh.Axis) * sh.Rot;
                if (t > 2.1f)
                {
                    sh.T.rotation = sh.Rot;
                    shoves.RemoveAt(i);
                }
            }
        }

        /// <summary>Vrai si le véhicule du joueur (rectangle au sol) touche un piéton ou une personne d'un arrêt :
        /// elle est bousculée (écart, chute, elle se relève).</summary>
        public bool Bump(Vector3 center, Vector3 fwd, float halfW, float halfL, out Vector3 where)
        {
            where = Vector3.zero;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) return false;
            fwd.Normalize();
            Vector3 right = new Vector3(fwd.z, 0f, -fwd.x);
            for (int i = 0; i < peds.Count; i++)
            {
                Ped p = peds[i];
                if (p.Go == null || p.Stun > 0f || !Touch(p.Go.transform, center, fwd, right, halfW, halfL)) continue;
                where = p.Go.transform.position;
                p.Stun = 2.3f;
                Vector3 push = StartShove(p.Go.transform, center, right);
                if (p.Av != null) p.Av.Play(HumanAction.Crouch);
                // Le piéton reprend ensuite sa marche à l'endroit où il a été poussé (pas de retour brusque)
                if (!p.Static && p.Lane < lanes.Count)
                {
                    Lane l = lanes[p.Lane];
                    if (p.Seg < l.P.Length - 1)
                    {
                        Vector3 pf = SegmentDir(l, p.Seg) * p.Dir;
                        Vector3 pr = new Vector3(pf.z, 0f, -pf.x);
                        p.BaseSide = Mathf.Clamp(p.BaseSide + Vector3.Dot(push, pr), -2.5f, 2.5f);
                        p.Side = p.BaseSide;
                        p.Dodge = 0f;
                    }
                }
                return true;
            }
            foreach (KeyValuePair<ClientStop, StopGroup> kv in stopGroups)
                foreach (GameObject person in kv.Value.People)
                {
                    if (person == null || IsShoved(person.transform) || !Touch(person.transform, center, fwd, right, halfW, halfL)) continue;
                    where = person.transform.position;
                    StartShove(person.transform, center, right);
                    return true;
                }
            return false;
        }

        // ------------------------------------------------------------------
        // Apparition
        // ------------------------------------------------------------------
        private bool FindSpot(Vector3 pp, out Vector2Int ls, out float t, out Vector3 pos)
        {
            Camera cam = Camera.main;
            for (int attempt = 0; attempt < 10; attempt++)
            {
                float ang = (float)rnd.NextDouble() * Mathf.PI * 2f;
                float rad = Mathf.Lerp(SpawnMin, SpawnMax, Mathf.Sqrt((float)rnd.NextDouble()));
                Vector3 c = pp + new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad);
                List<Vector2Int> cell;
                if (!grid.TryGetValue(KeyAt(c), out cell) || cell.Count == 0) continue;
                ls = cell[rnd.Next(cell.Count)];
                t = (float)rnd.NextDouble();
                Lane l = lanes[ls.x];
                pos = Vector3.Lerp(l.P[ls.y], l.P[ls.y + 1], t);
                float d = Vector3.Distance(new Vector3(pos.x, 0f, pos.z), new Vector3(pp.x, 0f, pp.z));
                if (d < SpawnMin * 0.8f || d > DespawnDist * 0.9f) continue;
                // Apparition hors du champ de la caméra quand c'est possible (pas de personnage qui surgit sous les yeux)
                if (cam != null && attempt < 7 && d < 60f)
                {
                    Vector3 toPos = pos - cam.transform.position;
                    toPos.y = 0f;
                    Vector3 cf = cam.transform.forward;
                    cf.y = 0f;
                    if (toPos.sqrMagnitude > 0.01f && cf.sqrMagnitude > 0.01f && Vector3.Dot(cf.normalized, toPos.normalized) > 0.45f) continue;
                }
                return true;
            }
            ls = Vector2Int.zero;
            t = 0f;
            pos = Vector3.zero;
            return false;
        }

        private void TrySpawn(Vector3 pp)
        {
            Vector2Int ls;
            float t;
            Vector3 pos;
            if (!FindSpot(pp, out ls, out t, out pos)) return;
            double r = rnd.NextDouble();
            if (r < 0.16 && peds.Count + 2 < target + 3) SpawnGroup(ls, t, pos);
            else if (r < 0.24) SpawnVendor(ls, t, pos);
            else SpawnWalker(ls, t, pos);
        }

        private Ped Create(GameObject prefab, Vector3 pos, string outfit)
        {
            if (prefab == null) return null;
            int seed = seedCounter++;
            GameObject g = HumanLibrary.Instantiate(prefab, transform, transform.InverseTransformPoint(pos), outfit, false, seed, false);
            HumanFigure f = g.GetComponent<HumanFigure>();
            if (f != null) f.enabled = false;
            Ped p = new Ped { Go = g, Av = g.GetComponent<HumanAvatar>(), GroundY = pos.y };
            p.GroundTimer = (float)rnd.NextDouble();
            peds.Add(p);
            return p;
        }

        private GameObject PickWalker(out string outfit, out bool woman)
        {
            int r = rnd.Next(100);
            woman = false;
            outfit = "civil";
            if (r < 44) { woman = true; return HumanLibrary.Woman(rnd); }
            if (r < 82) return Pick("Hommes");
            if (r < 90) { outfit = "travail"; return Pick("Ouvriers"); }
            if (r < 98) return Pick("Enfants");
            outfit = "police";
            return Pick("Police");
        }

        private GameObject Pick(string cat)
        {
            GameObject[] list = HumanLibrary.Load(cat);
            if (list.Length == 0) list = HumanLibrary.Load("Hommes");
            return list.Length == 0 ? null : list[rnd.Next(list.Length)];
        }

        private void SpawnWalker(Vector2Int ls, float t, Vector3 pos)
        {
            string outfit;
            bool woman;
            GameObject prefab = PickWalker(out outfit, out woman);
            Ped p = Create(prefab, pos, woman && rnd.Next(10) < 6 ? "pagne" : outfit);
            if (p == null) return;
            if (woman && p.Av != null && !p.Av.Child && rnd.Next(100) < 22)
            {
                HumanProps.Basin(p.Av, rnd);
                p.Slow = true;
            }
            PlaceOnLane(p, ls, t);
            if (rnd.Next(100) < 25) StartStanding(p, 4f + (float)rnd.NextDouble() * 14f);
            else StartWalking(p);
        }

        private void PlaceOnLane(Ped p, Vector2Int ls, float t)
        {
            p.Lane = ls.x;
            p.Seg = ls.y;
            p.T = t;
            p.Dir = rnd.Next(2) == 0 ? 1 : -1;
            p.BaseSide = 0.4f + (float)rnd.NextDouble() * 0.9f;
            p.Side = p.BaseSide;
            p.Static = false;
            Lane l = lanes[p.Lane];
            Vector3 fwd = SegmentDir(l, p.Seg) * p.Dir;
            Vector3 pos = Vector3.Lerp(l.P[p.Seg], l.P[p.Seg + 1], p.T) + new Vector3(fwd.z, 0f, -fwd.x) * p.Side;
            p.GroundY = Ground(pos, l.P[p.Seg].y);
            pos.y = p.GroundY;
            p.Go.transform.position = pos;
            p.Go.transform.rotation = Quaternion.LookRotation(fwd);
        }

        private void SpawnGroup(Vector2Int ls, float t, Vector3 pos)
        {
            Lane l = lanes[ls.x];
            Vector3 dir = SegmentDir(l, ls.y);
            Vector3 right = new Vector3(dir.z, 0f, -dir.x) * (rnd.Next(2) == 0 ? 1f : -1f);
            Vector3 center = pos + right * (1.6f + (float)rnd.NextDouble());
            // Jamais dans une parcelle clôturée : on essaie l'autre côté, sinon pas de groupe ici
            if (CityFill.IsBlocked(center, 1.3f)) center = pos - right * 1.2f;
            if (CityFill.IsBlocked(center, 1.3f)) return;
            center.y = Ground(center, pos.y);
            int n = 2 + rnd.Next(2);
            float a0 = (float)rnd.NextDouble() * 360f;
            for (int i = 0; i < n; i++)
            {
                string outfit;
                bool woman;
                GameObject prefab = PickWalker(out outfit, out woman);
                float ang = (a0 + i * 360f / n) * Mathf.Deg2Rad;
                Vector3 p0 = center + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * 0.75f;
                Ped p = Create(prefab, p0, woman ? "pagne" : outfit);
                if (p == null) continue;
                p.Static = true;
                p.Go.transform.position = p0;
                Vector3 look = center - p0;
                look.y = 0f;
                if (look.sqrMagnitude > 0.001f) p.Go.transform.rotation = Quaternion.LookRotation(look);
                if (p.Av != null)
                {
                    HumanAction a = raining && rnd.Next(2) == 0 ? HumanAction.Umbrella : (i % 2 == 0 ? HumanAction.Talk : (rnd.Next(3) == 0 ? HumanAction.Phone : HumanAction.Idle2));
                    p.Av.Play(a, 0f, true);
                    if (a == HumanAction.Umbrella) HumanProps.HandProp(p.Av, true);
                    else if (a == HumanAction.Phone) HumanProps.HandProp(p.Av, false);
                }
            }
        }

        /// <summary>Vendeuse accroupie devant une bassine de marchandises (bord de route, arrêts).</summary>
        private void SpawnVendor(Vector2Int ls, float t, Vector3 pos)
        {
            Lane l = lanes[ls.x];
            Vector3 dir = SegmentDir(l, ls.y);
            Vector3 right = new Vector3(dir.z, 0f, -dir.x) * (rnd.Next(2) == 0 ? 1f : -1f);
            Vector3 p0 = pos + right * 1.8f;
            if (CityFill.IsBlocked(p0, 0.9f)) { right = -right; p0 = pos + right * 1.1f; }
            if (CityFill.IsBlocked(p0, 0.9f)) return;
            p0.y = Ground(p0, pos.y);
            Ped p = Create(HumanLibrary.Woman(rnd), p0, "pagne");
            if (p == null) return;
            p.Static = true;
            p.Go.transform.position = p0;
            p.Go.transform.rotation = Quaternion.LookRotation(-right);
            if (p.Av != null) p.Av.Play(HumanAction.Crouch, 0f, true);
            Transform tr = p.Go.transform;
            Color[] goods = { new Color(0.95f, 0.8f, 0.15f), new Color(0.85f, 0.35f, 0.1f), new Color(0.35f, 0.6f, 0.15f), new Color(0.75f, 0.55f, 0.3f) };
            Color gc = goods[rnd.Next(goods.Length)];
            float s = 1f / Mathf.Max(0.01f, tr.localScale.x);
            GameObject basin = Prims.Cyl(tr, "Bassine", new Vector3(0f, 0.06f, 0.75f) * s, 0.6f * s, 0.12f * s, new Color(0.75f, 0.76f, 0.78f));
            Prims.NoShadows(basin);
            for (int i = 0; i < 6; i++)
            {
                float ang = i * 60f * Mathf.Deg2Rad;
                Vector3 off = new Vector3(Mathf.Cos(ang) * 0.15f, 0.16f, 0.75f + Mathf.Sin(ang) * 0.15f) * s;
                GameObject g = Prims.Ball(tr, "Marchandise", off, Vector3.one * 0.13f * s, ColorUtil.Shade(gc, 0.85f + 0.05f * (i % 3)));
                Prims.NoShadows(g);
            }
            GameObject cloth = Prims.Box(tr, "Pagne", new Vector3(0f, 0.005f, 0.75f) * s, new Vector3(1.0f, 0.01f, 0.8f) * s, HumanProps.RandomFabric(rnd));
            Prims.NoShadows(cloth);
        }

        private void Remove(int index)
        {
            Ped p = peds[index];
            if (p.Go != null) Destroy(p.Go);
            peds.RemoveAt(index);
        }

        private void Relocate(Ped p, Vector3 pp)
        {
            Vector2Int ls;
            float t;
            Vector3 pos;
            if (!FindSpot(pp, out ls, out t, out pos)) return;
            PlaceOnLane(p, ls, t);
            StartWalking(p);
        }

        // ------------------------------------------------------------------
        // Marche
        // ------------------------------------------------------------------
        private void StartWalking(Ped p)
        {
            p.Standing = false;
            if (p.Av == null) return;
            RemoveNamed(p.Go, "Parapluie", "Toile");
            float cycle = p.Slow ? p.Av.WalkCycleSpeed * 0.7f : p.Av.WalkCycleSpeed;
            p.Speed = cycle * (0.88f + (float)rnd.NextDouble() * 0.22f);
            p.Av.Play(p.Slow ? HumanAction.WalkSlow : HumanAction.Walk, 0.3f, true);
            p.Av.SetPlaybackSpeed(p.Speed / Mathf.Max(0.1f, cycle));
        }

        private void StartStanding(Ped p, float duration)
        {
            p.Standing = true;
            p.Timer = duration;
            if (p.Av == null) return;
            p.Av.SetPlaybackSpeed(1f);
            HumanAction a;
            int r = rnd.Next(100);
            if (raining && !p.Slow) a = HumanAction.Umbrella;
            else if (p.Slow) a = HumanAction.Idle;
            else if (r < 30) a = HumanAction.Phone;
            else if (r < 55) a = HumanAction.Wait;
            else if (r < 78) a = HumanAction.Idle2;
            else a = HumanAction.Idle;
            p.Av.Play(a, 0.3f, true);
            if (a == HumanAction.Umbrella || a == HumanAction.Phone)
            {
                // Un seul objet en main à la fois (pas d'accumulation au fil des arrêts)
                RemoveNamed(p.Go, "Telephone", "Parapluie");
                RemoveNamed(p.Go, "Toile", "Toile");
                HumanProps.HandProp(p.Av, a == HumanAction.Umbrella);
            }
        }

        private static void RemoveNamed(GameObject g, string a, string b)
        {
            foreach (Transform t in g.GetComponentsInChildren<Transform>(true))
                if (t != null && (t.name == a || t.name == b)) Destroy(t.gameObject);
        }

        private static Vector3 SegmentDir(Lane l, int seg)
        {
            Vector3 d = l.P[seg + 1] - l.P[seg];
            d.y = 0f;
            return d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.forward;
        }

        private bool Advance(Ped p, Lane l)
        {
            if (p.Dir > 0)
            {
                if (p.Seg + 1 >= l.P.Length - 1) { p.T = 1f; TurnAround(p); return false; }
                p.Seg++;
                p.T = 0f;
            }
            else
            {
                if (p.Seg - 1 < 0) { p.T = 0f; TurnAround(p); return false; }
                p.Seg--;
                p.T = 1f;
            }
            return true;
        }

        private void TurnAround(Ped p)
        {
            p.Dir = -p.Dir;
            StartStanding(p, 1f + (float)rnd.NextDouble() * 3f);
        }

        private void Walk(Ped p, float dt, Vector3 pp)
        {
            Lane l = lanes[p.Lane];
            float move = p.Speed * dt;
            int guard = 0;
            while (move > 0f && guard++ < 8)
            {
                float segLen = Vector3.Distance(l.P[p.Seg], l.P[p.Seg + 1]);
                if (segLen < 0.01f) { if (!Advance(p, l)) break; continue; }
                float dT = move / segLen;
                if (p.Dir > 0)
                {
                    p.T += dT;
                    if (p.T < 1f) move = 0f;
                    else { move = (p.T - 1f) * segLen; p.T = 1f; if (!Advance(p, l)) break; }
                }
                else
                {
                    p.T -= dT;
                    if (p.T > 0f) move = 0f;
                    else { move = -p.T * segLen; p.T = 0f; if (!Advance(p, l)) break; }
                }
            }
            if (p.Standing) return;

            Vector3 fwd = SegmentDir(l, p.Seg) * p.Dir;
            Vector3 right = new Vector3(fwd.z, 0f, -fwd.x);
            Vector3 basePos = Vector3.Lerp(l.P[p.Seg], l.P[p.Seg + 1], p.T);

            // Écart quand le joueur arrive vite à moins de 5 m
            Vector3 toPlayer = pp - basePos;
            toPlayer.y = 0f;
            float dodgeTarget = 0f;
            if (playerSpeed > 2f && toPlayer.sqrMagnitude < 25f)
            {
                float side = Vector3.Dot(toPlayer, right);
                dodgeTarget = side > 0f ? -1.8f : 1.8f;
                // Pas d'écart vers un mur de parcelle : de l'autre côté, ou un petit pas seulement
                if (CityFill.IsBlocked(basePos + right * (p.BaseSide + dodgeTarget), 0.35f)) dodgeTarget = -dodgeTarget * 0.6f;
            }
            p.Dodge = Mathf.MoveTowards(p.Dodge, dodgeTarget, dt * (dodgeTarget != 0f ? 4f : 1f));
            p.Side = Mathf.MoveTowards(p.Side, p.BaseSide + p.Dodge, dt * 2f);

            Vector3 pos = basePos + right * p.Side;
            p.GroundTimer -= dt;
            if (p.GroundTimer <= 0f)
            {
                p.GroundTimer = 1.2f + (float)rnd.NextDouble();
                p.GroundY = Ground(pos, basePos.y);
            }
            pos.y = p.GroundY;
            Transform tr = p.Go.transform;
            tr.position = pos;
            Quaternion want = Quaternion.LookRotation(fwd);
            tr.rotation = Quaternion.Slerp(tr.rotation, want, Mathf.Clamp01(dt * 6f));
        }

        /// <summary>Hauteur du sol (trottoir, terre, chaussée) sous un point.</summary>
        private static float Ground(Vector3 p, float fallback)
        {
            RaycastHit hit;
            int mask = ~(1 << RaycastVehicle.PlayerLayer);
            if (Physics.Raycast(new Vector3(p.x, fallback + 3f, p.z), Vector3.down, out hit, 8f, mask, QueryTriggerInteraction.Ignore) && hit.point.y < fallback + 0.8f)
                return hit.point.y;
            return Mathf.Min(fallback, 0.08f);
        }
    }
}
