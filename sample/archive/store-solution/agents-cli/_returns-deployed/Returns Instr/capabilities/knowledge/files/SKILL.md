---
name: membership-card-png
description: Generate a digital BlastPass membership card as a PNG image by running the bundled card_png.py script. Use when a member self-serves a lost/stolen/damaged card replacement, or any time a card needs to be shown on screen (vs. printed). The skill takes the member details returned by the Membership MCP (get_membership) plus the new_card_serial returned by reissue_card and passes them as command-line flags to a bundled Python (matplotlib) script that renders a CR80-sized membership card to a real PNG. It only renders the card from already-retrieved values — it never looks anything up or invents data.
---

# BlastPass Membership Card PNG Generator

This skill produces a **digital BlastPass membership card** as a shareable image by
running the **bundled `card_png.py` script**. It is the self-serve companion to
`membership-card-pdf`: when a member reissues a lost, stolen, or damaged card, you
mint a new serial via the Membership MCP and hand the member a PNG to save to their
phone **while the physical card ships**.

You do **not** write any Python. You take the member record you already pulled from
the **Membership MCP** (`get_membership`) and the **new serial** returned by
`reissue_card`, then **run the bundled script** with those values as CLI flags.

Never invent a member, tier, or serial number — every value on the card comes from
the MCP results. The script only *draws* the card; it does not decide anything.

## When to use

Use this skill in the **self-serve card reissue** scenario, after:

1. `get_membership(customer_id)` confirmed the member and returned their details.
2. The member verified their identity (last 4 of the console serial).
3. `reissue_card(customer_id, reason)` deactivated the old card and returned a
   `new_card_serial`.

Use the PDF skill instead when an in-store associate needs a **printable** card.
Use this PNG skill when the member needs an **on-screen / saveable** card.

## Inputs (map MCP results → CLI flags)

| Script flag | Source |
| --- | --- |
| `--member-name` | `get_membership.member_name` |
| `--member-id` | `get_membership.customer_id` |
| `--tier` | `get_membership.tier` (e.g. `"BlastPass Plus Extra"`) |
| `--tier-code` | `get_membership.tier_code` (`plus` \| `extra` \| `mega`) — drives accent color & perks |
| `--card-serial` | **`reissue_card.new_card_serial`** (the freshly minted card; falls back to `get_membership.card_serial` if not a reissue) |
| `--console-serial` | `get_membership.console_serial` |
| `--member-since` | `get_membership.activation_date` |
| `--valid-through` | `get_membership.term_end_date` |
| `--out` | optional output path (default `blastpass_card.png`) |

The `--tier-code` selects the card's accent color, header badge, and the perks line
printed beneath the card:

- `plus` → blue, "PLUS", 5% off + 10 BlastPoints/$1
- `extra` → purple, "PLUS EXTRA", 10% off + 20% BlastPoints bonus
- `mega` → orange, "MEGA!!!", 15% off + 50% BlastPoints + day-one exclusives

## Workflow

### 1. Gather the confirmed values

From `get_membership` (tier, dates, console serial, name, id) and from
`reissue_card` (`new_card_serial`). Do **not** pass any value the MCP didn't return.

### 2. Run the bundled script

Pass the confirmed values as flags. The script writes `blastpass_card.png` to the
working directory (or `--out`) and prints a one-line confirmation:

```bash
python3 card_png.py \
    --member-name "Jordan Pixel" \
    --member-id MEGA-BLAST-1024 \
    --tier "BlastPass Plus Extra" \
    --tier-code extra \
    --card-serial BLAST-A1B2-1024 \
    --console-serial OMEGA-7F3A-1024 \
    --member-since 2026-02-26 \
    --valid-through 2027-02-26
```

`--card-serial` must be the **new** serial from `reissue_card` — never the old
(now-deactivated) one.

### 3. Hand back the image

Tell the member their new digital card is ready (`blastpass_card.png`), read back
the **new card serial** and tier so they can confirm it, and remind them the old
card is now deactivated and the physical replacement ships in ~7 days.

## Notes

- The bundled script is `card_png.py`, validated against the four mock members in
  the Membership MCP. It depends only on `matplotlib` (available in the runtime).
- The card serial shown is the **new** one from `reissue_card`; the old serial is
  already deactivated server-side, so it must never appear on the card.
- The barcode is decorative (deterministic from the card serial) — it is not a real
  scannable symbology.
- All data is mock; the card is for demo purposes only.
