"""Synthesized bullet impacts for the surfaces that had no sound of their own: wood, dirt and water.

Provisional sounds (no recorded foley in the project): 44.1 kHz mono 16-bit like the existing
Impact_Ground/Impact_Metal clips, three variations each, written next to them.

    python Tools/AudioGen/make_impacts.py
"""
import os
import wave

import numpy as np

RATE = 44100
OUT = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "SourceFiles", "SoundFX", "Weapons")


def lowpass(x, cutoff):
    # one-pole filter, enough for shaping noise
    a = np.exp(-2.0 * np.pi * cutoff / RATE)
    y = np.empty_like(x)
    acc = 0.0
    for i, v in enumerate(x):
        acc = (1.0 - a) * v + a * acc
        y[i] = acc
    return y


def resonator(x, freq, decay):
    # two-pole band-pass ringing at freq, the "knock" of a plank
    r = np.exp(-1.0 / (decay * RATE))
    c = 2.0 * r * np.cos(2.0 * np.pi * freq / RATE)
    y = np.zeros_like(x)
    for i in range(len(x)):
        y[i] = x[i] + c * (y[i - 1] if i > 0 else 0.0) - r * r * (y[i - 2] if i > 1 else 0.0)
    return y


def envelope(n, attack, decay):
    t = np.arange(n) / RATE
    return np.minimum(t / attack, 1.0) * np.exp(-t / decay)


def wood(rng):
    n = int(0.32 * RATE)
    click = rng.standard_normal(n) * envelope(n, 0.0005, 0.004)
    body = resonator(click, rng.uniform(380, 520), 0.035) + 0.6 * resonator(click, rng.uniform(900, 1300), 0.018)
    crack = lowpass(rng.standard_normal(n), 3500) * envelope(n, 0.001, 0.03) * 0.5
    splinter = rng.standard_normal(n) * envelope(n, 0.01, 0.06) * 0.08
    return body * 0.05 + crack + splinter


def dirt(rng):
    n = int(0.36 * RATE)
    thud = lowpass(rng.standard_normal(n), 260) * envelope(n, 0.001, 0.05) * 3.0
    spray = lowpass(rng.standard_normal(n), 2200) * envelope(n, 0.004, 0.09) * 0.35
    # loose soil falling back down
    t = np.arange(n) / RATE
    patter = rng.standard_normal(n) * (rng.random(n) < 0.004) * np.exp(-((t - 0.12) ** 2) / 0.004) * 0.6
    return thud + spray + lowpass(patter, 4000)


def water(rng):
    n = int(0.5 * RATE)
    t = np.arange(n) / RATE
    plop = np.sin(2 * np.pi * (rng.uniform(500, 700) * t + 2600 * t * t)) * envelope(n, 0.001, 0.025) * 0.5
    splash = lowpass(rng.standard_normal(n), 5000) * envelope(n, 0.003, 0.08) * 0.6
    droplets = np.zeros(n)
    for _ in range(14):
        start = int(rng.uniform(0.05, 0.4) * RATE)
        length = int(0.02 * RATE)
        f = rng.uniform(1200, 2600)
        seg = np.sin(2 * np.pi * f * np.arange(length) / RATE) * np.exp(-np.arange(length) / (0.006 * RATE))
        droplets[start:start + length] += seg[: max(0, min(length, n - start))] * rng.uniform(0.05, 0.15)
    return plop + splash + droplets


def write(name, samples):
    samples = samples / (np.max(np.abs(samples)) + 1e-9) * 0.8
    fade = int(0.01 * RATE)
    samples[-fade:] *= np.linspace(1.0, 0.0, fade)
    data = (samples * 32767).astype("<i2")
    with wave.open(os.path.join(OUT, name), "wb") as f:
        f.setnchannels(1)
        f.setsampwidth(2)
        f.setframerate(RATE)
        f.writeframes(data.tobytes())
    print("wrote", name)


if __name__ == "__main__":
    rng = np.random.default_rng(7)
    for kind, make in (("Wood", wood), ("Dirt", dirt), ("Water", water)):
        for i in range(1, 4):
            write(f"Impact_{kind}_{i}.wav", make(rng))
