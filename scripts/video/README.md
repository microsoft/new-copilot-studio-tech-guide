# Demo video tooling

Scripts that produced the looping flagship demo video on the **Block Party
Trade-Up** scenario page (`src/pages/scenarios/block-party-trade-up.astro`).

These are documented for **reuse of the technique**, not as a turnkey tool. The
crop box, segment timings, and callout copy are all measured against one
specific screen recording. Re-recording the agent (different window size,
scroll positions, or conversation) means re-measuring the crop and re-timing
every callout.

> **Source recordings stay out of the repo.** The raw capture (~100MB `.mov`),
> `trimmed.mp4`, `final.mp4`, and the generated frame sequences are large and
> are intentionally not committed. Only these scripts and the final web asset
> (`public/video/*.mp4` + poster) live in git.

---

## What you get

Two render paths share the same overlay generator:

1. **Continuous cut** — the full 2-minute walkthrough, chat on the right with a
   persistent annotation card on the left whose content cross-fades per turn.
2. **Highlights cut** (what currently ships) — holds each turn's *settled*
   state and crossfades between them, ~54s. Sharper and shorter; embedded as a
   looping `<video autoplay loop muted playsinline>` so it repeats like a GIF
   but stays crisp.

Each path comes in two palettes:

| Generator | Palette | Use |
| --- | --- | --- |
| `make_overlays.py` | dark / near-black card | BlastBox theme |
| `make_overlays_cat.py` | light / white card | CAT theme (default) |

The CAT variant maps to the site palette: blue `#0078D4` (connected agents),
purple `#7C3AED` (orchestrator / MCP), green `#16A34A` (Python skills). All
primary text uses the `WHITE` constant, so flipping `WHITE` to a dark slate is
what turns the whole panel into the light skin.

---

## Prerequisites

| Tool | Notes |
| --- | --- |
| **ffmpeg** | trim, crop, alpha-merge corner rounding, overlay compositing, H.264, xfade |
| **Python 3 + Pillow** | draws every annotation frame, the rounded-corner mask, the shadows |
| **gifsicle** | only if you also want the (lower-quality) GIF fallback |
| **SF Pro / SF Mono** | macOS system fonts (`/System/Library/Fonts/SFNS.ttf`, `SFNSMono.ttf`) |

On macOS with Homebrew: `brew install ffmpeg gifsicle` and `pip install pillow`.

All scripts use **relative paths** and expect to run from a working directory
that contains your `trimmed.mp4` (and where `overlays*/`, `alt/`, output files
get written).

---

## Pipeline

```
recording.mov
   │  ① TRIM dead-air (tool-call spinners) → trimmed.mp4 (no audio)
   ▼
trimmed.mp4   ← reused unchanged by every render
   │  ② OVERLAYS  → background.png, mask.png, per-segment panels
   ▼
   ├─③a CONTINUOUS  ffmpeg composite over every frame → final.mp4
   └─③b HIGHLIGHTS  settled stills + crossfades → highlights-master.mp4
            │  ④ (optional) → looping GIF
            ▼
   copy MP4 + poster into public/video/
```

### ① Trim

Drop the 30–40s tool-call spinners between turns (per-second frame-difference
motion analysis works well) so the timeline is just the moments the UI changes.
Result: `trimmed.mp4` (the reference footage every render reuses).

### ② Overlays

```bash
# assets: light/dark page background (+ drop shadows) and the rounded mask
python3 make_overlays_cat.py assets       # → overlays_cat/{background,mask}.png

# single settled panel for one segment (any time inside the segment, away from
# a boundary, renders that segment's card at full alpha)
python3 make_overlays_cat.py preview 52    # → overlays_cat/preview_52.0.png

# full per-frame sequence (only needed for the CONTINUOUS cut)
python3 make_overlays.py                    # → overlays/seq/f%05d.png
```

Segment content lives in `blocks_for()`; the six segments are
`setup, t1, t2, t3, t4, summary`. Boundaries and the cross-fade timeline are in
`BOUND` / `active()`. Edit copy there.

### ③a Continuous composite (ffmpeg)

```bash
ffmpeg -y \
  -loop 1 -i overlays/background.png \
  -i trimmed.mp4 \
  -i overlays/mask.png \
  -framerate 30 -i overlays/seq/f%05d.png \
  -filter_complex "
    [1:v]crop=1590:1546:650:0,scale=1480:1439,fps=30[v];
    [2:v]format=gray[m];
    [v][m]alphamerge[vr];
    [0:v][vr]overlay=1340:53[b];
    [b][3:v]overlay=0:0:shortest=1,format=yuv420p[o]
  " -map "[o]" -t 120 -c:v libx264 -preset medium -crf 20 -r 30 -an final.mp4
```

The crop box `1590:1546:650:0`, the chat placement `overlay=1340:53`, and the
scale `1480:1439` are all measured for the reference recording. Re-measure if
you re-record.

### ③b Highlights cut (what ships)

1. Pick a **settled-state time** per turn (the chat fully rendered, input box
   clean, nothing mid-typing) by extracting candidate frames and eyeballing
   them. Put the chat-still times in the extraction loop and the panel times in
   `compose_cat_scenes.py` (`PANEL`).
2. Extract + crop each chat still from `trimmed.mp4`:
   ```bash
   ffmpeg -y -ss <t> -i trimmed.mp4 -frames:v 1 \
     -vf "crop=1590:1546:650:0,scale=1480:1439" alt/chat_<seg>.png
   ```
3. Composite each scene (background + rounded chat still + CAT panel):
   ```bash
   python3 compose_cat_scenes.py            # → alt/scene_<seg>.png
   ```
4. Build the crossfade video (hold durations + transition in `SCENES`/`T`):
   ```bash
   python3 build_highlights.py              # → alt/highlights-master.mp4
   ```
5. Copy into the site and regenerate the poster:
   ```bash
   cp alt/highlights-master.mp4 ../../public/video/block-party-trade-up-highlights.mp4
   ffmpeg -y -i alt/scene_setup.png -vf scale=1920:-1 -frames:v 1 \
     ../../public/video/block-party-trade-up-highlights-poster.jpg
   ```

### ④ Optional GIF

`gif_build.py` renders each held scene as a single long-duration frame plus
short crossfade frames on a shared palette, then you optimise with
`gifsicle -O3 --lossy`. **Prefer the looping MP4** — a 256-color GIF is noticeably
softer on text. Kept only as a no-`<video>` fallback technique.

---

## Embedding on the site

```astro
<video class="demo-video__player" autoplay loop muted playsinline preload="auto"
       poster={`${base}video/<name>-poster.jpg`}>
  <source src={`${base}video/<name>.mp4`} type="video/mp4" />
</video>
```

`autoplay` requires `muted`; `loop` repeats it; no `controls` gives the GIF-like
feel. The poster covers low-power modes that defer autoplay.

---

## PIL gotchas

- A partial-alpha `ImageDraw` fill **replaces** alpha (punches a hole) rather
  than blending. The `blend_rrect()` helper draws onto a separate transparent
  layer and `alpha_composite`s it back. Used for every translucent rounded rect.
- Square video corners can't be rounded by drawing over them. Use a real
  grayscale rounded-rect mask (`mask.png`) applied with ffmpeg `alphamerge`.
