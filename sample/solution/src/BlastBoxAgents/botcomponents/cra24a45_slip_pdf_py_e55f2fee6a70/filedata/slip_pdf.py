#!/usr/bin/env python3
"""BlastBox Returns & Service slip -> PDF renderer.

Renders the printable return / RMA / exchange slip that closes out a transaction
at the returns desk. All amounts and labels are passed in as a single JSON blob
of already-confirmed values; this script only lays them out and writes the PDF.

Usage:
    python3 slip_pdf.py --slip-json '<json>' [--output blastbox_slip.pdf]
    python3 slip_pdf.py --slip-file slip.json [--output blastbox_slip.pdf]

The JSON object accepts these fields (omit any that do not apply):
    slip_type      str   e.g. "EXCHANGE / UPGRADE", "RETURN / REFUND",
                         "WARRANTY SWAP", "MEMBERSHIP CANCELLATION"
    rma_ref        str   RMA / reference number
    date           str   YYYY-MM-DD
    store          str   store name / number
    associate      str   associate name or desk
    customer_name  str
    member_id      str   BlastPass member id
    member_tier    str   BlastPass tier
    items          list  of [item, sku, disposition, amount] rows
    settlement     list  of [label, amount] rows
    net_label      str   e.g. "NET DUE FROM CUSTOMER" / "REFUND TO CUSTOMER"
    net_amount     str   e.g. "$23.34"
    notes          str   free-text notes

Every value comes from earlier steps (Store Policy Agent ruling, Membership MCP,
Warehouse MCP, and the prorated-refund-calculator skill). This script never
decides or invents amounts.
"""

import argparse
import json
import sys

ACCENT = "#6b2fb5"
ROW_ALT = "#f3eefb"


def _esc(text):
    """Escape ampersands for reportlab Paragraph markup."""
    return str(text).replace("&", "&amp;")


def build_pdf(slip, output):
    from reportlab.lib.pagesizes import LETTER
    from reportlab.lib.units import inch
    from reportlab.lib import colors
    from reportlab.platypus import (SimpleDocTemplate, Paragraph, Spacer, Table,
                                     TableStyle, HRFlowable)
    from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle

    styles = getSampleStyleSheet()
    h1 = ParagraphStyle("h1", parent=styles["Title"], fontSize=18, spaceAfter=6)
    h2 = ParagraphStyle("h2", parent=styles["Heading2"], fontSize=12,
                        spaceBefore=10, spaceAfter=4)
    body = styles["BodyText"]

    doc = SimpleDocTemplate(output, pagesize=LETTER,
                            topMargin=0.7 * inch, bottomMargin=0.7 * inch)
    flow = []
    flow.append(Paragraph("BlastBox - Returns &amp; Service Slip", h1))
    flow.append(HRFlowable(width="100%", color=colors.HexColor(ACCENT)))

    meta = (f"<b>Slip type:</b> {_esc(slip.get('slip_type', ''))} &nbsp;&nbsp; "
            f"<b>RMA / Ref #:</b> {_esc(slip.get('rma_ref', ''))}<br/>"
            f"<b>Date:</b> {_esc(slip.get('date', ''))} &nbsp;&nbsp; "
            f"<b>Store / Associate:</b> {_esc(slip.get('store', ''))} / "
            f"{_esc(slip.get('associate', ''))}")
    flow.append(Paragraph(meta, body))

    if slip.get("customer_name"):
        flow.append(Paragraph("Customer", h2))
        flow.append(Paragraph(
            f"{_esc(slip.get('customer_name', ''))} &nbsp;|&nbsp; "
            f"BlastPass {_esc(slip.get('member_id', ''))} "
            f"({_esc(slip.get('member_tier', ''))})", body))

    items = slip.get("items") or []
    if items:
        flow.append(Paragraph("Items", h2))
        item_data = [["Item", "SKU", "Disposition", "Amount"]] + [list(r) for r in items]
        t = Table(item_data, colWidths=[2.6 * inch, 1.3 * inch, 1.5 * inch, 1.0 * inch])
        t.setStyle(TableStyle([
            ("BACKGROUND", (0, 0), (-1, 0), colors.HexColor(ACCENT)),
            ("TEXTCOLOR", (0, 0), (-1, 0), colors.white),
            ("FONTSIZE", (0, 0), (-1, -1), 9),
            ("GRID", (0, 0), (-1, -1), 0.5, colors.grey),
            ("ALIGN", (-1, 0), (-1, -1), "RIGHT"),
            ("ROWBACKGROUNDS", (0, 1), (-1, -1), [colors.white, colors.HexColor(ROW_ALT)]),
        ]))
        flow.append(t)

    settlement = slip.get("settlement") or []
    if settlement:
        flow.append(Paragraph("Settlement", h2))
        for label, amount in settlement:
            flow.append(Paragraph(f"{_esc(label)}: <b>{_esc(amount)}</b>", body))

    if slip.get("net_label") or slip.get("net_amount"):
        flow.append(Spacer(1, 6))
        flow.append(Paragraph(
            f"<b>{_esc(slip.get('net_label', ''))}: "
            f"{_esc(slip.get('net_amount', ''))}</b>", h2))

    if slip.get("notes"):
        flow.append(Paragraph("Notes", h2))
        flow.append(Paragraph(_esc(slip["notes"]), body))

    flow.append(Spacer(1, 10))
    flow.append(Paragraph("<i>Keep this slip for your records. Warranty swaps carry "
                          "the remaining original warranty.</i>", body))

    doc.build(flow)


def main(argv):
    parser = argparse.ArgumentParser(description="Render a BlastBox return/RMA slip PDF.")
    src = parser.add_mutually_exclusive_group(required=True)
    src.add_argument("--slip-json", help="Slip data as a JSON string.")
    src.add_argument("--slip-file", help="Path to a JSON file with the slip data.")
    parser.add_argument("--output", default="blastbox_slip.pdf",
                        help="Output PDF path (default: blastbox_slip.pdf).")
    args = parser.parse_args(argv[1:])

    try:
        if args.slip_json:
            slip = json.loads(args.slip_json)
        else:
            with open(args.slip_file, "r", encoding="utf-8") as fh:
                slip = json.load(fh)
    except (ValueError, OSError) as exc:
        print(f"Error: could not read slip JSON ({exc}).")
        return 1

    try:
        build_pdf(slip, args.output)
    except ImportError:
        print("Error: reportlab is required to render the slip PDF.")
        return 1

    print(f"WROTE {args.output}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
