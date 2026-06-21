#!/usr/bin/env python3
"""Build a compact, seamlessly-looping GIF of the highlights cut.

Each settled scene is ONE long-duration frame; only the crossfades emit
short intermediate frames. A shared palette avoids inter-frame fl__.
"""
from PIL import Image

GW = 1000  # gif width
XF_FRAMES = 7
XF_MS = 70           # per crossfade frame
SCENES = [
    ("setup", 9.0),
    ("t1", 8.0),
    ("t2", 10.0),
    ("t3", 7.0),
    ("t4", 8.0),
    ("summary", 9.0),
]

def load(seg):
    im = Image.open(f"alt/scene_{seg}.png").convert("RGB")
    h = round(GW * im.height / im.width)
    return im.resize((GW, h), Image.LANCZOS)

scenes = {s: load(s) for s, _ in SCENES}

# shared palette from all scenes stacked
stack = Image.new("RGB", (GW, sum(im.height for im in scenes.values())))
y = 0
for im in scenes.values():
    stack.paste(im, (0, y)); y += im.height
pal_img = stack.quantize(colors=256, method=Image.MEDIANCUT, dither=Image.NONE)

def to_p(rgb):
    return rgb.quantize(palette=pal_img, dither=Image.FLOYDSTEINBERG)

frames, durations = [], []
n = len(SCENES)
for i, (seg, hold) in enumerate(SCENES):
    a = scenes[seg]
    frames.append(to_p(a)); durations.append(int(hold * 1000))
    nxt = scenes[SCENES[(i + 1) % n][0]]
    for k in range(1, XF_FRAMES + 1):
        alpha = k / (XF_FRAMES + 1)
        frames.append(to_p(Image.blend(a, nxt, alpha)))
        durations.append(XF_MS)

frames[0].save(
    "alt/highlights.gif", save_all=True, append_images=frames[1:],
    duration=durations, loop=0, disposal=1, optimize=False,
)
print(f"frames={len(frames)} total~{sum(durations)/1000:.1f}s")
