// BALULA MOTO — sons synthétisés (prototype libre de droits) : moteurs, klaxons, sifflet, pluie, ville.
// À remplacer par des enregistrements réels de motos et tricycles de Kinshasa (prise de son à prévoir).
using BalulaMoto.Core;
using UnityEngine;

namespace BalulaMoto.Runtime
{
    public static class AudioSynth
    {
        private const int Rate = 22050;
        private static AudioClip engineMoto, engineTricycle, horn, hornLow, whistle, rain, city, thunder, cash, click;

        private static AudioClip Make(string name, float[] data)
        {
            AudioClip c = AudioClip.Create(name, data.Length, 1, Rate, false);
            c.SetData(data, 0);
            return c;
        }

        /// <summary>Monocylindre 4 temps : une explosion amortie par période, boucle parfaite.</summary>
        private static AudioClip Engine(string name, float firing, float resonance, float roughness, int seed)
        {
            int n = Rate;
            float[] d = new float[n];
            System.Random rnd = new System.Random(seed);
            float period = 1f / firing;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float local = t % period;
                float env = Mathf.Exp(-local * 38f);
                float s = env * Mathf.Sin(2f * Mathf.PI * resonance * local) * 0.8f;
                s += env * Mathf.Sin(2f * Mathf.PI * resonance * 2.03f * local) * 0.25f;
                s += Mathf.Sin(2f * Mathf.PI * firing * t) * 0.18f;
                s += ((float)rnd.NextDouble() - 0.5f) * roughness * env;
                d[i] = Mathf.Clamp(s, -1f, 1f) * 0.7f;
            }
            return Make(name, d);
        }

        private static AudioClip Horn(string name, float f1, float f2)
        {
            int n = Rate / 2;
            float[] d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float s = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * f1 * t)) * 0.35f + Mathf.Sign(Mathf.Sin(2f * Mathf.PI * f2 * t)) * 0.3f;
                d[i] = (float)System.Math.Tanh(s * 1.5f) * 0.6f;
            }
            return Make(name, d);
        }

        private static AudioClip Noise(string name, float seconds, float smoothing, float volume, int seed, bool fadeOut)
        {
            int n = (int)(Rate * seconds);
            float[] d = new float[n];
            System.Random rnd = new System.Random(seed);
            float y = 0f;
            for (int i = 0; i < n; i++)
            {
                float x = (float)rnd.NextDouble() * 2f - 1f;
                y += (x - y) * smoothing;
                float env = fadeOut ? Mathf.Exp(-i / (float)Rate * 1.6f) * Mathf.Clamp01(i / (Rate * 0.05f)) : 1f;
                d[i] = y * volume * env;
            }
            if (!fadeOut)
            {
                int fade = Rate / 10;
                for (int i = 0; i < fade; i++)
                {
                    float k = i / (float)fade;
                    d[n - fade + i] = d[n - fade + i] * (1f - k) + d[i] * k;
                }
            }
            return Make(name, d);
        }

        private static AudioClip Tones(string name, float[] freqs, float each)
        {
            int per = (int)(Rate * each);
            float[] d = new float[per * freqs.Length];
            for (int k = 0; k < freqs.Length; k++)
                for (int i = 0; i < per; i++)
                {
                    float t = i / (float)Rate;
                    d[k * per + i] = Mathf.Sin(2f * Mathf.PI * freqs[k] * t) * Mathf.Exp(-t * 6f) * 0.5f;
                }
            return Make(name, d);
        }

        public static AudioClip EngineMoto { get { if (engineMoto == null) engineMoto = Engine("MoteurMoto", 30f, 140f, 0.35f, 3); return engineMoto; } }
        public static AudioClip EngineTricycle { get { if (engineTricycle == null) engineTricycle = Engine("MoteurTricycle", 25f, 95f, 0.55f, 5); return engineTricycle; } }
        public static AudioClip HornHigh { get { if (horn == null) horn = Horn("Klaxon", 420f, 520f); return horn; } }
        public static AudioClip HornLow { get { if (hornLow == null) hornLow = Horn("KlaxonGrave", 300f, 360f); return hornLow; } }
        public static AudioClip Rain { get { if (rain == null) rain = Noise("Pluie", 2f, 0.55f, 0.5f, 9, false); return rain; } }
        public static AudioClip City { get { if (city == null) city = Noise("Ville", 3f, 0.04f, 0.9f, 13, false); return city; } }
        public static AudioClip Thunder { get { if (thunder == null) thunder = Noise("Tonnerre", 2.5f, 0.02f, 3.5f, 21, true); return thunder; } }
        public static AudioClip Cash { get { if (cash == null) cash = Tones("Paiement", new[] { 1320f, 1760f }, 0.09f); return cash; } }
        public static AudioClip Click { get { if (click == null) click = Tones("Clic", new[] { 2400f }, 0.025f); return click; } }

        public static AudioClip Whistle
        {
            get
            {
                if (whistle == null)
                {
                    int n = (int)(Rate * 0.9f);
                    float[] d = new float[n];
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)Rate;
                        float f = 2600f + Mathf.Sin(2f * Mathf.PI * 28f * t) * 180f;
                        d[i] = Mathf.Sin(2f * Mathf.PI * f * t) * 0.35f * Mathf.Clamp01((0.9f - t) * 8f);
                    }
                    whistle = Make("Sifflet", d);
                }
                return whistle;
            }
        }
    }

    /// <summary>Son du véhicule du joueur : moteur, klaxon, clignotants.</summary>
    public class VehicleAudio : MonoBehaviour
    {
        public RaycastVehicle Vehicle;
        public bool HornPressed;
        private AudioSource engine, hornSource, fx;
        private int lastIndicatorPhase;

        public void Setup(RaycastVehicle v)
        {
            Vehicle = v;
            engine = gameObject.AddComponent<AudioSource>();
            engine.clip = v.Kind == VehicleKind.Moto ? AudioSynth.EngineMoto : AudioSynth.EngineTricycle;
            engine.loop = true;
            engine.spatialBlend = 0.3f;
            engine.volume = 0.45f;
            engine.Play();
            hornSource = gameObject.AddComponent<AudioSource>();
            hornSource.clip = v.Kind == VehicleKind.Moto ? AudioSynth.HornHigh : AudioSynth.HornLow;
            hornSource.loop = true;
            hornSource.spatialBlend = 0.2f;
            hornSource.volume = 0.6f;
            fx = gameObject.AddComponent<AudioSource>();
            fx.spatialBlend = 0f;
        }

        public void PlayOneShot(AudioClip clip, float volume)
        {
            if (fx != null && clip != null) fx.PlayOneShot(clip, volume);
        }

        private void Update()
        {
            if (Vehicle == null || engine == null) return;
            float rpm = Vehicle.Rpm01;
            bool running = !Vehicle.EngineCut;
            engine.pitch = Mathf.Lerp(engine.pitch, running ? 0.75f + rpm * 1.9f : 0.5f, Time.deltaTime * 8f);
            engine.volume = running ? 0.25f + Vehicle.ThrottleSmoothed * 0.35f : 0f;
            if (HornPressed && !hornSource.isPlaying) hornSource.Play();
            else if (!HornPressed && hornSource.isPlaying) hornSource.Stop();
            int phase = Vehicle.Indicator != 0 ? (int)(Time.time / 0.4f) : 0;
            if (Vehicle.Indicator != 0 && phase != lastIndicatorPhase) fx.PlayOneShot(AudioSynth.Click, 0.25f);
            lastIndicatorPhase = phase;
        }
    }

    /// <summary>Ambiance : ville, pluie, tonnerre, klaxons lointains.</summary>
    public class AmbientAudio : MonoBehaviour
    {
        public static AmbientAudio Instance;
        private AudioSource rain, city, fx;
        private float nextHonk;

        private void Awake()
        {
            Instance = this;
            city = gameObject.AddComponent<AudioSource>();
            city.clip = AudioSynth.City;
            city.loop = true;
            city.volume = 0.12f;
            city.Play();
            rain = gameObject.AddComponent<AudioSource>();
            rain.clip = AudioSynth.Rain;
            rain.loop = true;
            rain.volume = 0f;
            rain.Play();
            fx = gameObject.AddComponent<AudioSource>();
        }

        public void SetRain(float intensity) { if (rain != null) rain.volume = Mathf.Clamp01(intensity) * 0.55f; }

        public void Thunder() { if (fx != null) fx.PlayOneShot(AudioSynth.Thunder, 0.8f); }

        public void Play(AudioClip clip, float volume) { if (fx != null && clip != null) fx.PlayOneShot(clip, volume); }

        public static void HonkAt(Vector3 position, bool low)
        {
            AudioSource.PlayClipAtPoint(low ? AudioSynth.HornLow : AudioSynth.HornHigh, position, 0.5f);
        }

        private void Update()
        {
            if (Time.timeScale <= 0f) return;
            if (Time.time > nextHonk)
            {
                nextHonk = Time.time + Random.Range(6f, 18f);
                if (fx != null) fx.PlayOneShot(Random.value < 0.5f ? AudioSynth.HornHigh : AudioSynth.HornLow, 0.04f);
            }
        }
    }
}
