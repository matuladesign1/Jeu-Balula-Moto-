#!/usr/bin/env python3
"""BALULA MOTO — tricycle taxi (type Bajaj RE) modélisé d'après les photos de référence fournies par MATULA DESIGN :
version bleue à bâche blanche (vues avant, arrière, trois-quarts), versions blanche et rouge à bâche noire.

Le modèle respecte les proportions du vrai Bajaj RE (longueur 2,64 m, largeur 1,30 m, hauteur 1,71 m, empattement
2,00 m, roues de 8 pouces) et les emplacements utilisés par la physique du jeu (roue avant à +1,10 m, roues arrière à
-0,90 m, voie 1,15 m, rayon 0,24 m). Aucun logo ni marque n'est reproduit (design commercial respecté, sans prétendre
à un partenariat officiel).

Pièces séparées pour le jeu (noms lus par ModelVehicles.cs) :
  Caisse_<matériau>            carrosserie, une pièce par matériau (la circulation fusionne par matériau)
  Roue_AV|ARG|ARD_Pneu/_Jante  roues : pivot au moyeu, rotation autour de l'axe X
  Fourche_<matériau>           garde-boue et bras avant : tournent avec la roue avant
  Phare_G/D, Cligno_AVG/AVD/ARG/ARD, Feu_ARG/ARD   feux (allumés par le jeu)
  Essuie_Glace                 essuie-glace monté en haut du pare-brise (balaie sous la pluie)
  Plaque_AR                    plaque arrière (la plaque du joueur s'y pose)

Repère de construction : main droite, X vers la GAUCHE du véhicule, Y vers le haut, Z vers l'avant, sol à Y = 0.
Unity retourne l'axe X des OBJ : dans le jeu, x vers la droite, y vers le haut, z vers l'avant.

Usage : python3 scripts/generer_tricycle.py [--apercu dossier]
"""
import math
import os
import sys

import numpy as np

ICI = os.path.dirname(os.path.abspath(__file__))
DEST = os.path.join(ICI, "..", "Assets", "BalulaMoto", "Resources", "BalulaMoto", "Vehicules")
NOM = "BajajRE"

ROUE_R = 0.24
ROUE_AV = np.array([0.0, ROUE_R, 1.10])
ROUE_AR_X = 0.575
ROUE_AR_Z = -0.90

# Couleurs (sRGB) : version bleue des photos ; le jeu repeint carrosserie, bâche et sièges selon la version
MATS = {
    "Carrosserie": (0.11, 0.53, 0.86),
    "Bache": (0.89, 0.875, 0.85),
    "Siege": (0.90, 0.89, 0.87),
    "Plastique_Noir": (0.075, 0.08, 0.085),
    "Tapis": (0.05, 0.05, 0.055),
    "Vitre": (0.55, 0.62, 0.66),
    "Pneu": (0.06, 0.06, 0.065),
    "Chrome": (0.80, 0.82, 0.84),
    "Phare_Verre": (0.93, 0.94, 0.95),
    "Feu_Rouge": (0.72, 0.04, 0.05),
    "Orange": (1.0, 0.50, 0.04),
    "Plaque": (1.0, 0.80, 0.10),
    "Cadran": (0.93, 0.93, 0.90),
    "Grille": (0.10, 0.105, 0.11),
}

GEO = {}   # (groupe, matériau) -> [V, N, F]


def mesh(group, mat):
    k = (group, mat)
    if k not in GEO:
        GEO[k] = [[], [], []]
    return GEO[k]


def add(group, mat, V, N, F):
    g = mesh(group, mat)
    base = len(g[0])
    g[0].extend([np.asarray(v, float) for v in V])
    g[1].extend([np.asarray(n, float) / max(1e-12, np.linalg.norm(n)) for n in N])
    g[2].extend([[base + i for i in f] for f in F])


def fnormal(P):
    n = np.zeros(3)
    for i in range(len(P)):
        a, b = P[i], P[(i + 1) % len(P)]
        n += np.cross(a, b)
    return n / max(1e-12, np.linalg.norm(n))


# ------------------------------------------------------------------------------------------------------------------
# Primitives
# ------------------------------------------------------------------------------------------------------------------
def superellipse(cx, cy, a, b, n, M, z, flat_bottom=0.0):
    """Contour fermé (sens direct vu depuis +Z) d'une super-ellipse de centre (cx, cy), demi-axes a, b."""
    pts = []
    for i in range(M):
        t = 2 * math.pi * i / M
        c, s = math.cos(t), math.sin(t)
        x = cx + a * math.copysign(abs(c) ** (2.0 / n), c)
        y = cy + b * math.copysign(abs(s) ** (2.0 / n), s)
        if flat_bottom > 0 and y < cy - b * (1 - flat_bottom):
            y = cy - b * (1 - flat_bottom) - (cy - b * (1 - flat_bottom) - y) * 0.15
        pts.append((x, y, z))
    return np.array(pts)


def loft(group, mat, sections, cap0=True, cap1=True, closed=True, smooth=True):
    """Surface passant par des contours successifs de même nombre de points (contours fermés)."""
    S = [np.asarray(s, float) for s in sections]
    K, M = len(S), len(S[0])
    grid = np.array(S)                    # K × M × 3
    cent = grid.mean(1)                   # centre de chaque contour
    # orientation : normale sortante (loin du centre du contour)
    m = M if closed else M - 1
    quads = []
    for k in range(K - 1):
        for i in range(m):
            j = (i + 1) % M
            quads.append([(k, i), (k, j), (k + 1, j), (k + 1, i)])
    test = quads[len(quads) // 2]
    P = np.array([grid[a, b] for a, b in test])
    c = (cent[test[0][0]] + cent[test[2][0]]) / 2
    if np.dot(fnormal(P), P.mean(0) - c) < 0:
        quads = [q[::-1] for q in quads]
    if smooth:
        acc = np.zeros((K, M, 3))
        for q in quads:
            P = np.array([grid[a, b] for a, b in q])
            n = np.cross(P[2] - P[0], P[3] - P[1])
            for a, b in q:
                acc[a, b] += n
        idx = {}
        V, N, F = [], [], []
        for k in range(K):
            for i in range(M):
                idx[k, i] = len(V)
                V.append(grid[k, i])
                N.append(acc[k, i])
        F = [[idx[a, b] for a, b in q] for q in quads]
        add(group, mat, V, N, F)
    else:
        V, N, F = [], [], []
        for q in quads:
            P = np.array([grid[a, b] for a, b in q])
            n = fnormal(P)
            F.append(list(range(len(V), len(V) + 4)))
            V.extend(P)
            N.extend([n] * 4)
        add(group, mat, V, N, F)
    for k, cap in ((0, cap0), (K - 1, cap1)):
        if not cap:
            continue
        P = grid[k]
        c = P.mean(0)
        other = grid[1] if k == 0 else grid[K - 2]
        out = c - other.mean(0)
        n = fnormal(P)
        ring = list(range(M))
        if np.dot(n, out) < 0:
            ring = ring[::-1]
            n = -n
        V = [c] + [P[i] for i in ring]
        F = [[0, 1 + i, 1 + (i + 1) % M] for i in range(M)]
        add(group, mat, V, [n] * len(V), F)


def lathe(group, mat, profile, center, axis, sides=24, smooth=True):
    """Révolution d'un profil fermé (u = position sur l'axe, v = rayon), sens direct dans le plan (u, v)."""
    axis = np.asarray(axis, float) / np.linalg.norm(axis)
    ref = np.array([0.0, 1.0, 0.0]) if abs(axis[1]) < 0.9 else np.array([1.0, 0.0, 0.0])
    e1 = np.cross(axis, ref)
    e1 /= np.linalg.norm(e1)
    e2 = np.cross(axis, e1)
    prof = np.asarray(profile, float)
    area = sum(prof[i][0] * prof[(i + 1) % len(prof)][1] - prof[(i + 1) % len(prof)][0] * prof[i][1] for i in range(len(prof)))
    if area < 0:
        prof = prof[::-1]
    L = len(prof)
    # normales 2D sortantes (polygone direct dans (u, v)) lissées aux sommets
    en = []
    for i in range(L):
        d = prof[(i + 1) % L] - prof[i]
        n = np.array([d[1], -d[0]])
        en.append(n / max(1e-12, np.linalg.norm(n)))
    # anneaux : un par sommet du profil, deux aux arêtes vives (> 50°)
    ring_of = {}
    V, N, F = [], [], []

    def ring(pt, nn):
        base = len(V)
        for s_ in range(sides + 1):
            a = 2 * math.pi * s_ / sides
            rad = math.cos(a) * e1 + math.sin(a) * e2
            V.append(np.asarray(center) + axis * pt[0] + rad * pt[1])
            N.append(axis * nn[0] + rad * nn[1])
        return base

    for i in range(L):
        prev = en[i - 1]
        cur = en[i]
        if smooth and np.dot(prev, cur) >= 0.64:
            b = ring(prof[i], prev + cur)
            ring_of[i, "in"] = ring_of[i, "out"] = b
        else:
            ring_of[i, "in"] = ring(prof[i], prev)
            ring_of[i, "out"] = ring(prof[i], cur)
    for i in range(L):
        j = (i + 1) % L
        r0, r1 = ring_of[i, "out"], ring_of[j, "in"]
        for s_ in range(sides):
            quad = [r0 + s_, r1 + s_, r1 + s_ + 1, r0 + s_ + 1]
            P = np.array([V[k] for k in quad])
            if np.linalg.norm(np.cross(P[2] - P[0], P[3] - P[1])) < 1e-14:
                continue
            nref = np.asarray(N[quad[0]]) + N[quad[2]]
            if np.dot(fnormal(P), nref) < 0:
                quad = quad[::-1]
            F.append(quad)
    add(group, mat, V, N, F)


def ear_clip(poly):
    """Triangulation d'un polygone simple (liste de points 2D, sens direct)."""
    pts = [np.asarray(p, float) for p in poly]
    idx = list(range(len(pts)))
    area = sum(pts[i][0] * pts[(i + 1) % len(pts)][1] - pts[(i + 1) % len(pts)][0] * pts[i][1] for i in range(len(pts)))
    if area < 0:
        idx.reverse()
    tris = []
    guard = 0
    while len(idx) > 3 and guard < 10000:
        guard += 1
        n = len(idx)
        for k in range(n):
            a, b, c = idx[k - 1], idx[k], idx[(k + 1) % n]
            pa, pb, pc = pts[a], pts[b], pts[c]
            cr = (pb[0] - pa[0]) * (pc[1] - pa[1]) - (pb[1] - pa[1]) * (pc[0] - pa[0])
            if cr <= 1e-12:
                continue
            inside = False
            for o in idx:
                if o in (a, b, c):
                    continue
                p = pts[o]
                d1 = (pb[0] - pa[0]) * (p[1] - pa[1]) - (pb[1] - pa[1]) * (p[0] - pa[0])
                d2 = (pc[0] - pb[0]) * (p[1] - pb[1]) - (pc[1] - pb[1]) * (p[0] - pb[0])
                d3 = (pa[0] - pc[0]) * (p[1] - pc[1]) - (pa[1] - pc[1]) * (p[0] - pc[0])
                if d1 >= 0 and d2 >= 0 and d3 >= 0:
                    inside = True
                    break
            if inside:
                continue
            tris.append((a, b, c))
            idx.pop(k)
            break
    if len(idx) == 3:
        tris.append(tuple(idx))
    return tris


def extrude_zy(group, mat, poly, x0, x1, smooth_sides=False):
    """Polygone du plan (z, y) extrudé selon X de x0 à x1 (x0 < x1)."""
    poly = [tuple(p) for p in poly]
    n = len(poly)
    tris = ear_clip([(p[0], p[1]) for p in poly])   # (z, y) direct
    # face x1 (normale +X) : dans le repère direct (Y, Z) vu depuis +X... on vérifie par le calcul
    for x, sgn in ((x1, 1.0), (x0, -1.0)):
        V = [np.array([x, p[1], p[0]]) for p in poly]
        F = []
        for (a, b, c) in tris:
            f = [a, b, c]
            if np.dot(fnormal(np.array([V[a], V[b], V[c]])), [sgn, 0, 0]) < 0:
                f = f[::-1]
            F.append(f)
        add(group, mat, V, [np.array([sgn, 0.0, 0.0])] * n, F)
    # côtés
    cz = np.mean([p[0] for p in poly])
    cy = np.mean([p[1] for p in poly])
    area = sum(poly[i][0] * poly[(i + 1) % n][1] - poly[(i + 1) % n][0] * poly[i][1] for i in range(n))
    V, N, F = [], [], []
    for i in range(n):
        p, q = poly[i], poly[(i + 1) % n]
        dz, dy = q[0] - p[0], q[1] - p[1]
        nrm = np.array([0.0, -dz, dy]) if area > 0 else np.array([0.0, dz, -dy])
        nrm /= max(1e-12, np.linalg.norm(nrm))
        quad = [np.array([x0, p[1], p[0]]), np.array([x1, p[1], p[0]]), np.array([x1, q[1], q[0]]), np.array([x0, q[1], q[0]])]
        f = [0, 1, 2, 3]
        if np.dot(fnormal(np.array(quad)), nrm) < 0:
            f = f[::-1]
        b = len(V)
        V.extend(quad)
        N.extend([nrm] * 4)
        F.append([b + k for k in f])
    add(group, mat, V, N, F)


def rounded_box(group, mat, center, dims, r, R=None, fine=None):
    """Pavé aux arêtes arrondies (rayon r), éventuellement tourné (matrice R). Biseau à 1, 2 ou 3 segments selon la
    taille (petites pièces : simple chanfrein lissé)."""
    h = np.asarray(dims, float) / 2.0
    R = np.eye(3) if R is None else R
    if r < 0.005 or min(dims) < 0.016:
        # petite pièce : pavé simple
        V, N, F = [], [], []
        for axis in range(3):
            for sign in (-1.0, 1.0):
                a1, a2 = (axis + 1) % 3, (axis + 2) % 3
                nrm = np.eye(3)[axis] * sign
                q = []
                for (u, w) in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
                    p = np.zeros(3)
                    p[axis] = sign * h[axis]
                    p[a1] = u * h[a1]
                    p[a2] = w * h[a2]
                    q.append(len(V))
                    V.append(R @ p + np.asarray(center))
                    N.append(R @ nrm)
                P = np.array([V[k] for k in q])
                if np.dot(fnormal(P), R @ nrm) < 0:
                    q.reverse()
                F.append(q)
        add(group, mat, V, N, F)
        return
    r = min(r, float(h.min()) * 0.999)
    seg = 2 if fine else 1
    angles = [90.0 * k / seg for k in range(seg + 1)]

    def coords(hh):
        c0 = hh - r
        prof = [c0 + r * math.sin(math.radians(a)) for a in angles]
        neg = [-p for p in reversed(prof)]
        return neg + prof if c0 > 1e-6 else neg[:-1] + prof

    C_ = [coords(h[i]) for i in range(3)]
    c0 = h - r
    V, N, F = [], [], []
    for axis in range(3):
        for sign in (-1.0, 1.0):
            a1, a2 = (axis + 1) % 3, (axis + 2) % 3
            grid = {}
            for i, u in enumerate(C_[a1]):
                for j, w in enumerate(C_[a2]):
                    p = np.zeros(3)
                    p[axis] = sign * h[axis]
                    p[a1] = u
                    p[a2] = w
                    inner = np.clip(p, -c0, c0)
                    d = p - inner
                    nd = np.linalg.norm(d)
                    nrm = d / nd if nd > 1e-9 else np.eye(3)[axis] * sign
                    grid[i, j] = len(V)
                    V.append(R @ (inner + nrm * r) + np.asarray(center))
                    N.append(R @ nrm)
            for i in range(len(C_[a1]) - 1):
                for j in range(len(C_[a2]) - 1):
                    q = [grid[i, j], grid[i + 1, j], grid[i + 1, j + 1], grid[i, j + 1]]
                    P = np.array([V[k] for k in q])
                    fn = fnormal(P)
                    if np.linalg.norm(np.cross(P[2] - P[0], P[3] - P[1])) < 1e-12:
                        continue
                    if np.dot(fn, np.array(N[q[0]]) + N[q[2]]) < 0:
                        q.reverse()
                    F.append(q)
    add(group, mat, V, N, F)


def tube(group, mat, a, b, r, sides=12, caps=True):
    a, b = np.asarray(a, float), np.asarray(b, float)
    d = b - a
    L = np.linalg.norm(d)
    ax = d / L
    lathe(group, mat, [(0.0, 0.0), (0.0, r), (L, r), (L, 0.0)] if caps else [(0.0, r), (L, r), (L, r * 0.999), (0.0, r * 0.999)],
          a, ax, sides, smooth=True)


def ring_panel(group, mat, outer, inner, axis, pos, thick):
    """Panneau percé : contours extérieur et intérieur 2D (même nombre de points, même sens), épaisseur selon l'axe
    'x' (plan z, y) ou 'z' (plan x, y), face avant à pos + thick/2."""
    outer, inner = np.asarray(outer, float), np.asarray(inner, float)
    M = len(outer)

    def P3(p, w):
        if axis == "x":
            return np.array([w, p[1], p[0]])
        return np.array([p[0], p[1], w])

    nx = np.array([1.0, 0, 0]) if axis == "x" else np.array([0, 0, 1.0])
    for w, sgn in ((pos + thick / 2, 1.0), (pos - thick / 2, -1.0)):
        V, F = [], []
        for i in range(M):
            V.extend([P3(outer[i], w), P3(inner[i], w)])
        for i in range(M):
            j = (i + 1) % M
            q = [2 * i, 2 * j, 2 * j + 1, 2 * i + 1]
            Pq = np.array([V[k] for k in q])
            if np.dot(fnormal(Pq), nx * sgn) < 0:
                q.reverse()
            F.append(q)
        add(group, mat, V, [nx * sgn] * len(V), F)
    for loop, out_sign in ((outer, 1.0), (inner, -1.0)):
        c = loop.mean(0)
        V, N, F = [], [], []
        for i in range(M):
            j = (i + 1) % M
            p, q = loop[i], loop[j]
            mid = (p + q) / 2
            d = q - p
            n2 = np.array([d[1], -d[0]])
            n2 /= max(1e-12, np.linalg.norm(n2))
            if np.dot(n2, mid - c) * out_sign < 0:
                n2 = -n2
            n3 = P3(n2, 0.0) - P3((0, 0), 0.0)
            quad = [P3(p, pos - thick / 2), P3(q, pos - thick / 2), P3(q, pos + thick / 2), P3(p, pos + thick / 2)]
            f = [0, 1, 2, 3]
            if np.dot(fnormal(np.array(quad)), n3) < 0:
                f = f[::-1]
            b = len(V)
            V.extend(quad)
            N.extend([n3] * 4)
            F.append([b + k for k in f])
        add(group, mat, V, N, F)


def rrect(c0, c1, w, h, r, M=28):
    """Rectangle arrondi (centre c0, c1 ; largeur w selon la 1re coordonnée, hauteur h), M points, sens direct."""
    pts = []
    corners = [(w / 2 - r, h / 2 - r, 0), (-w / 2 + r, h / 2 - r, 90), (-w / 2 + r, -h / 2 + r, 180), (w / 2 - r, -h / 2 + r, 270)]
    per = M // 4
    for (cx, cy, a0) in corners:
        for k in range(per):
            a = math.radians(a0 + 90.0 * k / (per - 1))
            pts.append((c0 + cx + r * math.cos(a), c1 + cy + r * math.sin(a)))
    return pts


def flat_quad(group, mat, P, double=False):
    P = [np.asarray(p, float) for p in P]
    n = fnormal(np.array(P))
    add(group, mat, P, [n] * 4, [[0, 1, 2, 3]])
    if double:
        add(group, mat, P[::-1], [-n] * 4, [[0, 1, 2, 3]])


def grid_patch(group, mat, f, nu, nv, double=False):
    """Surface paramétrée f(u, v) -> point 3D, u, v dans [0, 1] ; normales lissées."""
    G = np.array([[f(i / nu, j / nv) for j in range(nv + 1)] for i in range(nu + 1)])
    Nn = np.zeros_like(G)
    for i in range(nu + 1):
        for j in range(nv + 1):
            du = G[min(i + 1, nu), j] - G[max(i - 1, 0), j]
            dv = G[i, min(j + 1, nv)] - G[i, max(j - 1, 0)]
            Nn[i, j] = np.cross(du, dv)
    V, N, F = [], [], []
    for i in range(nu + 1):
        for j in range(nv + 1):
            V.append(G[i, j])
            N.append(Nn[i, j])
    for i in range(nu):
        for j in range(nv):
            a = i * (nv + 1) + j
            F.append([a, a + nv + 1, a + nv + 2, a + 1])
    add(group, mat, V, N, F)
    if double:
        add(group, mat, V, [-n for n in N], [f_[::-1] for f_ in F])


def flat_poly(group, mat, pts, double=False):
    P = np.array(pts, float)
    c = P.mean(0)
    n = fnormal(P)
    M = len(P)
    V = [c] + list(P)
    F = [[0, 1 + i, 1 + (i + 1) % M] for i in range(M)]
    add(group, mat, V, [n] * len(V), F)
    if double:
        add(group, mat, V, [-n] * len(V), [f[::-1] for f in F])


def sweep(group, mat, path, r, sides=10, caps=True):
    """Tube de rayon r le long d'une polyligne (repères parallèles)."""
    path = [np.asarray(p, float) for p in path]
    T = []
    for i in range(len(path)):
        a = path[max(0, i - 1)]
        b = path[min(len(path) - 1, i + 1)]
        t = b - a
        T.append(t / np.linalg.norm(t))
    ref = np.array([0.0, 1.0, 0.0]) if abs(T[0][1]) < 0.9 else np.array([1.0, 0.0, 0.0])
    u = np.cross(T[0], ref)
    u /= np.linalg.norm(u)
    secs = []
    for i, p in enumerate(path):
        u = u - T[i] * np.dot(u, T[i])
        u /= np.linalg.norm(u)
        w = np.cross(T[i], u)
        secs.append([p + r * (math.cos(2 * math.pi * k / sides) * u + math.sin(2 * math.pi * k / sides) * w) for k in range(sides)])
    loft(group, mat, secs, cap0=caps, cap1=caps)


def sec(z, a, yb, yt, n, M=32, taper=None):
    P = superellipse(0.0, (yb + yt) / 2, a, (yt - yb) / 2, n, M, z)
    if taper is not None:
        P[:, 0] *= np.array([taper(y) for y in P[:, 1]])
    return P


def interp_secs(keys, zs, M=32, taper=None):
    """Contours interpolés (z, a, yb, yt, n) aux positions zs."""
    K = np.array(keys, float)
    out = []
    for z in zs:
        vals = [np.interp(z, K[:, 0], K[:, j]) for j in range(1, 5)]
        out.append(sec(z, *vals, M=M, taper=taper))
    return out


def taper_capot(y):
    """Le capot avant est plus large en haut (pare-brise) qu'en bas (calandre), comme sur les photos."""
    return 1.0 - 0.10 * min(1.0, max(0.0, (1.0 - y) / 0.67))


def rot_x(a):
    c, s_ = math.cos(a), math.sin(a)
    return np.array([[1, 0, 0], [0, c, -s_], [0, s_, c]])


def rot_y(a):
    c, s_ = math.cos(a), math.sin(a)
    return np.array([[c, 0, s_], [0, 1, 0], [-s_, 0, c]])


def rot_z(a):
    c, s_ = math.cos(a), math.sin(a)
    return np.array([[c, -s_, 0], [s_, c, 0], [0, 0, 1]])


def mirror(fn):
    """Construit une pièce des deux côtés : fn(signe, côté) avec signe +1 à gauche (X+), -1 à droite."""
    fn(1.0, "G")
    fn(-1.0, "D")


# ==================================================================================================================
# Modèle
# ==================================================================================================================
B, CANVAS, SEAT, BLACK, MAT_, GLASS, TYRE, CHROME = "Carrosserie", "Bache", "Siege", "Plastique_Noir", "Tapis", "Vitre", "Pneu", "Chrome"
C = "Caisse"


def caisse_avant():
    # Capot avant (coque bleue, du tablier intérieur au nez)
    keys = [(0.64, 0.612, 0.30, 1.000, 6.0), (0.78, 0.612, 0.30, 1.010, 5.5), (0.92, 0.604, 0.35, 1.006, 4.6),
            (1.02, 0.588, 0.44, 1.000, 3.9), (1.12, 0.562, 0.515, 0.985, 3.5), (1.20, 0.527, 0.54, 0.958, 3.2),
            (1.26, 0.485, 0.553, 0.922, 3.0), (1.30, 0.434, 0.572, 0.868, 2.8), (1.325, 0.345, 0.60, 0.800, 2.6)]
    zs = [0.64, 0.70, 0.78, 0.85, 0.92, 0.97, 1.02, 1.07, 1.12, 1.16, 1.20, 1.23, 1.26, 1.28, 1.30, 1.315, 1.325]
    loft(C, B, interp_secs(keys, zs, M=36, taper=taper_capot))
    # Bavette noire (calandre) qui enveloppe le bas du nez, phares ronds à ses extrémités
    fk = [(1.21, 0.548, 0.532, 0.802, 6.0), (1.25, 0.522, 0.532, 0.802, 6.0), (1.28, 0.494, 0.537, 0.802, 5.5),
          (1.315, 0.452, 0.547, 0.797, 5.0), (1.335, 0.412, 0.562, 0.790, 4.5), (1.346, 0.345, 0.580, 0.772, 4.0)]
    loft(C, BLACK, interp_secs(fk, [1.21, 1.235, 1.25, 1.28, 1.30, 1.315, 1.327, 1.335, 1.341, 1.346], M=36, taper=taper_capot))
    # Panneau central en relief (prise d'air à trois fentes, sans logo)
    rounded_box(C, BLACK, (0.0, 0.69, 1.348), (0.34, 0.15, 0.02), 0.012)
    for y in (0.655, 0.69, 0.725):
        rounded_box(C, "Grille", (0.0, y, 1.357), (0.25, 0.012, 0.006), 0.003, fine=False)

    def phare(sg, cote):
        c = np.array([sg * 0.338, 0.688, 1.334])
        # cuvelage noir saillant, enjoliveur chromé, verre
        lathe(C, BLACK, [(-0.03, 0.0001), (-0.03, 0.093), (0.012, 0.093), (0.016, 0.086), (0.016, 0.0001)], c, (0, 0, 1), 14)
        lathe(C, CHROME, [(0.010, 0.064), (0.010, 0.086), (0.022, 0.084), (0.026, 0.074), (0.024, 0.064)], c, (0, 0, 1), 14)
        lathe("Phare_" + cote, "Phare_Verre", [(0.012, 0.0001), (0.012, 0.066), (0.020, 0.064), (0.026, 0.045), (0.029, 0.0001)], c, (0, 0, 1), 14)
    mirror(phare)
    # Plaque avant jaune (inclinée comme le capot)
    rounded_box(C, "Plaque", (0.0, 0.868, 1.303), (0.21, 0.065, 0.012), 0.006, R=rot_x(math.radians(-29)))
    # Clignotants avant orange aux coins hauts du capot

    def cligno_av(sg, cote):
        rounded_box("Cligno_AV" + cote, "Orange", (sg * 0.405, 0.905, 1.215), (0.12, 0.040, 0.026), 0.011,
                    R=rot_y(sg * math.radians(28)) @ rot_x(math.radians(-25)))
    mirror(cligno_av)
    # Tablier intérieur noir (côté chauffeur), planche de bord et compteur
    rounded_box(C, BLACK, (0.0, 0.66, 0.632), (1.16, 0.64, 0.03), 0.012)
    rounded_box(C, BLACK, (0.0, 1.018, 0.735), (1.10, 0.05, 0.25), 0.02)
    cz = np.array([0.0, 1.072, 0.655])
    ax = np.array([0.0, math.sin(math.radians(38)), -math.cos(math.radians(38))])   # vers le chauffeur, relevé
    lathe(C, BLACK, [(-0.05, 0.0001), (-0.05, 0.072), (0.02, 0.080), (0.03, 0.074), (0.03, 0.0001)], cz, ax, 22)
    lathe(C, "Cadran", [(0.031, 0.0001), (0.031, 0.062), (0.034, 0.060), (0.034, 0.0001)], cz, ax, 22)
    up = np.array([0.0, 1.0, 0.0])
    e1 = np.cross(up, ax)
    e1 /= np.linalg.norm(e1)
    e2 = np.cross(ax, e1)
    a = math.radians(125)
    tube(C, "Orange", cz + ax * 0.036, cz + ax * 0.036 + 0.05 * (math.cos(a) * e1 + math.sin(a) * e2), 0.0025, 5)


def glass_z(x, y):
    """Position du pare-brise (z) en un point : bas à (y 1,0 ; z 0,865), haut à (y 1,505 ; z 0,75), bombé au centre."""
    v = (y - 1.0) / 0.505
    hw = 0.55 * (1 - 0.027 * v)
    u = x / hw
    return 0.865 + (0.75 - 0.865) * v + 0.025 * (1 - u * u)


def pare_brise():
    def f(u, v):
        hw = 0.55 * (1 - 0.027 * v)
        x = -hw + 2 * hw * u
        y = 1.0 + 0.505 * v
        return np.array([x, y, glass_z(x, y)])
    grid_patch(C, GLASS, f, 10, 3, double=True)

    def montant(sg, cote):
        tube(C, B, (sg * 0.575, 0.985, 0.876), (sg * 0.556, 1.522, 0.744), 0.028, 12)
    mirror(montant)
    tube(C, BLACK, (-0.555, 1.0, 0.868), (0.555, 1.0, 0.868), 0.012, 8)

    # Rétroviseurs : tige noire et miroir rectangulaire (glace tournée vers l'arrière)
    def retro(sg, cote):
        tube(C, BLACK, (sg * 0.566, 1.235, 0.812), (sg * 0.715, 1.33, 0.86), 0.011, 8)
        R = rot_y(sg * math.radians(-12))
        c = np.array([sg * 0.752, 1.36, 0.862])
        rounded_box(C, BLACK, c, (0.13, 0.088, 0.032), 0.014, R=R)
        q = [c + R @ np.array(p) for p in ((-0.056, -0.036, -0.0172), (0.056, -0.036, -0.0172), (0.056, 0.036, -0.0172), (-0.056, 0.036, -0.0172))]
        if np.dot(fnormal(np.array(q)), R @ np.array([0.0, 0.0, -1.0])) < 0:
            q = q[::-1]
        flat_quad(C, CHROME, q)
    mirror(retro)

    # Essuie-glace monté en haut du pare-brise, au repos penché vers la droite (vu du chauffeur)
    piv = np.array([0.18, 1.474, glass_z(0.18, 1.474) + 0.016])
    t_down = np.array([0.0, -0.505, 0.115])
    t_down /= np.linalg.norm(t_down)
    a = math.radians(33)
    d = math.cos(a) * t_down + math.sin(a) * np.array([-1.0, 0.0, 0.0])
    d /= np.linalg.norm(d)
    tube("Essuie_Glace", BLACK, piv + np.array([0, 0, -0.012]), piv + np.array([0, 0, 0.018]), 0.016, 10)
    tube("Essuie_Glace", BLACK, piv, piv + d * 0.34, 0.0075, 6)
    nrm = np.cross(np.array([1.0, 0, 0]), d)
    nrm = nrm if nrm[2] > 0 else -nrm
    side = np.cross(d, nrm)
    R = np.stack([side, d, nrm], 1)
    rounded_box("Essuie_Glace", BLACK, piv + d * 0.37 - nrm * 0.004, (0.014, 0.40, 0.012), 0.004, R=R, fine=False)
    return piv


def plancher_et_sieges():
    rounded_box(C, MAT_, (0.0, 0.333, 0.05), (1.12, 0.05, 1.20), 0.012)       # plancher (tapis noir)
    rounded_box(C, BLACK, (0.0, 0.262, 0.05), (0.92, 0.09, 1.24), 0.02)       # châssis

    def bas_de_caisse(sg, cote):
        rounded_box(C, B, (sg * 0.565, 0.362, 0.035), (0.09, 0.18, 1.17), 0.035)
    mirror(bas_de_caisse)
    # Banquette avant (chauffeur + 2 places) sur socle noir
    rounded_box(C, BLACK, (0.0, 0.445, 0.02), (0.80, 0.17, 0.32), 0.02)
    rounded_box(C, SEAT, (0.0, 0.555, 0.02), (0.98, 0.09, 0.42), 0.035, fine=True)
    # Dossier du chauffeur (cloison capitonnée) et barre de maintien
    rounded_box(C, SEAT, (0.0, 0.865, -0.232), (0.92, 0.40, 0.07), 0.03, R=rot_x(math.radians(-8)), fine=True)
    tube(C, B, (-0.605, 1.085, -0.262), (0.605, 1.085, -0.262), 0.02, 10)

    def montant_cloison(sg, cote):
        tube(C, B, (sg * 0.47, 1.085, -0.262), (sg * 0.50, 0.45, -0.25), 0.018, 10)
        tube(C, B, (sg * 0.600, 0.44, -0.255), (sg * 0.628, 1.53, -0.275), 0.024, 12)    # montant central
    mirror(montant_cloison)
    # Banquette arrière (3 places) sur le coffre moteur, dossier contre le bloc arrière
    rounded_box(C, B, (0.0, 0.44, -0.80), (1.02, 0.20, 0.42), 0.03)
    rounded_box(C, SEAT, (0.0, 0.555, -0.80), (1.14, 0.09, 0.44), 0.035, fine=True)
    rounded_box(C, SEAT, (0.0, 0.84, -1.03), (1.14, 0.40, 0.08), 0.035, R=rot_x(math.radians(-10)), fine=True)


def caisse_arriere():
    # Bloc arrière (moteur) au-dessus des roues, puis partie basse plus étroite entre les roues
    keys = [(-1.03, 0.640, 0.56, 0.98, 6.0), (-1.10, 0.640, 0.56, 0.98, 6.0), (-1.20, 0.636, 0.56, 0.975, 5.5),
            (-1.27, 0.626, 0.57, 0.966, 5.0), (-1.305, 0.600, 0.58, 0.950, 4.5), (-1.322, 0.550, 0.60, 0.920, 4.0)]
    loft(C, B, interp_secs(keys, [-1.03, -1.12, -1.20, -1.24, -1.27, -1.29, -1.305, -1.315, -1.322], M=32)[::-1])
    keys2 = [(-0.99, 0.43, 0.29, 0.585, 5.0), (-1.27, 0.43, 0.29, 0.585, 5.0), (-1.30, 0.41, 0.30, 0.58, 4.5), (-1.312, 0.37, 0.31, 0.57, 4.0)]
    loft(C, B, interp_secs(keys2, [-0.99, -1.27, -1.29, -1.30, -1.312], M=24)[::-1])

    # Flancs arrière avec passage de roue (bord roulé)
    zc, yc, Ra = ROUE_AR_Z, ROUE_R, 0.31
    arch = []
    a0 = math.asin((0.30 - yc) / Ra)
    for k in range(17):
        a = (math.pi - a0) - (math.pi - 2 * a0) * k / 16      # de l'arrière vers l'avant, par le haut
        arch.append((zc + Ra * math.cos(a), yc + Ra * math.sin(a)))
    top = []
    for k in range(9):
        t = k / 8
        z = -1.27 + (0.75 * t)
        y = 0.95 - 0.09 * min(1.0, t / 0.62) - 0.06 * max(0.0, (t - 0.62) / 0.38)
        top.append((z, y))
    poly = [(-1.27, 0.30)]
    poly += list(arch)                                   # arche (de l'arrière vers l'avant par le haut)
    poly += [(-0.52, 0.30), (-0.52, 0.74), (-0.535, 0.785), (-0.565, 0.80)]
    poly += [p for p in reversed(top)][1:]                # dessus, de l'avant vers l'arrière

    def flanc(sg, cote):
        x0, x1 = (0.615, 0.662) if sg > 0 else (-0.662, -0.615)
        extrude_zy(C, B, poly, x0, x1)
        # bord roulé du passage de roue
        path = [np.array([sg * 0.662, y, z]) for (z, y) in arch]
        sweep(C, B, path, 0.017, 8)
        # coquille noire du passage de roue
        th0, th1 = a0, math.pi - a0

        def f(u, v):
            th = th0 + (th1 - th0) * u
            return np.array([sg * (0.47 + 0.15 * v), yc + 0.30 * math.sin(th), zc + 0.30 * math.cos(th)])
        grid_patch(C, BLACK, f, 12, 1, double=True)
    mirror(flanc)

    # Face arrière : grille, feux verticaux, clignotants, plaque, pare-chocs
    rounded_box(C, "Grille", (0.0, 0.752, -1.317), (0.62, 0.27, 0.02), 0.02)
    for k in range(6):
        rounded_box(C, BLACK, (0.0, 0.645 + k * 0.043, -1.329), (0.58, 0.010, 0.008), 0.003, fine=False)
    rounded_box(C, "Grille", (0.0, 0.535, -1.309), (0.42, 0.06, 0.02), 0.015)
    for k in range(3):
        rounded_box(C, BLACK, (0.0, 0.515 + k * 0.02, -1.321), (0.38, 0.007, 0.006), 0.002, fine=False)

    def feux_ar(sg, cote):
        rounded_box("Feu_AR" + cote, "Feu_Rouge", (sg * 0.478, 0.738, -1.326), (0.088, 0.24, 0.034), 0.02)
        rounded_box("Cligno_AR" + cote, "Orange", (sg * 0.478, 0.585, -1.322), (0.078, 0.05, 0.03), 0.012)
    mirror(feux_ar)
    rounded_box("Plaque_AR", "Plaque", (0.0, 0.418, -1.318), (0.26, 0.105, 0.012), 0.006)
    rounded_box(C, BLACK, (0.0, 0.325, -1.30), (1.12, 0.06, 0.11), 0.025)


def bache():
    keys = [(-1.285, 0.600, 1.530, 1.670, 5.0), (-1.27, 0.655, 1.520, 1.695, 6.0), (-1.10, 0.665, 1.520, 1.705, 6.0),
            (-0.50, 0.665, 1.525, 1.710, 6.0), (0.20, 0.665, 1.525, 1.710, 6.0), (0.62, 0.655, 1.520, 1.700, 6.0),
            (0.74, 0.620, 1.505, 1.685, 5.0), (0.79, 0.570, 1.500, 1.660, 4.5), (0.815, 0.50, 1.51, 1.62, 4.0)]
    zs = [-1.285, -1.27, -1.20, -1.10, -0.50, 0.20, 0.62, 0.69, 0.74, 0.77, 0.79, 0.805, 0.815]
    loft(C, CANVAS, interp_secs(keys, zs, M=32))
    # Paroi arrière en toile avec grande lunette
    ring_panel(C, CANVAS, rrect(0.0, 1.245, 1.29, 0.59, 0.08), rrect(0.0, 1.335, 0.70, 0.26, 0.06), "z", -1.258, 0.02)
    flat_poly(C, GLASS, [np.array([x, y, -1.258]) for (x, y) in rrect(0.0, 1.335, 0.70, 0.26, 0.06)], double=True)

    # Custodes (panneaux arrière latéraux) avec petite vitre, enroulements de toile au-dessus des ouvertures
    def custode(sg, cote):
        ring_panel(C, CANVAS, rrect(-1.035, 1.185, 0.47, 0.69, 0.04), rrect(-1.05, 1.33, 0.28, 0.21, 0.05), "x", sg * 0.648, 0.02)
        flat_poly(C, GLASS, [np.array([sg * 0.648, y, z]) for (z, y) in rrect(-1.05, 1.33, 0.28, 0.21, 0.05)], double=True)
        tube(C, B, (sg * 0.648, 0.86, -0.80), (sg * 0.652, 1.528, -0.80), 0.02, 8)
        tube(C, CANVAS, (sg * 0.657, 1.497, -0.78), (sg * 0.657, 1.497, -0.30), 0.03, 8)
        tube(C, CANVAS, (sg * 0.657, 1.497, -0.21), (sg * 0.657, 1.497, 0.56), 0.026, 8)
    mirror(custode)


def fourche():
    c = ROUE_AV
    R = 0.272
    secs = []
    for k in range(13):
        th = math.radians(16 + (150 - 16) * k / 12)
        rad = np.array([0.0, math.sin(th), math.cos(th)])
        loop = []
        for j in range(16):
            a = 2 * math.pi * j / 16
            u = 0.088 * math.copysign(abs(math.cos(a)) ** 0.4, math.cos(a))
            v = 0.011 * math.copysign(abs(math.sin(a)) ** 0.6, math.sin(a)) - 0.035 * (u / 0.088) ** 2
            loop.append(c + rad * (R + v) + np.array([u, 0.0, 0.0]))
        secs.append(loop)
    loft("Fourche", B, secs)
    # bras de suspension à droite (X-), amortisseur, moyeu
    tube("Fourche", BLACK, (-0.088, 0.575, 1.035), (-0.074, 0.24, 1.10), 0.024, 10)
    tube("Fourche", BLACK, (-0.088, 0.575, 1.035), (0.0, 0.60, 1.03), 0.024, 10)
    tube("Fourche", CHROME, (-0.098, 0.53, 0.985), (-0.090, 0.31, 1.15), 0.014, 8)
    lathe("Fourche", CHROME, [(-0.075, 0.0001), (-0.075, 0.03), (-0.06, 0.03), (-0.055, 0.0001)], c, (1, 0, 0), 14)


def roue(nom, centre, dehors):
    tread = [(0.042, 0.105), (0.050, 0.150), (0.050, 0.200), (0.045, 0.226), (0.030, 0.238), (0.010, 0.2405),
             (-0.010, 0.2405), (-0.030, 0.238), (-0.045, 0.226), (-0.050, 0.200), (-0.050, 0.150), (-0.042, 0.105)]
    lathe(nom + "_Pneu", TYRE, tread, centre, dehors, 18)
    rim = [(0.036, 0.0001), (0.036, 0.034), (0.022, 0.060), (0.034, 0.092), (0.040, 0.112), (0.030, 0.104),
           (-0.030, 0.104), (-0.040, 0.112), (-0.034, 0.092), (-0.020, 0.050), (-0.020, 0.0001)]
    lathe(nom + "_Jante", CHROME, rim, centre, dehors, 16)
    d = np.asarray(dehors, float)
    ref = np.array([0.0, 1.0, 0.0])
    e1 = np.cross(d, ref)
    e1 /= np.linalg.norm(e1)
    e2 = np.cross(d, e1)
    for k in range(4):
        a = math.radians(45 + 90 * k)
        p = np.asarray(centre) + d * 0.036 + 0.026 * (math.cos(a) * e1 + math.sin(a) * e2)
        lathe(nom + "_Jante", CHROME, [(0.0, 0.0001), (0.0, 0.0075), (0.010, 0.0075), (0.012, 0.0001)], p, d, 6)


def construire():
    caisse_avant()
    piv = pare_brise()
    plancher_et_sieges()
    caisse_arriere()
    bache()
    fourche()
    roue("Roue_AV", ROUE_AV, (1.0, 0.0, 0.0))
    roue("Roue_ARG", (ROUE_AR_X, ROUE_R, ROUE_AR_Z), (1.0, 0.0, 0.0))
    roue("Roue_ARD", (-ROUE_AR_X, ROUE_R, ROUE_AR_Z), (-1.0, 0.0, 0.0))
    return piv


# ------------------------------------------------------------------------------------------------------------------
# Écriture OBJ / MTL
# ------------------------------------------------------------------------------------------------------------------
def nom_objet(group, mat):
    if group == "Caisse" or group == "Fourche":
        return group + "_" + mat
    return group


def ecrire():
    os.makedirs(DEST, exist_ok=True)
    with open(os.path.join(DEST, NOM + ".mtl"), "w") as f:
        for n, c in MATS.items():
            f.write("newmtl %s\nKd %.4f %.4f %.4f\nKa 0 0 0\nKs 0.05 0.05 0.05\nNs 10\nd 1\nillum 2\n\n" % (n, c[0], c[1], c[2]))
    objets = {}
    for (g, mat), data in GEO.items():
        objets.setdefault(nom_objet(g, mat), []).append((mat, data))
    nv = nt = 0
    with open(os.path.join(DEST, NOM + ".obj"), "w") as f:
        f.write("# %s : tricycle taxi d'après les photos de MATULA DESIGN (scripts/generer_tricycle.py).\n" % NOM)
        f.write("# Mètres, Y vers le haut, avant vers +Z, X vers la gauche du véhicule (Unity retourne X). Normales fournies.\n")
        f.write("mtllib %s.mtl\n" % NOM)
        base = 1
        for o in sorted(objets):
            f.write("o %s\ng %s\n" % (o, o))
            items = sorted(objets[o], key=lambda t: t[0])
            for mat, (V, N, F) in items:
                for v in V:
                    f.write("v %.4f %.4f %.4f\n" % (v[0], v[1], v[2]))
            for mat, (V, N, F) in items:
                for n in N:
                    f.write("vn %.4f %.4f %.4f\n" % (n[0], n[1], n[2]))
            off = base
            for mat, (V, N, F) in items:
                f.write("usemtl %s\n" % mat)
                for face in F:
                    f.write("f " + " ".join("%d//%d" % (off + k, off + k) for k in face) + "\n")
                    nt += len(face) - 2
                off += len(V)
            nv += off - base
            base = off
    allv = np.array([v for (V, N, F) in GEO.values() for v in V])
    mn, mx = allv.min(0), allv.max(0)
    print("%s : %d sommets, %d triangles, %d objets" % (NOM, nv, nt, len(objets)))
    print("   largeur %.3f m, hauteur %.3f m, longueur %.3f m (z %.3f -> %.3f)" % (mx[0] - mn[0], mx[1], mx[2] - mn[2], mn[2], mx[2]))
    for o in sorted(objets):
        V = np.array([v for _, (Vv, N, F) in objets[o] for v in Vv])
        c = (V.min(0) + V.max(0)) / 2
        print("   %-24s centre Unity (%.3f, %.3f, %.3f)  taille (%.3f, %.3f, %.3f)" % (o, -c[0], c[1], c[2], *(V.max(0) - V.min(0))))


# ------------------------------------------------------------------------------------------------------------------
# Aperçu (rendu logiciel, faces arrière masquées : une face retournée apparaîtrait comme un trou)
# ------------------------------------------------------------------------------------------------------------------
VARIANTES = {
    "bleu": {"Carrosserie": (0.11, 0.53, 0.86), "Bache": (0.89, 0.875, 0.85), "Siege": (0.90, 0.89, 0.87)},
    "blanc": {"Carrosserie": (0.92, 0.92, 0.91), "Bache": (0.10, 0.10, 0.11), "Siege": (0.12, 0.12, 0.13)},
    "rouge": {"Carrosserie": (0.80, 0.04, 0.035), "Bache": (0.10, 0.10, 0.11), "Siege": (0.12, 0.12, 0.13)},
}


def apercu(dossier, variante="bleu", gabarits=False, vues=None, taille=(760, 560), suffixe=""):
    from PIL import Image, ImageDraw
    cols = dict(MATS)
    cols.update(VARIANTES[variante])
    tris, tcol = [], []
    for (g, mat), (V, N, F) in GEO.items():
        c = np.array(cols[mat])
        for face in F:
            for i in range(1, len(face) - 1):
                tris.append([V[face[0]], V[face[i]], V[face[i + 1]]])
                tcol.append(c)
    if gabarits:
        for (sx, sy, sz) in SIEGES:
            for (dx, dy, dz, w, h, d) in ((0, 0.32, 0.0, 0.36, 0.55, 0.24), (0, 0.72, 0.03, 0.2, 0.22, 0.22), (0, 0.06, 0.22, 0.34, 0.14, 0.42)):
                cc = np.array([-sx + dx, sy + dy, sz + dz])
                hw = np.array([w, h, d]) / 2
                corners = [cc + hw * np.array(s_) for s_ in ((-1, -1, -1), (1, -1, -1), (1, 1, -1), (-1, 1, -1), (-1, -1, 1), (1, -1, 1), (1, 1, 1), (-1, 1, 1))]
                for q in ((0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (2, 3, 7, 6), (1, 2, 6, 5), (0, 4, 7, 3)):
                    P = [corners[k] for k in q]
                    tris.append([P[0], P[1], P[2]]); tcol.append(np.array((0.85, 0.35, 0.25)))
                    tris.append([P[0], P[2], P[3]]); tcol.append(np.array((0.85, 0.35, 0.25)))
    T = np.array(tris)
    Cc = np.array(tcol)
    W, H = taille
    SS = 2
    if vues is None:
        vues = {
            "trois_quarts_avant_gauche": (np.array([3.3, 1.75, 3.9]), np.array([0, 0.82, 0.05])),
            "trois_quarts_avant_droit": (np.array([-3.4, 1.55, 3.8]), np.array([0, 0.8, 0.05])),
            "face": (np.array([0.0, 1.0, 6.2]), np.array([0, 0.86, 0.0])),
            "arriere": (np.array([0.4, 1.2, -6.2]), np.array([0, 0.86, 0.0])),
            "cote_gauche": (np.array([6.6, 1.0, 0.0]), np.array([0, 0.86, 0.0])),
            "trois_quarts_arriere": (np.array([-3.0, 1.9, -4.2]), np.array([0, 0.8, 0.0])),
        }
    lum = np.array([0.45, 0.8, 0.55])
    lum /= np.linalg.norm(lum)
    os.makedirs(dossier, exist_ok=True)
    images = []
    n_all = np.cross(T[:, 1] - T[:, 0], T[:, 2] - T[:, 0])
    nn = np.linalg.norm(n_all, axis=1)
    ok = nn > 1e-12
    for nom, (eye, tgt) in vues.items():
        fwd = tgt - eye
        fwd /= np.linalg.norm(fwd)
        upv = np.array([0, 1.0, 0]) if abs(fwd[1]) < 0.95 else np.array([0, 0, 1.0])
        right = np.cross(fwd, upv)
        right /= np.linalg.norm(right)
        up = np.cross(right, fwd)
        f = 1.0 / math.tan(math.radians(16))
        w, h = W * SS, H * SS
        img = np.ones((h, w, 3)) * np.array([0.90, 0.91, 0.92])
        zb = np.full((h, w), np.inf)
        vis = ok & (np.einsum("ij,ij->i", n_all, eye - T[:, 0]) > 0)
        for t, c, n in zip(T[vis], Cc[vis], n_all[vis]):
            n = n / np.linalg.norm(n)
            rel = t - eye
            zc = rel @ fwd
            if (zc <= 0.05).any():
                continue
            xs = (rel @ right) / zc * f * h / 2 + w / 2
            ys = h / 2 - (rel @ up) / zc * f * h / 2
            shade = 0.38 + 0.62 * max(0.0, float(np.dot(n, lum))) + 0.08 * max(0.0, float(n[1]))
            col = np.clip(c * shade, 0, 1)
            x0, x1 = int(max(0, math.floor(xs.min()))), int(min(w - 1, math.ceil(xs.max())))
            y0, y1 = int(max(0, math.floor(ys.min()))), int(min(h - 1, math.ceil(ys.max())))
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
        im = Image.fromarray((img * 255).astype(np.uint8)).resize((W, H), Image.LANCZOS)
        ImageDraw.Draw(im).text((8, 8), "%s %s" % (variante, nom), fill=(0, 0, 0))
        pth = os.path.join(dossier, "tricycle_%s_%s%s.png" % (variante, nom, suffixe))
        im.save(pth)
        images.append(pth)
    return images


# Places (hanches sur l'assise, repère Unity : x vers la droite) : chauffeur, 2 places à côté de lui, 3 à l'arrière
SIEGES = [(0.0, 0.60, -0.04), (-0.37, 0.60, -0.04), (0.37, 0.60, -0.04), (-0.36, 0.60, -0.86), (0.0, 0.60, -0.86), (0.36, 0.60, -0.86)]


if __name__ == "__main__":
    piv = construire()
    print("Pivot de l'essuie-glace (Unity) : (%.3f, %.3f, %.3f)" % (-piv[0], piv[1], piv[2]))
    ecrire()
    if "--apercu" in sys.argv:
        d = sys.argv[sys.argv.index("--apercu") + 1]
        for v in ("bleu", "blanc", "rouge") if "--variantes" in sys.argv else ("bleu",):
            print("\n".join(apercu(d, v, gabarits="--gabarits" in sys.argv)))
