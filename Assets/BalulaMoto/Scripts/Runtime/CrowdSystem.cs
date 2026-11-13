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
        }

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
        // Apparition
        // ------------------------------------------------------------------
        private bool FindSpot(Vector3 pp, out Vector2Int ls, out float t, out Vector3 pos)
        {
            for (int attempt = 0; attempt < 8; attempt++)
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
            if (a == HumanAction.Umbrella) HumanProps.HandProp(p.Av, true);
            else if (a == HumanAction.Phone) HumanProps.HandProp(p.Av, false);
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
