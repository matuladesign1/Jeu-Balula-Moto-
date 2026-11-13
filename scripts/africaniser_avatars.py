#!/usr/bin/env python3
"""BALULA MOTO — prépare les textures des personnages Rocketbox (licence MIT) pour une population de Kinshasa.

Pour chaque avatar retenu :
  * réduit les textures d'origine (TGA 2048) à 1024 pixels (JPG pour la couleur et le relief, PNG pour les cheveux) ;
  * pour les avatars à la peau claire, assombrit la peau vers un teint africain (plusieurs teints différents),
    en gardant les détails (pores, ombres, rides) : la peau est repérée par sa couleur, comparée à celle du visage ;
  * fonce les cheveux clairs ou châtains quand c'est demandé.

Usage :
    python3 scripts/africaniser_avatars.py SOURCE [--apercu DOSSIER]
SOURCE contient un dossier par avatar avec le FBX et les textures TGA d'origine
(github.com/microsoft/Microsoft-Rocketbox, Assets/Avatars/.../Export et Textures).
Les fichiers sont écrits dans Assets/BalulaMoto/ThirdParty/Rocketbox/Avatars/<Nom>/.
"""
import argparse
import glob
import os
import shutil
import sys

import numpy as np
from PIL import Image
from scipy import ndimage
from skimage import color

ICI = os.path.dirname(os.path.abspath(__file__))
DEST = os.path.join(ICI, "..", "Assets", "BalulaMoto", "ThirdParty", "Rocketbox", "Avatars")

# Teints visés (sRGB moyen de la joue, sous lumière neutre) : du brun moyen au brun très foncé.
TEINTS = {
    "fonce": (82, 53, 38),
    "ebene": (70, 45, 33),
    "brun": (98, 64, 45),
    "cafe": (112, 74, 52),
    "cuivre": (122, 80, 54),
}

# Avatar : (teint visé ou None pour garder la peau d'origine, foncer les cheveux)
AVATARS = {
    # déjà à la peau foncée : textures seulement réduites
    "Male_Adult_04": ("cafe", False),
    "Male_Adult_12": (None, False),
    "Male_Adult_18": (None, False),
    "Construction_Male_03": (None, False),
    "Male_Child_02": (None, False),
    # peau éclaircie à l'origine : teint africain
    "Business_Male_05": ("brun", True),
    "Male_Adult_17": ("fonce", True),
    "Sports_Male_03": ("cafe", True),
    "Delivery_Male_01": ("ebene", True),
    "Male_Adult_09": ("fonce", True),
    "Male_Adult_10": ("cuivre", True),
    "Wood_Male_01": ("brun", True),
    "Construction_Male_08": ("ebene", True),
    "Business_Female_01": ("cafe", True),
    "Female_Party_02": ("brun", True),
    "Female_Adult_02": ("fonce", True),
    "Female_Adult_11": ("cuivre", True),
    "Female_Adult_15": ("brun", True),
    "Medical_Female_01": ("cafe", True),
    "Female_Child_02": ("brun", True),
    "Police_Male_01": ("fonce", True),
}

TAILLE = 1024


def charger(path):
    im = Image.open(path)
    if im.size[0] != TAILLE:
        im = im.resize((TAILLE, TAILLE), Image.LANCZOS)
    return im


def lab_de(rgb_u8):
    return color.rgb2lab(rgb_u8.astype(np.float32) / 255.0)


def rgb_de(lab):
    rgb = color.lab2rgb(lab)
    return np.clip(rgb * 255.0 + 0.5, 0, 255).astype(np.uint8)


def reference_peau(lab_tete):
    """Couleur de peau de référence : médiane de plaques prises au front et aux joues du visage."""
    h, w = lab_tete.shape[:2]
    plaques = [(0.50, 0.15), (0.40, 0.31), (0.60, 0.31), (0.50, 0.22), (0.44, 0.40), (0.56, 0.40)]
    vals = []
    r = int(0.025 * w)
    for (u, v) in plaques:
        x, y = int(u * w), int(v * h)
        p = lab_tete[y - r:y + r, x - r:x + r].reshape(-1, 3)
        vals.append(p)
    v = np.concatenate(vals)
    # écarte les pixels trop sombres (sourcils, ombres) ou peu colorés
    chroma = np.hypot(v[:, 1], v[:, 2])
    ok = (v[:, 0] > 25) & (chroma > 6)
    if ok.sum() > 20:
        v = v[ok]
    return np.median(v, axis=0)


def masque_peau(lab, ref, zone_exclue=None, etendre=False, tete=False):
    """Masque doux (0..1) des pixels dont la teinte et la clarté ressemblent à la peau de référence."""
    L, a, b = lab[..., 0], lab[..., 1], lab[..., 2]
    d_ab = np.hypot(a - ref[1], b - ref[2])
    # teinte : angle de (a, b) ; la peau est entre le rouge-orangé et le jaune-orangé
    ang = np.degrees(np.arctan2(b, a))
    ang_ref = np.degrees(np.arctan2(ref[2], ref[1]))
    d_ang = np.abs(ang - ang_ref)
    m_ab = np.clip(1.0 - (d_ab - 9.0) / 10.0, 0.0, 1.0)
    m_ang = np.clip(1.0 - (d_ang - 14.0) / 12.0, 0.0, 1.0)
    m_L = np.clip((L - (ref[0] - 42.0)) / 10.0, 0.0, 1.0) * np.clip(((ref[0] + 22.0) - L) / 8.0, 0.0, 1.0)
    chroma = np.hypot(a, b)
    m_c = np.clip((chroma - 5.0) / 5.0, 0.0, 1.0)
    m = m_ab * m_ang * m_L * m_c
    if zone_exclue is not None:
        m = m * (1.0 - zone_exclue)
    # nettoyage : supprime les points isolés puis adoucit les bords
    dur = m > 0.5
    dur = ndimage.binary_opening(dur, iterations=1)
    dur = ndimage.binary_closing(dur, iterations=2)
    m = np.maximum(m * dur, ndimage.binary_erosion(dur, iterations=1) * 1.0)
    if tete:
        # Visage et cou : reflets plus clairs (front, nez, cou) reliés au visage, sans gagner les cheveux,
        # plus sombres que la peau.
        large = ((np.abs(ang - ang_ref) < 25.0) & (chroma > 7.0) & (L > ref[0] - 12.0) & (L < 98.0))
        pousse = ndimage.binary_propagation(dur, mask=large | dur)
        m = np.maximum(m, pousse * 1.0)
    if etendre:
        # Texture du corps : la peau des mains, paumes et genoux est plus rose ou plus claire que le visage.
        # On étend le masque aux pixels « couleur de peau » au sens large reliés à une zone déjà reconnue
        # (les îlots de peau de la texture sont séparés des vêtements par du noir).
        large = ((ang > ang_ref - 45.0) & (ang < ang_ref + 35.0) & (chroma > 6.0)
                 & (L > ref[0] - 45.0) & (L < 98.0))
        pousse = ndimage.binary_propagation(dur, mask=large | dur)
        m = np.maximum(m, pousse * 1.0)
    m = ndimage.gaussian_filter(m.astype(np.float32), 1.2)
    return np.clip(m, 0.0, 1.0)


def recolorer(lab, m, ref, cible_lab):
    """Déplace la peau vers le teint visé en gardant les variations (ombres, pores) en proportion."""
    out = lab.copy()
    kL = (cible_lab[0] / max(ref[0], 1.0)) * 1.05
    L2 = cible_lab[0] + (lab[..., 0] - ref[0]) * kL
    a2 = cible_lab[1] + (lab[..., 1] - ref[1]) * 0.75
    b2 = cible_lab[2] + (lab[..., 2] - ref[2]) * 0.75
    out[..., 0] = lab[..., 0] * (1 - m) + np.clip(L2, 2.0, 95.0) * m
    out[..., 1] = lab[..., 1] * (1 - m) + a2 * m
    out[..., 2] = lab[..., 2] * (1 - m) + b2 * m
    return out


def foncer_cheveux_tete(lab, m_peau, h, w):
    """Cheveux peints sur la texture de tête (zone du haut, hors visage) : bruns ou clairs -> presque noirs."""
    L, a, b = lab[..., 0], lab[..., 1], lab[..., 2]
    chroma = np.hypot(a, b)
    ang = np.degrees(np.arctan2(b, a))
    yy, xx = np.mgrid[0:h, 0:w]
    haut = (yy < 0.55 * h).astype(np.float32)
    bas_droite = ((yy > 0.6 * h) & (xx > 0.4 * w)).astype(np.float32)  # mèches de la texture
    zone = np.clip(haut + bas_droite, 0, 1)
    brun = ((ang > 15) & (ang < 100) & (chroma > 4) & (L > 14)).astype(np.float32)
    m = zone * brun * (1.0 - np.clip(m_peau * 1.5, 0, 1))
    m = ndimage.gaussian_filter(m, 1.0)
    out = lab.copy()
    out[..., 0] = L * (1 - m) + (4.0 + L * 0.30) * m
    out[..., 1] = a * (1 - m) + a * 0.35 * m
    out[..., 2] = b * (1 - m) + b * 0.30 * m
    return out


def zone_yeux_bouche(h, w):
    """Coin bas-gauche de la texture de tête : yeux, dents, langue (jamais recolorés)."""
    z = np.zeros((h, w), np.float32)
    z[int(0.56 * h):, :int(0.40 * w)] = 1.0
    return ndimage.gaussian_filter(z, 3.0)


def traiter(nom, src_dir, apercu=None, dest=None):
    teint, cheveux = AVATARS[nom]
    src = os.path.join(src_dir, nom)
    fbx = os.path.join(src, nom + ".fbx")
    if not os.path.isfile(fbx):
        print("absent :", nom)
        return False
    dst = os.path.join(dest or DEST, nom)
    dtex = os.path.join(dst, "Textures")
    os.makedirs(dtex, exist_ok=True)
    shutil.copyfile(fbx, os.path.join(dst, nom + ".fbx"))

    def fichier(suffixe):
        f = glob.glob(os.path.join(src, "*" + suffixe + ".tga"))
        return f[0] if f else None

    tete = fichier("_head_color")
    corps = fichier("_body_color")
    base = os.path.basename(tete).replace("_head_color.tga", "")
    im_tete = np.array(charger(tete).convert("RGB"))
    im_corps = np.array(charger(corps).convert("RGB"))
    avant = (im_tete.copy(), im_corps.copy())

    if teint is not None:
        lab_t = lab_de(im_tete)
        lab_c = lab_de(im_corps)
        ref = reference_peau(lab_t)
        cible = lab_de(np.array([[TEINTS[teint]]], np.uint8))[0, 0]
        h, w = im_tete.shape[:2]
        m_t = masque_peau(lab_t, ref, tete=True)
        m_c = masque_peau(lab_c, ref, etendre=True)
        lab_t2 = recolorer(lab_t, m_t, ref, cible)
        lab_c2 = recolorer(lab_c, m_c, ref, cible)
        if cheveux:
            lab_t2 = foncer_cheveux_tete(lab_t2, m_t, h, w)
        im_tete = rgb_de(lab_t2)
        im_corps = rgb_de(lab_c2)
        print("%-22s réf. L=%.0f a=%.0f b=%.0f -> %s ; peau tête %.0f %%, corps %.0f %%" % (
            nom, ref[0], ref[1], ref[2], teint, 100 * m_t.mean(), 100 * m_c.mean()))
    else:
        print("%-22s peau d'origine conservée" % nom)

    Image.fromarray(im_tete).save(os.path.join(dtex, base + "_head_color.jpg"), quality=90)
    Image.fromarray(im_corps).save(os.path.join(dtex, base + "_body_color.jpg"), quality=90)
    for suf in ("_head_normal", "_body_normal"):
        f = fichier(suf)
        if f:
            charger(f).convert("RGB").save(os.path.join(dtex, base + suf + ".jpg"), quality=90)
    f = fichier("_opacity_color")
    if f:
        op = charger(f).convert("RGBA")
        if teint is not None and cheveux:
            arr = np.array(op).astype(np.float32)
            rgb = arr[..., :3]
            lum = rgb.mean(axis=2, keepdims=True)
            # cheveux (mèches) presque noirs, avec un reflet brun très léger
            arr[..., :3] = np.clip(8 + rgb * 0.30 + (lum * 0.06), 0, 255)
            op = Image.fromarray(arr.astype(np.uint8), "RGBA")
        op.save(os.path.join(dtex, base + "_opacity_color.png"), optimize=True)

    if apercu:
        os.makedirs(apercu, exist_ok=True)
        planche = Image.new("RGB", (1024, 512))
        for i, im in enumerate([avant[0], im_tete, avant[1], im_corps]):
            planche.paste(Image.fromarray(im).resize((256, 256)), ((i % 4) * 256, 0))
        planche.crop((0, 0, 1024, 256)).save(os.path.join(apercu, nom + ".jpg"), quality=85)
    return True


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("source")
    ap.add_argument("--apercu")
    ap.add_argument("--seul", nargs="*")
    ap.add_argument("--dest")
    args = ap.parse_args()
    noms = args.seul if args.seul else list(AVATARS.keys())
    ok = 0
    for n in noms:
        if traiter(n, args.source, args.apercu, args.dest):
            ok += 1
    print(ok, "avatars prêts")
    return 0 if ok == len(noms) else 1


if __name__ == "__main__":
    sys.exit(main())
