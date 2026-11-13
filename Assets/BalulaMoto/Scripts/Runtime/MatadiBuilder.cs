// BALULA MOTO — zone de Matadi (Kongo Central) : maquette jouable du Pont Maréchal (pont suspendu sur le fleuve Congo).
//
// Données utilisées (sources publiques, à confirmer avant la version finale) :
//   - longueur totale 722 m, travée centrale 520 m, pont suspendu routier à deux voies, achevé en 1983 ;
//   - coordonnées approximatives 5,824466 S ; 13,433865 E (Wikipédia) ;
//   - référence photo du créateur : REF-KCE-001 (« Le pont Maréchal à Matadi »).
// Valeurs ESTIMÉES (à vérifier) : hauteur du tablier au-dessus de l'eau (~50 m), hauteur des pylônes,
// largeur du tablier, profondeur de la poutre en treillis, relief des berges.
// La zone est une maquette de jeu placée à l'écart de la piste d'essai : ce n'est pas un relevé topographique de Matadi.
using BalulaMoto.Core;
using UnityEngine;

namespace BalulaMoto.Runtime
{
    public static class MatadiBuilder
    {
        public const string Zone = "matadi";
        public const float TotalLength = 722f;   // source : Wikipédia
        public const float MainSpan = 520f;      // source : Wikipédia
        public const float DeckHeight = 50f;     // estimation (à vérifier)
        public const float TowerAboveDeck = 52f; // estimation (à vérifier)
        private const float CableX = 9.6f;        // plan des câbles et des poutres en treillis (estimation)

        private static readonly Color Steel = new Color(0.62f, 0.65f, 0.68f);
        private static readonly Color SteelDark = new Color(0.45f, 0.48f, 0.52f);
        private static readonly Color Concrete = new Color(0.72f, 0.71f, 0.68f);
        private static readonly Color Water = new Color(0.27f, 0.36f, 0.33f);
        private static readonly Color Grass = new Color(0.33f, 0.47f, 0.2f);
        private static readonly Color Rock = new Color(0.45f, 0.42f, 0.36f);
        private static readonly Color Asphalt = new Color(0.24f, 0.24f, 0.26f);

        public static void Build(WorldContext w, Transform parent, Vector3 origin)
        {
            Transform root = new GameObject("Zone_Matadi_PontMarechal").transform;
            root.SetParent(parent, false);
            root.position = origin;
            float D = DeckHeight;
            float half = TotalLength * 0.5f;          // 361 m
            float towerZ = MainSpan * 0.5f;           // 260 m
            float plateauEnd = half + 240f;

            // ---------------- Fleuve, berges, plateaux ----------------
            Prims.Box(root, "Fleuve_Congo", new Vector3(0f, -0.1f, 0f), new Vector3(2400f, 0.2f, 2f * towerZ + 40f), Water, false, 0.85f);
            for (int side = -1; side <= 1; side += 2)
            {
                float edge = towerZ + 10f;
                float rise = half - edge;
                float len = Mathf.Sqrt(rise * rise + D * D);
                float angle = Mathf.Atan2(D, rise) * Mathf.Rad2Deg;
                GameObject slope = Prims.Box(root, "Berge", new Vector3(0f, D * 0.5f - 2f, side * (edge + rise * 0.5f)), new Vector3(2400f, 4f, len + 6f), Rock);
                slope.transform.localRotation = Quaternion.Euler(side > 0 ? -angle : angle, 0f, 0f);
                // Plateau (sol praticable à la hauteur du tablier)
                GameObject plateau = Prims.Box(root, "Plateau", new Vector3(0f, D * 0.5f - 0.05f, side * (half + 120f)), new Vector3(2400f, D, 240f), Grass, true, 0.05f);
                plateau.AddComponent<SurfaceZone>().Kind = SurfaceKind.Terre;
                // Collines au loin
                for (int i = 0; i < 9; i++)
                {
                    float x = -1000f + i * 250f;
                    float h = 40f + (i * 37 % 5) * 14f;
                    Prims.Ball(root, "Colline", new Vector3(x, D + h * 0.3f, side * (plateauEnd + 60f + (i % 3) * 40f)), new Vector3(320f, h, 220f), new Color(0.27f, 0.42f, 0.18f));
                }
            }

            // ---------------- Tablier routier (praticable) ----------------
            GameObject deck = Prims.Box(root, "Tablier_Route", new Vector3(0f, D - 0.3f, 0f), new Vector3(10f, 0.6f, TotalLength + 2f), Asphalt, true, 0.18f);
            deck.AddComponent<SurfaceZone>().Kind = SurfaceKind.Goudron;
            WorldEnvironment.WetMaterials.Add(deck.GetComponent<Renderer>().sharedMaterial);
            for (float z = -half + 4f; z < half - 4f; z += 9f)
                Prims.Box(root, "Marquage", new Vector3(0f, D + 0.006f, z), new Vector3(0.15f, 0.01f, 3f), Color.white);
            for (int s = -1; s <= 1; s += 2)
            {
                // Glissières (empêchent de tomber dans le fleuve)
                Prims.Box(root, "Glissiere", new Vector3(s * 5.4f, D + 0.6f, 0f), new Vector3(0.25f, 1.2f, TotalLength), SteelDark, true);
                Prims.Box(root, "Trottoir", new Vector3(s * 7.45f, D + 0.1f, 0f), new Vector3(3.9f, 0.4f, TotalLength), Concrete);
                Prims.Box(root, "Garde-corps", new Vector3(s * 9.3f, D + 1.1f, 0f), new Vector3(0.1f, 0.1f, TotalLength), Steel);
            }

            // ---------------- Poutre de rigidité en treillis (sous le tablier) ----------------
            const float trussDepth = 7f; // estimation
            for (int s = -1; s <= 1; s += 2)
            {
                float x = s * CableX;
                Prims.Box(root, "Membrure_haute", new Vector3(x, D - 0.4f, 0f), new Vector3(0.6f, 0.8f, TotalLength), Steel);
                Prims.Box(root, "Membrure_basse", new Vector3(x, D - trussDepth, 0f), new Vector3(0.6f, 0.8f, TotalLength), Steel);
                bool up = true;
                for (float z = -half; z < half - 1f; z += 12f)
                {
                    Prims.Box(root, "Montant", new Vector3(x, D - trussDepth * 0.5f - 0.2f, z), new Vector3(0.35f, trussDepth, 0.35f), Steel);
                    Vector3 a = new Vector3(x, up ? D - 0.5f : D - trussDepth, z);
                    Vector3 b = new Vector3(x, up ? D - trussDepth : D - 0.5f, z + 12f);
                    Prims.Tube(root, "Diagonale", a, b, 0.3f, Steel);
                    up = !up;
                }
            }
            for (float z = -half; z <= half; z += 24f)
                Prims.Box(root, "Entretoise", new Vector3(0f, D - trussDepth, z), new Vector3(CableX * 2f, 0.4f, 0.4f), SteelDark);

            // ---------------- Pylônes ----------------
            float top = D + TowerAboveDeck;
            for (int s = -1; s <= 1; s += 2)
            {
                float z = s * towerZ;
                for (int leg = -1; leg <= 1; leg += 2)
                    Prims.Box(root, "Pylone", new Vector3(leg * 11.5f, top * 0.5f - 4f, z), new Vector3(3.2f, top + 8f, 4.5f), Concrete, true, 0.1f);
                Prims.Box(root, "Traverse_haute", new Vector3(0f, top - 1.5f, z), new Vector3(26f, 3.5f, 4f), Concrete);
                Prims.Box(root, "Traverse_basse", new Vector3(0f, D - trussDepth - 2f, z), new Vector3(26f, 3f, 4f), Concrete);
                Prims.Box(root, "Socle", new Vector3(0f, 1.5f, z), new Vector3(30f, 9f, 14f), Rock);
                // Feu de balisage au sommet
                GameObject beacon = Prims.Box(root, "Balisage", new Vector3(0f, top + 0.4f, z), new Vector3(0.8f, 0.8f, 0.8f), new Color(0.6f, 0.05f, 0.05f));
                WorldEnvironment.StreetLamps.Add(beacon.GetComponent<Renderer>());
            }

            // ---------------- Câbles porteurs et suspentes ----------------
            float sagLow = D + 3f;
            for (int s = -1; s <= 1; s += 2)
            {
                float x = s * CableX;
                Vector3 prev = Vector3.zero;
                bool first = true;
                for (float z = -towerZ; z <= towerZ + 0.1f; z += 13f)
                {
                    float y = sagLow + (top - sagLow) * (z / towerZ) * (z / towerZ);
                    Vector3 p = new Vector3(x, y, z);
                    if (!first) Prims.Tube(root, "Cable", prev, p, 0.7f, SteelDark);
                    if (Mathf.Abs(z) < towerZ - 1f && y > D + 1.2f)
                        Prims.Box(root, "Suspente", new Vector3(x, (y + D) * 0.5f, z), new Vector3(0.12f, y - D, 0.12f), SteelDark);
                    prev = p;
                    first = false;
                }
                // Travées latérales jusqu'aux massifs d'ancrage
                for (int end = -1; end <= 1; end += 2)
                {
                    Vector3 a = new Vector3(x, top, end * towerZ);
                    Vector3 b = new Vector3(x, D + 1.5f, end * (half + 8f));
                    Vector3 last = a;
                    for (int k = 1; k <= 8; k++)
                    {
                        float t = k / 8f;
                        Vector3 p = Vector3.Lerp(a, b, t) + Vector3.down * (Mathf.Sin(t * Mathf.PI) * 3f);
                        Prims.Tube(root, "Cable", last, p, 0.7f, SteelDark);
                        if (k < 8 && p.y > D + 1.2f) Prims.Box(root, "Suspente", new Vector3(x, (p.y + D) * 0.5f, p.z), new Vector3(0.12f, p.y - D, 0.12f), SteelDark);
                        last = p;
                    }
                    Prims.Box(root, "Massif_ancrage", new Vector3(x, D + 1f, end * (half + 14f)), new Vector3(6f, 4f, 14f), Concrete, true);
                }
            }

            // ---------------- Routes d'accès sur les plateaux ----------------
            for (int side = -1; side <= 1; side += 2)
            {
                Road(root, new Vector3(0f, D, side * half), new Vector3(0f, D, side * (half + 232f)), 10f);
                Road(root, new Vector3(-190f, D, side * (half + 200f)), new Vector3(190f, D, side * (half + 200f)), 9f);
                GameObject plaza = Prims.Cyl(root, "Rond-point", new Vector3(0f, D + 0.02f, side * (half + 200f)), 34f, 0.1f, Asphalt, true);
                plaza.AddComponent<SurfaceZone>().Kind = SurfaceKind.Goudron;
                Prims.Cyl(root, "Terre-plein", new Vector3(0f, D + 0.25f, side * (half + 200f)), 9f, 0.5f, Grass, true);
                for (float z = half + 20f; z < half + 190f; z += 34f)
                {
                    Lamp(root, new Vector3(6.5f, D, side * z));
                    Lamp(root, new Vector3(-6.5f, D, side * (z + 17f)));
                }
            }
            bool leftSide = true;
            for (float z = -half + 30f; z < half; z += 60f) { Lamp(root, new Vector3(leftSide ? -6.2f : 6.2f, D, z)); leftSide = !leftSide; }

            // Limites : parapets au bord des falaises, murs invisibles en bout de plateau
            for (int side = -1; side <= 1; side += 2)
            {
                for (int k = -1; k <= 1; k += 2)
                    Prims.Box(root, "Parapet", new Vector3(k * 605f, D + 0.55f, side * (half + 0.6f)), new Vector3(1190f, 1.1f, 0.5f), Concrete, true);
                Wall(root, new Vector3(0f, D + 2f, side * (half + 239f)), new Vector3(2400f, 4f, 1f));
                Wall(root, new Vector3(-1199f, D + 2f, side * (half + 120f)), new Vector3(1f, 4f, 240f));
                Wall(root, new Vector3(1199f, D + 2f, side * (half + 120f)), new Vector3(1f, 4f, 240f));
            }

            // Maisons sur la colline côté Matadi, quelques bâtiments sur la rive droite
            System.Random r = new System.Random(1983);
            Color[] walls = { new Color(0.93f, 0.88f, 0.76f), new Color(0.78f, 0.84f, 0.9f), new Color(0.96f, 0.76f, 0.62f), new Color(0.9f, 0.9f, 0.86f) };
            for (int i = 0; i < 60; i++)
            {
                int side = i < 42 ? -1 : 1;
                float x = (float)(r.NextDouble() * 760.0 - 380.0);
                float z = side * (half + 30f + (float)r.NextDouble() * 190f);
                if (Mathf.Abs(x) < 16f || Mathf.Abs(Mathf.Abs(z) - (half + 200f)) < 12f) continue;
                Vector3 p = new Vector3(x, D, z);
                Vector3 size = new Vector3(6f + (float)r.NextDouble() * 6f, 3f + (float)r.NextDouble() * 4f, 6f + (float)r.NextDouble() * 5f);
                Prims.Box(root, "Maison", p + new Vector3(0f, size.y * 0.5f, 0f), size, walls[i % walls.Length], true);
                Prims.Box(root, "Toit", p + new Vector3(0f, size.y + 0.2f, 0f), new Vector3(size.x + 0.8f, 0.4f, size.z + 0.8f), new Color(0.55f, 0.3f, 0.18f));
            }

            // Panneaux (honnêteté sur la nature de la maquette)
            const string text = "PONT MARÉCHAL — MATADI\nKongo Central · maquette de jeu\n722 m · travée centrale 520 m\nhauteurs et relief : à vérifier";
            Prims.Box(root, "Panneau", new Vector3(-9f, D + 3f, -(half + 40f)), new Vector3(9f, 3.4f, 0.2f), new Color(0.05f, 0.25f, 0.55f), true);
            Prims.Label(root, text, new Vector3(-9f, D + 3f, -(half + 40.15f)), 0.06f, Color.white, false);
            Prims.Box(root, "Panneau", new Vector3(9f, D + 3f, half + 40f), new Vector3(9f, 3.4f, 0.2f), new Color(0.05f, 0.25f, 0.55f), true);
            GameObject l2 = Prims.Label(root, text, new Vector3(9f, D + 3f, half + 40.15f), 0.06f, Color.white, false);
            l2.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            // ---------------- Lieux, arrêts, services ----------------
            AddPlace(w, root, origin, "matadi-tete-pont", "Tête de pont — côté Matadi", "Matadi", "pont", new Vector3(12f, D, -(half + 60f)), 3);
            AddPlace(w, root, origin, "matadi-colline", "Matadi — quartier sur la colline (maquette)", "Matadi", "quartier", new Vector3(-170f, D, -(half + 200f) - 8f), 2);
            AddPlace(w, root, origin, "matadi-rive-droite", "Tête de pont — rive droite (vers Boma)", "Rive droite", "pont", new Vector3(-12f, D, half + 60f), 3);
            AddPlace(w, root, origin, "matadi-route-boma", "Route de Boma — carrefour (maquette)", "Rive droite", "carrefour", new Vector3(170f, D, half + 200f + 8f), 2);
            InteractionPoint.Create(root, InteractionKind.Kadafi, "kadafi", "Kadafi de la tête de pont", origin + new Vector3(-14f, D, -(half + 90f)), 6f);
            InteractionPoint.Create(root, InteractionKind.PointVente, "point_vente", "Dépôt de carburant (rive droite)", origin + new Vector3(14f, D, half + 110f), 8f);

            // ---------------- Circulation (camions du port, minibus, taxis) ----------------
            float endZ = half + 190f;
            TrafficPath loop = TrafficPath.Loop("Pont_Marechal", new[]
            {
                origin + new Vector3(2.6f, D, -endZ), origin + new Vector3(2.6f, D, endZ),
                origin + new Vector3(-2.6f, D, endZ), origin + new Vector3(-2.6f, D, -endZ)
            }, 2.4f);
            w.TrafficPaths.Add(loop);

            w.HasMatadi = true;
            w.MatadiCenter = origin;
            w.MatadiSpawn = origin + new Vector3(2.6f, D + 0.6f, -(half + 150f));
            w.MatadiSpawnRotation = Quaternion.identity;
        }

        private static void AddPlace(WorldContext w, Transform root, Vector3 origin, string id, string name, string district, string category, Vector3 local, int weight)
        {
            PlaceRef p = WorldGeo.Place(id, name, district, category, origin + local, weight, true, Zone);
            w.Places.Add(p);
            ClientStop.Create(root, p, true);
        }

        private static void Road(Transform root, Vector3 a, Vector3 b, float width)
        {
            Vector3 dir = b - a;
            GameObject g = Prims.Box(root, "Route", (a + b) * 0.5f + new Vector3(0f, 0.02f, 0f), new Vector3(width, 0.12f, dir.magnitude), Asphalt, true, 0.18f);
            g.transform.localRotation = Quaternion.LookRotation(dir.normalized);
            g.AddComponent<SurfaceZone>().Kind = SurfaceKind.GoudronDegrade;
        }

        private static void Wall(Transform root, Vector3 local, Vector3 size)
        {
            GameObject g = new GameObject("Limite");
            g.transform.SetParent(root, false);
            g.transform.localPosition = local;
            g.AddComponent<BoxCollider>().size = size;
        }

        private static void Lamp(Transform root, Vector3 local)
        {
            Prims.Box(root, "Lampadaire", local + new Vector3(0f, 4.5f, 0f), new Vector3(0.16f, 9f, 0.16f), new Color(0.7f, 0.72f, 0.75f));
            GameObject lamp = Prims.Box(root, "Lampe", local + new Vector3(0f, 9f, 0f), new Vector3(0.6f, 0.18f, 0.6f), new Color(0.3f, 0.28f, 0.22f));
            WorldEnvironment.StreetLamps.Add(lamp.GetComponent<Renderer>());
        }
    }
}
