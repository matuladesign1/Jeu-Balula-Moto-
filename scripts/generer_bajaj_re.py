#!/usr/bin/env python3
"""BALULA MOTO — tricycle Bajaj RE de MATULA DESIGN, d'après son script Blender
(Docs/ModelesSources/Bajaj_RE_Animated_Unity_Generator.py), converti en OBJ + MTL pour Unity, sans Blender.

Le script Blender construit le tricycle avec des primitives (cubes arrondis, sphères, cylindres, tiges). Ce programme
refait exactement les mêmes pièces, aux mêmes positions et avec les mêmes couleurs, puis :
  - ramène la hauteur à celle du vrai Bajaj RE (1,71 m ; le script, fait à partir de deux photos, montait à 2,44 m
    avec des roues de 78 cm) : hauteurs × 0,70, roues au diamètre réel (48 cm) aux emplacements utilisés par la
    physique du jeu (empattement 2,00 m, voie arrière 1,15 m) ;
  - aménage l'intérieur pour que le chauffeur et les passagers tiennent assis sous le toit : banquette avant
    (chauffeur + 2 places), banquette arrière (3 places) creusée dans la caisse arrière, plancher bas ; vu de
    l'extérieur, la caisse est la même ;
  - rend visibles les vitres latérales (dans le script, elles étaient 2 cm à l'intérieur de la bâche, donc cachées) ;
  - sépare les pièces dont le jeu a besoin : 3 roues (pneu, moyeu, enjoliveur), phares, clignotants, feux arrière,
    essuie-glace (balayage sous la pluie), plaque arrière.
Axes Unity : x vers la droite, y vers le haut, z vers l'avant ; sol à y = 0.

Usage : python3 scripts/generer_bajaj_re.py [--apercu dossier]
"""
import math
import os
import sys

import numpy as np

ICI = os.path.dirname(os.path.abspath(__file__))
DEST = os.path.join(ICI, "..", "Assets", "BalulaMoto", "Resources", "BalulaMoto", "Vehicules")
NOM = "BajajRE"

SZ = 0.70        # hauteurs du script × 0,70 : toit à 1,70 m comme le vrai Bajaj RE
DZ = -0.125      # décalage avant/arrière : passages de roue au-dessus des roues du jeu
ROUE_R = 0.24    # rayon réel des roues (pneus 4.00-8), comme la physique du jeu
ROUE_AV = (0.0, ROUE_R, 1.10)            # positions Unity (empattement 2,00 m : 0,55 devant, 0,45 derrière)
ROUE_AR = (0.575, ROUE_R, -0.90)         # voie arrière 1,15 m

# Couleurs du script (Principled BSDF, linéaires) -> sRGB pour Unity
MATS = {
    "Body_Blue_Reference": (0.015, 0.22, 0.62),
    "Bumper_Black_Grey": (0.035, 0.04, 0.05),
    "Canopy_Off_White_Beige": (0.76, 0.73, 0.66),
    "Window_Glass_Tinted": (0.12, 0.19, 0.22),
    "Tyre_Rubber": (0.012, 0.014, 0.016),
    "Metal_Silver": (0.42, 0.45, 0.48),
    "Tail_Lamp_Red": (0.55, 0.012, 0.018),
    "Indicator_Amber": (1.0, 0.24, 0.015),
    "Headlamp_White": (0.82, 0.85, 0.82),
    "Plate_Yellow": (1.0, 0.65, 0.015),
    "Seat_Light_Grey": (0.58, 0.57, 0.53),
}
BLUE, DARK, CANVAS, GLASS, RUBBER, METAL, RED, AMBER, WHITE, YELLOW, SEAT = list(MATS)


def srgb(c):
    return tuple(12.92 * v if v <= 0.0031308 else 1.055 * v ** (1 / 2.4) - 0.055 for v in c)


# ----------------------------------------------------------------------------------------------------------------
# Primitives (repère Blender : X largeur, Y longueur avec l'avant vers -Y, Z hauteur), faces dans le sens direct
# ----------------------------------------------------------------------------------------------------------------
class Mesh:
    def __init__(self):
        self.v, self.n, self.f = [], [], []

    def add(self, v, n):
        self.v.append(np.asarray(v, float))
        self.n.append(np.asarray(n, float) / max(np.linalg.norm(n), 1e-9))
        return len(self.v) - 1


def rounded_box(dims, r, fine):
    """Cube arrondi (modificateur Biseau du script) : rayon r, limité comme Blender (pas de chevauchement)."""
    h = np.asarray(dims, float) / 2.0
    r = min(r, float(h.min()) * 0.999)
    angles = [0.0, 30.0] if fine else [0.0]
    ts = [r * math.tan(math.radians(a)) for a in angles] + [r]

    def coords(hh):
        c0 = hh - r
        pos = [c0 + t for t in ts]
        neg = [-p for p in reversed(pos)]
        out = neg + pos if c0 > 1e-6 else neg[:-1] + pos
        return out

    C = [coords(h[i]) for i in range(3)]
    m = Mesh()
    c0 = h - r
    for axis in range(3):
        for sign in (-1.0, 1.0):
            a1, a2 = (axis + 1) % 3, (axis + 2) % 3
            grid = {}
            for i, u in enumerate(C[a1]):
                for j, w in enumerate(C[a2]):
                    p = np.zeros(3)
                    p[axis] = sign * h[axis]
                    p[a1] = u
                    p[a2] = w
                    inner = np.clip(p, -c0, c0)
                    d = p - inner
                    nd = np.linalg.norm(d)
                    nrm = d / nd if nd > 1e-9 else np.eye(3)[axis] * sign
                    grid[i, j] = m.add(inner + nrm * r, nrm)
            for i in range(len(C[a1]) - 1):
                for j in range(len(C[a2]) - 1):
                    q = [grid[i, j], grid[i + 1, j], grid[i + 1, j + 1], grid[i, j + 1]]
                    if sign < 0:
                        q.reverse()
                    m.f.append(q)
    return m


def uv_sphere(seg, rings):
    m = Mesh()
    top = m.add((0, 0, 1), (0, 0, 1))
    rows = []
    for k in range(1, rings):
        th = math.pi * k / rings
        row = []
        for s in range(seg):
            ph = 2 * math.pi * s / seg
            p = (math.sin(th) * math.cos(ph), math.sin(th) * math.sin(ph), math.cos(th))
            row.append(m.add(p, p))
        rows.append(row)
    bot = m.add((0, 0, -1), (0, 0, -1))
    for s in range(seg):
        m.f.append([top, rows[0][s], rows[0][(s + 1) % seg]])
    for k in range(len(rows) - 1):
        for s in range(seg):
            a, b = rows[k][s], rows[k][(s + 1) % seg]
            c, d = rows[k + 1][(s + 1) % seg], rows[k + 1][s]
            m.f.append([a, d, c, b])
    for s in range(seg):
        m.f.append([bot, rows[-1][(s + 1) % seg], rows[-1][s]])
    return m


def cylinder(radius, depth, sides, bevel):
    """Cylindre d'axe Z, bords chanfreinés (modificateur Biseau du script)."""
    m = Mesh()
    hz = depth / 2.0
    b = min(bevel, hz * 0.9, radius * 0.5)
    ring = lambda rr, z, nf: [m.add((rr * math.cos(2 * math.pi * s / sides), rr * math.sin(2 * math.pi * s / sides), z),
                                    nf(2 * math.pi * s / sides)) for s in range(sides)]
    side = lambda a: (math.cos(a), math.sin(a), 0)
    up = lambda a: (0, 0, 1)
    down = lambda a: (0, 0, -1)
    cham_t = lambda a: (math.cos(a), math.sin(a), 1)
    cham_b = lambda a: (math.cos(a), math.sin(a), -1)
    capb = ring(radius - b, -hz, down)
    chb0 = ring(radius - b, -hz, cham_b)
    chb1 = ring(radius, -hz + b, cham_b)
    s0 = ring(radius, -hz + b, side)
    s1 = ring(radius, hz - b, side)
    cht1 = ring(radius, hz - b, cham_t)
    cht0 = ring(radius - b, hz, cham_t)
    capt = ring(radius - b, hz, up)
    cb = m.add((0, 0, -hz), (0, 0, -1))
    ct = m.add((0, 0, hz), (0, 0, 1))
    for s in range(sides):
        t = (s + 1) % sides
        m.f.append([cb, capb[t], capb[s]])
        m.f.append([chb0[s], chb0[t], chb1[t], chb1[s]])
        m.f.append([s0[s], s0[t], s1[t], s1[s]])
        m.f.append([cht1[s], cht1[t], cht0[t], cht0[s]])
        m.f.append([ct, capt[s], capt[t]])
    return m


def rot_euler(rx, ry, rz):
    cx, sx, cy, sy, cz, sz = math.cos(rx), math.sin(rx), math.cos(ry), math.sin(ry), math.cos(rz), math.sin(rz)
    Rx = np.array([[1, 0, 0], [0, cx, -sx], [0, sx, cx]])
    Ry = np.array([[cy, 0, sy], [0, 1, 0], [-sy, 0, cy]])
    Rz = np.array([[cz, -sz, 0], [sz, cz, 0], [0, 0, 1]])
    return Rz @ Ry @ Rx  # ordre XYZ de Blender


def rot_track_z(d):
    """Rotation qui amène l'axe Z local sur la direction d (to_track_quat('Z', 'Y') : seule la direction compte)."""
    z = np.asarray(d, float) / np.linalg.norm(d)
    ref = np.array([0.0, 0.0, 1.0]) if abs(z[2]) < 0.9 else np.array([0.0, 1.0, 0.0])
    x = np.cross(ref, z)
    x /= np.linalg.norm(x)
    y = np.cross(z, x)
    return np.stack([x, y, z], axis=1)


# ----------------------------------------------------------------------------------------------------------------
# Pièces
# ----------------------------------------------------------------------------------------------------------------
PARTS = []   # (groupe, matériau, Mesh déjà placé en repère Blender final)


def place(m, R=None, loc=(0, 0, 0), scale=None, sz=True):
    """Place la pièce (échelle, rotation, position), puis applique l'écrasement des hauteurs (sz)."""
    out = Mesh()
    S = np.ones(3) if scale is None else np.asarray(scale, float)
    R = np.eye(3) if R is None else R
    k = np.array([1.0, 1.0, SZ if sz else 1.0])
    for v, n in zip(m.v, m.n):
        p = (R @ (v * S)) + np.asarray(loc, float)
        nn = R @ (n / S)
        out.add(p * k, nn / k)
    out.f = [list(f) for f in m.f]
    return out


def cube(loc, dims, mat, bevel=0.04, group="Caisse", sz=True, fine=False):
    PARTS.append((group, mat, place(rounded_box(dims, bevel, fine), loc=loc, sz=sz)))


def uv(loc, scale, mat, group="Caisse", sz=True, seg=18, rings=9):
    PARTS.append((group, mat, place(uv_sphere(seg, rings), loc=loc, scale=scale, sz=sz)))


def cyl(loc, radius, depth, mat, rot=(0, 0, 0), sides=16, group="Caisse", sz=True, bevel=0.018, R=None):
    R = rot_euler(*rot) if R is None else R
    PARTS.append((group, mat, place(cylinder(radius, depth, sides, bevel), R=R, loc=loc, sz=sz)))


def rod(a, b, radius, mat, group="Caisse", sz=True, sides=8):
    a, b = np.asarray(a, float), np.asarray(b, float)
    d = b - a
    cyl((a + b) / 2, radius, float(np.linalg.norm(d)), mat, sides=sides, group=group, sz=sz, R=rot_track_z(d))


def side(x):
    """Côté Unity d'une pièce placée en X Blender : X > 0 → gauche (G), X < 0 → droite (D)."""
    return "G" if x > 0 else "D"


r90 = math.radians(90)

# --- Caisse (valeurs du script) ---
cube((0, -0.82, 1.00), (1.22, 0.74, 0.90), BLUE, 0.22, fine=True)           # Front_Body_Blue
uv((0, -1.03, 0.83), (0.60, 0.40, 0.50), BLUE)                              # Front_Nose_Rounded
cube((0, 1.155, 0.99), (0.92, 0.045, 0.72), BLUE, 0.09)                     # Rear_Engine_Panel
cube((0, 1.184, 1.08), (0.66, 0.025, 0.34), DARK, 0.06)                     # Rear_Grille
for z in (0.93, 1.00, 1.07, 1.14, 1.21):
    cube((0, 1.202, z), (0.60, 0.012, 0.012), METAL, 0.004)                 # Grille_Slat
cube((0, -1.245, 0.79), (1.03, 0.12, 0.43), DARK, 0.13)          # Front_Fascia
cube((0, -1.32, 0.59), (1.10, 0.12, 0.13), DARK, 0.05)                      # Front_Bumper
cube((0, 1.24, 0.55), (1.18, 0.13, 0.13), BLUE, 0.05)                       # Rear_Bumper
for x in (-0.36, 0.36):
    cyl((x, -1.315, 0.79), 0.145, 0.045, DARK, (r90, 0, 0))                 # Headlamp_Rim
    cyl((x, -1.343, 0.79), 0.112, 0.025, WHITE, (r90, 0, 0), group="Phare_" + side(x))
for x in (-0.46, 0.46):
    cube((x, -1.31, 1.12), (0.19, 0.055, 0.10), AMBER, 0.035, group="Cligno_AV" + side(x))
for x in (-0.53, 0.53):
    cube((x, 1.19, 0.94), (0.16, 0.07, 0.35), RED, 0.055, group="Feu_AR" + side(x))
    cube((x, 1.23, 0.78), (0.15, 0.025, 0.08), AMBER, 0.018, group="Cligno_AR" + side(x))
cube((0, 1.245, 0.69), (0.43, 0.025, 0.20), YELLOW, 0.018, group="Plaque_AR")  # Rear_Number_Plate
cube((0, -1.345, 1.00), (0.44, 0.025, 0.19), YELLOW, 0.018)                 # Front_Number_Plate
uv((0, -1.16, 0.69), (0.43, 0.40, 0.12), BLUE)                              # Front_Mudguard
for x in (-0.62, 0.62):
    uv((x, 0.72, 0.68), (0.30, 0.62, 0.19), BLUE)                           # Rear_Wheel_Arch

# Caisse arrière (Rear_Body_Blue : x ±0,66, y 0,20 → 1,14, z 0,58 → 1,46) : mêmes faces extérieures, creusée pour la
# banquette arrière (sinon les passagers auraient la tête dans le toit)
cube((0, 0.97, 1.02), (1.32, 0.34, 0.88), BLUE, 0.16)            # bloc arrière (moteur)
for x in (-0.63, 0.63):
    cube((x, 0.58, 1.02), (0.06, 0.76, 0.88), BLUE, 0.16)                   # flancs
cube((0, 0.50, 0.45), (1.20, 0.60, 0.11), BLUE, 0.02, sz=False)             # dessous de banquette

# --- Cabine / bâche (valeurs du script) ---
for x in (-0.59, 0.59):
    rod((x, -0.72, 1.30), (x, -0.68, 2.28), 0.045, CANVAS)                  # Cabin_Front_Pillar
    rod((x, 0.98, 1.30), (x, 0.90, 2.25), 0.045, CANVAS)                    # Cabin_Rear_Pillar
    rod((x, 0.10, 1.30), (x, 0.10, 2.26), 0.035, CANVAS)                    # Cabin_Mid_Frame
cube((0, 0.10, 2.30), (1.48, 2.08, 0.27), CANVAS, 0.20, fine=True)          # Canopy_Roof_OffWhite
cube((0, 1.02, 1.83), (1.23, 0.12, 0.90), CANVAS, 0.09)                     # Rear_Canvas_Wall
cube((0, 1.088, 1.93), (0.72, 0.025, 0.40), GLASS, 0.09)                    # Rear_Window
for x in (-0.635, 0.635):
    cube((x, 0.12, 1.82), (0.10, 1.55, 0.87), CANVAS, 0.07)                 # Side_Canvas_Panel
    cube((math.copysign(0.69, x), 0.14, 1.90), (0.025, 0.73, 0.37), GLASS, 0.08)  # Side_Window (sortie de la bâche)
cube((0, -0.70, 1.82), (1.12, 0.035, 0.79), GLASS, 0.08)                    # Front_Windshield_Glass
for x in (-0.60, 0.60):
    rod((x, -0.73, 1.37), (x, -0.73, 2.24), 0.035, METAL)                   # Windscreen_Frame_Side
rod((-0.60, -0.73, 2.23), (0.60, -0.73, 2.23), 0.035, CANVAS)               # Windscreen_Frame_Top
rod((-0.60, -0.73, 1.39), (0.60, -0.73, 1.39), 0.035, DARK)                 # Windscreen_Frame_Bottom
# Essuie-glace : dans le script, la tige est rattachée au pivot (0 ; -0,765 ; 1,48) puis déplacée de (0,24 ; 0 ; 0,32)
rod((0.0, -0.765, 1.48), (0.48, -0.765, 2.12), 0.018, METAL, group="Essuie_Glace", sides=8)
for x in (-1, 1):
    rod((x * 0.58, -0.68, 1.62), (x * 0.82, -0.70, 1.78), 0.025, METAL, sides=8)   # Mirror_Stem
    uv((x * 0.86, -0.70, 1.82), (0.12, 0.07, 0.19), DARK, seg=12, rings=6)          # Side_Mirror

# --- Intérieur (hauteurs réelles) : plancher bas, banquette avant (chauffeur + 2), banquette arrière (3) ---
cube((0, -0.125, 0.33), (1.20, 0.65, 0.06), DARK, 0.02, sz=False)           # plancher de cabine
cube((0, -0.05, 0.45), (0.80, 0.36, 0.18), DARK, 0.03, sz=False)            # socle de la banquette avant
cube((0, -0.05, 0.54), (1.02, 0.42, 0.12), SEAT, 0.04, sz=False)            # Front_Bench_Seat (assise à 0,60 m)
cube((0, 0.20, 0.80), (0.50, 0.08, 0.36), SEAT, 0.03, sz=False)             # dossier du chauffeur
cube((0, 0.53, 0.555), (1.10, 0.42, 0.11), SEAT, 0.04, sz=False)            # banquette arrière (assise à 0,61 m)
cube((0, 0.80, 0.86), (1.10, 0.12, 0.50), SEAT, 0.05, sz=False)             # Front_Bench_Back, contre le bloc arrière


# --- Roues : diamètre réel, aux emplacements de la physique du jeu (pneu, moyeu, enjoliveur côté extérieur) ---
def roue(groupe, pos_unity, r_script, w_script, cap_sign):
    k = ROUE_R / r_script
    X, Z, Y = -pos_unity[0], pos_unity[1], -(pos_unity[2] - DZ)
    w = w_script * k
    Ry = rot_euler(0, r90, 0)   # axe de roue selon X
    cyl((X, Y, Z), ROUE_R, w, RUBBER, sides=22, group=groupe, sz=False, bevel=0.018 * k, R=Ry)
    cyl((X, Y, Z), ROUE_R * 0.47, w * 1.04, METAL, sides=14, group=groupe, sz=False, bevel=0.018 * k, R=Ry)
    cyl((X + cap_sign * w * 0.54, Y, Z), ROUE_R * 0.18, 0.025 * k, DARK, sides=10, group=groupe, sz=False, bevel=0.008, R=Ry)


roue("Roue_AV", ROUE_AV, 0.36, 0.17, 1.0)
roue("Roue_ARG", (-ROUE_AR[0], ROUE_AR[1], ROUE_AR[2]), 0.39, 0.18, 1.0)    # gauche Unity = X Blender positif
roue("Roue_ARD", ROUE_AR, 0.39, 0.18, -1.0)


# ----------------------------------------------------------------------------------------------------------------
# Écriture OBJ (Unity retourne l'axe X des OBJ) : v = (X, Z, -Y + DZ) ; Unity lit (-X, Z, -Y + DZ)
# ----------------------------------------------------------------------------------------------------------------
def to_unity(p):
    return np.array([-p[0], p[2], -p[1] + DZ])


def ecrire():
    os.makedirs(DEST, exist_ok=True)
    with open(os.path.join(DEST, NOM + ".mtl"), "w") as f:
        for n, c in MATS.items():
            s = srgb(c)
            f.write("newmtl %s\nKd %.4f %.4f %.4f\nKa 0 0 0\nKs 0.05 0.05 0.05\nNs 10\nd 1\nillum 2\n\n" % (n, s[0], s[1], s[2]))
    groupes = {}
    for g, mat, m in PARTS:
        groupes.setdefault(g, []).append((mat, m))
    tris = 0
    nv = 0
    with open(os.path.join(DEST, NOM + ".obj"), "w") as f:
        f.write("# %s — Bajaj RE de MATULA DESIGN, d'après son script Blender, converti par scripts/generer_bajaj_re.py\n" % NOM)
        f.write("# (mètres, Y vers le haut, avant vers +Z dans Unity, sol à 0). Pièces : Caisse, Roue_AV/ARG/ARD, Phare_*,\n")
        f.write("# Cligno_*, Feu_AR*, Essuie_Glace, Plaque_AR. Normales fournies.\n")
        f.write("mtllib %s.mtl\n" % NOM)
        base = 1
        for g in sorted(groupes):
            f.write("o %s\ng %s\n" % (g, g))
            items = sorted(groupes[g], key=lambda t: t[0])
            for mat, m in items:
                for v in m.v:
                    f.write("v %.4f %.4f %.4f\n" % (v[0], v[2], -v[1] + DZ))
            for mat, m in items:
                for n in m.n:
                    f.write("vn %.4f %.4f %.4f\n" % (n[0], n[2], -n[1]))
            off = base
            cur = None
            for mat, m in items:
                if mat != cur:
                    f.write("usemtl %s\n" % mat)
                    cur = mat
                for face in m.f:
                    f.write("f " + " ".join("%d//%d" % (off + k, off + k) for k in face) + "\n")
                    tris += len(face) - 2
                off += len(m.v)
            base = off
            nv = base - 1
    allv = np.array([to_unity(v) for _, _, m in PARTS for v in m.v])
    mn, mx = allv.min(0), allv.max(0)
    print("%s : %d sommets, %d triangles, %d pièces" % (NOM, nv, tris, len(groupes)))
    print("   largeur %.2f m (x %.2f → %.2f), hauteur %.2f m, longueur %.2f m (z %.2f → %.2f)" % (mx[0] - mn[0], mn[0], mx[0], mx[1], mx[2] - mn[2], mn[2], mx[2]))
    for g in sorted(groupes):
        gv = np.array([to_unity(v) for _, m in groupes[g] for v in m.v])
        c = (gv.min(0) + gv.max(0)) / 2
        print("   %-13s centre (%.3f, %.3f, %.3f), taille (%.3f, %.3f, %.3f)" % (g, c[0], c[1], c[2], *(gv.max(0) - gv.min(0))))


# ----------------------------------------------------------------------------------------------------------------
# Aperçu (rendu logiciel, faces arrière masquées : une face retournée apparaîtrait comme un trou)
# ----------------------------------------------------------------------------------------------------------------
def apercu(dossier, personnes=True):
    from PIL import Image, ImageDraw
    tris, cols, nors = [], [], []
    for g, mat, m in PARTS:
        c = np.array(srgb(MATS[mat]))
        for face in m.f:
            for i in range(1, len(face) - 1):
                tris.append([to_unity(m.v[face[0]]), to_unity(m.v[face[i]]), to_unity(m.v[face[i + 1]])])
                cols.append(c)
    if personnes:
        # Gabarits de personnes assises (hanches sur l'assise, tête à 0,90 m au-dessus) pour vérifier la place sous le toit
        seats = [(0, 0.60, -0.075), (-0.39, 0.60, -0.075), (0.39, 0.60, -0.075), (-0.33, 0.61, -0.655), (0, 0.61, -0.655), (0.33, 0.61, -0.655)]
        for (sx, sy, sz_) in seats:
            box = rounded_box((0.40, 0.25, 0.90), 0.08, False)
            for face in box.f:
                pts = [np.array([v[0] + sx, v[2] + sy + 0.45, v[1] + sz_]) for v in (box.v[k] for k in face)]
                # repère de la boîte : x, y (profondeur) → z Unity, z (hauteur) → y Unity (symétrie, comme la caisse)
                for i in range(1, len(pts) - 1):
                    tris.append([pts[0], pts[i], pts[i + 1]])
                    cols.append(np.array((0.85, 0.35, 0.25)))
    T = np.array(tris)
    C = np.array(cols)
    W, H = 900, 640
    vues = {
        "cote_gauche": (np.array([-6.0, 1.1, 0.0]), np.array([0, 0.85, 0.0])),
        "trois_quarts_avant": (np.array([-3.6, 2.2, 4.4]), np.array([0, 0.8, 0.0])),
        "face": (np.array([0.0, 1.0, 6.0]), np.array([0, 0.85, 0.0])),
        "arriere_trois_quarts": (np.array([3.4, 2.0, -4.6]), np.array([0, 0.8, 0.0])),
        "dessus_coupe": (np.array([0.01, 7.0, 0.0]), np.array([0, 0.0, 0.0])),
    }
    lum = np.array([-0.4, 0.8, 0.45])
    lum /= np.linalg.norm(lum)
    os.makedirs(dossier, exist_ok=True)
    images = []
    for nom, (eye, tgt) in vues.items():
        fwd = tgt - eye
        fwd /= np.linalg.norm(fwd)
        upv = np.array([0, 1.0, 0]) if abs(fwd[1]) < 0.95 else np.array([0, 0, 1.0])
        right = np.cross(upv, fwd)
        right /= np.linalg.norm(right)
        up = np.cross(fwd, right)
        f = 1.0 / math.tan(math.radians(17))
        keep = np.ones(len(T), bool)
        if nom == "dessus_coupe":
            keep = T[:, :, 1].max(1) < 1.45   # coupe sous le toit : on voit les banquettes et les gabarits
        img = np.full((H, W, 3), 0.93)
        zb = np.full((H, W), np.inf)
        for t, c in zip(T[keep], C[keep]):
            n = -np.cross(t[1] - t[0], t[2] - t[0])  # repère Unity (main gauche) : la symétrie inverse le sens des faces
            nn = np.linalg.norm(n)
            if nn < 1e-12:
                continue
            n /= nn
            if np.dot(n, eye - t[0]) <= 0:      # face arrière : masquée
                continue
            rel = t - eye
            zc = rel @ fwd
            if (zc <= 0.05).any():
                continue
            xs = (rel @ right) / zc * f * H / 2 + W / 2
            ys = H / 2 - (rel @ up) / zc * f * H / 2
            shade = 0.35 + 0.65 * max(0.0, float(np.dot(n, lum)))
            col = np.clip(c * shade, 0, 1)
            x0, x1 = int(max(0, math.floor(xs.min()))), int(min(W - 1, math.ceil(xs.max())))
            y0, y1 = int(max(0, math.floor(ys.min()))), int(min(H - 1, math.ceil(ys.max())))
            if x0 > x1 or y0 > y1:
                continue
            gx, gy = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
            (ax, bx, cx), (ay, by, cy) = xs, ys
            den = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
            if abs(den) < 1e-9:
                continue
            l1 = ((by - cy) * (gx - cx) + (cx - bx) * (gy - cy)) / den
            l2 = ((cy - ay) * (gx - cx) + (ax - cx) * (gy - cy)) / den
            l3 = 1 - l1 - l2
            ins = (l1 >= -1e-6) & (l2 >= -1e-6) & (l3 >= -1e-6)
            if not ins.any():
                continue
            z = 1.0 / (l1 / zc[0] + l2 / zc[1] + l3 / zc[2])
            sub = zb[y0:y1 + 1, x0:x1 + 1]
            upd = ins & (z < sub)
            sub[upd] = z[upd]
            img[y0:y1 + 1, x0:x1 + 1][upd] = col
        im = Image.fromarray((img * 255).astype(np.uint8))
        ImageDraw.Draw(im).text((10, 10), nom, fill=(0, 0, 0))
        p = os.path.join(dossier, "bajaj_%s.png" % nom)
        im.save(p)
        images.append(p)
    print("Aperçus :", ", ".join(images))


if __name__ == "__main__":
    ecrire()
    if "--apercu" in sys.argv:
        apercu(sys.argv[sys.argv.index("--apercu") + 1])
