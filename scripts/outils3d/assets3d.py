#!/usr/bin/env python3
"""Boîte à outils de conversion des modèles 3D reçus (FBX binaire, glTF/GLB, .blend) vers des OBJ légers pour Unity.

Une « pièce » est un dict : name, mat (dict name/color/tex), V (n,3), N (n,3), UV (n,2), F (m,3) — triangles,
sommets non partagés entre matériaux. Repère de travail : main droite, Y haut (comme OBJ). Unity retourne l'axe X.
"""
import io
import json
import math
import os
import struct
import sys
import zlib

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))


# ------------------------------------------------------------------------------------------------------------------
# glTF / GLB
# ------------------------------------------------------------------------------------------------------------------
def _quat_mat(q):
    x, y, z, w = q
    return np.array([[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
                     [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
                     [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])


def load_glb(path):
    d = open(path, "rb").read()
    jl = struct.unpack_from("<I", d, 12)[0]
    js = json.loads(d[20:20 + jl])
    off = 20 + jl
    bl = struct.unpack_from("<I", d, off)[0]
    bin_ = d[off + 8:off + 8 + bl]
    CT = {5120: "b", 5121: "B", 5122: "h", 5123: "H", 5125: "I", 5126: "f"}
    NC = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}

    def acc(i):
        a = js["accessors"][i]
        bv = js["bufferViews"][a["bufferView"]]
        n, c = a["count"], NC[a["type"]]
        fmt = CT[a["componentType"]]
        size = struct.calcsize(fmt)
        stride = bv.get("byteStride", size * c)
        start = bv.get("byteOffset", 0) + a.get("byteOffset", 0)
        raw = np.frombuffer(bin_, dtype=np.dtype(fmt).newbyteorder("<"), count=(n - 1) * stride // size + c, offset=start)
        arr = np.lib.stride_tricks.as_strided(raw, shape=(n, c), strides=(stride, size)).astype(float)
        if a.get("normalized"):
            arr /= {5120: 127.0, 5121: 255.0, 5122: 32767.0, 5123: 65535.0}.get(a["componentType"], 1.0)
        return arr

    images = []
    for im in js.get("images", []):
        bv = js["bufferViews"][im["bufferView"]]
        raw = bin_[bv.get("byteOffset", 0):bv.get("byteOffset", 0) + bv["byteLength"]]
        images.append(Image.open(io.BytesIO(raw)).convert("RGBA"))
    mats = []
    for m in js.get("materials", []):
        pbr = m.get("pbrMetallicRoughness", {})
        col = pbr.get("baseColorFactor", [1, 1, 1, 1])
        tex = None
        if "baseColorTexture" in pbr:
            ti = js["textures"][pbr["baseColorTexture"]["index"]]
            tex = images[ti["source"]] if "source" in ti else None
        mats.append({"name": m.get("name", "Mat"), "color": tuple(col[:3]), "tex": tex, "alpha": m.get("alphaMode", "OPAQUE")})
    parts = []

    def walk(ni, parent):
        n = js["nodes"][ni]
        if "matrix" in n:
            M = np.array(n["matrix"], float).reshape(4, 4).T
        else:
            M = np.eye(4)
            S = np.diag(n.get("scale", [1, 1, 1]) + [1])
            R = np.eye(4)
            R[:3, :3] = _quat_mat(n.get("rotation", [0, 0, 0, 1]))
            T = np.eye(4)
            T[:3, 3] = n.get("translation", [0, 0, 0])
            M = T @ R @ S
        W = parent @ M
        if "mesh" in n:
            for pr in js["meshes"][n["mesh"]]["primitives"]:
                at = pr["attributes"]
                P = acc(at["POSITION"])
                Nn = acc(at["NORMAL"]) if "NORMAL" in at else None
                UV = acc(at["TEXCOORD_0"]) if "TEXCOORD_0" in at else np.zeros((len(P), 2))
                idx = acc(pr["indices"]).astype(int).reshape(-1) if "indices" in pr else np.arange(len(P))
                F = idx.reshape(-1, 3)
                Pw = (np.c_[P, np.ones(len(P))] @ W.T)[:, :3]
                if Nn is None:
                    Nn = np.zeros_like(P)
                Nw = Nn @ np.linalg.inv(W[:3, :3])
                Nw /= np.maximum(1e-12, np.linalg.norm(Nw, axis=1))[:, None]
                if np.linalg.det(W[:3, :3]) < 0:
                    F = F[:, ::-1]
                mat = mats[pr["material"]] if "material" in pr else {"name": "Defaut", "color": (0.8, 0.8, 0.8), "tex": None}
                parts.append({"name": n.get("name", "Objet"), "mat": mat, "V": Pw[F.reshape(-1)], "N": Nw[F.reshape(-1)],
                              "UV": np.c_[UV[:, 0], 1 - UV[:, 1]][F.reshape(-1)],
                              "F": np.arange(len(F) * 3).reshape(-1, 3)})
        for c in n.get("children", []):
            walk(c, W)

    for s in js["scenes"][js.get("scene", 0)]["nodes"]:
        walk(s, np.eye(4))
    return parts


# ------------------------------------------------------------------------------------------------------------------
# FBX binaire (hiérarchie des modèles, transformations, géométrie, UV, normales, matériaux)
# ------------------------------------------------------------------------------------------------------------------
def _euler_xyz(rx, ry, rz):
    rx, ry, rz = (math.radians(a) for a in (rx, ry, rz))
    cx, sx, cy, sy, cz, sz = math.cos(rx), math.sin(rx), math.cos(ry), math.sin(ry), math.cos(rz), math.sin(rz)
    Rx = np.array([[1, 0, 0], [0, cx, -sx], [0, sx, cx]])
    Ry = np.array([[cy, 0, sy], [0, 1, 0], [-sy, 0, cy]])
    Rz = np.array([[cz, -sz, 0], [sz, cz, 0], [0, 0, 1]])
    return Rz @ Ry @ Rx      # ordre XYZ (X d'abord)


def _h(R=None, t=None, s=None):
    M = np.eye(4)
    if R is not None:
        M[:3, :3] = R
    if s is not None:
        M[:3, :3] = M[:3, :3] @ np.diag(s)
    if t is not None:
        M[:3, 3] = t
    return M


def load_fbx(path, tex_dirs=()):
    from fbxbin import read, find
    nodes = read(path)
    objs = [n for n in nodes if n["name"] == "Objects"][0]
    conns = [n for n in nodes if n["name"] == "Connections"][0]
    gs = [n for n in nodes if n["name"] == "GlobalSettings"]
    byid = {k["props"][0]: k for k in objs["kids"]}
    children = {}
    parent_of = {}
    for c in conns["kids"]:
        kind, a, b = c["props"][0], c["props"][1], c["props"][2]
        children.setdefault(b, []).append((a, kind, c["props"][3] if len(c["props"]) > 3 else None))
        if kind == "OO":
            parent_of.setdefault(a, []).append(b)

    def props70(node):
        p = {}
        P = find(node, "Properties70")
        if P:
            for pp in P["kids"]:
                p[pp["props"][0]] = pp["props"][4:]
        return p

    def local(model):
        p = props70(model)
        T = p.get("Lcl Translation", [0, 0, 0])
        R = p.get("Lcl Rotation", [0, 0, 0])
        S = p.get("Lcl Scaling", [1, 1, 1])
        pre = p.get("PreRotation", [0, 0, 0])
        post = p.get("PostRotation", [0, 0, 0])
        Rp = p.get("RotationPivot", [0, 0, 0])
        Roff = p.get("RotationOffset", [0, 0, 0])
        Sp = p.get("ScalingPivot", [0, 0, 0])
        Soff = p.get("ScalingOffset", [0, 0, 0])
        M = (_h(t=T) @ _h(t=Roff) @ _h(t=Rp) @ _h(R=_euler_xyz(*pre)) @ _h(R=_euler_xyz(*R)) @
             np.linalg.inv(_h(R=_euler_xyz(*post))) @ np.linalg.inv(_h(t=Rp)) @ _h(t=Soff) @ _h(t=Sp) @
             _h(s=S) @ np.linalg.inv(_h(t=Sp)))
        G = _h(t=p.get("GeometricTranslation", [0, 0, 0])) @ _h(R=_euler_xyz(*p.get("GeometricRotation", [0, 0, 0]))) @ \
            _h(s=p.get("GeometricScaling", [1, 1, 1]))
        return M, G

    world_cache = {}

    def world(mid):
        if mid in world_cache:
            return world_cache[mid]
        m = byid.get(mid)
        if m is None or m["name"] != "Model":
            return np.eye(4)
        L, _ = local(m)
        par = [x for x in parent_of.get(mid, []) if x in byid and byid[x]["name"] == "Model"]
        W = (world(par[0]) if par else np.eye(4)) @ L
        world_cache[mid] = W
        return W

    unit = 1.0
    if gs:
        p = props70(gs[0])
        unit = float(p.get("UnitScaleFactor", [1.0])[0]) / 100.0     # mètres par unité
    # textures : matériau -> image diffuse
    tex_of_mat = {}
    for mid, lst in children.items():
        if mid in byid and byid[mid]["name"] == "Material":
            for (a, kind, pname) in lst:
                if a in byid and byid[a]["name"] == "Texture" and (pname is None or "Diffuse" in str(pname) or "Color" in str(pname)):
                    t = byid[a]
                    fn = find(t, "RelativeFilename") or find(t, "FileName")
                    if fn:
                        tex_of_mat[mid] = fn["props"][0].replace("\\", "/").split("/")[-1]
    parts = []
    for gid, g in byid.items():
        if g["name"] != "Geometry" or len(g["props"]) < 3 or g["props"][2] != "Mesh":
            continue
        Vn = find(g, "Vertices")
        Pn = find(g, "PolygonVertexIndex")
        if Vn is None or Pn is None:
            continue
        V = np.array(Vn["props"][0], float).reshape(-1, 3)
        pvi = np.array(Pn["props"][0], int)
        models = [b for b in parent_of.get(gid, []) if b in byid and byid[b]["name"] == "Model"]
        if not models:
            continue
        mid = models[0]
        W = world(mid)
        _, G = local(byid[mid])
        W = W @ G
        mats = [a for (a, kind, _) in children.get(mid, []) if a in byid and byid[a]["name"] == "Material"]
        # normales
        Nl = find(g, "LayerElementNormal")
        normals, nmap, nref, nidx = None, None, None, None
        if Nl:
            normals = np.array(find(Nl, "Normals")["props"][0], float).reshape(-1, 3)
            nmap = find(Nl, "MappingInformationType")["props"][0]
            nref = find(Nl, "ReferenceInformationType")["props"][0]
            ni = find(Nl, "NormalsIndex")
            nidx = np.array(ni["props"][0], int) if ni else None
        Ul = find(g, "LayerElementUV")
        uvs, umap, uref, uidx = None, None, None, None
        if Ul:
            uvs = np.array(find(Ul, "UV")["props"][0], float).reshape(-1, 2)
            umap = find(Ul, "MappingInformationType")["props"][0]
            uref = find(Ul, "ReferenceInformationType")["props"][0]
            ui = find(Ul, "UVIndex")
            uidx = np.array(ui["props"][0], int) if ui else None
        Ml = find(g, "LayerElementMaterial")
        mat_idx = None
        if Ml:
            mm = find(Ml, "Materials")["props"][0]
            mmap = find(Ml, "MappingInformationType")["props"][0]
            mat_idx = mm if mmap == "ByPolygon" else None
            mat_all = mm[0] if mmap == "AllSame" and len(mm) else 0
        neg = pvi < 0
        vidx = np.where(neg, -pvi - 1, pvi)
        ends = np.where(neg)[0]
        starts = np.r_[0, ends[:-1] + 1]
        sizes = ends - starts + 1
        cnt = np.maximum(0, sizes - 2)
        total = int(cnt.sum())
        tri_poly = np.repeat(np.arange(len(sizes)), cnt)
        inner = np.arange(total) - np.repeat(np.cumsum(cnt) - cnt, cnt) + 1
        c0 = starts[tri_poly]
        corners = np.stack([c0, c0 + inner, c0 + inner + 1], 1)
        flip = np.linalg.det(W[:3, :3]) < 0
        if flip:
            corners = corners[:, ::-1]
        cv = vidx[corners]

        def layer(arr, mapping, ref, index):
            if arr is None:
                return None
            if mapping == "ByPolygonVertex":
                j = index[corners] if (ref == "IndexToDirect" and index is not None) else corners
            elif mapping in ("ByControlPoint", "ByVertice", "ByVertex"):
                j = index[cv] if (ref == "IndexToDirect" and index is not None) else cv
            elif mapping == "ByPolygon":
                j = np.repeat(index[tri_poly] if (ref == "IndexToDirect" and index is not None) else tri_poly, 3).reshape(-1, 3)
            else:
                j = np.zeros_like(corners)
            return arr[j]
        Pw = (np.c_[V, np.ones(len(V))] @ W.T)[:, :3]
        Vt = Pw[cv]
        Nt = layer(normals, nmap, nref, nidx)
        if Nt is None:
            Nt = np.zeros_like(Vt)
        Nt = Nt @ np.linalg.inv(W[:3, :3])
        Ut = layer(uvs, umap, uref, uidx)
        if Ut is None:
            Ut = np.zeros(corners.shape + (2,))
        if mat_idx is not None:
            tmat = np.array(mat_idx, int)[tri_poly]
        else:
            tmat = np.full(len(tri_poly), mat_all if Ml else 0)
        for mi in np.unique(tmat):
            sel = tmat == mi
            Vo = Vt[sel].reshape(-1, 3)
            No = Nt[sel].reshape(-1, 3)
            Uo = Ut[sel].reshape(-1, 2)
            Uo = np.c_[Uo[:, 0], 1 - Uo[:, 1]]
            mid_mat = mats[mi] if mi < len(mats) else None
            mname, col, tex = "Defaut", (0.8, 0.8, 0.8), None
            if mid_mat is not None:
                m = byid[mid_mat]
                mname = m["props"][1].split("\x00")[0]
                p = props70(m)
                c = p.get("DiffuseColor") or p.get("Diffuse")
                if c:
                    col = tuple(float(x) for x in c[:3])
                tn = tex_of_mat.get(mid_mat)
                if tn:
                    for dd in tex_dirs:
                        cand = os.path.join(dd, tn)
                        if os.path.exists(cand):
                            tex = Image.open(cand).convert("RGBA")
                            break
            nl = np.linalg.norm(No, axis=1)
            No = No / np.maximum(1e-12, nl)[:, None]
            parts.append({"name": byid[mid]["props"][1].split("\x00")[0], "mat": {"name": mname, "color": col, "tex": tex},
                          "V": Vo * unit, "N": No, "UV": Uo, "F": np.arange(len(Vo)).reshape(-1, 3)})
    return parts


# ------------------------------------------------------------------------------------------------------------------
# .blend
# ------------------------------------------------------------------------------------------------------------------
def load_blend(path, exclude=(), search_dirs=()):
    from blend2obj import extract, to_obj_space, image_bytes
    bf, raw = extract(path, exclude=exclude)
    cache = {}
    parts = []
    for p in raw:
        m = p["material"]
        tex = None
        if m["image"] is not None:
            key = m["image"].addr
            if key not in cache:
                name, data = image_bytes(bf, m["image"], search_dirs)
                im = None
                if data:
                    try:
                        im = Image.open(io.BytesIO(data)).convert("RGBA")
                    except Exception:
                        im = None
                cache[key] = im
            tex = cache[key]
        V = to_obj_space(p["V"])
        N = to_obj_space(p["N"])
        F = p["F"]
        parts.append({"name": p["name"], "mat": {"name": m["name"], "color": m["color"], "tex": tex},
                      "V": V[F.reshape(-1)], "N": N[F.reshape(-1)], "UV": np.c_[p["UV"][:, 0], 1 - p["UV"][:, 1]][F.reshape(-1)],
                      "F": np.arange(len(F) * 3).reshape(-1, 3)})
    return parts


# ------------------------------------------------------------------------------------------------------------------
# Transformations, statistiques
# ------------------------------------------------------------------------------------------------------------------
def bounds(parts):
    A = np.concatenate([p["V"] for p in parts])
    return A.min(0), A.max(0)


def transform(parts, M3=None, scale=1.0, offset=(0, 0, 0)):
    """Applique v' = scale * (M3 @ v) + offset à toutes les pièces (normales tournées, faces retournées si miroir)."""
    M3 = np.eye(3) if M3 is None else np.asarray(M3, float)
    flip = np.linalg.det(M3) < 0
    for p in parts:
        p["V"] = scale * (p["V"] @ M3.T) + np.asarray(offset, float)
        Nn = p["N"] @ np.linalg.inv(M3)
        p["N"] = Nn / np.maximum(1e-12, np.linalg.norm(Nn, axis=1))[:, None]
        if flip:
            p["F"] = p["F"][:, ::-1]
    return parts


def rot_y(deg):
    a = math.radians(deg)
    return np.array([[math.cos(a), 0, math.sin(a)], [0, 1, 0], [-math.sin(a), 0, math.cos(a)]])


def rot_x(deg):
    a = math.radians(deg)
    return np.array([[1, 0, 0], [0, math.cos(a), -math.sin(a)], [0, math.sin(a), math.cos(a)]])


def tri_count(parts):
    return sum(len(p["F"]) for p in parts)


# ------------------------------------------------------------------------------------------------------------------
# Simplification par regroupement de sommets (grille), normales recalculées (angle de lissage)
# ------------------------------------------------------------------------------------------------------------------
def smooth_normals(V, F, angle=55.0):
    """Normales par coin de face, lissées entre faces voisines dont l'angle est inférieur au seuil."""
    if len(F) == 0:
        return np.zeros((0, 3))
    P = V[F]
    fn = np.cross(P[:, 1] - P[:, 0], P[:, 2] - P[:, 0])
    area = np.linalg.norm(fn, axis=1)
    fnu = fn / np.maximum(1e-20, area)[:, None]
    # sommets identiques par position
    key = np.round(V / 1e-5).astype(np.int64)
    _, vid = np.unique(key, axis=0, return_inverse=True)
    vid = vid.reshape(-1)
    corner_v = vid[F.reshape(-1)]
    corner_f = np.repeat(np.arange(len(F)), 3)
    acc = np.zeros((vid.max() + 1, 3))
    np.add.at(acc, corner_v, fn[corner_f])
    vnorm = acc / np.maximum(1e-20, np.linalg.norm(acc, axis=1))[:, None]
    cosmax = math.cos(math.radians(angle))
    N = vnorm[corner_v]
    use_face = (N * fnu[corner_f]).sum(1) < cosmax
    N[use_face] = fnu[corner_f][use_face]
    return N.reshape(-1, 3)


def cluster_part(p, cell):
    """Regroupe les sommets d'une pièce sur une grille de pas « cell » : triangles dégénérés supprimés."""
    V, F, UV = p["V"], p["F"], p["UV"]
    key = np.floor(V / cell).astype(np.int64)
    uniq, inv = np.unique(key, axis=0, return_inverse=True)
    inv = inv.reshape(-1)
    nU = len(uniq)
    rep = np.zeros((nU, 3))
    cnt = np.zeros(nU)
    np.add.at(rep, inv, V)
    np.add.at(cnt, inv, 1)
    rep /= cnt[:, None]
    uvr = np.zeros((nU, 2))
    np.add.at(uvr, inv, UV)
    uvr /= cnt[:, None]
    Fi = inv[F]
    ok = (Fi[:, 0] != Fi[:, 1]) & (Fi[:, 1] != Fi[:, 2]) & (Fi[:, 0] != Fi[:, 2])
    Fi = Fi[ok]
    # doublons
    srt = np.sort(Fi, axis=1)
    _, first = np.unique(srt, axis=0, return_index=True)
    Fi = Fi[np.sort(first)]
    # UV : on garde l'UV du coin d'origine (évite les mélanges entre îlots)
    origF = F[ok][np.sort(first)]
    Vn = rep[Fi.reshape(-1)]
    Un = UV[origF.reshape(-1)]
    Fn = np.arange(len(Vn)).reshape(-1, 3)
    q = dict(p)
    q["V"], q["UV"], q["F"] = Vn, Un, Fn
    q["N"] = smooth_normals(Vn, Fn)
    return q


def decimate(parts, target_tris, min_cell=0.0005, keep_small=0, keep_names=()):
    """Simplifie l'ensemble jusqu'à environ target_tris triangles (recherche du pas de grille). Les pièces de moins de
    keep_small triangles (tiges, petites pièces fines) sont gardées telles quelles si le budget le permet."""
    if tri_count(parts) <= target_tris:
        return parts
    if keep_small > 0 or keep_names:
        def petit(p):
            return len(p["F"]) < keep_small or any(k in p["name"] for k in keep_names)
        small = [p for p in parts if petit(p)]
        big = [p for p in parts if not petit(p)]
        ns = tri_count(small)
        if big and ns < target_tris * 0.6:
            return small + decimate(big, target_tris - ns, min_cell, 0)
    lo, hi = min_cell, 0.2
    best = None
    for _ in range(18):
        mid = math.sqrt(lo * hi)
        res = [q for q in (cluster_part(p, mid) for p in parts) if len(q["F"]) > 0]
        n = tri_count(res)
        if n > target_tris:
            lo = mid
        else:
            hi = mid
            best = res
            if target_tris - n < 0.06 * target_tris:
                break
    return best if best is not None else [q for q in (cluster_part(p, hi) for p in parts) if len(q["F"]) > 0]


def renormal(parts, angle=55.0):
    for p in parts:
        p["N"] = smooth_normals(p["V"], p["F"], angle)
    return parts


def merge_by_material(parts, group_of=lambda p: p["name"]):
    """Fusionne les pièces de même (groupe, matériau)."""
    out = {}
    for p in parts:
        k = (group_of(p), p["mat"]["name"])
        if k not in out:
            out[k] = {"name": k[0], "mat": p["mat"], "V": [], "N": [], "UV": [], "F": [], "n": 0}
        o = out[k]
        o["V"].append(p["V"])
        o["N"].append(p["N"])
        o["UV"].append(p["UV"])
        o["F"].append(p["F"] + o["n"])
        o["n"] += len(p["V"])
    res = []
    for o in out.values():
        res.append({"name": o["name"], "mat": o["mat"], "V": np.concatenate(o["V"]), "N": np.concatenate(o["N"]),
                    "UV": np.concatenate(o["UV"]), "F": np.concatenate(o["F"])})
    return res


def weld(p, eps=1e-5):
    """Partage les sommets identiques (position, normale, UV) : fichier plus léger."""
    key = np.c_[np.round(p["V"] / eps), np.round(p["N"] * 1000), np.round(p["UV"] * 4096)].astype(np.int64)
    _, idx, inv = np.unique(key, axis=0, return_index=True, return_inverse=True)
    inv = inv.reshape(-1)
    q = dict(p)
    q["V"], q["N"], q["UV"] = p["V"][idx], p["N"][idx], p["UV"][idx]
    q["F"] = inv[p["F"]]
    return q


# ------------------------------------------------------------------------------------------------------------------
# Écriture OBJ + MTL + textures
# ------------------------------------------------------------------------------------------------------------------
def write_obj(parts, out_dir, name, tex_size=1024, comment="", jpg=True):
    """Écrit name.obj / name.mtl ; les textures des matériaux sont enregistrées à côté (name_<mat>.jpg|png).
    Une pièce = un objet OBJ nommé par p['name'] (un seul matériau par objet)."""
    os.makedirs(out_dir, exist_ok=True)
    mats = {}
    for p in parts:
        mats.setdefault(p["mat"]["name"], p["mat"])
    with open(os.path.join(out_dir, name + ".mtl"), "w") as f:
        for mn, m in mats.items():
            c = m["color"]
            f.write("newmtl %s\nKd %.4f %.4f %.4f\nKa 0 0 0\nKs 0.05 0.05 0.05\nNs 10\nd 1\nillum 2\n" % (mn, c[0], c[1], c[2]))
            if m.get("tex") is not None:
                im = m["tex"]
                if max(im.size) > tex_size:
                    r = tex_size / max(im.size)
                    im = im.resize((max(4, int(im.size[0] * r)), max(4, int(im.size[1] * r))), Image.LANCZOS)
                has_alpha = im.mode == "RGBA" and np.asarray(im)[..., 3].min() < 250
                safe = "".join(ch if ch.isalnum() else "_" for ch in mn)
                fn = "%s_%s.%s" % (name, safe, "png" if (has_alpha or not jpg) else "jpg")
                if fn.endswith("jpg"):
                    im.convert("RGB").save(os.path.join(out_dir, fn), quality=88)
                else:
                    im.save(os.path.join(out_dir, fn))
                m["_file"] = fn
                f.write("map_Kd %s\n" % fn)
            f.write("\n")
    nv = nt = 0
    with open(os.path.join(out_dir, name + ".obj"), "w") as f:
        f.write("# %s\n# Mètres, Y vers le haut, avant vers +Z (Unity retourne X).\nmtllib %s.mtl\n" % (comment, name))
        base = 1
        for p in parts:
            q = weld(p)
            f.write("o %s\ng %s\nusemtl %s\n" % (q["name"], q["name"], q["mat"]["name"]))
            for v in q["V"]:
                f.write("v %.4f %.4f %.4f\n" % tuple(v))
            for t in q["UV"]:
                f.write("vt %.4f %.4f\n" % (t[0], 1 - t[1]))
            for n in q["N"]:
                f.write("vn %.3f %.3f %.3f\n" % tuple(n))
            for tri in q["F"]:
                a, b, c = (base + int(k) for k in tri)
                f.write("f %d/%d/%d %d/%d/%d %d/%d/%d\n" % (a, a, a, b, b, b, c, c, c))
            base += len(q["V"])
            nv += len(q["V"])
            nt += len(q["F"])
    return nv, nt


# ------------------------------------------------------------------------------------------------------------------
# Aperçu (rendu logiciel texturé, faces arrière masquées)
# ------------------------------------------------------------------------------------------------------------------
def render(parts, eye, target, W=640, H=480, fov=30, bg=(0.88, 0.9, 0.92), ss=2, light=(0.45, 0.8, 0.55), cull=True):
    eye, target = np.asarray(eye, float), np.asarray(target, float)
    fwd = target - eye
    fwd /= np.linalg.norm(fwd)
    upv = np.array([0, 1.0, 0]) if abs(fwd[1]) < 0.97 else np.array([0, 0, 1.0])
    right = np.cross(fwd, upv)
    right /= np.linalg.norm(right)
    up = np.cross(right, fwd)
    f = 1.0 / math.tan(math.radians(fov / 2))
    w, h = W * ss, H * ss
    img = np.ones((h, w, 3)) * np.array(bg)
    zb = np.full((h, w), np.inf)
    lum = np.asarray(light, float)
    lum /= np.linalg.norm(lum)
    for p in parts:
        V, N, UV, F = p["V"], p["N"], p["UV"], p["F"]
        tex = p["mat"].get("tex")
        T = np.asarray(tex.convert("RGB"), float) / 255.0 if tex is not None else None
        col = np.array(p["mat"]["color"], float)
        rel = V - eye
        zc = rel @ fwd
        xs = (rel @ right) / np.maximum(1e-6, zc) * f * h / 2 + w / 2
        ys = h / 2 - (rel @ up) / np.maximum(1e-6, zc) * f * h / 2
        P = V[F]
        fn = np.cross(P[:, 1] - P[:, 0], P[:, 2] - P[:, 0])
        vis = (np.einsum("ij,ij->i", fn, eye - P[:, 0]) > 0) if cull else np.ones(len(F), bool)
        vis &= (zc[F] > 0.05).all(1)
        for ti in np.where(vis)[0]:
            tri = F[ti]
            X, Y, Z = xs[tri], ys[tri], zc[tri]
            x0, x1 = int(max(0, X.min())), int(min(w - 1, X.max() + 1))
            y0, y1 = int(max(0, Y.min())), int(min(h - 1, Y.max() + 1))
            if x0 > x1 or y0 > y1:
                continue
            gx, gy = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
            den = (Y[1] - Y[2]) * (X[0] - X[2]) + (X[2] - X[1]) * (Y[0] - Y[2])
            if abs(den) < 1e-9:
                continue
            l1 = ((Y[1] - Y[2]) * (gx - X[2]) + (X[2] - X[1]) * (gy - Y[2])) / den
            l2 = ((Y[2] - Y[0]) * (gx - X[2]) + (X[0] - X[2]) * (gy - Y[2])) / den
            l3 = 1 - l1 - l2
            ins = (l1 >= -1e-6) & (l2 >= -1e-6) & (l3 >= -1e-6)
            if not ins.any():
                continue
            z = 1.0 / (l1 / Z[0] + l2 / Z[1] + l3 / Z[2])
            sub = zb[y0:y1 + 1, x0:x1 + 1]
            upd = ins & (z < sub)
            if not upd.any():
                continue
            nrm = N[tri].mean(0)
            nrm /= max(1e-9, np.linalg.norm(nrm))
            if np.dot(nrm, eye - P[ti, 0]) < 0:
                nrm = -nrm
            shade = 0.42 + 0.58 * max(0.0, float(np.dot(nrm, lum)))
            if T is not None:
                w0, w1, w2 = l1 / Z[0] * z, l2 / Z[1] * z, l3 / Z[2] * z
                uv = UV[tri]
                u = w0 * uv[0, 0] + w1 * uv[1, 0] + w2 * uv[2, 0]
                v = w0 * uv[0, 1] + w1 * uv[1, 1] + w2 * uv[2, 1]
                th, tw = T.shape[:2]
                tx = np.clip(((u % 1) * tw).astype(int), 0, tw - 1)
                ty = np.clip(((v % 1) * th).astype(int), 0, th - 1)
                c = T[ty, tx] * col
            else:
                c = np.broadcast_to(col, upd.shape + (3,))
            sub[upd] = z[upd]
            img[y0:y1 + 1, x0:x1 + 1][upd] = np.clip(c[upd] * shade, 0, 1)
    im = Image.fromarray((img * 255).astype(np.uint8))
    return im.resize((W, H), Image.LANCZOS) if ss > 1 else im


def sheet(images, cols, out):
    w = max(i.width for i in images)
    h = max(i.height for i in images)
    rows = (len(images) + cols - 1) // cols
    o = Image.new("RGB", (w * cols, h * rows), (255, 255, 255))
    for k, i in enumerate(images):
        o.paste(i, ((k % cols) * w, (k // cols) * h))
    o.save(out)
    return out


# ------------------------------------------------------------------------------------------------------------------
# Découpes et sélections de triangles
# ------------------------------------------------------------------------------------------------------------------
def tri_subset(p, mask, name=None, mat=None):
    """Nouvelle pièce avec les triangles retenus (mask booléen par triangle)."""
    F = p["F"][mask]
    idx = F.reshape(-1)
    q = {"name": name or p["name"], "mat": mat or p["mat"], "V": p["V"][idx], "N": p["N"][idx], "UV": p["UV"][idx],
         "F": np.arange(len(idx)).reshape(-1, 3)}
    return q


def split_mask(p, mask, name_a, name_b, mat_a=None, mat_b=None):
    out = []
    if mask.any():
        out.append(tri_subset(p, mask, name_a, mat_a))
    if (~mask).any():
        out.append(tri_subset(p, ~mask, name_b, mat_b))
    return out


def centroids(p):
    return p["V"][p["F"]].mean(1)


def cut_plane(p, axis, value):
    """Coupe les triangles d'une pièce par le plan coordonnée[axis] = value : renvoie (côté <, côté >=)."""
    V, N, UV, F = p["V"], p["N"], p["UV"], p["F"]
    neg = {"V": [], "N": [], "UV": []}
    pos = {"V": [], "N": [], "UV": []}

    def emit(dst, pts):
        for k in range(1, len(pts) - 1):
            for q in (pts[0], pts[k], pts[k + 1]):
                dst["V"].append(q[0]); dst["N"].append(q[1]); dst["UV"].append(q[2])

    for tri in F:
        pts = [(V[i], N[i], UV[i]) for i in tri]
        d = [q[0][axis] - value for q in pts]
        if all(x < 0 for x in d):
            emit(neg, pts); continue
        if all(x >= 0 for x in d):
            emit(pos, pts); continue
        a_poly, b_poly = [], []
        for k in range(3):
            q0, q1 = pts[k], pts[(k + 1) % 3]
            d0, d1 = d[k], d[(k + 1) % 3]
            (a_poly if d0 < 0 else b_poly).append(q0)
            if (d0 < 0) != (d1 < 0):
                t = d0 / (d0 - d1)
                v = q0[0] + (q1[0] - q0[0]) * t
                n = q0[1] + (q1[1] - q0[1]) * t
                uv = q0[2] + (q1[2] - q0[2]) * t
                q = (v, n, uv)
                a_poly.append(q); b_poly.append(q)
        if len(a_poly) >= 3: emit(neg, a_poly)
        if len(b_poly) >= 3: emit(pos, b_poly)

    def mk(d):
        if not d["V"]:
            return None
        Vv = np.array(d["V"]); Nn = np.array(d["N"]); Uu = np.array(d["UV"])
        return {"name": p["name"], "mat": p["mat"], "V": Vv, "N": Nn, "UV": Uu, "F": np.arange(len(Vv)).reshape(-1, 3)}
    return mk(neg), mk(pos)


def recentre(parts, ground=True, center_xz=True):
    mn, mx = bounds(parts)
    off = np.zeros(3)
    if center_xz:
        off[0] = -(mn[0] + mx[0]) / 2; off[2] = -(mn[2] + mx[2]) / 2
    if ground:
        off[1] = -mn[1]
    for p in parts:
        p["V"] = p["V"] + off
    return off


def set_color(parts, table, default=None):
    for p in parts:
        n = p["mat"]["name"]
        p["mat"] = dict(p["mat"])
        if n in table:
            p["mat"]["color"] = table[n]
        elif default is not None:
            p["mat"]["color"] = default
    return parts


def rename_mat(parts, mapping):
    """mapping : ancien nom -> (nouveau nom, couleur) ; les matériaux de même nouveau nom sont partagés."""
    cache = {}
    for p in parts:
        n = p["mat"]["name"]
        if n in mapping:
            nn, col = mapping[n]
            if nn not in cache:
                cache[nn] = {"name": nn, "color": col, "tex": None}
            p["mat"] = cache[nn]
    return parts
