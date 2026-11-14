// BALULA MOTO — ville générée : règles des boulevards (MATULA DESIGN, novembre 2026).
//   • 10 m libres entre la chaussée des boulevards et les maisons ou bâtiments (voir BoulevardRules) ;
//   • boulevard Lumumba : entre le grand boulevard et le petit boulevard, plus aucune maison — un espace vert
//     (pelouse, arbres) fermé de chaque côté par une clôture de barreaux de fer noirs, un trottoir le long des deux
//     chaussées, et une ouverture à chaque rue qui traverse la bande.
using System.Collections.Generic;
using UnityEngine;

namespace BalulaMoto.Runtime
{
    public partial class CityFill
    {
        private struct GreenPiece
        {
            public BoulevardRules.Strip S;
            /// <summary>Toute la bande (rien n'y est construit) et la pelouse clôturée (personne ne s'y tient).</summary>
            public Obb Box, Lawn;
            public bool HasLawn;
        }

        private BoulevardIndex boulevards;
        private readonly Dictionary<long, List<GreenPiece>> greenByCell = new Dictionary<long, List<GreenPiece>>();

        public int BoulevardCount { get { return boulevards != null ? boulevards.Boulevards.Count : 0; } }
        public int GreenPieces { get { return boulevards != null ? boulevards.Strips.Count : 0; } }
        public float GreenLength { get { return boulevards != null ? boulevards.StripLength : 0f; } }

        private bool IsBoulevardRoad(int road) { return boulevards != null && boulevards.Boulevards.Contains(road); }

        /// <summary>Morceau n° i de la bande verte (photos de contrôle) : bords côté grand et petit boulevard.</summary>
        public bool GreenSample(int i, out Vector3 inner, out Vector3 outer, out Vector3 along)
        {
            inner = outer = along = Vector3.zero;
            if (boulevards == null || i < 0 || i >= boulevards.Strips.Count) return false;
            BoulevardRules.Strip s = boulevards.Strips[i];
            inner = new Vector3(s.I0.x, 0f, s.I0.y);
            outer = new Vector3(s.O0.x, 0f, s.O0.y);
            Vector2 d = s.I1 - s.I0;
            along = new Vector3(d.x, 0f, d.y).normalized;
            return true;
        }

        private void PlanBoulevards()
        {
            boulevards = new BoulevardIndex(map.roads);
            greenByCell.Clear();
            float wb = BoulevardRules.WalkBand;
            foreach (BoulevardRules.Strip s in boulevards.Strips)
            {
                Vector2 along = s.I1 - s.I0;
                float len = along.magnitude;
                if (len < 0.3f) continue;
                along /= len;
                Vector2 across = new Vector2(-along.y, along.x);
                Vector2 mid0 = (s.I0 + s.O0) * 0.5f, mid1 = (s.I1 + s.O1) * 0.5f;
                if (Vector2.Dot((s.O0 - s.I0), across) < 0f) across = -across;
                float w = Mathf.Max(s.W0, s.W1);
                GreenPiece g = new GreenPiece { S = s };
                g.Box = new Obb { C = (mid0 + mid1) * 0.5f, X = along, Z = across, Hx = len * 0.5f + 0.3f, Hz = w * 0.5f };
                float lw = Mathf.Min(s.W0, s.W1) - 2f * wb;
                g.HasLawn = lw >= 1.5f;
                g.Lawn = new Obb { C = g.Box.C, X = along, Z = across, Hx = len * 0.5f, Hz = Mathf.Max(0.2f, lw * 0.5f) };
                long k = Key(g.Box.C);
                List<GreenPiece> l;
                if (!greenByCell.TryGetValue(k, out l)) { l = new List<GreenPiece>(); greenByCell[k] = l; }
                l.Add(g);
            }
        }

        private bool InGreen(Obb o, int cx, int cz)
        {
            if (greenByCell.Count == 0) return false;
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    List<GreenPiece> l;
                    if (!greenByCell.TryGetValue(Key(cx + dx, cz + dz), out l)) continue;
                    foreach (GreenPiece g in l)
                    {
                        float ddx = g.Box.C.x - o.C.x, ddz = g.Box.C.y - o.C.y, lim = g.Box.Hx + g.Box.Hz + o.Hx + o.Hz;
                        if (ddx * ddx + ddz * ddz > lim * lim) continue;
                        if (ObbOverlap(o, g.Box, 0.1f)) return true;
                    }
                }
            return false;
        }

        private bool InLawn(Vector2 q, float r, int cx, int cz)
        {
            if (greenByCell.Count == 0) return false;
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    List<GreenPiece> l;
                    if (!greenByCell.TryGetValue(Key(cx + dx, cz + dz), out l)) continue;
                    foreach (GreenPiece g in l) if (g.HasLawn && RectCircle(g.Lawn, q, r)) return true;
                }
            return false;
        }

        private static Vector3 G3(Vector2 p, float y) { return new Vector3(p.x, y, p.y); }

        /// <summary>Quadrilatère horizontal tourné vers le haut (pelouse).</summary>
        private static void UpQuad(MeshData md, Vector3 a, Vector3 b, Vector3 c, Vector3 d, int color)
        {
            if (Vector3.Cross(b - a, c - a).y >= 0f) md.Quad(a, b, c, d, color);
            else md.Quad(a, d, c, b, color);
        }

        /// <summary>Morceau de clôture en fer noir : poteau, lisses haute et basse, barreaux pointus.</summary>
        private static void IronFence(MeshData md, Vector2 p, Vector2 q, float barStep)
        {
            Vector2 d2 = q - p;
            float len = d2.magnitude;
            if (len < 0.3f) return;
            Vector2 dir = d2 / len;
            Vector3 r = new Vector3(dir.x, 0f, dir.y), u = Vector3.up, f = new Vector3(-dir.y, 0f, dir.x);
            Vector3 mid = G3((p + q) * 0.5f, 0f);
            const float H = 1.3f;
            // Petit muret de béton au pied, poteau au départ du morceau
            md.Box(mid + u * 0.09f, r, u, f, new Vector3(len * 0.5f, 0.09f, 0.11f), Concrete, Concrete);
            md.Box(G3(p, H * 0.5f + 0.06f), r, u, f, new Vector3(0.045f, H * 0.5f + 0.06f, 0.045f), Iron, Iron);
            md.Box(mid + u * (H - 0.06f), r, u, f, new Vector3(len * 0.5f, 0.022f, 0.022f), Iron, Iron);
            md.Box(mid + u * 0.32f, r, u, f, new Vector3(len * 0.5f, 0.022f, 0.022f), Iron, Iron);
            int nb = Mathf.Max(1, Mathf.FloorToInt(len / barStep));
            for (int i = 0; i < nb; i++)
            {
                Vector2 bp = p + dir * ((i + 0.5f) * len / nb);
                Vector3 c = G3(bp, 0.18f + H * 0.5f);
                // Barreau visible des deux côtés, avec sa pointe
                md.Panel(c, r, u, 0.011f, H * 0.5f, Iron);
                md.Panel(c, -r, u, 0.011f, H * 0.5f, Iron);
                Vector3 top = G3(bp, 0.18f + H);
                md.Tri(top - r * 0.03f, top + u * 0.09f, top + r * 0.03f, Iron);
                md.Tri(top + r * 0.03f, top + u * 0.09f, top - r * 0.03f, Iron);
            }
        }

        private void GreenTree(MeshData md, Vector2 p, System.Random rnd)
        {
            Vector3 tp = G3(p, 0f);
            float th = 2.4f + (float)rnd.NextDouble() * 1.6f;
            md.Box(tp + Vector3.up * (th * 0.5f), Vector3.right, Vector3.up, Vector3.forward, new Vector3(0.17f, th * 0.5f, 0.17f), Trunk, Trunk);
            float cr = 1.5f + (float)rnd.NextDouble() * 1.1f;
            int leaf = rnd.Next(2) == 0 ? Leaves : Leaves2;
            md.Octa(tp + Vector3.up * (th + cr * 0.45f), new Vector3(cr, cr * 0.8f, cr), leaf);
            md.Octa(tp + Vector3.up * (th + cr * 0.95f) + new Vector3(0.25f, 0f, -0.2f), new Vector3(cr * 0.7f, cr * 0.6f, cr * 0.7f), leaf == Leaves ? Leaves2 : Leaves);
        }

        /// <summary>Espace vert du boulevard Lumumba dans la case : pelouse, arbres, clôtures de fer noir et leurs collisions.</summary>
        private void GreenStrips(MeshData md, Transform parent, long cellKey)
        {
            List<GreenPiece> l;
            if (!greenByCell.TryGetValue(cellKey, out l)) return;
            float barStep = GameSettings.Quality == 0 ? 0.4f : (GameSettings.Quality == 1 ? 0.28f : 0.21f);
            float wb = BoulevardRules.WalkBand;
            Dictionary<int, Transform> colByAngle = new Dictionary<int, Transform>();
            GameObject colRoot = null;
            bool noted = false;
            foreach (GreenPiece g in l)
            {
                if (!g.HasLawn) continue;
                BoulevardRules.Strip s = g.S;
                Vector2 n0 = (s.O0 - s.I0) / Mathf.Max(0.01f, s.W0), n1 = (s.O1 - s.I1) / Mathf.Max(0.01f, s.W1);
                Vector2 a0 = s.I0 + n0 * wb, a1 = s.I1 + n1 * wb, b0 = s.O0 - n0 * wb, b1 = s.O1 - n1 * wb;
                System.Random gr = new System.Random(Hash(Mathf.RoundToInt(s.I0.x * 2f), Mathf.RoundToInt(s.I0.y * 2f), 41, 3));
                UpQuad(md, G3(a0, 0.07f), G3(a1, 0.07f), G3(b1, 0.07f), G3(b0, 0.07f), gr.Next(3) == 0 ? Lawn2 : Lawn);
                IronFence(md, a0, a1, barStep);
                IronFence(md, b0, b1, barStep);
                // Arbres : environ un tous les 9 m, sur deux rangées quand la pelouse est large
                float lw = g.Lawn.Hz * 2f;
                Vector2 lc = (a0 + a1 + b0 + b1) * 0.25f, across = g.Lawn.Z;
                if (lw >= 9f)
                {
                    if (gr.Next(100) < 40) GreenTree(md, lc - across * (lw * 0.22f), gr);
                    if (gr.Next(100) < 40) GreenTree(md, lc + across * (lw * 0.22f), gr);
                }
                else if (gr.Next(100) < 45) GreenTree(md, lc + across * (((float)gr.NextDouble() - 0.5f) * Mathf.Max(0f, lw - 2.5f)), gr);
                // Collisions des deux clôtures : un objet par orientation (à 2° près) dans la case
                if (colRoot == null)
                {
                    colRoot = new GameObject("ClotureEspaceVert");
                    colRoot.transform.SetParent(parent, false);
                }
                FenceCollider(colRoot.transform, colByAngle, a0, a1);
                FenceCollider(colRoot.transform, colByAngle, b0, b1);
                if (!noted)
                {
                    noted = true;
                    Note("bande_verte", G3(lc, 0f), G3(n0, 0f));
                }
            }
        }

        private static void FenceCollider(Transform root, Dictionary<int, Transform> byAngle, Vector2 p, Vector2 q)
        {
            Vector2 d = q - p;
            float len = d.magnitude;
            if (len < 0.3f) return;
            float ang = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;
            int bucket = Mathf.RoundToInt(Mathf.Repeat(ang, 180f) / 2f);
            Transform t;
            if (!byAngle.TryGetValue(bucket, out t))
            {
                GameObject g = new GameObject("Grille");
                t = g.transform;
                t.SetParent(root, false);
                t.position = G3(p, 0f);
                t.rotation = Quaternion.Euler(0f, bucket * 2f, 0f);
                byAngle[bucket] = t;
            }
            BoxCollider bc = t.gameObject.AddComponent<BoxCollider>();
            bc.center = t.InverseTransformPoint(G3((p + q) * 0.5f, 0.7f));
            bc.size = new Vector3(0.14f, 1.4f, len + 0.04f);
        }
    }
}
