// BALULA MOTO — personnages 3D réalistes : import des avatars Microsoft Rocketbox (licence MIT),
// création des animations, des contrôleurs et des prefabs utilisés par le jeu (foule, clients, passagers,
// conducteurs, policiers). Lancé automatiquement par « Préparer le projet » et par la compilation cloud.
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using BalulaMoto.Runtime;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

namespace BalulaMoto.EditorTools
{
    /// <summary>Réglages d'import des fichiers Rocketbox (modèles, animations, textures).</summary>
    public class RocketboxImportRules : AssetPostprocessor
    {
        public const string Root = "Assets/BalulaMoto/ThirdParty/Rocketbox/";

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Root)) return;
            ModelImporter mi = assetImporter as ModelImporter;
            if (mi == null) return;
            bool anim = assetPath.Contains("/Animations/");
            mi.importBlendShapes = false;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importVisibility = false;
            mi.isReadable = false;
            mi.animationType = ModelImporterAnimationType.Generic;
            mi.optimizeGameObjects = false;
            mi.importAnimation = anim;
            mi.materialImportMode = anim ? ModelImporterMaterialImportMode.None : ModelImporterMaterialImportMode.ImportStandard;
            if (anim)
            {
                mi.animationCompression = ModelImporterAnimationCompression.Optimal;
                mi.animationRotationError = 0.5f;
                mi.animationPositionError = 0.5f;
                mi.animationScaleError = 0.5f;
                mi.resampleCurves = true;
            }
            else mi.meshCompression = ModelImporterMeshCompression.Low;
        }

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root)) return;
            TextureImporter ti = assetImporter as TextureImporter;
            if (ti == null) return;
            string p = assetPath.ToLowerInvariant();
            bool normal = p.Contains("_normal");
            ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            bool opacity = p.Contains("_opacity");
            ti.mipmapEnabled = true;
            ti.alphaIsTransparency = opacity;
            ti.maxTextureSize = 1024;
            // Sur téléphone : relief en 256, mèches de cheveux et cils en 512, couleur en 1024 (21 avatars en mémoire).
            int phone = normal ? 256 : (opacity ? 512 : 1024);
            TextureImporterPlatformSettings android = ti.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.maxTextureSize = phone;
            ti.SetPlatformTextureSettings(android);
            TextureImporterPlatformSettings ios = ti.GetPlatformTextureSettings("iPhone");
            ios.overridden = true;
            ios.maxTextureSize = phone;
            ti.SetPlatformTextureSettings(ios);
        }

        private void OnPostprocessModel(GameObject g)
        {
            if (!assetPath.StartsWith(Root)) return;
            // Certains fichiers 3ds Max nomment le squelette « Bip02 » : tout est ramené à « Bip01 »
            RenameBip(g.transform);
        }

        private static void RenameBip(Transform t)
        {
            if (t.name.Contains("Bip02")) t.name = t.name.Replace("Bip02", "Bip01");
            for (int i = 0; i < t.childCount; i++) RenameBip(t.GetChild(i));
        }
    }

    public static class HumanBuilder
    {
        private const string Rbx = RocketboxImportRules.Root;
        private const string OutPrefabs = BalulaSetup.Root + "/Resources/BalulaHumans";
        private const string OutGen = BalulaSetup.Root + "/Generated/Humains";

        private struct Def
        {
            public string Name, Category;
            public bool Female, Child;
            public Def(string name, string category, bool female, bool child) { Name = name; Category = category; Female = female; Child = child; }
        }

        // Avatars retenus : tenues de ville, ouvriers, infirmière, enfants et policier ; tous ont un teint africain
        // (population de Kinshasa) : les textures de peau claire du pack ont été assombries par
        // scripts/africaniser_avatars.py. Les uniformes militaires étrangers du pack sont exclus.
        private static readonly Def[] Defs =
        {
            new Def("Business_Male_05", "Hommes", false, false),
            new Def("Male_Adult_04", "Hommes", false, false),
            new Def("Male_Adult_09", "Hommes", false, false),
            new Def("Male_Adult_10", "Hommes", false, false),
            new Def("Male_Adult_12", "Hommes", false, false),
            new Def("Male_Adult_17", "Hommes", false, false),
            new Def("Male_Adult_18", "Hommes", false, false),
            new Def("Sports_Male_03", "Hommes", false, false),
            new Def("Delivery_Male_01", "Hommes", false, false),
            new Def("Wood_Male_01", "Ouvriers", false, false),
            new Def("Construction_Male_03", "Ouvriers", false, false),
            new Def("Construction_Male_08", "Ouvriers", false, false),
            new Def("Business_Female_01", "Femmes", true, false),
            new Def("Female_Party_02", "Femmes", true, false),
            new Def("Female_Adult_02", "Femmes", true, false),
            new Def("Female_Adult_11", "Femmes", true, false),
            new Def("Female_Adult_15", "Femmes", true, false),
            new Def("Medical_Female_01", "Sante", true, false),
            new Def("Female_Child_02", "Enfants", true, true),
            new Def("Male_Child_02", "Enfants", false, true),
            new Def("Police_Male_01", "Police", false, false),
        };

        // État de l'Animator → fichier d'animation (hommes, femmes). L'ordre suit HumanAvatar.StateNames.
        private static readonly string[][] ClipFiles =
        {
            new[] { "Idle", "m_idle_neutral_01", "f_idle_neutral_01" },
            new[] { "Idle2", "m_idle_neutral_02", "f_idle_neutral_02" },
            new[] { "Walk", "m_walk_neutral_01", "f_walk_neutral_01" },
            new[] { "WalkSlow", "m_walk_slow_01", "f_walk_slow_01" },
            new[] { "Wave", "m_wave_01", "f_wave_01" },
            new[] { "Sit", "m_sit_chair_idle_neutral_01", "f_sit_chair_idle_neutral_01" },
            new[] { "Phone", "m_cell_phone_talk_01", "f_cell_phone_textmessage" },
            new[] { "Talk", "m_gestic_talk_neutral_01", "f_gestic_talk_neutral_01" },
            new[] { "Wait", "m_idle_waiting_01", "f_idle_waiting_01" },
            new[] { "Umbrella", "m_umbrella_idle_01", "f_umbrella_idle_01" },
            new[] { "Crouch", "m_crouch_idle", "f_crouch_idle" },
        };

        public static bool SourcesPresent { get { return AssetDatabase.IsValidFolder(Rbx + "Avatars"); } }

        public static bool PrefabsPresent
        {
            get
            {
                foreach (Def d in Defs)
                    if (AssetDatabase.LoadAssetAtPath<GameObject>(OutPrefabs + "/" + d.Category + "/" + d.Name + ".prefab") == null) return false;
                return true;
            }
        }

        [MenuItem("BALULA MOTO/5. Construire les personnages 3D (Rocketbox)", priority = 21)]
        public static void BuildMenu()
        {
            string report = BuildAll(true);
            EditorUtility.DisplayDialog("BALULA MOTO — personnages", report, "OK");
        }

        /// <summary>Construit animations, contrôleurs et prefabs. Retourne un rapport lisible.</summary>
        public static string BuildAll(bool force)
        {
            if (!SourcesPresent) return "Personnages 3D : dossier " + Rbx + "Avatars absent (les personnages en volumes simples restent utilisés).";
            if (!force && PrefabsPresent) return "Personnages 3D : déjà construits (" + Defs.Length + " avatars).";
            StringBuilder report = new StringBuilder();
            BalulaSetup.EnsureFolder(OutGen + "/Clips");
            BalulaSetup.EnsureFolder(OutGen + "/Materiaux");
            BalulaSetup.EnsureFolder(OutPrefabs);

            float maleBip, femaleBip;
            Dictionary<string, AnimationClip> male = BuildClipSet(1, "H", report, out maleBip);
            Dictionary<string, AnimationClip> female = BuildClipSet(2, "F", report, out femaleBip);
            AnimatorController cm = BuildController(OutGen + "/Hommes.controller", male);
            AnimatorController cf = BuildController(OutGen + "/Femmes.controller", female);

            bool urp = GraphicsSettings.currentRenderPipeline != null;
            Shader lit = Shader.Find(urp ? "Universal Render Pipeline/Lit" : "Standard");
            if (lit == null) lit = Shader.Find("Standard");
            int ok = 0;
            foreach (Def d in Defs)
            {
                string r = BuildPrefab(d, d.Female ? cf : cm, d.Female ? femaleBip : maleBip, lit, urp);
                if (r == null) ok++;
                else report.Append(r).Append('\n');
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            string summary = "Personnages 3D : " + ok + "/" + Defs.Length + " avatars Rocketbox prêts, " + male.Count + "+" + female.Count + " animations.";
            Debug.Log("[BALULA] " + summary + (report.Length > 0 ? "\n" + report : ""));
            return summary + (report.Length > 0 ? "\n" + report : "");
        }

        // ------------------------------------------------------------------
        // Animations : copie des rotations (et de la position de Bip01 seulement), pour que la même
        // animation convienne à tous les avatars quelles que soient leurs proportions.
        // ------------------------------------------------------------------
        private static Dictionary<string, AnimationClip> BuildClipSet(int column, string prefix, StringBuilder report, out float idleBipHeight)
        {
            Dictionary<string, AnimationClip> set = new Dictionary<string, AnimationClip>();
            idleBipHeight = -1f;
            foreach (string[] row in ClipFiles)
            {
                float h;
                AnimationClip c = DeriveClip(row[column], OutGen + "/Clips/" + prefix + "_" + row[0] + ".anim", out h);
                if (c == null) { report.Append("Animation absente : ").Append(row[column]).Append('\n'); continue; }
                set[row[0]] = c;
                if (row[0] == "Idle") idleBipHeight = h;
            }
            return set;
        }

        private static AnimationClip DeriveClip(string file, string outPath, out float bipHeight)
        {
            bipHeight = -1f;
            string src = Rbx + "Animations/" + file + ".fbx";
            AnimationClip source = null;
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(src))
            {
                AnimationClip c = o as AnimationClip;
                if (c != null && !c.name.StartsWith("__preview__")) { source = c; break; }
            }
            if (source == null) return null;

            AnimationClip clip = new AnimationClip();
            clip.name = Path.GetFileNameWithoutExtension(outPath);
            clip.frameRate = source.frameRate;
            AnimationCurve px = null, py = null, pz = null;
            foreach (EditorCurveBinding b in AnimationUtility.GetCurveBindings(source))
            {
                string path = NormalizePath(b.path);
                if (path == null) continue;
                if (b.propertyName.StartsWith("m_LocalScale")) continue;
                bool position = b.propertyName.StartsWith("m_LocalPosition");
                if (position && path != "Bip01") continue;
                AnimationCurve curve = AnimationUtility.GetEditorCurve(source, b);
                if (curve == null) continue;
                EditorCurveBinding nb = b;
                nb.path = path;
                AnimationUtility.SetEditorCurve(clip, nb, curve);
                if (position)
                {
                    if (b.propertyName.EndsWith(".x")) px = curve;
                    else if (b.propertyName.EndsWith(".y")) py = curve;
                    else if (b.propertyName.EndsWith(".z")) pz = curve;
                }
            }
            clip.EnsureQuaternionContinuity();
            if (px != null && py != null && pz != null)
            {
                bipHeight = new Vector3(px.Evaluate(0f), py.Evaluate(0f), pz.Evaluate(0f)).magnitude;
                // Les marches de la bibliothèque avancent le bassin (environ 1,2 m par cycle) puis reviennent
                // en arrière à la boucle : on retire cette avance (le jeu déplace déjà le personnage), sinon
                // il glisse et « saute » en arrière à chaque pas.
                float limit = Mathf.Max(0.02f, bipHeight * 0.05f);
                string[] axes = { ".x", ".y", ".z" };
                AnimationCurve[] curves = { px, py, pz };
                for (int a = 0; a < 3; a++)
                {
                    AnimationCurve fixedCurve = Detrend(curves[a], limit);
                    if (fixedCurve == null) continue;
                    EditorCurveBinding pb = EditorCurveBinding.FloatCurve("Bip01", typeof(Transform), "m_LocalPosition" + axes[a]);
                    AnimationUtility.SetEditorCurve(clip, pb, fixedCurve);
                }
            }
            AnimationClipSettings s = AnimationUtility.GetAnimationClipSettings(clip);
            s.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, s);
            if (AssetDatabase.LoadAssetAtPath<Object>(outPath) != null) AssetDatabase.DeleteAsset(outPath);
            AssetDatabase.CreateAsset(clip, outPath);
            return clip;
        }

        /// <summary>Retire la dérive linéaire d'une courbe de position (et la recentre) si elle dépasse la limite ;
        /// renvoie null si la courbe ne dérive pas.</summary>
        private static AnimationCurve Detrend(AnimationCurve c, float limit)
        {
            if (c == null || c.length < 2) return null;
            Keyframe[] k = c.keys;
            float t0 = k[0].time, t1 = k[k.Length - 1].time, span = t1 - t0;
            if (span <= 1e-4f) return null;
            float drift = k[k.Length - 1].value - k[0].value;
            if (Mathf.Abs(drift) < limit) return null;
            float slope = drift / span;
            float sum = 0f;
            const int n = 64;
            for (int i = 0; i < n; i++)
            {
                float t = t0 + span * (i + 0.5f) / n;
                sum += c.Evaluate(t) - slope * (t - t0);
            }
            float mean = sum / n;
            for (int i = 0; i < k.Length; i++)
            {
                k[i].value = k[i].value - slope * (k[i].time - t0) - mean;
                if (!float.IsInfinity(k[i].inTangent)) k[i].inTangent -= slope;
                if (!float.IsInfinity(k[i].outTangent)) k[i].outTangent -= slope;
            }
            AnimationCurve r = new AnimationCurve(k);
            r.preWrapMode = c.preWrapMode;
            r.postWrapMode = c.postWrapMode;
            return r;
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            path = path.Replace("Bip02", "Bip01");
            int i = path.IndexOf("Bip01");
            return i < 0 ? null : path.Substring(i);
        }

        private static AnimatorController BuildController(string path, Dictionary<string, AnimationClip> clips)
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(path) != null) AssetDatabase.DeleteAsset(path);
            AnimatorController ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
            AnimatorState idle = null;
            foreach (string[] row in ClipFiles)
            {
                AnimationClip c;
                if (!clips.TryGetValue(row[0], out c) || c == null) continue;
                AnimatorState st = sm.AddState(row[0]);
                st.motion = c;
                st.writeDefaultValues = false;
                if (row[0] == "Idle") idle = st;
            }
            if (idle != null) sm.defaultState = idle;
            EditorUtility.SetDirty(ctrl);
            return ctrl;
        }

        // ------------------------------------------------------------------
        // Prefab : modèle + matériaux + niveaux de détail + Animator + HumanAvatar
        // ------------------------------------------------------------------
        private static string BuildPrefab(Def d, AnimatorController ctrl, float clipBipHeight, Shader lit, bool urp)
        {
            string folder = Rbx + "Avatars/" + d.Name;
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "/" + d.Name + ".fbx");
            if (model == null) return "Avatar absent : " + d.Name;

            GameObject root = new GameObject("Humain_" + d.Name);
            GameObject inst = Object.Instantiate(model);
            inst.name = "Modele";
            inst.transform.SetParent(root.transform, false);
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;

            Transform bip = Find(inst.transform, "Bip01");
            if (bip == null)
            {
                Object.DestroyImmediate(root);
                return "Squelette Bip01 introuvable : " + d.Name;
            }
            if (bip.parent != inst.transform) bip.SetParent(inst.transform, true);

            // Matériaux
            Material body = MakeMaterial(d.Name + "_corps", lit, urp, Tex(folder, "_body_color"), Tex(folder, "_body_normal"), false);
            Material head = MakeMaterial(d.Name + "_tete", lit, urp, Tex(folder, "_head_color"), Tex(folder, "_head_normal"), false);
            Texture2D opacityTex = Tex(folder, "_opacity_color");
            Material opacity = opacityTex != null ? MakeMaterial(d.Name + "_cheveux", lit, urp, opacityTex, null, true) : null;
            // Accessoires ayant leur propre texture (casque, outils, équipement…) : sans cela ils prenaient la
            // texture du corps (casque bleu à motifs au lieu de blanc).
            Dictionary<string, Material> extras = new Dictionary<string, Material>();

            // Niveaux de détail fournis par Rocketbox : hipoly, midpoly, lowpoly, ultralowpoly
            List<Renderer> lod0 = new List<Renderer>(), lod1 = new List<Renderer>(), lod2 = new List<Renderer>(), lod3 = new List<Renderer>();
            SkinnedMeshRenderer bestMid = null;
            int bestMidBones = -1;
            List<SkinnedMeshRenderer> extraMids = new List<SkinnedMeshRenderer>();
            foreach (SkinnedMeshRenderer smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                string n = smr.name.ToLowerInvariant();
                Material[] mats = smr.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    string mn = mats[i] != null ? mats[i].name.ToLowerInvariant() : "";
                    if (mn.Contains("opacity") && opacity != null) { mats[i] = opacity; continue; }
                    if (mn.Contains("head")) { mats[i] = head; continue; }
                    if (mn.Contains("body")) { mats[i] = body; continue; }
                    mats[i] = ExtraMaterial(d, folder, mn, extras, lit, urp) ?? body;
                }
                smr.sharedMaterials = mats;
                smr.updateWhenOffscreen = false;
                smr.skinnedMotionVectors = false;
                smr.allowOcclusionWhenDynamic = true;
                if (n.Contains("ultralowpoly")) lod3.Add(smr);
                else if (n.Contains("lowpoly")) lod2.Add(smr);
                else if (n.Contains("midpoly"))
                {
                    int bones = smr.bones != null ? smr.bones.Length : 0;
                    if (bones > bestMidBones)
                    {
                        if (bestMid != null) extraMids.Add(bestMid);
                        bestMid = smr;
                        bestMidBones = bones;
                    }
                    else extraMids.Add(smr);
                }
                else lod0.Add(smr);
            }
            foreach (SkinnedMeshRenderer s in extraMids) Object.DestroyImmediate(s.gameObject);
            if (bestMid != null) lod1.Add(bestMid);
            SetShadows(lod0, true);
            SetShadows(lod1, true);
            SetShadows(lod2, false);
            SetShadows(lod3, false);
            List<LOD> lods = new List<LOD>();
            // Seuils (hauteur relative à l'écran) choisis selon les niveaux réellement présents dans le FBX.
            // Les avatars du projet n'ont souvent que le maillage « hipoly » : avec l'ancien seuil fixe de 0,5 pour
            // ce niveau, le corps n'était dessiné que s'il occupait la moitié de l'écran (au-delà de ~3 m, seuls le
            // casque ou le foulard restaient visibles). Le dernier niveau est toujours masqué très loin (~140 m).
            List<Renderer[]> present = new List<Renderer[]>();
            foreach (List<Renderer> l in new[] { lod0, lod1, lod2, lod3 }) if (l.Count > 0) present.Add(l.ToArray());
            float[][] table =
            {
                new[] { 0.012f },
                new[] { 0.07f, 0.012f },
                new[] { 0.14f, 0.045f, 0.012f },
                new[] { 0.25f, 0.1f, 0.035f, 0.012f }
            };
            if (present.Count > 0)
            {
                float[] th = table[present.Count - 1];
                for (int i = 0; i < present.Count; i++) lods.Add(new LOD(th[i], present[i]));
            }
            if (lods.Count == 0)
            {
                Object.DestroyImmediate(root);
                return "Aucun maillage dans : " + d.Name;
            }
            LODGroup lg = root.AddComponent<LODGroup>();
            lg.SetLODs(lods.ToArray());
            // Taille fixée à la main : en compilation sans écran (-nographics), RecalculateBounds peut donner
            // une taille nulle et le personnage n'est alors jamais dessiné (seul le casque restait visible).
            lg.localReferencePoint = new Vector3(0f, 0.9f, 0f);
            lg.size = 1.9f;
            foreach (List<Renderer> list in new[] { lod0, lod1, lod2, lod3 })
                foreach (Renderer r in list) HumanLibrary.GenerousBounds(r as SkinnedMeshRenderer);

            // Animator (animations « Generic » partagées par tous les avatars du même sexe)
            Animator an = inst.GetComponent<Animator>();
            if (an == null) an = inst.AddComponent<Animator>();
            an.runtimeAnimatorController = ctrl;
            an.applyRootMotion = false;
            an.cullingMode = AnimatorCullingMode.CullCompletely;
            an.updateMode = AnimatorUpdateMode.Normal;

            // Hauteur du bassin : l'avatar est décalé pour que ses pieds touchent le sol avec les animations communes
            float avatarBip = bip.localPosition.magnitude;
            float offset = clipBipHeight > 0.2f ? Mathf.Clamp(avatarBip - clipBipHeight, -0.6f, 0.3f) : 0f;
            inst.transform.localPosition = new Vector3(0f, offset, 0f);

            HumanAvatar av = root.AddComponent<HumanAvatar>();
            av.AvatarId = d.Name;
            av.Category = d.Category;
            av.Female = d.Female;
            av.Child = d.Child;
            av.Anim = an;
            av.Model = inst.transform;
            av.Bip01 = bip;
            av.Pelvis = Find(bip, "Bip01 Pelvis");
            av.Spine1 = Find(bip, "Bip01 Spine1");
            av.Spine2 = Find(bip, "Bip01 Spine2");
            av.Head = Find(bip, "Bip01 Head");
            av.UpperArmL = Find(bip, "Bip01 L UpperArm");
            av.ForearmL = Find(bip, "Bip01 L Forearm");
            av.HandL = Find(bip, "Bip01 L Hand");
            av.UpperArmR = Find(bip, "Bip01 R UpperArm");
            av.ForearmR = Find(bip, "Bip01 R Forearm");
            av.HandR = Find(bip, "Bip01 R Hand");
            av.ThighL = Find(bip, "Bip01 L Thigh");
            av.CalfL = Find(bip, "Bip01 L Calf");
            av.FootL = Find(bip, "Bip01 L Foot");
            av.ThighR = Find(bip, "Bip01 R Thigh");
            av.CalfR = Find(bip, "Bip01 R Calf");
            av.FootR = Find(bip, "Bip01 R Foot");
            av.WalkCycleSpeed = d.Child ? 1.05f : (d.Female ? 1.25f : 1.35f);
            av.HeadTopHeight = av.Head != null ? Mathf.Max(0.8f, inst.transform.InverseTransformPoint(av.Head.position).y + offset + 0.2f) : 1.7f;

            string catFolder = OutPrefabs + "/" + d.Category;
            BalulaSetup.EnsureFolder(catFolder);
            string outPath = catFolder + "/" + d.Name + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(root, outPath);
            Object.DestroyImmediate(root);
            return null;
        }

        /// <summary>Matériau d'un accessoire (« m102_helmet » → texture « m102_helmet_color »), ou null.</summary>
        private static Material ExtraMaterial(Def d, string folder, string matName, Dictionary<string, Material> cache, Shader lit, bool urp)
        {
            string key = matName;
            int sp = key.IndexOf(' ');
            if (sp > 0) key = key.Substring(0, sp);
            int us = key.LastIndexOf('_');
            string part = us >= 0 ? key.Substring(us + 1) : key;
            if (part.Length < 3) return null;
            Material m;
            if (cache.TryGetValue(part, out m)) return m;
            Texture2D c = Tex(folder, "_" + part + "_color");
            m = c != null ? MakeMaterial(d.Name + "_" + part, lit, urp, c, Tex(folder, "_" + part + "_normal"), false) : null;
            cache[part] = m;
            return m;
        }

        private static void SetShadows(List<Renderer> list, bool cast)
        {
            foreach (Renderer r in list)
            {
                r.shadowCastingMode = cast ? ShadowCastingMode.On : ShadowCastingMode.Off;
                r.receiveShadows = true;
            }
        }

        private static Texture2D Tex(string folder, string suffix)
        {
            string dir = folder + "/Textures";
            if (!AssetDatabase.IsValidFolder(dir)) return null;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { dir }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(p).ToLowerInvariant().EndsWith(suffix)) return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
            }
            return null;
        }

        private static Material MakeMaterial(string name, Shader shader, bool urp, Texture2D color, Texture2D normal, bool cutout)
        {
            string path = OutGen + "/Materiaux/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            else m.shader = shader;
            m.name = name;
            m.color = Color.white;
            if (urp)
            {
                m.SetTexture("_BaseMap", color);
                m.SetColor("_BaseColor", Color.white);
                m.SetFloat("_Smoothness", cutout ? 0.25f : 0.32f);
                m.SetFloat("_Metallic", 0f);
            }
            else
            {
                m.SetFloat("_Glossiness", cutout ? 0.25f : 0.32f);
                m.SetFloat("_Metallic", 0f);
            }
            m.mainTexture = color;
            if (normal != null)
            {
                m.SetTexture("_BumpMap", normal);
                m.EnableKeyword("_NORMALMAP");
            }
            if (cutout)
            {
                // Cheveux, cils : découpe alpha (moins coûteux et sans problème de tri, adapté au téléphone)
                m.SetOverrideTag("RenderType", "TransparentCutout");
                m.SetFloat("_Cutoff", 0.35f);
                m.EnableKeyword("_ALPHATEST_ON");
                m.DisableKeyword("_ALPHABLEND_ON");
                m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                m.renderQueue = (int)RenderQueue.AlphaTest;
                if (urp)
                {
                    m.SetFloat("_AlphaClip", 1f);
                    m.SetFloat("_Cull", 0f);
                }
                else
                {
                    m.SetFloat("_Mode", 1f);
                    m.SetInt("_SrcBlend", (int)BlendMode.One);
                    m.SetInt("_DstBlend", (int)BlendMode.Zero);
                    m.SetInt("_ZWrite", 1);
                }
            }
            m.enableInstancing = false;
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Transform Find(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                Transform r = Find(t.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }
    }
}
#endif
