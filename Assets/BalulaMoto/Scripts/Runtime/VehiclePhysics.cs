// BALULA MOTO — physique de conduite : roues à rayons (suspension, adhérence, freinage), moto et tricycle.
using System.Collections.Generic;
using BalulaMoto.Core;
using UnityEngine;

namespace BalulaMoto.Runtime
{
    public struct DriveInput
    {
        public float Throttle;
        public float Steer;
        public float FrontBrake;
        public float RearBrake;
    }

    /// <summary>Références visuelles d'un véhicule (remplies par VehicleBuilder ou par un modèle Blender).</summary>
    public class VehicleRig : MonoBehaviour
    {
        public Transform Body;
        public Transform SteerPivot;
        public List<Transform> Wheels = new List<Transform>();
        public List<Transform> Seats = new List<Transform>();
        public Transform CargoAnchor;
        public Transform CameraFirstPerson;
        public Transform CameraDashboard;
        public List<Renderer> HeadLights = new List<Renderer>();
        public List<Renderer> TailLights = new List<Renderer>();
        public List<Renderer> IndicatorsLeft = new List<Renderer>();
        public List<Renderer> IndicatorsRight = new List<Renderer>();
        public Light HeadLamp;
        public GameObject Driver;
    }

    public class WheelState
    {
        public Vector3 Anchor;
        public float Radius;
        public float Travel;
        public bool Steers;
        public bool Drives;
        public bool Front;
        public bool Left;
        public bool Right;
        public bool Grounded;
        public float Compression;
        public float CompressionVelocity;
        public float Load;
        public float Spin;
        public float HitDistance;
        public SurfaceKind Surface;
        public float JoltCooldown;
        public Transform Visual;
        public Vector3 VisualBase;
    }

    [RequireComponent(typeof(Rigidbody))]
    public class RaycastVehicle : MonoBehaviour
    {
        public const int PlayerLayer = 2; // "Ignore Raycast" : les rayons des roues ne touchent pas le véhicule
        private static readonly int RayMask = ~(1 << PlayerLayer);

        public VehicleKind Kind;
        public VehicleRig Rig;
        public Rigidbody Body;
        public DriveInput Controls;
        public bool EngineCut;
        public bool PushMode;
        public bool ReverseGear;
        public bool HeadlightsOn;
        public int Indicator; // -1 gauche, 1 droite, 2 détresse, 0 aucun
        public float Wetness;

        // Télémétrie
        public float ForwardSpeed { get; private set; }
        public float SpeedKmh { get { return Mathf.Abs(ForwardSpeed) * 3.6f; } }
        public int Gear { get; private set; }
        public float Rpm01 { get; private set; }
        public float LeanAngle { get; private set; }
        public float LongitudinalAccel { get; private set; }
        public float LateralAccel { get; private set; }
        public SurfaceKind CurrentSurface { get; private set; }
        public bool Grounded { get; private set; }
        public bool Crashed { get; private set; }
        public float ThrottleSmoothed { get; private set; }
        public float SteerAngle { get; private set; }

        public event System.Action<float, Collider> Impact;
        public event System.Action Crash;
        public event System.Action<float> HardBrake;
        public event System.Action<float> Jolt;

        private readonly List<WheelState> wheels = new List<WheelState>();
        private readonly Dictionary<int, SurfaceKind> surfaceCache = new Dictionary<int, SurfaceKind>();
        private VehicleStats stats;
        private float baseMass;
        private float extraMass;
        private float springK, damperC;
        private float cda = 0.7f;
        private float steerSmoothed;
        private float prevForwardSpeed;
        private Vector3 prevVelocity;
        private float distanceKm, throttleAccum, timeAccum, idleAccum, brakeLossAccum;
        private float crashTimer;
        private float upsideDownTimer;
        private float hardBrakeCooldown;
        private float indicatorClock;
        private Vector3 lastPos;

        // ------------------------------------------------------------------
        // Configuration
        // ------------------------------------------------------------------

        public void Setup(VehicleDef def, VehicleStats s, VehicleRig rig)
        {
            Kind = def.Kind;
            Rig = rig;
            Body = GetComponent<Rigidbody>();
            gameObject.layer = PlayerLayer;
            baseMass = def.masseKg + 75f;
            stats = s;
            Body.mass = baseMass;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.Continuous;
            Body.maxAngularVelocity = 12f;
            Compat.SetDamping(Body, 0.02f, Kind == VehicleKind.Moto ? 1.5f : 0.6f);

            float wb = Mathf.Max(0.8f, def.empattementM);
            float r = Mathf.Max(0.15f, def.rayonRoueM);
            float travel = Kind == VehicleKind.Moto ? 0.22f : 0.18f;
            float anchorY = travel * 0.6f + r;
            wheels.Clear();
            if (Kind == VehicleKind.Moto)
            {
                wheels.Add(new WheelState { Anchor = new Vector3(0f, anchorY, wb * 0.5f), Radius = r, Travel = travel, Steers = true, Front = true });
                wheels.Add(new WheelState { Anchor = new Vector3(0f, anchorY, -wb * 0.5f), Radius = r, Travel = travel, Drives = true });
                Body.centerOfMass = new Vector3(0f, 0.55f, -0.05f);
            }
            else
            {
                float track = Mathf.Max(0.9f, def.voieArriereM) * 0.5f;
                wheels.Add(new WheelState { Anchor = new Vector3(0f, anchorY, wb * 0.55f), Radius = r, Travel = travel, Steers = true, Front = true });
                wheels.Add(new WheelState { Anchor = new Vector3(-track, anchorY, -wb * 0.45f), Radius = r, Travel = travel, Drives = true, Left = true });
                wheels.Add(new WheelState { Anchor = new Vector3(track, anchorY, -wb * 0.45f), Radius = r, Travel = travel, Drives = true, Right = true });
                Body.centerOfMass = new Vector3(0f, 0.62f, -wb * 0.12f);
            }
            for (int i = 0; i < wheels.Count && rig != null && i < rig.Wheels.Count; i++)
            {
                wheels[i].Visual = rig.Wheels[i];
                if (rig.Wheels[i] != null) wheels[i].VisualBase = rig.Wheels[i].localPosition;
            }

            float L = Mathf.Max(1.5f, def.longueurM), W = Mathf.Max(0.5f, def.largeurM), H = Mathf.Max(1f, def.hauteurM);
            float m = baseMass;
            Body.inertiaTensor = new Vector3(m * (H * H + L * L) / 12f, m * (W * W + L * L) / 12f, m * (W * W + H * H) / 12f);
            Body.inertiaTensorRotation = Quaternion.identity;

            float perWheel = (baseMass + 70f) / wheels.Count;
            springK = perWheel * 9.81f / (0.4f * travel);
            damperC = 2f * 0.5f * Mathf.Sqrt(springK * perWheel);
            ApplyStats(s);
            lastPos = transform.position;
        }

        public void ApplyStats(VehicleStats s)
        {
            if (s == null) return;
            stats = s;
            float vmax = Mathf.Max(5f, s.MaxSpeedKmh / 3.6f);
            cda = Mathf.Clamp(2f * s.PowerKw * 1000f / (1.2f * vmax * vmax * vmax), 0.3f, 6f);
        }

        /// <summary>Passagers et marchandises alourdissent le véhicule.</summary>
        public void SetExtraMass(float kg)
        {
            extraMass = Mathf.Max(0f, kg);
            if (Body != null) Body.mass = baseMass + extraMass;
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            Body.isKinematic = true;
            transform.SetPositionAndRotation(position + Vector3.up * 0.15f, rotation);
            Body.isKinematic = false;
            Compat.SetVelocity(Body, Vector3.zero);
            Body.angularVelocity = Vector3.zero;
            foreach (WheelState w in wheels) { w.Compression = 0f; w.CompressionVelocity = 0f; }
            lastPos = transform.position;
            Crashed = false;
            crashTimer = 0f;
            upsideDownTimer = 0f;
        }

        /// <summary>Relève le véhicule après une chute ou un renversement.</summary>
        public void Recover()
        {
            Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            Teleport(transform.position + Vector3.up * 0.4f, Quaternion.LookRotation(fwd.normalized, Vector3.up));
        }

        // ------------------------------------------------------------------
        // Lecture de la télémétrie par le jeu
        // ------------------------------------------------------------------

        public float TakeDistanceKm(out float throttleAverage, out float idleSeconds, out float brakeSpeedLoss)
        {
            float d = distanceKm;
            throttleAverage = timeAccum > 0f ? throttleAccum / timeAccum : 0f;
            idleSeconds = idleAccum;
            brakeSpeedLoss = brakeLossAccum;
            distanceKm = 0f; throttleAccum = 0f; timeAccum = 0f; idleAccum = 0f; brakeLossAccum = 0f;
            return d;
        }

        public bool IsUpsideDown { get { return Vector3.Dot(transform.up, Vector3.up) < 0.35f; } }

        // ------------------------------------------------------------------
        // Simulation
        // ------------------------------------------------------------------

        private void FixedUpdate()
        {
            if (Body == null || Body.isKinematic || stats == null) return;
            float dt = Time.fixedDeltaTime;
            Vector3 vel = Compat.GetVelocity(Body);
            Vector3 fwd = transform.forward;
            Vector3 up = transform.up;
            ForwardSpeed = Vector3.Dot(vel, fwd);
            float speedAbs = Mathf.Abs(ForwardSpeed);
            bool moto = Kind == VehicleKind.Moto;
            float totalMass = Body.mass;

            // --- Entrées lissées
            float throttle = Mathf.Clamp01(Controls.Throttle);
            if (Crashed) throttle = 0f;
            ThrottleSmoothed = Mathf.MoveTowards(ThrottleSmoothed, throttle, dt * 4f);
            steerSmoothed = Mathf.MoveTowards(steerSmoothed, Mathf.Clamp(Controls.Steer, -1f, 1f), dt * 3.5f);
            float maxSteer = moto ? Mathf.Lerp(38f, 5f, speedAbs / 25f) : Mathf.Lerp(40f, 14f, speedAbs / 18f);
            SteerAngle = steerSmoothed * maxSteer;

            // --- Moteur
            float vmax = stats.MaxSpeedKmh / 3.6f;
            float power = stats.PowerKw * 1000f;
            float tractionLimit = totalMass * 9.81f * (moto ? 0.5f : 0.45f) * stats.LowSpeedTraction;
            float drive = 0f;
            if (EngineCut || PushMode)
            {
                if (PushMode && speedAbs < 1.4f) drive = Controls.Throttle * totalMass * 0.6f * (ReverseGear ? -1f : 1f);
            }
            else if (ReverseGear)
            {
                // Tricycle : vraie marche arrière ; moto : le pilote recule en poussant avec les pieds (au pas)
                float reverseMax = moto ? 1.6f : 3.5f;
                float reverseForce = moto ? totalMass * 1.1f : Mathf.Min(tractionLimit * 0.5f, power / Mathf.Max(speedAbs, 2f));
                if (ForwardSpeed > -reverseMax) drive = -ThrottleSmoothed * reverseForce;
            }
            else
            {
                drive = ThrottleSmoothed * Mathf.Min(tractionLimit, power / Mathf.Max(speedAbs, 2f));
                if (ForwardSpeed > vmax) drive = 0f;
                else if (ForwardSpeed > vmax * 0.97f) drive *= (vmax - ForwardSpeed) / (vmax * 0.03f);
            }
            if (!EngineCut && ThrottleSmoothed < 0.05f && speedAbs > 1f) drive -= Mathf.Sign(ForwardSpeed) * totalMass * 0.35f; // frein moteur

            int driveWheels = 0;
            foreach (WheelState w in wheels) if (w.Drives) driveWheels++;
            float totalBrake = stats.BrakeDecel * totalMass;

            // --- Roues
            int groundedCount = 0;
            for (int i = 0; i < wheels.Count; i++)
            {
                WheelState w = wheels[i];
                Vector3 anchorW = transform.TransformPoint(w.Anchor);
                float rayLen = w.Travel + w.Radius;
                RaycastHit hit;
                w.JoltCooldown -= dt;
                if (Physics.Raycast(anchorW, -up, out hit, rayLen, RayMask, QueryTriggerInteraction.Ignore))
                {
                    groundedCount++;
                    w.Grounded = true;
                    w.HitDistance = hit.distance;
                    float compression = Mathf.Clamp(rayLen - hit.distance, 0f, w.Travel);
                    float compVel = (compression - w.Compression) / dt;
                    w.CompressionVelocity = compVel;
                    w.Compression = compression;
                    float spring = compression * springK + compVel * damperC;
                    if (rayLen - hit.distance > w.Travel) spring += (rayLen - hit.distance - w.Travel) * springK * 6f; // butée
                    spring = Mathf.Max(0f, spring);
                    w.Load = spring;
                    Body.AddForceAtPosition(up * spring, anchorW);

                    w.Surface = SurfaceOf(hit.collider);
                    bool wet = Wetness > 0.3f;
                    float mu = SurfaceInfo.Grip(w.Surface, wet) * stats.Grip * (moto ? 1.0f : 0.92f);
                    float maxFriction = mu * spring;

                    Quaternion steerRot = Quaternion.AngleAxis(w.Steers ? SteerAngle : 0f, up);
                    Vector3 wFwd = Vector3.ProjectOnPlane(steerRot * fwd, hit.normal).normalized;
                    Vector3 wSide = Vector3.Cross(hit.normal, wFwd);
                    Vector3 pv = Body.GetPointVelocity(hit.point);
                    float vLong = Vector3.Dot(pv, wFwd);
                    float vLat = Vector3.Dot(pv, wSide);
                    float massShare = totalMass / wheels.Count;

                    float latF = -vLat * massShare / dt * 0.55f;
                    float longF = 0f;
                    if (w.Drives && driveWheels > 0) longF += drive / driveWheels;

                    float brakeInput;
                    if (moto) brakeInput = w.Front ? Controls.FrontBrake * 0.65f : Controls.RearBrake * 0.42f;
                    else brakeInput = Controls.FrontBrake / wheels.Count * 1.05f + (w.Front ? 0f : Controls.RearBrake * 0.35f);
                    float brakeF = brakeInput * totalBrake;
                    if (brakeF > 0f)
                    {
                        if (Mathf.Abs(vLong) > 0.4f) longF -= Mathf.Sign(vLong) * brakeF;
                        else longF -= vLong * massShare / dt * 0.6f;
                    }
                    float rolling = SurfaceInfo.Rolling(w.Surface) * spring;
                    if (Mathf.Abs(vLong) > 0.05f) longF -= Mathf.Sign(vLong) * Mathf.Min(rolling, Mathf.Abs(vLong) * massShare / dt);

                    Vector3 tire = wFwd * longF + wSide * latF;
                    float mag = tire.magnitude;
                    if (mag > maxFriction && mag > 0.001f) tire *= maxFriction / mag;
                    Body.AddForceAtPosition(tire, hit.point + up * 0.1f);

                    // Secousses : nids-de-poule, caniveaux, chocs de suspension
                    float bump = SurfaceInfo.Bumpiness(w.Surface);
                    if (bump > 0.3f && speedAbs > 3f)
                    {
                        Vector3 p = hit.point;
                        float noise = Mathf.PerlinNoise(p.x * 0.55f, p.z * 0.55f);
                        if (noise > 0.70f && w.JoltCooldown <= 0f)
                        {
                            Body.AddForceAtPosition(-up * bump * totalMass * 0.9f * Mathf.Min(speedAbs, 12f) / 6f, anchorW, ForceMode.Impulse);
                            w.JoltCooldown = 0.45f;
                            if (Jolt != null) Jolt(speedAbs * 3.6f);
                        }
                    }
                    if (compVel > 1.6f && speedAbs > 4f && w.JoltCooldown <= 0f)
                    {
                        w.JoltCooldown = 0.45f;
                        if (Jolt != null) Jolt(speedAbs * 3.6f);
                    }
                    w.Spin += vLong / w.Radius * dt * Mathf.Rad2Deg;
                }
                else
                {
                    w.Grounded = false;
                    w.Compression = Mathf.MoveTowards(w.Compression, 0f, dt * 2f);
                    w.HitDistance = rayLen;
                    w.Spin += ForwardSpeed / w.Radius * dt * Mathf.Rad2Deg * 0.5f;
                }
            }
            Grounded = groundedCount > 0;
            if (wheels.Count > 0) CurrentSurface = wheels[wheels.Count - 1].Grounded ? wheels[wheels.Count - 1].Surface : wheels[0].Surface;

            // --- Barre anti-roulis arrière (tricycle)
            if (!moto && wheels.Count == 3)
            {
                WheelState l = wheels[1], rr = wheels[2];
                float anti = (l.Compression - rr.Compression) * springK * 0.8f;
                if (l.Grounded) Body.AddForceAtPosition(-up * anti, transform.TransformPoint(l.Anchor));
                if (rr.Grounded) Body.AddForceAtPosition(up * anti, transform.TransformPoint(rr.Anchor));
            }

            // --- Traînée aérodynamique
            Body.AddForce(-vel * vel.magnitude * 0.5f * 1.2f * cda);

            // --- Équilibre : la moto est maintenue debout (l'inclinaison est visuelle)
            Vector3 axis = Vector3.Cross(up, Vector3.up);
            if (moto && !Crashed)
            {
                float roll = Vector3.Dot(axis, fwd);
                float rollRate = Vector3.Dot(Body.angularVelocity, fwd);
                Body.AddTorque(fwd * (roll * 90f - rollRate * 16f), ForceMode.Acceleration);
                if (!Grounded || Mathf.Abs(Vector3.Dot(axis, transform.right)) > 0.25f)
                {
                    float pitch = Vector3.Dot(axis, transform.right);
                    float pitchRate = Vector3.Dot(Body.angularVelocity, transform.right);
                    Body.AddTorque(transform.right * (pitch * 25f - pitchRate * 5f), ForceMode.Acceleration);
                }
            }
            else if (!moto && Vector3.Dot(up, Vector3.up) > 0.9f)
            {
                float roll = Vector3.Dot(axis, fwd);
                float rollRate = Vector3.Dot(Body.angularVelocity, fwd);
                Body.AddTorque(fwd * (roll * 6f - rollRate * 2.5f), ForceMode.Acceleration);
            }

            // --- Télémétrie
            Vector3 accel = (vel - prevVelocity) / dt;
            prevVelocity = vel;
            LongitudinalAccel = Mathf.Lerp(LongitudinalAccel, (ForwardSpeed - prevForwardSpeed) / dt, 0.25f);
            LateralAccel = Mathf.Lerp(LateralAccel, Vector3.Dot(accel, transform.right), 0.25f);
            prevForwardSpeed = ForwardSpeed;
            hardBrakeCooldown -= dt;
            float braking = Mathf.Max(Controls.FrontBrake, Controls.RearBrake);
            if (braking > 0.1f && LongitudinalAccel < 0f) brakeLossAccum += -LongitudinalAccel * dt * braking;
            if (LongitudinalAccel < -(moto ? 6.2f : 5.2f) && speedAbs > 3f && hardBrakeCooldown <= 0f)
            {
                hardBrakeCooldown = 1.5f;
                if (HardBrake != null) HardBrake(-LongitudinalAccel);
            }
            Vector3 flatMove = transform.position - lastPos;
            flatMove.y = 0f;
            distanceKm += flatMove.magnitude / 1000f;
            lastPos = transform.position;
            throttleAccum += ThrottleSmoothed * dt;
            timeAccum += dt;
            if (speedAbs < 0.5f && !EngineCut) idleAccum += dt;
            UpdateGearbox(speedAbs, vmax);

            // --- Chute / renversement
            if (Crashed)
            {
                crashTimer += dt;
            }
            else if (!moto)
            {
                upsideDownTimer = IsUpsideDown ? upsideDownTimer + dt : 0f;
                if (upsideDownTimer > 1.5f) TriggerCrash();
            }
        }

        private void UpdateGearbox(float speedAbs, float vmax)
        {
            int gears = Mathf.Max(1, stats.Gears);
            if (ReverseGear) { Gear = -1; Rpm01 = Mathf.Clamp01(0.2f + speedAbs / 4f); return; }
            if (Gear < 1) Gear = 1;
            float gearTop = vmax * Gear / gears;
            float rpm = speedAbs / Mathf.Max(0.5f, gearTop);
            if (rpm > 0.95f && Gear < gears) Gear++;
            else if (Gear > 1 && speedAbs / Mathf.Max(0.5f, vmax * (Gear - 1) / gears) < 0.55f) Gear--;
            gearTop = vmax * Gear / gears;
            float target = Mathf.Clamp01(speedAbs / Mathf.Max(0.5f, gearTop));
            if (speedAbs < 1f) target = 0.12f + ThrottleSmoothed * 0.35f;
            Rpm01 = Mathf.Lerp(Rpm01, EngineCut ? 0f : target, 0.2f);
        }

        private SurfaceKind SurfaceOf(Collider c)
        {
            int id = c.GetInstanceID();
            SurfaceKind kind;
            if (surfaceCache.TryGetValue(id, out kind)) return kind;
            SurfaceZone z = c.GetComponent<SurfaceZone>();
            if (z == null) z = c.GetComponentInParent<SurfaceZone>();
            kind = z != null ? z.Kind : SurfaceKind.Goudron;
            surfaceCache[id] = kind;
            return kind;
        }

        private void TriggerCrash()
        {
            if (Crashed) return;
            Crashed = true;
            crashTimer = 0f;
            if (Crash != null) Crash();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (Body == null) return;
            float dv = collision.impulse.magnitude / Mathf.Max(1f, Body.mass);
            if (dv < 1.5f) return;
            if (Impact != null) Impact(dv, collision.collider);
            if (Kind == VehicleKind.Moto && dv > 4.5f) TriggerCrash();
        }

        // ------------------------------------------------------------------
        // Visuel : roues, inclinaison, guidon, feux
        // ------------------------------------------------------------------

        private void LateUpdate()
        {
            if (Rig == null) return;
            bool moto = Kind == VehicleKind.Moto;
            float yawRate = Body != null ? Vector3.Dot(Body.angularVelocity, transform.up) : 0f;
            float targetLean = 0f;
            if (moto)
            {
                targetLean = -Mathf.Atan2(ForwardSpeed * yawRate, 9.81f) * Mathf.Rad2Deg;
                targetLean = Mathf.Clamp(targetLean, -42f, 42f);
                if (Crashed) targetLean = Mathf.MoveTowards(LeanAngle, 80f, Time.deltaTime * 160f);
            }
            LeanAngle = Mathf.Lerp(LeanAngle, targetLean, Time.deltaTime * 6f);
            if (Rig.Body != null) Rig.Body.localRotation = Quaternion.AngleAxis(LeanAngle, Vector3.forward);
            if (Rig.SteerPivot != null) Rig.SteerPivot.localRotation = Quaternion.AngleAxis(SteerAngle, Vector3.up);

            for (int i = 0; i < wheels.Count; i++)
            {
                WheelState w = wheels[i];
                if (w.Visual == null) continue;
                float drop = w.Grounded ? w.HitDistance - w.Radius : w.Travel;
                Vector3 lp = w.VisualBase;
                lp.y = w.Anchor.y - drop;
                w.Visual.localPosition = lp;
                Quaternion steer = w.Steers ? Quaternion.AngleAxis(SteerAngle, Vector3.up) : Quaternion.identity;
                w.Visual.localRotation = steer * Quaternion.AngleAxis(w.Spin, Vector3.right);
            }

            // Feux
            bool braking = Controls.FrontBrake > 0.1f || Controls.RearBrake > 0.1f;
            foreach (Renderer r in Rig.HeadLights) Mats.SetLamp(r, new Color(1f, 0.97f, 0.85f), HeadlightsOn && !EngineCut, 3f);
            foreach (Renderer r in Rig.TailLights) Mats.SetLamp(r, new Color(1f, 0.08f, 0.05f), braking || HeadlightsOn, braking ? 3f : 1.2f);
            if (Rig.HeadLamp != null) Rig.HeadLamp.enabled = HeadlightsOn && !EngineCut;
            indicatorClock += Time.deltaTime;
            bool blink = Mathf.Repeat(indicatorClock, 0.8f) < 0.4f;
            bool left = blink && (Indicator == -1 || Indicator == 2);
            bool right = blink && (Indicator == 1 || Indicator == 2);
            Color amber = new Color(1f, 0.55f, 0.05f);
            foreach (Renderer r in Rig.IndicatorsLeft) Mats.SetLamp(r, amber, left, 3f);
            foreach (Renderer r in Rig.IndicatorsRight) Mats.SetLamp(r, amber, right, 3f);
        }
    }
}
