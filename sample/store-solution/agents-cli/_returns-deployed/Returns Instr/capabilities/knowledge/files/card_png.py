#!/usr/bin/env python3
"""Render a BlastPass membership card to a PNG.

Bundled skill script: the agent runs this with the member's confirmed values
(from the Membership MCP get_membership + reissue_card results) as CLI flags. The
script only *draws* the card — it never looks anything up or invents data.

Usage:
    python3 card_png.py \
        --member-name "Jordan Pixel" \
        --member-id MEGA-BLAST-1024 \
        --tier "BlastPass Plus Extra" \
        --tier-code extra \
        --card-serial BLAST-A1B2-1024 \
        --console-serial OMEGA-7F3A-1024 \
        --member-since 2026-02-26 \
        --valid-through 2027-02-26 \
        [--out blastpass_card.png]

--card-serial must be the NEW serial from reissue_card (new_card_serial) on a
reissue, otherwise the card_serial on file from get_membership.
"""

import argparse
import hashlib
import sys

import matplotlib
matplotlib.use("Agg")  # headless: render straight to a file, no display needed
import matplotlib.pyplot as plt
from matplotlib.patches import FancyBboxPatch, Rectangle

# Tier styling: accent color, header badge text, perks blurb (printed under card)
TIERS = {
    "plus":  ("#3aa0ff", "PLUS",
              "Free 2-day shipping   |   5% off accessories   |   10 BlastPoints / $1"),
    "extra": ("#9b51e0", "PLUS EXTRA",
              "Free next-day shipping   |   10% off accessories   |   +20% BlastPoints bonus"),
    "mega":  ("#ff7a18", "MEGA!!!",
              "Same-day delivery   |   15% off everything   |   +50% BlastPoints   |   Day-one exclusives"),
}
INK = "#1b1030"
MUTED = "#9d90bd"


def parse_args(argv):
    p = argparse.ArgumentParser(description="Render a BlastPass membership card PNG.")
    p.add_argument("--member-name", required=True)
    p.add_argument("--member-id", required=True)
    p.add_argument("--tier", required=True, help='e.g. "BlastPass Plus Extra"')
    p.add_argument("--tier-code", required=True, choices=["plus", "extra", "mega"])
    p.add_argument("--card-serial", required=True,
                   help="NEW serial from reissue_card, else card_serial on file")
    p.add_argument("--console-serial", required=True)
    p.add_argument("--member-since", required=True, help="activation_date (YYYY-MM-DD)")
    p.add_argument("--valid-through", required=True, help="term_end_date (YYYY-MM-DD)")
    p.add_argument("--out", default="blastpass_card.png")
    return p.parse_args(argv)


def render(card, out_path):
    accent, tier_badge, perks = TIERS.get(card["tier_code"], TIERS["plus"])

    # Card geometry in inches (CR80 ratio 3.375 x 2.125), drawn on a small canvas.
    CARD_W, CARD_H = 3.375, 2.125
    fig_w, fig_h = CARD_W + 1.4, CARD_H + 1.0
    fig = plt.figure(figsize=(fig_w, fig_h), dpi=300)
    ax = fig.add_axes([0, 0, 1, 1])
    ax.set_xlim(0, fig_w)
    ax.set_ylim(0, fig_h)
    ax.axis("off")

    x0 = (fig_w - CARD_W) / 2
    y0 = (fig_h - CARD_H) / 2 + 0.18  # nudge up to leave room for perks footer

    # Card body (rounded)
    ax.add_patch(FancyBboxPatch(
        (x0, y0), CARD_W, CARD_H,
        boxstyle="round,pad=0,rounding_size=0.12",
        linewidth=0, facecolor=INK, zorder=1))

    # Accent header band (rounded top, squared bottom via an overlapping rectangle)
    band_h = 0.55
    ax.add_patch(FancyBboxPatch(
        (x0, y0 + CARD_H - band_h), CARD_W, band_h,
        boxstyle="round,pad=0,rounding_size=0.12",
        linewidth=0, facecolor=accent, zorder=2))
    ax.add_patch(Rectangle(
        (x0, y0 + CARD_H - band_h), CARD_W, band_h * 0.45,
        linewidth=0, facecolor=accent, zorder=2))

    pad = 0.22
    # Brand
    ax.text(x0 + pad, y0 + CARD_H - 0.34, "BlastBox Omega",
            color="white", fontsize=12, fontweight="bold", va="center", zorder=3)
    # Tier badge pill (right side of band)
    badge_w = 0.16 + 0.085 * len(tier_badge)
    bx = x0 + CARD_W - pad - badge_w
    ax.add_patch(FancyBboxPatch(
        (bx, y0 + CARD_H - 0.45), badge_w, 0.26,
        boxstyle="round,pad=0,rounding_size=0.06",
        linewidth=0, facecolor="white", alpha=0.22, zorder=3))
    ax.text(bx + badge_w / 2, y0 + CARD_H - 0.32, tier_badge,
            color="white", fontsize=7.5, fontweight="bold",
            ha="center", va="center", zorder=4)

    # Member name
    ax.text(x0 + pad, y0 + CARD_H - 0.80, card["member_name"],
            color="white", fontsize=13.5, fontweight="bold", va="center", zorder=3)
    # Card serial (mono, accent)
    ax.text(x0 + pad, y0 + CARD_H - 1.06, card["card_serial"],
            color=accent, fontsize=12.5, fontweight="bold",
            family="monospace", va="center", zorder=3)

    # Detail rows
    details = [
        ("MEMBER ID", card["member_id"]),
        ("MEMBER SINCE", card["member_since"]),
        ("VALID THROUGH", card["valid_through"]),
        ("CONSOLE S/N", card["console_serial"]),
    ]
    dy = y0 + CARD_H - 1.30
    for label, val in details:
        ax.text(x0 + pad, dy, label, color=MUTED, fontsize=6, va="center", zorder=3)
        ax.text(x0 + pad + 0.95, dy, str(val), color="white", fontsize=8,
                fontweight="bold", va="center", zorder=3)
        dy -= 0.165

    # Faux barcode strip (deterministic from the card serial)
    seed = int(hashlib.md5(card["card_serial"].encode()).hexdigest(), 16)
    bx2 = x0 + pad
    by2 = y0 + 0.14
    bar_h = 0.14
    w = 0.0
    i = 0
    max_w = CARD_W - 2 * pad
    while w < max_w:
        bar = 0.012 + ((seed >> (i % 32)) & 0x3) * 0.006
        if i % 2 == 0:
            ax.add_patch(Rectangle((bx2 + w, by2), bar, bar_h,
                                   linewidth=0, facecolor="white", zorder=3))
        w += bar
        i += 1

    # Perks footer (under the card) — shrink font if the perks line is long
    perks_fs = 6.5 if len(perks) <= 80 else 5.6
    ax.text(fig_w / 2, y0 - 0.16, perks, color="#666666", fontsize=perks_fs,
            style="italic", ha="center", va="center")
    ax.text(fig_w / 2, y0 - 0.34,
            "Present this card in-store or scan at checkout. "
            "A printed copy is on its way by mail.",
            color="#888888", fontsize=6.5, ha="center", va="center")

    fig.savefig(out_path, dpi=300, facecolor="white")
    plt.close(fig)


def main(argv):
    args = parse_args(argv)
    card = {
        "member_name": args.member_name,
        "member_id": args.member_id,
        "tier": args.tier,
        "tier_code": args.tier_code,
        "card_serial": args.card_serial,
        "console_serial": args.console_serial,
        "member_since": args.member_since,
        "valid_through": args.valid_through,
    }
    render(card, args.out)
    print(f"WROTE {args.out}")
    print(f"  {card['member_name']} · {card['tier']} · card {card['card_serial']}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
