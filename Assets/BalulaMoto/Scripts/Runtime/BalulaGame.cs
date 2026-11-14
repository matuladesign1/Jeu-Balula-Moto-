// BALULA MOTO — point d'entrée du jeu : données, monde (Kinshasa importée ou piste d'essai + zone de Matadi),
// interface, parties carrière / libre, sauvegardes automatiques et voyages entre zones.
using System;
using System.Collections.Generic;
using BalulaMoto.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BalulaMoto.Runtime
{
    public class BalulaGame : MonoBehaviour
    {
        public static BalulaGame Instance;

        public GameDatabase Db;
        public GameSession Session;
        public WorldContext World;
        public CameraRig Cam;
        public WorldEnvironment Env;
        public TrafficManager Traffic;
        public UIManager UI;
        public GameplayDirector Director;
        public Texture2D Logo;
        public bool Playing;
        public readonly List<string> LoadReport = new List<string>();

        private Transform worldRoot;
        private float autosaveTimer, densityTimer;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            GameSettings.Apply();

            Db = DataLoader.LoadAll(LoadReport);
            foreach (string line in LoadReport) Debug.Log("[BALULA] " + line);
            Logo = Resources.Load<Texture2D>("BalulaMoto/Logo");

            Cam = CameraRig.Create();
            Cam.Sensitivity = GameSettings.CameraSensitivity;
            Env = WorldEnvironment.Create(Cam.transform);
            new GameObject("AmbianceSonore").AddComponent<AmbientAudio>();
            // Musique de fond originale (rumba, sebene, ndombolo), réglable dans les paramètres
            MusicPlayer.Create();

            worldRoot = new GameObject("Monde").transform;
            Traffic = TrafficManager.Create(worldRoot);
            ImportedWorld imported = Compat.FindFirst<ImportedWorld>();
            World = imported != null ? ImportedWorldAdapter.Build(imported, worldRoot, Traffic) : TestTrackBuilder.Build(worldRoot, Traffic);
            // Zone de Matadi, posée à l'écart de la carte principale (hors du champ de vision)
            float offset = imported != null ? Mathf.Max(3200f, imported.halfSize.x + 2500f) : 3200f;
            MatadiBuilder.Build(World, worldRoot, new Vector3(offset, 0f, 0f));
            Traffic.Init(World.TrafficPaths, World.WalkPaths, GameSettings.Quality == 0 ? 10 : 22);
            // Foule de piétons réalistes autour du joueur (trottoirs OSM ou de la piste d'essai)
            if (HumanLibrary.Available) CrowdSystem.Create(worldRoot, CrowdSystem.CollectLanes(World, imported));
            // Ville pleine : parcelles clôturées, boutiques, panneaux publicitaires le long de toutes les rues
            if (imported != null) CityFill.Create(worldRoot, imported);
            // Routes en travaux (barrières, sable, pelleteuse, ouvriers) construites autour du joueur
            if (imported != null) Roadworks.Create(worldRoot, imported);
            // Policiers à pied aux carrefours (modèle de MATULA DESIGN) : sifflet, arrêt et amende en cas d'infraction
            if (imported != null) PolicePatrol.Create(worldRoot, imported);
            // Poussière soulevée par les véhicules sur les avenues de terre et les trottoirs (temps sec)
            DustFx.Create(worldRoot);
            // Plan de la ville pour la mini-carte et la carte plein écran
            CityMap.Build(imported, World);
            Cam.SetMenuOrbit(World.MenuCenter, 70f);
            // Garage du menu principal (moto du joueur et chauffeur debout à côté)
            GarageScene.Create();

            EnsureEventSystem();
            UI = UIManager.Create();
            Time.timeScale = 0f;
        }

        private void Start()
        {
            UI.ShowMainMenu();
            if (Db.Vehicles.Count == 0) UI.Toast("Données introuvables : vérifiez le dossier Resources/BalulaData.");
        }

        private static void EnsureEventSystem()
        {
            if (Compat.FindFirst<EventSystem>() != null) return;
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            UnityEngine.InputSystem.UI.InputSystemUIInputModule module = es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            module.AssignDefaultActions();
#else
            es.AddComponent<StandaloneInputModule>();
#endif
        }

        // ------------------------------------------------------------------
        // Parties
        // ------------------------------------------------------------------

        public void ContinueLast()
        {
            string slot = SaveService.LastSlot;
            if (string.IsNullOrEmpty(slot) || !SaveService.Exists(slot)) { UI.Toast("Aucune partie sauvegardée."); return; }
            PlayerProfile p = SaveService.Load(slot);
            if (p == null) { UI.Toast("Sauvegarde illisible : commencez une nouvelle partie."); return; }
            StartGame(p);
        }

        public void PlayMode(GameMode mode)
        {
            string slot = mode == GameMode.Libre ? SaveService.SlotFree : SaveService.SlotCareer;
            if (SaveService.Exists(slot))
            {
                PlayerProfile p = SaveService.Load(slot);
                if (p != null) { StartGame(p); return; }
            }
            UI.NewGame.Mode = mode;
            UI.Open(UI.NewGame);
        }

        public void NewGame(GameMode mode, string vehicleId, string playerName)
        {
            string slot = mode == GameMode.Libre ? SaveService.SlotFree : SaveService.SlotCareer;
            PlayerProfile p = GameSession.CreateProfile(Db, mode, vehicleId, playerName, slot);
            GameSettings.CopyTo(p);
            StartGame(p);
            if (UI.Hud != null) UI.Hud.ResetHelp();
        }

        public void StartGame(PlayerProfile profile)
        {
            StopGame();
            if (profile.parametres == null) GameSettings.CopyTo(profile);
            Session = new GameSession(Db, profile, Environment.TickCount);
            Session.Toast += OnToast;
            Session.MissionCompleted += OnMissionCompleted;
            Session.NewDay += OnNewDay;
            WeatherKind w;
            if (!string.IsNullOrEmpty(profile.meteo) && Enum.TryParse(profile.meteo, out w))
            {
                // Pas de pluie les jours secs (la pluie ne tombe plus tous les jours)
                if ((w == WeatherKind.Pluie || w == WeatherKind.Orage) && !WorldEnvironment.RainyDay(profile.jour)) w = WeatherKind.Nuageux;
                Env.SetWeather(w, true);
            }

            Director = new GameObject("Deroulement").AddComponent<GameplayDirector>();
            Director.Begin(Session, World, Env, Cam, Traffic);
            if (profile.provinceActuelle == "kongo-central" && World.HasMatadi && Session.Career.IsUnlocked("kongo-central"))
                Director.SpawnPlayer(World.MatadiSpawn, World.MatadiSpawnRotation);
            Env.Tick(Session, 0.02f);
            Traffic.UpdateDensity(Session.Hour);

            Playing = true;
            autosaveTimer = 0f;
            SaveService.LastSlot = profile.emplacement;
            UI.CloseAllModals();
            UI.ShowHud(true);
            Time.timeScale = 1f;
            Save();
        }

        private void StopGame()
        {
            Playing = false;
            if (Director != null)
            {
                Director.Shutdown();
                Destroy(Director.gameObject);
                Director = null;
            }
            if (Session != null)
            {
                Session.Toast -= OnToast;
                Session.MissionCompleted -= OnMissionCompleted;
                Session.NewDay -= OnNewDay;
                Session = null;
            }
            foreach (ClientStop s in ClientStop.All)
            {
                foreach (WaitingClient c in s.Clients) if (c != null) Destroy(c.gameObject);
                s.Clients.Clear();
            }
            PlayerControls.ResetTouch();
        }

        public void ReturnToMenu()
        {
            Save();
            StopGame();
            Cam.SetTarget(null);
            Cam.SetMenuOrbit(World.MenuCenter, 70f);
            UI.ShowMainMenu();
            Time.timeScale = 0f;
        }

        public void Quit()
        {
            Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ------------------------------------------------------------------
        // Voyages entre zones (Kinshasa ⇄ Matadi)
        // ------------------------------------------------------------------

        public string CurrentZone()
        {
            if (Director == null || Director.Player == null || World == null) return "kinshasa";
            return World.ZoneAt(Director.Player.transform.position);
        }

        public void TravelTo(string zone)
        {
            if (Session == null || Director == null) return;
            if (Director.State == RideState.EnCourse) { UI.Toast("Terminez d'abord la course en cours."); return; }
            if (zone == MatadiBuilder.Zone)
            {
                if (!World.HasMatadi || !Session.Career.IsUnlocked("kongo-central")) { UI.Toast("Débloquez d'abord la province du Kongo Central."); return; }
                Session.AdvanceMinutes(300f);
                Director.SpawnPlayer(World.MatadiSpawn, World.MatadiSpawnRotation);
                UI.Toast("Bienvenue à Matadi ! Le Pont Maréchal est devant vous.");
            }
            else
            {
                Session.AdvanceMinutes(300f);
                Director.SpawnPlayer(World.SpawnPosition, World.SpawnRotation);
                UI.Toast("Retour à Kinshasa, devant le garage MATULA.");
            }
            Traffic.UpdateDensity(Session.Hour);
            Save();
        }

        // ------------------------------------------------------------------
        // Boucle, sauvegardes
        // ------------------------------------------------------------------

        private void Update()
        {
            if (!Playing || Session == null) return;
            float dt = Time.deltaTime;
            if (dt > 0f)
            {
                Session.AdvanceMinutes(dt * Mathf.Max(0.01f, Db.Economy.minutesJeuParSeconde));
                Session.Profile.tempsJeuSecondes += dt;
                Env.Tick(Session, dt);
                densityTimer -= dt;
                if (densityTimer <= 0f) { densityTimer = 10f; Traffic.UpdateDensity(Session.Hour); }
            }
            autosaveTimer += Time.unscaledDeltaTime;
            if (autosaveTimer > 60f) { autosaveTimer = 0f; Save(); }
        }

        public void Save()
        {
            if (Session == null) return;
            PlayerProfile p = Session.Profile;
            GameSettings.CopyTo(p);
            if (Env != null) p.meteo = Env.Weather.ToString();
            p.provinceActuelle = CurrentZone() == MatadiBuilder.Zone ? "kongo-central" : "kinshasa";
            if (!SaveService.Save(p) && UI != null) UI.Toast("Échec de la sauvegarde (espace de stockage ?).");
        }

        private void OnApplicationPause(bool paused) { if (paused) Save(); }
        private void OnApplicationQuit() { Save(); }

        private void OnToast(string message) { if (UI != null) UI.Toast(message); }
        private void OnMissionCompleted(MissionDef m) { if (AmbientAudio.Instance != null) AmbientAudio.Instance.Play(AudioSynth.Cash, 0.6f); }
        private void OnNewDay(int day) { Save(); }
    }
}
