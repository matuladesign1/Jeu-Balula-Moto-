#!/usr/bin/env python3
"""BALULA MOTO — conversion des modèles 3D envoyés par MATULA DESIGN (14 novembre 2026) en OBJ légers pour Unity.

Fichiers d'origine (non versionnés, fournis par le créateur) :
  1  67-cottage_free_blend.rar (Cottage_FREE.blend)        -> Decor/Cottage.obj
  2  mgs_barriers (barrier1.fbx, barrier2.fbx)             -> Decor/Barriere_A.obj, Decor/Barriere_B.obj (rouge et blanc)
  3  fhouse.rar (fhouse.blend)                             -> Decor/MaisonF.obj (extérieur seulement)
  4  moto1 (motor.blend)                                   -> Vehicules/MotoCentrale.obj (moto centrale du jeu)
  8  source_engine_street_objects.glb (Leafia dev, CC-BY-4.0) -> Decor/Rue_*.obj (banc, benne, glissière, fût, cône,
     auvent, cartons)
  11 honda_cb_750_four_TS_00.FBX                           -> Vehicules/HondaCB750.obj (+ _LOD)
  12 honda_fbx.zip (honda.fbx)                             -> Vehicules/HondaClassique.obj (+ _LOD)
  13 5Building_FBX.zip (Building_01..05.fbx)               -> Decor/Immeuble_1..5.obj (façades redessinées : les
     textures d'origine ne sont pas dans les archives)
Les fichiers 5, 6, 7 (voitures de marque, droits incertains) et 9 (licence non commerciale) ne sont pas utilisés : les
voitures de la circulation sont des modèles génériques (scripts/generer_voitures.py). Le fichier 10 (Boulevard.skp,
SketchUp 2026) ne peut pas être lu ici.

Repère des OBJ écrits : mètres, Y vers le haut, avant vers +Z, X = gauche du modèle (Unity retourne l'axe X à l'import).
Usage : python3 scripts/convertir_modeles_recus.py SOURCES [--seulement nom,nom] [--apercu DOSSIER]
"""
import argparse
import copy
import io
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ICI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(ICI, "outils3d"))
import assets3d as A  # noqa: E402
from motos_cfg import CB750_COL, HONDA_COL  # noqa: E402

RACINE = os.path.dirname(ICI)
DECOR = os.path.join(RACINE, "Assets", "BalulaMoto", "Resources", "BalulaMoto", "Decor")
VEHICULES = os.path.join(RACINE, "Assets", "BalulaMoto", "Resources", "BalulaMoto", "Vehicules")
APERCUS = []


def apercu(nom, parts, vues=((1, 0.45, 1.1), (-1.1, 0.5, -0.9)), taille=(420, 320), dist=1.15):
    if not APERCUS:
        return
    mn, mx = A.bounds(parts)
    c = (mn + mx) / 2
    r = max(0.5, float(np.linalg.norm(mx - mn)))
    ims = []
    for v in vues:
        d = np.array(v, float)
        d /= np.linalg.norm(d)
        ims.append(A.render(parts, c + d * r * dist, c, taille[0], taille[1], fov=40, ss=1))
    A.sheet(ims, len(ims), os.path.join(APERCUS[0], nom + ".png"))


def charger_tex(chemin, taille=None, mode="RGB"):
    im = Image.open(chemin).convert(mode)
    if taille and max(im.size) > taille:
        r = taille / max(im.size)
        im = im.resize((max(4, int(im.size[0] * r)), max(4, int(im.size[1] * r))), Image.LANCZOS)
    return im


# ======================================================================================================================
# Fichier 2 : barrières (rouge et blanc, coupe nette au milieu de chaque élément)
# ======================================================================================================================
def barrieres(src):
    dossier = os.path.join(src, "f7509f5d-75-mgs_barriers")
    detail = charger_tex(os.path.join(dossier, "barrier1Diffuse.png"), 512, "L")
    a = np.asarray(detail, float)
    lo, hi = np.percentile(a, 3), np.percentile(a, 97)
    a = np.clip((a - lo) / max(1.0, hi - lo), 0, 1)
    # Détail du béton gardé discret : la peinture rouge et blanche domine
    a = 0.72 + 0.28 * a
    beton = Image.fromarray((a * 255).astype(np.uint8)).convert("RGB")
    rouge = {"name": "Rouge", "color": (0.80, 0.07, 0.06), "tex": beton}
    blanc = {"name": "Blanc", "color": (0.96, 0.96, 0.94), "tex": beton}
    metal = {"name": "Metal", "color": (0.25, 0.25, 0.26), "tex": None}
    for fichier, nom in (("barrier1.fbx", "Barriere_A"), ("barrier2.fbx", "Barriere_B")):
        parts = A.load_fbx(os.path.join(dossier, fichier))
        # Longueur le long de +Z (sens de la route), centrée, au sol
        A.transform(parts, A.rot_y(90))
        A.recentre(parts)
        out = []
        for p in parts:
            if p["name"].startswith("Bracket"):
                p["mat"] = metal
                p["name"] = "Crochet"
                out.append(p)
                continue
            neg, pos = A.cut_plane(p, 2, 0.0)
            for q, m in ((neg, rouge), (pos, blanc)):
                if q is None:
                    continue
                q["mat"] = m
                q["name"] = "Beton_" + m["name"]
                out.append(q)
        out = A.merge_by_material(out)
        out = A.decimate(out, 420)
        A.renormal(out, 40)
        nv, nt = A.write_obj(out, DECOR, nom, tex_size=512, comment=nom + " — barrière de béton (mgs_barriers), peinte rouge et blanc")
        print(nom, nv, "sommets", nt, "triangles", A.bounds(out))
        apercu(nom, out)


# ======================================================================================================================
# Fichier 1 : cottage
# ======================================================================================================================
def cottage(src):
    parts = A.load_blend(os.path.join(src, "67-cottage_free_blend", "Cottage_FREE.blend"), exclude=("Shadow", "Camera"))
    # Façade (porche, marches, porte) déjà tournée vers -Z : côté rue dans le jeu
    A.recentre(parts)
    for p in parts:
        p["mat"] = dict(p["mat"])
        p["mat"]["name"] = "Cottage"
        p["mat"]["color"] = (1.0, 1.0, 1.0)
        p["name"] = "Cottage"
    out = A.merge_by_material(parts)
    nv, nt = A.write_obj(out, DECOR, "Cottage", tex_size=1024, comment="Cottage (Cottage_FREE.blend), façade vers -Z")
    print("Cottage", nv, nt, A.bounds(out))
    apercu("Cottage", out, vues=((0.6, 0.4, -1), (-1, 0.5, 0.8)))


# ======================================================================================================================
# Fichier 3 : maison « fhouse » (extérieur, sans meubles ni plans parasites)
# ======================================================================================================================
FHOUSE_INTERIEUR = {
    'Plane.009', 'Plane.010', 'Plane.011', 'Plane.012', 'Plane.013', 'Plane.015', 'Plane.016', 'Plane.017', 'Plane.018',
    'Plane.020', 'Plane.026', 'Plane.027', 'Plane.030', 'Plane.031', 'Plane.032', 'Cube.006', 'Cube.008', 'Cube.009',
    'Cube.010', 'Cube.012', 'Plane.028', 'Plane.029', 'Plane.033', 'Plane', 'Circle', 'Plane.014', 'Cube.011', 'Cube.004',
    'Plane.005'}
FHOUSE_COL = {"Material.003": (0.93, 0.91, 0.87), "Material.002": (0.16, 0.16, 0.17), "Material.001": (0.45, 0.38, 0.27),
              "wall": (0.78, 0.68, 0.5), "window": (0.55, 0.66, 0.74), "table": (0.12, 0.1, 0.09), "Defaut": (0.8, 0.8, 0.8)}


def maison_f(src):
    dossier = os.path.join(src, "e5233a2f-37-fhouse")
    parts = [p for p in A.load_blend(os.path.join(dossier, "fhouse.blend"), search_dirs=(dossier,)) if p["name"] not in FHOUSE_INTERIEUR]
    # 4 unités Blender = 1 m ; entrée (renfoncement en L) tournée vers -Z (rue)
    A.transform(parts, A.rot_y(180), 0.25)
    A.recentre(parts)
    for p in parts:
        n = p["mat"]["name"]
        p["mat"] = dict(p["mat"])
        if p["mat"]["tex"] is not None:
            p["mat"]["color"] = (1.0, 1.0, 1.0)
        else:
            p["mat"]["color"] = FHOUSE_COL.get(n, p["mat"]["color"])
        p["mat"]["name"] = "F_" + "".join(ch if ch.isalnum() else "_" for ch in n)
        p["name"] = "Maison"
    out = A.merge_by_material(parts)
    nv, nt = A.write_obj(out, DECOR, "MaisonF", tex_size=512, comment="Maison fhouse (fhouse.blend), extérieur, entrée vers -Z")
    print("MaisonF", nv, nt, A.bounds(out))
    apercu("MaisonF", out, vues=((0.6, 0.4, -1), (-1, 0.5, 0.8)))


# ======================================================================================================================
# Fichier 13 : cinq immeubles (façades vitrées dessinées ici, les textures d'origine manquent)
# ======================================================================================================================
def texture_facade(path_size=512):
    """Une travée de 3,2 m sur un étage de 3,2 m : fenêtre vitrée encadrée, allège, bandeau de béton."""
    S = path_size
    rng = np.random.default_rng(7)
    base = 205 + rng.normal(0, 6, (S, S))
    base = Image.fromarray(np.clip(base, 0, 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.2)).convert("RGB")
    d = ImageDraw.Draw(base)
    m = S // 16
    # bandeau de plancher
    d.rectangle([0, S - m, S, S], fill=(170, 170, 168))
    # vitrage
    x0, y0, x1, y1 = int(S * 0.1), int(S * 0.12), int(S * 0.9), int(S * 0.78)
    glass = np.zeros((y1 - y0, x1 - x0, 3))
    yy = np.linspace(0, 1, y1 - y0)[:, None]
    glass[..., 0] = 60 + 50 * yy
    glass[..., 1] = 92 + 55 * yy
    glass[..., 2] = 118 + 60 * yy
    # reflet diagonal
    xx = np.linspace(0, 1, x1 - x0)[None, :]
    band = np.exp(-((xx - yy * 0.6 - 0.25) ** 2) / 0.004) * 70
    glass += band[..., None]
    base.paste(Image.fromarray(np.clip(glass, 0, 255).astype(np.uint8)), (x0, y0))
    d.rectangle([x0, y0, x1, y1], outline=(60, 62, 66), width=max(2, S // 96))
    d.line([((x0 + x1) // 2, y0), ((x0 + x1) // 2, y1)], fill=(60, 62, 66), width=max(2, S // 128))
    d.line([(x0, int(y0 + (y1 - y0) * 0.35)), (x1, int(y0 + (y1 - y0) * 0.35))], fill=(60, 62, 66), width=max(2, S // 160))
    # appui de fenêtre
    d.rectangle([x0 - 4, y1, x1 + 4, y1 + S // 40], fill=(225, 225, 222))
    return base


def texture_socle(S=512):
    """Rez-de-chaussée : vitrines sombres et rideaux métalliques."""
    im = Image.new("RGB", (S, S), (120, 118, 112))
    d = ImageDraw.Draw(im)
    d.rectangle([int(S * 0.06), int(S * 0.08), int(S * 0.94), int(S * 0.9)], fill=(40, 48, 56))
    for k in range(1, 4):
        x = int(S * 0.06 + (S * 0.88) * k / 4)
        d.line([(x, int(S * 0.08)), (x, int(S * 0.9))], fill=(150, 150, 150), width=S // 80)
    d.rectangle([0, 0, S, int(S * 0.08)], fill=(90, 90, 92))
    return im


def texture_toit(S=256):
    rng = np.random.default_rng(3)
    a = 150 + rng.normal(0, 10, (S, S))
    return Image.fromarray(np.clip(a, 0, 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.8)).convert("RGB")


TEINTES_IMMEUBLES = [(0.93, 0.89, 0.8), (0.86, 0.88, 0.9), (0.95, 0.93, 0.88), (0.9, 0.84, 0.74), (0.96, 0.96, 0.95)]


def immeubles(src):
    dossier = os.path.join(src, "8465e2dd-5Building_FBX")
    fac = {"name": "Imm_Facade", "color": (1, 1, 1), "tex": texture_facade()}
    soc = {"name": "Imm_Socle", "color": (1, 1, 1), "tex": texture_socle()}
    toi = {"name": "Imm_Toit", "color": (0.8, 0.8, 0.78), "tex": texture_toit()}
    for i in range(1, 6):
        parts = A.load_fbx(os.path.join(dossier, "Building_0%d.fbx" % i))
        A.recentre(parts)
        # Façade principale (la plus large) tournée vers -Z (rue) : on garde l'orientation d'origine
        teinte = TEINTES_IMMEUBLES[i - 1]
        mf = dict(fac, name="Imm%d_Facade" % i, color=teinte)
        ms = dict(soc, name="Imm%d_Socle" % i)
        mt = dict(toi, name="Imm%d_Toit" % i)
        out = []
        for p in parts:
            P = p["V"][p["F"]]
            fn = np.cross(P[:, 1] - P[:, 0], P[:, 2] - P[:, 0])
            fn /= np.maximum(1e-9, np.linalg.norm(fn, axis=1))[:, None]
            cy = P[:, :, 1].mean(1)
            up = fn[:, 1]
            vert = np.abs(up) < 0.7
            # UV planaires en mètres : une travée de 3,2 m par carreau de texture
            UV = np.zeros((len(p["F"]), 3, 2))
            t = np.stack([fn[:, 2], np.zeros(len(fn)), -fn[:, 0]], 1)
            t /= np.maximum(1e-9, np.linalg.norm(t, axis=1))[:, None]
            for k in range(3):
                UV[:, k, 0] = np.where(vert, (P[:, k] * t).sum(1) / 3.2, P[:, k, 0] / 4.0)
                UV[:, k, 1] = np.where(vert, 1 - P[:, k, 1] / 3.2, P[:, k, 2] / 4.0)
            q = dict(p)
            q["UV"] = UV.reshape(-1, 2)
            q["V"] = P.reshape(-1, 3)
            q["N"] = np.repeat(fn, 3, axis=0)
            q["F"] = np.arange(len(q["V"])).reshape(-1, 3)
            cls = np.where(~vert, 2, np.where(cy < 3.6, 1, 0))
            for c, m in ((0, mf), (1, ms), (2, mt)):
                if (cls == c).any():
                    out.append(A.tri_subset(q, cls == c, "Immeuble", m))
        out = A.merge_by_material(out)
        nom = "Immeuble_%d" % i
        nv, nt = A.write_obj(out, DECOR, nom, tex_size=512, comment=nom + " (5Building_FBX, Building_0%d), façades dessinées par le jeu" % i)
        print(nom, nv, nt, A.bounds(out))
        apercu(nom, out, vues=((0.7, 0.3, -1), (-1, 0.6, 0.8)), taille=(300, 420), dist=1.0)


# ======================================================================================================================
# Fichier 8 : mobilier urbain (Source Engine Street Objects, Leafia dev, CC-BY-4.0)
# ======================================================================================================================
# indice dans le GLB -> (nom, rotation Y pour mettre la longueur en X et la face vers +Z)
RUE = {1: ("Rue_Banc", 90), 5: ("Rue_Benne", 90), 6: ("Rue_Glissiere", 90), 7: ("Rue_Fut", 0), 8: ("Rue_Cone", 0),
       9: ("Rue_Auvent", -90), 13: ("Rue_Cartons", 0)}
DOUBLE_FACE = {"Rue_Auvent"}   # toile à une seule face dans le fichier : visible des deux côtés


def double_face(p):
    q = dict(p)
    q["V"] = np.concatenate([p["V"], p["V"]])
    q["N"] = np.concatenate([p["N"], -p["N"]])
    q["UV"] = np.concatenate([p["UV"], p["UV"]])
    n = len(p["V"])
    q["F"] = np.concatenate([p["F"], p["F"][:, ::-1] + n])
    return q


def mobilier(src, glb):
    parts = A.load_glb(glb)
    for i, (nom, rot) in RUE.items():
        p = copy.deepcopy(parts[i])
        # 1 unité du fichier = 10 unités Source (0,75 pouce) = 0,19 m
        A.transform([p], A.rot_y(rot), 0.19)
        A.recentre([p])
        p["name"] = nom
        p["mat"] = dict(p["mat"])
        p["mat"]["name"] = nom
        p["mat"]["color"] = (1.0, 1.0, 1.0)
        if nom in DOUBLE_FACE:
            p = double_face(p)
        nv, nt = A.write_obj([p], DECOR, nom, tex_size=256, comment=nom + " — Source Engine Street Objects (Leafia dev, CC-BY-4.0)")
        print(nom, nv, nt, (A.bounds([p])[1] - A.bounds([p])[0]).round(2))
        apercu(nom, [p], vues=((0.3, 0.35, 1), (1, 0.4, -0.4)), taille=(300, 260))


# ======================================================================================================================
# Motos : groupes fonctionnels pour le jeu (préfixes lus par ModelMotos.cs)
#   Caisse_<rôle>_<mat>     : partie fixe (cadre, moteur, réservoir, selle…)
#   Direction_<rôle>_<mat>  : tourne avec le guidon (fourche, phare, compteurs, rétroviseurs, garde-boue avant)
#   RoueAV_<rôle>_<mat>, RoueAR_<rôle>_<mat> : roues (tournent autour de leur moyeu)
#   rôle : Corps, Phare, FeuAR, ClignoG, ClignoD, Plaque, Peinture (couleur choisie par le joueur)
# ======================================================================================================================
def nom_mat(s):
    return "".join(ch if ch.isalnum() else "x" for ch in s)


def groupe(p, grp, role, mat_name=None, color=None):
    m = dict(p["mat"])
    if mat_name is not None:
        m["name"] = mat_name
    if color is not None:
        m["color"] = color
    m["tex"] = None
    q = dict(p)
    q["mat"] = m
    q["name"] = "%s_%s_%s" % (grp, role, nom_mat(m["name"]))
    return q


def fusion_groupes(parts):
    return A.merge_by_material(parts, group_of=lambda p: p["name"])


def roue_info(parts, prefix):
    sel = [p for p in parts if p["name"].startswith(prefix)]
    if not sel:
        return None
    mn, mx = A.bounds(sel)
    c = (mn + mx) / 2
    return c, (mx[1] - mn[1]) / 2


def ecrire_moto(parts, nom, cible_haute, cible_lod, comment, garder=()):
    parts = fusion_groupes(parts)
    hi = A.decimate(parts, cible_haute, keep_small=2600, keep_names=garder)
    A.renormal(hi, 50)
    nv, nt = A.write_obj(hi, VEHICULES, nom, comment=comment)
    print(nom, nv, "sommets", nt, "triangles")
    lod = A.decimate(parts, cible_lod)
    A.renormal(lod, 50)
    nv2, nt2 = A.write_obj(lod, VEHICULES, nom + "_LOD", comment=comment + " — version légère (circulation, parkings)")
    print(nom + "_LOD", nv2, "sommets", nt2, "triangles")
    for pref in ("RoueAV", "RoueAR"):
        ri = roue_info(hi, pref)
        if ri:
            print("   ", pref, "centre", np.round(ri[0], 3), "rayon", round(float(ri[1]), 3))
    mn, mx = A.bounds(hi)
    print("    dimensions", np.round(mx - mn, 3), "min", np.round(mn, 3), "max", np.round(mx, 3))
    apercu(nom, hi, vues=((1, 0.35, 0.9), (-1, 0.4, -0.5), (0, 0.6, 1.2)), taille=(420, 320), dist=1.0)
    if APERCUS:
        # Contrôle des groupes : direction en vert, roues en bleu, feux en jaune
        couleurs = []
        for p in hi:
            q = dict(p)
            q["mat"] = dict(p["mat"])
            n = p["name"]
            q["mat"]["color"] = (0.15, 0.75, 0.2) if n.startswith("Direction") else ((0.2, 0.35, 0.95) if n.startswith("Roue") else (0.6, 0.6, 0.6))
            if "_Phare_" in n or "_FeuAR_" in n or "_Cligno" in n:
                q["mat"]["color"] = (1.0, 0.85, 0.1)
            if "_Peinture_" in n:
                q["mat"]["color"] = (0.85, 0.15, 0.15)
            couleurs.append(q)
        apercu(nom + "_groupes", couleurs, vues=((1, 0.3, 0.2), (-1, 0.3, -0.2), (0.2, 0.5, 1.2)), taille=(420, 320), dist=1.0)
    return hi


# ---------------------------------------------------------------- Fichier 4 : moto centrale (motor.blend)
MOTO1_COL = {"metal_red": (0.62, 0.02, 0.02), "metal_silver": (0.82, 0.83, 0.85), "metal_engine": (0.62, 0.63, 0.64),
             "metal_black": (0.04, 0.04, 0.045), "tire": (0.05, 0.05, 0.05), "leather": (0.06, 0.05, 0.05),
             "flash": (1.0, 0.55, 0.05), "front_light": (0.9, 0.9, 0.86), "light_back": (0.6, 0.04, 0.03),
             "mirror": (0.75, 0.82, 0.9), "plate_black": (0.05, 0.05, 0.05), "plate_white": (0.95, 0.95, 0.93),
             "gauge_rpm_black": (0.06, 0.06, 0.07), "gauge_km_black": (0.06, 0.06, 0.07), "gauge_rpm_silver": (0.7, 0.71, 0.72),
             "gauge_km_metal": (0.7, 0.71, 0.72)}
MOTO1_DIRECTION = {"Front_water", "Headlight", "frontWheelShaft", "front_bumper", "front_turnlights", "gauge_rpm",
                   "gauge_speed", "mirrors", "steering"}


def moto_centrale(src):
    dossier = os.path.join(src, "73bdace9-57-moto1", "moto1")
    parts = A.load_blend(os.path.join(dossier, "motor.blend"), exclude=("road", "Camera", "trademark"), search_dirs=(dossier,))
    parts = [p for p in parts if p["name"] != "trademark"]
    # Repère du fichier : avant vers +X, sol à y = -2,25 ; 1 unité = 0,27 m (roue de 0,62 m)
    A.transform(parts, A.rot_y(-90), 0.27)
    fa = (3.87 * 0.27, -1.09 * 0.27)
    ra = (-1.105 * 0.27, -1.055 * 0.27)
    mid = (fa[0] + ra[0]) / 2
    for p in parts:
        p["V"] = p["V"] + np.array([0.0, 2.25 * 0.27, -mid])
    out = []
    for p in parts:
        n, m = p["name"], p["mat"]["name"]
        col = MOTO1_COL.get(m, p["mat"]["color"])
        if n == "Front_wheel":
            out.append(groupe(p, "RoueAV", "Corps", m, col)); continue
        if n == "back_wheel":
            out.append(groupe(p, "RoueAR", "Corps", m, col)); continue
        grp = "Direction" if n in MOTO1_DIRECTION else "Caisse"
        role = "Corps"
        if m == "front_light":
            role = "Phare"
        elif m == "light_back":
            role = "FeuAR"
        elif m == "flash":
            # Clignotants séparés par côté (X > 0 = gauche)
            cx = A.centroids(p)[:, 0]
            for q in A.split_mask(p, cx > 0, n, n):
                side = "ClignoG" if A.centroids(q)[:, 0].mean() > 0 else "ClignoD"
                out.append(groupe(q, grp, side, m, col))
            continue
        elif n == "numberPlate":
            role = "Plaque"
        elif m == "metal_red":
            role = "Peinture"
        out.append(groupe(p, grp, role, m, col))
    ecrire_moto(out, "MotoCentrale", 26000, 6500, "Moto centrale (moto1/motor.blend fourni par MATULA DESIGN), sans logo")


# ---------------------------------------------------------------- Fichier 11 : Honda CB750 Four
CB750_PEINTURE = {"0CarrozzeriaLP"}


def honda_cb750(src, fbx):
    parts = A.load_fbx(fbx)
    for p in parts:
        if p["name"] == "0Gomma":
            # Objet exporté avec une inversion parasite (rotation 180° + échelle -1) : on la retire
            p["V"] = -p["V"]
            p["N"] = -p["N"]
            p["F"] = p["F"][:, ::-1]
    parts = [p for p in parts if p["name"] != "0FregioCromato"]   # emblème chromé retiré
    A.set_color(parts, CB750_COL)
    fa = np.array([0.0, 0.335, 0.745])
    ra = np.array([0.0, 0.335, -0.675])
    mid = (fa[2] + ra[2]) / 2
    for p in parts:
        p["V"] = p["V"] - np.array([0.0, 0.0, mid])
    fa[2] -= mid
    ra[2] -= mid
    head = np.array([0.0, 1.0, 0.47 - mid])
    u = np.array([0.0, math.cos(math.radians(27)), -math.sin(math.radians(27))])
    roue_mats = {"0Pneumatici", "raggi", "Cerchi", "CromMed", "steel", "CromeVarie", "0BulloniBig", "0minuteria", "0Ingranaggio", "aria"}
    jamais_dir = {"frame", "0CarrozzeriaLP", "engine", "CarterHonda", "CarterTestata", "catena", "11 - Default", "terminali",
                  "0Ingranaggio", "PlasticaBianca", "Pedale", "tela"}
    out = []
    for p in parts:
        m = p["mat"]["name"]
        P = p["V"][p["F"]]
        c = P.mean(1)
        # roue avant / arrière : triangles entièrement dans le cylindre de la roue
        def dans_roue(ax, half):
            dyz = np.sqrt((P[:, :, 1] - ax[1]) ** 2 + (P[:, :, 2] - ax[2]) ** 2)
            return (dyz.max(1) < 0.347) & (np.abs(P[:, :, 0]).max(1) < half)
        rf = dans_roue(fa, 0.115) if m in roue_mats else np.zeros(len(P), bool)
        rr = dans_roue(ra, 0.135) if m in roue_mats else np.zeros(len(P), bool)
        # direction : en avant de l'axe de direction (fourche, phare, freins avant), garde-boue avant, guidon et compteurs
        rel = c - head
        s = rel[:, 2] * u[1] - rel[:, 1] * u[2]           # distance signée à l'axe (vers l'avant > 0)
        dfa = np.sqrt((c[:, 1] - fa[1]) ** 2 + (c[:, 2] - fa[2]) ** 2)
        garde = (dfa > 0.28) & (dfa < 0.48) & (np.abs(c[:, 0]) < 0.13) & (c[:, 1] > fa[1] - 0.02)
        avant = (s > -0.085) & (c[:, 1] > 0.12) & (c[:, 2] > fa[2] - 0.55)
        hauts = np.isin(m, ["Manubrio", "Specchio", "Tachimetri", "VetroClear", "cruscotto", "Fanale"]) & (c[:, 1] > 0.8) & (c[:, 2] > head[2] - 0.45)
        clig = (m == "Frecce") & (c[:, 2] > 0)
        dirm = (avant | garde | hauts | clig) & ~rf & ~rr
        if m in jamais_dir:
            dirm &= False
        cls = np.where(rf, 1, np.where(rr, 2, np.where(dirm, 3, 0)))
        for k, grp in ((0, "Caisse"), (1, "RoueAV"), (2, "RoueAR"), (3, "Direction")):
            if not (cls == k).any():
                continue
            q = A.tri_subset(p, cls == k)
            role = "Corps"
            if m == "Fanale":
                role = "Phare"
            elif m == "stop":
                role = "FeuAR"
            elif m == "Frecce":
                role = None
            elif m in CB750_PEINTURE:
                role = "Peinture"
            if role is None:
                cx = A.centroids(q)[:, 0]
                for qq in A.split_mask(q, cx > 0, "g", "d"):
                    out.append(groupe(qq, grp, "ClignoG" if A.centroids(qq)[:, 0].mean() > 0 else "ClignoD", m))
                continue
            out.append(groupe(q, grp, role, m))
    print("   CB750 : tête de direction", np.round(head, 3), "axe", np.round(u, 3))
    ecrire_moto(out, "HondaCB750", 30000, 7000, "Honda CB750 Four (honda_cb_750_four_TS_00.FBX fourni par MATULA DESIGN), sans emblème")


# ---------------------------------------------------------------- Fichier 12 : Honda (honda.fbx)
def honda_classique(src, fbx):
    parts = [p for p in A.load_fbx(fbx) if p["name"] in ("arm_belakang", "ban_belakang", "ban_depan", "chassis", "cockpit",
                                                          "shock_belakang", "spakbor_depan", "stang")]
    A.set_color(parts, HONDA_COL)
    # Avant vers -Z dans le fichier : demi-tour ; 1 unité = 0,18 m
    A.transform(parts, A.rot_y(180), 0.18)
    fr = roue_info([dict(p, name="RoueAV") for p in parts if p["name"] == "ban_depan" and p["mat"]["name"] == "ban"], "RoueAV")
    rr = roue_info([dict(p, name="RoueAR") for p in parts if p["name"] == "ban_belakang" and p["mat"]["name"] == "ban"], "RoueAR")
    mid = (fr[0][2] + rr[0][2]) / 2
    ymin = A.bounds(parts)[0][1]
    for p in parts:
        p["V"] = p["V"] - np.array([0.0, ymin, mid])
    out = []
    for p in parts:
        n, m = p["name"], p["mat"]["name"]
        if n == "ban_depan":
            if m == "abu-abu":
                out.append(groupe(p, "Direction", "Corps", m))   # étrier de frein : ne tourne pas avec la roue
            else:
                out.append(groupe(p, "RoueAV", "Corps", m))
            continue
        if n == "ban_belakang":
            out.append(groupe(p, "RoueAR", "Corps", m)); continue
        grp = "Direction" if n in ("stang", "cockpit", "spakbor_depan") else "Caisse"
        if m in ("lampu", "mika_lampu"):
            c = A.centroids(p)
            if n == "cockpit":
                # phare au centre, clignotants avant sur les côtés
                side = np.abs(c[:, 0]) > 0.09
                for q in A.split_mask(p, side, n, n):
                    cq = A.centroids(q)
                    if np.abs(cq[:, 0]).mean() > 0.09:
                        for qq in A.split_mask(q, cq[:, 0] > 0, n, n):
                            out.append(groupe(qq, grp, "ClignoG" if A.centroids(qq)[:, 0].mean() > 0 else "ClignoD", m, (1.0, 0.55, 0.05)))
                    else:
                        out.append(groupe(q, grp, "Phare", m))
            else:
                out.append(groupe(p, grp, "FeuAR", m, (0.6, 0.04, 0.03)))
            continue
        if m == "belakang_sein":
            c = A.centroids(p)
            for q in A.split_mask(p, c[:, 0] > 0, n, n):
                out.append(groupe(q, grp, "ClignoG" if A.centroids(q)[:, 0].mean() > 0 else "ClignoD", m))
            continue
        role = "Peinture" if m in ("plastik", "body") else "Corps"
        out.append(groupe(p, grp, role, m))
    ecrire_moto(out, "HondaClassique", 33000, 7000, "Moto Honda (honda.fbx fourni par MATULA DESIGN), modèle exact à vérifier",
                garder=("Direction_Corps_spion", "Direction_Corps_besi", "Direction_Corps_rangka", "Direction_Corps_karetxspion"))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("sources", help="dossier des archives extraites (assets_recus)")
    ap.add_argument("--glb", default="/mnt/user-data/uploads/d899b6d0-source_engine_street_objects.glb")
    ap.add_argument("--cb750", default="/mnt/user-data/uploads/f59da8f8-honda_cb_750_four_TS_00.FBX")
    ap.add_argument("--seulement", default="")
    ap.add_argument("--apercu", default="")
    a = ap.parse_args()
    if a.apercu:
        os.makedirs(a.apercu, exist_ok=True)
        APERCUS.append(a.apercu)
    os.makedirs(DECOR, exist_ok=True)
    taches = {
        "barrieres": lambda: barrieres(a.sources),
        "cottage": lambda: cottage(a.sources),
        "maison": lambda: maison_f(a.sources),
        "immeubles": lambda: immeubles(a.sources),
        "mobilier": lambda: mobilier(a.sources, a.glb),
        "moto": lambda: moto_centrale(a.sources),
        "cb750": lambda: honda_cb750(a.sources, a.cb750),
        "honda": lambda: honda_classique(a.sources, os.path.join(a.sources, "df22f9b0-honda_fbx", "honda.fbx")),
    }
    choix = [s for s in a.seulement.split(",") if s] or list(taches)
    for k in choix:
        print("==", k)
        taches[k]()


if __name__ == "__main__":
    main()
