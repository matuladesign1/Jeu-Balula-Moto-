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
            if (!fadeOut) d = Loopable(d, Rate / 10);
            return Make(name, d);
        }

        /// <summary>Boucle sans clic : la fin se fond dans le début et la lecture reprend juste après le fondu.</summary>
        private static float[] Loopable(float[] d, int fade)
        {
            int n = d.Length;
            fade = Mathf.Clamp(fade, 1, n / 3);
            float[] o = new float[n - fade];
            for (int i = 0; i < o.Length; i++) o[i] = d[i + fade];
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                int j = o.Length - fade + i;
                o[j] = d[j + fade] * (1f - k) + d[i] * k;
            }
            return o;
        }

        /// <summary>Ambiance de rue (10 s) : grondement lointain, murmure de foule qui varie, motos qui passent au loin.</summary>
        private static AudioClip CityAmbience()
        {
            float seconds = 10.5f;
            int n = (int)(Rate * seconds);
            float[] d = new float[n];
            System.Random rnd = new System.Random(13);
            float low = 0f, mid1 = 0f, mid2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float x = (float)rnd.NextDouble() * 2f - 1f;
                low += (x - low) * 0.012f;
                mid1 += (x - mid1) * 0.18f;
                mid2 += (mid1 - mid2) * 0.18f;
                float murmur = (mid1 - mid2) * (0.55f + 0.25f * Mathf.Sin(2f * Mathf.PI * 0.37f * t) + 0.2f * Mathf.Sin(2f * Mathf.PI * 0.83f * t + 1.3f));
                d[i] = low * 1.6f + murmur * 0.5f;
            }
            // Motos lointaines : bourdonnement qui monte puis s'éloigne (effet Doppler léger)
            float[] starts = { 0.8f, 4.1f, 7.2f };
            float[] freqs = { 31f, 27f, 34f };
            for (int m = 0; m < starts.Length; m++)
            {
                int a = (int)(starts[m] * Rate), len = (int)(2.6f * Rate);
                for (int i = 0; i < len && a + i < n; i++)
                {
                    float u = i / (float)len;
                    float env = Mathf.Sin(Mathf.PI * u);
                    float f = freqs[m] * (1.06f - 0.12f * u);
                    float tt = i / (float)Rate;
                    float local = tt % (1f / f);
                    float pulse = Mathf.Exp(-local * 45f) * Mathf.Sin(2f * Mathf.PI * 110f * local);
                    d[a + i] += pulse * env * env * 0.09f;
                }
            }
            return Make("Ville", Loopable(d, Rate / 2));
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
        public static AudioClip City { get { if (city == null) city = CityAmbience(); return city; } }
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
            city.volume = 0f;
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
            // Bruit de la ville seulement pendant la partie (le menu garde la musique seule)
            bool playing = BalulaGame.Instance != null && BalulaGame.Instance.Playing && Time.timeScale > 0f;
            if (city != null) city.volume = Mathf.MoveTowards(city.volume, playing ? 0.09f : 0f, Time.unscaledDeltaTime * 0.1f);
            if (Time.timeScale <= 0f) return;
            if (Time.time > nextHonk)
            {
                nextHonk = Time.time + Random.Range(6f, 18f);
                if (fx != null) fx.PlayOneShot(Random.value < 0.5f ? AudioSynth.HornHigh : AudioSynth.HornLow, 0.04f);
            }
        }
    }

    /// <summary>Musique de fond : morceaux originaux (rumba, sebene, ndombolo) joués en continu, enchaînés en
    /// fondu, volume réglable (Paramètres > Musique, 0 % = coupée). Plus douce pendant la conduite.</summary>
    public class MusicPlayer : MonoBehaviour
    {
        public static MusicPlayer Instance;
        private AudioSource[] sources;
        private int current;
        private AudioClip[] clips;
        private readonly System.Collections.Generic.List<int> order = new System.Collections.Generic.List<int>();
        private int orderIndex;
        private float[] levels = new float[2];
        private int lastClip = -1;
        public string NowPlaying { get; private set; }

        public static MusicPlayer Create()
        {
            if (Instance != null) return Instance;
            GameObject g = new GameObject("Musique");
            DontDestroyOnLoad(g);
            Instance = g.AddComponent<MusicPlayer>();
            return Instance;
        }

        private void Awake()
        {
            Instance = this;
            clips = Resources.LoadAll<AudioClip>("BalulaMoto/Musique");
            System.Array.Sort(clips, (a, b) => string.CompareOrdinal(a.name, b.name));
            sources = new AudioSource[2];
            for (int i = 0; i < 2; i++)
            {
                sources[i] = gameObject.AddComponent<AudioSource>();
                sources[i].loop = false;
                sources[i].playOnAwake = false;
                sources[i].spatialBlend = 0f;
                sources[i].priority = 0;
                sources[i].ignoreListenerPause = true;
                sources[i].volume = 0f;
            }
            if (clips.Length == 0) Debug.LogWarning("[BALULA] Aucune musique dans Resources/BalulaMoto/Musique");
        }

        private float Target()
        {
            float v = GameSettings.MusicVolume;
            if (v <= 0.001f) return 0f;
            bool driving = BalulaGame.Instance != null && BalulaGame.Instance.Playing && Time.timeScale > 0f;
            return v * (driving ? 0.38f : 0.6f);
        }

        private int NextClip()
        {
            if (clips.Length == 0) return -1;
            if (lastClip < 0)
            {
                // Premier morceau : la rumba calme (menu)
                for (int i = 0; i < clips.Length; i++) if (clips[i].name.Contains("nzela")) return i;
                return 0;
            }
            if (orderIndex >= order.Count)
            {
                order.Clear();
                for (int i = 0; i < clips.Length; i++) order.Add(i);
                for (int i = order.Count - 1; i > 0; i--)
                {
                    int j = Random.Range(0, i + 1);
                    int tmp = order[i]; order[i] = order[j]; order[j] = tmp;
                }
                if (order.Count > 1 && order[0] == lastClip) { int tmp = order[0]; order[0] = order[1]; order[1] = tmp; }
                orderIndex = 0;
            }
            return order[orderIndex++];
        }

        public void Skip()
        {
            StartNext();
        }

        private void StartNext()
        {
            int k = NextClip();
            if (k < 0) return;
            lastClip = k;
            current = 1 - current;
            AudioSource s = sources[current];
            s.clip = clips[k];
            s.time = 0f;
            s.volume = 0f;
            levels[current] = 0f;
            s.Play();
            NowPlaying = Title(clips[k].name);
        }

        public static string Title(string clipName)
        {
            if (clipName.Contains("sebene")) return "Sebene ya Balula";
            if (clipName.Contains("nzela")) return "Nzela Malamu";
            if (clipName.Contains("butu")) return "Butu ya Kin";
            if (clipName.Contains("ndombolo")) return "Ndombolo ya Moto";
            return clipName;
        }

        private void Update()
        {
            if (clips == null || clips.Length == 0) return;
            float target = Target();
            AudioSource cur = sources[current];
            if (target <= 0f)
            {
                for (int i = 0; i < 2; i++)
                {
                    levels[i] = Mathf.MoveTowards(levels[i], 0f, Time.unscaledDeltaTime * 0.8f);
                    sources[i].volume = levels[i];
                    if (levels[i] <= 0f && sources[i].isPlaying) sources[i].Pause();
                }
                return;
            }
            if (cur.clip == null) { StartNext(); cur = sources[current]; }
            else if (!cur.isPlaying && cur.time <= 0.01f) { StartNext(); cur = sources[current]; }
            else if (!cur.isPlaying) cur.UnPause();
            // Fondu enchaîné 3 s avant la fin du morceau
            if (cur.clip != null && cur.isPlaying && cur.clip.length - cur.time < 3f) { StartNext(); cur = sources[current]; }
            for (int i = 0; i < 2; i++)
            {
                float goal = i == current ? target : 0f;
                levels[i] = Mathf.MoveTowards(levels[i], goal, Time.unscaledDeltaTime * (i == current ? 0.35f : 0.5f));
                sources[i].volume = levels[i];
                if (i != current && levels[i] <= 0f && sources[i].isPlaying) sources[i].Stop();
            }
        }
    }
}
