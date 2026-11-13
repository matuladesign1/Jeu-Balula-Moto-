// BALULA MOTO — boîte à outils d'interface (uGUI créé par code, adapté aux écrans de téléphone).
using System;
using System.Collections.Generic;
using BalulaMoto.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BalulaMoto.Runtime
{
    public static class UIKit
    {
        public static readonly Color Bg = new Color(0.04f, 0.05f, 0.08f, 0.97f);
        public static readonly Color Panel = new Color(0.1f, 0.12f, 0.17f, 0.97f);
        public static readonly Color PanelLight = new Color(0.17f, 0.2f, 0.27f, 1f);
        public static readonly Color Accent = new Color(0.98f, 0.78f, 0.05f);
        public static readonly Color Blue = new Color(0f, 0.5f, 1f);
        public static readonly Color Red = new Color(0.86f, 0.18f, 0.16f);
        public static readonly Color Green = new Color(0.18f, 0.68f, 0.34f);
        public static readonly Color Text = new Color(0.96f, 0.96f, 0.96f);
        public static readonly Color TextDim = new Color(0.68f, 0.72f, 0.8f);
        public static readonly Color Shade = new Color(0f, 0f, 0f, 0.62f);
        public static readonly Color Dark = new Color(0.03f, 0.03f, 0.05f, 0.72f);

        private static Sprite rounded;

        /// <summary>Rectangle à coins arrondis (9-slice) généré à l'exécution.</summary>
        public static Sprite Rounded
        {
            get
            {
                if (rounded != null) return rounded;
                const int size = 64, radius = 18;
                Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;
                Color32[] px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float dx = Mathf.Max(0f, Mathf.Max(radius - x - 0.5f, x + 0.5f - (size - radius)));
                        float dy = Mathf.Max(0f, Mathf.Max(radius - y - 0.5f, y + 0.5f - (size - radius)));
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01(radius - d + 0.5f);
                        px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                    }
                tex.SetPixels32(px);
                tex.Apply();
                rounded = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
                return rounded;
            }
        }

        public static RectTransform Rect(Transform parent, string name)
        {
            GameObject g = new GameObject(name, typeof(RectTransform));
            g.transform.SetParent(parent, false);
            return (RectTransform)g.transform;
        }

        public static RectTransform Stretch(RectTransform rt, float left, float top, float right, float bottom)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static Image Box(Transform parent, string name, Color c, bool roundedCorners = true)
        {
            RectTransform rt = Rect(parent, name);
            Image img = rt.gameObject.AddComponent<Image>();
            img.color = c;
            if (roundedCorners)
            {
                img.sprite = Rounded;
                img.type = Image.Type.Sliced;
            }
            return img;
        }

        public static Text Label(Transform parent, string text, int size, Color c, TextAnchor align, FontStyle style = FontStyle.Normal)
        {
            RectTransform rt = Rect(parent, "Texte");
            Text t = rt.gameObject.AddComponent<Text>();
            t.font = Fonts.Default;
            t.text = text;
            t.fontSize = size;
            t.color = c;
            t.alignment = align;
            t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.raycastTarget = false;
            t.supportRichText = true;
            return t;
        }

        public static Button Button(Transform parent, string label, Color bg, Color fg, int size, UnityAction onClick)
        {
            Image img = Box(parent, "Bouton_" + label, bg);
            Button b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            ColorBlock cb = b.colors;
            cb.highlightedColor = new Color(1f, 1f, 1f, 1f);
            cb.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            cb.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f);
            b.colors = cb;
            Text t = Label(img.transform, label, size, fg, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(t.rectTransform, 10, 4, 10, 4);
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = Mathf.Max(12, size / 2);
            t.resizeTextMaxSize = size;
            if (onClick != null) b.onClick.AddListener(onClick);
            return b;
        }

        public static void SetButtonText(Button b, string text)
        {
            Text t = b.GetComponentInChildren<Text>();
            if (t != null) t.text = text;
        }

        public static LayoutElement Size(Component c, float height, float width = -1f)
        {
            LayoutElement le = c.gameObject.GetComponent<LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
            if (height >= 0f) { le.preferredHeight = height; le.minHeight = height; }
            if (width >= 0f) { le.preferredWidth = width; le.minWidth = width; }
            return le;
        }

        public static VerticalLayoutGroup VList(RectTransform rt, float spacing, int padding)
        {
            VerticalLayoutGroup v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = new RectOffset(padding, padding, padding, padding);
            v.childControlHeight = true;
            v.childControlWidth = true;
            v.childForceExpandHeight = false;
            v.childForceExpandWidth = true;
            return v;
        }

        public static RectTransform Row(Transform parent, float height, float spacing)
        {
            RectTransform rt = Rect(parent, "Ligne");
            HorizontalLayoutGroup h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childControlHeight = true;
            h.childControlWidth = true;
            h.childForceExpandHeight = true;
            h.childForceExpandWidth = true;
            Size(rt, height);
            return rt;
        }

        /// <summary>Liste défilante verticale. Les enfants de "content" sont empilés.</summary>
        public static RectTransform ScrollList(Transform parent, out RectTransform content)
        {
            RectTransform view = Rect(parent, "Defilement");
            Image bg = view.gameObject.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.01f);
            view.gameObject.AddComponent<RectMask2D>();
            ScrollRect sr = view.gameObject.AddComponent<ScrollRect>();
            content = Rect(view, "Contenu");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            VerticalLayoutGroup v = VList(content, 12f, 8);
            v.childAlignment = TextAnchor.UpperCenter;
            ContentSizeFitter f = content.gameObject.AddComponent<ContentSizeFitter>();
            f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.content = content;
            sr.viewport = view;
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 30f;
            return view;
        }

        public static RectTransform Bar(Transform parent, Color back, Color fill, out RectTransform fillRect)
        {
            Image b = Box(parent, "Jauge", back);
            Image f = Box(b.transform, "Remplissage", fill);
            fillRect = f.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0.5f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            return b.rectTransform;
        }

        public static void SetBar(RectTransform fill, float value01)
        {
            if (fill == null) return;
            fill.anchorMax = new Vector2(Mathf.Clamp01(value01), 1f);
            fill.offsetMax = Vector2.zero;
        }

        public static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                GameObject c = t.GetChild(i).gameObject;
                c.SetActive(false);
                UnityEngine.Object.Destroy(c);
            }
        }

        /// <summary>Carte d'information (fond + texte riche) pour les listes.</summary>
        public static Text Card(Transform parent, string richText, float height, Color bg)
        {
            Image card = Box(parent, "Carte", bg);
            Size(card, height);
            Text t = Label(card.transform, richText, 26, Text, TextAnchor.MiddleLeft);
            Stretch(t.rectTransform, 22, 8, 22, 8);
            return t;
        }

        public static string Hex(Color c) { return "#" + ColorUtility.ToHtmlStringRGB(c); }
        public static string Colored(string text, Color c) { return "<color=" + Hex(c) + ">" + text + "</color>"; }

        /// <summary>Place un élément par son coin haut-gauche dans son parent (x vers la droite, y vers le bas).</summary>
        public static RectTransform TopLeft(RectTransform rt, float x, float y, float w, float h)
        {
            return Place(rt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -y), new Vector2(w, h));
        }

        private static Sprite circle;

        /// <summary>Disque (boutons ronds du HUD).</summary>
        public static Sprite Circle
        {
            get
            {
                if (circle != null) return circle;
                const int size = 128;
                Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;
                Color32[] px = new Color32[size * size];
                float r = size * 0.5f;
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float dx = x + 0.5f - r, dy = y + 0.5f - r;
                        float a = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) - 0.5f);
                        px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                    }
                tex.SetPixels32(px);
                tex.Apply();
                circle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
                return circle;
            }
        }

        public static Image Disc(Transform parent, string name, Color c)
        {
            RectTransform rt = Rect(parent, name);
            Image img = rt.gameObject.AddComponent<Image>();
            img.sprite = Circle;
            img.color = c;
            img.preserveAspect = true;
            return img;
        }

        /// <summary>Bouton à cocher/sélection : couleur selon l'état.</summary>
        public static void SetSelected(Button b, bool selected)
        {
            Image img = b.GetComponent<Image>();
            if (img != null) img.color = selected ? Accent : PanelLight;
            Text t = b.GetComponentInChildren<Text>();
            if (t != null) t.color = selected ? Color.black : Text;
        }
    }

    /// <summary>Textes communs à plusieurs écrans.</summary>
    public static class UIFormat
    {
        public static string Distance(float meters)
        {
            if (meters < 1000f) return Mathf.RoundToInt(meters / 10f) * 10 + " m";
            return (meters / 1000f).ToString("0.0").Replace('.', ',') + " km";
        }

        public static string Percent(float v01) { return Mathf.RoundToInt(Mathf.Clamp01(v01) * 100f) + " %"; }

        public static string MissionProgressText(MissionDef m, MissionProgress p)
        {
            int value = p != null ? p.progression : 0;
            switch (m.objectif)
            {
                case MissionObjectives.Solde:
                case MissionObjectives.RevenusJour:
                    return Cdf.Format(value) + " / " + Cdf.Format(m.cible);
                case MissionObjectives.NoteMoyenne:
                    return TextUtil.Rating(value / 10f) + " → " + TextUtil.Rating(m.cible / 10f);
                case MissionObjectives.SuivreVehicule:
                    return value + " s / " + m.cible + " s";
                case MissionObjectives.VisiterLieu:
                case MissionObjectives.PossederCategorie:
                case MissionObjectives.DebloquerProvince:
                    return value >= m.cible ? "fait" : "à faire";
            }
            return value + " / " + m.cible;
        }
    }

    /// <summary>Bouton maintenu (accélérateur, freins, direction, klaxon).</summary>
    public class TouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public Action<bool> Changed;
        public bool Pressed { get; private set; }
        private Image image;
        private Color baseColor;

        private void Awake()
        {
            image = GetComponent<Image>();
            if (image != null) baseColor = image.color;
        }

        public void OnPointerDown(PointerEventData e) { Set(true); }
        public void OnPointerUp(PointerEventData e) { Set(false); }
        private void OnDisable() { if (Pressed) Set(false); }

        private void Set(bool v)
        {
            Pressed = v;
            if (image != null) image.color = v ? Color.Lerp(baseColor, Color.white, 0.35f) : baseColor;
            if (Changed != null) Changed(v);
        }
    }

    /// <summary>Zone de glissement plein écran : orientation de la caméra.</summary>
    public class DragZone : MonoBehaviour, IDragHandler, IScrollHandler
    {
        public Action<Vector2> Dragged;
        public Action<float> Scrolled;
        public void OnDrag(PointerEventData e) { if (Dragged != null) Dragged(e.delta); }
        public void OnScroll(PointerEventData e) { if (Scrolled != null) Scrolled(e.scrollDelta.y); }
    }

    /// <summary>Base des écrans et panneaux.</summary>
    public abstract class UIPanel
    {
        public RectTransform Root;
        public bool Modal = true;
        /// <summary>Peut être fermé par le bouton retour (Android) / Échap.</summary>
        public bool Closable = true;
        protected UIManager ui;
        protected RectTransform content;
        protected Text titleText;

        public bool IsOpen { get { return Root != null && Root.gameObject.activeSelf; } }

        public void Init(UIManager manager, RectTransform parent)
        {
            ui = manager;
            Root = UIKit.Rect(parent, GetType().Name);
            UIKit.Stretch(Root, 0, 0, 0, 0);
            Build();
            Root.gameObject.SetActive(false);
        }

        protected abstract void Build();
        public virtual void Refresh() { }
        public virtual void OnClose() { }

        /// <summary>Cadre modal standard : fond assombri, panneau central, titre, bouton Fermer.</summary>
        protected void Frame(string title, float width, float height, bool closeButton)
        {
            Closable = closeButton;
            Image shade = UIKit.Box(Root, "Voile", UIKit.Shade, false);
            UIKit.Stretch(shade.rectTransform, 0, 0, 0, 0);
            Image panel = UIKit.Box(Root, "Panneau", UIKit.Panel);
            UIKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, height));
            Image bar = UIKit.Box(panel.transform, "Titre", UIKit.PanelLight);
            bar.rectTransform.anchorMin = new Vector2(0f, 1f);
            bar.rectTransform.anchorMax = new Vector2(1f, 1f);
            bar.rectTransform.pivot = new Vector2(0.5f, 1f);
            bar.rectTransform.sizeDelta = new Vector2(0f, 96f);
            bar.rectTransform.anchoredPosition = Vector2.zero;
            titleText = UIKit.Label(bar.transform, title, 40, UIKit.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIKit.Stretch(titleText.rectTransform, 30, 0, 260, 0);
            if (closeButton)
            {
                Button close = UIKit.Button(bar.transform, "Fermer", UIKit.Red, Color.white, 30, () => ui.Close(this));
                UIKit.Place((RectTransform)close.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-18f, 0f), new Vector2(210f, 70f));
            }
            content = UIKit.Rect(panel.transform, "Contenu");
            UIKit.Stretch(content, 24, 110, 24, 20);
        }

        protected void SetTitle(string t) { if (titleText != null) titleText.text = t; }
    }
}
