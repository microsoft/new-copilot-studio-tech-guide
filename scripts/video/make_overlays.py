#!/usr/bin/env python3
"""Block Party Trade-Up — polished demo overlays (v3).

Layout: a PERSISTENT full-height annotation card on the LEFT whose inner
content cross-fades through Setup -> Turn 1..4 -> Summary (card frame is
always present, so there are no empty moments). The chat recording is
cropped tight (no gutters), rounded-cornered and shadowed, on the RIGHT.

Outputs:
  - overlays/background.png  (light bg + soft card shadow, static)
  - overlays/mask.png        (rounded-corner alpha mask for the video)
  - overlays/seq/f*.png      (transparent left-card overlay per frame)
Composited 1:1 over trimmed.mp4 by ffmpeg.
"""
import os, sys, shutil
from PIL import Image, ImageDraw, ImageFont, ImageFilter

W, H = 2906, 1546
FPS = 30
DUR = 120.0
SEQ = "overlays/seq"

# ---- geometry ----------------------------------------------------------
MARGIN = 86
CARD_GAP = 64
# chat (right)
CROP_X, CROP_Y, CROP_W, CROP_H = 650, 0, 1590, 1546
VID_W = 1480
VID_H = round(VID_W * CROP_H / CROP_W)          # 1439
VID_X = W - MARGIN - VID_W                        # 1340
VID_Y = (H - VID_H) // 2                           # 53
VID_R = 30                                          # corner radius
# left card
LX = MARGIN
LW = VID_X - CARD_GAP - LX                          # 1190
CARD_TOP = VID_Y
CARD_H = VID_H
PAD = 54

# ---- fonts -------------------------------------------------------------
SF = "/System/Library/Fonts/SFNS.ttf"
MONO = "/System/Library/Fonts/SFNSMono.ttf"
_fc = {}
def font(size, weight="Regular", mono=False):
    k = (size, weight, mono)
    if k in _fc: return _fc[k]
    f = ImageFont.truetype(MONO if mono else SF, size)
    if not mono:
        try: f.set_variation_by_name(weight)
        except Exception: pass
    _fc[k] = f
    return f

# ---- palette -----------------------------------------------------------
ACCENT = (124, 122, 255)
BLUE   = (96, 165, 250)
EMER   = (52, 211, 153)
CARD   = (15, 20, 38)
CARD2  = (30, 38, 64)
WHITE  = (255, 255, 255)
SUB    = (203, 211, 230)
MUTE   = (150, 160, 185)
BG     = (236, 237, 242)

def A(col, a): return col + (int(255*a),)

# ---- helpers -----------------------------------------------------------
def blend_rrect(img, box, r, fill=None, outline=None, width=1):
    layer = Image.new("RGBA", img.size, (0,0,0,0))
    ImageDraw.Draw(layer).rounded_rectangle(box, radius=r, fill=fill, outline=outline, width=width)
    img.alpha_composite(layer)

def wrap(text, fnt, maxw):
    out, cur = [], ""
    for w in text.split():
        t = (cur+" "+w).strip()
        if fnt.getlength(t) <= maxw: cur = t
        else:
            if cur: out.append(cur)
            cur = w
    if cur: out.append(cur)
    return out

def ease(t):
    t = max(0.0, min(1.0, t)); return t*t*(3-2*t)

# ---- fonts used in card -----------------------------------------------
INNERW = LW - PAD*2
def fonts():
    return dict(
        kick=font(32, "Bold"), title=font(68, "Bold"),
        para=font(38, "Regular"), sub=font(27, "Bold"),
        mlab=font(35, "Semibold"), mval=font(46, "Bold"),
        slab=font(27, "Bold"), chip=font(30, "Regular", mono=True),
        alab=font(28, "Bold"), aval=font(44, "Semibold"), anote=font(29, "Regular"),
        rlab=font(28, "Bold"), rval=font(42, "Regular"),
    )
F = None

# ---- block measure/draw ------------------------------------------------
TITLE_LH, PARA_LH = 74, 51

def measure(blocks):
    h = 0
    for b in blocks:
        t = b["type"]
        if t == "kicker": h += 46
        elif t == "subhead": h += 50
        elif t == "title":
            h += len(wrap(b["text"], F["title"], INNERW))*TITLE_LH + 12
        elif t == "para":
            h += len(wrap(b["text"], F["para"], INNERW))*PARA_LH + 20
        elif t == "metrics": h += len(b["items"])*100 + 6
        elif t == "stack":
            for lab, items, kind in b["sections"]:
                h += 44
                curx = 0; rows = 1
                for it in items:
                    wseg = F["chip"].getlength(it)+44+16
                    if curx+wseg > INNERW: rows += 1; curx = 0
                    curx += wseg
                h += rows*60 + 16
        elif t == "arch": h += len(b["rows"])*120
        elif t == "results": h += len(b["rows"])*102
        elif t == "gap": h += b["h"]
    return h

def draw_blocks(img, d, x, y, blocks, a):
    ca = int(255*a)
    for b in blocks:
        t = b["type"]
        if t == "kicker":
            d.text((x, y), b["text"], font=F["kick"], fill=A(b.get("color",ACCENT), a)); y += 46
        elif t == "subhead":
            d.text((x, y+8), b["text"], font=F["sub"], fill=A(MUTE, a)); y += 50
        elif t == "title":
            for ln in wrap(b["text"], F["title"], INNERW):
                d.text((x, y), ln, font=F["title"], fill=A(WHITE, a)); y += TITLE_LH
            y += 12
        elif t == "para":
            for ln in wrap(b["text"], F["para"], INNERW):
                d.text((x, y), ln, font=F["para"], fill=A(SUB, a)); y += PARA_LH
            y += 20
        elif t == "metrics":
            y += 6
            for lab, val, pos in b["items"]:
                col = EMER if pos else WHITE
                blend_rrect(img, [x, y, x+INNERW, y+84], 16, fill=A(CARD2, 0.92*a))
                d.text((x+26, y+24), lab, font=F["mlab"], fill=A(SUB, a))
                d.text((x+INNERW-26-F["mval"].getlength(val), y+18), val, font=F["mval"], fill=A(col, a))
                y += 100
        elif t == "stack":
            for lab, items, kind in b["sections"]:
                col = {"agent":BLUE,"mcp":ACCENT,"skill":EMER}[kind]
                d.text((x, y), lab, font=F["slab"], fill=A(MUTE, a)); y += 44
                curx = x
                for it in items:
                    wseg = F["chip"].getlength(it)+44
                    if curx+wseg > x+INNERW: curx = x; y += 60
                    blend_rrect(img, [curx, y, curx+wseg, y+50], 12, fill=A(CARD2, 0.92*a), outline=A(col, 0.6*a), width=2)
                    d.text((curx+22, y+9), it, font=F["chip"], fill=A(col, a))
                    curx += wseg + 16
                y += 60 + 16
        elif t == "arch":
            for lab, val, note, col in b["rows"]:
                d.text((x, y), lab, font=F["alab"], fill=A(col, a))
                d.text((x, y+36), val, font=F["aval"], fill=A(WHITE, a))
                d.text((x, y+88), note, font=F["anote"], fill=A(MUTE, a))
                y += 120
        elif t == "results":
            for lab, val, col in b["rows"]:
                d.text((x, y), lab, font=F["rlab"], fill=A(ACCENT, a))
                d.text((x, y+36), val, font=F["rval"], fill=A(col, a))
                y += 102
        elif t == "gap":
            y += b["h"]
    return y

# ---- card frame + content ---------------------------------------------
def render_card(segblocks_alpha_pairs):
    """segblocks_alpha_pairs: list of (blocks, alpha). Card frame always full."""
    img = Image.new("RGBA", (W, H), (0,0,0,0)); d = ImageDraw.Draw(img)
    blend_rrect(img, [LX, CARD_TOP, LX+LW, CARD_TOP+CARD_H], 30, fill=A(CARD, 0.95), outline=A(ACCENT, 0.45), width=2)
    for blocks, a in segblocks_alpha_pairs:
        if a <= 0.003: continue
        h = measure(blocks)
        y0 = CARD_TOP + max(PAD, (CARD_H - h)//2)
        draw_blocks(img, d, LX+PAD, y0, blocks, a)
    return img

# ===== SEGMENT CONTENT ==================================================
def blocks_for(seg):
    if seg == "setup":
        return [
            {"type":"kicker","text":"THE SCENARIO","color":ACCENT},
            {"type":"title","text":"Block Party Trade-Up"},
            {"type":"para","text":"A customer's BlastBox console died days before the neighborhood block party. The associate just types plain-language requests — one Copilot Studio agent orchestrates the entire resolution."},
            {"type":"subhead","text":"THE STACK"},
            {"type":"arch","rows":[
                ("ORCHESTRATOR","Store Associate Assistant","generative orchestration",ACCENT),
                ("CONNECTED AGENTS","Store Policy · Inventory & Fulfillment","child agents",BLUE),
                ("MCP SERVERS","Membership MCP v2 · Order Management MCP","streamable tools",ACCENT),
                ("PYTHON SKILLS","prorated-refund · points-reconciliation · slip-pdf","code interpreter",EMER),
            ]},
        ]
    if seg == "t1":
        return [
            {"type":"kicker","text":"TURN 1 / 4"},
            {"type":"title","text":"The Ask"},
            {"type":"para","text":"The associate relays the situation: a dead BlastBox console, a block party this Saturday, and a request to cancel BlastPass."},
            {"type":"para","text":"The orchestrator fans out in parallel — policy to the Store Policy Agent, stock to the Inventory & Fulfillment Agent — then calls Membership MCP v2 to load the account and read the BlastPoints balance (4,200) itself, never asking the associate."},
            {"type":"stack","sections":[
                ("CONNECTED AGENTS",["Store Policy","Inventory & Fulfillment"],"agent"),
                ("MEMBERSHIP MCP v2",["get_membership"],"mcp"),
            ]},
        ]
    if seg == "t2":
        return [
            {"type":"kicker","text":"TURN 2 / 4"},
            {"type":"title","text":"Ruling + Live Math"},
            {"type":"para","text":"Associate confirms a manufacturing fault. The Store Policy Agent rules: free warranty swap, no restocking fee. The prorated-refund-calculator skill runs the unused-BlastPass math on the spot while Inventory confirms MEGA Edition stock."},
            {"type":"metrics","items":[
                ("Prorated BlastPass refund","$76.66",False),
                ("Net due after upgrade","$23.34",False),
            ]},
            {"type":"stack","sections":[
                ("PYTHON SKILL",["prorated-refund-calculator"],"skill"),
                ("CONNECTED AGENT",["Inventory & Fulfillment"],"agent"),
            ]},
        ]
    if seg == "t3":
        return [
            {"type":"kicker","text":"TURN 3 / 4"},
            {"type":"title","text":"Closing the Customer"},
            {"type":"para","text":"The customer is on the fence about upgrading. The orchestrator queries the Inventory & Fulfillment Agent for MEGA-Edition–exclusive titles."},
            {"type":"para","text":"It surfaces three real AAA games available only on MEGA — grounded in the live catalog, with no invented SKUs — to turn the upgrade into an easy yes."},
            {"type":"stack","sections":[
                ("CONNECTED AGENT",["Inventory & Fulfillment"],"agent"),
            ]},
        ]
    if seg == "t4":
        return [
            {"type":"kicker","text":"TURN 4 / 4"},
            {"type":"title","text":"Execute & Settle"},
            {"type":"para","text":"A single instruction drives the full settlement across both MCP servers and all three Python skills, in dependency order."},
            {"type":"metrics","items":[
                ("BlastPoints redeemed","21,400 → $105",True),
                ("Net due settled","$23.34",False),
            ]},
            {"type":"stack","sections":[
                ("ORDER MANAGEMENT MCP",["search_orders","get_order","request_return"],"mcp"),
                ("MEMBERSHIP MCP v2",["cancel_membership"],"mcp"),
                ("PYTHON SKILLS",["points-reconciliation","slip-pdf-generator"],"skill"),
            ]},
        ]
    if seg == "summary":
        return [
            {"type":"kicker","text":"RESOLVED IN ONE VISIT","color":EMER},
            {"type":"title","text":"Settled at the counter"},
            {"type":"results","rows":[
                ("DEFECTIVE CONSOLE","Free warranty swap into the MEGA Edition",WHITE),
                ("PRORATED REFUND","$76.66 applied as store credit",WHITE),
                ("NET DUE AFTER UPGRADE","$23.34",WHITE),
                ("BLASTPOINTS REDEEMED","21,400 pts  →  $105.00 off the balance",EMER),
                ("PAPERWORK","BlastPass cancelled · RA-50022 · slip PDF printed",WHITE),
            ]},
        ]
    return []

# ===== TIMELINE =========================================================
# segment containing t, plus cross-fades at boundaries
ORDER = ["setup","t1","t2","t3","t4","summary"]
BOUND = [  # (time, before, after)
    (7.0, "setup", "t1"),
    (34.0, "t1", "t2"),
    (70.0, "t2", "t3"),
    (95.0, "t3", "t4"),
    (112.0, "t4", "summary"),
]
HW = 0.35
def seg_of(t):
    s = "setup"
    for tb, b, a in BOUND:
        if t >= tb: s = a
    return s
def active(t):
    for tb, b, a in BOUND:
        if abs(t - tb) < HW:
            f = ease((t-(tb-HW))/(2*HW))
            return [(b, round(1-f,3)), (a, round(f,3))]
    return [(seg_of(t), 1.0)]

_bcache = {}
def blocks_cached(seg):
    if seg not in _bcache: _bcache[seg] = blocks_for(seg)
    return _bcache[seg]

def build(t):
    pairs = [(blocks_cached(s), a) for s, a in active(t)]
    return render_card(pairs)

# ===== STATIC ASSETS ====================================================
def make_background():
    img = Image.new("RGB", (W, H), BG).convert("RGBA")
    sh = Image.new("RGBA", (W, H), (0,0,0,0))
    for box, rad, col in [
        ([VID_X+8, VID_Y+20, VID_X+VID_W+8, VID_Y+VID_H+30], 40, (20,24,45,120)),     # chat shadow
        ([LX+6, CARD_TOP+18, LX+LW+6, CARD_TOP+CARD_H+26], 36, (20,24,45,110)),         # left card shadow
    ]:
        ImageDraw.Draw(sh).rounded_rectangle(box, radius=rad, fill=col)
    sh = sh.filter(ImageFilter.GaussianBlur(30))
    img.alpha_composite(sh)
    img.convert("RGB").save("overlays/background.png")
    print("bg ok | chat", VID_X, VID_Y, VID_W, VID_H, "| left", LX, CARD_TOP, LW, CARD_H)

def make_mask():
    m = Image.new("L", (VID_W, VID_H), 0)
    ImageDraw.Draw(m).rounded_rectangle([0, 0, VID_W-1, VID_H-1], radius=VID_R, fill=255)
    m.save("overlays/mask.png")
    print("mask ok", VID_W, VID_H, "r", VID_R)

# ===== MAIN =============================================================
def main():
    global F
    F = fonts()
    if len(sys.argv) >= 2 and sys.argv[1] == "assets":
        os.makedirs("overlays", exist_ok=True); make_background(); make_mask(); return
    if len(sys.argv) >= 3 and sys.argv[1] == "preview":
        t = float(sys.argv[2]); build(t).save(f"overlays/preview_{t:.1f}.png")
        print("wrote", f"overlays/preview_{t:.1f}.png"); return
    os.makedirs(SEQ, exist_ok=True)
    n = int(DUR*FPS); cache = {}
    for i in range(n):
        t = i/FPS; sig = tuple(active(t)); path = f"{SEQ}/f{i:05d}.png"
        if sig in cache: shutil.copy(cache[sig], path)
        else:
            build(t).save(path); cache[sig] = path
        if i % 450 == 0: print(f"frame {i}/{n} t={t:.1f} {sig} unique={len(cache)}")
    print("done", n, "frames, unique", len(cache))

if __name__ == "__main__":
    main()
