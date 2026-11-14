// BALULA MOTO — modèles 3D posés dans la ville : décor (Resources/BalulaMoto/Decor : maisons, immeubles, barrières,
// mobilier urbain) et véhicules garés (Resources/BalulaMoto/Vehicules). Chaque modèle est chargé une fois ; ses
// matériaux deviennent des matériaux du jeu (couleur et texture du fichier). Les modèles d'une case de ville sont
// fusionnés par matériau (une passe de dessin par matériau et par case) : léger pour les téléphones.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BalulaMoto.Runtime
{
    public class DecorModel
    {
        public string Name;
        public readonly List<Mesh> Meshes = new List<Mesh>();
        public readonly List<int> SubMesh = new List<int>();
        public readonly List<Material> Mats = new List<Material>();
        public readonly List<string> MatNames = new List<string>();
        public readonly List<string> PartNames = new List<string>();
        public readonly List<Matrix4x4> Local = new List<Matrix4x4>();
        public Bounds Bounds;
        public int Vertices;
        public bool Valid { get { return Meshes.Count > 0; } }
    }

    public static class DecorModels
    {
        private static readonly Dictionary<string, DecorModel> cache = new Dictionary<string, DecorModel>();
        private static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();

        /// <summary>Modèle « Decor/Cottage », « Vehicules/Voiture_Coupe »… ; null s'il manque.</summary>
        public static DecorModel Get(string path)
        {
            DecorModel m;
            if (cache.TryGetValue(path, out m)) return m;
            m = Load(path);
            cache[path] = m;
            return m;
        }

        public static bool Has(string path) { return Get(path) != null; }

        private static DecorModel Load(string path)
        {
            GameObject prefab = Resources.Load<GameObject>("BalulaMoto/" + path);
            if (prefab == null) { Debug.LogWarning("[BalulaMoto] Modèle absent : " + path); return null; }
            int slash = path.LastIndexOf('/');
            string folder = slash >= 0 ? path.Substring(0, slash + 1) : "";
            string shortName = slash >= 0 ? path.Substring(slash + 1) : path;
            DecorModel d = new DecorModel { Name = shortName };
            Matrix4x4 toModel = prefab.transform.worldToLocalMatrix;
            bool first = true;
            foreach (MeshFilter mf in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = mf.sharedMesh;
                if (mesh == null) continue;
                if (!mesh.isReadable) { Debug.LogWarning("[BalulaMoto] Maillage non lisible (" + path + ") : posé à part."); }
                MeshRenderer mr = mf.GetComponent<MeshRenderer>();
                Material[] src = mr != null ? mr.sharedMaterials : new Material[0];
                Matrix4x4 m = toModel * mf.transform.localToWorldMatrix;
                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    Material sm = s < src.Length ? src[s] : null;
                    string mn = sm != null ? sm.name : "Defaut";
                    d.Meshes.Add(mesh);
                    d.SubMesh.Add(s);
                    d.Local.Add(m);
                    d.MatNames.Add(mn);
                    d.PartNames.Add(mf.name);
                    d.Mats.Add(GameMaterial(folder, shortName, sm));
                }
                d.Vertices += mesh.vertexCount;
                Bounds b = TransformBounds(mesh.bounds, m);
                if (first) { d.Bounds = b; first = false; }
                else d.Bounds.Encapsulate(b);
            }
            return d.Valid ? d : null;
        }

        private static string Safe(string s)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (char c in s) sb.Append(char.IsLetterOrDigit(c) ? c : '_');
            return sb.ToString();
        }

        /// <summary>Matériau du jeu pour un matériau du fichier : couleur (Kd) et texture (map_Kd) du modèle.</summary>
        private static Material GameMaterial(string folder, string model, Material src)
        {
            string name = src != null ? src.name : "Defaut";
            string key = folder + model + "/" + name;
            Material m;
            if (mats.TryGetValue(key, out m) && m != null) return m;
            Color c = src != null && src.HasProperty("_Color") ? src.color : Color.white;
            c.a = 1f;
            Texture tex = src != null && src.HasProperty("_MainTex") ? src.mainTexture : null;
            if (tex == null) tex = Resources.Load<Texture2D>("BalulaMoto/" + folder + model + "_" + Safe(name));
            if (tex == null) tex = Resources.Load<Texture2D>("BalulaMoto/" + folder + Safe(name));
            string ln = name.ToLowerInvariant();
            float smooth = ln.Contains("chrom") || ln.Contains("silver") || ln.Contains("vitre") || ln.Contains("glass") ? 0.75f : 0.12f;
            m = new Material(Mats.Lit(c, smooth)) { name = "Decor_" + model + "_" + name };
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (tex != null)
            {
                m.mainTexture = tex;
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            }
            m.enableInstancing = false;
            mats[key] = m;
            return m;
        }

        public static Bounds TransformBounds(Bounds b, Matrix4x4 m)
        {
            Vector3 c = m.MultiplyPoint3x4(b.center);
            Vector3 e = b.extents;
            Vector3 ax = m.MultiplyVector(new Vector3(e.x, 0f, 0f)), ay = m.MultiplyVector(new Vector3(0f, e.y, 0f)), az = m.MultiplyVector(new Vector3(0f, 0f, e.z));
            Vector3 ext = new Vector3(Mathf.Abs(ax.x) + Mathf.Abs(ay.x) + Mathf.Abs(az.x), Mathf.Abs(ax.y) + Mathf.Abs(ay.y) + Mathf.Abs(az.y), Mathf.Abs(ax.z) + Mathf.Abs(ay.z) + Mathf.Abs(az.z));
            return new Bounds(c, ext * 2f);
        }

        /// <summary>Pose un modèle seul (objets séparés, pour les modèles dont les maillages ne sont pas lisibles).</summary>
        public static GameObject Place(DecorModel model, Transform parent, Matrix4x4 world, string name)
        {
            if (model == null) return null;
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);
            for (int i = 0; i < model.Meshes.Count; i++)
            {
                GameObject g = new GameObject(model.PartNames[i]);
                g.transform.SetParent(root.transform, false);
                Matrix4x4 m = world * model.Local[i];
                g.transform.localPosition = m.GetColumn(3);
                g.transform.localRotation = m.rotation;
                g.transform.localScale = m.lossyScale;
                g.AddComponent<MeshFilter>().sharedMesh = model.Meshes[i];
                MeshRenderer r = g.AddComponent<MeshRenderer>();
                Material[] arr = new Material[model.Meshes[i].subMeshCount];
                for (int s = 0; s < arr.Length; s++) arr[s] = model.Mats[i];
                r.sharedMaterials = arr;
            }
            return root;
        }
    }

    /// <summary>Lot de modèles d'une case de ville, fusionnés par matériau au moment de l'émission.</summary>
    public class DecorBatch
    {
        private readonly Dictionary<Material, List<CombineInstance>> parts = new Dictionary<Material, List<CombineInstance>>();
        private readonly Dictionary<Material, int> verts = new Dictionary<Material, int>();
        private readonly List<KeyValuePair<DecorModel, Matrix4x4>> unreadable = new List<KeyValuePair<DecorModel, Matrix4x4>>();
        public int Instances { get; private set; }

        /// <summary>Ajoute un modèle. paintMat remplace les matériaux de peinture (pièces « _Peinture_ » ou matériau
        /// « Peinture ») : couleur propre à chaque voiture ou moto garée.</summary>
        public void Add(DecorModel m, Matrix4x4 world, Material paintMat = null)
        {
            if (m == null) return;
            Instances++;
            if (!m.Meshes[0].isReadable) { unreadable.Add(new KeyValuePair<DecorModel, Matrix4x4>(m, world)); return; }
            for (int i = 0; i < m.Meshes.Count; i++)
            {
                Material mat = m.Mats[i];
                if (paintMat != null && (m.MatNames[i] == "Peinture" || m.PartNames[i].Contains("_Peinture_"))) mat = paintMat;
                List<CombineInstance> l;
                if (!parts.TryGetValue(mat, out l)) { l = new List<CombineInstance>(); parts[mat] = l; verts[mat] = 0; }
                l.Add(new CombineInstance { mesh = m.Meshes[i], subMeshIndex = m.SubMesh[i], transform = world * m.Local[i] });
                verts[mat] += m.Meshes[i].vertexCount;
            }
        }

        public void Emit(Transform parent, string name, bool castShadows)
        {
            foreach (KeyValuePair<Material, List<CombineInstance>> kv in parts)
            {
                // Lots trop gros : découpés pour rester sous la limite des index 16 bits des vieux téléphones
                List<CombineInstance> list = kv.Value;
                int start = 0;
                while (start < list.Count)
                {
                    int count = 0, v = 0;
                    while (start + count < list.Count && (count == 0 || v + list[start + count].mesh.vertexCount < 60000))
                    {
                        v += list[start + count].mesh.vertexCount;
                        count++;
                    }
                    CombineInstance[] arr = list.GetRange(start, count).ToArray();
                    start += count;
                    Mesh mesh = new Mesh { name = name };
                    if (v > 65000) mesh.indexFormat = IndexFormat.UInt32;
                    mesh.CombineMeshes(arr, true, true);
                    mesh.RecalculateBounds();
                    mesh.UploadMeshData(true);
                    GameObject g = new GameObject(name);
                    g.transform.SetParent(parent, false);
                    g.AddComponent<MeshFilter>().sharedMesh = mesh;
                    MeshRenderer r = g.AddComponent<MeshRenderer>();
                    r.sharedMaterial = kv.Key;
                    if (!castShadows) r.shadowCastingMode = ShadowCastingMode.Off;
                    g.AddComponent<MeshCleanup>().Target = mesh;
                }
            }
            foreach (KeyValuePair<DecorModel, Matrix4x4> u in unreadable) DecorModels.Place(u.Key, parent, u.Value, name);
            parts.Clear();
            verts.Clear();
            unreadable.Clear();
            Instances = 0;
        }
    }
}
