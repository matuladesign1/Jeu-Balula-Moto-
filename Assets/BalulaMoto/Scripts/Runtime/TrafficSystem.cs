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
            targetCount = rush ? 34 : (night ? 10 : 24);
            if (GameSettings.Quality == 0) targetCount = Mathf.RoundToInt(targetCount * 0.6f);
            else if (GameSettings.Quality == 2) targetCount = Mathf.RoundToInt(targetCount * 1.25f);
            BalulaGame g = BalulaGame.Instance;
            bool rain = g != null && g.Env != null && g.Env.RainIntensity > 0.3f;
            if (CrowdSystem.Instance != null) CrowdSystem.Instance.SetTimeOfDay(h, rain);
        }

        private AIVehicleType RandomType()
        {
            // Kinshasa : beaucoup de taxis jaunes, de minibus et de motos-taxis
            float r = Random.value;
            if (r < 0.26f) return AIVehicleType.TaxiJaune;
            if (r < 0.45f) return AIVehicleType.MinibusJaune;
            if (r < 0.70f) return AIVehicleType.MotoTaxi;
            if (r < 0.82f) return AIVehicleType.Berline;
            if (r < 0.88f) return AIVehicleType.MinibusBlanc;
            if (r < 0.94f) return AIVehicleType.BusTransco;
            return AIVehicleType.CamionRemorque;
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
                case AIVehicleType.BusTransco: return Random.Range(9f, 11f);
                case AIVehicleType.MotoTaxi: return Random.Range(11f, 14f);
                default: return Random.Range(9.5f, 13f);
            }
        }

        private void Update()
        {
            densityTimer -= Time.deltaTime;
            if (densityTimer > 0f || Paths.Count == 0) return;
            densityTimer = 1.5f;
            int normal = 0;
            AIVehicle far = null;
            foreach (AIVehicle v in Vehicles)
            {
                if (v.Special) continue;
                normal++;
                if (far == null && Player != null && (v.transform.position - Player.transform.position).sqrMagnitude > 900f * 900f) far = v;
            }
            if (far != null) { Despawn(far); return; }
            if (normal < targetCount)
            {
                TrafficPath p = Paths[Random.Range(0, Paths.Count)];
                float s = Random.Range(0f, p.Length);
                Vector3 pos = p.PositionAt(s);
                bool ok = Player == null || ((pos - Player.transform.position).sqrMagnitude > 45f * 45f && (pos - Player.transform.position).sqrMagnitude < 700f * 700f);
                foreach (AIVehicle o in Vehicles)
                    if ((o.transform.position - pos).sqrMagnitude < 18f * 18f) { ok = false; break; }
                if (ok)
                {
                    AIVehicleType type = RandomType();
                    Spawn(type, p, s, CruiseFor(type));
                }
            }
            else if (normal > targetCount + 2)
            {
                foreach (AIVehicle v in Vehicles)
                {
                    if (v.Special) continue;
                    if (Player == null || (v.transform.position - Player.transform.position).sqrMagnitude > 120f * 120f) { Despawn(v); break; }
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
                float target = v.Cruise;
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
                        AmbientAudio.HonkAt(v.transform.position, v.Type == AIVehicleType.BusTransco || v.Type == AIVehicleType.CamionRemorque);
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
