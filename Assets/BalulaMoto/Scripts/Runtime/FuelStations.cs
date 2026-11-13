// BALULA MOTO — stations-service : auvent blanc à bandeau rouge, îlots de pompes, distributeurs, boutique vitrée,
// totem avec le prix du litre, éclairage de nuit. Marque FICTIVE « BALULA ÉNERGIE » (aucun logo ni nom de
// compagnie réelle). La station est posée au bord de la rue la plus proche du point OpenStreetMap, face à la rue ;
// son emprise est calculée à l'import de la carte pour que les bâtiments OSM et les parcelles générées l'évitent.
using System.Collections.Generic;
using UnityEngine;

namespace BalulaMoto.Runtime
{
    public static class FuelStations
    {
        public struct Zone { public Vector2 C; public float R; }

        /// <summary>Emprises des stations construites (centre, rayon) : les parcelles générées les évitent.</summary>
        public static readonly List<Zone> Zones = new List<Zone>();

        private static readonly Color White = new Color(0.95f, 0.95f, 0.93f);
        private static readonly Color Red = new Color(0.84f, 0.08f, 0.1f);
        private static readonly Color DarkRed = new Color(0.5f, 0.04f, 0.05f);
        private static readonly Color Concrete = new Color(0.6f, 0.6f, 0.58f);
        private static readonly Color Curb = new Color(0.78f, 0.78f, 0.75f);
        private static readonly Color Dark = new Color(0.07f, 0.08f, 0.1f);
        private static readonly Color Glass = new Color(0.1f, 0.2f, 0.24f);
        private static readonly Color Steel = new Color(0.55f, 0.57f, 0.6f);
        private static readonly Color Green = new Color(0.22f, 0.45f, 0.16f);
        private static readonly Color LampOff = new Color(0.9f, 0.92f, 0.94f);
        private static Mesh quad;
        private static Material bannerMat, logoMat, shopMat;

        // ------------------------------------------------------------------
        // Emplacement (calculé à l'import, ou au chargement pour une ancienne carte)
        // ------------------------------------------------------------------

        /// <summary>Choisit la rue la plus proche (de préférence une grande avenue) et place l'avant du terrain
        /// au bord de la chaussée, du côté du point de la station. Faux si aucune rue à moins de 80 m.</summary>
        public static bool Plan(List<ImportedRoad> roads, Vector2 p, out Vector2 origin, out Vector2 normal, out bool big)
        {
            origin = p;
            normal = Vector2.up;
            big = false;
            if (roads == null) return false;
            float bestScore = float.MaxValue;
            Vector2 bestQ = p, bestDir = Vector2.right;
            float bestHw = 3f;
            int bestRank = 0;
            for (int r = 0; r < roads.Count; r++)
            {
                ImportedRoad road = roads[r];
                if (road == null || road.rank < 2 || road.pts.Count < 2) continue;
                List<Vector2> pts = road.pts;
                for (int i = 0; i < pts.Count - 1; i++)
                {
                    Vector2 a = pts[i], b = pts[i + 1];
                    if (Mathf.Min(a.x, b.x) > p.x + 80f || Mathf.Max(a.x, b.x) < p.x - 80f || Mathf.Min(a.y, b.y) > p.y + 80f || Mathf.Max(a.y, b.y) < p.y - 80f) continue;
                    Vector2 ab = b - a;
                    float len2 = ab.sqrMagnitude;
                    if (len2 < 4f) continue;
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
                    Vector2 q = a + ab * t;
                    float d = Vector2.Distance(p, q);
                    if (d > 80f) continue;
                    // Une grande avenue un peu plus loin est préférée à une ruelle collée à la station
                    float score = Mathf.Max(0f, d - road.width * 0.5f) - road.rank * 2.5f;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestQ = q;
                        bestDir = ab / Mathf.Sqrt(len2);
                        bestHw = road.width * 0.5f;
                        bestRank = road.rank;
                    }
                }
            }
            if (bestScore == float.MaxValue) return false;
            Vector2 left = new Vector2(-bestDir.y, bestDir.x);
            Vector2 rel = p - bestQ;
            float side = Vector2.Dot(rel, left);
            normal = Mathf.Abs(side) < 0.5f ? left : (side > 0f ? left : -left);
            origin = bestQ + normal * (bestHw + 1.6f);
            big = bestRank >= 5;
            return true;
        }

        public static Vector2 LotSize(bool big) { return big ? new Vector2(34f, 31f) : new Vector2(26f, 27f); }

        /// <summary>Vrai si le point est sur le terrain de la station (marge en mètres).</summary>
        public static bool InLot(Vector2 origin, Vector2 normal, Vector2 size, Vector2 p, float margin)
        {
            Vector2 x = new Vector2(normal.y, -normal.x);
            Vector2 d = p - origin;
            float u = Vector2.Dot(d, x), v = Vector2.Dot(d, normal);
            return Mathf.Abs(u) <= size.x * 0.5f + margin && v >= -margin && v <= size.y + margin;
        }

        // ------------------------------------------------------------------
        // Construction
        // ------------------------------------------------------------------

        /// <summary>Construit la station d'un lieu « carburant ». Retourne le centre de l'auvent (zone « faire le plein »).</summary>
        public static Vector3 Build(Transform parent, ImportedPlace place, ImportedWorld map, long pricePerLitre)
        {
            Vector2 origin, normal;
            bool big;
            if (place.stationSize.x > 1f)
            {
                origin = new Vector2(place.stationOrigin.x, place.stationOrigin.z);
                float yr = place.stationYaw * Mathf.Deg2Rad;
                normal = new Vector2(Mathf.Sin(yr), Mathf.Cos(yr));
                big = place.stationSize.x > 30f;
            }
            else if (!Plan(map != null ? map.roads : null, new Vector2(place.position.x, place.position.z), out origin, out normal, out big))
            {
                origin = new Vector2(place.position.x, place.position.z);
                normal = Vector2.up;
                big = false;
            }
            return Build(parent, place.name, new Vector3(origin.x, 0f, origin.y), new Vector3(normal.x, 0f, normal.y), big, pricePerLitre);
        }

        public static Vector3 Build(Transform parent, string name, Vector3 origin, Vector3 normal, bool big, long pricePerLitre)
        {
            Vector2 size = LotSize(big);
            float W = size.x, D = size.y;
            GameObject rootGo = new GameObject("Station_" + name);
            Transform root = rootGo.transform;
            root.SetParent(parent, false);
            root.position = origin;
            root.rotation = Quaternion.LookRotation(normal.sqrMagnitude > 0.01f ? normal.normalized : Vector3.forward, Vector3.up);

            // Dalle de béton (visuelle, sans collision : on roule sur le sol)
            Prims.Box(root, "Dalle", new Vector3(0f, 0.035f, D * 0.5f), new Vector3(W, 0.06f, D), Concrete, false, 0.15f);
            // Bordures basses et jardinières sur les côtés (entrée et sortie côté rue)
            foreach (float sx in new[] { -1f, 1f })
            {
                Prims.Box(root, "Jardiniere", new Vector3(sx * (W * 0.5f - 0.4f), 0.25f, D * 0.5f + 2.5f), new Vector3(0.8f, 0.5f, D - 6f), Curb, true);
                Prims.Box(root, "Plantes", new Vector3(sx * (W * 0.5f - 0.4f), 0.58f, D * 0.5f + 2.5f), new Vector3(0.65f, 0.2f, D - 6.4f), Green);
            }

            // Auvent
            float cz = D * 0.42f, cw = big ? 24f : 16f, cd = 12f, ch = 5.6f, thick = 0.9f;
            float bottom = ch - thick * 0.5f;
            Prims.Box(root, "Auvent", new Vector3(0f, ch, cz), new Vector3(cw, thick, cd), White, false, 0.3f);
            // Bande rouge tout autour (comme sur les stations de Kinshasa), bandeau de marque au centre devant et derrière
            Prims.Box(root, "BandeAvant", new Vector3(0f, bottom + 0.18f, cz - cd * 0.5f - 0.02f), new Vector3(cw + 0.05f, 0.33f, 0.05f), Red);
            Prims.Box(root, "BandeArriere", new Vector3(0f, bottom + 0.18f, cz + cd * 0.5f + 0.02f), new Vector3(cw + 0.05f, 0.33f, 0.05f), Red);
            Prims.Box(root, "BandeGauche", new Vector3(-cw * 0.5f - 0.02f, bottom + 0.18f, cz), new Vector3(0.05f, 0.33f, cd + 0.05f), Red);
            Prims.Box(root, "BandeDroite", new Vector3(cw * 0.5f + 0.02f, bottom + 0.18f, cz), new Vector3(0.05f, 0.33f, cd + 0.05f), Red);
            Prims.Box(root, "Liseré", new Vector3(0f, bottom + 0.36f, cz - cd * 0.5f - 0.025f), new Vector3(cw + 0.06f, 0.04f, 0.05f), DarkRed);
            Quad(root, "Bandeau", new Vector3(0f, ch - 0.02f, cz - cd * 0.5f - 0.06f), new Vector2(Mathf.Min(cw * 0.55f, 11f), 0.86f), Vector3.back, Banner());
            Quad(root, "BandeauArriere", new Vector3(0f, ch - 0.02f, cz + cd * 0.5f + 0.06f), new Vector2(Mathf.Min(cw * 0.55f, 11f), 0.86f), Vector3.forward, Banner());
            // Éclairage sous l'auvent (allumé la nuit avec les lampadaires)
            for (int ix = 0; ix < (big ? 4 : 3); ix++)
                for (int iz = 0; iz < 2; iz++)
                {
                    float x = (ix - ((big ? 4 : 3) - 1) * 0.5f) * (cw / (big ? 4.4f : 3.4f));
                    Prims.Box(root, "Lampe", new Vector3(x, bottom - 0.03f, cz + (iz == 0 ? -2.8f : 2.8f)), new Vector3(1.5f, 0.05f, 0.5f), LampOff, false, 0.5f);
                }

            // Îlots de pompes (rangées parallèles à la rue, piliers de l'auvent posés dessus)
            float[] rows = { cz - 3.3f, cz + 3.3f };
            float[] cols = big ? new[] { -6.2f, 6.2f } : new[] { 0f };
            foreach (float rz in rows)
                foreach (float rx in cols)
                {
                    Vector3 c = new Vector3(rx, 0f, rz);
                    Prims.Box(root, "Ilot", c + new Vector3(0f, 0.12f, 0f), new Vector3(5.6f, 0.24f, 1.3f), Curb, true);
                    Prims.Box(root, "IlotBord", c + new Vector3(0f, 0.245f, 0f), new Vector3(5.65f, 0.02f, 1.35f), Red);
                    Prims.Box(root, "Pilier", c + new Vector3(0f, 0.24f + (bottom - 0.24f) * 0.5f, 0f), new Vector3(0.45f, bottom - 0.24f, 0.45f), White, true, 0.4f);
                    Prims.Box(root, "PilierBas", c + new Vector3(0f, 0.24f + 0.45f, 0f), new Vector3(0.47f, 0.9f, 0.47f), Red);
                    foreach (float px in new[] { -1.75f, 1.75f }) Dispenser(root, c + new Vector3(px, 0.24f, 0f));
                    Prims.Cyl(root, "Poubelle", c + new Vector3(2.55f, 0.24f + 0.35f, 0.38f), 0.36f, 0.7f, Steel);
                }

            // Boutique vitrée au fond du terrain
            float sw = big ? 13f : 10f, sd = 7f, sh = 3.9f, sz = D - sd * 0.5f - 0.6f;
            Prims.Box(root, "Boutique", new Vector3(0f, sh * 0.5f, sz), new Vector3(sw, sh, sd), White, true, 0.25f);
            Prims.Box(root, "Toit", new Vector3(0f, sh + 0.15f, sz), new Vector3(sw + 0.7f, 0.3f, sd + 0.7f), new Color(0.42f, 0.43f, 0.45f));
            float front = sz - sd * 0.5f - 0.03f;
            Prims.Box(root, "Vitrine", new Vector3(-sw * 0.12f, 1.45f, front), new Vector3(sw * 0.62f, 2.4f, 0.06f), Glass, false, 0.9f);
            Prims.Box(root, "Porte", new Vector3(sw * 0.34f, 1.15f, front - 0.01f), new Vector3(1.6f, 2.3f, 0.06f), new Color(0.14f, 0.26f, 0.3f), false, 0.9f);
            Prims.Box(root, "Cadre", new Vector3(sw * 0.34f, 2.33f, front - 0.02f), new Vector3(1.7f, 0.08f, 0.07f), Steel);
            Quad(root, "Enseigne", new Vector3(0f, 3.3f, front - 0.05f), new Vector2(sw * 0.92f, sw * 0.92f * 0.156f), Vector3.back, Shop());
            Prims.Box(root, "Socle", new Vector3(0f, 0.08f, front - 0.9f), new Vector3(sw + 0.4f, 0.16f, 1.8f), Curb);
            // Présentoir d'huiles moteur et bouteille de gaz devant la boutique
            Prims.Box(root, "Presentoir", new Vector3(-sw * 0.5f + 1.2f, 0.75f, front - 0.6f), new Vector3(1.4f, 1.2f, 0.5f), Red, true);
            for (int i = 0; i < 6; i++)
                Prims.Box(root, "Bidon", new Vector3(-sw * 0.5f + 0.7f + (i % 3) * 0.48f, 1.48f + (i / 3) * 0.0f, front - 0.6f + (i / 3 == 0 ? -0.1f : 0.12f)), new Vector3(0.22f, 0.3f, 0.15f), i % 2 == 0 ? new Color(0.95f, 0.75f, 0.05f) : new Color(0.1f, 0.35f, 0.7f));
            // Borne air / eau
            Prims.Box(root, "BorneAir", new Vector3(W * 0.5f - 2.2f, 0.65f, D * 0.78f), new Vector3(0.45f, 1.3f, 0.35f), new Color(0.15f, 0.35f, 0.75f), true);

            // Totem au bord de la rue : logo et prix du litre (prix du jeu, valeur des données économiques)
            Vector3 tp = new Vector3(-W * 0.5f + 2.4f, 0f, 1.0f);
            Prims.Box(root, "TotemSocle", tp + new Vector3(0f, 0.25f, 0f), new Vector3(1.9f, 0.5f, 0.8f), Concrete, true);
            Prims.Box(root, "Totem", tp + new Vector3(0f, 0.5f + 3.2f, 0f), new Vector3(1.5f, 6.4f, 0.4f), White, true, 0.3f);
            Prims.Box(root, "TotemG", tp + new Vector3(-0.78f, 0.5f + 3.2f, 0f), new Vector3(0.08f, 6.4f, 0.42f), Red);
            Prims.Box(root, "TotemD", tp + new Vector3(0.78f, 0.5f + 3.2f, 0f), new Vector3(0.08f, 6.4f, 0.42f), Red);
            Prims.Box(root, "TotemPrix", tp + new Vector3(0f, 3.4f, 0f), new Vector3(1.32f, 1.7f, 0.44f), Dark, false, 0.6f);
            Quad(root, "TotemLogo", tp + new Vector3(0f, 5.75f, -0.23f), new Vector2(1.3f, 1.3f), Vector3.back, Logo());
            Quad(root, "TotemLogo2", tp + new Vector3(0f, 5.75f, 0.23f), new Vector2(1.3f, 1.3f), Vector3.forward, Logo());
            string price = pricePerLitre > 0 ? Money(pricePerLitre) : "—";
            foreach (float face in new[] { -1f, 1f })
            {
                Quaternion q = face < 0f ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f);
                GameObject l1 = Prims.Label(root, "ESSENCE", tp + new Vector3(0f, 3.85f, face * 0.235f), 0.032f, new Color(1f, 0.82f, 0.1f), false);
                l1.transform.localRotation = q;
                GameObject l2 = Prims.Label(root, price, tp + new Vector3(0f, 3.25f, face * 0.235f), 0.05f, Color.white, false);
                l2.transform.localRotation = q;
                GameObject l3 = Prims.Label(root, "le litre", tp + new Vector3(0f, 2.78f, face * 0.235f), 0.022f, new Color(0.8f, 0.8f, 0.8f), false);
                l3.transform.localRotation = q;
            }

            Combine(root);
            Vector3 service = root.TransformPoint(new Vector3(0f, 0f, cz));
            Vector3 center = root.TransformPoint(new Vector3(0f, 0f, D * 0.5f));
            Zones.Add(new Zone { C = new Vector2(center.x, center.z), R = Mathf.Max(W, D) * 0.62f });
            // Masquée de loin (téléphones)
            LODGroup lg = rootGo.AddComponent<LODGroup>();
            lg.SetLODs(new[] { new LOD(0.025f, rootGo.GetComponentsInChildren<Renderer>()) });
            lg.localReferencePoint = new Vector3(0f, 3f, D * 0.5f);
            lg.size = Mathf.Max(W, D);
            return service;
        }

        private static void Dispenser(Transform root, Vector3 p)
        {
            Prims.Box(root, "PompeSocle", p + new Vector3(0f, 0.06f, 0f), new Vector3(1.05f, 0.12f, 0.62f), Steel);
            Prims.Box(root, "Pompe", p + new Vector3(0f, 0.12f + 0.85f, 0f), new Vector3(0.9f, 1.7f, 0.5f), White, true, 0.45f);
            Prims.Box(root, "PompeTete", p + new Vector3(0f, 0.12f + 1.7f + 0.2f, 0f), new Vector3(0.96f, 0.4f, 0.56f), Red, false, 0.5f);
            foreach (float s in new[] { -1f, 1f })
            {
                Prims.Box(root, "Ecran", p + new Vector3(0f, 1.42f, s * 0.255f), new Vector3(0.62f, 0.38f, 0.02f), Dark, false, 0.8f);
                Prims.Box(root, "Chiffres", p + new Vector3(-0.12f, 1.47f, s * 0.262f), new Vector3(0.3f, 0.09f, 0.01f), new Color(0.2f, 0.85f, 0.35f));
                Prims.Box(root, "Pistolet", p + new Vector3(-0.47f, 0.95f, s * 0.12f), new Vector3(0.08f, 0.26f, 0.1f), Dark);
                Prims.Box(root, "Pistolet2", p + new Vector3(0.47f, 0.95f, s * 0.12f), new Vector3(0.08f, 0.26f, 0.1f), new Color(0.1f, 0.5f, 0.15f));
                Prims.Tube(root, "Tuyau", p + new Vector3(-0.47f, 1.95f, s * 0.12f), p + new Vector3(-0.52f, 1.08f, s * 0.12f), 0.035f, Dark);
                Prims.Tube(root, "Tuyau2", p + new Vector3(0.47f, 1.95f, s * 0.12f), p + new Vector3(0.52f, 1.08f, s * 0.12f), 0.035f, Dark);
            }
        }

        private static string Money(long v)
        {
            string s = v.ToString();
            System.Text.StringBuilder b = new System.Text.StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                if (i > 0 && (s.Length - i) % 3 == 0) b.Append(' ');
                b.Append(s[i]);
            }
            return b + " FC";
        }

        // ------------------------------------------------------------------
        // Textures (marque fictive) et regroupement des maillages
        // ------------------------------------------------------------------

        private static Mesh QuadMesh()
        {
            if (quad == null)
            {
                GameObject tmp = GameObject.CreatePrimitive(PrimitiveType.Quad);
                tmp.SetActive(false);
                quad = tmp.GetComponent<MeshFilter>().sharedMesh;
                Object.Destroy(tmp);
            }
            return quad;
        }

        private static Material Textured(string file)
        {
            Texture2D tex = Resources.Load<Texture2D>("BalulaMoto/Station/" + file);
            Material m = new Material(Mats.Lit(Color.white, 0.35f));
            m.color = Color.white;
            if (tex != null)
            {
                m.mainTexture = tex;
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            }
            m.enableInstancing = false;
            return m;
        }

        private static Material Banner() { if (bannerMat == null) bannerMat = Textured("station_bandeau"); return bannerMat; }
        private static Material Logo() { if (logoMat == null) logoMat = Textured("station_logo"); return logoMat; }
        private static Material Shop() { if (shopMat == null) shopMat = Textured("station_boutique"); return shopMat; }

        /// <summary>Panneau texturé tourné vers « facing » (le quad d'Unity se voit depuis -Z : on l'oriente en conséquence).</summary>
        private static void Quad(Transform root, string name, Vector3 localPos, Vector2 size, Vector3 facing, Material m)
        {
            GameObject go = Prims.MeshObject(root, name, QuadMesh(), m);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.LookRotation(-facing, Vector3.up);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
        }

        /// <summary>Regroupe les morceaux par matériau (une dizaine d'appels de dessin par station au lieu de 150) ;
        /// les boîtes de collision restent en place.</summary>
        public static void Combine(Transform root)
        {
            Dictionary<Material, List<CombineInstance>> groups = new Dictionary<Material, List<CombineInstance>>();
            Matrix4x4 toLocal = root.worldToLocalMatrix;
            List<GameObject> empty = new List<GameObject>();
            foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                MeshRenderer mr = mf.GetComponent<MeshRenderer>();
                if (mr == null || mf.sharedMesh == null || mr.sharedMaterial == null) continue;
                List<CombineInstance> list;
                if (!groups.TryGetValue(mr.sharedMaterial, out list)) { list = new List<CombineInstance>(); groups[mr.sharedMaterial] = list; }
                list.Add(new CombineInstance { mesh = mf.sharedMesh, transform = toLocal * mf.transform.localToWorldMatrix });
                if (mf.GetComponent<Collider>() == null && mf.transform.childCount == 0) empty.Add(mf.gameObject);
                else
                {
                    Object.Destroy(mr);
                    Object.Destroy(mf);
                }
            }
            foreach (GameObject g in empty) Object.Destroy(g);
            Material lamp = Mats.Lit(LampOff, 0.5f);
            foreach (KeyValuePair<Material, List<CombineInstance>> kv in groups)
            {
                Mesh mesh = new Mesh();
                mesh.name = "Station_" + kv.Key.name;
                mesh.CombineMeshes(kv.Value.ToArray(), true, true);
                mesh.RecalculateBounds();
                GameObject go = new GameObject("Station_" + kv.Key.name);
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = kv.Key;
                go.AddComponent<MeshCleanup>().Target = mesh;
                if (kv.Key == lamp)
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    WorldEnvironment.StreetLamps.Add(r);
                }
            }
        }
    }
}
