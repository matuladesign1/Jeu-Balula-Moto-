#!/usr/bin/env python3
"""BALULA MOTO — textures des murs de clôture, à la manière des photos de Kinshasa envoyées par MATULA DESIGN :
parpaings bruts avec poteaux, ciment gris sale sur soubassement de moellons, parpaings sombres, chaux sale,
et les inscriptions peintes à la main « Cette parcelle n'est pas à vendre » (rouge, craie, bleu, noir).
Aucune photo n'est copiée : tout est dessiné par ce script (bruit, blocs, taches, lettres peintes).

Sorties (Assets/BalulaMoto/Resources/BalulaMoto/Textures) :
  murs_parcelles.png : 512 x 2048, 8 bandes de 512 x 256 (4 m de mur x 2,2 m de haut chacune) ;
      bandes 0-3 = murs qui se répètent en longueur, bandes 4-7 = panneaux à inscription (4 m, sans répétition)
  barbele.png        : 512 x 128 (2 m x 0,5 m), fil de fer barbelé en spirale (concertina), fond transparent

Usage : python3 scripts/generer_textures_murs.py
"""
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter

ICI = os.path.dirname(os.path.abspath(__file__))
DEST = os.path.join(ICI, "..", "Assets", "BalulaMoto", "Resources", "BalulaMoto", "Textures")
W, H = 512, 256            # une bande : 4 m x 2,2 m
PX_M_X, PX_M_Y = W / 4.0, H / 2.2
POTEAU = 2.0               # un poteau tous les 2 m
FONT_B = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"
FONT_R = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"


def bruit(rng, h, w, beta=2.0, periodic=True):
    """Bruit fractal (spectre en 1/f^beta), périodique en x et en y, normalisé entre 0 et 1."""
    n = rng.standard_normal((h, w))
    f = np.fft.fft2(n)
    fy = np.fft.fftfreq(h)[:, None]
    fx = np.fft.fftfreq(w)[None, :]
    r = np.sqrt(fx * fx + fy * fy)
    r[0, 0] = 1.0
    f /= r ** (beta / 2.0)
    f[0, 0] = 0
    out = np.real(np.fft.ifft2(f))
    out -= out.min()
    out /= max(out.max(), 1e-9)
    return out


def melange(base, couleur, masque):
    m = masque[..., None]
    return base * (1 - m) + np.asarray(couleur)[None, None, :] * m


def hauteur(y_px):
    """Hauteur au-dessus du sol (m) d'une ligne de l'image (ligne 0 = haut du mur)."""
    return (H - 1 - y_px) / PX_M_Y


def salete_bas(rng, img, hmax=0.38, couleur=(0.27, 0.23, 0.19), force=0.6):
    """Éclaboussures de boue et traces d'humidité au pied du mur (taches irrégulières, pas une bande nette)."""
    yy = np.arange(H)[:, None] * np.ones((1, W))
    h = hauteur(yy)
    taches = bruit(rng, H, W, 1.8)
    grain = bruit(rng, H, W, 0.4)
    lim = hmax * (0.5 + 0.8 * bruit(rng, 1, W, 2.2)[0][None, :])
    fondu = np.clip((lim - h) / 0.22, 0, 1)
    m = fondu * np.clip(taches * 1.3 - 0.15, 0, 1) * (0.6 + 0.4 * grain) * force
    # humidité : bande plus sombre et diffuse juste au-dessus du sol
    hum = np.clip((0.12 - h) / 0.12, 0, 1) * 0.35
    return melange(img, couleur, np.clip(m + hum, 0, 1))


def coulures(rng, img, n=14, couleur=(0.30, 0.29, 0.27), force=0.35):
    """Traînées verticales de pluie depuis le haut du mur."""
    m = np.zeros((H, W))
    for _ in range(n):
        x = rng.integers(0, W)
        larg = rng.integers(3, 14)
        long = rng.integers(H // 4, H)
        prof = np.linspace(1, 0, long) ** 1.5
        for dx in range(-larg, larg + 1):
            xx = (x + dx) % W
            m[:long, xx] = np.maximum(m[:long, xx], prof * (1 - abs(dx) / (larg + 1)) * rng.uniform(0.4, 1))
    m = np.array(Image.fromarray((m * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.5))) / 255.0
    return melange(img, couleur, m * force)


def poteaux(rng, img, clair=(0.66, 0.65, 0.62), largeur=0.17):
    """Poteaux en béton tous les 2 m (lisses, un peu plus clairs, ombre sur un bord)."""
    lw = int(largeur * PX_M_X)
    for k in range(int(4 / POTEAU)):
        x0 = int(k * POTEAU * PX_M_X) - lw // 2
        for i in range(lw):
            xx = (x0 + i) % W
            t = i / max(lw - 1, 1)
            ombre = 0.82 if t > 0.85 else (1.08 if t < 0.12 else 1.0)
            col = np.asarray(clair) * ombre
            img[:, xx] = img[:, xx] * 0.25 + col * 0.75
    return img


def parpaings(rng, base=(0.56, 0.55, 0.52), var=0.05, joint=(0.64, 0.63, 0.6), bas_moellons=False):
    """Blocs de 40 x 20 cm en quinconce, joints de mortier, grain du béton."""
    bw, bh = int(0.40 * PX_M_X), int(0.20 * PX_M_Y)
    img = np.zeros((H, W, 3))
    grain = bruit(rng, H, W, 0.6)
    large = bruit(rng, H, W, 2.4)
    rows = H // bh + 2
    for r in range(rows):
        y0 = H - (r + 1) * bh
        off = (bw // 2) * (r % 2)
        for c in range(-1, W // bw + 2):
            x0 = c * bw + off
            col = np.asarray(base) * (1 + rng.uniform(-var, var)) + rng.uniform(-0.02, 0.02)
            ys, ye = max(y0, 0), min(y0 + bh, H)
            if ys >= ye:
                continue
            for x in range(x0, x0 + bw):
                img[ys:ye, x % W] = col
    # joints
    for r in range(rows):
        y = H - r * bh
        for dy in range(2):
            if 0 <= y - dy - 1 < H:
                img[y - dy - 1, :] = joint
        off = (bw // 2) * (r % 2)
        y0, y1 = max(H - (r + 1) * bh, 0), min(H - r * bh, H)
        for c in range(-1, W // bw + 2):
            x = (c * bw + off) % W
            img[y0:y1, x] = joint
            img[y0:y1, (x + 1) % W] = joint
    img *= (0.88 + 0.24 * grain)[..., None]
    img *= (0.9 + 0.2 * large)[..., None]
    # éclats et trous dans quelques blocs
    for _ in range(18):
        x, y = rng.integers(0, W), rng.integers(0, H)
        rr = rng.integers(2, 6)
        yy, xx = np.ogrid[:H, :W]
        d = (yy - y) ** 2 + ((xx - x + W // 2) % W - W // 2) ** 2
        img[d < rr * rr] *= 0.75
    if bas_moellons:
        img = moellons(rng, img)
    return np.clip(img, 0, 1)


def moellons(rng, img, haut=0.32):
    """Soubassement en pierres irrégulières grises, jointoyées au mortier (photos : base des murs)."""
    hp = int(haut * PX_M_Y)
    pil = Image.new("RGB", (W, hp), (128, 126, 120))
    d = ImageDraw.Draw(pil)
    for _ in range(int(W * hp / 260)):
        cx, cy = rng.integers(0, W), rng.integers(0, hp)
        rx_, ry_ = rng.integers(8, 17), rng.integers(5, 10)
        g = int(rng.integers(78, 128))
        tint = rng.uniform(0.92, 1.02)
        col = (int(g * tint), g, int(g * 0.93))
        n = rng.integers(5, 8)
        ang = np.sort(rng.uniform(0, 2 * np.pi, n))
        pts = [(cx + rx_ * rng.uniform(0.7, 1.0) * np.cos(a), cy + ry_ * rng.uniform(0.7, 1.0) * np.sin(a)) for a in ang]
        for dxw in (-W, 0, W):
            d.polygon([(px + dxw, py) for px, py in pts], fill=col, outline=(150, 148, 142))
    a = np.asarray(pil).astype(float) / 255.0
    a *= (0.85 + 0.3 * bruit(rng, hp, W, 0.8))[..., None]
    img[H - hp:, :] = a
    # chaînage en béton au-dessus des pierres
    img[H - hp - 4:H - hp, :] = img[H - hp - 4:H - hp, :] * 0.5 + 0.6 * 0.5
    return img


def ciment(rng, base=(0.6, 0.6, 0.57)):
    """Enduit de ciment gris, taches, fissures fines, poteaux, soubassement en moellons."""
    img = np.ones((H, W, 3)) * np.asarray(base)
    img *= (0.84 + 0.3 * bruit(rng, H, W, 2.2))[..., None]
    img *= (0.92 + 0.16 * bruit(rng, H, W, 0.7))[..., None]
    pil = Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8))
    d = ImageDraw.Draw(pil)
    for _ in range(9):
        x, y = rng.integers(0, W), rng.integers(0, H // 2)
        pts = [(x, y)]
        for _ in range(rng.integers(6, 16)):
            x += rng.integers(-6, 7)
            y += rng.integers(3, 10)
            pts.append((x, y))
        d.line(pts, fill=(95, 95, 92), width=1)
    img = np.asarray(pil).astype(float) / 255.0
    img = poteaux(rng, img, clair=(0.64, 0.64, 0.61))
    img = moellons(rng, img)
    return img


def chaux(rng):
    """Mur chaulé sale : blanc cassé, enduit gris qui apparaît, coulures, moisissure verte en bas, boue."""
    img = np.ones((H, W, 3)) * np.asarray((0.86, 0.85, 0.8))
    img *= (0.9 + 0.12 * bruit(rng, H, W, 1.0))[..., None]
    plaques = bruit(rng, H, W, 2.6)
    img = melange(img, (0.62, 0.62, 0.6), np.clip((plaques - 0.68) * 6, 0, 1) * 0.9)
    img = poteaux(rng, img, clair=(0.88, 0.87, 0.83))
    yy = np.arange(H)[:, None] * np.ones((1, W))
    h = hauteur(yy)
    mois = np.clip((0.7 - h) / 0.5, 0, 1) * (0.3 + 0.7 * bruit(rng, H, W, 1.4))
    img = melange(img, (0.3, 0.33, 0.24), mois * 0.55)
    return img


def fond(style, rng):
    if style == "parpaings":
        img = parpaings(rng)
    elif style == "ciment":
        img = ciment(rng)
    elif style == "sombre":
        img = parpaings(rng, base=(0.42, 0.42, 0.41), var=0.07, joint=(0.5, 0.5, 0.48), bas_moellons=True)
        plaques = bruit(rng, H, W, 2.8)
        img = melange(img, (0.55, 0.55, 0.53), np.clip((plaques - 0.72) * 5, 0, 1) * 0.85)
    else:
        img = chaux(rng)
    img = coulures(rng, img, n=16 if style == "chaux" else 10)
    img = salete_bas(rng, img, hmax=0.42 if style in ("ciment", "sombre") else 0.36)
    # haut du mur légèrement assombri (bord du chaperon)
    img[:3, :] *= 0.8
    return np.clip(img, 0, 1)


def inscription(img, lignes, couleur, rng, style="peinture", taille=34, interligne=1.25, y_centre=0.52):
    """Lettres peintes à la main (dessinées à double résolution puis réduites) : chaque lettre penche un peu,
    taille et hauteur irrégulières, coulures de peinture ; à la craie, trait clair et poudreux."""
    S = 2
    calque = Image.new("L", (W * S, H * S), 0)
    police_f = FONT_B if style == "peinture" else FONT_R
    total_h = len(lignes) * taille * interligne * S
    y = int(H * S * y_centre - total_h / 2)
    for ligne in lignes:
        tailles = [int(taille * S * rng.uniform(0.93, 1.07)) for _ in ligne]
        fonts = [ImageFont.truetype(police_f, t) for t in tailles]
        largeurs = [f.getbbox(ch)[2] - f.getbbox(ch)[0] if ch != " " else int(t * 0.35) for ch, f, t in zip(ligne, fonts, tailles)]
        espace = int(taille * S * 0.06)
        tot = sum(largeurs) + espace * (len(ligne) - 1)
        x = (W * S - tot) // 2 + int(rng.integers(-12, 13))
        for ch, f, t, lw in zip(ligne, fonts, tailles, largeurs):
            if ch != " ":
                lettre = Image.new("L", (t * 2, t * 2), 0)
                dl = ImageDraw.Draw(lettre)
                bx = f.getbbox(ch)
                ox, oy = t // 2 - bx[0], t // 2
                if style == "peinture":
                    for ddx, ddy in ((0, 0), (1, 0), (0, 1)):
                        dl.text((ox + ddx, oy + ddy), ch, font=f, fill=255)
                else:
                    dl.text((ox, oy), ch, font=f, fill=235)
                lettre = lettre.rotate(rng.uniform(-5, 5), resample=Image.BICUBIC)
                py = y + int(rng.integers(-5, 6)) - t // 2 + int(t * 0.15)
                calque.paste(255, (x - t // 2 + bx[0], py), lettre)
                # coulures sous certaines lettres
                if style == "peinture" and rng.random() < 0.3:
                    dc = ImageDraw.Draw(calque)
                    cx = x + int(rng.uniform(0.2, 0.8) * lw)
                    cy = py + t // 2 + int(t * 0.95)
                    dc.line([(cx, cy), (cx, cy + int(rng.integers(10, 46)))], fill=int(rng.integers(150, 230)), width=3)
            x += lw + espace
        y += int(taille * interligne * S)
    calque = calque.resize((W, H), Image.LANCZOS)
    a = np.asarray(calque).astype(float) / 255.0
    if style == "peinture":
        a *= 0.82 + 0.18 * bruit(rng, H, W, 0.5)
    else:
        a *= 0.45 + 0.5 * bruit(rng, H, W, 0.3)
    return melange(img, couleur, np.clip(a, 0, 1))


def barbele(rng):
    """Concertina : spirales qui se chevauchent, avec leurs lames, répétable en longueur (2 m)."""
    S = 2
    w, h = 512 * S, 128 * S
    fil = Image.new("L", (w, h), 0)
    clair = Image.new("L", (w, h), 0)
    d, dc = ImageDraw.Draw(fil), ImageDraw.Draw(clair)
    pas = 22 * S
    rx, ry = 62 * S, 56 * S
    for i in range(-(rx // pas) - 2, w // pas + (rx // pas) + 3):
        cx = i * pas + int(rng.integers(-3, 4))
        cy = h // 2 + int(rng.integers(-4, 5))
        for dxw in (-w, 0, w):
            box = [cx - rx + dxw, cy - ry, cx + rx + dxw, cy + ry]
            d.ellipse(box, outline=255, width=3 * S // 2 + 1)
            dc.arc([box[0] + 2, box[1] + 2, box[2] - 2, box[3] - 2], 200, 300, fill=255, width=S)
        # lames du barbelé le long de la spirale
        for k in range(10):
            an = rng.uniform(0, 2 * np.pi)
            px, py = cx + rx * np.cos(an), cy + ry * np.sin(an)
            for dxw in (-w, 0, w):
                d.line([(px - 6 * S + dxw, py - 4 * S), (px + 6 * S + dxw, py + 4 * S)], fill=255, width=S + 1)
    a = np.asarray(fil.resize((512, 128), Image.LANCZOS)).astype(float) / 255.0
    hl = np.asarray(clair.resize((512, 128), Image.LANCZOS)).astype(float) / 255.0
    rgb = np.ones((128, 512, 3)) * np.asarray((0.24, 0.24, 0.25))
    rgb = melange(rgb, (0.62, 0.63, 0.65), np.clip(hl, 0, 1) * 0.8)
    out = np.dstack([rgb, np.clip(a * 1.4, 0, 1)])
    return Image.fromarray((out * 255).astype(np.uint8), "RGBA")


def main():
    os.makedirs(DEST, exist_ok=True)
    rng = np.random.default_rng(243)
    styles = ["parpaings", "ciment", "sombre", "chaux"]
    bandes = [fond(s, rng) for s in styles]
    # Panneaux à inscription : bande 4 + v = même mur que la variante v, lettres peintes par-dessus
    rouge, craie, bleu, noir = (0.72, 0.1, 0.1), (0.93, 0.93, 0.9), (0.08, 0.2, 0.62), (0.08, 0.08, 0.08)
    bandes.append(inscription(fond("parpaings", rng), ["CETTE PARCELLE", "N'EST PAS À VENDRE"], noir, rng, taille=33, y_centre=0.45))
    bandes.append(inscription(fond("ciment", rng), ["CETTE PARCELLE", "N'EST PAS À VENDRE"], rouge, rng, taille=34, y_centre=0.44))
    bandes.append(inscription(fond("sombre", rng), ["Cette maison", "n'est pas à vendre", "Attention !!!"], craie, rng, style="craie", taille=29, y_centre=0.42))
    bandes.append(inscription(fond("chaux", rng), ["CETTE PARCELLE N'EST PAS", "À VENDRE", "ATTENTION AUX ESCROCS"], bleu, rng, taille=25, y_centre=0.42))
    atlas = np.concatenate(bandes, axis=0)
    Image.fromarray((np.clip(atlas, 0, 1) * 255).astype(np.uint8), "RGB").save(os.path.join(DEST, "murs_parcelles.png"))
    barbele(rng).save(os.path.join(DEST, "barbele.png"))
    print("murs_parcelles.png %dx%d (8 bandes de 4 m x 2,2 m), barbele.png 512x128" % (atlas.shape[1], atlas.shape[0]))


if __name__ == "__main__":
    main()
