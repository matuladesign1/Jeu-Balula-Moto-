#!/usr/bin/env python3
"""BALULA MOTO — textures des cours d'eau en tranchée, à la manière de la photo de rivière envoyée par MATULA DESIGN :
berges en pente couvertes d'herbe avec des plaques de terre nue, terre humide au bord de l'eau, eau turquoise ridée ;
parois de béton des canaux et des caniveaux. Aucune photo n'est copiée : tout est dessiné par ce script (bruit, touffes,
taches).

Sorties (Assets/BalulaMoto/Resources/BalulaMoto/Textures) :
  berge.png        : 512 x 256 (4 m le long de la berge x toute la pente), haut de l'image = sommet de la berge (herbe),
                     bas = bord de l'eau (terre mouillée) ; se répète en longueur
  beton_canal.png  : 256 x 256 (2 m le long x toute la paroi), béton sale, ligne d'eau verdâtre en bas
  eau_ondes.png    : 256 x 256, pentes des rides de l'eau (rouge = pente en x, vert = pente en z, 0,5 = plat),
                     texture linéaire lue par le shader BalulaMoto/Eau (pas une « normal map » Unity)

Usage : python3 scripts/generer_textures_riviere.py
"""
import os
import numpy as np
from PIL import Image

ICI = os.path.dirname(os.path.abspath(__file__))
DEST = os.path.join(ICI, "..", "Assets", "BalulaMoto", "Resources", "BalulaMoto", "Textures")


def bruit(rng, h, w, beta=2.0):
    """Bruit fractal (spectre en 1/f^beta), périodique en x et en y, normalisé entre 0 et 1."""
    n = rng.standard_normal((h, w))
    f = np.fft.fft2(n)
    fy = np.fft.fftfreq(h)[:, None]
    fx = np.fft.fftfreq(w)[None, :]
    r = np.sqrt(fx * fx + fy * fy)
    r[0, 0] = 1.0
    f /= r ** (beta / 2.0)
    f[0, 0] = 0
    out = np.real(np.fft.ifft2(f))
    out -= out.min()
    out /= max(out.max(), 1e-9)
    return out


def melange(base, couleur, masque):
    m = np.clip(masque, 0, 1)[..., None]
    return base * (1 - m) + np.asarray(couleur, dtype=np.float64)[None, None, :] * m


def berge(rng):
    H, W = 256, 512
    v = np.linspace(0.0, 1.0, H)[:, None] * np.ones((1, W))      # 0 = sommet (herbe), 1 = bord de l'eau
    # Herbe : plusieurs verts, touffes plus claires et plus sombres
    vert1 = np.array((0.30, 0.44, 0.17))
    vert2 = np.array((0.42, 0.53, 0.22))
    t = bruit(rng, H, W, 1.6)
    img = vert1[None, None, :] * (1 - t[..., None]) + vert2[None, None, :] * t[..., None]
    grain = bruit(rng, H, W, 0.3)
    img *= (0.82 + 0.3 * grain)[..., None]
    # Brins d'herbe : petits traits verticaux clairs/sombres
    brins = np.zeros((H, W))
    ys = rng.integers(0, H, 5200)
    xs = rng.integers(0, W, 5200)
    ls = rng.integers(3, 9, 5200)
    for x, y, l in zip(xs, ys, ls):
        brins[max(0, y - l):y, x] += rng.uniform(-1, 1)
    img *= (1 + 0.16 * np.clip(brins, -1, 1))[..., None]
    # Plaques de terre nue dans l'herbe (plus nombreuses vers le bas), bords adoucis
    terre = np.array((0.47, 0.36, 0.23)) * (0.85 + 0.3 * bruit(rng, H, W, 1.0))[..., None]
    plaques = bruit(rng, H, W, 2.6)
    seuil = 0.74 - 0.30 * v
    m = np.clip((plaques - seuil) / 0.14, 0, 1) * 0.85
    img = img * (1 - m[..., None]) + terre * m[..., None]
    # Bas de la berge : terre puis boue humide (bord irrégulier, ondulé), mousse verdâtre au ras de l'eau
    limite = 0.70 + 0.16 * (bruit(rng, 1, W, 3.2)[0][None, :] - 0.5)
    limite = limite + 0.06 * (bruit(rng, H, W, 2.4) - 0.5)
    passage = np.clip((v - limite) / 0.14, 0, 1)
    boue = np.array((0.38, 0.29, 0.19)) * (0.80 + 0.35 * bruit(rng, H, W, 1.6))[..., None]
    boue *= (0.9 + 0.15 * bruit(rng, H, W, 0.3))[..., None]
    img = img * (1 - passage[..., None]) + boue * passage[..., None]
    mouille = np.clip((v - 0.88) / 0.10, 0, 1)
    img = melange(img, (0.22, 0.22, 0.15), mouille * 0.7)
    # Cailloux épars dans la terre
    cail = bruit(rng, H, W, 0.15)
    img = melange(img, (0.56, 0.53, 0.48), np.clip((cail - 0.94) / 0.03, 0, 1) * passage * 0.7)
    return np.clip(img, 0, 1)


def beton(rng):
    H, W = 256, 256
    v = np.linspace(0.0, 1.0, H)[:, None] * np.ones((1, W))
    base = np.array((0.56, 0.56, 0.53))
    img = base[None, None, :] * (0.86 + 0.24 * bruit(rng, H, W, 1.4))[..., None]
    img *= (0.92 + 0.12 * bruit(rng, H, W, 0.3))[..., None]
    # Joints verticaux tous les 2 m (bords de la texture) et coulures sombres
    x = np.arange(W)[None, :] * np.ones((H, 1))
    joint = (np.abs(x - 0) < 2) | (np.abs(x - (W - 1)) < 2)
    img[joint] *= 0.7
    coul = np.zeros((H, W))
    for _ in range(22):
        cx = int(rng.integers(0, W))
        w = int(rng.integers(1, 4))
        l = int(H * rng.uniform(0.15, 0.75))
        force = rng.uniform(0.25, 0.6)
        fondu = np.linspace(1.0, 0.0, l)[:, None]
        cols = np.arange(cx - w, cx + w + 1) % W
        coul[:l, cols] += force * fondu
    # Coulures adoucies (léger flou horizontal)
    coul = (np.roll(coul, 1, axis=1) + coul * 2 + np.roll(coul, -1, axis=1)) / 4.0
    img *= (1 - 0.32 * np.clip(coul, 0, 1))[..., None]
    # Ligne d'eau : algues vert sombre en bas, dépôts de boue
    algue = np.clip((v - 0.78 - 0.05 * (bruit(rng, 1, W, 2.0)[0][None, :] - 0.5)) / 0.1, 0, 1)
    img = melange(img, (0.17, 0.22, 0.13), algue * 0.85)
    taches = bruit(rng, H, W, 2.0)
    img = melange(img, (0.33, 0.29, 0.23), np.clip((taches - 0.7) / 0.1, 0, 1) * 0.5)
    return np.clip(img, 0, 1)


def ondes(rng):
    N = 256
    h = bruit(rng, N, N, 3.0) * 0.7 + bruit(rng, N, N, 2.2) * 0.3
    gx = (np.roll(h, -1, axis=1) - np.roll(h, 1, axis=1)) * 0.5 * N / 8.0
    gy = (np.roll(h, -1, axis=0) - np.roll(h, 1, axis=0)) * 0.5 * N / 8.0
    s = max(np.abs(gx).max(), np.abs(gy).max(), 1e-6)
    out = np.zeros((N, N, 3))
    out[..., 0] = 0.5 + 0.5 * gx / s
    out[..., 1] = 0.5 + 0.5 * gy / s
    out[..., 2] = 0.5 + 0.5 * h
    return np.clip(out, 0, 1)


def main():
    os.makedirs(DEST, exist_ok=True)
    rng = np.random.default_rng(2611)
    Image.fromarray((berge(rng) * 255).astype(np.uint8), "RGB").save(os.path.join(DEST, "berge.png"))
    Image.fromarray((beton(rng) * 255).astype(np.uint8), "RGB").save(os.path.join(DEST, "beton_canal.png"))
    Image.fromarray((ondes(rng) * 255).astype(np.uint8), "RGB").save(os.path.join(DEST, "eau_ondes.png"))
    print("berge.png 512x256, beton_canal.png 256x256, eau_ondes.png 256x256")


if __name__ == "__main__":
    main()
