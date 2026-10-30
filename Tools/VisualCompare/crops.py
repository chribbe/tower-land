import sys
from PIL import Image
a = Image.open(sys.argv[1]).convert('RGB'); b = Image.open(sys.argv[2]).convert('RGB').resize(a.size)
W, H = a.size
boxes = [(0, 0.12, 0.45, 0.45), (0.3, 0.2, 0.7, 0.45), (0.0, 0.55, 0.5, 0.9)]  # sky/mountains, cubes, ground/rock
rows = []
for (x0, y0, x1, y1) in boxes:
    box = (int(x0*W), int(y0*H), int(x1*W), int(y1*H))
    ca, cb = a.crop(box), b.crop(box)
    row = Image.new('RGB', (ca.width*2 + 10, ca.height), 'white'); row.paste(ca, (0, 0)); row.paste(cb, (ca.width + 10, 0))
    rows.append(row)
out = Image.new('RGB', (max(r.width for r in rows), sum(r.height for r in rows) + 20), 'white')
y = 0
for r in rows: out.paste(r, (0, y)); y += r.height + 10
out.save(sys.argv[3])
