#!/usr/bin/env python3
"""Build the CAT-themed 'key moments' highlights cut.

Holds each settled-state scene, crossfading between them. Keeps the original
continuous video untouched. Outputs alt/highlights-master.mp4 (1080p-ish).
"""
import subprocess, os

os.environ["PATH"] = "/opt/homebrew/bin:" + os.environ.get("PATH", "")

OW, OH, FPS, T = 1920, 1022, 30, 0.7
# (scene, hold seconds)
SCENES = [
    ("setup", 10.0),
    ("t1", 9.0),
    ("t2", 11.0),
    ("t3", 8.0),
    ("t4", 9.0),
    ("summary", 10.0),
]

inputs = []
for seg, d in SCENES:
    inputs += ["-loop", "1", "-t", f"{d:.3f}", "-framerate", str(FPS),
               "-i", f"alt/scene_{seg}.png"]

# pre-process each input: scale + fps + format
pre = []
for i in range(len(SCENES)):
    pre.append(
        f"[{i}:v]scale={OW}:{OH}:force_original_aspect_ratio=decrease,"
        f"pad={OW}:{OH}:(ow-iw)/2:(oh-ih)/2:color=0xECEDF2,"
        f"fps={FPS},format=yuv420p,setsar=1[v{i}]"
    )

# xfade chain
chain = []
prev = "v0"
acc = SCENES[0][1]
for k in range(1, len(SCENES)):
    off = acc - T
    out = f"x{k}" if k < len(SCENES) - 1 else "vout"
    chain.append(
        f"[{prev}][v{k}]xfade=transition=fade:duration={T}:offset={off:.3f}[{out}]"
    )
    acc += SCENES[k][1] - T
    prev = out

fc = ";".join(pre + chain)
total = sum(d for _, d in SCENES) - (len(SCENES) - 1) * T
print(f"total duration ~ {total:.1f}s")

cmd = ["ffmpeg", "-y", *inputs, "-filter_complex", fc,
       "-map", "[vout]", "-c:v", "libx264", "-crf", "20",
       "-preset", "medium", "-pix_fmt", "yuv420p", "-movflags", "+faststart",
       "alt/highlights-master.mp4"]
subprocess.run(cmd, check=True)
print("wrote alt/highlights-master.mp4")
