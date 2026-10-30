import sys
from PIL import Image
render, mockup, out = sys.argv[1], sys.argv[2], sys.argv[3]
a = Image.open(render).convert('RGB'); b = Image.open(mockup).convert('RGB').resize(a.size)
w, h = a.size
side = Image.new('RGB', (w * 3, h))
side.paste(a, (0, 0)); side.paste(b, (w, 0)); side.paste(Image.blend(a, b, 0.5), (w * 2, 0))
side = side.resize((w * 3 * 2 // 3, h * 2 // 3))
side.save(out)
