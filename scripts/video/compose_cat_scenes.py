#!/usr/bin/env python3
"""Compose held settled-state scenes for the CAT-themed highlights cut.

Each scene = light background + rounded chat still (right) + CAT panel (left).
Outputs alt/scene_<seg>.png at full master resolution (2906x1546).
"""
from PIL import Image

W, H = 2906, 1546
VID_W, VID_H, VID_X, VID_Y = 1480, 1439, 1340, 53

SEGS = ["setup", "t1", "t2", "t3", "t4", "summary"]
PANEL = {"setup": 3.0, "t1": 20.0, "t2": 52.0, "t3": 82.0, "t4": 103.0, "summary": 116.0}

bg = Image.open("overlays_cat/background.png").convert("RGBA")
mask = Image.open("overlays_cat/mask.png").convert("L")

for seg in SEGS:
    scene = bg.copy()
    chat = Image.open(f"alt/chat_{seg}.png").convert("RGBA")
    chat.putalpha(mask)
    scene.alpha_composite(chat, (VID_X, VID_Y))
    panel = Image.open(f"overlays_cat/preview_{PANEL[seg]:.1f}.png").convert("RGBA")
    scene.alpha_composite(panel, (0, 0))
    scene.convert("RGB").save(f"alt/scene_{seg}.png")
    print("scene", seg, "ok")
