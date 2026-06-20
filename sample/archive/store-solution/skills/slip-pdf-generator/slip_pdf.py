from reportlab.lib.pagesizes import LETTER
from reportlab.lib.units import inch
from reportlab.lib import colors
from reportlab.platypus import (SimpleDocTemplate, Paragraph, Spacer, Table,
                                TableStyle, HRFlowable)
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle

# --- confirmed values (fill these in) ---
slip = {
    "slip_type": "EXCHANGE / UPGRADE",
    "rma_ref": "RMA-OMEGA-1024",
    "date": "2026-06-07",
    "store": "BlastBox #214 — Seattle",
    "associate": "Associate Desk",
    "customer_name": "Jordan Pixel",
    "member_id": "MEGA-BLAST-1024",
    "member_tier": "BlastPass Plus Extra",
    "items": [
        # (item, sku, disposition, amount)
        ("BlastBox Omega (defective)", "SKU-OMEGA", "Warranty credit", "$399.99"),
        ("BlastBox Omega MEGA Edition", "SKU-OMEGA-MEGA", "Upgrade (new)", "$499.99"),
    ],
    "settlement": [
        ("Upgrade difference ($499.99 - $399.99)", "$100.00"),
        ("BlastPass Plus Extra refund applied as store credit", "-$76.66"),
    ],
    "net_label": "NET DUE FROM CUSTOMER",
    "net_amount": "$23.34",
    "notes": ("Manufacturing defect confirmed within 30 days - no restocking fee. "
              "'MEGA Lizards from Outer Space' requires the MEGA Edition. "
              "BlastPass Plus Extra (MEGA-BLAST-1024) cancelled; 8 unused months prorated."),
}

styles = getSampleStyleSheet()
h1 = ParagraphStyle("h1", parent=styles["Title"], fontSize=18, spaceAfter=6)
h2 = ParagraphStyle("h2", parent=styles["Heading2"], fontSize=12, spaceBefore=10, spaceAfter=4)
body = styles["BodyText"]

doc = SimpleDocTemplate("blastbox_slip.pdf", pagesize=LETTER,
                        topMargin=0.7*inch, bottomMargin=0.7*inch)
flow = []
flow.append(Paragraph("BlastBox - Returns &amp; Service Slip", h1))
flow.append(HRFlowable(width="100%", color=colors.HexColor("#6b2fb5")))
meta = (f"<b>Slip type:</b> {slip['slip_type']} &nbsp;&nbsp; "
        f"<b>RMA / Ref #:</b> {slip['rma_ref']}<br/>"
        f"<b>Date:</b> {slip['date']} &nbsp;&nbsp; "
        f"<b>Store / Associate:</b> {slip['store']} / {slip['associate']}")
flow.append(Paragraph(meta, body))

flow.append(Paragraph("Customer", h2))
flow.append(Paragraph(f"{slip['customer_name']} &nbsp;|&nbsp; "
                      f"BlastPass {slip['member_id']} ({slip['member_tier']})", body))

flow.append(Paragraph("Items", h2))
item_data = [["Item", "SKU", "Disposition", "Amount"]] + [list(r) for r in slip["items"]]
t = Table(item_data, colWidths=[2.6*inch, 1.3*inch, 1.5*inch, 1.0*inch])
t.setStyle(TableStyle([
    ("BACKGROUND", (0, 0), (-1, 0), colors.HexColor("#6b2fb5")),
    ("TEXTCOLOR", (0, 0), (-1, 0), colors.white),
    ("FONTSIZE", (0, 0), (-1, -1), 9),
    ("GRID", (0, 0), (-1, -1), 0.5, colors.grey),
    ("ALIGN", (-1, 0), (-1, -1), "RIGHT"),
    ("ROWBACKGROUNDS", (0, 1), (-1, -1), [colors.white, colors.HexColor("#f3eefb")]),
]))
flow.append(t)

flow.append(Paragraph("Settlement", h2))
for label, amount in slip["settlement"]:
    flow.append(Paragraph(f"{label}: <b>{amount}</b>", body))
flow.append(Spacer(1, 6))
flow.append(Paragraph(f"<b>{slip['net_label']}: {slip['net_amount']}</b>", h2))

flow.append(Paragraph("Notes", h2))
flow.append(Paragraph(slip["notes"], body))
flow.append(Spacer(1, 10))
flow.append(Paragraph("<i>Keep this slip for your records. Warranty swaps carry "
                      "the remaining original warranty.</i>", body))

doc.build(flow)
print("WROTE blastbox_slip.pdf")
