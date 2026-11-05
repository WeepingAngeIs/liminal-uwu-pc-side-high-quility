"""Render the two original after-hours room tones as mono, periodic PCM."""
from pathlib import Path
import wave
import numpy as np

out = Path(__file__).resolve().parents[1] / "Assets/Stillwater/Media/Audio"
rate, duration = 22050, 24
count = rate * duration
time = np.arange(count) / rate
rng = np.random.default_rng(6407)

def air(centre, bandwidth):
    bins = np.fft.rfftfreq(count, 1 / rate)
    shape = np.exp(-.5 * ((bins - centre) / bandwidth)**2)
    spectrum = shape * np.exp(2j * np.pi * rng.random(len(shape)))
    waveform = np.fft.irfft(spectrum, n=count)
    return waveform / np.std(waveform)

def ring(frequency):
    return np.sin(2 * np.pi * round(frequency * duration) / duration * time)

def save(name, mix):
    with wave.open(str(out / (name + ".wav")), "wb") as file:
        file.setparams((1, 2, rate, count, "NONE", "not compressed"))
        file.writeframes((np.clip(mix, -1, 1) * 32767).astype("<i2").tobytes())

pump = .055*ring(47.5) + .022*ring(95) + .008*air(260,125)
bath = pump * (.84+.12*ring(.125))
for strike in (1.11, 5.35, 8.74, 9.15, 16.2, 18.91, 22.05):
    pitch = rng.uniform(560,820)
    for delay, gain in ((0,1),(.084,.29),(.157,.13),(.234,.06)):
        age = (time-strike-delay) % duration
        envelope = np.exp(-age*13) * (1-np.exp(-age*1900))
        bath += gain * .105 * envelope * np.sin(2*np.pi*(pitch*age+4.1*(1-np.exp(-age*17))))
save("baths", bath)
reception = (.031*ring(59.83)+.009*ring(119.66))*(.83+.17*ring(.167)) + .009*air(225,115)
for strike in (3.8,11.8,19.8):
    age = (time-strike) % duration
    reception += .043*np.exp(-age*53)*(1-np.exp(-age*800))*np.sin(2*np.pi*age*1270)
save("annex",reception)

