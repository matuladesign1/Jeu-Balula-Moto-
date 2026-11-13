// BALULA MOTO — personnages 3D réalistes (avatars Microsoft Rocketbox, licence MIT) : animations,
// accessoires (casque, foulard, bassine, parapluie, téléphone, gilet), pose des conducteurs et passagers
// (cinématique inverse), choix des modèles selon la tenue demandée.
// Les prefabs sont créés dans l'éditeur par HumanBuilder (menu BALULA MOTO > 5) dans Resources/BalulaHumans.
using System.Collections.Generic;
using UnityEngine;

namespace BalulaMoto.Runtime
{
    public enum HumanAction { Idle, Idle2, Walk, WalkSlow, Wave, Sit, Phone, Talk, Wait, Umbrella, Crouch }

    /// <summary>Choix et création des personnages réalistes selon la tenue demandée par le jeu.</summary>
    public static class HumanLibrary
    {
        private static readonly Dictionary<string, GameObject[]> cache = new Dictionary<string, GameObject[]>();
        private static int available = -1;

        public static bool Available
        {
            get
            {
                if (available < 0) available = Load("Hommes").Length + Load("Femmes").Length > 0 ? 1 : 0;
                return available == 1;
            }
        }

        public static GameObject[] Load(string category)
        {
            GameObject[] list;
            if (cache.TryGetValue(category, out list)) return list;
            list = Resources.LoadAll<GameObject>("BalulaHumans/" + category);
            if (list == null) list = new GameObject[0];
            cache[category] = list;
            return list;
        }

        private static GameObject From(string category, System.Random rnd)
        {
            GameObject[] list = Load(category);
            if (list.Length == 0) return null;
            return list[rnd.Next(list.Length)];
        }

        private static GameObject Named(string category, string contains, System.Random rnd)
        {
            List<GameObject> found = new List<GameObject>();
            foreach (GameObject g in Load(category)) if (g.name.Contains(contains)) found.Add(g);
            return found.Count > 0 ? found[rnd.Next(found.Count)] : From(category, rnd);
        }

        /// <summary>Avatars proposés pour le chauffeur (identifiant du modèle, description).</summary>
        public static readonly string[][] DriverChoices =
        {
            new[] { "Male_Adult_04", "Homme — tenue de ville" },
            new[] { "Male_Adult_12", "Homme — chemise" },
            new[] { "Male_Adult_17", "Homme — décontracté" },
            new[] { "Male_Adult_18", "Homme — veste" },
            new[] { "Sports_Male_03", "Homme — sport" },
            new[] { "Delivery_Male_01", "Homme — livreur" },
            new[] { "Business_Male_05", "Homme — costume" },
            new[] { "Wood_Male_01", "Homme — artisan" },
            new[] { "Construction_Male_03", "Homme — chantier" },
            new[] { "Business_Female_01", "Femme — tailleur" },
            new[] { "Female_Party_02", "Femme — sortie" }
        };

        public static GameObject FindPrefab(string avatarId)
        {
            if (string.IsNullOrEmpty(avatarId) || !Available) return null;
            foreach (string cat in new[] { "Hommes", "Ouvriers", "Femmes", "Sante", "Police", "Enfants" })
                foreach (GameObject g in Load(cat))
                    if (g != null && g.name == avatarId) return g;
            return null;
        }

        /// <summary>Crée l'avatar demandé (chauffeur choisi dans le menu) ou, à défaut, un personnage selon la tenue.</summary>
        public static GameObject SpawnNamed(string avatarId, Transform parent, Vector3 localPos, string outfit, bool seated, int seed, bool helmet)
        {
            if (!Available) return null;
            GameObject prefab = FindPrefab(avatarId);
            if (prefab == null) return Spawn(parent, localPos, outfit, seated, seed, helmet);
            return Instantiate(prefab, parent, localPos, outfit, seated, seed, helmet);
        }

        /// <summary>Une femme : modèles « Femmes », parfois l'infirmière.</summary>
        public static GameObject Woman(System.Random rnd)
        {
            GameObject g = rnd.Next(5) == 0 ? From("Sante", rnd) : From("Femmes", rnd);
            return g != null ? g : From("Femmes", rnd);
        }

        /// <summary>Choisit un modèle (mêmes noms de tenues que les personnages en volumes simples).</summary>
        public static GameObject Pick(string outfit, int seed)
        {
            System.Random rnd = new System.Random(seed * 31 + 7);
            GameObject g;
            switch (outfit)
            {
                case "police":
                case "roulage": g = From("Police", rnd); break;
                case "travail":
                case "mecano": g = From("Ouvriers", rnd); break;
                case "blouse": g = From("Sante", rnd); break;
                case "uniforme": g = From("Enfants", rnd); break;
                case "pagne": g = Woman(rnd); break;
                case "vendeur": g = rnd.Next(10) < 6 ? Woman(rnd) : From("Hommes", rnd); break;
                case "costume": g = rnd.Next(3) == 0 ? Named("Femmes", "Business", rnd) : Named("Hommes", "Business", rnd); break;
                case "chauffeur": g = From("Hommes", rnd); break;
                default:
                    int r = rnd.Next(100);
                    g = r < 52 ? From("Hommes", rnd) : (r < 86 ? Woman(rnd) : (r < 93 ? From("Enfants", rnd) : From("Ouvriers", rnd)));
                    break;
            }
            if (g == null) g = From("Hommes", rnd);
            if (g == null) g = From("Femmes", rnd);
            return g;
        }

        /// <summary>Crée un personnage réaliste, ou null si les prefabs ne sont pas disponibles.</summary>
        public static GameObject Spawn(Transform parent, Vector3 localPos, string outfit, bool seated, int seed, bool helmet)
        {
            if (!Available) return null;
            GameObject prefab = Pick(outfit, seed);
            if (prefab == null) return null;
            return Instantiate(prefab, parent, localPos, outfit, seated, seed, helmet);
        }

        public static GameObject Instantiate(GameObject prefab, Transform parent, Vector3 localPos, string outfit, bool seated, int seed, bool helmet)
        {
            GameObject g = Object.Instantiate(prefab, parent, false);
            g.name = prefab.name;
            EnsureVisible(g);
            g.transform.localPosition = localPos;
            g.transform.localRotation = Quaternion.identity;
            System.Random rnd = new System.Random(seed * 13 + 5);
            HumanAvatar av = g.GetComponent<HumanAvatar>();
            if (av != null && !av.Child && !seated) g.transform.localScale = Vector3.one * (0.96f + (float)rnd.NextDouble() * 0.08f);
            HumanFigure fig = g.AddComponent<HumanFigure>();
            fig.Avatar = av;
            fig.Seated = seated;
            if (av == null) return g;
            if (seated)
            {
                if (av.Anim != null) av.Anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                SeatAnchor sa = g.AddComponent<SeatAnchor>();
                sa.Avatar = av;
                sa.LocalSeat = localPos;
            }
            av.Play(seated ? HumanAction.Sit : HumanAction.Idle, 0f, true);
            HumanProps.Dress(av, outfit, helmet, rnd);
            return g;
        }

        /// <summary>Garantit que le personnage est dessiné : taille du groupe de détails et boîtes englobantes
        /// des maillages animés (une valeur nulle les fait disparaître, seul le casque restait visible).</summary>
        public static void EnsureVisible(GameObject g)
        {
            LODGroup lg = g.GetComponent<LODGroup>();
            if (lg != null)
            {
                lg.localReferencePoint = new Vector3(0f, 0.9f, 0f);
                lg.size = 1.9f;
            }
            foreach (SkinnedMeshRenderer smr in g.GetComponentsInChildren<SkinnedMeshRenderer>(true)) GenerousBounds(smr);
        }

        /// <summary>Boîte englobante de 4 m autour de l'os racine (quelle que soit son orientation ou son échelle).</summary>
        public static void GenerousBounds(SkinnedMeshRenderer smr)
        {
            if (smr == null) return;
            Transform rb = smr.rootBone != null ? smr.rootBone : smr.transform;
            float scale = Mathf.Abs(rb.lossyScale.x);
            if (scale < 1e-5f) scale = 1f;
            smr.localBounds = new Bounds(Vector3.zero, Vector3.one * (4f / scale));
        }

        /// <summary>Conducteur de moto ou de tricycle : mains sur le guidon, pieds sur les repose-pieds.</summary>
        public static void SetupRider(GameObject human, Transform handL, Transform handR, Transform footL, Transform footR, float lean)
        {
            if (human == null) return;
            HumanAvatar av = human.GetComponent<HumanAvatar>();
            if (av == null) return;
            RiderPose rp = human.AddComponent<RiderPose>();
            rp.Avatar = av;
            rp.HandTargetL = handL;
            rp.HandTargetR = handR;
            rp.FootTargetL = footL;
            rp.FootTargetR = footR;
            rp.Lean = lean;
        }

        /// <summary>Passager assis à califourchon : pieds sur les repose-pieds (dans l'espace du siège).</summary>
        public static void SetupStraddle(GameObject human, Transform seat, float footDrop, float footSpread, float footForward)
        {
            if (human == null || seat == null || human.GetComponent<HumanAvatar>() == null) return;
            Transform l = new GameObject("PiedPassagerG").transform;
            l.SetParent(seat, false);
            l.localPosition = new Vector3(-footSpread, -footDrop, footForward);
            Transform r = new GameObject("PiedPassagerD").transform;
            r.SetParent(seat, false);
            r.localPosition = new Vector3(footSpread, -footDrop, footForward);
            SetupRider(human, null, null, l, r, 0f);
        }

        /// <summary>Personnage de la circulation : pose calculée une fois puis figée (aucun coût par image).</summary>
        public static void MakeStatic(GameObject human, bool lowDetail)
        {
            if (human == null) return;
            SeatAnchor sa = human.GetComponent<SeatAnchor>();
            if (sa != null) sa.FreezeWhenSettled = true;
            RiderPose rp = human.GetComponent<RiderPose>();
            if (rp != null) rp.FreezeWhenSettled = true;
            if (sa == null && rp == null)
            {
                HumanAvatar av = human.GetComponent<HumanAvatar>();
                if (av != null) human.AddComponent<FreezeAfter>().Avatar = av;
            }
            HumanFigure f = human.GetComponent<HumanFigure>();
            if (f != null) f.enabled = false;
            if (lowDetail) KeepLowDetail(human);
        }

        /// <summary>Garde seulement les maillages légers (conducteurs vus à travers les vitres).</summary>
        public static void KeepLowDetail(GameObject human)
        {
            LODGroup lg = human.GetComponent<LODGroup>();
            if (lg == null) return;
            LOD[] lods = lg.GetLODs();
            if (lods.Length < 3) return;
            for (int i = 0; i < lods.Length - 2; i++)
                foreach (Renderer r in lods[i].renderers)
                    if (r != null) Object.Destroy(r.gameObject);
            LOD a = lods[lods.Length - 2], b = lods[lods.Length - 1];
            lg.SetLODs(new[] { new LOD(0.1f, a.renderers), new LOD(0.015f, b.renderers) });
        }
    }

    /// <summary>Accessoires simples attachés aux os (casque, foulard, bassine, gilet, téléphone, parapluie).</summary>
    public static class HumanProps
    {
        private static readonly Color[] Fabric =
        {
            new Color(0.9f, 0.32f, 0.05f), new Color(0.12f, 0.45f, 0.2f), new Color(0.1f, 0.3f, 0.7f), new Color(0.7f, 0.08f, 0.35f),
            new Color(0.95f, 0.75f, 0.1f), new Color(0.4f, 0.1f, 0.5f), new Color(0.0f, 0.5f, 0.55f), new Color(0.78f, 0.1f, 0.1f)
        };

        public static Color RandomFabric(System.Random rnd) { return Fabric[rnd.Next(Fabric.Length)]; }

        public static void Dress(HumanAvatar av, string outfit, bool helmet, System.Random rnd)
        {
            if (helmet) Helmet(av);
            else if (av.Female && !av.Child && (outfit == "pagne" ? rnd.Next(10) < 7 : (outfit == "vendeur" || outfit == "civil") && rnd.Next(10) < 3)) Headwrap(av, RandomFabric(rnd));
            if (outfit == "roulage") Vest(av, new Color(1f, 0.85f, 0f));
        }

        private static GameObject Attach(Transform bone, HumanAvatar av, string name, Vector3 worldPos, Mesh mesh, Vector3 size, Color c, float smooth)
        {
            GameObject g = Prims.MeshObject(bone, name, mesh, Mats.Lit(c, smooth));
            g.transform.position = worldPos;
            g.transform.rotation = av.transform.rotation;
            g.transform.localScale = Vector3.one;
            Vector3 ls = bone.lossyScale;
            g.transform.localScale = new Vector3(size.x / Mathf.Max(0.001f, ls.x), size.y / Mathf.Max(0.001f, ls.y), size.z / Mathf.Max(0.001f, ls.z));
            MeshRenderer mr = g.GetComponent<MeshRenderer>();
            if (mr != null) mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g;
        }

        /// <summary>Position d'un point de la tête dans la pose de repos (au moment de l'attache).</summary>
        private static Vector3 HeadPoint(HumanAvatar av, float up, float forward)
        {
            float s = av.transform.lossyScale.y * (av.Child ? 0.85f : 1f);
            Vector3 basePos = av.Head != null ? av.Head.position : av.transform.position + av.transform.up * (av.HeadTopHeight - 0.2f);
            return basePos + av.transform.up * up * s + av.transform.forward * forward * s;
        }

        public static void Helmet(HumanAvatar av)
        {
            if (av.Head == null) return;
            float s = av.Child ? 0.85f : 1f;
            Attach(av.Head, av, "Casque", HeadPoint(av, 0.11f, -0.005f), Prims.Sphere, new Vector3(0.29f, 0.29f, 0.32f) * s, new Color(0.06f, 0.06f, 0.07f), 0.75f);
            Attach(av.Head, av, "Visiere", HeadPoint(av, 0.1f, 0.12f), Prims.Sphere, new Vector3(0.24f, 0.12f, 0.1f) * s, new Color(0.08f, 0.1f, 0.13f), 0.95f);
        }

        public static void Headwrap(HumanAvatar av, Color c)
        {
            if (av.Head == null) return;
            Attach(av.Head, av, "Foulard", HeadPoint(av, 0.17f, -0.03f), Prims.Sphere, new Vector3(0.23f, 0.17f, 0.25f), c, 0.25f);
            Attach(av.Head, av, "Noeud", HeadPoint(av, 0.25f, 0.02f), Prims.Sphere, new Vector3(0.12f, 0.08f, 0.1f), Color.Lerp(c, Color.white, 0.15f), 0.25f);
        }

        /// <summary>Bassine portée sur la tête (vendeuses ambulantes), avec marchandises.</summary>
        public static void Basin(HumanAvatar av, System.Random rnd)
        {
            if (av.Head == null) return;
            Vector3 top = HeadPoint(av, 0.215f, 0f);
            Attach(av.Head, av, "Coussinet", top + av.transform.up * 0.015f, Prims.Cylinder, new Vector3(0.16f, 0.015f, 0.16f), RandomFabric(rnd), 0.2f);
            Color basin = rnd.Next(2) == 0 ? new Color(0.75f, 0.76f, 0.78f) : new Color(0.15f, 0.35f, 0.75f);
            Attach(av.Head, av, "Bassine", top + av.transform.up * 0.08f, Prims.Cylinder, new Vector3(0.52f, 0.05f, 0.52f), basin, rnd.Next(2) == 0 ? 0.8f : 0.4f);
            Color[] goods = { new Color(0.95f, 0.8f, 0.15f), new Color(0.85f, 0.35f, 0.1f), new Color(0.35f, 0.6f, 0.15f), new Color(0.75f, 0.55f, 0.3f), new Color(0.9f, 0.9f, 0.85f) };
            Color gc = goods[rnd.Next(goods.Length)];
            for (int i = 0; i < 7; i++)
            {
                float ang = i * 51f * Mathf.Deg2Rad;
                float rad = i == 0 ? 0f : 0.14f;
                Vector3 off = av.transform.right * Mathf.Cos(ang) * rad + av.transform.forward * Mathf.Sin(ang) * rad;
                Attach(av.Head, av, "Marchandise", top + av.transform.up * (i == 0 ? 0.2f : 0.15f) + off, Prims.Sphere, Vector3.one * 0.12f, ColorUtil.Shade(gc, 0.85f + 0.05f * (i % 3)), 0.3f);
            }
        }

        public static void Vest(HumanAvatar av, Color c)
        {
            Transform spine = av.Spine2 != null ? av.Spine2 : av.Spine1;
            if (spine == null) return;
            Attach(spine, av, "Gilet", spine.position + av.transform.up * 0.02f, Prims.Cube, new Vector3(0.42f, 0.46f, 0.27f), c, 0.3f);
        }

        /// <summary>Téléphone ou parapluie : attaché après la mise en pose, à la main la plus haute.</summary>
        public static void HandProp(HumanAvatar av, bool umbrella)
        {
            if (av == null || av.HandL == null || av.HandR == null) return;
            av.gameObject.AddComponent<DelayedHandProp>().Init(av, umbrella);
        }
    }

    /// <summary>Attend deux images que l'animation soit appliquée, puis attache l'objet à la main la plus haute.</summary>
    public class DelayedHandProp : MonoBehaviour
    {
        private HumanAvatar av;
        private bool umbrella;
        private int frames;

        public void Init(HumanAvatar a, bool isUmbrella) { av = a; umbrella = isUmbrella; }

        private void LateUpdate()
        {
            if (av == null) { Destroy(this); return; }
            if (++frames < 3) return;
            Transform hand = av.HandL.position.y > av.HandR.position.y ? av.HandL : av.HandR;
            if (umbrella)
            {
                Vector3 up = av.transform.up;
                Vector3 grip = hand.position;
                GameObject shaft = Prims.MeshObject(hand, "Parapluie", Prims.Cylinder, Mats.Lit(new Color(0.15f, 0.12f, 0.1f), 0.4f));
                shaft.transform.position = grip + up * 0.45f;
                shaft.transform.rotation = av.transform.rotation;
                SetWorldScale(shaft.transform, new Vector3(0.022f, 0.48f, 0.022f));
                GameObject canopy = Prims.MeshObject(hand, "Toile", Prims.Sphere, Mats.Lit(rndColor(), 0.35f));
                canopy.transform.position = grip + up * 0.95f;
                canopy.transform.rotation = av.transform.rotation;
                SetWorldScale(canopy.transform, new Vector3(1.05f, 0.32f, 1.05f));
            }
            else
            {
                GameObject phone = Prims.MeshObject(hand, "Telephone", Prims.Cube, Mats.Lit(new Color(0.05f, 0.05f, 0.06f), 0.8f));
                phone.transform.position = hand.position + av.transform.forward * 0.03f;
                phone.transform.rotation = hand.rotation;
                SetWorldScale(phone.transform, new Vector3(0.075f, 0.15f, 0.012f));
            }
            Destroy(this);
        }

        private Color rndColor()
        {
            Color[] c = { new Color(0.08f, 0.08f, 0.1f), new Color(0.1f, 0.25f, 0.6f), new Color(0.7f, 0.1f, 0.1f), new Color(0.15f, 0.45f, 0.2f) };
            return c[Random.Range(0, c.Length)];
        }

        private static void SetWorldScale(Transform t, Vector3 size)
        {
            t.localScale = Vector3.one;
            Vector3 ls = t.lossyScale;
            t.localScale = new Vector3(size.x / Mathf.Max(0.0001f, ls.x), size.y / Mathf.Max(0.0001f, ls.y), size.z / Mathf.Max(0.0001f, ls.z));
        }
    }

    /// <summary>Assied le personnage : déplace le modèle pour que le bassin repose sur le siège.</summary>
    [DefaultExecutionOrder(100)]
    public class SeatAnchor : MonoBehaviour
    {
        public HumanAvatar Avatar;
        /// <summary>Position des hanches dans l'espace du parent (siège).</summary>
        public Vector3 LocalSeat;
        public bool FreezeWhenSettled;
        private float settle = 0.7f;

        private void LateUpdate()
        {
            if (Avatar == null || Avatar.Pelvis == null || Avatar.Model == null) return;
            Transform parent = transform.parent;
            Vector3 target = parent != null ? parent.TransformPoint(LocalSeat) : LocalSeat;
            Vector3 delta = target + transform.up * 0.03f - Avatar.Pelvis.position;
            if (settle > 0f || delta.sqrMagnitude > 0.0004f) Avatar.Model.position += delta;
            settle -= Time.deltaTime > 0f ? Time.deltaTime : 0.02f;
            if (settle <= 0f && FreezeWhenSettled && GetComponent<RiderPose>() == null)
            {
                Avatar.Freeze();
                enabled = false;
            }
        }
    }

    /// <summary>Fige un personnage debout après la première pose (figurants immobiles).</summary>
    public class FreezeAfter : MonoBehaviour
    {
        public HumanAvatar Avatar;
        private float t = 0.6f;

        private void LateUpdate()
        {
            t -= Time.deltaTime > 0f ? Time.deltaTime : 0.02f;
            if (t > 0f) return;
            if (Avatar != null) Avatar.Freeze();
            enabled = false;
        }
    }

    /// <summary>Pose de conduite : buste légèrement penché, mains et pieds placés par cinématique inverse.</summary>
    [DefaultExecutionOrder(200)]
    public class RiderPose : MonoBehaviour
    {
        public HumanAvatar Avatar;
        public Transform HandTargetL, HandTargetR, FootTargetL, FootTargetR;
        public float Lean = 8f;
        public float Weight = 1f;
        public bool FreezeWhenSettled;
        private float settle = 0.9f;

        private void LateUpdate()
        {
            if (Avatar == null) return;
            Transform root = transform;
            Vector3 up = root.up, fwd = root.forward, right = root.right;
            if (Lean != 0f && Avatar.Spine1 != null) Avatar.Spine1.rotation = Quaternion.AngleAxis(Lean, right) * Avatar.Spine1.rotation;
            if (HandTargetL != null && Avatar.UpperArmL != null)
                TwoBoneIK.Solve(Avatar.UpperArmL, Avatar.ForearmL, Avatar.HandL, HandTargetL.position, Avatar.ForearmL.position - right * 0.3f - up * 0.25f, Weight);
            if (HandTargetR != null && Avatar.UpperArmR != null)
                TwoBoneIK.Solve(Avatar.UpperArmR, Avatar.ForearmR, Avatar.HandR, HandTargetR.position, Avatar.ForearmR.position + right * 0.3f - up * 0.25f, Weight);
            if (FootTargetL != null && Avatar.ThighL != null)
                TwoBoneIK.Solve(Avatar.ThighL, Avatar.CalfL, Avatar.FootL, FootTargetL.position, Avatar.CalfL.position + fwd * 0.4f - right * 0.25f + up * 0.1f, Weight);
            if (FootTargetR != null && Avatar.ThighR != null)
                TwoBoneIK.Solve(Avatar.ThighR, Avatar.CalfR, Avatar.FootR, FootTargetR.position, Avatar.CalfR.position + fwd * 0.4f + right * 0.25f + up * 0.1f, Weight);
            if (FreezeWhenSettled)
            {
                settle -= Time.deltaTime > 0f ? Time.deltaTime : 0.02f;
                if (settle <= 0f)
                {
                    Avatar.Freeze();
                    SeatAnchor sa = GetComponent<SeatAnchor>();
                    if (sa != null) sa.enabled = false;
                    enabled = false;
                }
            }
        }
    }

    /// <summary>Cinématique inverse analytique à deux segments (bras ou jambe) avec direction du coude/genou.</summary>
    public static class TwoBoneIK
    {
        public static void Solve(Transform a, Transform b, Transform c, Vector3 target, Vector3 hint, float weight)
        {
            if (a == null || b == null || c == null || weight <= 0f) return;
            Quaternion aRot0 = a.rotation, bRot0 = b.rotation;
            Vector3 aPos = a.position, bPos = b.position, cPos = c.position;
            float lab = Vector3.Distance(aPos, bPos), lbc = Vector3.Distance(bPos, cPos);
            if (lab < 1e-4f || lbc < 1e-4f) return;
            float lat = Mathf.Clamp(Vector3.Distance(aPos, target), Mathf.Abs(lab - lbc) + 1e-3f, lab + lbc - 1e-3f);

            // 1) Pliage au coude / genou
            Vector3 ba = aPos - bPos, bc = cPos - bPos;
            float currentAngle = Vector3.Angle(ba, bc);
            float cosD = (lab * lab + lbc * lbc - lat * lat) / (2f * lab * lbc);
            float desired = Mathf.Acos(Mathf.Clamp(cosD, -1f, 1f)) * Mathf.Rad2Deg;
            Vector3 axis = Vector3.Cross(bc, ba);
            if (axis.sqrMagnitude < 1e-8f) axis = Vector3.Cross(bc, hint - bPos);
            if (axis.sqrMagnitude < 1e-8f) axis = Vector3.Cross(bc, Vector3.up);
            if (axis.sqrMagnitude < 1e-8f) return;
            axis.Normalize();
            b.rotation = Quaternion.AngleAxis(currentAngle - desired, axis) * b.rotation;

            // 2) Orientation du segment supérieur vers la cible
            cPos = c.position;
            Vector3 toC = cPos - aPos, toT = target - aPos;
            if (toC.sqrMagnitude > 1e-8f && toT.sqrMagnitude > 1e-8f) a.rotation = Quaternion.FromToRotation(toC, toT) * a.rotation;

            // 3) Rotation autour de l'axe épaule→main pour orienter le coude vers l'indice
            Vector3 t = toT.normalized;
            Vector3 vb = Vector3.ProjectOnPlane(b.position - aPos, t);
            Vector3 vh = Vector3.ProjectOnPlane(hint - aPos, t);
            if (vb.sqrMagnitude > 1e-8f && vh.sqrMagnitude > 1e-8f)
                a.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(vb, vh, t), t) * a.rotation;

            if (weight < 1f)
            {
                a.rotation = Quaternion.Slerp(aRot0, a.rotation, weight);
                b.rotation = Quaternion.Slerp(bRot0, b.rotation, weight);
            }
        }
    }
}
