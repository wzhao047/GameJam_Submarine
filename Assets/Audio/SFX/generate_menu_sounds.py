"""Rebuild the original, synthesized menu sounds; no external samples required."""
import math
import random
import struct
import wave
from pathlib import Path

RATE = 48000
ROOT = Path(__file__).resolve().parents[2] / 'Resources' / 'MenuAudio'


def render(name, duration, click):
    rng = random.Random(17 if click else 11)
    samples = []
    previous_noise = 0.0
    for i in range(round(duration * RATE)):
        t = i / RATE
        attack = min(1.0, t / 0.003)
        release = min(1.0, (duration - t) / 0.014)
        envelope = attack * release * math.exp(-t / (0.032 if click else 0.019))
        frequency = 620.0 if click else 850.0
        phase = 2 * math.pi * (frequency * t + (850 if click else -650) * t * t)
        tone = math.sin(phase) + 0.12 * math.sin(2 * phase)
        noise = rng.uniform(-1, 1)
        transient = (noise - previous_noise) * math.exp(-t / 0.0018)
        previous_noise = noise
        value = 0.32 * envelope * tone + (0.10 if click else 0.035) * attack * transient
        if click and t >= 0.035:
            u = t - 0.035
            tail = min(1, u / 0.003) * release * math.exp(-u / 0.025)
            value += 0.15 * tail * math.sin(2 * math.pi * 930 * u)
        samples.append(value)
    # Keep the beginning/end silent and preserve headroom for fast navigation.
    samples[0] = samples[-1] = 0.0
    peak = max(abs(s) for s in samples)
    assert peak < 0.8
    with wave.open(str(ROOT / name), 'wb') as output:
        output.setnchannels(1)
        output.setsampwidth(2)
        output.setframerate(RATE)
        output.writeframes(b''.join(struct.pack('<h', round(s * 32767)) for s in samples))
    print(f'{name}: {duration:.3f}s, peak {peak:.3f}, 48 kHz mono PCM')


if __name__ == '__main__':
    render('Menu_Hover.wav', 0.075, False)
    render('Menu_Click.wav', 0.135, True)
