#!/usr/bin/env python3
"""Render a customer-facing curbside bay recommendation as a one-page PDF."""

import argparse
from pathlib import Path
import sys


ACCENT = "#6b2fb5"
LIGHT_ACCENT = "#f3eefb"


def _escape(value):
    return (
        str(value)
        .replace("&", "&amp;")
        .replace("<", "&lt;")
        .replace(">", "&gt;")
    )


def _required_text(value, flag):
    cleaned = value.strip()
    if not cleaned:
        raise ValueError(f"{flag} must not be empty")
    return cleaned


def _nonnegative_int(value):
    parsed = int(value)
    if parsed < 0:
        raise argparse.ArgumentTypeError("must be zero or greater")
    return parsed


def _queue_text(orders_ahead):
    if orders_ahead == 0:
        return "There are no orders ahead."
    if orders_ahead == 1:
        return "There is one order ahead."
    return f"There are {orders_ahead} orders ahead."


def _duration_text(seconds):
    minutes, remaining_seconds = divmod(seconds, 60)
    parts = []
    if minutes:
        parts.append(f"{minutes} minute" + ("" if minutes == 1 else "s"))
    if remaining_seconds:
        parts.append(
            f"{remaining_seconds} second"
            + ("" if remaining_seconds == 1 else "s")
        )
    return " ".join(parts) if parts else "no additional wait"


def _eta_text(wait_seconds, eta_minutes):
    if eta_minutes is None:
        return None
    margin = eta_minutes * 60 - wait_seconds
    if margin > 0:
        return (
            f"Expected arrival: {eta_minutes} minutes. "
            f"Buffer after the current wait: {_duration_text(margin)}."
        )
    if margin == 0:
        return (
            f"Expected arrival: {eta_minutes} minutes. "
            "The current wait aligns with your arrival."
        )
    return (
        f"Expected arrival: {eta_minutes} minutes. "
        f"The current wait may extend {_duration_text(abs(margin))} "
        "past your arrival."
    )


def build_pdf(details, output):
    from reportlab.lib import colors
    from reportlab.lib.pagesizes import LETTER
    from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
    from reportlab.lib.units import inch
    from reportlab.graphics.barcode import code128
    from reportlab.platypus import (
        HRFlowable,
        Paragraph,
        SimpleDocTemplate,
        Spacer,
        Table,
        TableStyle,
    )

    styles = getSampleStyleSheet()
    title = ParagraphStyle(
        "CurbTitle",
        parent=styles["Title"],
        fontSize=20,
        leading=24,
        textColor=colors.HexColor(ACCENT),
        spaceAfter=4,
    )
    subtitle = ParagraphStyle(
        "CurbSubtitle",
        parent=styles["BodyText"],
        fontSize=10,
        textColor=colors.HexColor("#555555"),
        spaceAfter=10,
    )
    bay_style = ParagraphStyle(
        "Bay",
        parent=styles["Title"],
        fontSize=30,
        leading=34,
        alignment=1,
        textColor=colors.HexColor(ACCENT),
    )
    heading = ParagraphStyle(
        "Section",
        parent=styles["Heading2"],
        fontSize=12,
        leading=14,
        textColor=colors.HexColor(ACCENT),
        spaceBefore=10,
        spaceAfter=5,
    )
    body = ParagraphStyle(
        "CurbBody",
        parent=styles["BodyText"],
        fontSize=10,
        leading=14,
    )
    doc = SimpleDocTemplate(
        output,
        pagesize=LETTER,
        leftMargin=0.65 * inch,
        rightMargin=0.65 * inch,
        topMargin=0.55 * inch,
        bottomMargin=0.55 * inch,
    )
    flow = [
        Paragraph("BlastBox Curb Pickup", title),
        Paragraph("Customer bay recommendation", subtitle),
        HRFlowable(width="100%", color=colors.HexColor(ACCENT)),
        Spacer(1, 8),
    ]

    meta = Table(
        [
            [
                Paragraph(
                    f"<b>Order</b><br/>{_escape(details['order'])}<br/>"
                    f"<b>Customer</b><br/>{_escape(details['customer'])}",
                    body,
                ),
                Paragraph(
                    f"<b>Store</b><br/>{_escape(details['store'])}",
                    body,
                ),
                code128.Code128(
                    details["order"],
                    barHeight=0.42 * inch,
                    barWidth=0.012 * inch,
                    humanReadable=True,
                ),
            ]
        ],
        colWidths=[1.8 * inch, 2.5 * inch, 2.0 * inch],
    )
    meta.setStyle(
        TableStyle(
            [
                ("BACKGROUND", (0, 0), (-1, -1), colors.HexColor(LIGHT_ACCENT)),
                ("BOX", (0, 0), (-1, -1), 0.5, colors.HexColor(ACCENT)),
                ("INNERGRID", (0, 0), (-1, -1), 0.25, colors.white),
                ("VALIGN", (0, 0), (-1, -1), "TOP"),
                ("LEFTPADDING", (0, 0), (-1, -1), 8),
                ("RIGHTPADDING", (0, 0), (-1, -1), 8),
                ("TOPPADDING", (0, 0), (-1, -1), 7),
                ("BOTTOMPADDING", (0, 0), (-1, -1), 7),
            ]
        )
    )
    flow.extend([meta, Spacer(1, 10)])

    bay_panel = Table(
        [
            [
                Paragraph(f"BAY {_escape(details['bay'])}", bay_style),
                Paragraph(
                    f"<b>{_escape(details['queue_text'])}</b><br/>"
                    f"Expected wait: "
                    f"{_escape(_duration_text(details['expected_wait_seconds']))}."
                    + (
                        f"<br/>{_escape(details['eta_text'])}"
                        if details["eta_text"]
                        else ""
                    ),
                    body,
                ),
            ]
        ],
        colWidths=[2.3 * inch, 4.0 * inch],
    )
    bay_panel.setStyle(
        TableStyle(
            [
                ("BOX", (0, 0), (-1, -1), 1.0, colors.HexColor(ACCENT)),
                ("VALIGN", (0, 0), (-1, -1), "MIDDLE"),
                ("BACKGROUND", (0, 0), (0, 0), colors.HexColor(LIGHT_ACCENT)),
                ("LEFTPADDING", (0, 0), (-1, -1), 10),
                ("RIGHTPADDING", (0, 0), (-1, -1), 10),
                ("TOPPADDING", (0, 0), (-1, -1), 12),
                ("BOTTOMPADDING", (0, 0), (-1, -1), 12),
            ]
        )
    )
    flow.append(bay_panel)

    flow.extend(
        [
            Paragraph("Conditions", heading),
            Paragraph(_escape(details["conditions"]), body),
            Paragraph("Why this bay", heading),
            Paragraph(_escape(details["reason"]), body),
        ]
    )

    flow.extend(
        [
            Spacer(1, 12),
            HRFlowable(width="100%", color=colors.HexColor(ACCENT)),
            Spacer(1, 6),
            Paragraph(
                "This is a current recommendation, not a reservation or guarantee. "
                "Please stay in your vehicle and have your order number ready.",
                body,
            ),
        ]
    )
    doc.build(flow)


def main(argv):
    parser = argparse.ArgumentParser(
        description="Render a BlastBox curbside bay recommendation PDF."
    )
    parser.add_argument("--order", required=True)
    parser.add_argument("--customer", required=True)
    parser.add_argument("--store", required=True)
    parser.add_argument("--bay", required=True)
    parser.add_argument("--orders-ahead", type=_nonnegative_int, required=True)
    parser.add_argument(
        "--expected-wait-seconds", type=_nonnegative_int, required=True
    )
    parser.add_argument("--eta-minutes", type=_nonnegative_int)
    parser.add_argument("--conditions", required=True)
    parser.add_argument("--reason", required=True)
    parser.add_argument("--out", required=True)
    args = parser.parse_args(argv[1:])

    try:
        if args.expected_wait_seconds != args.orders_ahead * 30:
            raise ValueError(
                "--expected-wait-seconds must equal --orders-ahead "
                "multiplied by 30"
            )
        details = {
            "order": _required_text(args.order, "--order"),
            "customer": _required_text(args.customer, "--customer"),
            "store": _required_text(args.store, "--store"),
            "bay": _required_text(args.bay, "--bay"),
            "orders_ahead": args.orders_ahead,
            "expected_wait_seconds": args.expected_wait_seconds,
            "queue_text": _queue_text(args.orders_ahead),
            "eta_text": _eta_text(
                args.expected_wait_seconds, args.eta_minutes
            ),
            "conditions": _required_text(args.conditions, "--conditions"),
            "reason": _required_text(args.reason, "--reason"),
        }
        output = _required_text(args.out, "--out")
    except ValueError as exc:
        parser.error(str(exc))

    try:
        build_pdf(details, output)
    except ImportError:
        print("Error: reportlab is required to render the curbside PDF.")
        return 1
    except OSError as exc:
        print(f"Error: could not write PDF ({exc}).")
        return 1

    if not Path(output).is_file():
        print("Error: PDF output was not created.")
        return 1
    print(f"WROTE {output}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
