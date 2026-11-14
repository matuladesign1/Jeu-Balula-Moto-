// BALULA MOTO — ville générée : lieux aménagés à l'import (demandes de MATULA DESIGN, novembre 2026).
//   • esplanades clôturées (Échangeur, Stade des Martyrs) : aucune parcelle dedans ;
//   • cité Motel Fikin : maisons à l'intérieur, mais aucune parcelle à cheval sur la clôture ; écoles génériques ;
//   • arbres posés à l'import (esplanades, ronds-points) : modèles fournis par MATULA DESIGN, tronc solide.
using System.Collections.Generic;
using UnityEngine;

namespace BalulaMoto.Runtime
{
    public partial class CityFill
    {
        private class LieuPoly
        {
            public ImportedArea A;
            public Rect Box;
            /// <summary>Vrai : rien n'y est construit (esplanade) ; faux : on construit dedans, sans couper la clôture (cité).</summary>
            public bool NoBuild;
        }

        private readonly List<LieuPoly> lieuPolys = new List<LieuPoly>();
        private readonly List<ImportedArea> schools = new List<ImportedArea>();
        private readonly Dictionary<long, List<Vector3>> treesByCell = new Dictionary<long, List<Vector3>>();
        private bool inSchool;

        public int SchoolCount { get { return schools.Count; } }
        public int TreeSpotCount { get; private set; }

        private void PlanLieux()
        {
            lieuPolys.Clear();
            schools.Clear();
            treesByCell.Clear();
            if (map.areas != null)
                foreach (ImportedArea a in map.areas)
                {
                    if (a == null) continue;
                    if ((a.kind == "esplanade" || a.kind == "cite") && a.pts != null && a.pts.Count >= 3)
                    {
                        Vector2 mn = a.pts[0], mx = a.pts[0];
                        foreach (Vector2 p in a.pts) { mn = Vector2.Min(mn, p); mx = Vector2.Max(mx, p); }
                        lieuPolys.Add(new LieuPoly { A = a, Box = Rect.MinMaxRect(mn.x, mn.y, mx.x, mx.y), NoBuild = a.kind == "esplanade" });
                    }
                    else if (a.kind == "ecole") schools.Add(a);
                }
            if (map.treeSpots != null)
                foreach (Vector3 t in map.treeSpots)
                {
                    long k = Key(new Vector2(t.x, t.z));
                    List<Vector3> l;
                    if (!treesByCell.TryGetValue(k, out l)) { l = new List<Vector3>(); treesByCell[k] = l; }
                    l.Add(t);
                    TreeSpotCount++;
                }
        }

        /// <summary>Vrai si le rectangle est interdit par un lieu aménagé : esplanade (même en partie), clôture de la cité
        /// traversée, terrain réservé d'une école (sauf l'école elle-même).</summary>
        private bool LieuBlocks(Obb o)
        {
            if (lieuPolys.Count == 0 && schools.Count == 0) return false;
            float diag = o.Hx + o.Hz;
            Rect ob = new Rect(o.C.x - diag, o.C.y - diag, diag * 2f, diag * 2f);
            foreach (LieuPoly lp in lieuPolys)
            {
                if (!lp.Box.Overlaps(ob)) continue;
                List<Vector2> pts = lp.A.pts;
                int inside = 0;
                for (int sx = -1; sx <= 1; sx += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                        if (lp.A.Contains(o.C + o.X * (sx * o.Hx) + o.Z * (sz * o.Hz))) inside++;
                bool cIn = lp.A.Contains(o.C);
                if (lp.NoBuild && (inside > 0 || cIn)) return true;
                if (!lp.NoBuild && inside > 0 && inside < 4) return true;
                // Un côté du contour qui coupe le rectangle (clôture traversée)
                for (int i = 0, j = pts.Count - 1; i < pts.Count; j = i++)
                {
                    Vector2 a = pts[j], b = pts[i];
                    if (Mathf.Max(a.x, b.x) < ob.xMin || Mathf.Min(a.x, b.x) > ob.xMax || Mathf.Max(a.y, b.y) < ob.yMin || Mathf.Min(a.y, b.y) > ob.yMax) continue;
                    if (RectSeg(o, a, b) < 0.6f) return true;
                }
            }
            if (!inSchool)
                foreach (ImportedArea s in schools)
                {
                    if ((s.c - o.C).sqrMagnitude > (diag + s.hx + s.hz) * (diag + s.hx + s.hz)) continue;
                    Obb so = new Obb { C = s.c, X = s.x, Z = s.Z, Hx = s.hx, Hz = s.hz };
                    if (ObbOverlap(o, so, 0.3f)) return true;
                }
            return false;
        }

        /// <summary>Écoles de la case (cité Motel Fikin) : terrain clos de murs, bâtiment de classes, cour, drapeau,
        /// enseigne « ÉCOLE » (école générique : aucun nom inventé).</summary>
        private void BuildSchools(long cellKey, MeshData md, GameObject go, List<Obb> placed)
        {
            foreach (ImportedArea s in schools)
            {
                if (Key(s.c) != cellKey) continue;
                Obb o = new Obb { C = s.c, X = s.x, Z = s.Z, Hx = s.hx, Hz = s.hz };
                placed.Add(o);
                SchoolParcel(md, go.transform, s.c, s.x, s.Z, s.hx * 2f, s.hz * 2f, new System.Random(Mathf.RoundToInt(s.c.x * 3f + s.c.y * 7f) & 0xffff));
                Note("ecole", new Vector3(s.c.x, 0f, s.c.y), new Vector3(s.Z.x, 0f, s.Z.y));
            }
        }

        private void SchoolParcel(MeshData md, Transform parent, Vector2 c2, Vector2 x2, Vector2 z2, float w, float d, System.Random rnd)
        {
            // Repère : z2 vers l'intérieur du terrain (la rue est côté -z2)
            Vector3 c = new Vector3(c2.x, 0f, c2.y);
            Vector3 r = new Vector3(x2.x, 0f, x2.y), f = new Vector3(z2.x, 0f, z2.y), u = Vector3.up;
            const float H = 2.3f, T = 0.22f;
            float hw = w * 0.5f, hd = d * 0.5f;
            int variant = rnd.Next(4);
            float phase = (float)rnd.NextDouble();
            // Murs d'enceinte et grand portail côté rue
            float gw = 4f, gx = -gw * 0.5f;
            Vector3 front = c - f * hd;
            float leftLen = gx + hw, rightLen = hw - (gx + gw);
            FenceWall(md, front + r * (-hw + leftLen * 0.5f), r, -f, leftLen * 0.5f, H, T, variant, -1, false, phase);
            FenceWall(md, front + r * (hw - rightLen * 0.5f), r, -f, rightLen * 0.5f, H, T, variant, -1, false, phase + 0.3f);
            md.Box(front + r * (gx + gw * 0.5f) + u * 1.1f, r, u, f, new Vector3(gw * 0.5f, 1.1f, 0.04f), GateFirst + 1, GateFirst + 1);
            FenceWall(md, c + f * hd, r, f, hw, H, T, variant, -1, false, phase + 0.5f);
            FenceWall(md, c - r * hw, f, -r, hd, H, T, variant, -1, false, phase + 0.7f);
            FenceWall(md, c + r * hw, f, r, hd, H, T, variant, -1, false, phase + 0.9f);
            // Bâtiment des classes au fond : bleu clair et blanc, véranda, toit de tôle grise
            float bw = w - 3f, bd = Mathf.Min(7.5f, d * 0.42f), bh = 3.4f;
            Vector3 bc = c + f * (hd - bd * 0.5f - 1.2f);
            md.Box(bc + u * (bh * 0.5f), r, u, f, new Vector3(bw * 0.5f, bh * 0.5f, bd * 0.5f), 3, Concrete);
            md.Box(bc - f * (bd * 0.5f) + u * 0.45f, r, u, f, new Vector3(bw * 0.5f, 0.45f, 0.06f), 18, 18);        // soubassement blanc
            Vector3 face = bc - f * (bd * 0.5f + 0.02f);
            int classes = Mathf.Max(3, Mathf.FloorToInt(bw / 4.2f));
            for (int i = 0; i < classes; i++)
            {
                float x = -bw * 0.5f + (i + 0.5f) * bw / classes;
                md.Panel(face + r * (x - 0.9f) + u * 1.15f, r, u, 0.48f, 1.05f, Trunk);                                // porte de classe
                md.Panel(face + r * (x + 0.75f) + u * 1.75f, r, u, 0.7f, 0.55f, Window);                               // fenêtre
            }
            // Toit à un pan qui couvre la véranda
            Vector3 b0 = bc + u * bh;
            float ox = bw * 0.5f + 0.4f;
            Vector3 eB = b0 + f * (bd * 0.5f + 0.4f) + u * 0.6f, eF = b0 - f * (bd * 0.5f + 2.2f);
            md.Quad(eF - r * ox, eB - r * ox, eB + r * ox, eF + r * ox, RoofGray);
            md.Quad(eF + r * ox, eB + r * ox, eB - r * ox, eF - r * ox, RoofGray);
            for (int k = 0; k <= 4; k++)
            {
                Vector3 pp = bc - f * (bd * 0.5f + 2.0f) + r * (-bw * 0.5f + bw * k / 4f);
                md.Box(pp + u * (bh * 0.5f), r, u, f, new Vector3(0.12f, bh * 0.5f, 0.12f), 18, 18);                  // poteaux de véranda
            }
            // Mât et drapeau (bleu, étoile jaune, bande rouge)
            Vector3 mast = c - f * (hd - 3.5f) + r * (hw - 3f);
            md.Box(mast + u * 3.5f, r, u, f, new Vector3(0.05f, 3.5f, 0.05f), 20, 20);
            md.Box(mast + u * 6.4f + r * 0.75f, r, u, f, new Vector3(0.7f, 0.42f, 0.01f), 6, 6);
            md.Box(mast + u * 6.4f + r * 0.75f - f * 0.012f, r, u, f, new Vector3(0.72f, 0.07f, 0.008f), 9, 9);
            // Enseigne au-dessus du portail
            Vector3 sign = front + u * (H + 0.75f) - f * 0.15f;
            md.Box(sign, r, u, f, new Vector3(1.9f, 0.42f, 0.05f), 6, 6);
            md.Box(sign - u * 0.9f + r * 1.5f, r, u, f, new Vector3(0.06f, 0.6f, 0.06f), 20, 20);
            md.Box(sign - u * 0.9f - r * 1.5f, r, u, f, new Vector3(0.06f, 0.6f, 0.06f), 20, 20);
            GameObject l = Prims.Label(parent, "ÉCOLE", sign - f * 0.06f, 0.06f, Color.white, true);
            l.name = "Enseigne_Ecole";
            l.transform.rotation = Quaternion.LookRotation(-f, u);
            // Arbres de la cour
            if (TreeModels.Ready)
            {
                TreeModels.Add(batch, c - f * (hd - 3f) - r * (hw - 3f), 6f + (float)rnd.NextDouble() * 2f, rnd);
                TreeModels.Add(batch, c + f * 0.5f + r * (hw - 3.5f), 5.5f + (float)rnd.NextDouble() * 2f, rnd);
            }
            // Collisions
            GameObject col = ParcelCollider(parent, c, f);
            AddBox(col, new Vector3(0f, H * 0.5f, -hd), new Vector3(w, H, T + 0.1f));
            AddBox(col, new Vector3(0f, H * 0.5f, hd), new Vector3(w, H, T));
            AddBox(col, new Vector3(-hw, H * 0.5f, 0f), new Vector3(T, H, d));
            AddBox(col, new Vector3(hw, H * 0.5f, 0f), new Vector3(T, H, d));
            Vector3 lb = col.transform.InverseTransformPoint(bc);
            AddBox(col, new Vector3(lb.x, bh * 0.5f, lb.z), new Vector3(bw, bh, bd));
        }

        /// <summary>Parcelle d'une rue goudronnée : façade sur la même ligne que ses voisines, collée à elles (aucun espace
        /// entre deux clôtures). Si le modèle ou la profondeur ne tient pas, une parcelle simple moins profonde garde la
        /// même ligne de façade.</summary>
        private void AlignedLot(int ri, ImportedRoad road, float[] cum, float start, float w, float d, float back, float side, int model, bool parking, int seed, int row,
                                float half, List<Sg> near, List<Vector3> blds, int cx, int cz, long cellKey, List<Obb> placed, MeshData md, GameObject go, Cell cell)
        {
            Vector2 p, dir;
            At(ri, cum, start + w * 0.5f, out p, out dir);
            Vector2 inward = new Vector2(-dir.y, dir.x) * side;
            bool avenue = road.rank >= 5;
            if (model < 0 && !parking && row < 2) d = Mathf.Min(d, 18.6f);
            for (int attempt = 0; attempt < 4; attempt++)
            {
                bool plain = attempt > 0;
                float depth = plain ? Mathf.Min(d, 18.6f) - (attempt - 1) * 3.5f : d;
                if (depth < 9f) break;
                Vector2 c = p + inward * (back + depth * 0.5f);
                Obb o = new Obb { C = c, X = dir, Z = inward, Hx = w * 0.5f, Hz = depth * 0.5f };
                // Marge négative : deux parcelles voisines se touchent (léger recouvrement toléré dans les courbes)
                if (!Fits(o, near, blds, 0.8f, cx, cz, true, -0.45f)) continue;
                placed.Add(o);
                System.Random pr = new System.Random(seed);
                Vector2 xr = dir * side;
                if (!plain && model >= 0) ModelParcel(md, go.transform, c, xr, inward, w, depth, model, pr);
                else if (!plain && parking) ParkingParcel(md, go.transform, c, xr, inward, w, depth, pr);
                else if (avenue && row == 0 && !plain && pr.Next(10) < 7) ShopParcel(md, go.transform, c, xr, inward, w, depth, road.rank, pr);
                else Parcel(md, go.transform, c, xr, inward, w, depth, pr);
                cell.Parcels++;
                if (row == 0 && pr.Next(100) < 16)
                {
                    Vector2 sp = p + inward * (half + 2.2f) + dir * (((float)pr.NextDouble() - 0.5f) * (w - 3f));
                    if (!OnRoad(sp, near, 1.0f) && !InWater(sp, 0.5f)) Stall(md, cellKey, sp, -inward, pr);
                }
                return;
            }
        }

        /// <summary>Arbres posés à l'import dans la case : modèles fusionnés, tronc solide.</summary>
        private void ImportTrees(long cellKey, GameObject go)
        {
            List<Vector3> l;
            if (!treesByCell.TryGetValue(cellKey, out l) || !TreeModels.Ready) return;
            GameObject trunks = null;
            foreach (Vector3 t in l)
            {
                System.Random rnd = new System.Random(Mathf.RoundToInt(t.x * 11f) ^ Mathf.RoundToInt(t.z * 17f));
                TreeModels.Add(batch, new Vector3(t.x, 0f, t.z), t.y, rnd);
                if (trunks == null) { trunks = new GameObject("Troncs"); trunks.transform.SetParent(go.transform, false); }
                BoxCollider bc = trunks.AddComponent<BoxCollider>();
                bc.center = new Vector3(t.x, 1.3f, t.z);
                bc.size = new Vector3(0.55f, 2.6f, 0.55f);
            }
        }
    }
}
