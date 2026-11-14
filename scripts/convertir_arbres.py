#!/usr/bin/env python3
# BALULA MOTO — conversion des arbres fournis par MATULA DESIGN (Blend Swap, licence CC0) en modèles légers :
#   • « Low-Poly Tree » (laser blaster)        → Arbre_Simple (≈ 500 triangles)
#   • « Low-poly foliege » (EugeneKiver)        → Arbre_Chene, Arbre_Erable, Arbre_Pin (feuillage allégé)
# Les feuilles sont des cartes texturées avec transparence (couleur + masque réunis en PNG RGBA) ; l'écorce garde
# sa texture. Sortie : Assets/BalulaMoto/Resources/BalulaMoto/Decor/Arbre_*.obj (+ .mtl, textures).
# Usage : python3 scripts/convertir_arbres.py <Low poly foliege.blend> <New Tree Lod Packed.blend> [--apercu DIR]
import io, os, sys
import numpy as np
from PIL import Image
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "outils3d"))
import assets3d as A
from blendfile import BlendFile
from blend2obj import image_bytes

OUT = os.path.join(HERE, "..", "Assets", "BalulaMoto", "Resources", "BalulaMoto", "Decor")


def slots(path):
    """Images de chaque matériau : couleur (mapto 1) et masque de transparence (mapto 128)."""
    bf = BlendFile(path)
    res = {}
    for b in bf.find_blocks("MA"):
        ma = bf.block_structs(b)[0]
        name = ma.get("id").get("name")[2:]
        col = alpha = None
        for mt_addr in (ma.get("mtex") or []):
            mt = bf.deref_struct(mt_addr) if mt_addr else None
            if mt is None:
                continue
            tex = bf.deref_struct(mt.get("tex")) if mt.get("tex") else None
            im = bf.deref_struct(tex.get("ima")) if tex is not None and tex.get("ima") else None
            if im is None:
                continue
            _, data = image_bytes(bf, im)
            if not data:
                continue
            img = Image.open(io.BytesIO(data))
            img.load()
            mapto = mt.get("mapto") or 0
            if mapto & 1 and col is None:
                col = img
                if mapto & 128:
                    alpha = img
            elif mapto & 128 and alpha is None:
                alpha = img
        res[name] = (col, alpha)
    return res


def rgba(col, alpha, size):
    c = col.convert("RGBA").resize((size, size), Image.LANCZOS)
    if alpha is not None and alpha is not col:
        a = alpha.convert("L").resize((size, size), Image.LANCZOS)
        # Masque à fond blanc et feuilles noires (pack « Low-poly foliege ») : on l'inverse
        arr = np.asarray(a)
        border = np.concatenate([arr[0], arr[-1], arr[:, 0], arr[:, -1]])
        if border.mean() > 128:
            a = Image.fromarray(255 - arr)
        c.putalpha(a)
    return c


def components(part):
    """Composantes connexes de triangles (une carte de feuilles = une composante)."""
    F = part["F"]
    parent = list(range(len(part["V"])))

    def find(x):
        while parent[x] != x:
            parent[x] = parent[parent[x]]
            x = parent[x]
        return x
    for f in F:
        a = find(int(f[0]))
        for v in f[1:]:
            b = find(int(v))
            if a != b:
                parent[b] = a
    groups = {}
    for i, f in enumerate(F):
        groups.setdefault(find(int(f[0])), []).append(i)
    return list(groups.values())


def thin_leaves(part, keep, grow, seed):
    """Garde une partie des cartes de feuilles (réparties au hasard) et les agrandit un peu pour garder le volume."""
    rnd = np.random.RandomState(seed)
    comps = components(part)
    sel = [c for c in comps if rnd.rand() < keep]
    mask = np.zeros(len(part["F"]), bool)
    for c in sel:
        mask[c] = True
    p = A.tri_subset(part, mask)
    V = p["V"].copy()
    # Agrandit chaque carte autour de son centre
    for c in components(p):
        idx = np.unique(p["F"][c].reshape(-1))
        ctr = V[idx].mean(axis=0)
        V[idx] = ctr + (V[idx] - ctr) * grow
    p["V"] = V
    return p


def normalize(parts, height):
    lo, hi = A.bounds(parts)
    # Pied du tronc : centre des sommets les plus bas
    allV = np.concatenate([p["V"] for p in parts])
    low = allV[allV[:, 1] < lo[1] + 0.25]
    base = np.array([low[:, 0].mean(), lo[1], low[:, 2].mean()])
    s = height / (hi[1] - lo[1])
    out = []
    for p in parts:
        q = dict(p)
        q["V"] = (p["V"] - base) * s
        out.append(q)
    return out


def main():
    a_path, b_path = sys.argv[1], sys.argv[2]
    prev = sys.argv[sys.argv.index("--apercu") + 1] if "--apercu" in sys.argv else None
    os.makedirs(OUT, exist_ok=True)
    pa = A.load_blend(a_path)
    pb = A.load_blend(b_path)
    sa, sb = slots(a_path), slots(b_path)
    imgs = []

    def bark_mat(p, cname, color_img, size=256):
        q = dict(p)
        q["mat"] = {"name": "Ecorce", "color": (1.0, 1.0, 1.0), "tex": color_img.convert("RGB").resize((size, size), Image.LANCZOS)}
        return q

    def leaf_mat(p, col, alpha, size=512):
        q = dict(p)
        q["mat"] = {"name": "Feuilles", "color": (1.0, 1.0, 1.0), "tex": rgba(col, alpha, size)}
        return q

    specs = []
    # Arbre simple (pack B) : version détaillée « Plane.044 »
    g = [p for p in pb if p["name"] == "Plane.044"]
    trunk = [p for p in g if p["mat"]["name"] == "Material"][0]
    leaves = [p for p in g if p["mat"]["name"] == "Material.001"][0]
    bark_col = sb["Material"][0] or sa["bark brown"][0]
    specs.append(("Arbre_Simple", [bark_mat(trunk, "Ecorce", bark_col), leaf_mat(leaves, sb["Material.001"][0], sb["Material.001"][1], 512)], 7.6))
    # Pack A : chêne (feuilles vertes), érable (feuilles jaune-vert), pin
    for nm, bark_name, leaf_name, out, keep, height in (("tree small joined", "bark brown", "leaf oak green", "Arbre_Chene", 0.3, 6.2),
                                                        ("tree oak joined", "Bark mossy", "leaf mapple yellow", "Arbre_Erable", 0.3, 6.6),
                                                        ("tree pine joined", "Bark rough", "leaf pine", "Arbre_Pin", 0.38, 7.2)):
        g = [p for p in pa if p["name"] == nm]
        b = [p for p in g if p["mat"]["name"] == bark_name][0]
        l = [p for p in g if p["mat"]["name"] == leaf_name][0]
        # Téléphones : écorce allégée, moins de cartes de feuilles mais plus grandes (même volume de feuillage)
        if A.tri_count([b]) > 560:
            b = A.decimate([b], 440)[0]
        l = thin_leaves(l, keep, 1.42, len(out))
        specs.append((out, [bark_mat(b, "Ecorce", sa[bark_name][0]), leaf_mat(l, sa[leaf_name][0], sa[leaf_name][1], 512)], height))
    for name, parts, height in specs:
        parts = normalize(parts, height)
        A.write_obj(parts, OUT, name, 512, "Arbre fourni par MATULA DESIGN (Blend Swap, licence CC0), allégé pour le jeu", False)
        lo, hi = A.bounds(parts)
        print(name, "triangles", A.tri_count(parts), "taille", np.round(hi - lo, 2))
        if prev:
            c = (lo + hi) / 2
            r = max(hi - lo)
            imgs.append(A.render(parts, c + np.array([r * 1.5, r * 0.35, r * 1.5]), c, W=360, H=360, fov=35, cull=False))
    if prev:
        os.makedirs(prev, exist_ok=True)
        A.sheet(imgs, 4, os.path.join(prev, "arbres.png"))


if __name__ == "__main__":
    main()
