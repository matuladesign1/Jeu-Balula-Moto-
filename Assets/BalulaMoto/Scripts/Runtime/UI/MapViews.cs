// BALULA MOTO — mini-carte (en haut à droite, tournée dans le sens de la caméra) et carte plein écran
// avec la légende : clients qui attendent, arrêts, stations-service, garages, ateliers, magasin de véhicules,
// lieux de référence du créateur, contrôles de police et destination du client en cours.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BalulaMoto.Runtime
{
    /// <summary>Quadrilatère texturé dont les coordonnées de texture tournent autour d'un centre (carte « cap en haut »).</summary>
    public class RotatedMapGraphic : MaskableGraphic
    {
        private Texture tex;
        private Vector2 uvCenter = new Vector2(0.5f, 0.5f);
        private float yaw, uvPerPx = 0.001f;

        public override Texture mainTexture { get { return tex != null ? tex : Texture2D.whiteTexture; } }

        public void SetTexture(Texture t)
        {
            tex = t;
            SetMaterialDirty();
            SetVerticesDirty();
        }

        public Texture Current { get { return tex; } }

        public void Set(Vector2 center, float yawRadians, float uvPerPixel)
        {
            uvCenter = center;
            yaw = yawRadians;
            uvPerPx = uvPerPixel;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            Vector2 c = r.center;
            float cs = Mathf.Cos(yaw), sn = Mathf.Sin(yaw);
            Vector2[] corners = { new Vector2(r.xMin, r.yMin), new Vector2(r.xMin, r.yMax), new Vector2(r.xMax, r.yMax), new Vector2(r.xMax, r.yMin) };
            UIVertex v = UIVertex.simpleVert;
            v.color = color;
            foreach (Vector2 p in corners)
            {
                float x = p.x - c.x, y = p.y - c.y;
                float east = x * cs + y * sn, north = -x * sn + y * cs;
                v.position = new Vector3(p.x, p.y, 0f);
                v.uv0 = new Vector4(uvCenter.x + east * uvPerPx, uvCenter.y + north * uvPerPx, 0f, 0f);
                vh.AddVert(v);
            }
            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
        }
    }

    /// <summary>Petits repères ronds avec une lettre (réutilisés d'une image à l'autre).</summary>
    public class MapIconPool
    {
        private readonly RectTransform root;
        private readonly float size;
        private readonly List<Image> discs = new List<Image>();
        private readonly List<Text> letters = new List<Text>();
        private readonly List<Text> labels = new List<Text>();
        private int used;

        public MapIconPool(RectTransform root, float size) { this.root = root; this.size = size; }

        public void Begin() { used = 0; }

        public void Add(MapIconKind kind, Vector2 pos, string label, bool showLabel)
        {
            if (used >= discs.Count)
            {
                Image d = UIKit.Disc(root, "Repere", Color.white);
                d.raycastTarget = false;
                Outline o = d.gameObject.AddComponent<Outline>();
                o.effectColor = new Color(0f, 0f, 0f, 0.85f);
                o.effectDistance = new Vector2(2f, -2f);
                Text t = UIKit.Label(d.transform, "", Mathf.RoundToInt(size * 0.62f), Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                UIKit.Stretch(t.rectTransform, 0, 0, 0, 0);
                Text l = UIKit.Label(root, "", Mathf.RoundToInt(size * 0.6f), Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
                l.horizontalOverflow = HorizontalWrapMode.Overflow;
                Outline lo = l.gameObject.AddComponent<Outline>();
                lo.effectColor = new Color(0f, 0f, 0f, 0.9f);
                discs.Add(d);
                letters.Add(t);
                labels.Add(l);
            }
            Image disc = discs[used];
            float s = kind == MapIconKind.Destination ? size * 1.3f : size;
            UIKit.Place(disc.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(s, s));
            disc.color = CityMap.IconColor(kind);
            letters[used].text = CityMap.IconLetter(kind);
            letters[used].color = kind == MapIconKind.Lieu || kind == MapIconKind.Client ? Color.black : Color.white;
            if (!disc.gameObject.activeSelf) disc.gameObject.SetActive(true);
            Text lab = labels[used];
            bool show = showLabel && !string.IsNullOrEmpty(label);
            if (lab.gameObject.activeSelf != show) lab.gameObject.SetActive(show);
            if (show)
            {
                lab.text = label;
                UIKit.Place(lab.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), pos + new Vector2(s * 0.65f, 0f), new Vector2(360f, s));
            }
            used++;
        }

        public void End()
        {
            for (int i = used; i < discs.Count; i++)
            {
                if (discs[i].gameObject.activeSelf) discs[i].gameObject.SetActive(false);
                if (labels[i].gameObject.activeSelf) labels[i].gameObject.SetActive(false);
            }
        }

        public void BringToFront()
        {
            foreach (Text l in labels) l.transform.SetAsLastSibling();
            foreach (Image d in discs) d.transform.SetAsLastSibling();
        }
    }

    // ======================================================================
    // Mini-carte
    // ======================================================================
    public class MiniMapView : MonoBehaviour
    {
        private RotatedMapGraphic plan;
        private RectTransform playerArrow, north;
        private MapIconPool pool;
        private readonly List<MapIcon> icons = new List<MapIcon>();
        private float iconTimer, viewMeters = 260f;
        private Text distText;

        public static MiniMapView Build(RectTransform parent, UIManager ui, Vector2 pos, float size)
        {
            Image ring = UIKit.Disc(parent, "MiniCarte", new Color(0.02f, 0.02f, 0.03f, 0.88f));
            UIKit.Place(ring.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), pos, new Vector2(size + 14f, size + 14f));
            // Canevas séparé : la mini-carte bouge à chaque image sans recalculer tout le HUD
            ring.gameObject.AddComponent<Canvas>();
            ring.gameObject.AddComponent<GraphicRaycaster>();
            Button b = ring.gameObject.AddComponent<Button>();
            b.targetGraphic = ring;
            b.onClick.AddListener(() => ui.OpenMap());

            Image maskImg = UIKit.Disc(ring.transform, "Masque", Color.white);
            maskImg.raycastTarget = false;
            UIKit.Stretch(maskImg.rectTransform, 7, 7, 7, 7);
            Mask m = maskImg.gameObject.AddComponent<Mask>();
            m.showMaskGraphic = false;

            RectTransform pr = UIKit.Rect(maskImg.transform, "Plan");
            UIKit.Stretch(pr, 0, 0, 0, 0);
            RotatedMapGraphic g = pr.gameObject.AddComponent<RotatedMapGraphic>();
            g.raycastTarget = false;
            g.color = Color.white;

            RectTransform iconRoot = UIKit.Rect(maskImg.transform, "Reperes");
            UIKit.Stretch(iconRoot, 0, 0, 0, 0);

            Text arrow = UIKit.Label(maskImg.transform, "▲", 40, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Place(arrow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(46f, 46f));
            Outline ao = arrow.gameObject.AddComponent<Outline>();
            ao.effectColor = new Color(0f, 0f, 0f, 0.9f);
            ao.effectDistance = new Vector2(2f, -2f);

            Text n = UIKit.Label(ring.transform, "N", 24, new Color(1f, 0.35f, 0.3f), TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Place(n.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, size * 0.5f - 12f), new Vector2(30f, 30f));
            Outline no = n.gameObject.AddComponent<Outline>();
            no.effectColor = Color.black;

            Text dist = UIKit.Label(ring.transform, "", 20, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Place(dist.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(size, 30f));
            Outline dO = dist.gameObject.AddComponent<Outline>();
            dO.effectColor = Color.black;

            MiniMapView v = ring.gameObject.AddComponent<MiniMapView>();
            v.plan = g;
            v.playerArrow = arrow.rectTransform;
            v.north = n.rectTransform;
            v.pool = new MapIconPool(iconRoot, 26f);
            v.distText = dist;
            return v;
        }

        private void LateUpdate()
        {
            GameplayDirector g = GameplayDirector.Instance;
            if (g == null || g.Player == null || !CityMap.Ready) return;
            if (plan.Current != CityMap.Texture) plan.SetTexture(CityMap.Texture);
            Vector3 pp = g.Player.transform.position;
            Transform cam = g.Cam != null ? g.Cam.transform : g.Player.transform;
            Vector3 f = Vector3.ProjectOnPlane(cam.forward, Vector3.up);
            float yaw = f.sqrMagnitude > 0.0001f ? Mathf.Atan2(f.x, f.z) : 0f;
            float target = Mathf.Lerp(230f, 430f, Mathf.Clamp01(g.Player.SpeedKmh / 60f));
            viewMeters = Mathf.Lerp(viewMeters, target, Time.unscaledDeltaTime * 1.2f);
            float r = Mathf.Max(10f, plan.rectTransform.rect.width * 0.5f);
            float mPerPx = viewMeters / r;
            plan.Set(CityMap.ToUV(pp), yaw, mPerPx / CityMap.Size);

            Vector3 vf = Vector3.ProjectOnPlane(g.Player.transform.forward, Vector3.up);
            float vyaw = vf.sqrMagnitude > 0.0001f ? Mathf.Atan2(vf.x, vf.z) : yaw;
            playerArrow.localEulerAngles = new Vector3(0f, 0f, -(vyaw - yaw) * Mathf.Rad2Deg);
            north.anchoredPosition = new Vector2(-Mathf.Sin(yaw), Mathf.Cos(yaw)) * (r - 6f);

            iconTimer -= Time.unscaledDeltaTime;
            if (iconTimer <= 0f)
            {
                iconTimer = 0.4f;
                CityMap.CollectIcons(icons, g);
            }
            float cs = Mathf.Cos(yaw), sn = Mathf.Sin(yaw);
            string destDist = "";
            pool.Begin();
            foreach (MapIcon ic in icons)
            {
                float dx = ic.Position.x - pp.x, dz = ic.Position.z - pp.z;
                float lx = (dx * cs - dz * sn) / mPerPx, ly = (dx * sn + dz * cs) / mPerPx;
                float d = Mathf.Sqrt(lx * lx + ly * ly);
                bool dest = ic.Kind == MapIconKind.Destination;
                if (dest) destDist = "Destination : " + UIFormat.Distance(Mathf.Sqrt(dx * dx + dz * dz));
                if (d > r - 12f)
                {
                    if (!dest) continue;
                    lx *= (r - 16f) / d;
                    ly *= (r - 16f) / d;
                }
                pool.Add(ic.Kind, new Vector2(lx, ly), null, false);
            }
            pool.End();
            if (distText.text != destDist) distText.text = destDist;
        }
    }

    // ======================================================================
    // Carte plein écran
    // ======================================================================
    public class MapPanel : UIPanel
    {
        private RawImage map;
        private RectTransform area, iconRoot, playerArrow;
        private MapIconPool pool;
        private readonly List<MapIcon> icons = new List<MapIcon>();
        private float zoom = 0.3f;
        private Vector2 center = new Vector2(0.5f, 0.5f);
        private Text infoText;
        private readonly Dictionary<MapIconKind, Text> legendCounts = new Dictionary<MapIconKind, Text>();
        // Noms des rues écrits sur la carte, le long de chaque rue
        private RectTransform nameRoot;
        private readonly List<Text> roadNames = new List<Text>();
        private const int MaxRoadNames = 110;

        protected override void Build()
        {
            Frame("CARTE DE LA VILLE", 1840f, 1000f, true);
            float side = 860f;
            Image areaImg = UIKit.Box(content, "ZoneCarte", new Color(0.05f, 0.06f, 0.08f, 1f), false);
            area = areaImg.rectTransform;
            UIKit.Place(area, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(side + 360f, side));
            area.gameObject.AddComponent<RectMask2D>();
            area.gameObject.AddComponent<Canvas>();
            area.gameObject.AddComponent<GraphicRaycaster>();
            RectTransform mr = UIKit.Rect(area, "Plan");
            UIKit.Place(mr, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(side + 360f, side + 360f));
            map = mr.gameObject.AddComponent<RawImage>();
            map.raycastTarget = false;
            nameRoot = UIKit.Rect(area, "NomsDesRues");
            UIKit.Stretch(nameRoot, 0, 0, 0, 0);
            iconRoot = UIKit.Rect(area, "Reperes");
            UIKit.Stretch(iconRoot, 0, 0, 0, 0);
            pool = new MapIconPool(iconRoot, 30f);
            Text arrow = UIKit.Label(area, "▲", 46, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            playerArrow = arrow.rectTransform;
            UIKit.Place(playerArrow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(54f, 54f));
            Outline ao = arrow.gameObject.AddComponent<Outline>();
            ao.effectColor = Color.black;
            ao.effectDistance = new Vector2(2f, -2f);

            DragZone dz = areaImg.gameObject.AddComponent<DragZone>();
            dz.Dragged = d =>
            {
                float k = ui.Canvas != null && ui.Canvas.scaleFactor > 0f ? 1f / ui.Canvas.scaleFactor : 1f;
                Rect ar = area.rect;
                float h = Mathf.Max(1f, ar.height);
                center -= d * k / h * zoom;
                ClampView();
            };
            dz.Scrolled = s => Zoom(s > 0f ? 0.8f : 1.25f);

            // Colonne de droite : légende et commandes
            RectTransform side2 = UIKit.Rect(content, "Legende");
            UIKit.Place(side2, new Vector2(1f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(520f, side));
            Text title = UIKit.Label(side2, "LÉGENDE", 30, UIKit.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIKit.TopLeft(title.rectTransform, 10, 0, 500, 44);
            MapIconKind[] kinds = { MapIconKind.Client, MapIconKind.Arret, MapIconKind.Carburant, MapIconKind.Garage, MapIconKind.Atelier, MapIconKind.Concession, MapIconKind.Lieu, MapIconKind.Police, MapIconKind.Destination };
            for (int i = 0; i < kinds.Length; i++)
            {
                float y = 54f + i * 50f;
                Image d = UIKit.Disc(side2, "Leg", CityMap.IconColor(kinds[i]));
                d.raycastTarget = false;
                UIKit.Place(d.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -y), new Vector2(38f, 38f));
                Text l = UIKit.Label(d.transform, CityMap.IconLetter(kinds[i]), 24, kinds[i] == MapIconKind.Lieu || kinds[i] == MapIconKind.Client ? Color.black : Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                UIKit.Stretch(l.rectTransform, 0, 0, 0, 0);
                Text n = UIKit.Label(side2, CityMap.IconName(kinds[i]), 24, UIKit.Text, TextAnchor.MiddleLeft);
                UIKit.TopLeft(n.rectTransform, 64, y, 360, 38);
                Text c = UIKit.Label(side2, "", 24, UIKit.TextDim, TextAnchor.MiddleRight, FontStyle.Bold);
                UIKit.TopLeft(c.rectTransform, 420, y, 90, 38);
                legendCounts[kinds[i]] = c;
            }
            float by = 54f + kinds.Length * 50f + 14f;
            Button plus = UIKit.Button(side2, "+", UIKit.PanelLight, Color.white, 40, () => Zoom(0.6f));
            UIKit.Place((RectTransform)plus.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -by), new Vector2(120f, 84f));
            Button minus = UIKit.Button(side2, "−", UIKit.PanelLight, Color.white, 40, () => Zoom(1f / 0.6f));
            UIKit.Place((RectTransform)minus.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(140f, -by), new Vector2(120f, 84f));
            Button me = UIKit.Button(side2, "Ma position", UIKit.Accent, Color.black, 26, CenterOnPlayer);
            UIKit.Place((RectTransform)me.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(270f, -by), new Vector2(240f, 84f));
            infoText = UIKit.Label(side2, "", 21, UIKit.TextDim, TextAnchor.UpperLeft);
            UIKit.TopLeft(infoText.rectTransform, 10, by + 96f, 500, side - by - 100f);

            Root.gameObject.AddComponent<PanelTicker>().Tick = Tick;
        }

        public override void Refresh()
        {
            if (CityMap.Ready) map.texture = CityMap.Texture;
            zoom = 0.3f;
            CenterOnPlayer();
            CityMap.CollectIcons(icons, GameplayDirector.Instance);
            int[] counts = new int[16];
            foreach (MapIcon ic in icons) counts[(int)ic.Kind]++;
            foreach (KeyValuePair<MapIconKind, Text> kv in legendCounts) kv.Value.text = counts[(int)kv.Key].ToString();
            infoText.text = "Glissez pour déplacer la carte, + et − pour zoomer : les noms des rues apparaissent en zoomant.\n" +
                            "Plan des rues : © contributeurs OpenStreetMap (ODbL).\n" +
                            "Les lieux de référence marqués « à vérifier » attendent la confirmation du créateur.";
            SetTitle("CARTE — " + CityMap.Name);
        }

        private void CenterOnPlayer()
        {
            GameplayDirector g = GameplayDirector.Instance;
            if (g != null && g.Player != null && CityMap.Ready) center = CityMap.ToUV(g.Player.transform.position);
            ClampView();
        }

        private void Zoom(float factor)
        {
            zoom = Mathf.Clamp(zoom * factor, 0.03f, 1f);
            ClampView();
        }

        private void ClampView()
        {
            float aspect = area != null && area.rect.height > 1f ? area.rect.width / area.rect.height : 1.4f;
            float hw = zoom * 0.5f * aspect, hh = zoom * 0.5f;
            center.x = hw >= 0.5f ? 0.5f : Mathf.Clamp(center.x, hw, 1f - hw);
            center.y = hh >= 0.5f ? 0.5f : Mathf.Clamp(center.y, hh, 1f - hh);
        }

        private void Tick()
        {
            if (!IsOpen || map == null || !CityMap.Ready) return;
            Rect ar = area.rect;
            float aspect = ar.height > 1f ? ar.width / ar.height : 1.4f;
            // Le plan couvre exactement la zone : coordonnées de texture = fenêtre de vue (même échelle en x et y)
            map.rectTransform.sizeDelta = new Vector2(ar.width, ar.height);
            map.uvRect = new Rect(center.x - zoom * 0.5f * aspect, center.y - zoom * 0.5f, zoom * aspect, zoom);
            float pxPerUv = ar.height / zoom;
            GameplayDirector g = GameplayDirector.Instance;
            bool showNames = zoom <= 0.35f, showStops = zoom <= 0.1f;
            // Noms des rues : les avenues d'abord, puis les rues à mesure qu'on zoome
            int minRank = zoom <= 0.07f ? 2 : (zoom <= 0.16f ? 3 : (zoom <= 0.4f ? 5 : 8));
            int shown = 0;
            foreach (CityMap.RoadLabel rl in CityMap.RoadLabels)
            {
                if (rl.Rank < minRank) break; // liste triée par importance
                Vector2 q = (rl.UV - center) * pxPerUv;
                if (Mathf.Abs(q.x) > ar.width * 0.5f - 30f || Mathf.Abs(q.y) > ar.height * 0.5f - 12f) continue;
                if (shown >= roadNames.Count)
                {
                    if (roadNames.Count >= MaxRoadNames) break;
                    Text nt = UIKit.Label(nameRoot, "", 20, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                    nt.horizontalOverflow = HorizontalWrapMode.Overflow;
                    nt.verticalOverflow = VerticalWrapMode.Overflow;
                    nt.raycastTarget = false;
                    Outline no = nt.gameObject.AddComponent<Outline>();
                    no.effectColor = new Color(0f, 0f, 0f, 0.95f);
                    no.effectDistance = new Vector2(1.5f, -1.5f);
                    roadNames.Add(nt);
                }
                Text t = roadNames[shown++];
                if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
                if (t.text != rl.Name) t.text = rl.Name;
                t.fontSize = rl.Rank >= 8 ? 24 : (rl.Rank >= 5 ? 21 : 17);
                t.color = rl.Water ? new Color(0.6f, 0.82f, 1f) : (rl.Rank >= 5 ? Color.white : new Color(0.9f, 0.92f, 0.95f));
                UIKit.Place(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), q, new Vector2(300f, 26f));
                t.rectTransform.localEulerAngles = new Vector3(0f, 0f, rl.Angle);
            }
            for (int i = shown; i < roadNames.Count; i++)
                if (roadNames[i].gameObject.activeSelf) roadNames[i].gameObject.SetActive(false);

            pool.Begin();
            foreach (MapIcon ic in icons)
            {
                Vector2 uv = CityMap.ToUV(ic.Position);
                Vector2 p = (uv - center) * pxPerUv;
                if (Mathf.Abs(p.x) > ar.width * 0.5f + 20f || Mathf.Abs(p.y) > ar.height * 0.5f + 20f) continue;
                bool named = ic.Kind == MapIconKind.Destination || ((ic.Kind == MapIconKind.Lieu || ic.Kind == MapIconKind.Carburant || ic.Kind == MapIconKind.Garage || ic.Kind == MapIconKind.Concession) && showNames) || ((ic.Kind == MapIconKind.Arret || ic.Kind == MapIconKind.Client) && showStops);
                pool.Add(ic.Kind, p, ic.Label, named);
            }
            pool.End();
            if (g != null && g.Player != null)
            {
                Vector2 uv = CityMap.ToUV(g.Player.transform.position);
                playerArrow.anchoredPosition = (uv - center) * pxPerUv;
                Vector3 vf = Vector3.ProjectOnPlane(g.Player.transform.forward, Vector3.up);
                float yaw = vf.sqrMagnitude > 0.0001f ? Mathf.Atan2(vf.x, vf.z) * Mathf.Rad2Deg : 0f;
                playerArrow.localEulerAngles = new Vector3(0f, 0f, -yaw);
                playerArrow.SetAsLastSibling();
            }
        }
    }

    /// <summary>Appelle une fonction à chaque image (pour les panneaux, qui ne sont pas des composants).</summary>
    public class PanelTicker : MonoBehaviour
    {
        public System.Action Tick;
        private void Update() { if (Tick != null) Tick(); }
    }
}
