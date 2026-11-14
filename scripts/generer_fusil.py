#!/usr/bin/env python3
"""BALULA MOTO — fusil d'assaut de type Kalachnikov (AK) porté en bandoulière par les policiers du jeu.

Modèle générique, sans marquage : carcasse et canon en métal noirci, crosse, poignée et garde-main en bois,
chargeur courbe, tube des gaz, guidon, hausse, bretelle. Longueur 0,88 m (dimensions courantes de ce type d'arme).
Origine au milieu de la carcasse ; canon vers +Z, guidon vers +Y, X vers la gauche (Unity retourne X).
Le jeu ne l'utilise que comme accessoire porté dans le dos : aucune fonction de tir.

Usage : python3 scripts/generer_fusil.py [--apercu dossier]
"""
import math
import os
import sys

import numpy as np

ICI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, ICI)
import generer_tricycle as kit  # noqa: E402  (primitives de modélisation)

DEST = os.path.join(ICI, "..", "Assets", "BalulaMoto", "Resources", "BalulaMoto", "Accessoires")
NOM = "FusilAK"
MATS = {"Metal_Noirci": (0.11, 0.115, 0.12), "Bois": (0.42, 0.21, 0.10), "Bretelle": (0.16, 0.15, 0.12)}
M, W, S = "Metal_Noirci", "Bois", "Bretelle"
G = "Fusil"


def construire():
    kit.GEO.clear()
    rb = kit.rounded_box
    # carcasse et couvercle
    rb(G, M, (0.0, 0.0, 0.0), (0.040, 0.060, 0.24), 0.008)
    rb(G, M, (0.0, 0.036, -0.005), (0.036, 0.022, 0.22), 0.010)
    # poignée pistolet et pontet
    rb(G, W, (0.0, -0.065, -0.07), (0.030, 0.105, 0.042), 0.010, R=kit.rot_x(math.radians(-18)))
    kit.tube(G, M, (0.0, -0.035, -0.045), (0.0, -0.045, 0.02), 0.004, 6)
    # crosse en bois
    kit.loft(G, W, [kit.superellipse(0.0, y, 0.019, h, 3.0, 12, z) for (z, y, h) in
                    ((-0.12, 0.004, 0.026), (-0.20, -0.012, 0.033), (-0.30, -0.032, 0.045), (-0.385, -0.048, 0.058),
                     (-0.40, -0.050, 0.060))])
    rb(G, M, (0.0, -0.050, -0.404), (0.040, 0.122, 0.012), 0.005)     # plaque de couche
    # chargeur courbe
    secs = []
    for k in range(9):
        t = k / 8
        p = np.array([0.0, -0.03 - 0.19 * t, 0.06 + 0.075 * t ** 1.6])
        w = 0.026 + 0.004 * t
        secs.append([p + np.array([u, 0.0, v]) for (u, v) in ((-0.013, -w), (0.013, -w), (0.013, w), (-0.013, w))])
    kit.loft(G, M, secs, smooth=False)
    # garde-main et tube des gaz en bois, canon, bloc des gaz, guidon, cache-flamme
    rb(G, W, (0.0, -0.004, 0.20), (0.040, 0.050, 0.17), 0.012)
    rb(G, W, (0.0, 0.040, 0.19), (0.026, 0.024, 0.15), 0.010)
    kit.tube(G, M, (0.0, 0.0, 0.12), (0.0, 0.0, 0.47), 0.0085, 8)
    rb(G, M, (0.0, 0.022, 0.30), (0.024, 0.050, 0.03), 0.006)
    kit.tube(G, M, (0.0, 0.040, 0.27), (0.0, 0.040, 0.30), 0.009, 8)
    rb(G, M, (0.0, 0.035, 0.425), (0.010, 0.050, 0.014), 0.004)
    kit.tube(G, M, (0.0, 0.0, 0.45), (0.0, 0.0, 0.49), 0.0115, 8)
    rb(G, M, (0.0, 0.052, 0.105), (0.026, 0.016, 0.03), 0.004)        # hausse
    rb(G, M, (0.022, 0.012, 0.02), (0.006, 0.012, 0.05), 0.002)        # levier de sûreté
    # bretelle : du garde-main à la crosse, détendue
    path = []
    for k in range(13):
        t = k / 12
        z = 0.15 + (-0.36 - 0.15) * t
        y = -0.045 - 0.11 * math.sin(math.pi * t)
        path.append(np.array([0.022, y, z]))
    kit.sweep(G, S, path, 0.006, 6)
    return kit.GEO


def ecrire():
    os.makedirs(DEST, exist_ok=True)
    with open(os.path.join(DEST, NOM + ".mtl"), "w") as f:
        for n, c in MATS.items():
            f.write("newmtl %s\nKd %.4f %.4f %.4f\nKa 0 0 0\nKs 0.1 0.1 0.1\nNs 20\nd 1\nillum 2\n\n" % (n, c[0], c[1], c[2]))
    nv = nt = 0
    with open(os.path.join(DEST, NOM + ".obj"), "w") as f:
        f.write("# %s : fusil type Kalachnikov générique (scripts/generer_fusil.py). Mètres, canon vers +Z.\nmtllib %s.mtl\n" % (NOM, NOM))
        base = 1
        for (g, mat) in sorted(kit.GEO):
            V, N, F = kit.GEO[(g, mat)]
            o = "Fusil_" + mat
            f.write("o %s\ng %s\n" % (o, o))
            for v in V:
                f.write("v %.4f %.4f %.4f\n" % tuple(v))
            for n in N:
                f.write("vn %.4f %.4f %.4f\n" % tuple(n))
            f.write("usemtl %s\n" % mat)
            for face in F:
                f.write("f " + " ".join("%d//%d" % (base + k, base + k) for k in face) + "\n")
                nt += len(face) - 2
            base += len(V)
            nv += len(V)
    allv = np.array([v for (V, N, F) in kit.GEO.values() for v in V])
    print("%s : %d sommets, %d triangles, longueur %.3f m" % (NOM, nv, nt, allv[:, 2].max() - allv[:, 2].min()))


if __name__ == "__main__":
    construire()
    ecrire()
    if "--apercu" in sys.argv:
        kit.MATS.update(MATS)
        d = sys.argv[sys.argv.index("--apercu") + 1]
        vues = {"cote": (np.array([2.2, 0.1, 0.0]), np.array([0.0, -0.03, 0.04])),
                "trois_quarts": (np.array([1.4, 0.7, 1.4]), np.array([0.0, -0.03, 0.04]))}
        kit.VARIANTES["fusil"] = {}
        print(kit.apercu(d, "fusil", vues=vues, taille=(700, 420)))
