// BALULA MOTO — circulation : trajets, véhicules PNJ (taxis, minibus, bus, camions, motos), piétons,
// densité selon l'heure, pannes, embouteillages, poursuite et escorte de police scénarisées.
using System.Collections.Generic;
using UnityEngine;

namespace BalulaMoto.Runtime
{
    public class TrafficPath
    {
        public string Name;
        public readonly List<Vector3> Points = new List<Vector3>();
        public TrafficLightController Light;
        public readonly List<float> StopS = new List<float>();
        public readonly List<LightAxis> StopAxes = new List<LightAxis>();
        public float Length { get; private set; }
        private float[] cumulative;

        public void Build()
        {
            int n = Points.Count;
            cumulative = new float[n + 1];
            cumulative[0] = 0f;
            for (int i = 0; i < n; i++)
                cumulative[i + 1] = cumulative[i] + Vector3.Distance(Points[i], Points[(i + 1) % n]);
            Length = cumulative[n];
        }

        public Vector3 PositionAt(float s)
        {
            if (Points.Count == 0 || Length <= 0f) return Vector3.zero;
            s = Mathf.Repeat(s, Length);
            int lo = 0, hi = Points.Count - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (cumulative[mid] <= s) lo = mid; else hi = mid - 1;
            }
            float segLen = cumulative[lo + 1] - cumulative[lo];
            float t = segLen > 0f ? (s - cumulative[lo]) / segLen : 0f;
            return Vector3.Lerp(Points[lo], Points[(lo + 1) % Points.Count], t);
        }

        public Vector3 DirectionAt(float s)
        {
            Vector3 d = PositionAt(s + 2f) - PositionAt(s - 0.5f);
            return d.sqrMagnitude > 0.0001f ? d.normalized : Vector3.forward;
        }

        public float ClosestS(Vector3 p)
        {
            float best = 0f, bestD = float.MaxValue;
            for (float s = 0f; s < Length; s += 1f)
            {
                float d = (PositionAt(s) - p).sqrMagnitude;
                if (d < bestD) { bestD = d; best = s; }
            }
            return best;
        }

        public void AddStop(Vector3 stopPoint, LightAxis axis, TrafficLightController light)
        {
            Light = light;
            StopS.Add(ClosestS(stopPoint));
            StopAxes.Add(axis);
        }

        /// <summary>Boucle fermée passant par les coins donnés, avec virages arrondis.</summary>
        public static TrafficPath Loop(string name, IList<Vector3> corners, float radius)
        {
            TrafficPath p = new TrafficPath { Name = name };
            int n = corners.Count;
            for (int i = 0; i < n; i++)
            {
                Vector3 prev = corners[(i - 1 + n) % n], c = corners[i], next = corners[(i + 1) % n];
                Vector3 a = c + (prev - c).normalized * Mathf.Min(radius, Vector3.Distance(prev, c) * 0.45f);
                Vector3 b = c + (next - c).normalized * Mathf.Min(radius, Vector3.Distance(next, c) * 0.45f);
                for (int k = 0; k <= 4; k++)
                {
                    float t = k / 4f;
                    p.Points.Add((1 - t) * (1 - t) * a + 2 * (1 - t) * t * c + t * t * b);
                }
            }
            p.Build();
            return p;
        }
    }

    public class AIVehicle : MonoBehaviour
    {
        public AIVehicleType Type;
        public TrafficPath Path;
        public float S;
        public float Speed;
        public float Cruise;
        public float Length = 4f;
        public bool BrokenDown;
        public float BreakdownTimer;
        public bool Special;
        public bool IgnoreLights;
        public bool Pursuing;
        public bool Sirens;
        public Transform PursuitTarget;
        public Rigidbody Rb;
        public List<Renderer> BrakeLights = new List<Renderer>();
        public List<Renderer> Hazards = new List<Renderer>();
        public List<Transform> Wheels = new List<Transform>();
        public float BlockedTime;
        public float HonkCooldown;
        private float wheelSpin;
        private bool brakeShown;

        public void UpdateVisuals(bool braking, float dt)
        {
            wheelSpin += Speed / 0.35f * dt * Mathf.Rad2Deg;
            foreach (Transform w in Wheels) w.localRotation = Quaternion.AngleAxis(wheelSpin, Vector3.right);
            if (braking != brakeShown)
            {
                brakeShown = braking;
                foreach (Renderer r in BrakeLights) Mats.SetLamp(r, new Color(1f, 0.08f, 0.05f), braking, 2.5f);
            }
            bool blink = (BrokenDown || Sirens) && Mathf.Repeat(Time.time, 0.7f) < 0.35f;
            for (int i = 0; i < Hazards.Count; i++)
            {
                Color c = Sirens ? (i % 2 == 0 ? new Color(1f, 0.05f, 0.05f) : new Color(0.1f, 0.3f, 1f)) : new Color(1f, 0.55f, 0.05f);
                bool on = Sirens ? (i % 2 == 0 ? blink : !blink) : blink;
                Mats.SetLamp(Hazards[i], c, on, 3f);
            }
        }
    }

    public class Walker : MonoBehaviour
    {
        public TrafficPath Path;
        public float S;
        public float Speed = 1.3f;
    }

    public class TrafficManager : MonoBehaviour
    {
        public static TrafficManager Instance;
        public readonly List<TrafficPath> Paths = new List<TrafficPath>();
        public readonly List<AIVehicle> Vehicles = new List<AIVehicle>();
        public readonly List<Walker> Walkers = new List<Walker>();
        public RaycastVehicle Player;
        private int targetCount = 16;
        private int seed = 100;
        private float densityTimer;
        private float hour = 8f;

        public static TrafficManager Create(Transform parent)
        {
            GameObject g = new GameObject("Circulation");
            g.transform.SetParent(parent, false);
            Instance = g.AddComponent<TrafficManager>();
            return Instance;
        }

        public void Init(List<TrafficPath> paths, List<TrafficPath> walkPaths, int walkers)
        {
            Paths.AddRange(paths);
            // Avec les personnages réalistes, les piétons sont gérés par la foule (CrowdSystem)
            if (HumanLibrary.Available) walkers = 0;
            if (walkPaths != null && walkPaths.Count > 0 && walkers > 0)
            {
                for (int i = 0; i < walkers; i++)
                {
                    TrafficPath wp = walkPaths[i % walkPaths.Count];
                    string[] outfits = { "pagne", "civil", "etudiant", "costume", "travail", "uniforme", "vendeur" };
                    GameObject h = VehicleBuilder.BuildHuman(transform, Vector3.zero, outfits[i % outfits.Length], false, 500 + i, false);
                    HumanFigure f = h.GetComponent<HumanFigure>();
                    if (f != null) f.Walking = true;
                    Walker w = h.AddComponent<Walker>();
                    w.Path = wp;
                    w.S = Random.Range(0f, wp.Length);
                    w.Speed = Random.Range(1.0f, 1.6f);
                    Walkers.Add(w);
                }
            }
        }

        /// <summary>Plus de circulation aux heures de pointe, moins la nuit.</summary>
        public void UpdateDensity(float h)
        {
            hour = h;
            bool rush = (h >= 6.5f && h < 9f) || (h >= 16f && h < 19f);
            bool night = h >= 21f || h < 5f;
            // Kinshasa : circulation dense, embouteillages aux heures de pointe
            targetCount = rush ? 42 : (night ? 12 : 30);
            if (GameSettings.Quality == 0) targetCount = Mathf.RoundToInt(targetCount * 0.55f);
            else if (GameSettings.Quality == 2) targetCount = Mathf.RoundToInt(targetCount * 1.3f);
            BalulaGame g = BalulaGame.Instance;
            bool rain = g != null && g.Env != null && g.Env.RainIntensity > 0.3f;
            if (CrowdSystem.Instance != null) CrowdSystem.Instance.SetTimeOfDay(h, rain);
        }

        private AIVehicleType RandomType()
        {
            // Kinshasa : beaucoup de taxis jaunes, de minibus et de motos-taxis
            float r = Random.value;
            if (r < 0.21f) return AIVehicleType.TaxiJaune;
            if (r < 0.37f) return AIVehicleType.MinibusJaune;
            if (r < 0.58f) return AIVehicleType.MotoTaxi;
            if (r < 0.67f) return AIVehicleType.Berline;
            if (r < 0.72f) return AIVehicleType.MinibusBlanc;
            if (r < 0.77f) return AIVehicleType.BusTransco;
            if (r < 0.80f) return AIVehicleType.CamionRemorque;
            if (r < 0.84f) return AIVehicleType.CamionCiterne;
            if (r < 0.89f) return AIVehicleType.Pickup;
            if (r < 0.95f) return AIVehicleType.QuatreQuatre;
            return AIVehicleType.TricycleTaxi;
        }

        public AIVehicle Spawn(AIVehicleType type, TrafficPath path, float s, float cruise)
        {
            float length;
            List<Renderer> brake, hazards;
            List<Transform> wheels;
            GameObject g = VehicleBuilder.BuildTraffic(transform, type, seed++, out length, out brake, out hazards, out wheels);
            AIVehicle v = g.AddComponent<AIVehicle>();
            v.Type = type;
            v.Path = path;
            v.S = s;
            v.Cruise = cruise;
            v.Length = length;
            v.Rb = g.GetComponent<Rigidbody>();
            v.BrakeLights = brake;
            v.Hazards = hazards;
            v.Wheels = wheels;
            if (path != null)
            {
                g.transform.position = path.PositionAt(s);
                g.transform.rotation = Quaternion.LookRotation(path.DirectionAt(s));
            }
            Vehicles.Add(v);
            return v;
        }

        public void Despawn(AIVehicle v)
        {
            if (v == null) return;
            Vehicles.Remove(v);
            Destroy(v.gameObject);
        }

        private float CruiseFor(AIVehicleType t)
        {
            switch (t)
            {
                case AIVehicleType.CamionRemorque: return Random.Range(6f, 8f);
                case AIVehicleType.CamionCiterne: return Random.Range(6f, 8.5f);
                case AIVehicleType.TricycleTaxi: return Random.Range(7f, 9.5f);
                case AIVehicleType.BusTransco: return Random.Range(9f, 11f);
                case AIVehicleType.MotoTaxi: return Random.Range(11f, 14f);
                default: return Random.Range(9.5f, 13f);
            }
        }

        // Emplacements d'apparition (tous les 20 m de chaque boucle), rangés par cases de 60 m
        private struct SpawnSlot { public int Path; public float S; public Vector3 Pos; }
        private readonly Dictionary<long, List<SpawnSlot>> spawnGrid = new Dictionary<long, List<SpawnSlot>>();
        private const float SpawnCell = 60f;
        private int gridPathCount = -1;

        private static long SpawnKey(Vector3 p)
        {
            return ((long)Mathf.FloorToInt(p.x / SpawnCell) << 32) ^ (uint)Mathf.FloorToInt(p.z / SpawnCell);
        }

        private void BuildSpawnGrid()
        {
            spawnGrid.Clear();
            for (int i = 0; i < Paths.Count; i++)
            {
                TrafficPath tp = Paths[i];
                for (float s = 0f; s < tp.Length; s += 20f)
                {
                    Vector3 pos = tp.PositionAt(s);
                    long key = SpawnKey(pos);
                    List<SpawnSlot> l;
                    if (!spawnGrid.TryGetValue(key, out l)) { l = new List<SpawnSlot>(); spawnGrid[key] = l; }
                    l.Add(new SpawnSlot { Path = i, S = s, Pos = pos });
                }
            }
            gridPathCount = Paths.Count;
        }

        /// <summary>Fait apparaître un véhicule sur une rue entre 60 et 300 m du joueur, hors du champ de vision proche.</summary>
        private bool TrySpawnNear(Vector3 pp)
        {
            Camera cam = Camera.main;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                float ang = Random.value * Mathf.PI * 2f;
                float rad = Mathf.Lerp(65f, 300f, Mathf.Sqrt(Random.value));
                Vector3 c = pp + new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad);
                List<SpawnSlot> l;
                if (!spawnGrid.TryGetValue(SpawnKey(c), out l) || l.Count == 0) continue;
                SpawnSlot slot = l[Random.Range(0, l.Count)];
                float d2 = (slot.Pos - pp).sqrMagnitude;
                if (Player != null && d2 < 55f * 55f) continue;
                if (cam != null && Player != null && d2 < 130f * 130f)
                {
                    Vector3 to = slot.Pos - cam.transform.position;
                    if (to.sqrMagnitude > 1f && Vector3.Dot(cam.transform.forward, to.normalized) > 0.55f) continue;
                }
                bool ok = true;
                foreach (AIVehicle o in Vehicles)
                    if ((o.transform.position - slot.Pos).sqrMagnitude < 14f * 14f) { ok = false; break; }
                if (!ok) continue;
                AIVehicleType type = RandomType();
                Spawn(type, Paths[slot.Path], slot.S, CruiseFor(type));
                return true;
            }
            return false;
        }

        private static float ApproxLength(AIVehicleType t)
        {
            switch (t)
            {
                case AIVehicleType.MotoTaxi: return 2.1f;
                case AIVehicleType.BusTransco: return 11f;
                case AIVehicleType.CamionRemorque: return 13f;
                case AIVehicleType.CamionCiterne: return 11.5f;
                case AIVehicleType.TricycleTaxi: return 3f;
                case AIVehicleType.Pickup: return 5.4f;
                case AIVehicleType.MinibusJaune:
                case AIVehicleType.MinibusBlanc: return 5.2f;
                default: return 4.5f;
            }
        }

        /// <summary>Embouteillage aux heures de pointe : une file de véhicules derrière un bus, un camion ou un
        /// minibus qui avance au pas. Les suivants se collent derrière (distance de sécurité) et klaxonnent.</summary>
        private bool TrySpawnJam(Vector3 pp, out int added)
        {
            added = 0;
            Camera cam = Camera.main;
            for (int attempt = 0; attempt < 6; attempt++)
            {
                float ang = Random.value * Mathf.PI * 2f;
                float rad = Mathf.Lerp(80f, 230f, Random.value);
                Vector3 c = pp + new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad);
                List<SpawnSlot> l;
                if (!spawnGrid.TryGetValue(SpawnKey(c), out l) || l.Count == 0) continue;
                SpawnSlot slot = l[Random.Range(0, l.Count)];
                TrafficPath path = Paths[slot.Path];
                if (path.Length < 200f) continue;
                int n = Random.Range(4, 8);
                AIVehicleType[] types = new AIVehicleType[n];
                float[] ss = new float[n];
                float r0 = Random.value;
                types[0] = r0 < 0.35f ? AIVehicleType.BusTransco : (r0 < 0.5f ? AIVehicleType.CamionRemorque : (r0 < 0.65f ? AIVehicleType.CamionCiterne : AIVehicleType.MinibusJaune));
                ss[0] = slot.S;
                for (int k = 1; k < n; k++)
                {
                    types[k] = RandomType();
                    ss[k] = ss[k - 1] - (ApproxLength(types[k - 1]) + ApproxLength(types[k])) * 0.5f - Random.Range(2.2f, 3.5f);
                }
                bool ok = true;
                for (int k = 0; k < n && ok; k++)
                {
                    Vector3 pos = path.PositionAt(Mathf.Repeat(ss[k], path.Length));
                    float d2 = (pos - pp).sqrMagnitude;
                    if (Player != null && d2 < 60f * 60f) { ok = false; break; }
                    if (cam != null && Player != null && d2 < 140f * 140f)
                    {
                        Vector3 to = pos - cam.transform.position;
                        if (to.sqrMagnitude > 1f && Vector3.Dot(cam.transform.forward, to.normalized) > 0.5f) { ok = false; break; }
                    }
                    foreach (AIVehicle o in Vehicles)
                        if ((o.transform.position - pos).sqrMagnitude < 10f * 10f) { ok = false; break; }
                }
                if (!ok) continue;
                for (int k = 0; k < n; k++)
                {
                    AIVehicle v = Spawn(types[k], path, Mathf.Repeat(ss[k], path.Length), k == 0 ? Random.Range(2.2f, 4.2f) : CruiseFor(types[k]));
                    v.Speed = k == 0 ? 2f : 1.5f;
                }
                added = n;
                return true;
            }
            return false;
        }

        private void Update()
        {
            densityTimer -= Time.deltaTime;
            if (densityTimer > 0f || Paths.Count == 0) return;
            densityTimer = 0.4f;
            if (gridPathCount != Paths.Count) BuildSpawnGrid();
            Vector3 pp = Player != null ? Player.transform.position : Vector3.zero;
            int normal = 0;
            AIVehicle far = null;
            foreach (AIVehicle v in Vehicles)
            {
                if (v.Special) continue;
                normal++;
                if (far == null && Player != null && (v.transform.position - pp).sqrMagnitude > 430f * 430f) far = v;
            }
            if (far != null) { Despawn(far); normal--; }
            bool rush = (hour >= 7f && hour < 9.5f) || (hour >= 16f && hour < 19.5f);
            if (normal < targetCount)
            {
                int added;
                if (rush && Random.value < 0.3f && TrySpawnJam(pp, out added)) normal += added;
                // Jusqu'à deux véhicules par passage : la circulation se remplit vite autour du joueur
                for (int k = 0; k < 2 && normal < targetCount; k++)
                    if (TrySpawnNear(pp)) normal++;
            }
            else if (normal > targetCount + (rush ? 9 : 3))
            {
                foreach (AIVehicle v in Vehicles)
                {
                    if (v.Special) continue;
                    if (Player == null || (v.transform.position - pp).sqrMagnitude > 150f * 150f) { Despawn(v); break; }
                }
            }
        }

        private float GapAhead(AIVehicle v)
        {
            float gap = 999f;
            Vector3 pos = v.transform.position, fwd = v.transform.forward, right = v.transform.right;
            foreach (AIVehicle o in Vehicles)
            {
                if (o == v) continue;
                Vector3 d = o.transform.position - pos;
                float ahead = Vector3.Dot(d, fwd);
                if (ahead <= 0f || ahead > 30f) continue;
                if (Mathf.Abs(Vector3.Dot(d, right)) > 2.3f) continue;
                gap = Mathf.Min(gap, ahead - (v.Length + o.Length) * 0.5f);
            }
            if (Player != null)
            {
                Vector3 d = Player.transform.position - pos;
                float ahead = Vector3.Dot(d, fwd);
                if (ahead > 0f && ahead < 30f && Mathf.Abs(Vector3.Dot(d, right)) < 2.4f)
                    gap = Mathf.Min(gap, ahead - (v.Length + 2f) * 0.5f);
            }
            return gap;
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            for (int i = Vehicles.Count - 1; i >= 0; i--)
            {
                AIVehicle v = Vehicles[i];
                if (v == null) { Vehicles.RemoveAt(i); continue; }
                if (v.Pursuing && v.PursuitTarget != null) { Pursue(v, dt); continue; }
                if (v.Path == null) continue;

                if (!v.Special && !v.BrokenDown && Random.value < dt / 900f)
                {
                    v.BrokenDown = true;
                    v.BreakdownTimer = Random.Range(25f, 60f);
                }
                float target = Mathf.Min(v.Cruise, Roadworks.SpeedLimit(v.transform.position));
                if (v.BrokenDown)
                {
                    v.BreakdownTimer -= dt;
                    target = 0f;
                    if (v.BreakdownTimer <= 0f) v.BrokenDown = false;
                }
                float gap = GapAhead(v);
                if (gap < 28f) target = Mathf.Min(target, Mathf.Max(0f, (gap - 3f) * 0.7f));
                if (!v.IgnoreLights && v.Path.Light != null)
                {
                    for (int k = 0; k < v.Path.StopS.Count; k++)
                    {
                        float ds = Mathf.Repeat(v.Path.StopS[k] - v.S, v.Path.Length);
                        if (ds > 32f) continue;
                        LightColor c = v.Path.Light.ColorFor(v.Path.StopAxes[k]);
                        bool mustStop = c == LightColor.Rouge || (c == LightColor.Orange && ds > 8f);
                        if (mustStop) target = Mathf.Min(target, Mathf.Max(0f, (ds - v.Length * 0.5f - 1.5f) * 0.6f));
                    }
                }
                bool braking = target < v.Speed - 0.3f;
                v.Speed = Mathf.MoveTowards(v.Speed, target, (braking ? 6f : 2.2f) * dt);
                v.S += v.Speed * dt;
                Vector3 pos = v.Path.PositionAt(v.S);
                Quaternion rot = Quaternion.Slerp(v.transform.rotation, Quaternion.LookRotation(v.Path.DirectionAt(v.S)), dt * 6f);
                if (v.Rb != null) { v.Rb.MovePosition(pos); v.Rb.MoveRotation(rot); }
                else v.transform.SetPositionAndRotation(pos, rot);
                v.UpdateVisuals(braking || v.Speed < 0.1f, dt);

                // Klaxon quand on est bloqué (embouteillage, véhicule en panne, joueur arrêté devant)
                v.BlockedTime = gap < 6f && v.Speed < 0.5f && !v.BrokenDown ? v.BlockedTime + dt : 0f;
                v.HonkCooldown -= dt;
                if (v.BlockedTime > 4f && v.HonkCooldown <= 0f)
                {
                    v.HonkCooldown = Random.Range(4f, 9f);
                    if (Player != null && (Player.transform.position - v.transform.position).sqrMagnitude < 60f * 60f)
                        AmbientAudio.HonkAt(v.transform.position, v.Type == AIVehicleType.BusTransco || v.Type == AIVehicleType.CamionRemorque || v.Type == AIVehicleType.CamionCiterne);
                }
            }
            for (int i = 0; i < Walkers.Count; i++)
            {
                Walker w = Walkers[i];
                if (w == null || w.Path == null) continue;
                w.S += w.Speed * dt;
                w.transform.position = w.Path.PositionAt(w.S);
                w.transform.rotation = Quaternion.LookRotation(w.Path.DirectionAt(w.S));
            }
        }

        private void Pursue(AIVehicle v, float dt)
        {
            Vector3 to = v.PursuitTarget.position - v.transform.position;
            to.y = 0f;
            float dist = to.magnitude;
            Vector3 fwd = v.transform.forward;
            Vector3 desired = dist > 0.1f ? to / dist : fwd;
            Vector3 newFwd = Vector3.RotateTowards(fwd, desired, Mathf.Deg2Rad * 80f * dt, 0f);
            float target = dist < 10f ? 4f : Mathf.Min(19f, 6f + dist * 0.3f);
            v.Speed = Mathf.MoveTowards(v.Speed, target, 5f * dt);
            Vector3 pos = v.transform.position + newFwd * v.Speed * dt;
            pos.y = 0f;
            Quaternion rot = Quaternion.LookRotation(newFwd);
            if (v.Rb != null) { v.Rb.MovePosition(pos); v.Rb.MoveRotation(rot); }
            else v.transform.SetPositionAndRotation(pos, rot);
            v.UpdateVisuals(false, dt);
        }
    }
}
