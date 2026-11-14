#!/usr/bin/env python3
"""Conversion .blend (Blender 2.7x – 2.9x) -> maillages Python (et OBJ) : objets maillage avec leur matrice, miroirs
appliqués, normales (lisses/plates selon Blender), UV, matériaux (couleur de base du nœud Principled ou couleur du
matériau, texture image liée), images internes extraites.

    from blend2obj import extract
    parts = extract("fichier.blend", exclude=("Camera", "Plane"))
    # parts : liste de dict(name, material, V (n,3), N (n,3), UV (n,2), F (m,3)) en repère Blender (Z haut, avant -Y)
"""
import os
import struct
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from blendfile import BlendFile  # noqa: E402

CD_MLOOPUV = 16
MOD_MIRROR = 5


def _node_tree_info(bf, ntree_addr):
    """Couleur de base (Principled/Diffuse) et image reliée à la couleur de base (sinon première image) d'un arbre."""
    nt = bf.deref_struct(ntree_addr)
    if nt is None:
        return None, None
    nodes = bf.listbase(nt.get("nodes"))
    links = bf.listbase(nt.get("links"))
    by_addr = {n.addr: n for n in nodes}
    into = {}
    for lk in links:
        into[lk.get("tosock")] = lk.get("fromnode")

    def find_image(node, depth=0):
        if node is None or depth > 6:
            return None
        if (node.get("idname") or "") == "ShaderNodeTexImage" and node.get("id"):
            return bf.deref_struct(node.get("id"))
        for sock in bf.listbase(node.get("inputs")):
            src = into.get(sock.addr)
            if src:
                im = find_image(by_addr.get(src), depth + 1)
                if im is not None:
                    return im
        return None

    color, image = None, None
    for n in nodes:
        idname = n.get("idname") or ""
        if idname in ("ShaderNodeBsdfPrincipled", "ShaderNodeBsdfDiffuse", "ShaderNodeEmission"):
            for sock in bf.listbase(n.get("inputs")):
                if sock.get("name") in ("Base Color", "Color"):
                    raw = bf.block_bytes(sock.get("default_value")) if sock.get("default_value") else None
                    if raw and len(raw) >= 12 and color is None:
                        color = struct.unpack_from(bf.e + "3f", raw, 0)
                    src = into.get(sock.addr)
                    if src and image is None:
                        image = find_image(by_addr.get(src))
                    break
    if image is None:
        for n in nodes:
            if (n.get("idname") or "") == "ShaderNodeTexImage" and n.get("id"):
                im = bf.deref_struct(n.get("id"))
                nm = (im.get("name") or "").lower() if im is not None else ""
                if any(k in nm for k in ("base", "diff", "col", "albedo")) or image is None:
                    image = im
    return color, image


def _material_info(bf, ma_addr):
    ma = bf.deref_struct(ma_addr)
    if ma is None:
        return {"name": "Defaut", "color": (0.8, 0.8, 0.8), "image": None}
    name = ma.get("id").get("name")[2:]
    color = (ma.get("r"), ma.get("g"), ma.get("b"))
    image = None
    if ma.get("nodetree") and ma.get("use_nodes"):
        c, im = _node_tree_info(bf, ma.get("nodetree"))
        if c is not None:
            color = c
        image = im
    if image is None and ma.has("mtex"):
        for mt_addr in (ma.get("mtex") or []):
            mt = bf.deref_struct(mt_addr) if mt_addr else None
            if mt is None:
                continue
            tex = bf.deref_struct(mt.get("tex")) if mt.get("tex") else None
            if tex is not None and tex.get("ima"):
                image = bf.deref_struct(tex.get("ima"))
                break
    return {"name": name, "color": tuple(float(c) for c in color), "image": image}


def image_bytes(bf, im, search_dirs=()):
    """Contenu d'une image (empaquetée dans le .blend, ou fichier externe retrouvé par son nom)."""
    if im is None:
        return None, None
    path = im.get("name") or ""
    base = os.path.basename(path.replace("\\", "/"))
    pf = None
    if im.has("packedfile") and im.get("packedfile"):
        pf = bf.deref_struct(im.get("packedfile"))
    if pf is None and im.has("packedfiles"):
        for ipf in bf.listbase(im.get("packedfiles")):
            if ipf.get("packedfile"):
                pf = bf.deref_struct(ipf.get("packedfile"))
                break
    if pf is not None:
        size = pf.get("size")
        data = bf.block_bytes(pf.get("data"))
        if data:
            return base, data[:size]
    for d in search_dirs:
        p = os.path.join(d, base)
        if os.path.exists(p):
            return base, open(p, "rb").read()
    return base, None


def extract(path, exclude=(), apply_mirror=True):
    bf = BlendFile(path)
    obs = [bf.block_structs(b)[0] for b in bf.find_blocks("OB")]
    parts = []
    for o in obs:
        if o.get("type") != 1 or not o.get("data"):
            continue
        oname = o.get("id").get("name")[2:]
        if any(oname.startswith(e) for e in exclude):
            continue
        me = bf.deref_struct(o.get("data"))
        nv, np_, nl = me.get("totvert"), me.get("totpoly"), me.get("totloop")
        if nv == 0 or np_ == 0:
            continue
        # sommets
        mv_b = bf.deref(me.get("mvert"))
        MV = bf.block_structs(mv_b)
        co = np.array([m.get("co") for m in MV], float)
        ml = bf.block_structs(bf.deref(me.get("mloop")))
        loop_v = np.array([m.get("v") for m in ml], int)
        mp = bf.block_structs(bf.deref(me.get("mpoly")))
        polys = [(p.get("loopstart"), p.get("totloop"), p.get("mat_nr"), p.get("flag")) for p in mp]
        # UV (calque actif)
        uv = None
        ldata = me.get("ldata")
        layers = bf.deref(ldata.get("layers"))
        if layers is not None:
            for lay in bf.block_structs(layers):
                if lay.get("type") == CD_MLOOPUV and lay.get("data"):
                    ub = bf.deref(lay.get("data"))
                    if ub is not None:
                        uvs = bf.block_structs(ub)
                        uv = np.array([u.get("uv") for u in uvs], float)
                        break
        # matériaux (données du maillage ou de l'objet)
        totcol = me.get("totcol") or 0
        mats_me = bf.pointer_array(me.get("mat")) if me.get("mat") else []
        mats_ob = bf.pointer_array(o.get("mat")) if o.get("mat") else []
        matbits = bf.block_bytes(o.get("matbits")) if o.get("matbits") else None
        slots = []
        for k in range(max(1, totcol)):
            a = None
            if matbits and k < len(matbits) and matbits[k] and k < len(mats_ob):
                a = mats_ob[k]
            elif k < len(mats_me):
                a = mats_me[k]
            slots.append(_material_info(bf, a) if a else {"name": "Defaut", "color": (0.8, 0.8, 0.8), "image": None})
        M = np.array(o.get("obmat"), float).T   # obmat[colonne][ligne] -> matrice 4x4 (colonnes)
        # miroirs
        mirrors = []
        if apply_mirror:
            for md in bf.listbase(o.get("modifiers")):
                head = md.get("modifier") if md.has("modifier") else md
                if head is None or head.get("type") != MOD_MIRROR:
                    continue
                if (head.get("mode") or 0) & 1 == 0:   # désactivé dans la vue
                    continue
                flag = md.get("flag") or 0
                axes = [a for a, bit in ((0, 1 << 3), (1, 1 << 4), (2, 1 << 5)) if flag & bit]
                if not axes:
                    axes = [md.get("axis") or 0]
                mirrors.append(axes)
        # copies (miroir) : liste de matrices d'échelle locales
        scales = [np.ones(3)]
        for axes in mirrors:
            for a in axes:
                new = []
                for s in scales:
                    s2 = s.copy()
                    s2[a] *= -1
                    new.append(s2)
                scales = scales + new
        by_mat = {}
        for (ls, tl, mat_nr, flag) in polys:
            by_mat.setdefault(min(mat_nr, len(slots) - 1), []).append((ls, tl, flag))
        for mi, plist in by_mat.items():
            for sc in scales:
                flip = np.prod(sc) < 0
                V, N, UVo, F = [], [], [], []
                # normales lisses par sommet (moyenne des faces lisses)
                pts_local = co * sc
                pw = (np.c_[pts_local, np.ones(len(pts_local))] @ M.T)[:, :3]
                vn = np.zeros((nv, 3))
                fnorms = []
                for (ls, tl, flag) in plist:
                    idx = loop_v[ls:ls + tl]
                    P = pw[idx]
                    n = np.zeros(3)
                    for i in range(tl):
                        n += np.cross(P[i], P[(i + 1) % tl])
                    if flip:
                        n = -n
                    fnorms.append(n)
                    if flag & 1:
                        vn[idx] += n
                for (ls, tl, flag), fn in zip(plist, fnorms):
                    idx = loop_v[ls:ls + tl]
                    order = list(range(tl))
                    if flip:
                        order = order[::-1]
                    base = len(V)
                    for i in order:
                        V.append(pw[idx[i]])
                        nn = vn[idx[i]] if flag & 1 else fn
                        L = np.linalg.norm(nn)
                        N.append(nn / L if L > 1e-12 else np.array([0, 0, 1.0]))
                        UVo.append(uv[ls + i] if uv is not None else (0.0, 0.0))
                    for i in range(1, tl - 1):
                        F.append((base, base + i, base + i + 1))
                parts.append({"name": oname, "material": slots[mi], "V": np.array(V), "N": np.array(N),
                              "UV": np.array(UVo, float), "F": np.array(F, int)})
    return bf, parts


def to_obj_space(V):
    """Repère Blender (Z haut, avant -Y) -> repère OBJ (Y haut, avant +Z, main droite)."""
    V = np.asarray(V, float)
    return np.c_[V[:, 0], V[:, 2], -V[:, 1]]
