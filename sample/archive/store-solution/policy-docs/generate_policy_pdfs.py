#!/usr/bin/env python3
"""Generate the three whimsical BlastPass membership policy PDFs.

These are the knowledge documents attached to the Store Policy Agent in
Scenario 1. Each tier gets its own policy booklet. The refund rules embedded
here are the single source of truth that the agent reads to (a) confirm the
member's tier and (b) hand the parent agent the numbers it needs to run the
BlastPass refund calculation in Python.

Run:  python3 generate_policy_pdfs.py
Output: blastpass-plus-policy.pdf, blastpass-plus-extra-policy.pdf,
        blastpass-plus-extra-mega-policy.pdf
"""

import os

from reportlab.lib import colors
from reportlab.lib.enums import TA_CENTER, TA_LEFT
from reportlab.lib.pagesizes import LETTER
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import inch
from reportlab.platypus import (
    BaseDocTemplate,
    Frame,
    HRFlowable,
    PageTemplate,
    Paragraph,
    Spacer,
    Table,
    TableStyle,
)

HERE = os.path.dirname(os.path.abspath(__file__))

# ---------------------------------------------------------------------------
# Tier data (single source of truth - mirrors skill-scripts/blastpass_refund.py)
# ---------------------------------------------------------------------------
TIERS = [
    {
        "file": "blastpass-plus-policy.pdf",
        "name": "BlastPass Plus",
        "tagline": "The friendly on-ramp to the BlastBox Omega galaxy.",
        "annual": 79.99,
        "fee": 0.00,
        "credit": 0.00,
        "accent": colors.HexColor("#2E86DE"),
        "accent_soft": colors.HexColor("#D6EAF8"),
        "badge": "TIER 1",
        "perks": [
            "Online co-op for up to 4 BlastBuddies",
            "Monthly Mystery Cartridge (one free indie game)",
            "Cloud save vault: 25 GB of glorious save states",
            "Members-only neon controller skins",
        ],
        "fee_text": "No cancellation fee. Plus members fly free.",
    },
    {
        "file": "blastpass-plus-extra-policy.pdf",
        "name": "BlastPass Plus Extra",
        "tagline": "More games, more cloud, more BLAST. The crowd favorite.",
        "annual": 129.99,
        "fee": 10.00,
        "credit": 0.00,
        "accent": colors.HexColor("#8E44AD"),
        "accent_soft": colors.HexColor("#EBDEF0"),
        "badge": "TIER 2",
        "perks": [
            "Everything in Plus, turbo-charged",
            "The Extra Vault: 200+ classic & blockbuster titles",
            "100 GB cloud save vault",
            "Two Mystery Cartridges a month",
            "Early-access demo drops, 7 days before launch",
        ],
        "fee_text": "A $10 cancellation fee keeps the Extra Vault lights on.",
    },
    {
        "file": "blastpass-plus-extra-mega-policy.pdf",
        "name": "BlastPass Plus Extra MEGA!!!",
        "tagline": "Maximum overdrive. For the player who wants it ALL (!!!).",
        "annual": 199.99,
        "fee": 25.00,
        "credit": 20.00,
        "accent": colors.HexColor("#E67E22"),
        "accent_soft": colors.HexColor("#FDEBD0"),
        "badge": "TIER 3 - MEGA",
        "perks": [
            "Everything in Plus Extra, cranked to eleven",
            "The MEGA Vault: 600+ titles incl. day-one MEGA exclusives",
            "Unlimited cloud save vault (yes, really)",
            "Four Mystery Cartridges a month + a quarterly Loot Crate",
            "MEGA Lounge priority servers & golden controller skin",
            "One-time $20 MEGA Welcome Credit toward the BlastBox Store",
        ],
        "fee_text": (
            "A $25 cancellation fee applies. The one-time $20 MEGA Welcome "
            "Credit is non-refundable and is deducted from any prorated refund."
        ),
    },
]


def money(v):
    return f"${v:,.2f}"


def build_styles():
    styles = getSampleStyleSheet()
    styles.add(ParagraphStyle(
        name="MegaTitle", parent=styles["Title"], fontSize=30, leading=34,
        textColor=colors.white, alignment=TA_CENTER, spaceAfter=2,
    ))
    styles.add(ParagraphStyle(
        name="Tagline", parent=styles["Normal"], fontSize=12, leading=16,
        textColor=colors.white, alignment=TA_CENTER, fontName="Helvetica-Oblique",
    ))
    styles.add(ParagraphStyle(
        name="SectionH", parent=styles["Heading2"], fontSize=15, leading=18,
        spaceBefore=14, spaceAfter=6,
    ))
    styles.add(ParagraphStyle(
        name="Body", parent=styles["Normal"], fontSize=10.5, leading=15,
        alignment=TA_LEFT, spaceAfter=4,
    ))
    styles.add(ParagraphStyle(
        name="Perk", parent=styles["Normal"], fontSize=10.5, leading=15,
        leftIndent=14, spaceAfter=2, bulletIndent=2,
    ))
    styles.add(ParagraphStyle(
        name="Fine", parent=styles["Normal"], fontSize=8.5, leading=11,
        textColor=colors.HexColor("#555555"),
    ))
    styles.add(ParagraphStyle(
        name="Stamp", parent=styles["Normal"], fontSize=11, leading=14,
        textColor=colors.white, alignment=TA_CENTER, fontName="Helvetica-Bold",
    ))
    return styles


def make_header(canvas, doc, tier):
    """Colored banner across the top of every page."""
    canvas.saveState()
    w, h = LETTER
    band_h = 1.45 * inch
    canvas.setFillColor(tier["accent"])
    canvas.rect(0, h - band_h, w, band_h, stroke=0, fill=1)
    # playful pixel squares in the banner
    canvas.setFillColor(colors.white)
    canvas.setFillAlpha(0.12)
    for i, x in enumerate(range(40, int(w), 70)):
        size = 10 + (i % 3) * 6
        canvas.rect(x, h - band_h + 12 + (i % 4) * 14, size, size, stroke=0, fill=1)
    canvas.setFillAlpha(1)
    # footer
    canvas.setFillColor(tier["accent"])
    canvas.rect(0, 0, w, 0.28 * inch, stroke=0, fill=1)
    canvas.setFillColor(colors.white)
    canvas.setFont("Helvetica-Bold", 8)
    canvas.drawCentredString(
        w / 2, 0.10 * inch,
        "BlastBox Omega  -  BlastPass Membership Terms  -  Policy Ref "
        f"BP-{tier['badge'].split()[1] if len(tier['badge'].split()) > 1 else '1'}  -  v2026.1",
    )
    canvas.restoreState()


def tier_story(tier, styles):
    story = []
    # Banner content sits inside the colored band drawn by make_header.
    story.append(Spacer(1, 6))
    story.append(Paragraph(f"&#9650;&#9650;&#9650; {tier['badge']} &#9650;&#9650;&#9650;", styles["Stamp"]))
    story.append(Paragraph(tier["name"], styles["MegaTitle"]))
    story.append(Paragraph(tier["tagline"], styles["Tagline"]))
    story.append(Spacer(1, 38))

    # Pricing strip
    price_tbl = Table(
        [[
            Paragraph(f"<b>Annual price</b><br/>{money(tier['annual'])}/yr", styles["Body"]),
            Paragraph(f"<b>Monthly value</b><br/>{money(tier['annual']/12)}", styles["Body"]),
            Paragraph(f"<b>Cancellation fee</b><br/>{money(tier['fee'])}", styles["Body"]),
        ]],
        colWidths=[2.1 * inch, 2.1 * inch, 2.1 * inch],
    )
    price_tbl.setStyle(TableStyle([
        ("BACKGROUND", (0, 0), (-1, -1), tier["accent_soft"]),
        ("BOX", (0, 0), (-1, -1), 0.5, tier["accent"]),
        ("INNERGRID", (0, 0), (-1, -1), 0.5, colors.white),
        ("VALIGN", (0, 0), (-1, -1), "MIDDLE"),
        ("TOPPADDING", (0, 0), (-1, -1), 10),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 10),
        ("LEFTPADDING", (0, 0), (-1, -1), 12),
    ]))
    story.append(price_tbl)
    story.append(Spacer(1, 6))

    story.append(Paragraph("What you get", styles["SectionH"]))
    story.append(HRFlowable(width="100%", thickness=1, color=tier["accent"]))
    for perk in tier["perks"]:
        story.append(Paragraph(f"&#9642; {perk}", styles["Perk"]))

    # Refund policy - the part the agent actually reads
    story.append(Paragraph("Cancellation &amp; refund policy", styles["SectionH"]))
    story.append(HRFlowable(width="100%", thickness=1, color=tier["accent"]))
    story.append(Paragraph(
        "<b>Step one, always:</b> confirm which BlastPass tier the member holds "
        "&mdash; Plus, Plus Extra, or Plus Extra MEGA!!! &mdash; <b>before</b> "
        "quoting any refund. Refund math changes by tier.",
        styles["Body"]))
    story.append(Paragraph(
        f"<b>This document covers: {tier['name']} only.</b>", styles["Body"]))
    story.append(Spacer(1, 4))

    story.append(Paragraph(
        "<b>1. 14-day cooling-off (full refund).</b> If the membership was "
        "activated <b>14 days ago or less</b> AND the member has streamed "
        "<b>under 2 hours</b> of BlastPlay, refund the full annual price of "
        f"{money(tier['annual'])} with <b>no cancellation fee</b>.",
        styles["Body"]))
    story.append(Paragraph(
        "<b>2. Prorated cancellation (the usual case).</b> Otherwise, refund the "
        "value of the <b>whole unused months</b> remaining in the 12-month term, "
        "then subtract the tier cancellation fee:",
        styles["Body"]))
    story.append(Paragraph(
        f"&nbsp;&nbsp;&nbsp;&#9642; Monthly value = {money(tier['annual'])} &divide; 12 = "
        f"<b>{money(tier['annual']/12)}</b>",
        styles["Body"]))
    story.append(Paragraph(
        "&nbsp;&nbsp;&nbsp;&#9642; Count <b>whole</b> unused months only "
        "(every full 30 days left in the 365-day term).",
        styles["Body"]))
    story.append(Paragraph(
        f"&nbsp;&nbsp;&nbsp;&#9642; {tier['fee_text']}",
        styles["Body"]))
    if tier["credit"] > 0:
        story.append(Paragraph(
            f"<b>3. MEGA Welcome Credit.</b> Deduct the one-time "
            f"{money(tier['credit'])} non-refundable MEGA Welcome Credit from the "
            "prorated refund.",
            styles["Body"]))
    story.append(Paragraph(
        "A refund is <b>never negative</b> &mdash; if fees exceed the prorated "
        "value, the refund is $0.00.",
        styles["Body"]))

    story.append(Spacer(1, 8))
    # Worked example box
    ex = Table(
        [[Paragraph(
            "<b>Quick example.</b> A member activated this tier 100 days ago and "
            "streamed plenty. Unused days = 365 &minus; 100 = 265 &rarr; 8 whole "
            f"unused months. Refund = 8 &times; {money(tier['annual']/12)} &minus; "
            f"fee {money(tier['fee'])}"
            + (f" &minus; credit {money(tier['credit'])}" if tier["credit"] else "")
            + f" = <b>{money(max(8*(tier['annual']/12) - tier['fee'] - tier['credit'], 0))}</b>.",
            styles["Body"])]],
        colWidths=[6.5 * inch],
    )
    ex.setStyle(TableStyle([
        ("BACKGROUND", (0, 0), (-1, -1), tier["accent_soft"]),
        ("BOX", (0, 0), (-1, -1), 0.5, tier["accent"]),
        ("TOPPADDING", (0, 0), (-1, -1), 8),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 8),
        ("LEFTPADDING", (0, 0), (-1, -1), 10),
        ("RIGHTPADDING", (0, 0), (-1, -1), 10),
    ]))
    story.append(ex)

    story.append(Spacer(1, 10))
    story.append(Paragraph(
        "All amounts are illustrative and fictional. BlastBox Omega, BlastPass, "
        "BlastBuddies, Mystery Cartridge and the MEGA Lounge are invented for this "
        "Copilot Studio Modern Agent Experience demo. No real consoles were "
        "harmed in the making of this policy.",
        styles["Fine"]))
    return story


def generate(tier):
    out = os.path.join(HERE, tier["file"])
    styles = build_styles()
    doc = BaseDocTemplate(
        out, pagesize=LETTER,
        leftMargin=0.9 * inch, rightMargin=0.9 * inch,
        topMargin=0.55 * inch, bottomMargin=0.55 * inch,
        title=f"{tier['name']} - Membership Policy",
        author="BlastBox Omega",
    )
    frame = Frame(
        doc.leftMargin, doc.bottomMargin,
        doc.width, doc.height,
        id="main",
    )
    doc.addPageTemplates([
        PageTemplate(id="tier", frames=[frame],
                     onPage=lambda c, d: make_header(c, d, tier))
    ])
    doc.build(tier_story(tier, styles))
    return out


if __name__ == "__main__":
    for t in TIERS:
        path = generate(t)
        print("wrote", os.path.relpath(path, HERE))
