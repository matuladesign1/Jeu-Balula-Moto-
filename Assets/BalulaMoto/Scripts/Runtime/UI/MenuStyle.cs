// BALULA MOTO — habillage des écrans d'accueil et de garage (inspiré des captures envoyées par MATULA DESIGN, adapté au
// jeu) : barre du haut sombre avec retour, titre et sous-titre, badge de niveau et argent sur fond orange ; grandes
// cartes de mode à gauche ; gros boutons du bas (gris : boutique, vert : rouler ou acheter) ; barres de caractéristiques.
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace BalulaMoto.Runtime
{
    public static class MenuStyle
    {
        public static readonly Color Bar = new Color(0.07f, 0.075f, 0.09f, 0.94f);
        public static readonly Color BarLight = new Color(0.82f, 0.84f, 0.88f, 0.96f);
        public static readonly Color Orange = new Color(0.98f, 0.6f, 0.05f, 0.98f);
        public static readonly Color GreenTop = new Color(0.36f, 0.82f, 0.12f, 1f);
        public static readonly Color GreenBottom = new Color(0.1f, 0.55f, 0.08f, 1f);
        public static readonly Color GreyTop = new Color(0.88f, 0.89f, 0.9f, 1f);
        public static readonly Color GreyBottom = new Color(0.55f, 0.57f, 0.6f, 1f);
        public static readonly Color Teal = new Color(0.05f, 0.55f, 0.65f, 1f);
        public static readonly Color Card = new Color(0.05f, 0.06f, 0.08f, 0.9f);
        public static readonly Color StatGreen = new Color(0.2f, 0.85f, 0.12f, 1f);
        public static readonly Color ValueYellow = new Color(1f, 0.85f, 0.2f, 1f);

        public class Header
        {
            public Text Title, Subtitle, Level, Money, Rank;
            public Button Back;
        }

        private static Image Plain(Transform parent, string name, Color c)
        {
            Image i = UIKit.Box(parent, name, c, false);
            i.raycastTarget = false;
            return i;
        }

        /// <summary>Bande oblique décorative (comme les découpes des captures d'exemple).</summary>
        public static Image Slash(Transform parent, Color c, Vector2 anchor, Vector2 pos, Vector2 size, float angle)
        {
            Image i = Plain(parent, "Biseau", c);
            UIKit.Place(i.rectTransform, anchor, new Vector2(0.5f, 0.5f), pos, size);
            i.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
            return i;
        }

        /// <summary>Barre du haut : bouton retour (si action), titre et sous-titre, badge de niveau, argent.</summary>
        public static Header TopBar(RectTransform root, string title, string subtitle, UnityAction back)
        {
            Header h = new Header();
            Image bar = Plain(root, "BarreHaut", Bar);
            bar.rectTransform.anchorMin = new Vector2(0f, 1f);
            bar.rectTransform.anchorMax = new Vector2(1f, 1f);
            bar.rectTransform.pivot = new Vector2(0.5f, 1f);
            bar.rectTransform.sizeDelta = new Vector2(0f, 104f);
            bar.rectTransform.anchoredPosition = Vector2.zero;
            RectTransform b = bar.rectTransform;
            // Bandeau clair du titre, découpé en biais
            Image band = Plain(b, "BandeauTitre", BarLight);
            UIKit.Place(band.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(back != null ? 150f : 20f, -8f), new Vector2(780f, 66f));
            Slash(b, Bar, new Vector2(0f, 1f), new Vector2((back != null ? 150f : 20f) + 780f, -40f), new Vector2(60f, 130f), -28f);
            if (back != null)
            {
                h.Back = UIKit.Button(b, "‹", new Color(0.02f, 0.02f, 0.03f, 1f), Color.white, 80, back);
                UIKit.Place((RectTransform)h.Back.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -6f), new Vector2(128f, 92f));
            }
            h.Title = UIKit.Label(band.transform, title, 46, new Color(0.08f, 0.08f, 0.1f), TextAnchor.MiddleLeft, FontStyle.Bold);
            UIKit.Stretch(h.Title.rectTransform, 30, 0, 20, 0);
            h.Subtitle = UIKit.Label(b, subtitle, 22, new Color(0.85f, 0.87f, 0.9f), TextAnchor.UpperLeft);
            UIKit.Place(h.Subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2((back != null ? 150f : 20f) + 34f, -76f), new Vector2(900f, 30f));
            // Badge du niveau (rond) et argent sur fond orange, à droite
            Image money = Plain(b, "Argent", Orange);
            UIKit.Place(money.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(430f, 92f));
            Slash(b, Orange, new Vector2(1f, 1f), new Vector2(-430f, -46f), new Vector2(60f, 130f), 28f);
            Image coin = UIKit.Disc(money.transform, "Piece", new Color(1f, 0.82f, 0.25f));
            UIKit.Place(coin.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(52f, 52f));
            Text fc = UIKit.Label(coin.transform, "FC", 20, new Color(0.45f, 0.25f, 0.02f), TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Stretch(fc.rectTransform, 0, 0, 0, 0);
            h.Money = UIKit.Label(money.transform, "", 40, Color.white, TextAnchor.MiddleRight, FontStyle.Bold);
            UIKit.Stretch(h.Money.rectTransform, 76, 0, 22, 0);
            Outline mo = h.Money.gameObject.AddComponent<Outline>();
            mo.effectColor = new Color(0.45f, 0.2f, 0f, 0.8f);
            Image badge = UIKit.Disc(b, "Niveau", new Color(0.92f, 0.32f, 0.08f));
            UIKit.Place(badge.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-470f, -10f), new Vector2(78f, 78f));
            h.Level = UIKit.Label(badge.transform, "", 30, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Stretch(h.Level.rectTransform, 0, 0, 0, 0);
            Text lvl = UIKit.Label(b, "NIV.", 20, new Color(0.85f, 0.87f, 0.9f), TextAnchor.UpperLeft, FontStyle.Bold);
            UIKit.Place(lvl.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-438f, -12f), new Vector2(70f, 26f));
            h.Rank = UIKit.Label(b, "", 18, new Color(0.85f, 0.87f, 0.9f), TextAnchor.LowerLeft);
            UIKit.Place(h.Rank.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-460f, -64f), new Vector2(130f, 30f));
            return h;
        }

        /// <summary>Gros bouton du bas : vert (rouler, acheter) ou gris (boutique, garage), avec chevrons.</summary>
        public static Button BigButton(RectTransform root, string label, bool green, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, UnityAction action)
        {
            Image img = UIKit.Box(root, "Gros_" + label, green ? GreenBottom : GreyBottom, false);
            UIKit.Place(img.rectTransform, anchor, pivot, pos, size);
            Button b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            ColorBlock cb = b.colors;
            cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            cb.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.7f);
            b.colors = cb;
            Image top = Plain(img.transform, "Reflet", green ? GreenTop : GreyTop);
            top.rectTransform.anchorMin = new Vector2(0f, 0.45f);
            top.rectTransform.anchorMax = new Vector2(1f, 1f);
            top.rectTransform.offsetMin = new Vector2(4f, 0f);
            top.rectTransform.offsetMax = new Vector2(-4f, -4f);
            Color stripe = green ? new Color(1f, 1f, 1f, 0.16f) : new Color(0f, 0f, 0f, 0.1f);
            for (int i = 0; i < 2; i++)
                Slash(img.transform, stripe, new Vector2(0f, 0.5f), new Vector2(46f + i * 44f, 0f), new Vector2(26f, size.y * 1.3f), -30f);
            Text t = UIKit.Label(img.transform, label, Mathf.RoundToInt(size.y * 0.42f), green ? Color.white : new Color(0.1f, 0.1f, 0.12f), TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Stretch(t.rectTransform, 70, 0, 20, 0);
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = 16;
            t.resizeTextMaxSize = Mathf.RoundToInt(size.y * 0.42f);
            if (green)
            {
                Outline o = t.gameObject.AddComponent<Outline>();
                o.effectColor = new Color(0.05f, 0.25f, 0.02f, 0.9f);
            }
            if (action != null) b.onClick.AddListener(action);
            return b;
        }

        public static void SetBigLabel(Button b, string text)
        {
            foreach (Text t in b.GetComponentsInChildren<Text>()) { t.text = text; break; }
        }

        /// <summary>Carte de mode : fond sombre, bande de couleur, étiquette (NOUVEAU, REPRENDRE…), titre, chevron.</summary>
        public static Button ModeCard(RectTransform root, string title, string sub, Color stripe, Vector2 pos, UnityAction action, out Text tag, out Image frame)
        {
            Image img = UIKit.Box(root, "Carte_" + title, Card);
            UIKit.Place(img.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), pos, new Vector2(520f, 170f));
            Button b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            if (action != null) b.onClick.AddListener(action);
            Image band = UIKit.Box(img.transform, "Bande", stripe);
            band.raycastTarget = false;
            band.rectTransform.anchorMin = new Vector2(0f, 0f);
            band.rectTransform.anchorMax = new Vector2(0f, 1f);
            band.rectTransform.pivot = new Vector2(0f, 0.5f);
            band.rectTransform.sizeDelta = new Vector2(150f, -16f);
            band.rectTransform.anchoredPosition = new Vector2(8f, 0f);
            Slash(img.transform, new Color(stripe.r, stripe.g, stripe.b, 0.35f), new Vector2(0f, 0.5f), new Vector2(178f, 0f), new Vector2(34f, 220f), -24f);
            Text big = UIKit.Label(band.transform, title.Substring(0, 1), 92, new Color(1f, 1f, 1f, 0.92f), TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Stretch(big.rectTransform, 0, 0, 0, 0);
            Text t = UIKit.Label(img.transform, title, 40, Color.white, TextAnchor.LowerLeft, FontStyle.Bold);
            UIKit.Place(t.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(200f, 22f), new Vector2(280f, 60f));
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = 22;
            t.resizeTextMaxSize = 40;
            Text s = UIKit.Label(img.transform, sub, 19, UIKit.TextDim, TextAnchor.UpperLeft);
            UIKit.Place(s.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(200f, -58f), new Vector2(290f, 60f));
            tag = UIKit.Label(img.transform, "", 22, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Place(tag.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(196f, -12f), new Vector2(170f, 34f));
            Image tagBg = UIKit.Box(img.transform, "FondEtiquette", new Color(0.86f, 0.12f, 0.1f, 1f));
            tagBg.raycastTarget = false;
            UIKit.Place(tagBg.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(196f, -12f), new Vector2(170f, 34f));
            tagBg.transform.SetSiblingIndex(tag.transform.GetSiblingIndex());
            Text chev = UIKit.Label(img.transform, "›", 64, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Place(chev.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-8f, 6f), new Vector2(50f, 70f));
            // Cadre de sélection (coins jaunes comme l'exemple)
            frame = UIKit.Box(img.transform, "Selection", new Color(1f, 0.82f, 0.1f, 0f));
            frame.raycastTarget = false;
            UIKit.Stretch(frame.rectTransform, -10, -10, -10, -10);
            Outline fo = frame.gameObject.AddComponent<Outline>();
            fo.effectColor = new Color(1f, 0.82f, 0.1f, 1f);
            fo.effectDistance = new Vector2(4f, 4f);
            return b;
        }

        /// <summary>Ligne de caractéristique : libellé, valeur (jaune) et jauge verte.</summary>
        public static void StatRow(Transform parent, string label, float y, out Text value, out RectTransform fill)
        {
            Text l = UIKit.Label(parent, label, 24, Color.white, TextAnchor.MiddleLeft);
            UIKit.TopLeft(l.rectTransform, 18, y, 250, 34);
            value = UIKit.Label(parent, "", 24, ValueYellow, TextAnchor.MiddleRight, FontStyle.Bold);
            UIKit.TopLeft(value.rectTransform, 200, y, 196, 34);
            RectTransform bar = UIKit.Bar(parent, new Color(1f, 1f, 1f, 0.16f), StatGreen, out fill);
            UIKit.TopLeft(bar, 18, y + 36, 378, 14);
        }

        public static void Line(Transform parent, string label, float y, out Text value)
        {
            Text l = UIKit.Label(parent, label, 22, new Color(0.88f, 0.9f, 0.93f), TextAnchor.MiddleLeft);
            UIKit.TopLeft(l.rectTransform, 18, y, 230, 30);
            value = UIKit.Label(parent, "", 22, ValueYellow, TextAnchor.MiddleRight, FontStyle.Bold);
            UIKit.TopLeft(value.rectTransform, 200, y, 196, 30);
        }
    }
}
