"""Small periodic ambience assets. Source and licensing are recorded alongside the project."""
from pathlib import Path
import subprocess
import wave
import numpy as np
import imageio_ffmpeg

root = Path(__file__).resolve().parents[1]
out = root / 'Assets/Stillwater/Media/Audio'
rate, seconds = 24000, 28
count = rate * seconds
t = np.arange(count) / rate
rng = np.random.default_rng(8831)

def noise(center, width):
    frequencies = np.fft.rfftfreq(count, 1/rate)
    spectrum = np.exp(-.5*((frequencies-center)/width)**2) * np.exp(2j*np.pi*rng.random(len(frequencies)))
    spectrum[0] = 0
    signal = np.fft.irfft(spectrum, n=count)
    return signal / np.std(signal)

def tone(hz):
    return np.sin(2*np.pi*round(hz*seconds)/seconds*t)

def save(name, signal):
    signal -= np.mean(signal)
    if np.max(np.abs(signal)) > .94:
        signal *= .94 / np.max(np.abs(signal))
    with wave.open(str(out / (name+'.wav')), 'wb') as output:
        output.setparams((1, 2, rate, len(signal), 'NONE', 'not compressed'))
        output.writeframes((signal * 32767).astype('<i2').tobytes())
    print(name, 'seconds', len(signal)/rate, 'RMS', round(float(np.sqrt(np.mean(signal**2))), 4), 'peak', round(float(np.max(np.abs(signal))),4))

# Rain is a separate quiet bus, with no low drones mixed into its slider.
rain = (.052*noise(1300,950) + .019*noise(4200,1800)) * (.92+.08*tone(.14))
save('rain', rain)
# Audible upper harmonics retain weight on small headset speakers without clipping.
rumble = (.22*tone(39.25)+.12*tone(58.86)+.075*tone(78.5)+.06*noise(110,65))
rumble *= .78+.13*tone(.179)+.08*tone(.321)
rumble += .022*noise(310,140) + .027*tone(156.96)*(.5+.5*tone(.071))
save('aperture', rumble)

source = root/'Assets/Stillwater/ThirdParty/Joth/The Surreal Truth.mp3'
pcm = subprocess.check_output([imageio_ffmpeg.get_ffmpeg_exe(), '-v','error','-i',str(source),'-t','32','-ac','1','-ar',str(rate),'-f','f32le','pipe:1'])
raw = np.frombuffer(pcm,dtype='<f4').copy()
if len(raw)<32*rate: raise ValueError('Source is too short')
loop=raw[:count].copy()
overlap=4*rate
blend=np.linspace(0,1,overlap,endpoint=False)
loop[:overlap]=raw[count:count+overlap]*(1-blend)+loop[:overlap]*blend
loop*=.105/max(float(np.sqrt(np.mean(loop**2))),.0001)
save('space',loop)
