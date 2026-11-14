// BALULA MOTO — photos de contrôle prises automatiquement dans l'éditeur (GitHub Actions, écran virtuel) :
// menu garage, caméra de poursuite, chauffeur, véhicules de la circulation, piétons, station-service, rues et
// vues aériennes, avec un rapport (maillages animés, groupes de détails, matériaux, hauteur au-dessus du sol)
// pour vérifier que tout est dessiné avant d'envoyer un APK.
// Ligne de commande : -executeMethod BalulaMoto.EditorTools.BalulaSnapshots.Run -balulaOsm carte.osm -balulaPhotos dossier
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using BalulaMoto.Core;
using BalulaMoto.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BalulaMoto.EditorTools
{
    [InitializeOnLoad]
    public static class BalulaSnapshots
    {
        private const string KeyActive = "BalulaSnap.Active";
        private const string KeyDir = "BalulaSnap.Dir";
        private const string KeyStart = "BalulaSnap.Start";
        private const double TimeoutSeconds = 2400.0;
        private static double waitStartReal, lastDiag;
        private const int W = 1280, H = 720;

        private static readonly List<Func<bool>> steps = new List<Func<bool>>();
        private static readonly StringBuilder report = new StringBuilder();
        private static int stepIndex, shotIndex, frames, waitFrames;
        private static float waitUntil;
        private static double lastEnter = -100.0;
        private static bool planned, finished;
        private static Camera snap;
        private static RenderTexture rt;
        private static Texture2D tex;

        static BalulaSnapshots()
        {
            if (!SessionState.GetBool(KeyActive, false)) return;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        /// <summary>Prépare le projet (personnages, carte), puis lance le mode Jeu et prend les photos.</summary>
        public static void Run()
        {
            try
            {
                string dir = BalulaSetup.Arg("-balulaPhotos");
                if (string.IsNullOrEmpty(dir)) dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Builds", "Photos");
                Directory.CreateDirectory(dir);
                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                    EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
                Debug.Log("[BALULA] " + BalulaSetup.PrepareInternal());
                BalulaSetup.ImportOsmForBuild();
                EditorSceneManager.OpenScene(BalulaSetup.ScenePath, OpenSceneMode.Single);
                // Shaders compilés tout de suite (sinon objets provisoirement dessinés en cyan sur les photos)
                EditorSettings.asyncShaderCompilation = false;
                SessionState.SetBool(KeyActive, true);
                SessionState.SetString(KeyDir, dir);
                SessionState.SetFloat(KeyStart, (float)EditorApplication.timeSinceStartup);
                EditorApplication.update -= Tick;
                EditorApplication.update += Tick;
                Debug.Log("[BALULA] Photos : passage en mode Jeu…");
                lastEnter = EditorApplication.timeSinceStartup;
                EditorApplication.EnterPlaymode();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        private static void Tick()
        {
            if (finished) return;
            double now = EditorApplication.timeSinceStartup;
            double start = SessionState.GetFloat(KeyStart, (float)now);
            if (now - start > TimeoutSeconds)
            {
                report.AppendLine("DÉLAI DÉPASSÉ à l'étape " + stepIndex + " (mode Jeu : " + EditorApplication.isPlaying + ")");
                Finish(3);
                return;
            }
            try
            {
                if (!EditorApplication.isPlaying)
                {
                    if (!EditorApplication.isPlayingOrWillChangePlaymode && now - lastEnter > 45.0)
                    {
                        lastEnter = now;
                        Debug.Log("[BALULA] Photos : nouvelle demande de passage en mode Jeu");
                        EditorApplication.EnterPlaymode();
                    }
                    return;
                }
                // Une erreur dans le journal peut mettre le mode Jeu en pause (« Error Pause ») : on reprend aussitôt
                if (EditorApplication.isPaused)
                {
                    EditorApplication.isPaused = false;
                    report.AppendLine("   (mode Jeu mis en pause par l'éditeur à l'étape " + stepIndex + " : reprise)");
                }
                BalulaGame game = BalulaGame.Instance;
                if (game == null || game.UI == null || game.World == null) return;
                frames++;
                if (!planned)
                {
                    if (frames < 20) return;
                    planned = true;
                    Plan(game);
                }
                if (waitFrames > 0 || Time.time < waitUntil)
                {
                    if (waitFrames > 0) waitFrames--;
                    // Partie figée par une fenêtre ouverte par le jeu (contrôle de police, client...) pendant une attente :
                    // personne ne répond dans les photos, on la ferme après 20 s réelles
                    if (game.Playing && Time.timeScale == 0f && game.UI.AnyModalOpen && now - waitStartReal > 20.0)
                    {
                        report.AppendLine("   (fenêtre du jeu ouverte pendant l'attente de l'étape " + stepIndex + " : fermée pour continuer)");
                        game.UI.CloseAllModals();
                    }
                    // Attente anormalement longue (jeu figé ou très lent) : diagnostic chaque minute, abandon après 4 minutes
                    if (now - lastDiag > 60.0)
                    {
                        lastDiag = now;
                        string diag = "   [attente étape " + stepIndex + "] " + (now - waitStartReal).ToString("0") + " s réelles — Time.time " + Time.time.ToString("0.0") + " / " + waitUntil.ToString("0.0") +
                                      ", timeScale " + Time.timeScale + ", images " + frames + ", dernière image " + Time.unscaledDeltaTime.ToString("0.00") + " s, véhicules " +
                                      (game.Traffic != null ? game.Traffic.Vehicles.Count : -1) + ", agents " + (PolicePatrol.Instance != null ? PolicePatrol.Instance.ActiveOfficers : -1);
                        Debug.Log("[BALULA] " + diag);
                        if (now - waitStartReal > 90.0) report.AppendLine(diag);
                    }
                    if (now - waitStartReal > 240.0)
                    {
                        report.AppendLine("   ATTENTE ABANDONNÉE à l'étape " + stepIndex + " après 4 minutes réelles (Time.time " + Time.time.ToString("0.0") + ", timeScale " + Time.timeScale + ")");
                        waitFrames = 0;
                        waitUntil = 0f;
                    }
                    return;
                }
                if (stepIndex >= steps.Count)
                {
                    Finish(0);
                    return;
                }
                if (steps[stepIndex]()) stepIndex++;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                report.AppendLine("ERREUR à l'étape " + stepIndex + " : " + e);
                Finish(1);
            }
        }

        private static void Wait(float gameSeconds, int minFrames)
        {
            steps.Add(() =>
            {
                waitUntil = Time.time + gameSeconds;
                waitFrames = minFrames;
                waitStartReal = EditorApplication.timeSinceStartup;
                lastDiag = waitStartReal;
                return true;
            });
        }

        private static void Plan(BalulaGame game)
        {
            steps.Clear();
            steps.Add(() => { Header(); PrepareUI(); return true; });
            Wait(0f, 8);
            steps.Add(() =>
            {
                ShootUI("menu_accueil");
                ShootMain("menu_garage");
                foreach (HumanAvatar h in Compat.FindAll<HumanAvatar>())
                    if (h != null && (h.transform.position - GarageScene.Origin).sqrMagnitude < 400f)
                        Describe("Chauffeur du garage", h.gameObject, Camera.main != null ? Camera.main.transform.position : h.transform.position);
                return true;
            });
            // Garage du menu : plateau tournant, moto centrale puis les deux Honda, onglet chauffeur
            steps.Add(() => { game.UI.OpenShowroom(false); return true; });
            // Menu : le jeu est en pause (temps arrêté), on attend des images et non des secondes de jeu
            Wait(0f, 24);
            foreach (string vid in new[] { "tvs_hlx150", "honda_cb750_four", "honda_classique", "bajaj_re" })
            {
                string v = vid;
                steps.Add(() => { if (!game.UI.Showroom.SelectVehicle(v)) report.AppendLine("Garage : modèle absent " + v); return true; });
                Wait(0f, 10);
                steps.Add(() => { PrepareUI(); return true; });
                Wait(0f, 8);
                steps.Add(() => { ShootUI("garage_plateau_" + v); return true; });
            }
            steps.Add(() => { game.UI.Showroom.ShowDriverTab(); return true; });
            Wait(0f, 10);
            steps.Add(() => { PrepareUI(); return true; });
            Wait(0f, 8);
            steps.Add(() => { ShootUI("garage_chauffeur"); game.UI.Close(game.UI.Showroom); return true; });
            Wait(0f, 8);
            steps.Add(() => { StartGame(game); return true; });
            Wait(32f, 60);
            steps.Add(() => { ShootAround(game, "depart"); ShootStopAndSigns(game, "depart"); ShootPlayerMoto(game); return true; });
            // Modèles de MATULA DESIGN dans la ville (novembre 2026)
            steps.Add(() => { TeleportToRoadNamed(game, "lumumba"); return true; });
            Wait(14f, 40);
            steps.Add(() => { ShootModels(game, "lumumba"); return true; });
            steps.Add(() => { TeleportToGreenStrip(game); return true; });
            Wait(12f, 40);
            steps.Add(() => { ShootGreenStrip(game); return true; });
            steps.Add(() => { TeleportToRoadNamed(game, "huilerie"); return true; });
            Wait(12f, 40);
            steps.Add(() => { ShootModels(game, "huilerie"); return true; });
            steps.Add(() => { TeleportToDirtStreet(game); return true; });
            Wait(12f, 40);
            steps.Add(() => { ShootModels(game, "quartier"); DustRun(game, true); return true; });
            Wait(2.5f, 15);
            steps.Add(() => { ShootDust(game); DustRun(game, false); return true; });
            Wait(1.5f, 12);
            steps.Add(() => { ShootPhotoTricycle(); return true; });
            steps.Add(() => { BuildBajajShowcase(game); return true; });
            Wait(2f, 20);
            steps.Add(() => { ShootBajajShowcase(); return true; });
            // Police : poste le plus proche, agents, piéton bousculé devant l'agent, arrêt, amende payée
            steps.Add(() => { TeleportToPolice(game); return true; });
            Wait(8f, 30);
            steps.Add(() => { ShootPolice(game); PoliceBump(game); return true; });
            Wait(2f, 15);
            steps.Add(() => { PoliceLog(game, "2 s après le piéton bousculé"); return true; });
            Wait(3f, 15);
            steps.Add(() => { PoliceLog(game, "agent arrivé au véhicule"); ShootPoliceStop(game); PayPolice(game); return true; });
            Wait(3f, 15);
            steps.Add(() => { PoliceLog(game, "après l'amende"); return true; });
            // Monuments (modèles MATULA DESIGN) : stade des Martyrs et tour de l'Échangeur
            foreach (string mname in new[] { "Monument_StadeMartyrs", "Monument_TourEchangeur" })
            {
                string mn = mname;
                steps.Add(() => { TeleportToObject(game, mn); return true; });
                Wait(12f, 30);
                steps.Add(() => { ShootMonument(game, mn); return true; });
            }
            // (rivière et chantier : vérifiés dans les livraisons précédentes, photos retirées pour garder la durée)
            // Embouteillage sur une rue goudronnée (créé près du joueur pour la photo)
            steps.Add(() => { JamTest(game); return true; });
            Wait(6f, 30);
            steps.Add(() => { ShootJam(game); return true; });

            // Une grande avenue de jour, puis la même de nuit (lampadaires, fenêtres, phares)
            steps.Add(() => { Teleport(game, 0); return true; });
            Wait(22f, 40);
            steps.Add(() => { ShootStreet(game, "avenue"); ShootStopAndSigns(game, "avenue"); return true; });
            // Un arrêt : foule des deux côtés, taxis et bus qui s'arrêtent
            steps.Add(() => { TeleportToStop(game); return true; });
            Wait(26f, 60);
            steps.Add(() => { ShootStopAndSigns(game, "arret"); return true; });
            Wait(22f, 40);
            steps.Add(() => { ShootStopAndSigns(game, "arret2"); return true; });
            steps.Add(() =>
            {
                if (game.Session != null) game.Session.Profile.minuteDuJour = 20.5f * 60f;
                report.AppendLine();
                report.AppendLine("Passage à 20 h 30 (nuit).");
                return true;
            });
            Wait(16f, 40);
            steps.Add(() =>
            {
                report.AppendLine("Niveau de nuit : " + Night.Level.ToString("0.00") + " — phares du joueur : " + (game.Director.Player != null && game.Director.Player.HeadlightsOn));
                ShootStreet(game, "nuit");
                return true;
            });
            // Essai du tricycle : démarrage à fond, puis virage ; on mesure l'inclinaison
            steps.Add(() => { StartTricycle(game); return true; });
            Wait(6f, 30);
            steps.Add(() => { TrikeLog(game, "à l'arrêt"); GameplayDirector.TestInput = new DriveInput { Throttle = 1f }; return true; });
            Wait(1.2f, 10);
            steps.Add(() => { TrikeLog(game, "1,2 s après le démarrage (plein gaz)"); return true; });
            Wait(2.5f, 10);
            steps.Add(() =>
            {
                TrikeLog(game, "3,7 s (plein gaz)");
                RaycastVehicle pl = game.Director.Player;
                if (pl != null) Shoot("tricycle_demarrage", pl.transform.position + pl.transform.forward * 5.5f + pl.transform.right * 2.2f + Vector3.up * 1.5f, pl.transform.position + Vector3.up * 0.9f);
                GameplayDirector.TestInput = new DriveInput { Throttle = 0.6f, Steer = 0.7f };
                return true;
            });
            Wait(1.5f, 10);
            steps.Add(() => { TrikeLog(game, "virage à droite (1,5 s)"); GameplayDirector.TestInput = new DriveInput { FrontBrake = 1f, RearBrake = 1f }; return true; });
            Wait(2.5f, 10);
            steps.Add(() =>
            {
                TrikeLog(game, "après freinage");
                RaycastVehicle pl = game.Director.Player;
                if (pl != null) Shoot("tricycle_apres_essai", pl.transform.position - pl.transform.forward * 6f + pl.transform.right * 2f + Vector3.up * 1.8f, pl.transform.position + Vector3.up * 0.9f);
                GameplayDirector.TestInput = null;
                return true;
            });
        }

        // ------------------------------------------------------------------
        // Partie et déplacements
        // ------------------------------------------------------------------

        private static void StartGame(BalulaGame game)
        {
            string vid = null;
            foreach (VehicleDef d in game.Db.StartingVehicles()) { vid = d.id; break; }
            game.NewGame(GameMode.Libre, vid, "Photo CI");
            if (game.Session != null) game.Session.Profile.minuteDuJour = 10f * 60f;
            if (game.Env != null) game.Env.SetWeather(WeatherKind.Soleil, true);
            report.AppendLine("Partie libre démarrée avec le véhicule : " + vid + " (10 h, soleil)");
        }

        private static float trikeMaxRoll;

        private static void StartTricycle(BalulaGame game)
        {
            string vid = null;
            foreach (VehicleDef d in game.Db.Vehicles) if (d.Kind == VehicleKind.Tricycle) { vid = d.id; break; }
            report.AppendLine();
            report.AppendLine("=== Essai du tricycle : " + (vid ?? "aucun tricycle dans les données") + " ===");
            if (vid == null) return;
            game.NewGame(GameMode.Libre, vid, "Essai tricycle");
            if (game.Session != null) game.Session.Profile.minuteDuJour = 10f * 60f;
            trikeMaxRoll = 0f;
        }

        private static void TrikeLog(BalulaGame game, string when)
        {
            RaycastVehicle pl = game.Director != null ? game.Director.Player : null;
            if (pl == null) { report.AppendLine("   (pas de véhicule)"); return; }
            Transform t = pl.transform;
            // Roulis : angle entre l'axe droit du véhicule et l'horizontale ; tangage : idem pour l'axe avant
            float roll = Mathf.Asin(Mathf.Clamp(t.right.y, -1f, 1f)) * Mathf.Rad2Deg;
            float pitch = Mathf.Asin(Mathf.Clamp(t.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
            trikeMaxRoll = Mathf.Max(trikeMaxRoll, Mathf.Abs(roll));
            report.AppendLine("   " + when + " : type " + pl.Kind + ", vitesse " + pl.SpeedKmh.ToString("0.0") + " km/h, roulis " + roll.ToString("0.0") + "°, tangage " + pitch.ToString("0.0") +
                              "°, hauteur " + t.position.y.ToString("0.00") + " m, renversé " + pl.IsUpsideDown + ", accidenté " + pl.Crashed + " — roulis maximal mesuré " + trikeMaxRoll.ToString("0.0") + "°");
        }

        // ------------------------------------------------------------------
        // Police
        // ------------------------------------------------------------------
        private static long moneyBeforeFine;
        private static Vector3 policeSpot;

        private static void TeleportToPolice(BalulaGame game)
        {
            report.AppendLine();
            PolicePatrol pp = PolicePatrol.Instance;
            RaycastVehicle pl = game.Director != null ? game.Director.Player : null;
            if (pp == null || pl == null) { report.AppendLine("=== police : aucun poste (carte sans rues ?) ==="); return; }
            Vector3 j, spot;
            if (!pp.NearestPost(pl.transform.position, out j, out spot)) { report.AppendLine("=== police : aucun poste ==="); return; }
            // Sur la chaussée la plus proche de l'agent, à 6-18 m, en regardant vers le carrefour
            Vector3 best = spot;
            float bd = float.MaxValue;
            foreach (TrafficPath tp in game.World.TrafficPaths)
                foreach (Vector3 q in tp.Points)
                {
                    float d = (q - spot).sqrMagnitude;
                    if (d > 36f && d < 18f * 18f && d < bd) { bd = d; best = q; }
                }
            Vector3 look = j - best;
            look.y = 0f;
            if (look.sqrMagnitude < 1f) look = spot - best;
            look.y = 0f;
            if (look.sqrMagnitude < 0.01f) look = Vector3.forward;
            game.Director.SpawnPlayer(new Vector3(best.x, 0.3f, best.z), Quaternion.LookRotation(look.normalized, Vector3.up));
            policeSpot = spot;
            moneyBeforeFine = game.Session != null ? game.Session.Profile.argent : 0;
            report.AppendLine("=== police : " + pp.PostCount + " postes dans la ville ; poste le plus proche, carrefour " + j.ToString("F0") + ", agent " + spot.ToString("F0") +
                              " ; agents réalistes (avatar en tenue congolaise, fusil) " + PoliceAgent.Realistic + " ===");
        }

        private static Transform NearestAgent(BalulaGame game, float radius)
        {
            RaycastVehicle pl = game.Director != null ? game.Director.Player : null;
            if (PolicePatrol.Instance == null || pl == null) return null;
            return PolicePatrol.Instance.NearestOfficer(pl.transform.position, radius);
        }

        private static void ShootPolice(BalulaGame game)
        {
            Transform a = PolicePatrol.Instance != null ? PolicePatrol.Instance.NearestOfficer(policeSpot, 30f) : null;
            if (a == null) a = NearestAgent(game, 80f);
            RaycastVehicle plr = game.Director != null ? game.Director.Player : null;
            if (a != null && plr != null)
            {
                // la moto du joueur ne doit pas cacher l'agent : on la recule à 11 m
                Vector3 away = plr.transform.position - a.position;
                away.y = 0f;
                if (away.magnitude < 8f)
                {
                    if (away.sqrMagnitude < 0.01f) away = -a.forward;
                    away.y = 0f;
                    away.Normalize();
                    Vector3 np = a.position + away * 11f;
                    game.Director.SpawnPlayer(new Vector3(np.x, 0.3f, np.z), Quaternion.LookRotation(-away, Vector3.up));
                    report.AppendLine("   moto du joueur reculée à 11 m de l'agent pour les photos");
                }
            }
            report.AppendLine("Agents en place autour du joueur : " + (PolicePatrol.Instance != null ? PolicePatrol.Instance.ActiveOfficers : 0));
            if (a == null) { report.AppendLine("   aucun agent à moins de 80 m"); return; }
            Vector3 c = a.position + Vector3.up * 1.0f;
            Shoot("police_agent_face", a.position + a.forward * 2.6f + Vector3.up * 1.55f, c + Vector3.up * 0.35f);
            Shoot("police_agent_trois_quarts", a.position + a.forward * 2.2f + a.right * 1.8f + Vector3.up * 1.4f, c);
            Shoot("police_agent_profil", a.position + a.right * 3.2f + Vector3.up * 1.2f, c);
            Shoot("police_agent_dos_fusil", a.position - a.forward * 2.4f + a.right * 1.0f + Vector3.up * 1.6f, c + Vector3.up * 0.3f);
            Shoot("police_agent_visage", a.position + a.forward * 1.1f + Vector3.up * 1.72f, a.position + Vector3.up * 1.62f);
            HumanAvatar hav = a.GetComponent<HumanAvatar>();
            bool hasGun = false;
            foreach (Transform tr in a.GetComponentsInChildren<Transform>(true)) if (tr.name == "FusilEnBandouliere") hasGun = true;
            report.AppendLine("   agent réaliste " + (hav != null) + " (" + (hav != null ? hav.AvatarId : "policier dessiné") + "), fusil en bandoulière " + hasGun);
            RaycastVehicle pl = game.Director.Player;
            Vector3 mid = (a.position + pl.transform.position) * 0.5f;
            Shoot("police_carrefour", mid + (pl.transform.position - a.position).normalized * 4f + Vector3.up * 9f - pl.transform.forward * 14f, mid);
            Bounds b = new Bounds(a.position, Vector3.zero);
            foreach (Renderer r in a.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
            report.AppendLine("   agent à " + Vector3.Distance(a.position, pl.transform.position).ToString("0.0") + " m du joueur, taille " + b.size.y.ToString("0.00") + " m, pieds à y=" + b.min.y.ToString("0.00"));
        }

        private static void PoliceBump(BalulaGame game)
        {
            Transform a = NearestAgent(game, 80f);
            RaycastVehicle pl = game.Director.Player;
            Vector3 where = pl.transform.position + pl.transform.forward * 2f;
            game.Director.TestPedestrianBump(where);
            report.AppendLine("Piéton bousculé (simulé) devant le véhicule, agent le plus proche à " + (a != null ? Vector3.Distance(a.position, where).ToString("0") + " m" : "—") +
                              " : arrêt demandé " + game.Director.PoliceStopPending + " (" + game.Director.PendingInfraction + ")");
        }

        private static void PoliceLog(BalulaGame game, string when)
        {
            UIManager ui = UIManager.Instance;
            bool open = ui != null && ui.Police != null && ui.Police.IsOpen;
            Transform a = NearestAgent(game, 100f);
            RaycastVehicle pl = game.Director.Player;
            string text = "";
            if (open)
            {
                FieldInfo fi = typeof(PolicePanel).GetField("details", BindingFlags.NonPublic | BindingFlags.Instance);
                UnityEngine.UI.Text t = fi != null ? fi.GetValue(ui.Police) as UnityEngine.UI.Text : null;
                if (t != null) text = " — panneau : " + System.Text.RegularExpressions.Regex.Replace(t.text, "<[^>]*>", "").Replace("\n", " / ");
            }
            report.AppendLine("Police, " + when + " : arrêt en attente " + game.Director.PoliceStopPending + ", panneau d'amende ouvert " + open +
                              ", agent à " + (a != null ? Vector3.Distance(a.position, pl.transform.position).ToString("0.0") + " m" : "—") +
                              ", argent " + (game.Session != null ? game.Session.Profile.argent.ToString() : "?") + " FC" + text);
        }

        private static void ShootPoliceStop(BalulaGame game)
        {
            Transform a = NearestAgent(game, 30f);
            RaycastVehicle pl = game.Director.Player;
            if (a == null || pl == null) return;
            Vector3 mid = (a.position + pl.transform.position) * 0.5f + Vector3.up * 1.0f;
            Vector3 side = Vector3.Cross(Vector3.up, a.position - pl.transform.position).normalized;
            Shoot("police_controle", mid + side * 4.5f + Vector3.up * 0.8f, mid);
            Shoot("police_controle_haut", mid + side * 3f - pl.transform.forward * 4f + Vector3.up * 3.5f, mid);
        }

        private static void PayPolice(BalulaGame game)
        {
            UIManager ui = UIManager.Instance;
            if (ui == null || ui.Police == null || !ui.Police.IsOpen) { report.AppendLine("Amende : panneau non ouvert, rien à payer"); return; }
            MethodInfo pay = typeof(PolicePanel).GetMethod("Pay", BindingFlags.NonPublic | BindingFlags.Instance);
            if (pay != null) pay.Invoke(ui.Police, null);
            long after = game.Session != null ? game.Session.Profile.argent : 0;
            report.AppendLine("Amende payée : " + (moneyBeforeFine - after) + " FC (avant " + moneyBeforeFine + ", après " + after + ")");
            ui.Close(ui.Police);
        }

        private static void TeleportToObject(BalulaGame game, string objectName)
        {
            GameObject g = GameObject.Find(objectName);
            report.AppendLine();
            if (g == null || game.Director == null) { report.AppendLine("=== " + objectName + " : absent de la carte ==="); return; }
            // Sur la rue la plus proche du monument, à 60 m au moins
            Vector3 c = g.transform.position;
            Vector3 best = c + new Vector3(140f, 0f, 0f);
            float bd = float.MaxValue;
            foreach (TrafficPath tp in game.World.TrafficPaths)
                foreach (Vector3 q in tp.Points)
                {
                    float d = (q - c).sqrMagnitude;
                    if (d > 60f * 60f && d < bd) { bd = d; best = q; }
                }
            Vector3 look = c - best;
            look.y = 0f;
            game.Director.SpawnPlayer(new Vector3(best.x, 0.3f, best.z), Quaternion.LookRotation(look.normalized, Vector3.up));
            report.AppendLine("=== " + objectName + " à " + c.ToString("F0") + " ; joueur placé à " + Mathf.Sqrt(bd).ToString("0") + " m ===");
        }

        private static void ShootMonument(BalulaGame game, string objectName)
        {
            GameObject g = GameObject.Find(objectName);
            if (g == null) return;
            Bounds b = new Bounds(g.transform.position, Vector3.zero);
            int parts = 0;
            foreach (Renderer r in g.GetComponentsInChildren<Renderer>()) { b.Encapsulate(r.bounds); parts++; }
            report.AppendLine("Modèle : " + parts + " parties, boîte " + b.size.ToString("F0") + " m, centre " + b.center.ToString("F0") + ", rotation " + g.transform.eulerAngles.y.ToString("0") + "°");
            string tag = objectName.Replace("Monument_", "").ToLowerInvariant();
            float r0 = Mathf.Max(b.extents.x, b.extents.z);
            Vector3 c = b.center;
            Shoot(tag + "_vue_ensemble", c + new Vector3(r0 * 1.5f, Mathf.Max(60f, b.size.y * 0.9f), -r0 * 1.5f), c);
            RaycastVehicle pl = game.Director != null ? game.Director.Player : null;
            if (pl != null)
            {
                Vector3 p = pl.transform.position;
                Vector3 back = pl.transform.position - new Vector3(c.x, 0f, c.z);
                back.y = 0f;
                back = back.sqrMagnitude > 1f ? back.normalized : Vector3.forward;
                Shoot(tag + "_depuis_la_rue", p + back * 5f + Vector3.up * 2.4f, new Vector3(c.x, Mathf.Min(b.size.y * 0.5f, 30f), c.z));
            }
            // Devant la clôture : barreaux, arbres et monument à travers
            Vector3 dir = new Vector3(1f, 0f, 0.35f).normalized;
            Vector3 fence = new Vector3(c.x, 0f, c.z) + dir * (r0 + 22f);
            Shoot(tag + "_cloture", fence + Vector3.up * 1.7f, new Vector3(c.x, 6f, c.z));
            Shoot(tag + "_cloture_de_pres", new Vector3(c.x, 0f, c.z) + dir * (r0 + 14f) + Vector3.up * 1.6f + Vector3.Cross(Vector3.up, dir) * 6f, new Vector3(c.x, 2f, c.z) + dir * (r0 + 6f));
        }

        private static void TeleportToWater(BalulaGame game)
        {
            ImportedWorld map = Compat.FindFirst<ImportedWorld>();
            report.AppendLine();
            if (map == null || map.waterways == null || map.waterways.Count == 0 || game.Director == null) { report.AppendLine("=== riviere : aucun cours d'eau dans la carte ==="); return; }
            // Point de rivière le plus proche d'une rue carrossable (là où un pont ou un passage existe)
            Vector3 best = Vector3.zero;
            TrafficPath bestPath = null;
            int bestIdx = 0;
            float bd = float.MaxValue;
            string name = "";
            foreach (ImportedRoad w in map.waterways)
            {
                if (w.width < 4f) continue;
                foreach (Vector2 q2 in w.pts)
                {
                    Vector3 q = new Vector3(q2.x, 0f, q2.y);
                    if (q.magnitude > 3200f) continue;
                    foreach (TrafficPath tp in game.World.TrafficPaths)
                    {
                        if (tp.Points.Count < 2 || (tp.Points[0] - q).sqrMagnitude > 900f * 900f) continue;
                        for (int i = 0; i < tp.Points.Count; i++)
                        {
                            float d = (tp.Points[i] - q).sqrMagnitude;
                            if (d < bd) { bd = d; best = q; bestPath = tp; bestIdx = i; name = w.name; }
                        }
                    }
                }
            }
            if (bestPath == null) { report.AppendLine("=== riviere : aucune rue près d'un cours d'eau ==="); return; }
            // Sur la rue, 18 m avant l'endroit où elle croise (ou longe) la rivière, en regardant vers elle :
            // les photos montrent l'eau, les berges et le pont éventuel au lieu d'un mur de parcelle.
            List<Vector3> pts = bestPath.Points;
            int iPrev = Mathf.Max(0, bestIdx - 1), iNext = Mathf.Min(pts.Count - 1, bestIdx + 1);
            Vector3 look = pts[iNext] - pts[iPrev];
            look.y = 0f;
            if (look.sqrMagnitude < 0.01f) look = best - pts[bestIdx];
            look.y = 0f;
            if (look.sqrMagnitude < 0.01f) look = Vector3.forward;
            look.Normalize();
            Vector3 pos = pts[bestIdx];
            float walked = 0f;
            for (int i = bestIdx; i > 0 && walked < 18f; i--)
            {
                float seg = Vector3.Distance(pts[i], pts[i - 1]);
                if (walked + seg >= 18f) { pos = Vector3.Lerp(pts[i], pts[i - 1], (18f - walked) / Mathf.Max(seg, 0.01f)); walked = 18f; break; }
                walked += seg;
                pos = pts[i - 1];
            }
            if (walked < 1f) pos = pts[bestIdx] - look * 18f;
            game.Director.SpawnPlayer(new Vector3(pos.x, 0.3f, pos.z), Quaternion.LookRotation(look, Vector3.up));
            report.AppendLine("=== riviere « " + name + " » : " + map.waterways.Count + " cours d'eau dans la carte ; rue la plus proche à " + Mathf.Sqrt(bd).ToString("0") + " m ; joueur sur la rue, 18 m avant ===");
        }

        /// <summary>Rivière en tranchée la plus proche du joueur : vue le long de l'eau, vue depuis la berge vers le pont.</summary>
        private static void ShootRiver(BalulaGame game)
        {
            ImportedWorld map = Compat.FindFirst<ImportedWorld>();
            RaycastVehicle player = game.Director != null ? game.Director.Player : null;
            if (map == null || player == null || map.waterSegs == null || map.waterSegs.Count == 0) { report.AppendLine("   rivière : aucune tranchée dans la carte"); return; }
            if (map.report != null)
                foreach (string line in map.report.Split('\n'))
                    if (line.StartsWith("Rivières") || line.StartsWith("Eau :")) report.AppendLine("   " + line);
            Vector3 pp = player.transform.position;
            int best = -1;
            float bd = float.MaxValue;
            for (int i = 0; i < map.waterSegs.Count; i++)
            {
                Vector4 w = map.waterSegs[i];
                if (map.waterHw[i] < 4f) continue;
                float d = (new Vector2((w.x + w.z) * 0.5f, (w.y + w.w) * 0.5f) - new Vector2(pp.x, pp.z)).sqrMagnitude;
                if (d < bd) { bd = d; best = i; }
            }
            if (best < 0) return;
            Vector4 s = map.waterSegs[best];
            Vector3 c = new Vector3((s.x + s.z) * 0.5f, 0f, (s.y + s.w) * 0.5f);
            Vector3 dir = new Vector3(s.z - s.x, 0f, s.w - s.y).normalized, nrm = new Vector3(-dir.z, 0f, dir.x);
            float hw = map.waterHw[best];
            report.AppendLine("   tranchée la plus proche : centre (" + c.x.ToString("0") + ", " + c.z.ToString("0") + "), demi-largeur " + hw.ToString("0.0") + " m, à " + Mathf.Sqrt(bd).ToString("0") + " m du joueur");
            Shoot("riviere_le_long_de_l_eau", c - dir * 34f + Vector3.up * 5f, c + dir * 10f - Vector3.up * 1f);
            Shoot("riviere_depuis_la_berge", c - dir * 22f + nrm * (hw + 3f) + Vector3.up * 1.7f, c + dir * 6f - Vector3.up * 1f);
            Shoot("riviere_berge_de_pres", c + dir * 9f + nrm * (hw + 1.5f) + Vector3.up * 1.6f, c - nrm * hw - Vector3.up * 1.2f);
            Shoot("riviere_tranchee_de_haut", c + Vector3.up * 45f + dir * 0.5f, c);
        }

        private static GameObject bajajShow;
        private static AIVehicle photoTri;
        private static string photoTriTag = "depart";

        /// <summary>Tricycle de la circulation ajouté à l'étape précédente : chauffeur et passagers sont maintenant assis.</summary>
        private static void ShootPhotoTricycle()
        {
            if (photoTri == null) return;
            Transform bt = photoTri.transform;
            Vector3 bc = bt.position + Vector3.up * 0.95f;
            Shoot(photoTriTag + "_tricycle_trois_quarts", bc + bt.forward * 4.6f + bt.right * 3.2f + Vector3.up * 0.5f, bc);
            Shoot(photoTriTag + "_tricycle_face", bc + bt.forward * 4.8f + Vector3.up * 0.25f, bc);
            Shoot(photoTriTag + "_tricycle_cote", bc + bt.right * 5.2f, bc);
            Shoot(photoTriTag + "_tricycle_arriere", bc - bt.forward * 4.6f - bt.right * 2.4f + Vector3.up * 0.4f, bc);
            Shoot(photoTriTag + "_tricycle_interieur", bt.TransformPoint(new Vector3(1.6f, 1.75f, -0.4f)), bt.TransformPoint(new Vector3(0f, 0.8f, -0.4f)));
            Describe("Tricycle de la circulation", photoTri.gameObject, bc + bt.right * 5.2f);
            photoTri = null;
        }

        /// <summary>Bajaj RE du joueur (modèle de MATULA DESIGN) posé près du départ, avec le chauffeur et 5 passagers assis.</summary>
        private static void BuildBajajShowcase(BalulaGame game)
        {
            report.AppendLine();
            RaycastVehicle pl = game.Director != null ? game.Director.Player : null;
            VehicleDef def = game.Db != null ? game.Db.Vehicle("bajaj_re") : null;
            if (pl == null || def == null) { report.AppendLine("=== Bajaj RE du joueur : définition ou joueur absent ==="); return; }
            Transform t = pl.transform;
            bajajShow = new GameObject("Photo_BajajRE");
            Vector3 fwd = t.forward, side = t.right;
            fwd.y = 0f;
            side.y = 0f;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            if (side.sqrMagnitude < 0.01f) side = Vector3.Cross(Vector3.up, fwd);
            Vector3 basePos = t.position + fwd.normalized * 9f;
            RaycastHit gh;
            basePos.y = Physics.Raycast(basePos + Vector3.up * 30f, Vector3.down, out gh, 60f, ~(1 << RaycastVehicle.PlayerLayer)) ? gh.point.y + 0.02f : t.position.y;
            bajajShow.transform.position = basePos;
            bajajShow.transform.rotation = Quaternion.LookRotation(side.normalized, Vector3.up);
            VehicleRig rig = VehicleBuilder.BuildVisual(bajajShow.transform, def, null, true);
            int seated = 0;
            foreach (Transform seat in rig.Seats)
            {
                GameObject h = VehicleBuilder.BuildHuman(seat, Vector3.zero, seated % 2 == 0 ? "civil" : "pagne", true, 300 + seated, false);
                if (h != null) seated++;
            }
            Bounds b = new Bounds(bajajShow.transform.position, Vector3.zero);
            foreach (Renderer r in bajajShow.GetComponentsInChildren<Renderer>()) if (r.GetComponentInParent<HumanAvatar>() == null) b.Encapsulate(r.bounds);
            float headTop = 0f;
            foreach (HumanAvatar h in bajajShow.GetComponentsInChildren<HumanAvatar>())
                foreach (Renderer r in h.GetComponentsInChildren<Renderer>()) headTop = Mathf.Max(headTop, r.bounds.max.y - bajajShow.transform.position.y);
            report.AppendLine("=== Bajaj RE du joueur : modèle fourni " + (VehicleBuilder.BajajModel != null) + ", roues " + rig.Wheels.Count + ", places " + rig.Seats.Count + " (" + seated + " passagers posés), chauffeur " + (rig.Driver != null) +
                              ", taille " + b.size.x.ToString("0.00") + " × " + b.size.y.ToString("0.00") + " × " + b.size.z.ToString("0.00") + " m, sommet des têtes à " + headTop.ToString("0.00") + " m (toit à 1,70 m, dessous à 1,52 m) ===");
        }

        private static void ShootBajajShowcase()
        {
            if (bajajShow == null) return;
            Transform bt = bajajShow.transform;
            Vector3 c = bt.position + Vector3.up * 0.9f;
            Shoot("bajaj_joueur_trois_quarts_avant", c + bt.forward * 4.4f - bt.right * 3.0f + Vector3.up * 0.6f, c);
            Shoot("bajaj_joueur_cote", c - bt.right * 5.0f + Vector3.up * 0.1f, c);
            Shoot("bajaj_joueur_arriere", c - bt.forward * 4.4f + bt.right * 2.6f + Vector3.up * 0.5f, c);
            Shoot("bajaj_joueur_interieur", c + bt.forward * 2.2f + Vector3.up * 1.6f - bt.right * 0.9f, c - Vector3.up * 0.2f);
            Shoot("bajaj_joueur_face", c + bt.forward * 5.2f + Vector3.up * 0.25f, c);
            Shoot("bajaj_joueur_trois_quarts_arriere_gauche", c - bt.forward * 4.0f - bt.right * 3.0f + Vector3.up * 0.9f, c);
            Shoot("bajaj_joueur_vue_du_chauffeur", bt.TransformPoint(new Vector3(0f, 1.37f, 0.03f)), bt.TransformPoint(new Vector3(0f, 1.05f, 2.5f)));
            Describe("Bajaj RE du joueur", bajajShow, c - bt.right * 5.0f);
            UnityEngine.Object.Destroy(bajajShow);
            bajajShow = null;
        }

        private static bool jamMade;

        private static void JamTest(BalulaGame game)
        {
            report.AppendLine();
            RaycastVehicle player = game.Director != null ? game.Director.Player : null;
            jamMade = false;
            if (player == null || game.Traffic == null) return;
            // Retour au point de départ (grandes avenues goudronnées autour)
            game.Director.SpawnPlayer(game.World.SpawnPosition, game.World.SpawnRotation);
            jamMade = game.Traffic.TestJam(game.World.SpawnPosition);
            report.AppendLine("=== embouteillage : " + (jamMade ? "file créée près du départ" : "aucune rue goudronnée libre trouvée") + " (embouteillages depuis le début : " + game.Traffic.Jams + ") ===");
        }

        private static void ShootJam(BalulaGame game)
        {
            if (!jamMade || game.Traffic == null) return;
            Vector3 c = game.Traffic.LastJam, dir = game.Traffic.LastJamDir, side = Vector3.Cross(Vector3.up, dir);
            int close = 0;
            foreach (AIVehicle v in game.Traffic.Vehicles) if (v != null && (v.transform.position - c).sqrMagnitude < 60f * 60f) close++;
            report.AppendLine("   véhicules à moins de 60 m du milieu de la file : " + close);
            Shoot("embouteillage_de_face", c + dir * 42f + side * 3f + Vector3.up * 3.2f, c + Vector3.up * 1f);
            Shoot("embouteillage_de_haut", c - dir * 30f + side * 14f + Vector3.up * 16f, c + dir * 8f);
            Shoot("embouteillage_trottoir", c - dir * 6f + side * 7f + Vector3.up * 1.7f, c + dir * 14f + Vector3.up * 1.2f);
        }

        private static void TeleportToStop(BalulaGame game)
        {
            RaycastVehicle player = game.Director != null ? game.Director.Player : null;
            report.AppendLine();
            if (player == null) return;
            Vector3 p = player.transform.position;
            // Arrêt le plus proche situé au bord d'une grande rue (là où passent taxis et bus)
            ClientStop best = null;
            Vector3 bestRoad = Vector3.zero, bestDir = Vector3.forward;
            float bd = float.MaxValue;
            foreach (ClientStop cs in ClientStop.All)
            {
                if (cs == null) continue;
                Vector3 c = cs.transform.position;
                foreach (TrafficPath tp in game.World.TrafficPaths)
                {
                    if (!tp.Major || tp.Points.Count < 2) continue;
                    for (int i = 0; i < tp.Points.Count - 1; i++)
                    {
                        if ((tp.Points[i] - c).sqrMagnitude > 12f * 12f) continue;
                        float d = (c - p).sqrMagnitude;
                        if (d < bd) { bd = d; best = cs; bestRoad = tp.Points[i]; bestDir = (tp.Points[i + 1] - tp.Points[i]).normalized; }
                    }
                }
            }
            if (best == null) { report.AppendLine("=== arret : aucun arrêt au bord d'une grande rue ==="); return; }
            Vector3 pos = bestRoad - bestDir * 22f;
            game.Director.SpawnPlayer(new Vector3(pos.x, 0.3f, pos.z), Quaternion.LookRotation(bestDir, Vector3.up));
            report.AppendLine("=== arret « " + (best.Place != null ? best.Place.Name : "?") + " » à " + best.transform.position.ToString("F0") + " (" + Mathf.Sqrt(bd).ToString("0") + " m) ===");
        }

        private static void ShootStopAndSigns(BalulaGame game, string tag)
        {
            RaycastVehicle player = game.Director != null ? game.Director.Player : null;
            if (player == null) return;
            Vector3 p = player.transform.position;
            report.AppendLine("Arrêts : " + (CrowdSystem.Instance != null ? CrowdSystem.Instance.StopPeople : -1) + " personnes aux arrêts proches (" + CrowdSystem.PerSide + " par côté) — poteaux électriques : " +
                              (CityFill.Instance != null ? CityFill.Instance.PoleCount : -1) + " — carrefours avec plaques : " + (CityFill.Instance != null ? CityFill.Instance.SignCount : -1));
            // Groupe d'arrêt le plus proche
            Transform nearest = null;
            float bd = float.MaxValue;
            int count = 0;
            if (CrowdSystem.Instance != null)
                foreach (Transform ch in CrowdSystem.Instance.transform)
                {
                    if (!ch.name.StartsWith("Arret_")) continue;
                    count++;
                    float d = (ch.position - p).sqrMagnitude;
                    if (d < bd) { bd = d; nearest = ch; }
                }
            if (nearest != null)
            {
                Vector3 c = nearest.position;
                Shoot(tag + "_arret_foule", c + new Vector3(9f, 5.5f, 9f), c + Vector3.up * 1f);
                Shoot(tag + "_arret_de_pres", c + nearest.forward * 4.5f + Vector3.up * 1.6f, c + Vector3.up * 1.2f);
                report.AppendLine("Arrêt photographié à " + Mathf.Sqrt(bd).ToString("0") + " m du joueur (" + count + " personnes d'arrêt en tout)");
            }
            // Plaque de rue la plus proche
            GameObject sign = null;
            bd = float.MaxValue;
            foreach (TextMesh tm in Compat.FindAll<TextMesh>())
            {
                if (tm == null || tm.transform.parent == null || tm.transform.parent.name != "Plaque") continue;
                float d = (tm.transform.position - p).sqrMagnitude;
                if (d < bd) { bd = d; sign = tm.gameObject; }
            }
            if (sign != null)
            {
                Vector3 c = sign.transform.position;
                Vector3 n = sign.transform.forward;
                Shoot(tag + "_plaque_de_rue", c - n * 2.6f + Vector3.up * -0.3f, c);
                Shoot(tag + "_plaque_carrefour", c - n * 9f + Vector3.up * 0.5f + sign.transform.right * 3f, c + Vector3.up * -0.8f);
                report.AppendLine("Plaque « " + sign.GetComponent<TextMesh>().text + " » à " + Mathf.Sqrt(bd).ToString("0") + " m");
            }
            // Un taxi ou un bus à l'arrêt, s'il y en a un
            if (TrafficManager.Instance != null)
            {
                int dwell = 0;
                AIVehicle shown = null;
                foreach (AIVehicle v in TrafficManager.Instance.Vehicles)
                    if (v != null && v.Dwelling) { dwell++; if (shown == null) shown = v; }
                report.AppendLine("Véhicules arrêtés à un arrêt en ce moment : " + dwell);
                if (shown != null)
                {
                    Vector3 c = shown.transform.position + Vector3.up;
                    Shoot(tag + "_taxi_a_l_arret", c + shown.transform.right * -8f + shown.transform.forward * 5f + Vector3.up * 2.5f, c);
                }
            }
        }

        private static void Teleport(BalulaGame game, int index)
        {
            List<TrafficPath> paths = game.World.TrafficPaths;
            RaycastVehicle player = game.Director != null ? game.Director.Player : null;
            Vector3 from = player != null ? player.transform.position : Vector3.zero;
            // Longues voies à bonne distance du point actuel (une par appel)
            List<TrafficPath> candidates = new List<TrafficPath>();
            foreach (TrafficPath p in paths)
                if (p != null && p.Length > 600f && p.Points.Count > 3) candidates.Add(p);
            if (candidates.Count == 0) { report.AppendLine("Aucune longue voie pour changer de quartier."); return; }
            candidates.Sort((a, b) => b.Length.CompareTo(a.Length));
            TrafficPath chosen = candidates[Mathf.Min(candidates.Count - 1, 2 + index * 5)];
            float s = chosen.Length * (0.3f + 0.2f * index);
            Vector3 pos = chosen.PositionAt(s);
            Vector3 ahead = chosen.PositionAt(s + 6f) - pos;
            ahead.y = 0f;
            if (ahead.sqrMagnitude < 0.01f) ahead = Vector3.forward;
            game.Director.SpawnPlayer(pos + Vector3.up * 0.3f, Quaternion.LookRotation(ahead.normalized, Vector3.up));
            report.AppendLine("Déplacement vers « " + chosen.Name + " » (" + chosen.Length.ToString("0") + " m) à " + pos.ToString("F0") +
                              ", distance " + Vector3.Distance(from, pos).ToString("0") + " m");
        }

        /// <summary>Moto du joueur (moto centrale du jeu) vue de près.</summary>
        private static void ShootPlayerMoto(BalulaGame game)
        {
            RaycastVehicle pl = game.Director != null ? game.Director.Player : null;
            if (pl == null) return;
            Transform t = pl.transform;
            Vector3 c = t.position + Vector3.up * 0.65f;
            Shoot("moto_centrale_trois_quarts", c + t.forward * 2.6f + t.right * 1.9f + Vector3.up * 0.35f, c);
            Shoot("moto_centrale_profil", c + t.right * 3.2f + Vector3.up * 0.1f, c);
            Shoot("moto_centrale_arriere", c - t.forward * 2.8f - t.right * 1.4f + Vector3.up * 0.5f, c);
            report.AppendLine("Moto du joueur : " + (game.Session != null && game.Session.ActiveDef != null ? game.Session.ActiveDef.nom + " (modèle " + game.Session.ActiveDef.modele3d + ")" : "?"));
        }

        /// <summary>Le joueur est posé sur la rue dont le nom contient le mot donné (la plus longue).</summary>
        private static void TeleportToRoadNamed(BalulaGame game, string key)
        {
            report.AppendLine();
            ImportedWorld map = Compat.FindFirst<ImportedWorld>();
            if (map == null || game.Director == null) { report.AppendLine("=== " + key + " : pas de carte ==="); return; }
            ImportedRoad best = null;
            float bestLen = 0f;
            foreach (ImportedRoad r in map.roads)
            {
                if (r.rank < 3 || string.IsNullOrEmpty(r.name) || !TextUtil.Normalize(r.name).Contains(key)) continue;
                float len = 0f;
                for (int i = 0; i < r.pts.Count - 1; i++) len += Vector2.Distance(r.pts[i], r.pts[i + 1]);
                if (len > bestLen) { bestLen = len; best = r; }
            }
            if (best == null) { report.AppendLine("=== " + key + " : rue introuvable dans la carte ==="); return; }
            int k = best.pts.Count / 2;
            Vector2 a = best.pts[Mathf.Max(0, k - 1)], b = best.pts[Mathf.Min(best.pts.Count - 1, k + 1)];
            Vector2 d = (b - a).normalized;
            Vector2 p = best.pts[k] + new Vector2(d.y, -d.x) * Mathf.Min(3.2f, best.width * 0.3f);
            game.Director.SpawnPlayer(new Vector3(p.x, 0.3f, p.y), Quaternion.LookRotation(new Vector3(d.x, 0f, d.y), Vector3.up));
            report.AppendLine("=== " + best.name + " (" + bestLen.ToString("0") + " m, largeur " + best.width.ToString("0.0") + " m, rang " + best.rank + ") : joueur posé au milieu ===");
        }

        private static Vector3 greenInner, greenOuter, greenAlong;
        private static bool greenFound;

        /// <summary>Le joueur est posé sur le grand boulevard Lumumba, au droit du milieu de l'espace vert.</summary>
        private static void TeleportToGreenStrip(BalulaGame game)
        {
            report.AppendLine();
            CityFill cf = CityFill.Instance;
            greenFound = cf != null && cf.GreenPieces > 0 && cf.GreenSample(cf.GreenPieces / 2, out greenInner, out greenOuter, out greenAlong);
            if (!greenFound || game.Director == null)
            {
                report.AppendLine("=== Espace vert de Lumumba : aucune bande trouvée entre le grand et le petit boulevard (" + (cf != null ? cf.GreenPieces : -1) + " morceaux) ===");
                return;
            }
            Vector3 across = (greenOuter - greenInner).normalized;
            Vector3 pos = greenInner - across * 2.5f + Vector3.up * 0.3f;
            game.Director.SpawnPlayer(pos, Quaternion.LookRotation(greenAlong, Vector3.up));
            report.AppendLine("=== Espace vert de Lumumba : " + cf.GreenPieces + " morceaux (" + (cf.GreenLength / 1000f).ToString("0.00") + " km) ; largeur ici " +
                              Vector3.Distance(greenInner, greenOuter).ToString("0.0") + " m ; joueur sur le grand boulevard ===");
        }

        private static void ShootGreenStrip(BalulaGame game)
        {
            if (!greenFound) return;
            Vector3 across = (greenOuter - greenInner).normalized;
            Vector3 mid = (greenInner + greenOuter) * 0.5f;
            Shoot("lumumba_espace_vert_depuis_le_boulevard", greenInner - across * 4f - greenAlong * 10f + Vector3.up * 2.2f, mid + greenAlong * 6f + Vector3.up * 0.8f);
            Shoot("lumumba_espace_vert_vu_d_en_haut", mid - greenAlong * 16f + Vector3.up * 14f, mid + greenAlong * 6f);
            Shoot("lumumba_cloture_fer_noir", greenInner + across * 1.0f - greenAlong * 3f + Vector3.up * 1.5f, greenInner + across * 2.2f + greenAlong * 3f + Vector3.up * 0.8f);
        }

        /// <summary>Rue de terre (non goudronnée) assez longue, proche du joueur.</summary>
        private static void TeleportToDirtStreet(BalulaGame game)
        {
            report.AppendLine();
            ImportedWorld map = Compat.FindFirst<ImportedWorld>();
            if (map == null || game.Director == null || game.Director.Player == null) return;
            Vector3 from = game.Director.Player.transform.position;
            ImportedRoad best = null;
            float bd = float.MaxValue;
            foreach (ImportedRoad r in map.roads)
            {
                if (r.rank != 3 || r.surface == (int)SurfaceKind.Goudron || r.surface == (int)SurfaceKind.GoudronDegrade || r.pts.Count < 3) continue;
                float len = 0f;
                for (int i = 0; i < r.pts.Count - 1; i++) len += Vector2.Distance(r.pts[i], r.pts[i + 1]);
                if (len < 220f) continue;
                Vector2 m = r.pts[r.pts.Count / 2];
                float dd = (new Vector2(from.x, from.z) - m).sqrMagnitude;
                if (dd < bd) { bd = dd; best = r; }
            }
            if (best == null) { report.AppendLine("=== quartier : aucune rue de terre ==="); return; }
            int k = best.pts.Count / 2;
            Vector2 d = (best.pts[k + 1] - best.pts[k - 1]).normalized;
            Vector2 p = best.pts[k] + new Vector2(d.y, -d.x) * 1.2f;
            game.Director.SpawnPlayer(new Vector3(p.x, 0.3f, p.y), Quaternion.LookRotation(new Vector3(d.x, 0f, d.y), Vector3.up));
            report.AppendLine("=== Quartier : rue de terre « " + best.name + " » (surface " + (SurfaceKind)best.surface + ") ===");
        }

        /// <summary>Photos des modèles posés autour du joueur : maisons, immeubles, barrières, étals, parkings, voitures, motos.</summary>
        private static void ShootModels(BalulaGame game, string tag)
        {
            RaycastVehicle pl = game.Director != null ? game.Director.Player : null;
            if (pl == null) return;
            Vector3 pp = pl.transform.position;
            CityFill cf = CityFill.Instance;
            if (cf != null)
                report.AppendLine("   Ville : " + cf.ModelHouses + " maisons de modèle, " + cf.Towers + " immeubles, " + cf.Barriers + " barrières, " + cf.Stalls + " étals, " +
                                  cf.ParkedVehicles + " véhicules garés (cases chargées : " + cf.LoadedCells + ")");
            if (cf != null)
                report.AppendLine("   Boulevards (10 m libres devant les maisons) : " + cf.BoulevardCount + " tronçons ; espace vert de Lumumba : " + cf.GreenPieces + " morceaux, " +
                                  (cf.GreenLength / 1000f).ToString("0.00") + " km");
            if (CrowdSystem.Instance != null) report.AppendLine("   Vendeurs aux étals : " + CrowdSystem.Instance.StallVendors + " ; piétons : " + CrowdSystem.Instance.Count);
            ShootStreet(game, tag);
            HashSet<string> done = new HashSet<string>();
            for (int i = CityFill.RecentSpots.Count - 1; i >= 0; i--)
            {
                CityFill.Spot sp = CityFill.RecentSpots[i];
                if (done.Contains(sp.Kind) || (sp.P - pp).sqrMagnitude > 160f * 160f) continue;
                done.Add(sp.Kind);
                Vector3 f = sp.Face.sqrMagnitude > 0.01f ? sp.Face.normalized : Vector3.forward;
                Vector3 r = Vector3.Cross(Vector3.up, f);
                switch (sp.Kind)
                {
                    case "immeuble": Shoot(tag + "_immeuble", sp.P + f * 30f + r * 12f + Vector3.up * 6f, sp.P + Vector3.up * 14f); break;
                    case "cottage":
                    case "maison": Shoot(tag + "_" + sp.Kind, sp.P + f * 17f + r * 7f + Vector3.up * 5f, sp.P + Vector3.up * 2.2f); break;
                    case "barriere": Shoot(tag + "_barrieres", sp.P + r * 14f + f * 4f + Vector3.up * 2.4f, sp.P + Vector3.up * 0.5f); break;
                    case "etal": Shoot(tag + "_etal", sp.P + f * 4.2f + r * 1.6f + Vector3.up * 1.7f, sp.P + Vector3.up * 0.9f); break;
                    case "parking": Shoot(tag + "_parking", sp.P + f * 11f + r * 5f + Vector3.up * 4.5f, sp.P + Vector3.up * 0.6f); break;
                    case "mobilier": Shoot(tag + "_mobilier", sp.P + f * 4f + r * 2f + Vector3.up * 1.6f, sp.P + Vector3.up * 0.5f); break;
                    case "bande_verte": Shoot(tag + "_espace_vert", sp.P - f * 9f + r * 9f + Vector3.up * 3.2f, sp.P + Vector3.up * 0.9f); break;
                }
            }
            // Voitures génériques et motos des modèles fournis dans la circulation
            if (game.Traffic != null)
            {
                Dictionary<string, int> types = new Dictionary<string, int>();
                bool car = false, moto = false;
                foreach (AIVehicle v in game.Traffic.Vehicles)
                {
                    if (v == null) continue;
                    string k = v.name.StartsWith("PNJ_MotoTaxi_") ? v.name.Substring(4) : v.Type.ToString();
                    int n;
                    types.TryGetValue(k, out n);
                    types[k] = n + 1;
                    Transform t = v.transform;
                    if (!car && VehicleBuilder.IsCarModelType(v.Type) && (t.position - pp).sqrMagnitude < 120f * 120f)
                    {
                        car = true;
                        Shoot(tag + "_voiture_" + v.Type, t.position + t.forward * 5.5f + t.right * 3.5f + Vector3.up * 1.6f, t.position + Vector3.up * 0.7f);
                    }
                    if (!moto && v.name.StartsWith("PNJ_MotoTaxi_") && (t.position - pp).sqrMagnitude < 120f * 120f)
                    {
                        moto = true;
                        Shoot(tag + "_" + v.name.Substring(4), t.position + t.forward * 3f + t.right * 2.2f + Vector3.up * 1.2f, t.position + Vector3.up * 0.8f);
                    }
                }
                StringBuilder sb = new StringBuilder("   Circulation :");
                foreach (KeyValuePair<string, int> kv in types) sb.Append(' ').Append(kv.Key).Append('×').Append(kv.Value);
                report.AppendLine(sb.ToString());
            }
            int walkingPolice = 0;
            foreach (AgentRonde r in Compat.FindAll<AgentRonde>()) if (r != null) walkingPolice++;
            report.AppendLine("   Policiers en ronde : " + walkingPolice + " ; jour de pluie : " + (game.Session != null && WorldEnvironment.RainyDay(game.Session.Profile.jour)));
        }

        private static void DustRun(BalulaGame game, bool on)
        {
            GameplayDirector.TestInput = on ? new DriveInput { Throttle = 1f } : null;
        }

        private static void ShootDust(BalulaGame game)
        {
            RaycastVehicle pl = game.Director != null ? game.Director.Player : null;
            if (pl == null) return;
            Transform t = pl.transform;
            report.AppendLine("   Poussière : surface " + pl.CurrentSurface + ", " + pl.SpeedKmh.ToString("0") + " km/h, particules émises " + (DustFx.Instance != null ? DustFx.Instance.Emitted : -1));
            Shoot("poussiere_derriere_la_moto", t.position - t.forward * 7f + t.right * 2.5f + Vector3.up * 2.2f, t.position + Vector3.up * 0.6f);
        }

        private static void TeleportToWorks(BalulaGame game)
        {
            ImportedWorld map = Compat.FindFirst<ImportedWorld>();
            if (map == null || map.works == null || map.works.Count == 0 || game.Director == null) { report.AppendLine("Aucun chantier dans la carte."); return; }
            Vector3 from = game.Director.Player != null ? game.Director.Player.transform.position : Vector3.zero;
            WorkZone best = null;
            float bd = float.MaxValue;
            foreach (WorkZone z in map.works)
            {
                float d = (z.center - from).sqrMagnitude;
                if (d < bd) { bd = d; best = z; }
            }
            Vector3 dir = new Vector3(best.dir.x, 0f, best.dir.z).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, dir);
            Vector3 pos = best.center - dir * (best.length * 0.5f + 16f) + right * Mathf.Min(1.6f, best.width * 0.25f);
            game.Director.SpawnPlayer(new Vector3(pos.x, 0.3f, pos.z), Quaternion.LookRotation(dir, Vector3.up));
            report.AppendLine("Déplacement vers le chantier de « " + best.road + " » (" + best.length.ToString("0") + " m, largeur " + best.width.ToString("0.0") + " m) ; chantiers dans la carte : " + map.works.Count);
        }

        // ------------------------------------------------------------------
        // Séries de photos
        // ------------------------------------------------------------------

        private static void ShootAround(BalulaGame game, string tag)
        {
            RaycastVehicle player = game.Director != null ? game.Director.Player : null;
            if (player == null) { report.AppendLine("Pas de véhicule joueur."); return; }
            Transform t = player.transform;
            Vector3 p = t.position;
            report.AppendLine();
            report.AppendLine("=== " + tag + " : joueur à " + p.ToString("F1") + " ===");
            Counts(p);
            ShootMain(tag + "_camera_poursuite");
            Shoot(tag + "_chauffeur_cote", p + t.right * 2.6f + Vector3.up * 1.45f + t.forward * 0.3f, p + Vector3.up * 1.15f);
            Shoot(tag + "_chauffeur_face", p + t.forward * 3.6f + Vector3.up * 1.6f, p + Vector3.up * 1.15f);
            Describe("Véhicule du joueur et chauffeur", player.gameObject, t.position + t.right * 2.6f);

            // Un tricycle de la circulation (Bajaj RE de MATULA DESIGN) arrêté sur la voie la plus proche : face, trois-quarts, côté, arrière
            if (TrafficManager.Instance != null)
            {
                TrafficPath busPath = null;
                float busS = 0f, busD = float.MaxValue;
                foreach (TrafficPath tp in game.World.TrafficPaths)
                {
                    if (tp == null || tp.Points.Count < 2) continue;
                    float acc = 0f;
                    for (int i = 0; i < tp.Points.Count - 1; i++)
                    {
                        float dd = (tp.Points[i] - p).sqrMagnitude;
                        if (dd < busD && dd > 400f) { busD = dd; busPath = tp; busS = acc; }
                        acc += Vector3.Distance(tp.Points[i], tp.Points[i + 1]);
                    }
                }
                if (busPath != null)
                {
                    AIVehicle tri = TrafficManager.Instance.Spawn(AIVehicleType.TricycleTaxi, busPath, busS, 0f);
                    tri.Special = true;
                    Transform bt = tri.transform;
                    report.AppendLine("Tricycle de la circulation ajouté pour la photo à " + bt.position.ToString("F0") + " — modèle du tricycle : " + (VehicleBuilder.BajajModel != null) + ", roues : " + tri.Wheels.Count + ", longueur " + tri.Length.ToString("0.0") + " m (photos après installation des passagers)");
                    photoTri = tri;
                    photoTriTag = tag;
                }
            }
            // Circulation : le plus proche de chaque type
            List<AIVehicle> cars = new List<AIVehicle>();
            if (TrafficManager.Instance != null) foreach (AIVehicle v in TrafficManager.Instance.Vehicles) if (v != null) cars.Add(v);
            cars.Sort((a, b) => (a.transform.position - p).sqrMagnitude.CompareTo((b.transform.position - p).sqrMagnitude));
            HashSet<AIVehicleType> seen = new HashSet<AIVehicleType>();
            int carShots = 0;
            foreach (AIVehicle v in cars)
            {
                if (carShots >= 8) break;
                if (seen.Contains(v.Type)) continue;
                seen.Add(v.Type);
                carShots++;
                Transform vt = v.transform;
                Vector3 c = vt.position + Vector3.up * 1.1f;
                Shoot(tag + "_circulation_" + v.Type, c + vt.right * 7f - vt.forward * 3f + Vector3.up * 1.4f, c);
                Describe("Circulation " + v.Type + " (" + Vector3.Distance(vt.position, p).ToString("0") + " m du joueur)", v.gameObject, c + vt.right * 7f);
            }

            // Piétons et clients (hors véhicules)
            List<HumanAvatar> people = new List<HumanAvatar>();
            foreach (HumanAvatar h in Compat.FindAll<HumanAvatar>())
            {
                if (h == null || !h.gameObject.activeInHierarchy) continue;
                if (h.GetComponentInParent<RaycastVehicle>() != null || h.GetComponentInParent<AIVehicle>() != null) continue;
                if ((h.transform.position - GarageScene.Origin).sqrMagnitude < 2500f) continue;
                people.Add(h);
            }
            people.Sort((a, b) => (a.transform.position - p).sqrMagnitude.CompareTo((b.transform.position - p).sqrMagnitude));
            report.AppendLine("Piétons hors véhicules : " + people.Count);
            for (int i = 0; i < people.Count && i < 5; i++)
            {
                Transform ht = people[i].transform;
                Vector3 c = ht.position + Vector3.up * 1.0f;
                Shoot(tag + "_pieton" + (i + 1), c + ht.forward * 3.2f + Vector3.up * 0.55f, c);
                Describe("Piéton " + people[i].AvatarId + " (" + Vector3.Distance(ht.position, p).ToString("0") + " m du joueur)", people[i].gameObject, c + ht.forward * 3.2f);
            }

            // Station-service la plus proche
            InteractionPoint best = null;
            float bestD = float.MaxValue;
            foreach (InteractionPoint ip in InteractionPoint.All)
            {
                if (ip == null || ip.Kind != InteractionKind.StationService) continue;
                float d = (ip.transform.position - p).sqrMagnitude;
                if (d < bestD) { bestD = d; best = ip; }
            }
            if (best != null)
            {
                Vector3 s = best.transform.position;
                Shoot(tag + "_station_service", s + new Vector3(20f, 8f, -20f), s + Vector3.up * 2f);
                Shoot(tag + "_station_pompes", s + Vector3.up * 1.7f + new Vector3(9f, 0f, 9f), s + Vector3.up * 1.2f);
                report.AppendLine("Station-service « " + best.Label + " » à " + Mathf.Sqrt(bestD).ToString("0") + " m");
            }

            ShootStreet(game, tag);
        }

        private static void ShootStreet(BalulaGame game, string tag)
        {
            RaycastVehicle player = game.Director != null ? game.Director.Player : null;
            if (player == null) return;
            Transform t = player.transform;
            Vector3 p = t.position;
            if (tag != "depart")
            {
                report.AppendLine();
                report.AppendLine("=== " + tag + " : joueur à " + p.ToString("F1") + " ===");
                Counts(p);
                ShootMain(tag + "_camera_poursuite");
            }
            Vector3 f = t.forward;
            f.y = 0f;
            f.Normalize();
            Shoot(tag + "_rue_hauteur_yeux", p + Vector3.up * 1.7f - f * 2f, p + f * 60f + Vector3.up * 1.2f);
            Shoot(tag + "_rue_derriere", p + Vector3.up * 1.7f + f * 3f, p - f * 60f + Vector3.up * 1.2f);
            Shoot(tag + "_vue_oblique", p - f * 45f + t.right * 25f + Vector3.up * 32f, p + f * 15f);
            Shoot(tag + "_vue_aerienne", p + Vector3.up * 160f - f * 70f, p + f * 20f);
        }

        // ------------------------------------------------------------------
        // Rapport
        // ------------------------------------------------------------------

        private static void Header()
        {
            report.AppendLine("BALULA MOTO — photos de contrôle " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm") + " UTC");
            report.AppendLine("Unity " + Application.unityVersion + " — cible active : " + EditorUserBuildSettings.activeBuildTarget);
            report.AppendLine("Carte graphique : " + SystemInfo.graphicsDeviceName + " / " + SystemInfo.graphicsDeviceType + " / " + SystemInfo.graphicsDeviceVersion);
            report.AppendLine("Compute shaders : " + SystemInfo.supportsComputeShaders + " — instanciation : " + SystemInfo.supportsInstancing);
            int q = QualitySettings.GetQualityLevel();
            report.AppendLine("Qualité : " + (q >= 0 && q < QualitySettings.names.Length ? QualitySettings.names[q] : q.ToString()) +
                              " — lodBias " + QualitySettings.lodBias + " — maximumLODLevel " + QualitySettings.maximumLODLevel +
                              " — skinWeights " + QualitySettings.skinWeights);
            report.AppendLine("Android : API graphiques " + string.Join(", ", Array.ConvertAll(PlayerSettings.GetGraphicsAPIs(BuildTarget.Android), a => a.ToString())) +
                              " (par défaut : " + PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android) + ")");
            PropertyInfo md = typeof(PlayerSettings).GetProperty("meshDeformation", BindingFlags.Public | BindingFlags.Static);
            report.AppendLine("Déformation des maillages (skinning) : " + (md != null ? md.GetValue(null, null).ToString() : "?"));
            report.AppendLine("Avatars disponibles : " + HumanLibrary.Available + " (hommes " + HumanLibrary.Load("Hommes").Length + ", femmes " + HumanLibrary.Load("Femmes").Length + ")");
            report.AppendLine("Musique : " + (MusicPlayer.Instance != null ? (MusicPlayer.Instance.NowPlaying ?? "(pas encore)") : "lecteur absent") + " — volume réglé " + GameSettings.MusicVolume);
            int huge = 0;
            foreach (Renderer r in Compat.FindAll<Renderer>())
            {
                Bounds b = r.bounds;
                bool bad = float.IsNaN(b.center.x) || float.IsNaN(b.extents.x) || float.IsInfinity(b.extents.x);
                if (!bad && b.size.x < 1200f && b.size.z < 1200f) continue;
                if (++huge > 25) continue;
                Material m = r.sharedMaterial;
                report.AppendLine("   Grand objet : " + r.name + " (" + r.GetType().Name + ") centre " + b.center.ToString("F0") + " taille " + b.size.ToString("F0") +
                                  (m != null ? " — " + m.name + " couleur " + m.color : ""));
            }
            report.AppendLine("Objets de plus de 1,2 km ou à boîte invalide : " + huge);
        }

        private static void Counts(Vector3 p)
        {
            int cars = TrafficManager.Instance != null ? TrafficManager.Instance.Vehicles.Count : -1;
            int crowd = CrowdSystem.Instance != null ? CrowdSystem.Instance.Count : -1;
            int avatars = 0, smrs = 0, near = 0;
            foreach (HumanAvatar h in Compat.FindAll<HumanAvatar>())
            {
                avatars++;
                if ((h.transform.position - p).sqrMagnitude < 80f * 80f) near++;
            }
            foreach (SkinnedMeshRenderer s in Compat.FindAll<SkinnedMeshRenderer>()) if (s.enabled && s.gameObject.activeInHierarchy) smrs++;
            report.AppendLine("Circulation : " + cars + " véhicules — foule : " + crowd + " — avatars : " + avatars + " (dont " + near +
                              " à moins de 80 m) — maillages animés actifs : " + smrs + " — stations : " + CountStations() + " — arrêts : " + ClientStop.All.Count);
        }

        private static int CountStations()
        {
            int n = 0;
            foreach (InteractionPoint ip in InteractionPoint.All) if (ip != null && ip.Kind == InteractionKind.StationService) n++;
            return n;
        }

        private static void Describe(string label, GameObject go, Vector3 viewer)
        {
            report.AppendLine("— " + label + " : " + go.name + " à " + go.transform.position.ToString("F2") + " échelle " + go.transform.lossyScale.ToString("F2"));
            RaycastHit hit;
            Vector3 o = go.transform.position + Vector3.up * 4f;
            if (Physics.Raycast(o, Vector3.down, out hit, 12f, ~0, QueryTriggerInteraction.Ignore))
                report.AppendLine("   sol sous l'objet : y=" + hit.point.y.ToString("F2") + " (" + hit.collider.name + "), écart " + (go.transform.position.y - hit.point.y).ToString("F2") + " m");
            foreach (LODGroup lg in go.GetComponentsInChildren<LODGroup>(true))
            {
                LOD[] lods = lg.GetLODs();
                float dist = Vector3.Distance(viewer, lg.transform.TransformPoint(lg.localReferencePoint));
                float scale = Mathf.Abs(lg.transform.lossyScale.y);
                float rel = lg.size * scale / (2f * Mathf.Max(0.01f, dist) * Mathf.Tan(31f * Mathf.Deg2Rad)) * QualitySettings.lodBias;
                StringBuilder th = new StringBuilder();
                foreach (LOD l in lods) th.Append(l.screenRelativeTransitionHeight.ToString("0.###")).Append(' ');
                report.AppendLine("   LODGroup " + lg.name + " : taille " + lg.size.ToString("F2") + ", seuils " + th + "— hauteur relative estimée " + rel.ToString("0.###") +
                                  " à " + dist.ToString("0.0") + " m, actif " + lg.enabled);
            }
            int shown = 0, total = 0;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                total++;
                if (r.isVisible) shown++;
                if (total > 40) continue;
                Material m = r.sharedMaterial;
                string mat = m != null ? m.name + " [" + (m.shader != null ? m.shader.name : "sans shader") + (m.enableInstancing ? ", instancié" : "") +
                                         ", file " + m.renderQueue + "]" : "AUCUN MATÉRIAU";
                string extra = "";
                SkinnedMeshRenderer smr = r as SkinnedMeshRenderer;
                if (smr != null)
                {
                    extra = " | maillage " + (smr.sharedMesh != null ? smr.sharedMesh.name + " " + smr.sharedMesh.vertexCount + " sommets" : "ABSENT") +
                            ", os " + (smr.bones != null ? smr.bones.Length : 0) + ", racine " + (smr.rootBone != null ? smr.rootBone.name : "aucune") +
                            ", boîte " + smr.localBounds.size.ToString("F1") + ", qualité " + smr.quality + ", horsEcran " + smr.updateWhenOffscreen;
                }
                else
                {
                    MeshFilter mf = r.GetComponent<MeshFilter>();
                    if (mf != null) extra = " | maillage " + (mf.sharedMesh != null ? mf.sharedMesh.name + " " + mf.sharedMesh.vertexCount + " sommets" : "ABSENT");
                }
                report.AppendLine("   " + (r is SkinnedMeshRenderer ? "[animé] " : "") + r.name + " actif=" + (r.enabled && r.gameObject.activeInHierarchy) +
                                  " visible=" + r.isVisible + " boîte " + r.bounds.center.ToString("F1") + " ± " + r.bounds.extents.ToString("F1") + " " + mat + extra);
            }
            report.AppendLine("   rendus visibles : " + shown + " / " + total);
        }

        // ------------------------------------------------------------------
        // Prise de vue
        // ------------------------------------------------------------------

        private static Camera SnapCamera()
        {
            if (snap == null)
            {
                GameObject g = new GameObject("CameraPhotosCI");
                snap = g.AddComponent<Camera>();
            }
            Camera main = Camera.main;
            if (main != null) snap.CopyFrom(main);
            snap.enabled = false;
            return snap;
        }

        private static void ShootMain(string name)
        {
            Camera cam = SnapCamera();
            Render(name, cam);
        }

        private static void Shoot(string name, Vector3 pos, Vector3 look)
        {
            Camera cam = SnapCamera();
            cam.transform.position = pos;
            Vector3 d = look - pos;
            cam.transform.rotation = d.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(d.normalized, Vector3.up) : Quaternion.identity;
            Render(name, cam);
        }

        private static RenderMode uiPrevMode;
        private static bool uiPrepared;

        /// <summary>Étape 1 d'une photo avec l'interface : le canevas passe en mode caméra sur la caméra photo,
        /// activée et dessinant dans la texture, pour que l'interface soit dans l'image.</summary>
        private static void PrepareUI()
        {
            Canvas c = UIManager.Instance != null ? UIManager.Instance.Canvas : null;
            if (c == null) { uiPrepared = false; return; }
            Camera cam = SnapCamera();
            EnsureTarget();
            cam.targetTexture = rt;
            cam.aspect = (float)W / H;
            cam.cullingMask |= (1 << 5) | (1 << c.gameObject.layer);
            cam.enabled = true;
            uiPrevMode = c.renderMode;
            c.renderMode = RenderMode.ScreenSpaceCamera;
            c.worldCamera = cam;
            c.planeDistance = cam.nearClipPlane + 0.6f;
            uiPrepared = true;
        }

        /// <summary>Étape 2 (quelques images plus tard) : photo avec l'interface, puis le canevas revient en surimpression.</summary>
        private static void ShootUI(string name)
        {
            Canvas c = UIManager.Instance != null ? UIManager.Instance.Canvas : null;
            if (!uiPrepared || snap == null || c == null) { report.AppendLine("   (interface non préparée : photo sans interface)"); ShootMain(name); return; }
            Canvas.ForceUpdateCanvases();
            Render(name, snap);
            snap.enabled = false;
            c.renderMode = uiPrevMode;
            c.worldCamera = null;
            uiPrepared = false;
        }

        private static void EnsureTarget()
        {
            if (rt == null)
            {
                rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
                rt.antiAliasing = 1;
                rt.Create();
            }
            if (tex == null) tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        }

        private static void Render(string name, Camera cam)
        {
            EnsureTarget();
            cam.targetTexture = rt;
            cam.aspect = (float)W / H;
            cam.Render();
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = null;
            shotIndex++;
            string file = Path.Combine(SessionState.GetString(KeyDir, "."), shotIndex.ToString("00") + "_" + Safe(name) + ".png");
            File.WriteAllBytes(file, tex.EncodeToPNG());
            report.AppendLine("Photo " + Path.GetFileName(file) + " — caméra " + cam.transform.position.ToString("F1"));
        }

        private static string Safe(string s)
        {
            StringBuilder b = new StringBuilder();
            foreach (char c in s) b.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_');
            return b.ToString();
        }

        private static void Finish(int code)
        {
            if (finished) return;
            finished = true;
            SessionState.SetBool(KeyActive, false);
            EditorApplication.update -= Tick;
            string text = report.ToString();
            try { File.WriteAllText(Path.Combine(SessionState.GetString(KeyDir, "."), "rapport.txt"), text); }
            catch (Exception e) { Debug.LogException(e); }
            Debug.Log("[BALULA] Rapport des photos :\n" + text);
            EditorApplication.Exit(code);
        }
    }
}
#endif
