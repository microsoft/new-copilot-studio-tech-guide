from reportlab.lib.pagesizes import LETTER
from reportlab.lib.units import inch
from reportlab.lib import colors
from reportlab.pdfgen import canvas

# --- confirmed values (filled from the Membership MCP get_membership call) ---
card = {
    "member_name": "Sam Sparkle",
    "member_id": "MEGA-BLAST-2048",
    "tier": "BlastPass Plus Extra MEGA!!!",
    "tier_code": "mega",            # plus | extra | mega
    "console_serial": "OMEGA-9B1C-2048",
    "member_since": "2026-05-30",   # activation_date
    "valid_through": "2027-05-30",  # term_end_date
    "status": "active",
}

# Tier styling: accent color, a short badge for the header, and the perks blurb.
TIERS = {
    "plus":  ("#3aa0ff", "PLUS",
              "Free 2-day shipping  |  5% off accessories  |  10 BlastPoints / $1"),
    "extra": ("#9b51e0", "PLUS EXTRA",
              "Free next-day shipping  |  10% off accessories  |  +20% BlastPoints bonus"),
    "mega":  ("#ff7a18", "MEGA!!!",
              "Same-day delivery  |  15% off everything  |  +50% BlastPoints  |  Day-one exclusives"),
}
accent_hex, tier_badge, perks = TIERS.get(card["tier_code"], TIERS["plus"])
accent = colors.HexColor(accent_hex)
ink = colors.HexColor("#1b1030")

# Card geometry: a standard CR80 card (3.375 x 2.125 in) centered on the page.
PAGE_W, PAGE_H = LETTER
CARD_W, CARD_H = 3.375 * inch, 2.125 * inch
x0 = (PAGE_W - CARD_W) / 2
y0 = (PAGE_H - CARD_H) / 2

c = canvas.Canvas("blastpass_card.pdf", pagesize=LETTER)

# Cut guide
c.setStrokeColor(colors.HexColor("#cccccc"))
c.setDash(2, 2)
c.rect(x0 - 6, y0 - 6, CARD_W + 12, CARD_H + 12)
c.setDash()

# Card body
c.setFillColor(ink)
c.roundRect(x0, y0, CARD_W, CARD_H, 10, stroke=0, fill=1)
# Accent header band
c.setFillColor(accent)
c.roundRect(x0, y0 + CARD_H - 0.55 * inch, CARD_W, 0.55 * inch, 10, stroke=0, fill=1)
c.rect(x0, y0 + CARD_H - 0.55 * inch, CARD_W, 0.18 * inch, stroke=0, fill=1)  # square the bottom of the band

# Brand + tier badge
c.setFillColor(colors.white)
c.setFont("Helvetica-Bold", 14)
c.drawString(x0 + 0.22 * inch, y0 + CARD_H - 0.37 * inch, "BlastBox Omega")
# Tier badge pill on the right of the header band
c.setFont("Helvetica-Bold", 8)
badge_w = c.stringWidth(tier_badge, "Helvetica-Bold", 8) + 0.18 * inch
bx_badge = x0 + CARD_W - 0.22 * inch - badge_w
c.setFillColor(colors.Color(1, 1, 1, alpha=0.22))
c.roundRect(bx_badge, y0 + CARD_H - 0.42 * inch, badge_w, 0.20 * inch, 4, stroke=0, fill=1)
c.setFillColor(colors.white)
c.drawCentredString(bx_badge + badge_w / 2, y0 + CARD_H - 0.36 * inch, tier_badge)

# Member name
c.setFillColor(colors.white)
c.setFont("Helvetica-Bold", 13)
c.drawString(x0 + 0.22 * inch, y0 + CARD_H - 0.85 * inch, card["member_name"])

# Member id (mono, spaced)
c.setFillColor(accent)
c.setFont("Courier-Bold", 12)
c.drawString(x0 + 0.22 * inch, y0 + CARD_H - 1.08 * inch, card["member_id"])

# Detail rows
details = [
    ("MEMBER SINCE", card["member_since"]),
    ("VALID THROUGH", card["valid_through"]),
    ("CONSOLE S/N", card["console_serial"]),
]
dy = y0 + CARD_H - 1.30 * inch
for label, val in details:
    c.setFillColor(colors.HexColor("#9d90bd"))
    c.setFont("Helvetica", 6)
    c.drawString(x0 + 0.22 * inch, dy, label)
    c.setFillColor(colors.white)
    c.setFont("Helvetica-Bold", 8)
    c.drawString(x0 + 1.05 * inch, dy, str(val))
    dy -= 0.17 * inch

# Faux barcode strip
bx = x0 + 0.22 * inch
by = y0 + 0.13 * inch
c.setFillColor(colors.white)
import hashlib
seed = int(hashlib.md5(card["member_id"].encode()).hexdigest(), 16)
w = 0.0
i = 0
while w < CARD_W - 0.44 * inch:
    bar = 0.012 * inch + ((seed >> (i % 32)) & 0x3) * 0.006 * inch
    if i % 2 == 0:
        c.rect(bx + w, by, bar, 0.13 * inch, stroke=0, fill=1)
    w += bar
    i += 1

# Perks footer (printed under the card, not on it)
c.setFillColor(colors.HexColor("#555555"))
c.setFont("Helvetica-Oblique", 8)
c.drawCentredString(PAGE_W / 2, y0 - 0.45 * inch, perks)
c.setFont("Helvetica", 7)
c.drawCentredString(PAGE_W / 2, y0 - 0.62 * inch,
                    "Present this card in-store or scan at checkout. Lost cards can be reprinted at any BlastBox desk.")

c.showPage()
c.save()
print("WROTE blastpass_card.pdf")
