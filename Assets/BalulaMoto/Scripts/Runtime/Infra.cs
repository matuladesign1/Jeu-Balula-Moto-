// BALULA MOTO — outils Unity communs : compatibilité de versions, matériaux, primitives, couleurs, polices.
using System.Collections.Generic;
using BalulaMoto.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace BalulaMoto.Runtime
{
    /// <summary>Compatibilité Unity 2022 LTS / Unity 6.</summary>
    public static class Compat
    {
        public static Vector3 GetVelocity(Rigidbody rb)
        {
#if UNITY_6000_0_OR_NEWER
            return rb.linearVelocity;
#else
            return rb.velocity;
#endif
        }

        public static void SetVelocity(Rigidbody rb, Vector3 v)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = v;
#else
            rb.velocity = v;
#endif
        }

        public static void SetDamping(Rigidbody rb, float linear, float angular)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearDamping = linear;
            rb.angularDamping = angular;
#else
            rb.drag = linear;
            rb.angularDrag = angular;
#endif
        }

        public static T FindFirst<T>() where T : UnityEngine.Object
        {
#if UNITY_2022_2_OR_NEWER
            return UnityEngine.Object.FindFirstObjectByType<T>();
#else
            return UnityEngine.Object.FindObjectOfType<T>();
#endif
        }

        public static T[] FindAll<T>() where T : UnityEngine.Object
        {
#if UNITY_2022_2_OR_NEWER
            return UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None);
#else
            return UnityEngine.Object.FindObjectsOfType<T>();
#endif
        }
    }

    public static class ColorUtil
    {
        /// <summary>Accepte "#RRGGBB" ou "Nom|#RRGGBB".</summary>
        public static Color Parse(string value, Color fallback)
        {
            if (string.IsNullOrEmpty(value)) return fallback;
            int bar = value.IndexOf('|');
            string hex = bar >= 0 ? value.Substring(bar + 1) : value;
            Color c;
            if (ColorUtility.TryParseHtmlString(hex.Trim(), out c)) return c;
            return fallback;
        }

        public static string DisplayName(string option)
        {
            if (string.IsNullOrEmpty(option)) return "";
            int bar = option.IndexOf('|');
            return bar >= 0 ? option.Substring(0, bar) : option;
        }

        public static Color Shade(Color c, float factor)
        {
            return new Color(Mathf.Clamp01(c.r * factor), Mathf.Clamp01(c.g * factor), Mathf.Clamp01(c.b * factor), c.a);
        }

        public static Color Hex(string hex)
        {
            Color c;
            return ColorUtility.TryParseHtmlString(hex, out c) ? c : Color.magenta;
        }
    }

    /// <summary>
    /// Matériaux créés à l'exécution. Fonctionne avec URP (recommandé pour mobile) et le pipeline intégré.
    /// Pour les builds, le menu « BALULA MOTO > Préparer le projet » crée des matériaux de base dans Resources
    /// afin que les shaders soient inclus.
    /// </summary>
    public static class Mats
    {
        private static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();
        private static Material litBase, unlitBase;
        private static bool initialized;

        private static void Init()
        {
            if (initialized) return;
            initialized = true;
            litBase = Resources.Load<Material>("BalulaMoto/MatLit");
            unlitBase = Resources.Load<Material>("BalulaMoto/MatUnlit");
            if (litBase == null)
            {
                Shader s = null;
                if (GraphicsSettings.currentRenderPipeline != null)
                {
                    s = Shader.Find("Universal Render Pipeline/Lit");
                    if (s == null) s = Shader.Find("HDRP/Lit");
                }
                if (s == null) s = Shader.Find("Standard");
                if (s == null) s = Shader.Find("Universal Render Pipeline/Lit");
                litBase = new Material(s);
            }
            if (unlitBase == null)
            {
                Shader s = null;
                if (GraphicsSettings.currentRenderPipeline != null) s = Shader.Find("Universal Render Pipeline/Unlit");
                if (s == null) s = Shader.Find("Unlit/Color");
                unlitBase = s != null ? new Material(s) : new Material(litBase);
            }
        }

        public static Material Lit(Color c, float smoothness = 0.2f)
        {
            Init();
            string key = "L" + ColorUtility.ToHtmlStringRGBA(c) + smoothness.ToString("0.00");
            Material m;
            if (cache.TryGetValue(key, out m) && m != null) return m;
            m = new Material(litBase);
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            m.enableInstancing = true;
            cache[key] = m;
            return m;
        }

        public static Material Emissive(Color c, float intensity)
        {
            Init();
            string key = "E" + ColorUtility.ToHtmlStringRGBA(c) + intensity.ToString("0.0");
            Material m;
            if (cache.TryGetValue(key, out m) && m != null) return m;
            m = new Material(litBase);
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            m.EnableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", c * intensity);
            cache[key] = m;
            return m;
        }

        public static Material Unlit(Color c)
        {
            Init();
            string key = "U" + ColorUtility.ToHtmlStringRGBA(c);
            Material m;
            if (cache.TryGetValue(key, out m) && m != null) return m;
            m = new Material(unlitBase);
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            cache[key] = m;
            return m;
        }

        /// <summary>Allume ou éteint une ampoule (phare, feu tricolore, lampadaire).</summary>
        public static void SetLamp(Renderer r, Color on, bool lit, float intensity = 2.5f)
        {
            if (r == null) return;
            r.sharedMaterial = lit ? Emissive(on, intensity) : Lit(ColorUtil.Shade(on, 0.25f), 0.5f);
        }

        /// <summary>Rend une surface plus brillante quand la route est mouillée.</summary>
        public static void SetWetness(Material m, float wetness)
        {
            if (m == null) return;
            float s = Mathf.Lerp(0.15f, 0.75f, wetness);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", s);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", s);
        }
    }

    /// <summary>Primitives et petites constructions procédurales (maquettes volumétriques).</summary>
    public static class Prims
    {
        private static Mesh cube, cylinder, sphere, capsule;

        private static Mesh Builtin(PrimitiveType type)
        {
            GameObject tmp = GameObject.CreatePrimitive(type);
            tmp.SetActive(false);
            Mesh m = tmp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(tmp);
            return m;
        }

        public static Mesh Cube { get { if (cube == null) cube = Builtin(PrimitiveType.Cube); return cube; } }
        public static Mesh Cylinder { get { if (cylinder == null) cylinder = Builtin(PrimitiveType.Cylinder); return cylinder; } }
        public static Mesh Sphere { get { if (sphere == null) sphere = Builtin(PrimitiveType.Sphere); return sphere; } }
        public static Mesh Capsule { get { if (capsule == null) capsule = Builtin(PrimitiveType.Capsule); return capsule; } }

        public static GameObject MeshObject(Transform parent, string name, Mesh mesh, Material mat)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            return go;
        }

        public static GameObject Box(Transform parent, string name, Vector3 localPos, Vector3 size, Color color, bool collider = false, float smooth = 0.2f)
        {
            GameObject go = MeshObject(parent, name, Cube, Mats.Lit(color, smooth));
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            if (collider) go.AddComponent<BoxCollider>();
            return go;
        }

        public static GameObject BoxRot(Transform parent, string name, Vector3 localPos, Vector3 size, Vector3 euler, Color color, bool collider = false)
        {
            GameObject go = Box(parent, name, localPos, size, color, collider);
            go.transform.localRotation = Quaternion.Euler(euler);
            return go;
        }

        /// <summary>Cylindre vertical (diamètre, hauteur).</summary>
        public static GameObject Cyl(Transform parent, string name, Vector3 localPos, float diameter, float height, Color color, bool collider = false)
        {
            GameObject go = MeshObject(parent, name, Cylinder, Mats.Lit(color, 0.3f));
            go.transform.localPosition = localPos;
            go.transform.localScale = new Vector3(diameter, height * 0.5f, diameter);
            if (collider) go.AddComponent<BoxCollider>();
            return go;
        }

        /// <summary>Cylindre couché selon l'axe X (roues, tubes transversaux).</summary>
        public static GameObject CylX(Transform parent, string name, Vector3 localPos, float diameter, float width, Color color)
        {
            GameObject go = MeshObject(parent, name, Cylinder, Mats.Lit(color, 0.35f));
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            go.transform.localScale = new Vector3(diameter, width * 0.5f, diameter);
            return go;
        }

        /// <summary>Tube entre deux points locaux.</summary>
        public static GameObject Tube(Transform parent, string name, Vector3 a, Vector3 b, float diameter, Color color)
        {
            GameObject go = MeshObject(parent, name, Cylinder, Mats.Lit(color, 0.45f));
            Vector3 mid = (a + b) * 0.5f;
            Vector3 dir = b - a;
            go.transform.localPosition = mid;
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);
            go.transform.localScale = new Vector3(diameter, dir.magnitude * 0.5f, diameter);
            return go;
        }

        public static GameObject Ball(Transform parent, string name, Vector3 localPos, Vector3 size, Color color)
        {
            GameObject go = MeshObject(parent, name, Sphere, Mats.Lit(color, 0.3f));
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            return go;
        }

        public static GameObject Pill(Transform parent, string name, Vector3 localPos, Vector3 size, Color color)
        {
            GameObject go = MeshObject(parent, name, Capsule, Mats.Lit(color, 0.2f));
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            return go;
        }

        /// <summary>Texte 3D (panneaux, enseignes, plaques). Double face si demandé.</summary>
        public static GameObject Label(Transform parent, string text, Vector3 localPos, float charSize, Color color, bool twoSided)
        {
            GameObject root = new GameObject("Texte");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPos;
            MakeText(root.transform, text, charSize, color, Quaternion.identity);
            if (twoSided) MakeText(root.transform, text, charSize, color, Quaternion.Euler(0f, 180f, 0f));
            return root;
        }

        private static void MakeText(Transform parent, string text, float charSize, Color color, Quaternion rot)
        {
            GameObject go = new GameObject("Face");
            go.transform.SetParent(parent, false);
            go.transform.localRotation = rot;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            TextMesh tm = go.AddComponent<TextMesh>();
            tm.font = Fonts.Default;
            tm.text = text;
            tm.fontSize = 64;
            tm.characterSize = charSize;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            if (Fonts.Default != null) mr.sharedMaterial = Fonts.Default.material;
            mr.shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>Prisme extrudé selon X à partir d'un profil (Y,Z) — ralentisseurs, rampes.</summary>
        public static Mesh Prism(Vector2[] profile, float length)
        {
            List<Vector3> v = new List<Vector3>();
            List<int> t = new List<int>();
            float h = length * 0.5f;
            int n = profile.Length;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = profile[i], b = profile[(i + 1) % n];
                int s = v.Count;
                v.Add(new Vector3(-h, a.x, a.y));
                v.Add(new Vector3(h, a.x, a.y));
                v.Add(new Vector3(h, b.x, b.y));
                v.Add(new Vector3(-h, b.x, b.y));
                t.Add(s); t.Add(s + 1); t.Add(s + 2);
                t.Add(s); t.Add(s + 2); t.Add(s + 3);
                t.Add(s); t.Add(s + 2); t.Add(s + 1);
                t.Add(s); t.Add(s + 3); t.Add(s + 2);
            }
            Mesh m = new Mesh();
            m.name = "Prisme";
            m.SetVertices(v);
            m.SetTriangles(t, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        public static void Paint(GameObject go, Color c)
        {
            Renderer r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = Mats.Lit(c);
        }

        public static void NoShadows(GameObject go)
        {
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
        }

        public static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayerRecursive(child.gameObject, layer);
        }
    }

    public static class Fonts
    {
        private static Font font;

        public static Font Default
        {
            get
            {
                if (font == null)
                {
#if UNITY_2022_2_OR_NEWER
                    font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
                    font = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
                }
                return font;
            }
        }
    }

    public static class WorldGeo
    {
        public static Vector3 ToWorld(PlaceRef p, float y)
        {
            return new Vector3((float)p.X, (float)p.Y + y, (float)p.Z);
        }

        public static PlaceRef Place(string id, string name, string district, string category, Vector3 pos, int weight, bool stop, string zone = "kinshasa")
        {
            return new PlaceRef { Id = id, Name = name, District = district, Category = category, X = pos.x, Y = pos.y, Z = pos.z, Weight = weight, IsStop = stop, Zone = zone };
        }
    }
}
