#!/usr/bin/env python3
"""BALULA MOTO — trois voitures génériques pour la circulation et les parkings (aucune marque, aucun logo).

MATULA DESIGN a envoyé trois modèles de voitures de marque (coupé américain, citadine japonaise, SUV de luxe) dont les
droits d'utilisation sont incertains : le créateur a choisi de garder des voitures génériques du même genre.
  Voiture_Coupe    : grand coupé sportif à long capot (5,00 m)
  Voiture_Citadine : petite citadine à hayon (3,80 m)
  Voiture_SUV      : SUV compact (4,50 m)

Pièces (noms lus par ModelVehicles.cs) : Caisse_Peinture (couleur tirée au hasard par le jeu), Caisse_Vitre, Caisse_Noir,
Caisse_Chrome, Caisse_Plaque, Roue_AVG|AVD|ARG|ARD_Pneu/_Jante, Phare_G/D, Stop_G/D, Cligno_AVG/AVD/ARG/ARD.
Repère : mètres, X = gauche, Y vers le haut, Z vers l'avant, sol à Y = 0, empattement centré sur Z = 0.

Usage : python3 scripts/generer_voitures.py [--apercu DOSSIER]
"""
import argparse
import math
import os
import sys

import numpy as np

ICI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(ICI, "outils3d"))
import assets3d as A  # noqa: E402

DEST = os.path.join(ICI, "..", "Assets", "BalulaMoto", "Resources", "BalulaMoto", "Vehicules")

MATS = {
    "Peinture": (0.75, 0.75, 0.77), "Vitre": (0.12, 0.15, 0.18), "Noir": (0.05, 0.05, 0.055), "Chrome": (0.8, 0.81, 0.83),
    "Plaque": (0.95, 0.95, 0.93), "Pneu": (0.05, 0.05, 0.05), "Jante": (0.7, 0.71, 0.73), "Phare_Verre": (0.92, 0.93, 0.92),
    "Feu_Rouge": (0.62, 0.04, 0.04), "Orange": (1.0, 0.52, 0.05),
}

# Voitures : longueur, largeur, empattement, rayon de roue, largeur de pneu, garde au sol ; profil latéral par
# sections (z, demi-largeur, ligne de caisse ys, toit yt, demi-largeur de toit wg) de l'arrière vers l'avant ;
# fenêtres : pare-brise (z bas, z haut), lunette (z haut, z bas), montants (z) noircis.
VOITURES = {
    "Voiture_Coupe": dict(
        L=5.0, W=1.92, wb=2.95, R=0.35, tw=0.25, yb=0.2,
        secs=[(-2.50, 0.84, 0.86, 0.86, 0.0), (-2.44, 0.93, 0.94, 0.94, 0.0), (-2.25, 0.955, 0.98, 0.98, 0.0),
              (-1.60, 0.96, 0.98, 0.98, 0.0), (-1.28, 0.96, 0.98, 1.10, 0.62), (-0.95, 0.955, 0.97, 1.36, 0.66),
              (-0.62, 0.955, 0.96, 1.42, 0.67), (0.15, 0.955, 0.95, 1.42, 0.67), (0.45, 0.955, 0.95, 1.35, 0.66),
              (0.95, 0.955, 0.95, 0.95, 0.0), (1.70, 0.955, 0.93, 0.93, 0.0), (2.30, 0.95, 0.88, 0.88, 0.0),
              (2.46, 0.92, 0.83, 0.83, 0.0), (2.50, 0.84, 0.78, 0.78, 0.0)],
        ws=(0.95, 0.15), rw=(-0.62, -1.28), pillars=[-0.25], nose=0.62, tail=0.62),
    "Voiture_Citadine": dict(
        L=3.8, W=1.665, wb=2.43, R=0.29, tw=0.18, yb=0.17,
        secs=[(-1.85, 0.74, 0.92, 1.18, 0.5), (-1.80, 0.80, 0.98, 1.36, 0.56), (-1.65, 0.825, 1.0, 1.47, 0.6),
              (-1.35, 0.83, 0.99, 1.52, 0.62), (0.25, 0.83, 0.95, 1.52, 0.63), (0.55, 0.83, 0.94, 1.46, 0.62),
              (1.10, 0.83, 0.90, 0.90, 0.0), (1.55, 0.82, 0.84, 0.84, 0.0), (1.85, 0.79, 0.76, 0.76, 0.0),
              (1.95, 0.72, 0.70, 0.70, 0.0)],
        ws=(1.10, 0.25), rw=(-1.65, -1.85), pillars=[-0.55], nose=0.5, tail=0.55),
    "Voiture_SUV": dict(
        L=4.5, W=1.84, wb=2.66, R=0.36, tw=0.24, yb=0.24,
        secs=[(-2.25, 0.82, 1.0, 1.30, 0.56), (-2.18, 0.89, 1.04, 1.46, 0.64), (-2.0, 0.915, 1.06, 1.56, 0.69),
              (-1.6, 0.92, 1.06, 1.60, 0.71), (0.35, 0.92, 1.03, 1.60, 0.71), (0.70, 0.92, 1.02, 1.52, 0.70),
              (1.30, 0.92, 1.00, 1.00, 0.0), (1.85, 0.915, 0.96, 0.96, 0.0), (2.15, 0.89, 0.90, 0.90, 0.0),
              (2.25, 0.82, 0.84, 0.84, 0.0)],
        ws=(1.30, 0.35), rw=(-2.0, -2.25), pillars=[-0.55, -1.25], nose=0.62, tail=0.62),
}

NL = 8   # points de la caisse basse (demi-section)
NU = 5   # points du pavillon


def demi_section(w, yb, ys, yt, wg):
    """Demi-section (x >= 0) du bas-milieu au haut-milieu."""
    low = [(0.0, yb), (w - 0.16, yb), (w - 0.05, yb + 0.05), (w - 0.005, yb + 0.16), (w, (yb + ys) * 0.5),
           (w, ys - 0.1), (w - 0.015, ys - 0.035), (w - 0.06, ys)]
    deck = [(w - 0.08, ys + 0.004), (w * 0.72, ys + 0.022), (w * 0.48, ys + 0.03), (w * 0.24, ys + 0.034), (0.0, ys + 0.035)]
    g = float(np.clip((yt - ys) / 0.28, 0.0, 1.0))
    wgb = w - 0.1
    gh = [(wgb, ys + 0.01), (wg + 0.05, yt - 0.09), (wg - 0.03, yt - 0.015), (wg * 0.5, yt), (0.0, yt + 0.003)]
    up = [((1 - g) * a[0] + g * b[0], (1 - g) * a[1] + g * b[1]) for a, b in zip(deck, gh)]
    return low + up


def anneau(z, w, yb, ys, yt, wg):
    half = demi_section(w, yb, ys, yt, wg)
    right = [(-x, y) for x, y in half]                  # côté droit (x < 0)
    left = [(x, y) for x, y in half[::-1]][1:-1]          # côté gauche (x > 0), sans doublons au milieu
    pts = right + left
    return np.array([(x, y, z) for x, y in pts])


def interp_secs(secs, zs):
    K = np.array(secs, float)
    out = []
    for z in zs:
        row = [z] + [float(np.interp(z, K[:, 0], K[:, k])) for k in range(1, 5)]
        out.append(row)
    return out


def corps(spec):
    L, wb, R = spec["L"], spec["wb"], spec["R"]
    zr, zf = spec["secs"][0][0], spec["secs"][-1][0]
    # sections serrées aux extrémités et aux passages de roues
    zs = sorted(set(np.round(np.concatenate([np.linspace(zr, zf, 46), np.linspace(zr, zr + 0.25, 6), np.linspace(zf - 0.25, zf, 6),
                                             [s[0] for s in spec["secs"]]]), 4)))
    rows = interp_secs(spec["secs"], zs)
    axles = (wb / 2, -wb / 2)
    rings = []
    for z, w, ys, yt, wg in rows:
        yb = spec["yb"]
        for za in axles:
            d = abs(z - za)
            if d < R + 0.07:
                yb = max(yb, R + math.sqrt(max(0.0, (R + 0.07) ** 2 - d * d)))
        # extrémités arrondies : bas relevé (pare-chocs)
        if z - zr < 0.12 or zf - z < 0.12:
            yb = max(yb, spec["yb"] + 0.1)
        if wg <= 0.0:
            wg = w * 0.6
        rings.append(anneau(z, w, yb, ys, yt, wg))
    G = np.array(rings)                                   # K × M × 3
    K, M = G.shape[:2]
    nh = NL + NU                                          # points par demi-section
    parts = {}

    def put(mat, quad):
        parts.setdefault(mat, []).append(quad)

    ws0, ws1 = spec["ws"]
    rw0, rw1 = spec["rw"]
    for k in range(K - 1):
        zc = 0.5 * (G[k, 0, 2] + G[k + 1, 0, 2])
        for i in range(M):
            j = (i + 1) % M
            quad = [G[k, i], G[k + 1, i], G[k + 1, j], G[k, j]]
            # indice dans la demi-section (0 = bas milieu … nh-1 = haut milieu), symétrique
            hi = i if i < nh else (M - i)
            hj = j if j < nh else (M - j)
            h = min(hi, hj)
            mat = "Peinture"
            if h == 0:
                mat = "Noir"                                  # dessous
            elif h >= NL - 1:
                u = h - (NL - 1)                              # 0 = base du pavillon
                in_ws = ws1 < zc < ws0
                in_rw = rw1 < zc < rw0
                in_roof = rw0 <= zc <= ws1
                if in_ws:
                    mat = "Vitre" if u >= 1 else "Peinture"
                elif in_rw:
                    mat = "Vitre" if u >= 1 else "Peinture"
                elif in_roof:
                    if u in (1, 2):
                        mat = "Vitre"
                        for pz in spec["pillars"]:
                            if abs(zc - pz) < 0.06:
                                mat = "Noir"
                        if zc - rw0 < 0.07 or ws1 - zc < 0.05:
                            mat = "Peinture"
            put(mat, quad)
    out = []
    for mat, quads in parts.items():
        V = np.array([q for quad in quads for q in (quad[0], quad[1], quad[2], quad[0], quad[2], quad[3])])
        F = np.arange(len(V)).reshape(-1, 3)
        p = {"name": "Caisse_" + mat, "mat": {"name": mat, "color": MATS[mat], "tex": None}, "V": V, "N": np.zeros_like(V),
             "UV": np.zeros((len(V), 2)), "F": F}
        out.append(p)
    # orientation : les quadrilatères ont tous le même sens de parcours ; un seul test sur un flanc, au milieu
    kq, iq = K // 2, NL // 2
    P0, P1, P2 = G[kq, iq], G[kq + 1, iq], G[kq + 1, iq + 1]
    n = np.cross(P1 - P0, P2 - P0)
    out_dir = np.array([P0[0], 0.0, 0.0])
    if np.dot(n, out_dir) < 0:
        for p in out:
            p["F"] = p["F"][:, ::-1].copy()
    # bouchons avant et arrière (éventails)
    for k, sign in ((0, -1.0), (K - 1, 1.0)):
        ring = G[k]
        cc = ring.mean(0)
        tris = []
        for i in range(M):
            a, b = ring[i], ring[(i + 1) % M]
            t = [cc, a, b]
            n = np.cross(b - a, cc - a)
            if n[2] * sign < 0:
                t = [cc, b, a]
            tris.extend(t)
        V = np.array(tris)
        out.append({"name": "Caisse_Peinture", "mat": {"name": "Peinture", "color": MATS["Peinture"], "tex": None}, "V": V,
                    "N": np.zeros_like(V), "UV": np.zeros((len(V), 2)), "F": np.arange(len(V)).reshape(-1, 3)})
    return out, G


def boite(name, mat, c, size, r=0.0):
    """Boîte (arêtes chanfreinées si r > 0)."""
    hx, hy, hz = np.asarray(size, float) / 2
    if r <= 0:
        P = np.array([[x, y, z] for x in (-hx, hx) for y in (-hy, hy) for z in (-hz, hz)])
        faces = [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)]
        V = []
        for f in faces:
            q = [P[i] for i in f]
            V.extend([q[0], q[1], q[2], q[0], q[2], q[3]])
        V = np.array(V) + np.asarray(c, float)
    else:
        # super-ellipsoïde grossière
        U, Vn = 12, 6
        pts = np.zeros((Vn + 1, U, 3))
        for a in range(Vn + 1):
            th = -math.pi / 2 + math.pi * a / Vn
            for b in range(U):
                ph = 2 * math.pi * b / U
                ct, st, cp, sp = math.cos(th), math.sin(th), math.cos(ph), math.sin(ph)
                e = 0.35
                f = lambda v: math.copysign(abs(v) ** e, v)
                pts[a, b] = (hx * f(ct) * f(cp), hy * f(st), hz * f(ct) * f(sp))
        V = []
        for a in range(Vn):
            for b in range(U):
                q = [pts[a, b], pts[a, (b + 1) % U], pts[a + 1, (b + 1) % U], pts[a + 1, b]]
                V.extend([q[0], q[2], q[1], q[0], q[3], q[2]])
        V = np.array(V) + np.asarray(c, float)
    F = np.arange(len(V)).reshape(-1, 3)
    p = {"name": name, "mat": {"name": mat, "color": MATS[mat], "tex": None}, "V": V, "N": np.zeros_like(V), "UV": np.zeros((len(V), 2)), "F": F}
    # faces vers l'extérieur
    P = V[F]
    n = np.cross(P[:, 1] - P[:, 0], P[:, 2] - P[:, 0])
    flip = (n * (P.mean(1) - np.asarray(c, float))).sum(1) < 0
    p["F"][flip] = p["F"][flip][:, ::-1]
    return p


def roue(nom, centre, R, tw, outward):
    """Pneu (tore aplati) et jante à voile creux ; axe selon X."""
    parts = []
    segs = 20
    prof_pneu = [(-tw / 2, R * 0.62), (-tw / 2, R * 0.86), (-tw * 0.42, R * 0.97), (-tw * 0.2, R), (tw * 0.2, R), (tw * 0.42, R * 0.97),
                 (tw / 2, R * 0.86), (tw / 2, R * 0.62)]
    prof_jante = [(-tw * 0.38, 0.0), (-tw * 0.38, R * 0.64), (tw * 0.1, R * 0.64), (tw * 0.36, R * 0.6), (tw * 0.42, R * 0.2), (tw * 0.44, 0.0)]
    for mat, prof, closed in (("Pneu", prof_pneu, True), ("Jante", prof_jante, False)):
        V = []
        for s in range(segs):
            a0, a1 = 2 * math.pi * s / segs, 2 * math.pi * (s + 1) / segs
            for t in range(len(prof) - (0 if closed else 1)):
                u0, r0 = prof[t]
                u1, r1 = prof[(t + 1) % len(prof)]
                def pt(u, r, a):
                    x = u * outward
                    return np.array([x, r * math.cos(a), r * math.sin(a)]) + centre
                q = [pt(u0, r0, a0), pt(u1, r1, a0), pt(u1, r1, a1), pt(u0, r0, a1)]
                V.extend([q[0], q[1], q[2], q[0], q[2], q[3]])
        V = np.array(V)
        F = np.arange(len(V)).reshape(-1, 3)
        P = V[F]
        n = np.cross(P[:, 1] - P[:, 0], P[:, 2] - P[:, 0])
        cc = P.mean(1)
        radial = cc - centre
        radial[:, 0] = 0
        if mat == "Pneu":
            # loin du cercle central du tore (section du pneu centrée sur r = 0,81 R)
            rr = np.linalg.norm(radial, axis=1, keepdims=True)
            core = centre + radial / np.maximum(1e-9, rr) * (0.81 * R)
            ref = cc - core
        else:
            ref = np.zeros_like(cc)
            ref[:, 0] = outward
        flip = (n * ref).sum(1) < 0
        F[flip] = F[flip][:, ::-1]
        parts.append({"name": "%s_%s" % (nom, mat), "mat": {"name": mat, "color": MATS[mat], "tex": None}, "V": V,
                      "N": np.zeros_like(V), "UV": np.zeros((len(V), 2)), "F": F})
    # moyeu et 5 branches sombres sur la face extérieure
    for b in range(5):
        a = 2 * math.pi * b / 5
        c = centre + np.array([outward * tw * 0.43, R * 0.36 * math.cos(a), R * 0.36 * math.sin(a)])
        p = boite(nom + "_Jante", "Noir", c, (0.01, R * 0.16, R * 0.16))
        p["name"] = nom + "_Jante"
        parts.append(p)
    return parts


def voiture(nom, spec):
    out, G = corps(spec)
    L, W, wb, R, tw = spec["L"], spec["W"], spec["wb"], spec["R"], spec["tw"]
    zf, zr = spec["secs"][-1][0], spec["secs"][0][0]
    # Roues (X = gauche)
    xw = W / 2 - tw / 2 - 0.04
    for code, sx, z in (("AVG", 1, wb / 2), ("AVD", -1, wb / 2), ("ARG", 1, -wb / 2), ("ARD", -1, -wb / 2)):
        out += roue("Roue_" + code, np.array([sx * xw, R, z]), R, tw, sx)
    # Avant : phares, calandre, pare-chocs, clignotants
    secf = interp_secs(spec["secs"], [zf - 0.06])[0]
    ysf = secf[2]
    wf = secf[1]
    for code, sx in (("G", 1), ("D", -1)):
        out.append(boite("Phare_" + code, "Phare_Verre", (sx * (wf - 0.2), ysf - 0.1, zf - 0.015), (0.26, 0.1, 0.07)))
        out.append(boite("Cligno_AV" + code, "Orange", (sx * (wf - 0.04), ysf - 0.11, zf - 0.03), (0.05, 0.06, 0.08)))
        out.append(boite("Stop_" + code, "Feu_Rouge", (sx * (wf - 0.14), spec["secs"][0][2] - 0.12, zr + 0.015), (0.24, 0.1, 0.07)))
        out.append(boite("Cligno_AR" + code, "Orange", (sx * (wf - 0.07), spec["secs"][0][2] - 0.21, zr + 0.015), (0.08, 0.05, 0.06)))
        # rétroviseurs (base du pare-brise)
        zws = spec["ws"][0]
        sws = interp_secs(spec["secs"], [zws])[0]
        out.append(boite("Caisse_Peinture", "Peinture", (sx * (sws[1] + 0.06), sws[2] + 0.12, zws - 0.08), (0.12, 0.09, 0.14), r=0.02))
    out.append(boite("Caisse_Noir", "Noir", (0.0, ysf - 0.28, zf - 0.04), (wf * 1.0, 0.16, 0.07), r=0.015))      # calandre basse
    out.append(boite("Caisse_Noir", "Noir", (0.0, ysf - 0.12, zf - 0.03), (wf * 0.55, 0.1, 0.05)))                # calandre
    out.append(boite("Caisse_Noir", "Noir", (0.0, spec["yb"] + 0.18, zr + 0.01), (wf * 1.7, 0.12, 0.06)))          # pare-chocs arrière
    out.append(boite("Caisse_Plaque", "Plaque", (0.0, spec["yb"] + 0.38, zr - 0.012), (0.42, 0.1, 0.01)))
    out.append(boite("Caisse_Plaque", "Plaque", (0.0, ysf - 0.3, zf + 0.005), (0.42, 0.1, 0.01)))
    # bas de caisse noirs
    for sx in (1, -1):
        out.append(boite("Caisse_Noir", "Noir", (sx * (W / 2 - 0.01), spec["yb"] + 0.07, 0.0), (0.03, 0.1, wb - 2 * R - 0.25)))
    out = A.merge_by_material(out)
    A.renormal(out, 38)
    nv, nt = A.write_obj(out, DEST, nom, comment=nom + " — voiture générique BALULA MOTO (sans marque)")
    mn, mx = A.bounds(out)
    print(nom, nv, "sommets", nt, "triangles", "dimensions", np.round(mx - mn, 3))
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--apercu", default="")
    a = ap.parse_args()
    ims = []
    for nom, spec in VOITURES.items():
        parts = voiture(nom, spec)
        if a.apercu:
            os.makedirs(a.apercu, exist_ok=True)
            show = []
            for p in parts:
                q = dict(p)
                q["mat"] = dict(p["mat"])
                if q["mat"]["name"] == "Peinture":
                    q["mat"]["color"] = {"Voiture_Coupe": (0.75, 0.08, 0.06), "Voiture_Citadine": (0.92, 0.92, 0.9), "Voiture_SUV": (0.12, 0.2, 0.35)}[nom]
                show.append(q)
            c = np.array([0.0, 0.7, 0.0])
            for d in ((4.2, 1.6, 3.6), (-4.0, 1.4, -3.4), (5.5, 0.6, 0.0)):
                ims.append(A.render(show, c + np.array(d), c, 420, 300, fov=40, ss=1))
    if a.apercu:
        A.sheet(ims, 3, os.path.join(a.apercu, "voitures.png"))


if __name__ == "__main__":
    main()
