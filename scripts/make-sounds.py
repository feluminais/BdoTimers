"""Synthesizes the built-in alert sounds into src/BdoTimers.App/Assets/Sounds (22 050 Hz, mono, 16-bit WAV).

Standard library only, so the sounds are ours to ship. Run from the repo root: python scripts/make-sounds.py
"""
import math
import os
import random
import struct
import wave

RATE = 22050
OUT = os.path.join(os.path.dirname(__file__), "..", "src", "BdoTimers.App", "Assets", "Sounds")


def partials(duration, parts, attack=0.004, bend=0.0):
    """Sum of sine partials: parts = [(frequency, amplitude, decay_seconds)]. bend sags the pitch at the start."""
    n = int(duration * RATE)
    out = [0.0] * n
    for freq, amp, decay in parts:
        phase = 0.0
        for i in range(n):
            t = i / RATE
            f = freq * (1 + bend * math.exp(-t * 12))
            phase += 2 * math.pi * f / RATE
            env = min(1.0, t / attack) * math.exp(-t / decay)
            out[i] += amp * env * math.sin(phase)
    return out


def gong():
    # Inharmonic partials with long, uneven decays and a small pitch sag after the strike.
    f = 98.0
    parts = [(f, 1.0, 1.9), (f * 1.47, 0.7, 1.5), (f * 2.09, 0.55, 1.2), (f * 2.56, 0.4, 1.0),
             (f * 3.17, 0.3, 0.8), (f * 4.12, 0.2, 0.6), (f * 5.43, 0.12, 0.4)]
    return partials(3.2, parts, attack=0.008, bend=0.015)


def horn():
    # Low brass blast: harmonic series that brightens as it swells, gentle vibrato, then a shorter second call.
    out = []
    for duration, f0 in ((1.25, 146.8), (0.85, 146.8)):
        n = int(duration * RATE)
        phase = 0.0
        for i in range(n):
            t = i / RATE
            swell = min(1.0, t / 0.28)
            release = min(1.0, (duration - t) / 0.3)
            env = swell * release
            f = f0 * (1 + 0.006 * math.sin(2 * math.pi * 5.2 * t) * swell)
            phase += 2 * math.pi * f / RATE
            s = 0.0
            for k in range(1, 9):
                s += (env ** (0.6 + 0.35 * k)) / k * math.sin(k * phase)
            out.append(s * env)
        out.extend([0.0] * int(0.12 * RATE))
    return out


def bell():
    # Church-bell partial ratios (hum, prime, tierce, quint, nominal, upper partials) at a low pitch.
    f = 262.0
    parts = [(f * 0.5, 0.5, 2.4), (f, 0.8, 1.8), (f * 1.19, 0.5, 1.4), (f * 1.5, 0.35, 1.2),
             (f * 2.0, 0.6, 1.0), (f * 2.51, 0.25, 0.7), (f * 3.01, 0.2, 0.5), (f * 4.07, 0.12, 0.35)]
    return partials(2.8, parts, attack=0.003)


def chime():
    # The old two-tone idea, softened: two short sine notes with a little second harmonic and quick decay.
    out = []
    for freq in (659.3, 987.8):
        out.extend(partials(0.55, [(freq, 1.0, 0.28), (freq * 2, 0.15, 0.12)], attack=0.006))
    out.extend(partials(0.9, [(659.3, 0.8, 0.45), (1318.5, 0.1, 0.2)], attack=0.006))
    return out


def write(name, samples):
    peak = max(abs(s) for s in samples) or 1.0
    scale = 0.7 * 32767 / peak  # about -3 dBFS
    fade = int(0.02 * RATE)
    frames = bytearray()
    for i, s in enumerate(samples):
        if i >= len(samples) - fade:
            s *= (len(samples) - i) / fade
        frames += struct.pack("<h", int(max(-32768, min(32767, s * scale))))
    os.makedirs(OUT, exist_ok=True)
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(bytes(frames))
    print(f"{name}.wav  {len(samples) / RATE:.1f}s")


if __name__ == "__main__":
    random.seed(0)
    for name, make in (("gong", gong), ("horn", horn), ("bell", bell), ("chime", chime)):
        write(name, make())
