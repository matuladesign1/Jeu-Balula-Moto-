#!/usr/bin/env python3
"""BALULA MOTO — convertit le bus TRANSCO fourni par MATULA DESIGN (FBX texte 7.4) en OBJ + MTL pour Unity.

Le FBX d'origine (Docs/ModelesSources/TRANSCO_Bus_Unity.fbx) déclare l'axe Y vers le haut mais ses sommets sont écrits
avec Z vers le haut (hauteur 0,16 à 3,59 m), en mètres alors que l'unité déclarée est le centimètre, sans normales.
Importé tel quel, Unity coucherait le bus sur le côté et le réduirait à 1/100. Le script :
  - garde la géométrie et les couleurs telles quelles ;
  - met l'avant du bus (X positif du fichier, côté pare-brise et inscription TRANSCO) vers l'avant d'Unity (+Z),
    le haut vers +Y, et pose les roues au sol (y = 0) ;
  - sépare les pièces dont le jeu a besoin : 4 roues (pneu et jante, pour qu'elles tournent), phares, feux stop,
    clignotants ; le reste forme la carrosserie (une pièce par matériau).

Usage : python3 scripts/convertir_bus.py SOURCE.fbx [Nom]
"""
import os, re, sys
from collections import defaultdict

ICI = os.path.dirname(os.path.abspath(__file__))
DEST = os.path.join(ICI, "..", "Assets", "BalulaMoto", "Resources", "BalulaMoto", "Vehicules")


def tableau(s, nom, typ):
    m = re.search(nom + r':\s*\*(\d+)\s*\{\s*a:\s*([^}]*)\}', s)
    if not m:
        raise SystemExit("Tableau " + nom + " introuvable")
    vals = [typ(x) for x in m.group(2).replace("\n", "").replace(" ", "").split(",") if x]
    if len(vals) != int(m.group(1)):
        raise SystemExit("Tableau %s : %d valeurs au lieu de %s" % (nom, len(vals), m.group(1)))
    return vals


def main(src, nom="BusTransco"):
    s = open(src, encoding="utf-8", errors="replace").read()
    V = tableau(s, "Vertices", float)
    I = tableau(s, "PolygonVertexIndex", int)
    M = tableau(s, "Materials", int)
    P = [tuple(V[i:i + 3]) for i in range(0, len(V), 3)]
    mats = []
    for m in re.finditer(r'Material: (\d+), "Material::([^"]*)"', s):
        seg = s[m.end():m.end() + 600]
        d = re.search(r'P: "DiffuseColor"[^\n]*?,\s*([-\d.e]+),\s*([-\d.e]+),\s*([-\d.e]+)', seg)
        mats.append((m.group(2), tuple(float(x) for x in d.groups())))
    polys, cur = [], []
    for k in I:
        if k < 0:
            cur.append(~k)
            polys.append(cur)
            cur = []
        else:
            cur.append(k)
    if len(polys) != len(M):
        raise SystemExit("Nombre de faces (%d) différent du nombre de matériaux par face (%d)" % (len(polys), len(M)))
    if max(max(p) for p in polys) >= len(P):
        raise SystemExit("Indice de sommet hors limites")
    nommat = [n for n, _ in mats]

    # Pièces connexes (sommets partagés)
    parent = list(range(len(P)))

    def racine(a):
        while parent[a] != a:
            parent[a] = parent[parent[a]]
            a = parent[a]
        return a

    for poly in polys:
        for k in poly[1:]:
            ra, rb = racine(poly[0]), racine(k)
            if ra != rb:
                parent[ra] = rb
    comp = defaultdict(lambda: {"faces": [], "mat": None, "b": [1e9, -1e9, 1e9, -1e9, 1e9, -1e9]})
    for fi, poly in enumerate(polys):
        c = comp[racine(poly[0])]
        c["faces"].append(fi)
        c["mat"] = nommat[M[fi]]
        b = c["b"]
        for k in poly:
            x, y, z = P[k]
            b[0] = min(b[0], x); b[1] = max(b[1], x); b[2] = min(b[2], y); b[3] = max(b[3], y); b[4] = min(b[4], z); b[5] = max(b[5], z)
    centre = lambda b: ((b[0] + b[1]) / 2, (b[2] + b[3]) / 2, (b[4] + b[5]) / 2)

    # Roues : les pneus (caoutchouc), puis jantes, boulons et moyeux autour de chacun
    pneus = [c for c in comp.values() if c["mat"] == "Rubber_Black"]
    if len(pneus) != 4:
        raise SystemExit("4 pneus attendus, %d trouvés" % len(pneus))
    roues = {}
    for c in pneus:
        cx, cy, cz = centre(c["b"])
        cle = ("AV" if cx > 0 else "AR") + ("G" if cy > 0 else "D")
        roues[cle] = {"centre": (cx, cy, cz), "Pneu": list(c["faces"]), "Jante": []}
        c["pris"] = True
    for c in comp.values():
        if c.get("pris"):
            continue
        cx, cy, cz = centre(c["b"])
        for cle, r in roues.items():
            rx, ry, rz = r["centre"]
            if abs(cx - rx) < 0.75 and abs(cz - rz) < 0.75 and abs(cy) > 1.0 and (cy > 0) == (ry > 0) and c["mat"] in ("Metal_Silver", "Metal_Dark"):
                r["Jante" if c["mat"] == "Metal_Silver" else "Pneu"].extend(c["faces"])
                c["pris"] = True
                break

    # Feux : à l'avant, phares en haut et clignotants en bas ; à l'arrière, stop en haut et en bas, clignotant au milieu
    groupes = defaultdict(list)
    for c in comp.values():
        if c.get("pris") or c["mat"] != "Light":
            continue
        cx, cy, cz = centre(c["b"])
        if cx > 0:
            groupes["Phare" if cz > 1.2 else "ClignoAV"].extend(c["faces"])
        else:
            groupes["ClignoAR" if 1.15 < cz < 1.38 else "Stop"].extend(c["faces"])
        c["pris"] = True
    for c in comp.values():
        if not c.get("pris"):
            groupes["Caisse_" + c["mat"]].extend(c["faces"])
    for cle, r in sorted(roues.items()):
        groupes["Roue_%s_Pneu" % cle] = r["Pneu"]
        groupes["Roue_%s_Jante" % cle] = r["Jante"]

    xs = [p[0] for p in P]
    zmin = min(p[2] for p in P)
    cx = (min(xs) + max(xs)) / 2
    os.makedirs(DEST, exist_ok=True)
    with open(os.path.join(DEST, nom + ".mtl"), "w") as f:
        for n, c in mats:
            f.write("newmtl %s\nKd %.4f %.4f %.4f\nKa 0 0 0\nKs 0.05 0.05 0.05\nNs 10\nd 1\nillum 2\n\n" % (n, c[0], c[1], c[2]))
    tris = 0
    with open(os.path.join(DEST, nom + ".obj"), "w") as f:
        f.write("# %s — bus TRANSCO de MATULA DESIGN converti par scripts/convertir_bus.py (Y vers le haut, mètres,\n" % nom)
        f.write("# avant du bus vers +Z dans Unity, roues au sol). Pièces : Caisse_*, Roue_*_Pneu/Jante, Phare, Stop, Cligno*.\n")
        f.write("mtllib %s.mtl\n" % nom)
        base = 1
        for g in sorted(groupes):
            faces = groupes[g]
            if not faces:
                continue
            verts = sorted({k for fi in faces for k in polys[fi]})
            loc = {k: i for i, k in enumerate(verts)}
            f.write("o %s\ng %s\n" % (g, g))
            # FBX (main droite, Z haut, avant +X) -> OBJ (main droite, Y haut) : (y, z, x).
            # Unity retourne l'axe X des OBJ : l'avant du bus arrive sur +Z, l'inscription TRANSCO se lit à l'endroit.
            for k in verts:
                x, y, z = P[k]
                f.write("v %.4f %.4f %.4f\n" % (y, z - zmin, x - cx))
            parmat = defaultdict(list)
            for fi in faces:
                parmat[nommat[M[fi]]].append(fi)
            for mn in sorted(parmat):
                f.write("usemtl %s\n" % mn)
                for fi in parmat[mn]:
                    f.write("f " + " ".join(str(base + loc[k]) for k in polys[fi]) + "\n")
                    tris += len(polys[fi]) - 2
            base += len(verts)
    print("%s : %d sommets, %d triangles, %d pièces ; longueur %.2f m, largeur %.2f m, hauteur %.2f m (roues posées au sol, %.2f m retirés)"
          % (nom, len(P), tris, sum(1 for g in groupes.values() if g), max(xs) - min(xs),
             max(p[1] for p in P) - min(p[1] for p in P), max(p[2] for p in P) - zmin, zmin))
    for cle, r in sorted(roues.items()):
        rx, ry, rz = r["centre"]
        print("   roue %s : centre avant/arrière %.2f m, côté %.2f m, hauteur %.2f m (Unity : x=%.2f y=%.2f z=%.2f)"
              % (cle, rx, ry, rz, -ry, rz - zmin, rx - cx))
    for g in sorted(groupes):
        print("   %-22s %5d faces" % (g, len(groupes[g])))


if __name__ == "__main__":
    main(*sys.argv[1:])
