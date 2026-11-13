#!/usr/bin/env python3
"""BALULA MOTO — convertit les modèles FBX (texte, 7.4) fournis par MATULA DESIGN en OBJ + MTL pour Unity.

Les FBX d'origine (Docs/ModelesSources) sont écrits à la main : axe vertical Z, unité déclarée en centimètres alors
que les dimensions sont en mètres, pas de normales. Importés tels quels, Unity les réduirait à 1/100 ou les coucherait.
Le script garde la géométrie et les couleurs telles quelles, met l'axe Y vers le haut (mètres) et pose le modèle au sol.

Usage : python3 scripts/convertir_monuments.py SOURCE.fbx Nom
"""
import os, re, sys
import numpy as np

ICI = os.path.dirname(os.path.abspath(__file__))
DEST = os.path.join(ICI, "..", "Assets", "BalulaMoto", "Modeles", "Monuments")

def nums(txt, typ=float):
    return [typ(x) for x in txt.replace("\n", "").replace(" ", "").split(",") if x]

def main(src, nom):
    s = open(src, encoding="utf-8", errors="replace").read()
    geos = list(re.finditer(r'Geometry: (\d+), "Geometry::([^"]*)", "Mesh" \{', s))
    mats = {}
    for m in re.finditer(r'Material: (\d+), "Material::([^"]*)"', s):
        seg = s[m.end():m.end() + 3000]
        d = re.search(r'P: "DiffuseColor"[^\n]*?,\s*([-\d.e]+),\s*([-\d.e]+),\s*([-\d.e]+)', seg)
        mats[m.group(1)] = (m.group(2), tuple(float(x) for x in d.groups()) if d else (0.7, 0.7, 0.7))
    models = {m.group(1): m.group(2) for m in re.finditer(r'Model: (\d+), "Model::([^"]*)"', s)}
    links = re.findall(r'C: "OO",\s*(\d+),\s*(\d+)', s)
    geo_model = {a: b for a, b in links if b in models}
    model_mat = {b: a for a, b in links if a in mats and b in models}
    parts, allv = [], []
    for i, g in enumerate(geos):
        seg = s[g.end(): geos[i + 1].start() if i + 1 < len(geos) else len(s)]
        v = np.array(nums(re.search(r'Vertices: \*\d+ \{\s*a: ([^}]*)\}', seg).group(1))).reshape(-1, 3)
        raw = np.array(nums(re.search(r'PolygonVertexIndex: \*\d+ \{\s*a: ([^}]*)\}', seg).group(1)))
        if np.any(np.abs(raw - np.round(raw)) > 1e-6):
            # Fichier abîmé à l'export : une mise à l'échelle de la hauteur a aussi été appliquée à la liste des
            # faces (une valeur sur trois multipliée par le même facteur). On retrouve le facteur et on corrige.
            third = raw[2::3]
            nz = third[np.abs(third) > 20]
            k = np.median(np.round(nz / 0.9756097561) / nz)
            fixed = raw.copy()
            fixed[2::3] = third * k
            if np.any(np.abs(fixed - np.round(fixed)) > 1e-3):
                raise SystemExit("Liste des faces illisible dans " + g.group(2))
            print("   faces réparées (facteur %.5f) : %s" % (1 / k, g.group(2)))
            raw = fixed
        idx = [int(round(x)) for x in raw]
        if max(max(idx), max(-k - 1 for k in idx if k < 0)) >= len(v):
            raise SystemExit("Indices hors limites dans " + g.group(2))
        mat = mats.get(model_mat.get(geo_model.get(g.group(1), ""), ""), (g.group(2), (0.7, 0.7, 0.7)))
        parts.append((mat, v, idx))
        allv.append(v)
    allv = np.concatenate(allv)
    zmin = allv[:, 2].min()
    cx, cy = (allv[:, 0].min() + allv[:, 0].max()) / 2, (allv[:, 1].min() + allv[:, 1].max()) / 2
    os.makedirs(DEST, exist_ok=True)
    safe = lambda n: re.sub(r'[^A-Za-z0-9]+', '_', n).strip('_')
    with open(os.path.join(DEST, nom + ".mtl"), "w") as f:
        for (mn, c), _, _ in parts:
            f.write("newmtl %s\nKd %.4f %.4f %.4f\nKa 0 0 0\nKs 0.05 0.05 0.05\nNs 10\nd 1\nillum 2\n\n" % (safe(mn), c[0], c[1], c[2]))
    tris = 0
    with open(os.path.join(DEST, nom + ".obj"), "w") as f:
        f.write("# %s — modèle MATULA DESIGN converti par scripts/convertir_monuments.py (Y vers le haut, mètres)\nmtllib %s.mtl\n" % (nom, nom))
        base = 1
        for (mn, c), v, idx in parts:
            f.write("o %s\ng %s\nusemtl %s\n" % (safe(mn), safe(mn), safe(mn)))
            # FBX : X droite, Y profondeur, Z haut (main droite) -> OBJ : X, Y haut, Z = -Y (main droite)
            for x, y, z in v:
                f.write("v %.4f %.4f %.4f\n" % (x - cx, z - zmin, -(y - cy)))
            poly = []
            for k in idx:
                if k < 0:
                    poly.append(-k - 1)
                    f.write("f " + " ".join(str(base + p) for p in poly) + "\n")
                    tris += len(poly) - 2
                    poly = []
                else:
                    poly.append(k)
            base += len(v)
    e = allv.max(0) - allv.min(0)
    print("%s : %d parties, %d sommets, %d triangles, emprise %.1f x %.1f m, hauteur %.1f m" % (nom, len(parts), len(allv), tris, e[0], e[1], e[2]))
    for (mn, c), v, _ in parts:
        print("   %-24s %6d sommets  couleur %.2f %.2f %.2f" % (mn, len(v), *c))

if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
