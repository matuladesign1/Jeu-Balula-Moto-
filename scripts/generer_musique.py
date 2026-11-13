#!/usr/bin/env python3
# BALULA MOTO — musiques de fond ORIGINALES, entièrement générées par ce programme (aucun échantillon,
# aucune chanson existante, aucune mélodie reprise) : rumba et sebene congolais instrumentaux.
# Guitares (synthèse additive), basse, batterie, congas, maracas, orgue. Œuvres de MATULA DESIGN pour le jeu.
# Usage : python3 scripts/generer_musique.py dossier_sortie   (fichiers .wav ; conversion en .ogg avec ffmpeg)
import os
import sys

import numpy as np
from scipy.signal import butter, fftconvolve, sosfilt

SR = 32000
NOTE = {'C': 0, 'C#': 1, 'Db': 1, 'D': 2, 'D#': 3, 'Eb': 3, 'E': 4, 'F': 5, 'F#': 6, 'Gb': 6,
        'G': 7, 'G#': 8, 'Ab': 8, 'A': 9, 'A#': 10, 'Bb': 10, 'B': 11}
MAJOR = [0, 4, 7]
MINOR = [0, 3, 7]


def midi(name):
    return 12 * (int(name[-1]) + 1) + NOTE[name[:-1]]


def hz(m):
    return 440.0 * 2 ** ((m - 69) / 12.0)


def bp(lo, hi, order=2):
    return butter(order, [lo, hi], btype='bandpass', fs=SR, output='sos')


def hp(f, order=2):
    return butter(order, f, btype='highpass', fs=SR, output='sos')


def lp(f, order=2):
    return butter(order, f, btype='lowpass', fs=SR, output='sos')


class Track:
    def __init__(self, seconds, seed):
        self.n = int(seconds * SR)
        self.L = np.zeros(self.n)
        self.R = np.zeros(self.n)
        self.sendL = np.zeros(self.n)
        self.sendR = np.zeros(self.n)
        self.rng = np.random.default_rng(seed)

    def put(self, sig, t, pan=0.0, rev=0.15):
        i = int(t * SR)
        if i >= self.n or i < 0:
            return
        sig = sig[: self.n - i]
        th = (pan + 1.0) * np.pi / 4.0
        gl, gr = np.cos(th), np.sin(th)
        self.L[i:i + len(sig)] += sig * gl
        self.R[i:i + len(sig)] += sig * gr
        if rev > 0:
            self.sendL[i:i + len(sig)] += sig * gl * rev
            self.sendR[i:i + len(sig)] += sig * gr * rev

    # ------------------------------------------------------------------ instruments
    def guitar(self, f, dur, vel=1.0, bright=1.0, pluck=0.17):
        n = int((dur + 0.06) * SR)
        t = np.arange(n) / SR
        out = np.zeros(n)
        nh = int(min(16, (SR / 2.3) / f))
        for h in range(1, nh + 1):
            fh = f * h * (1.0 + 0.00035 * h * h)
            amp = abs(np.sin(np.pi * h * pluck)) / h ** (1.15 - 0.35 * bright)
            tau = (1.1 / (1.0 + 0.25 * h)) * (0.55 + 0.45 * bright)
            out += amp * np.sin(2 * np.pi * fh * t + self.rng.uniform(0, 6.283)) * np.exp(-t / tau)
        a = int(0.0015 * SR)
        out[:a] *= np.linspace(0, 1, a)
        k = int(0.008 * SR)
        out[:k] += self.rng.standard_normal(k) * np.exp(-np.arange(k) / (0.0015 * SR)) * 0.12 * bright
        r0 = int(dur * SR)
        if r0 < n:
            out[r0:] *= np.linspace(1, 0, n - r0)
        return out * vel * 0.22

    def bass(self, f, dur, vel=1.0):
        n = int((dur + 0.04) * SR)
        t = np.arange(n) / SR
        env = np.exp(-t / 0.5)
        a = int(0.005 * SR)
        env[:a] *= np.linspace(0, 1, a)
        y = (np.sin(2 * np.pi * f * t) + 0.38 * np.sin(4 * np.pi * f * t) * np.exp(-t / 0.18)
             + 0.14 * np.sin(6 * np.pi * f * t) * np.exp(-t / 0.08))
        y *= env
        r0 = int(dur * SR)
        if r0 < n:
            y[r0:] *= np.linspace(1, 0, n - r0)
        return np.tanh(1.4 * y) * vel * 0.42

    def organ(self, freqs, dur, vel=1.0):
        n = int((dur + 0.3) * SR)
        t = np.arange(n) / SR
        y = np.zeros(n)
        vib = 1.0 + 0.003 * np.sin(2 * np.pi * 5.2 * t)
        for f in freqs:
            for h, a in ((1, 1.0), (2, 0.45), (3, 0.25), (4, 0.12)):
                y += a * np.sin(2 * np.pi * f * h * np.cumsum(vib) / SR)
        env = np.minimum(1.0, t / 0.25)
        r0 = int(dur * SR)
        env[r0:] *= np.linspace(1, 0, n - r0)
        return y * env * vel * 0.03

    def kick(self, vel=1.0):
        n = int(0.38 * SR)
        t = np.arange(n) / SR
        f = 47.0 + 95.0 * np.exp(-t * 26.0)
        y = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t * 8.0)
        c = int(0.003 * SR)
        y[:c] += self.rng.standard_normal(c) * 0.25
        return y * vel * 0.85

    def snare(self, vel=1.0):
        n = int(0.24 * SR)
        t = np.arange(n) / SR
        noise = sosfilt(bp(1300, 7500), self.rng.standard_normal(n))
        y = noise * np.exp(-t * 20.0) * 0.75 + np.sin(2 * np.pi * 190 * t) * np.exp(-t * 28.0) * 0.55
        return y * vel * 0.5

    def rim(self, vel=1.0):
        n = int(0.09 * SR)
        t = np.arange(n) / SR
        y = np.sin(2 * np.pi * 830 * t) * np.exp(-t * 55.0) + sosfilt(bp(2000, 6500), self.rng.standard_normal(n)) * np.exp(-t * 75.0) * 0.45
        return y * vel * 0.3

    def hat(self, vel=1.0, open_=False):
        n = int((0.3 if open_ else 0.07) * SR)
        t = np.arange(n) / SR
        y = sosfilt(hp(7000), self.rng.standard_normal(n)) * np.exp(-t * (8.0 if open_ else 50.0))
        return y * vel * 0.2

    def shaker(self, vel=1.0):
        n = int(0.1 * SR)
        t = np.arange(n) / SR
        env = np.minimum(1.0, t / 0.018) * np.exp(-t * 32.0)
        return sosfilt(bp(3500, 9500), self.rng.standard_normal(n)) * env * vel * 0.1

    def conga(self, f=215.0, vel=1.0, slap=False):
        n = int(0.28 * SR)
        t = np.arange(n) / SR
        fr = f * (1.0 + 0.22 * np.exp(-t * 38.0))
        y = np.sin(2 * np.pi * np.cumsum(fr) / SR) * np.exp(-t * (13.0 if not slap else 30.0))
        if slap:
            y += sosfilt(bp(1500, 5500), self.rng.standard_normal(n)) * np.exp(-t * 65.0) * 0.6
        return y * vel * 0.32

    def cymbal(self, vel=1.0):
        n = int(1.6 * SR)
        t = np.arange(n) / SR
        y = sosfilt(hp(5000), self.rng.standard_normal(n)) * np.exp(-t * 2.6)
        return y * vel * 0.12

    # ------------------------------------------------------------------ rendu final
    def master(self, path, fade_out=2.5):
        t = np.linspace(0, 1.6, int(1.6 * SR))
        ir = self.rng.standard_normal(len(t)) * np.exp(-t / 0.32)
        ir = sosfilt(lp(5200), ir)
        ir[: int(0.012 * SR)] = 0.0
        ir /= np.sqrt(np.sum(ir * ir))
        ir2 = np.roll(ir, int(0.007 * SR))
        wetL = fftconvolve(self.sendL, ir)[: self.n]
        wetR = fftconvolve(self.sendR, ir2)[: self.n]
        L = self.L + wetL * 0.9
        R = self.R + wetR * 0.9
        # Égalisation légère : un peu moins de grave boueux, présence des guitares
        L = L - 0.25 * sosfilt(lp(120), L)
        R = R - 0.25 * sosfilt(lp(120), R)
        peak = max(np.max(np.abs(L)), np.max(np.abs(R)), 1e-6)
        L = np.tanh(1.25 * L / peak) / np.tanh(1.25)
        R = np.tanh(1.25 * R / peak) / np.tanh(1.25)
        fi = int(0.25 * SR)
        fo = int(fade_out * SR)
        for ch in (L, R):
            ch[:fi] *= np.linspace(0, 1, fi)
            ch[-fo:] *= np.linspace(1, 0, fo) ** 1.5
        st = np.stack([L, R], axis=1) * 0.89
        pcm = (np.clip(st, -1, 1) * 32767).astype('<i2')
        import wave
        with wave.open(path, 'wb') as w:
            w.setnchannels(2)
            w.setsampwidth(2)
            w.setframerate(SR)
            w.writeframes(pcm.tobytes())


# ---------------------------------------------------------------------- écriture des morceaux
def chord_tones(root, quality, low, high):
    """Notes de l'accord (MIDI) entre low et high."""
    out = []
    for m in range(low, high + 1):
        if (m - root) % 12 in quality:
            out.append(m)
    return out


def song(path, bpm, prog, bars, seed, style, key_root, title):
    step = 60.0 / bpm / 4.0
    bar_len = step * 16
    seconds = bars * bar_len + 3.0
    tr = Track(seconds, seed)
    rng = np.random.default_rng(seed + 1)
    # Thème mélodique : motif de 2 mesures (degrés dans l'accord), répété et varié
    motif_rhythm = [0, 3, 6, 8, 10, 12, 14] if style != 'nuit' else [0, 4, 6, 8, 12]
    motif_deg = rng.integers(0, 4, size=len(motif_rhythm) * 2)
    sebene_patterns = [
        [4, 2, 3, 1, 4, 2, 3, 1, 5, 3, 4, 2, 4, 2, 3, 1],
        [5, 2, 5, 3, 5, 2, 5, 3, 5, 2, 5, 3, 4, 2, 3, 1],
        [3, 1, 2, 0, 3, 1, 2, 0, 4, 2, 3, 1, 3, 1, 2, 0],
        [5, 4, 3, 2, 4, 3, 2, 1, 3, 2, 1, 0, 1, 2, 3, 4],
        [4, None, 2, 3, None, 1, 4, None, 2, 3, None, 1, 5, 4, 3, 2],
    ]
    for b in range(bars):
        t0 = b * bar_len
        root_name, quality = prog[b % len(prog)]
        root = midi(root_name)
        nxt_root = midi(prog[(b + 1) % len(prog)][0])
        intro = b < 4
        outro = b >= bars - 2
        sebene = (style in ('sebene', 'ndombolo') and b >= bars // 4) or (style == 'rumba' and b >= bars // 2)
        quiet = style == 'nuit'

        # --- basse
        if not intro or style == 'nuit':
            r = root
            while r > midi('E2') + 7:
                r -= 12
            fifth = r + 7
            octave = r + 12
            third = r + (4 if quality == MAJOR else 3)
            approach = nxt_root
            while approach > r + 5:
                approach -= 12
            approach -= 2 if rng.random() < 0.5 else 1
            if sebene:
                pattern = {0: r, 3: r, 6: fifth, 8: octave, 10: fifth, 11: third, 14: approach}
            elif quiet:
                pattern = {0: r, 6: fifth, 8: octave, 12: fifth, 14: third}
            else:
                pattern = {0: r, 3: r, 6: fifth, 8: octave, 12: fifth, 14: approach}
            for s, m in pattern.items():
                tr.put(tr.bass(hz(m), step * 1.8, 0.95 if s in (0, 8) else 0.75), t0 + s * step, 0.0, 0.05)

        # --- batterie
        if not intro or style == 'ndombolo':
            for s in range(16):
                ts = t0 + s * step
                swing = 0.012 if (s % 2 == 1 and style in ('rumba', 'nuit')) else 0.0
                if style == 'ndombolo':
                    if s % 4 == 0 and not intro:
                        tr.put(tr.kick(1.0), ts, 0.0, 0.0)
                    if s in (4, 12) and not intro:
                        tr.put(tr.snare(0.95), ts, 0.05, 0.12)
                    tr.put(tr.hat(0.9 if s % 4 == 2 else 0.45, open_=(s % 4 == 2)), ts, 0.35, 0.03)
                    if s % 2 == 1:
                        tr.put(tr.rim(0.35 + 0.35 * (s in (3, 7, 11, 15))), ts, -0.3, 0.05)
                elif sebene:
                    if s in (0, 6, 8, 14):
                        tr.put(tr.kick(1.0 if s in (0, 8) else 0.7), ts, 0.0, 0.0)
                    if s in (4, 12):
                        tr.put(tr.snare(0.9), ts, 0.05, 0.12)
                    # cavacha : roulement continu de la caisse claire (doubles croches accentuées)
                    acc = s in (2, 6, 10, 14)
                    tr.put(tr.rim(0.55 if acc else 0.28), ts, -0.25, 0.04)
                    tr.put(tr.hat(0.7 if acc else 0.35), ts, 0.35, 0.02)
                else:
                    if s in (0, 10):
                        tr.put(tr.kick(0.9 if not quiet else 0.6), ts + swing, 0.0, 0.0)
                    if s == 12:
                        tr.put(tr.rim(0.8), ts + swing, -0.2, 0.08)
                    if s % 2 == 0:
                        tr.put(tr.hat(0.45 if not quiet else 0.25), ts + swing, 0.35, 0.02)
                tr.put(tr.shaker(0.8 if s % 2 == 0 else 0.5), ts + swing, -0.45, 0.02)
            # congas (tumbao simplifié)
            for s, f, v, sl in ((0, 200, 0.4, False), (4, 260, 0.7, True), (8, 200, 0.4, False), (12, 185, 0.9, False), (14, 185, 0.8, False)):
                tr.put(tr.conga(f, v, sl), t0 + s * step, 0.5, 0.06)
            if b % 8 == 0 and not intro:
                tr.put(tr.cymbal(0.9), t0, 0.3, 0.1)

        # --- guitare rythmique (accords en contretemps)
        if not intro:
            voicing = chord_tones(root, quality, midi('G3'), midi('E4'))[:3]
            for s in (2, 6, 10, 14) if not quiet else (4, 12):
                for m in voicing:
                    tr.put(tr.guitar(hz(m), step * 0.9, 0.55, 0.6, 0.3), t0 + s * step + 0.004 * voicing.index(m), 0.45, 0.18)

        # --- mi-solo : arpèges en croches dans le médium
        if not intro or style == 'nuit':
            mid = chord_tones(root, quality, midi('A3'), midi('B4'))
            arp = [0, 2, 1, 2, 0, 2, 1, 3] if not quiet else [0, 1, 2, 1]
            div = 2 if not quiet else 4
            for i, d in enumerate(arp):
                m = mid[min(d, len(mid) - 1)]
                tr.put(tr.guitar(hz(m), step * div * 1.1, 0.6, 0.75, 0.2), t0 + i * div * step, -0.4, 0.2)

        # --- solo : thème (intro, couplet) puis sebene (arpèges rapides dans l'aigu)
        high = chord_tones(root, quality, midi('E4') if quiet else midi('A4'), midi('E6'))
        if sebene and not outro:
            pat = sebene_patterns[(b // 4) % len(sebene_patterns)]
            for s, d in enumerate(pat):
                if d is None:
                    continue
                m = high[min(d, len(high) - 1)]
                tr.put(tr.guitar(hz(m), step * 1.6, 0.85 if s % 4 == 0 else 0.7, 1.0, 0.12), t0 + s * step, 0.3, 0.25)
        else:
            half = (b % 2) * len(motif_rhythm)
            for i, s in enumerate(motif_rhythm):
                d = int(motif_deg[half + i])
                if b % 4 == 3 and i == len(motif_rhythm) - 1:
                    d = 0
                m = high[min(d + 1, len(high) - 1)]
                ln = (motif_rhythm[i + 1] - s) if i + 1 < len(motif_rhythm) else 16 - s
                tr.put(tr.guitar(hz(m), step * ln * 0.95, 0.8, 0.9, 0.14), t0 + s * step, 0.2, 0.3)

        # --- orgue (morceau de nuit et rumba)
        if style in ('nuit', 'rumba') and not intro:
            pad = chord_tones(root, quality, midi('C4'), midi('B4'))
            tr.put(tr.organ([hz(m) for m in pad], bar_len * 0.98, 0.9 if quiet else 0.6), t0, -0.1, 0.3)

    # dernier accord tenu
    tend = bars * bar_len
    root_name, quality = prog[0]
    for m in chord_tones(midi(root_name), quality, midi('E3'), midi('E5')):
        tr.put(tr.guitar(hz(m), 2.2, 0.6, 0.8, 0.2), tend, 0.0, 0.4)
    tr.put(tr.bass(hz(midi(root_name) - 24 if midi(root_name) - 24 > midi('E1') else midi(root_name) - 12), 2.0, 0.9), tend, 0.0, 0.05)
    tr.master(path)
    print('%s : %s (%d BPM, %.0f s)' % (os.path.basename(path), title, bpm, seconds))


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else '.'
    os.makedirs(out, exist_ok=True)
    song(os.path.join(out, 'musique_1_sebene_ya_balula.wav'), 138,
         [('A3', MAJOR), ('D4', MAJOR), ('E4', MAJOR), ('D4', MAJOR)], 48, 11, 'sebene', 'A', 'Sebene ya Balula')
    song(os.path.join(out, 'musique_2_nzela_malamu.wav'), 112,
         [('D4', MAJOR), ('G3', MAJOR), ('A3', MAJOR), ('G3', MAJOR)], 40, 23, 'rumba', 'D', 'Nzela Malamu')
    song(os.path.join(out, 'musique_3_butu_ya_kin.wav'), 94,
         [('E4', MAJOR), ('C#4', MINOR), ('A3', MAJOR), ('B3', MAJOR)], 32, 37, 'nuit', 'E', 'Butu ya Kin')
    song(os.path.join(out, 'musique_4_ndombolo_ya_moto.wav'), 148,
         [('G3', MAJOR), ('C4', MAJOR), ('D4', MAJOR), ('C4', MAJOR)], 52, 41, 'ndombolo', 'G', 'Ndombolo ya Moto')


if __name__ == '__main__':
    main()
