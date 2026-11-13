// BALULA MOTO — cycle jour/nuit, météo (soleil, nuages, pluie, orage, brouillard léger, poussière), éclairage public.
using System.Collections.Generic;
using BalulaMoto.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace BalulaMoto.Runtime
{
    public class WorldEnvironment : MonoBehaviour
    {
        public static readonly List<Renderer> StreetLamps = new List<Renderer>();
        public static readonly List<Material> WetMaterials = new List<Material>();

        public Light Sun;
        public WeatherKind Weather = WeatherKind.Soleil;
        public float RainIntensity;
        public float Wetness;
        public float Overcast;
        public bool LampsOn { get; private set; }

        private WeatherKind target = WeatherKind.Soleil;
        private float nextWeatherChange = 90f;
        private float lightningTimer;
        private float flash;
        private ParticleSystem rain;
        private Transform follow;

        public static string WeatherLabel(WeatherKind w)
        {
            switch (w)
            {
                case WeatherKind.Soleil: return "Soleil";
                case WeatherKind.Nuageux: return "Nuageux";
                case WeatherKind.Pluie: return "Pluie";
                case WeatherKind.Orage: return "Orage";
                case WeatherKind.Brouillard: return "Brouillard léger";
                case WeatherKind.Poussiere: return "Poussière";
            }
            return "";
        }

        public static WorldEnvironment Create(Transform cameraTransform)
        {
            GameObject go = new GameObject("Environnement");
            WorldEnvironment env = go.AddComponent<WorldEnvironment>();
            Light sun = RenderSettings.sun;
            if (sun == null)
            {
                foreach (Light l in Compat.FindAll<Light>())
                    if (l.type == LightType.Directional) { sun = l; break; }
            }
            if (sun == null)
            {
                GameObject s = new GameObject("Soleil");
                sun = s.AddComponent<Light>();
                sun.type = LightType.Directional;
            }
            sun.shadows = LightShadows.Soft;
            RenderSettings.sun = sun;
            env.Sun = sun;
            env.follow = cameraTransform;
            env.BuildRain();
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.ambientMode = AmbientMode.Flat;
            return env;
        }

        private void BuildRain()
        {
            GameObject g = new GameObject("Pluie");
            g.transform.SetParent(transform, false);
            rain = g.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = rain.main;
            main.loop = true;
            main.startLifetime = 1.1f;
            main.startSpeed = 22f;
            main.startSize = 0.03f;
            main.maxParticles = 2500;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new Color(0.75f, 0.8f, 0.9f, 0.5f);
            ParticleSystem.EmissionModule em = rain.emission;
            em.rateOverTime = 0f;
            ParticleSystem.ShapeModule sh = rain.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(40f, 40f, 1f); // après la rotation de 90°, X/Y local = plan horizontal, Z local = vers le bas
            g.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            ParticleSystemRenderer pr = g.GetComponent<ParticleSystemRenderer>();
            pr.renderMode = ParticleSystemRenderMode.Stretch;
            pr.lengthScale = 2f;
            pr.velocityScale = 0.04f;
            pr.sharedMaterial = Mats.Unlit(new Color(0.75f, 0.82f, 0.95f, 1f));
            rain.Play();
        }

        public void SetWeather(WeatherKind w, bool immediate)
        {
            target = w;
            Weather = w;
            if (immediate)
            {
                RainIntensity = w == WeatherKind.Pluie ? 0.7f : (w == WeatherKind.Orage ? 1f : 0f);
                Wetness = RainIntensity > 0f ? 1f : 0f;
            }
        }

        /// <summary>Saison des pluies à Kinshasa : environ octobre à mai ; saison sèche : juin à septembre.</summary>
        private WeatherKind RandomWeather(int day)
        {
            int month = ((day - 1) / 30 + 10) % 12; // la partie commence en novembre
            bool rainySeason = month <= 4 || month >= 9;
            float r = Random.value;
            if (rainySeason)
            {
                if (r < 0.35f) return WeatherKind.Soleil;
                if (r < 0.6f) return WeatherKind.Nuageux;
                if (r < 0.82f) return WeatherKind.Pluie;
                if (r < 0.92f) return WeatherKind.Orage;
                return WeatherKind.Brouillard;
            }
            if (r < 0.5f) return WeatherKind.Soleil;
            if (r < 0.8f) return WeatherKind.Nuageux;
            if (r < 0.95f) return WeatherKind.Poussiere;
            return WeatherKind.Brouillard;
        }

        public void Tick(GameSession s, float dt)
        {
            if (s == null) return;
            float hour = s.Hour;
            nextWeatherChange -= dt;
            if (nextWeatherChange <= 0f)
            {
                target = RandomWeather(s.Profile.jour);
                nextWeatherChange = Random.Range(120f, 300f);
            }
            if (Weather != target)
            {
                Weather = target;
                s.Profile.meteo = Weather.ToString();
                s.Notify("Météo : " + WeatherLabel(Weather));
            }
            float rainTarget = Weather == WeatherKind.Pluie ? 0.7f : (Weather == WeatherKind.Orage ? 1f : 0f);
            RainIntensity = Mathf.MoveTowards(RainIntensity, rainTarget, dt * 0.05f);
            Wetness = RainIntensity > 0.05f ? Mathf.MoveTowards(Wetness, 1f, dt * 0.08f) : Mathf.MoveTowards(Wetness, 0f, dt * 0.008f);
            float overcastTarget = Weather == WeatherKind.Soleil ? 0f : (Weather == WeatherKind.Nuageux ? 0.5f : (Weather == WeatherKind.Poussiere ? 0.35f : 0.85f));
            Overcast = Mathf.MoveTowards(Overcast, overcastTarget, dt * 0.05f);

            // Soleil : lever vers 6 h, coucher vers 18 h 15 (proche de l'équateur)
            float dayAngle = (hour - 6f) / 12.25f * 180f;
            Sun.transform.rotation = Quaternion.Euler(dayAngle, 35f, 0f);
            float daylight = Mathf.Clamp01(Mathf.Sin(Mathf.Clamp(dayAngle, 0f, 180f) * Mathf.Deg2Rad) * 1.6f);
            float dusk = Mathf.Clamp01(1f - Mathf.Abs(dayAngle - 90f) / 90f);
            Color sunColor = Color.Lerp(new Color(1f, 0.55f, 0.3f), new Color(1f, 0.96f, 0.88f), dusk);
            Sun.color = sunColor;
            Sun.intensity = daylight * Mathf.Lerp(1.25f, 0.35f, Overcast) + flash;
            Sun.shadowStrength = Mathf.Lerp(0.85f, 0.25f, Overcast);
            Color dayAmbient = Color.Lerp(new Color(0.62f, 0.66f, 0.72f), new Color(0.45f, 0.47f, 0.5f), Overcast);
            Color nightAmbient = new Color(0.07f, 0.08f, 0.13f);
            RenderSettings.ambientLight = Color.Lerp(nightAmbient, dayAmbient, daylight) + new Color(flash, flash, flash) * 0.3f;
            Color fogColor = Weather == WeatherKind.Poussiere ? new Color(0.72f, 0.6f, 0.45f) : Color.Lerp(new Color(0.75f, 0.82f, 0.9f), new Color(0.55f, 0.57f, 0.6f), Overcast);
            RenderSettings.fogColor = Color.Lerp(new Color(0.03f, 0.04f, 0.07f), fogColor, daylight);
            float fog = Weather == WeatherKind.Brouillard ? 0.012f : (Weather == WeatherKind.Poussiere ? 0.009f : 0.0026f + RainIntensity * 0.006f);
            RenderSettings.fogDensity = Mathf.Lerp(RenderSettings.fogDensity, fog, dt * 0.2f);
            if (Camera.main != null)
            {
                Camera.main.clearFlags = CameraClearFlags.SolidColor;
                Camera.main.backgroundColor = Color.Lerp(new Color(0.02f, 0.03f, 0.07f), Color.Lerp(new Color(0.52f, 0.72f, 0.95f), new Color(0.6f, 0.63f, 0.67f), Overcast), daylight);
            }

            // Éclairage public
            bool lamps = hour >= 18.3f || hour < 6.1f || (Overcast > 0.8f && daylight < 0.5f);
            if (lamps != LampsOn)
            {
                LampsOn = lamps;
                foreach (Renderer r in StreetLamps) Mats.SetLamp(r, new Color(1f, 0.82f, 0.5f), lamps, 2.2f);
            }
            // Lumières de la ville et phares (shaders BalulaMoto/Lumiere et Halo) : fondu d'environ 6 secondes
            Night.Set(Mathf.MoveTowards(Night.Level, lamps ? 1f : 0f, Mathf.Max(dt, 0.001f) * 0.16f));
            foreach (Material m in WetMaterials) Mats.SetWetness(m, Wetness);

            // Pluie et orage
            if (follow != null) rain.transform.position = follow.position + Vector3.up * 14f + follow.forward * 6f;
            ParticleSystem.EmissionModule em = rain.emission;
            em.rateOverTime = RainIntensity * 1800f;
            if (AmbientAudio.Instance != null) AmbientAudio.Instance.SetRain(RainIntensity);
            if (Weather == WeatherKind.Orage)
            {
                lightningTimer -= dt;
                if (lightningTimer <= 0f)
                {
                    lightningTimer = Random.Range(8f, 22f);
                    flash = 1.6f;
                    if (AmbientAudio.Instance != null) AmbientAudio.Instance.Thunder();
                }
            }
            flash = Mathf.MoveTowards(flash, 0f, dt * 6f);
        }

        public bool IsRaining { get { return RainIntensity > 0.2f; } }
    }
}
