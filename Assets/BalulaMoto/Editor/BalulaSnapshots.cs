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
        private const double TimeoutSeconds = 1800.0;
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
                return true;
            });
        }

        private static void Plan(BalulaGame game)
        {
            steps.Clear();
            steps.Add(() => { Header(); ShootMain("menu_garage"); return true; });
            steps.Add(() => { StartGame(game); return true; });
            Wait(25f, 60);
            steps.Add(() => { ShootAround(game, "depart"); return true; });
            // Deux autres quartiers (grandes avenues) : la ville pleine se reconstruit autour du joueur
            for (int k = 0; k < 2; k++)
            {
                int index = k;
                steps.Add(() => { Teleport(game, index); return true; });
                Wait(14f, 40);
                steps.Add(() => { ShootStreet(game, "quartier" + (index + 2)); return true; });
            }
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

            // Circulation : le plus proche de chaque type
            List<AIVehicle> cars = new List<AIVehicle>();
            if (TrafficManager.Instance != null) foreach (AIVehicle v in TrafficManager.Instance.Vehicles) if (v != null) cars.Add(v);
            cars.Sort((a, b) => (a.transform.position - p).sqrMagnitude.CompareTo((b.transform.position - p).sqrMagnitude));
            HashSet<AIVehicleType> seen = new HashSet<AIVehicleType>();
            int carShots = 0;
            foreach (AIVehicle v in cars)
            {
                if (carShots >= 7) break;
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

        private static void Render(string name, Camera cam)
        {
            if (rt == null)
            {
                rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
                rt.antiAliasing = 1;
                rt.Create();
            }
            if (tex == null) tex = new Texture2D(W, H, TextureFormat.RGB24, false);
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
