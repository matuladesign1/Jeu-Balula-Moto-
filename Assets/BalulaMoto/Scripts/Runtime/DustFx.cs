// BALULA MOTO — poussière soulevée par les véhicules sur les avenues de terre et les trottoirs, par temps sec :
// derrière la moto ou le tricycle du joueur, et derrière les véhicules de la circulation dans les rues non goudronnées.
// Un seul système de particules pour toute la ville (émission à la demande) : léger pour les téléphones.
using BalulaMoto.Core;
using UnityEngine;

namespace BalulaMoto.Runtime
{
    public class DustFx : MonoBehaviour
    {
        public static DustFx Instance;
        private ParticleSystem ps;
        private float timer;
        private int cursor;
        public int Emitted { get; private set; }

        public static DustFx Create(Transform parent)
        {
            GameObject g = new GameObject("Poussiere");
            g.transform.SetParent(parent, false);
            Instance = g.AddComponent<DustFx>();
            Instance.Build();
            return Instance;
        }

        private void Build()
        {
            ps = gameObject.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.0f, 2.1f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = -0.015f;
            main.maxParticles = GameSettings.Quality == 0 ? 260 : 640;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new Color(0.87f, 0.79f, 0.66f, 0.62f);
            ParticleSystem.EmissionModule em = ps.emission;
            em.rateOverTime = 0f;
            ParticleSystem.ShapeModule sh = ps.shape;
            sh.enabled = false;
            ParticleSystem.SizeOverLifetimeModule sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.45f), new Keyframe(1f, 2.4f)));
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient gr = new Gradient();
            gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                       new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0.55f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = gr;
            ParticleSystem.VelocityOverLifetimeModule vel = ps.velocityOverLifetime;
            vel.enabled = false;
            ParticleSystemRenderer pr = GetComponent<ParticleSystemRenderer>();
            pr.renderMode = ParticleSystemRenderMode.Billboard;
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pr.receiveShadows = false;
            Shader sd = Resources.Load<Shader>("BalulaMoto/Shaders/Poussiere");
            if (sd == null) sd = Shader.Find("BalulaMoto/Poussiere");
            if (sd != null && sd.isSupported)
            {
                Material m = new Material(sd) { name = "Poussiere" };
                m.enableInstancing = false;
                pr.sharedMaterial = m;
            }
            else
            {
                pr.sharedMaterial = Mats.Unlit(new Color(0.72f, 0.62f, 0.5f));
                main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
            }
            ps.Play();
        }

        /// <summary>Surfaces qui font de la poussière : terre, poussière, nids-de-poule, chantiers (pas la boue ni le goudron).</summary>
        public static bool Dusty(SurfaceKind s)
        {
            return s == SurfaceKind.Terre || s == SurfaceKind.Poussiere || s == SurfaceKind.NidsDePoule || s == SurfaceKind.Travaux;
        }

        private void Puff(Vector3 pos, Vector3 back, float speed, int count)
        {
            ParticleSystem.EmitParams ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                ep.position = pos + new Vector3(Random.Range(-0.35f, 0.35f), Random.Range(0.05f, 0.25f), Random.Range(-0.35f, 0.35f));
                ep.velocity = back * Mathf.Clamp(speed * 0.12f, 0.3f, 1.6f) + new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(0.25f, 0.6f), Random.Range(-0.3f, 0.3f));
                ep.applyShapeToPosition = false;
                ps.Emit(ep, 1);
                Emitted++;
            }
        }

        private void Update()
        {
            if (ps == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            // Sol mouillé : pas de poussière
            WorldEnvironment env = BalulaGame.Instance != null ? BalulaGame.Instance.Env : null;
            if (env != null && (env.Wetness > 0.25f || env.RainIntensity > 0.05f)) return;
            timer += dt;
            if (timer < 0.06f) return;
            timer = 0f;
            Camera cam = Camera.main;
            Vector3 cp = cam != null ? cam.transform.position : Vector3.zero;
            TrafficManager tm = TrafficManager.Instance;
            // Joueur : derrière la roue arrière, sur la terre et les trottoirs
            if (tm != null && tm.Player != null)
            {
                RaycastVehicle v = tm.Player;
                float sp = Mathf.Abs(v.ForwardSpeed);
                Transform t = v.transform;
                bool dusty = sp > 3.5f && Dusty(v.CurrentSurface);
                if (!dusty && sp > 3.5f)
                {
                    // Sur le trottoir (bande au bord de la chaussée, goudronnée ou non) : poussière aussi
                    float edge;
                    Vector3 away;
                    bool paved;
                    dusty = CityFill.NearestCarriageway(t.position, out edge, out away, out paved) && edge > 0.3f && edge < 4.5f;
                }
                if (dusty)
                {
                    Vector3 back = -t.forward;
                    Puff(t.position + back * (v.Kind == VehicleKind.Moto ? 0.75f : 1.1f), back, sp, sp > 11f ? 4 : 3);
                }
            }
            // Circulation : véhicules proches qui roulent dans une rue de terre (boucle non goudronnée)
            if (tm == null || tm.Vehicles.Count == 0) return;
            int budget = GameSettings.Quality == 0 ? 3 : 6;
            for (int n = 0; n < tm.Vehicles.Count && budget > 0; n++)
            {
                cursor = (cursor + 1) % tm.Vehicles.Count;
                AIVehicle a = tm.Vehicles[cursor];
                if (a == null || a.Speed < 3.5f || a.Path == null || a.Path.Major) continue;
                Vector3 p = a.transform.position;
                if ((p - cp).sqrMagnitude > 75f * 75f) continue;
                budget--;
                Vector3 back = -a.transform.forward;
                Puff(p + back * (a.Length * 0.5f), back, a.Speed, a.Length > 6f ? 3 : 2);
            }
        }
    }
}
