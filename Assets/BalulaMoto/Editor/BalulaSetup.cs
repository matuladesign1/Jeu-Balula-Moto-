// BALULA MOTO — outils de l'éditeur : préparation du projet (scène, matériaux, icône, réglages Android)
// et construction de l'APK en un clic.
#if UNITY_EDITOR
using System.IO;
using BalulaMoto.Core;
using BalulaMoto.Runtime;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace BalulaMoto.EditorTools
{
    public static class BalulaSetup
    {
        public const string Root = "Assets/BalulaMoto";
        public const string ScenePath = Root + "/Scenes/BalulaMoto.unity";
        public const string IconPath = Root + "/Art/IconeApplication_1024.png";
        public const string SplashPath = Root + "/Art/Ecran_Demarrage_1920x1080.png";
        public const string MaterialsFolder = Root + "/Resources/BalulaMoto";
        public const string BundleId = "com.matuladesign.balulamoto";

        [MenuItem("BALULA MOTO/1. Préparer le projet", priority = 1)]
        public static void Prepare()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string report = PrepareInternal();
            EditorUtility.DisplayDialog("BALULA MOTO", "Projet prêt.\n\n" + report + "\n\nOuvrez la scène « BalulaMoto » et appuyez sur Lecture (Play).", "OK");
        }

        public static string PrepareInternal()
        {
            CreateMaterials();
            string humans = HumanBuilder.BuildAll(false);
            string scene = CreateOrUpdateScene();
            ConfigurePlayer();
            string icon = ConfigureIcon();
            string data = ValidateData();
            AssetDatabase.SaveAssets();
            return scene + "\n" + icon + "\n" + data + "\n" + humans;
        }

        /// <summary>Crée un dossier d'assets (et ses parents) connu de l'AssetDatabase.</summary>
        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // ------------------------------------------------------------------
        // Matériaux de base : garantissent l'inclusion des shaders (et de la variante émissive) dans le build
        // ------------------------------------------------------------------
        private static void CreateMaterials()
        {
            EnsureFolder(MaterialsFolder);
            bool urp = GraphicsSettings.currentRenderPipeline != null;
            Shader lit = Shader.Find(urp ? "Universal Render Pipeline/Lit" : "Standard");
            Shader unlit = Shader.Find(urp ? "Universal Render Pipeline/Unlit" : "Unlit/Color");
            if (lit == null) lit = Shader.Find("Standard");
            if (unlit == null) unlit = Shader.Find("Unlit/Color");
            SaveMaterial("MatLit", lit, false);
            SaveMaterial("MatUnlit", unlit, false);
            SaveMaterial("MatEmissive", lit, true);
            SaveGlass(lit, urp);
        }

        /// <summary>Vitre teintée transparente (véhicules) : garantit l'inclusion de la variante transparente du shader.</summary>
        private static void SaveGlass(Shader shader, bool urp)
        {
            if (shader == null) return;
            string path = MaterialsFolder + "/MatGlass.mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            else m.shader = shader;
            Color c = new Color(0.08f, 0.11f, 0.14f, 0.5f);
            m.color = c;
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_ZWrite", 0);
            m.renderQueue = (int)RenderQueue.Transparent;
            if (urp)
            {
                m.SetColor("_BaseColor", c);
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetFloat("_Smoothness", 0.95f);
                m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            else
            {
                m.SetFloat("_Mode", 3f);
                m.SetFloat("_Glossiness", 0.95f);
                m.SetFloat("_Metallic", 0.1f);
                m.SetInt("_SrcBlend", (int)BlendMode.One);
                m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                m.DisableKeyword("_ALPHATEST_ON");
                m.DisableKeyword("_ALPHABLEND_ON");
                m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            }
            EditorUtility.SetDirty(m);
        }

        private static void SaveMaterial(string name, Shader shader, bool emissive)
        {
            if (shader == null) return;
            string path = MaterialsFolder + "/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            else m.shader = shader;
            m.color = Color.white;
            if (emissive)
            {
                m.EnableKeyword("_EMISSION");
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.white * 2f);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
        }

        // ------------------------------------------------------------------
        // Scène de jeu (jamais écrasée : une carte importée est conservée)
        // ------------------------------------------------------------------
        private static string CreateOrUpdateScene()
        {
            EnsureFolder(Root + "/Scenes");
            Scene scene;
            bool created = false;
            if (File.Exists(ScenePath)) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            else
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                created = true;
            }
            if (Compat.FindFirst<BalulaGame>() == null) new GameObject("BALULA MOTO").AddComponent<BalulaGame>();
            // Brouillard et lumière ambiante : activés dans la scène pour que les variantes de shader soient conservées au build
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.002f;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.skybox = null;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            return created ? "Scène créée : " + ScenePath : "Scène mise à jour : " + ScenePath;
        }

        // ------------------------------------------------------------------
        // Réglages du lecteur : nom, identifiant, orientation paysage, Android 64 bits
        // ------------------------------------------------------------------
        private static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "MATULA DESIGN";
            PlayerSettings.productName = "BALULA MOTO";
            if (string.IsNullOrEmpty(PlayerSettings.bundleVersion) || PlayerSettings.bundleVersion == "0.1" || PlayerSettings.bundleVersion == "1.0")
                PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, BundleId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
            PlayerSettings.SplashScreen.backgroundColor = new Color(0.98f, 0f, 0f);
        }

        private static string ConfigureIcon()
        {
            Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon == null) return "Icône introuvable : " + IconPath;
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            TextureImporter ti = AssetImporter.GetAtPath(SplashPath) as TextureImporter;
            if (ti != null && ti.textureType != TextureImporterType.Sprite)
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.SaveAndReimport();
            }
            Sprite splash = AssetDatabase.LoadAssetAtPath<Sprite>(SplashPath);
            if (splash != null) PlayerSettings.SplashScreen.logos = new[] { PlayerSettings.SplashScreenLogo.Create(2.5f, splash) };
            return "Icône de l'application : logo BALULA MOTO (REF-APP-001).";
        }

        [MenuItem("BALULA MOTO/3. Vérifier les données", priority = 3)]
        public static void ValidateDataMenu()
        {
            EditorUtility.DisplayDialog("BALULA MOTO — données", ValidateData(), "OK");
        }

        private static string ValidateData()
        {
            System.Collections.Generic.List<string> report = new System.Collections.Generic.List<string>();
            GameDatabase db = DataLoader.LoadAll(report);
            foreach (string r in report) Debug.Log("[BALULA] " + r);
            return "Données : " + db.Vehicles.Count + " véhicules, " + db.Locations.Count + " lieux, " + db.Provinces.Count + " provinces, " +
                   db.Missions.Count + " missions, " + db.References.Count + " références photo. " +
                   (report.Count == 0 ? "Aucun problème." : report.Count + " remarque(s) dans la Console.");
        }

        // ------------------------------------------------------------------
        // APK Android
        // ------------------------------------------------------------------
        [MenuItem("BALULA MOTO/2. Construire l'APK Android", priority = 2)]
        public static void BuildApkMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                if (!EditorUtility.DisplayDialog("BALULA MOTO", "La plateforme active n'est pas Android. Basculer vers Android ? (quelques minutes la première fois)", "Basculer", "Annuler")) return;
                if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
                {
                    EditorUtility.DisplayDialog("BALULA MOTO", "Impossible de passer sur Android : installez le module « Android Build Support » (avec OpenJDK et Android SDK & NDK) depuis Unity Hub > Installs > Add modules.", "OK");
                    return;
                }
            }
            string path = BuildApk(null);
            if (path != null)
            {
                EditorUtility.RevealInFinder(path);
                EditorUtility.DisplayDialog("BALULA MOTO", "APK créé :\n" + path + "\n\nCopiez-le sur le téléphone et installez-le (autoriser les « sources inconnues »).", "OK");
            }
            else EditorUtility.DisplayDialog("BALULA MOTO", "La construction a échoué : consultez la Console (Window > General > Console) et envoyez-moi les messages en rouge.", "OK");
        }

        /// <summary>Construit l'APK. Retourne le chemin, ou null en cas d'échec.</summary>
        public static string BuildApk(string outputPath)
        {
            PrepareInternal();
            if (string.IsNullOrEmpty(outputPath))
                outputPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Builds", "BalulaMoto.apk");
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            EditorUserBuildSettings.buildAppBundle = false;
            BuildPlayerOptions o = new BuildPlayerOptions();
            o.scenes = new[] { ScenePath };
            o.locationPathName = outputPath;
            o.target = BuildTarget.Android;
            o.targetGroup = BuildTargetGroup.Android;
            o.options = BuildOptions.None;
            BuildReport report = BuildPipeline.BuildPlayer(o);
            Debug.Log("[BALULA] Build " + report.summary.result + " — " + report.summary.totalSize / (1024UL * 1024UL) + " Mo — " + outputPath);
            return report.summary.result == BuildResult.Succeeded ? outputPath : null;
        }

        /// <summary>
        /// Point d'entrée pour une compilation automatique (ligne de commande / GitHub Actions).
        /// Options : -customBuildPath (APK), -balulaOsm (fichier .osm à importer), -balulaOsmLat/-balulaOsmLon (origine),
        /// -balulaOsmRadius (m), -balulaOsmBuildings (nombre max de bâtiments).
        /// </summary>
        public static void BuildAndroidCI()
        {
            int code = 1;
            try
            {
                string output = Arg("-customBuildPath") ?? Arg("-balulaOutput");
                if (!string.IsNullOrEmpty(output) && !output.EndsWith(".apk")) output = Path.Combine(output, "BalulaMoto.apk");
                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                    EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
                Debug.Log("[BALULA] " + PrepareInternal());
                ImportOsmForBuild();
                string result = BuildApk(output);
                code = result != null ? 0 : 1;
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                code = 1;
            }
            EditorApplication.Exit(code);
        }

        private static string Arg(string name)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }

        /// <summary>Importe la vraie carte de Kinshasa (OpenStreetMap) dans la scène avant la compilation, si fournie.</summary>
        private static void ImportOsmForBuild()
        {
            string osm = Arg("-balulaOsm");
            if (string.IsNullOrEmpty(osm)) return;
            if (!File.Exists(osm)) { Debug.LogWarning("[BALULA] Carte OSM introuvable : " + osm + " — la piste d'essai sera utilisée."); return; }
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (Compat.FindFirst<ImportedWorld>() != null) { Debug.Log("[BALULA] La scène contient déjà une carte importée : conservée."); return; }
            double lat = ParseD(Arg("-balulaOsmLat"), -4.32194), lon = ParseD(Arg("-balulaOsmLon"), 15.31194);
            float radius = (float)ParseD(Arg("-balulaOsmRadius"), 2500.0);
            int maxBuildings = (int)ParseD(Arg("-balulaOsmBuildings"), 9000.0);
            string report = OsmImporterWindow.ImportBatch(osm, "Kinshasa", lat, lon, radius, maxBuildings);
            Debug.Log("[BALULA] Import OSM : " + report);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static double ParseD(string s, double fallback)
        {
            double v;
            return !string.IsNullOrEmpty(s) && double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v) ? v : fallback;
        }
    }
}
#endif
