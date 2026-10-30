"""Generates Art/Textures/Ground_Detail.png: tileable noise channels for the meadow ground shader.
R = fine grass speckle, G = large soft patches, B = medium variation. Mean ~0.5 (0.5 = no change)."""
import numpy as np
from PIL import Image

N = 512
rng = np.random.default_rng(7)

def periodic_noise(sigma, aniso=(1.0, 1.0)):
    """White noise low-passed with a Gaussian in frequency space -> seamlessly tileable."""
    noise = rng.standard_normal((N, N))
    fy = np.fft.fftfreq(N)[:, None] * aniso[1]
    fx = np.fft.fftfreq(N)[None, :] * aniso[0]
    gauss = np.exp(-2 * (np.pi * sigma) ** 2 * (fx ** 2 + fy ** 2))
    out = np.real(np.fft.ifft2(np.fft.fft2(noise) * gauss))
    out -= out.mean()
    return out / (out.std() + 1e-9)

def to_channel(x, contrast):
    return np.clip(0.5 + x * contrast, 0, 1)

fine = periodic_noise(0.9) * 0.7 + periodic_noise(2.5) * 0.5 + periodic_noise(1.2, (1.0, 0.35)) * 0.4  # blade-ish streaks
r = to_channel(fine / fine.std(), 0.16)
g = to_channel(periodic_noise(48), 0.18)
b = to_channel(periodic_noise(14), 0.17)
img = np.stack([r, g, b], axis=-1)
Image.fromarray((img * 255).astype(np.uint8), 'RGB').save(r'D:/ASG/TowerLand/Assets/TowerLand/Art/Textures/Ground_Detail.png')
print('ok', r.mean(), g.mean(), b.mean())
