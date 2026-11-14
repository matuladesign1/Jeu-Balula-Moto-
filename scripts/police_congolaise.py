#!/usr/bin/env python3
"""BALULA MOTO — habille l'avatar policier Rocketbox (Police_Male_01, licence MIT) en agent de la police congolaise,
d'après la photo de référence et les indications de MATULA DESIGN (tenue bleu de nuit, inscription « POLICE » sur la
poche gauche, béret noir, ceinturon et rangers noirs).

Ce que fait le programme (à lancer une seule fois, après scripts/africaniser_avatars.py) :
  * FBX : la casquette à visière du modèle devient un béret noir. Les 172 sommets de la casquette (une coque séparée,
    attachée à 100 % à l'os de la tête) sont replacés sur une surface de béret : bandeau serré au-dessus du front,
    calotte souple, plus large et retombant du côté droit, comme la façon réglementaire de porter le béret.
    La visière est repliée dans le bandeau. Le crâne (cheveux) est déjà modélisé sous la casquette : le béret est
    posé dessus sans jamais le traverser. Les normales de la coque sont recalculées. Le reste du fichier est réécrit
    octet pour octet (outil fbxrw : un aller-retour sans modification redonne le fichier d'origine).
  * Texture de la tête : la casquette (bleue) devient de la laine noire.
  * Texture du corps : les écussons étrangers des manches (drapeau et insigne de la police afghane du pack d'origine)
    sont effacés, le tissu gris-bleu passe au bleu de nuit demandé par MATULA DESIGN, et la poche de poitrine gauche
    porte l'inscription « POLICE » en petites lettres blanches bordées de bleu (posée en suivant la surface du maillage).
Aucun insigne officiel n'est reproduit.

Usage : python3 scripts/police_congolaise.py [--apercu dossier]
"""
import math
import os
import sys

import numpy as np
from PIL import Image

ICI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, ICI)
from fbxrw import FBX  # noqa: E402

AV = os.environ.get("POLICE_AV") or os.path.join(ICI, "..", "Assets", "BalulaMoto", "ThirdParty", "Rocketbox", "Avatars", "Police_Male_01")
FBX_PATH = os.path.join(AV, "Police_Male_01.fbx")
TEX_HEAD = os.path.join(AV, "Textures", "m220_head_color.jpg")
TEX_BODY = os.path.join(AV, "Textures", "m220_body_color.jpg")
TEX_HEAD_N = os.path.join(AV, "Textures", "m220_head_normal.jpg")


def smoothstep(a, b, x):
    t = min(1.0, max(0.0, (x - a) / (b - a)))
    return t * t * (3 - 2 * t)


class Geo:
    def __init__(self, f):
        objs = f.top("Objects")
        self.geo = [k for k in objs.kids if k.name == "Geometry" and k.props[2].val == b"Mesh"][0]
        g = self.geo
        self.pV = g.find("Vertices").props[0]
        self.V = np.array(self.pV.val).reshape(-1, 3)
        pvi = g.find("PolygonVertexIndex").props[0].val
        uvl = g.find("LayerElementUV")
        self.UV = np.array(uvl.find("UV").props[0].val).reshape(-1, 2)
        self.UVI = np.array(uvl.find("UVIndex").props[0].val)
        self.MI = np.array(g.find("LayerElementMaterial").find("Materials").props[0].val)
        nl = g.find("LayerElementNormal")
        assert nl.find("MappingInformationType").props[0].val == b"ByPolygonVertex"
        assert nl.find("ReferenceInformationType").props[0].val == b"Direct"
        self.pN = nl.find("Normals").props[0]
        self.N = np.array(self.pN.val).reshape(-1, 3)
        self.polys = []
        cur = []
        for k, i in enumerate(pvi):
            if i < 0:
                cur.append((-i - 1, k))
                self.polys.append(cur)
                cur = []
            else:
                cur.append((i, k))


def uv_color(tex, uv):
    h, w = tex.shape[:2]
    return tex[int((1 - uv[1] % 1) * h) % h, int((uv[0] % 1) * w) % w]


def ray_far(T, o, d):
    """Distance au point d'impact le plus éloigné du rayon (o, d) sur les triangles T, ou None."""
    e1 = T[:, 1] - T[:, 0]
    e2 = T[:, 2] - T[:, 0]
    h = np.cross(d, e2)
    a = (e1 * h).sum(1)
    ok = np.abs(a) > 1e-9
    f = np.where(ok, 1 / np.where(ok, a, 1), 0)
    q0 = o - T[:, 0]
    u = f * (q0 * h).sum(1)
    q = np.cross(q0, e1)
    v = f * (q * d).sum(1)
    t = f * (e2 * q).sum(1)
    hit = ok & (u >= 0) & (v >= 0) & (u + v <= 1) & (t > 0)
    return float(t[hit].max()) if hit.any() else None


def beret(f, head_tex):
    g = Geo(f)
    V, polys, MI = g.V, g.polys, g.MI
    HEAD = 1   # matériau m220_head (ordre des connexions du modèle : body, head, equipment, pistol)
    isblue = lambda c: c[2] > c[0] + 18 and c[2] > 60
    blue = [i for i, p in enumerate(polys) if MI[i] == HEAD and isblue(uv_color(head_tex, g.UV[[g.UVI[k] for _, k in p]].mean(0)))]
    if len(blue) < 150:
        raise SystemExit("Casquette introuvable (déjà transformée en béret ?) : rien n'est modifié.")
    # coque complète de la casquette : composante connexe des polygones bleus (doublure noire comprise)
    from collections import defaultdict
    v2p = defaultdict(list)
    for i, p in enumerate(polys):
        for a, _ in p:
            v2p[a].append(i)
    comp = set(blue)
    stack = list(blue)
    while stack:
        i = stack.pop()
        for a, _ in polys[i]:
            for j in v2p[a]:
                if j not in comp:
                    comp.add(j)
                    stack.append(j)
    assert all(MI[i] == HEAD for i in comp), "la casquette touche d'autres pièces"
    bset = set(blue)
    bv = set(a for i in blue for a, _ in polys[i])
    lv = set(a for i in comp if i not in bset for a, _ in polys[i])
    cv = sorted(bv | lv)
    P = V[cv]
    tris = []
    for i, p in enumerate(polys):
        if MI[i] != HEAD or i in comp:
            continue
        for t in range(1, len(p) - 1):
            tris.append([p[0][0], p[t][0], p[t + 1][0]])
    T = V[np.array(tris)]
    ztop = P[:, 2].max()
    topv = P[P[:, 2] > ztop - 0.8]
    cx, cy = topv[:, 0].mean(), topv[:, 1].mean()
    rel = P - np.array([cx, cy, 0.0])
    rho = np.hypot(rel[:, 0], rel[:, 1])
    az = np.arctan2(rel[:, 1], rel[:, 0])
    isb = np.array([a in bv for a in cv])
    islin = np.array([a in lv and a not in bv for a in cv])
    side = isb & (P[:, 2] < ztop - 1.3)
    band_r = float(np.median(rho[side & (rel[:, 1] > -6)]))
    isvis = isb & (rho > band_r + 1.2) & (P[:, 2] < ztop - 6.3) & (rel[:, 1] < 0)
    ns = 16
    sec = ((az + np.pi) / (2 * np.pi) * ns).astype(int) % ns
    zr_med = float(np.nanmedian([P[(sec == q) & isb & ~isvis, 2].min() if ((sec == q) & isb & ~isvis).any() else np.nan
                                 for q in range(ns)]))   # hauteur du bord de la casquette
    # paramètre méridien s : 0 au bord, 0,55 à l'arête du dessus de la casquette, 1 au centre du dessus
    s = np.zeros(len(cv))
    for k in range(len(cv)):
        if islin[k]:
            s[k] = 0.0
        elif isvis[k]:
            s[k] = 0.04 + 0.10 * min(1.0, max(0.0, (P[k, 2] - (zr_med - 1.55)) / 4.0))
        elif P[k, 2] > ztop - 0.8:
            s[k] = 0.55 + 0.45 * min(1.0, max(0.0, 1 - rho[k] / band_r))
        else:
            s[k] = 0.55 * min(1.0, max(0.0, (P[k, 2] - zr_med) / (ztop - 0.5 - zr_med)))

    # Surface du béret (repère du modèle : x à gauche du porteur, avant vers y-, haut z, centimètres)
    def band_z(x, y):                  # bandeau : plus bas derrière et du côté droit (x-)
        return zr_med + 0.45 - 0.085 * (y - cy + 4.3) + 0.035 * x

    O = np.array([cx, cy + 1.0, ztop - 15.3])

    def scalp(dx, dy, z):
        d = np.array([dx, dy, 0.0])
        o = np.array([cx, cy, z])
        r = ray_far(T, o, d)
        return o + d * ((r if r else band_r) + 0.42)

    ecx, ecy = cx - 2.3, cy + 0.6

    def edge(phi):
        ex = ecx + math.cos(phi) * 11.3
        ey = ecy + math.sin(phi) * 10.6
        right = max(0.0, -math.cos(phi))
        front = max(0.0, -math.sin(phi))
        return np.array([ex, ey, band_z(ex, ey) + 4.3 - 2.2 * right ** 1.2 + 0.6 * front])

    C = np.array([cx - 1.0, cy + 0.4, ztop + 2.0])
    newP = np.zeros_like(P)
    for k in range(len(cv)):
        phi = az[k]
        dx, dy = math.cos(phi), math.sin(phi)
        zb = band_z(cx + dx * band_r, cy + dy * band_r)
        B = scalp(dx, dy, zb)
        Bt = scalp(dx, dy, zb + 2.0)
        E = edge(phi)
        sk = s[k]
        if sk <= 0.15:
            u = sk / 0.15
            pnt = B * (1 - u) + Bt * u
        elif sk <= 0.55:
            u = (sk - 0.15) / 0.40
            ctrl = np.array([E[0] * 0.92 + Bt[0] * 0.08, E[1] * 0.92 + Bt[1] * 0.08, Bt[2] + 0.35])
            pnt = (1 - u) ** 2 * Bt + 2 * u * (1 - u) * ctrl + u * u * E
        else:
            u = (sk - 0.55) / 0.45
            ctrl = np.array([E[0] * 0.8 + C[0] * 0.2, E[1] * 0.8 + C[1] * 0.2, E[2] * 0.15 + C[2] * 0.85])
            pnt = (1 - u) ** 2 * E + 2 * u * (1 - u) * ctrl + u * u * C
        d = pnt - O
        L = np.linalg.norm(d)
        d /= L
        r = ray_far(T, O, d)
        marge = 0.6 + 0.9 * sk          # jamais dans le crâne (marge plus grande sous les grandes faces du dessus)
        if r is not None and L < r + marge:
            pnt = O + d * (r + marge)
        newP[k] = pnt
    # Normales lisses d'un coussin aplati (centre du béret, demi-axes 11 × 11 × 4,5 cm)
    Oc = np.array([cx - 1.5, cy + 0.4, ztop - 2.5])
    nrm = (newP - Oc) / np.array([11.0 ** 2, 11.0 ** 2, 4.5 ** 2])
    nrm /= np.linalg.norm(nrm, axis=1)[:, None]
    vmap = {a: k for k, a in enumerate(cv)}
    V2 = V.copy()
    V2[cv] = newP
    N2 = g.N.copy()
    for i in comp:
        for a, k in polys[i]:
            N2[k] = nrm[vmap[a]]
    g.pV.set(V2.reshape(-1).tolist())
    g.pN.set(N2.reshape(-1).tolist())
    print("bord d'origine %.2f cm, axe (%.2f ; %.2f)" % (zr_med, cx, cy))
    print("Béret : %d sommets de la casquette replacés (%d polygones), bandeau r = %.1f cm, haut à %.1f cm"
          % (len(cv), len(comp), band_r, newP[:, 2].max()))
    return g, comp, V2


def uv_mask(g, polys_idx, w, h):
    from PIL import ImageDraw
    m = Image.new("L", (w, h), 0)
    dr = ImageDraw.Draw(m)
    for i in polys_idx:
        pts = [((g.UV[g.UVI[k]][0] % 1) * w, (1 - g.UV[g.UVI[k]][1] % 1) * h) for _, k in g.polys[i]]
        dr.polygon(pts, fill=255)
    return np.asarray(m) > 0


def head_texture(g, comp, path):
    import cv2
    im = np.asarray(Image.open(path).convert("RGB")).astype(float)
    h, w = im.shape[:2]
    cap = uv_mask(g, comp, w, h)
    other = uv_mask(g, [i for i in range(len(g.polys)) if g.MI[i] == 1 and i not in comp], w, h)
    cap = cv2.dilate(cap.astype(np.uint8), np.ones((5, 5), np.uint8)) > 0
    cap &= ~other
    rng = np.random.default_rng(7)
    lum = im.mean(2)
    detail = (lum - cv2.GaussianBlur(lum, (0, 0), 6)) * 0.35          # plis du tissu d'origine, atténués
    noise = cv2.GaussianBlur(rng.normal(0, 1, (h, w)), (0, 0), 0.8) * 2.5   # laine
    base = 24 + detail + noise
    out = im.copy()
    for c, k in enumerate((1.0, 1.0, 1.08)):
        out[..., c] = np.where(cap, np.clip(base * k, 8, 60), im[..., c])
    Image.fromarray(out.astype(np.uint8)).save(path, quality=92)
    print("Texture de la tête : %d pixels de casquette passés en laine noire" % cap.sum())
    # relief : les coutures de la casquette (visière, calotte) disparaissent, grain léger de la laine
    nm = np.asarray(Image.open(TEX_HEAD_N).convert("RGB")).astype(float)
    nh, nw = nm.shape[:2]
    capn = cv2.resize(cap.astype(np.uint8), (nw, nh), interpolation=cv2.INTER_NEAREST) > 0
    gx = cv2.GaussianBlur(rng.normal(0, 1, (nh, nw)), (0, 0), 1.0) * 6
    gy = cv2.GaussianBlur(rng.normal(0, 1, (nh, nw)), (0, 0), 1.0) * 6
    flat = np.stack([128 + gx, 128 + gy, np.full((nh, nw), 250.0)], -1)
    nm = np.where(capn[..., None], flat, nm)
    Image.fromarray(np.clip(nm, 0, 255).astype(np.uint8)).save(TEX_HEAD_N, quality=92)


def surface_uv(g, mat, x, z):
    """UV du point de la surface vue de face (rayon vers +Y depuis l'avant) en (x, z) cm, matériau donné."""
    if not hasattr(g, "_tris"):
        g._tris = {}
    if mat not in g._tris:
        T, TU = [], []
        for i, p in enumerate(g.polys):
            if g.MI[i] != mat:
                continue
            for t in range(1, len(p) - 1):
                tri = [p[0], p[t], p[t + 1]]
                T.append([g.V[a] for a, _ in tri])
                TU.append([g.UV[g.UVI[k]] for _, k in tri])
        g._tris[mat] = (np.array(T), np.array(TU))
    T, TU = g._tris[mat]
    o = np.array([x, -80.0, z])
    d = np.array([0.0, 1.0, 0.0])
    e1 = T[:, 1] - T[:, 0]
    e2 = T[:, 2] - T[:, 0]
    h = np.cross(d, e2)
    a = (e1 * h).sum(1)
    ok = np.abs(a) > 1e-9
    f = np.where(ok, 1 / np.where(ok, a, 1), 0)
    q0 = o - T[:, 0]
    u = f * (q0 * h).sum(1)
    q = np.cross(q0, e1)
    v = f * (q * d).sum(1)
    t = f * (e2 * q).sum(1)
    m = ok & (u >= 0) & (v >= 0) & (u + v <= 1) & (t > 0)
    if not m.any():
        return None
    k = np.where(m)[0][np.argmin(t[m])]
    w = np.array([1 - u[k] - v[k], u[k], v[k]])
    return (w[:, None] * TU[k]).sum(0)


# Inscription « POLICE » sur la poche de poitrine gauche (côté gauche du porteur = x+ dans le FBX), en blanc bordé de bleu
POCHE_X, POCHE_Z, POCHE_L = 11.0, 130.7, 8.4      # centre et largeur du texte (cm), sous le rabat de la poche


def inscription(g, im):
    from PIL import ImageDraw, ImageFont
    h, w = im.shape[:2]
    font_path = None
    for fp in ("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", "/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf"):
        if os.path.exists(fp):
            font_path = fp
            break
    PX = 40.0                                     # pixels par cm dans l'image du texte
    font = ImageFont.truetype(font_path, 200)
    txt = "POLICE"
    stroke = 16
    bb = font.getbbox(txt, stroke_width=stroke)
    tw, th = bb[2] - bb[0], bb[3] - bb[1]
    scale = POCHE_L * PX / tw
    font = ImageFont.truetype(font_path, max(10, int(200 * scale)))
    stroke = max(2, int(round(16 * scale)))
    bb = font.getbbox(txt, stroke_width=stroke)
    tw, th = bb[2] - bb[0], bb[3] - bb[1]
    W, H = tw + 8, th + 8
    lay = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ImageDraw.Draw(lay).text((4 - bb[0], 4 - bb[1]), txt, font=font, fill=(248, 248, 248, 255),
                             stroke_width=stroke, stroke_fill=(52, 112, 222, 255))
    L = np.asarray(lay).astype(float)
    wc, hc = W / PX, H / PX
    x0, z1 = POCHE_X - wc / 2, POCHE_Z + hc / 2
    # grille UV (pas de 0,25 cm) sur la zone du texte, interpolée pour chaque pixel du texte
    gx = np.arange(x0 - 0.25, x0 + wc + 0.5, 0.25)
    gz = np.arange(z1 - hc - 0.25, z1 + 0.5, 0.25)
    G = np.zeros((len(gz), len(gx), 2))
    for j, z in enumerate(gz):
        for i, x in enumerate(gx):
            uv = surface_uv(g, 0, x, z)
            G[j, i] = uv if uv is not None else np.nan
    if np.isnan(G).any():
        raise SystemExit("Poche introuvable sur le maillage : inscription non posée.")
    acc = np.zeros((h, w, 3))
    accA = np.zeros((h, w))
    cnt = np.zeros((h, w))
    jj, ii = np.mgrid[0:H, 0:W]
    xs = x0 + (ii + 0.5) / PX
    zs = z1 - (jj + 0.5) / PX
    fx = (xs - gx[0]) / 0.25
    fz = (zs - gz[0]) / 0.25
    i0 = np.clip(np.floor(fx).astype(int), 0, len(gx) - 2)
    j0 = np.clip(np.floor(fz).astype(int), 0, len(gz) - 2)
    tx_, tz_ = fx - i0, fz - j0
    uv = (G[j0, i0] * ((1 - tx_) * (1 - tz_))[..., None] + G[j0, i0 + 1] * (tx_ * (1 - tz_))[..., None]
          + G[j0 + 1, i0] * ((1 - tx_) * tz_)[..., None] + G[j0 + 1, i0 + 1] * (tx_ * tz_)[..., None])
    px = np.clip(((uv[..., 0] % 1) * w).astype(int), 0, w - 1)
    py = np.clip(((1 - uv[..., 1] % 1) * h).astype(int), 0, h - 1)
    a = L[..., 3] / 255.0
    np.add.at(cnt, (py, px), 1)
    np.add.at(accA, (py, px), a)
    for c in range(3):
        np.add.at(acc[..., c], (py, px), L[..., c] * a)
    m = cnt > 0
    alpha = np.zeros((h, w))
    alpha[m] = accA[m] / cnt[m]
    col = np.zeros((h, w, 3))
    mm = accA > 1e-6
    col[mm] = acc[mm] / accA[mm][:, None]
    out = im.astype(float) * (1 - alpha[..., None]) + col * alpha[..., None]
    print("Inscription POLICE posée sur la poche gauche : %d texels" % int((alpha > 0.05).sum()))
    return np.clip(out, 0, 255).astype(np.uint8)


def body_texture(path, g=None):
    import cv2
    im = np.asarray(Image.open(path).convert("RGB"))
    hsv = cv2.cvtColor(im, cv2.COLOR_RGB2HSV).astype(float)   # H 0-180, S, V 0-255
    # 1) écussons des manches : pixels qui ne sont pas du tissu bleu-gris, dans les zones des deux écussons
    mask = np.zeros(im.shape[:2], np.uint8)
    for (x0, y0, x1, y1) in ((105, 505, 192, 553), (830, 500, 912, 578)):
        sub = hsv[y0:y1, x0:x1]
        fabric = (sub[..., 0] > 95) & (sub[..., 0] < 120) & (sub[..., 1] > 40) & (sub[..., 1] < 140) & (sub[..., 2] > 70)
        mask[y0:y1, x0:x1] = (~fabric).astype(np.uint8)
    mask = cv2.dilate(mask, np.ones((7, 7), np.uint8))
    im = cv2.inpaint(im, mask, 9, cv2.INPAINT_TELEA)
    print("Écussons étrangers effacés : %d pixels" % (mask > 0).sum())
    # 2) tissu gris-bleu -> bleu de nuit (demande de MATULA DESIGN) : teinte 224°, plus saturé, plus sombre
    hsv = cv2.cvtColor(im, cv2.COLOR_RGB2HSV).astype(float)
    H, S, Vv = hsv[..., 0], hsv[..., 1], hsv[..., 2]
    fab = (H > 92) & (H < 125) & (S > 25) & (Vv > 28)
    wgt = np.clip((S - 25) / 20.0, 0, 1) * np.clip((Vv - 28) / 20.0, 0, 1) * fab
    H2 = H + 6.5 * wgt
    S2 = np.clip(S * (1 + 0.85 * wgt), 0, 255)
    V2 = np.clip(Vv * (1 - 0.38 * wgt), 0, 255)
    out = cv2.cvtColor(np.stack([H2, S2, V2], -1).astype(np.uint8), cv2.COLOR_HSV2RGB)
    print("Tissu de l'uniforme passé au bleu de nuit : %.0f %% de la texture" % (100.0 * fab.mean()))
    # 3) inscription POLICE sur la poche gauche
    if g is not None:
        out = inscription(g, out)
    Image.fromarray(out).save(path, quality=93)


def main():
    f = FBX(FBX_PATH)
    head_tex = np.asarray(Image.open(TEX_HEAD).convert("RGB")).astype(float)
    g, comp, V2 = beret(f, head_tex)
    n = f.save(FBX_PATH)
    print("FBX réécrit : %d octets" % n)
    head_texture(g, comp, TEX_HEAD)
    body_texture(TEX_BODY, g)


if __name__ == "__main__":
    main()
