# Self-Serve BlastPass Card Reissue

> **Scenario 0 (rebuilt) — the self-serve warm-up.** The member is signed into the
> **MegaBlast member portal** and talks to the assistant directly (no store
> associate in the loop). They lost their BlastPass card and want a replacement.
> This is the gentlest end-to-end shape in the demo — **one MCP + one skill + a
> generated file** — but rebuilt for **conversational clarity**: identity is
> verified before anything changes, and exactly **one capability is spotlighted per
> turn**.
>
> Replaces the old "Card Reprint" warm-up (associate voice, single turn). The new
> version is **member voice, two turns**, and actually mutates state (the old card
> is deactivated) instead of just reprinting.

## Cast on stage

| Component | Kind | Role in this run |
| --- | --- | --- |
| **Returns & Service Assistant** | Parent agent | Talks directly to the signed-in member; owns the Membership MCP + the card skill. |
| **Membership MCP** | Parent tool | `get_membership` (look up + verify), `reissue_card` (deactivate old card, mint new one). |
| **membership-card-png** | Skill (runtime Python) | Renders the new digital card to a **PNG** via matplotlib. |
| **File generation** | Output | A digital BlastPass membership card (`blastpass_card.png`) the member can save immediately. |

**Member on file:** `MEGA-BLAST-1024` — **Jordan Pixel**, **BlastPass Plus Extra**,
console serial `OMEGA-7F3A-1024`, card on file `BLAST-7F3A-1024`, activated
2026-02-26, term ends 2027-02-26.

---

## Turn 1 — The request → ask for the membership ID

### 👤 Member (signed in to the MegaBlast portal)

> Hi — I lost my BlastPass card. Can you send me a new one?

### ⚙️ Orchestration (what the agent did)

1. **Returns & Service Assistant** (reasoning): *"They want a replacement card. I don't have
   a membership ID yet, and I can't look anything up — or verify anyone — without
   it. I'll ask for it first; nothing changes until I have it."*

### 🤖 Returns & Service Assistant

> Oh no — let's get you a new one. 💜 No problem at all.
>
> To pull up your membership, what's your **BlastPass member ID**? It usually looks
> like `MEGA-BLAST-####`.

> **Why one turn for this:** the member opens with just the request, **not** their
> ID. The agent's only job here is to ask for the one thing it needs to start — the
> membership ID. No lookup, no card change, no file.

---

## Turn 2 — Look up → the verification gate

### 👤 Member

> Sure, it's MEGA-BLAST-1024.

### ⚙️ Orchestration (what the agent did)

1. → **`get_membership`** (Membership MCP) — record for `MEGA-BLAST-1024`
   (Jordan Pixel, Plus Extra, console `OMEGA-7F3A-1024`).
2. **Returns & Service Assistant** (reasoning): *"Before I deactivate a card and issue a new
   one, I should confirm this is really the account holder. I'll verify with **two
   factors the real owner has** — their **name** and the **last 4 of the console
   serial** — and I won't reveal either; the member has to tell me both."*

### 🤖 Returns & Service Assistant

> Got it — I found a **BlastPass Plus Extra** membership under that ID. 💜
>
> Before I deactivate the old card and issue a new one, I just need to confirm
> it's really you. Can you give me **two things**:
>
> 1. The **name** on the membership, and
> 2. The **last 4 digits of your console's serial number** (it's printed on the
>    bottom of your BlastBox Omega).

> **Why one turn for this:** the agent does a single read-only lookup and asks for
> the verification details. No card is touched, nothing is generated — the only job
> of this turn is to establish trust before any state changes. Note it confirms the
> tier but **not** the member's name, since the name is one of the things being
> verified.

---

## Turn 3 — Verify → reissue → digital card + shipping

### 👤 Member

> It's Jordan Pixel, and the last 4 are 1024.

### ⚙️ Orchestration (what the agent did)

1. **Returns & Service Assistant** (reasoning): *"Name **Jordan Pixel** matches the record,
   and `OMEGA-7F3A-1024` ends in `1024` — both factors match. Identity confirmed.
   Now I can safely reissue the card."*
2. → **`reissue_card("MEGA-BLAST-1024", reason: "lost")`** (Membership MCP) —
   deactivates `BLAST-7F3A-1024`, mints a new `card_serial`
   (e.g. `BLAST-A1B2-1024`), keeps the membership **active**, queues the physical
   card for standard mail (ETA +7 days), returns a reissue confirmation number.
3. **Run `membership-card-png`** — generates and runs Python (matplotlib) that
   renders a CR80 digital card using the member details from `get_membership`
   plus the **`new_card_serial`** from `reissue_card`: member name, tier badge
   (Plus Extra = purple), member ID, console serial, member-since / valid-through
   dates, a decorative barcode keyed to the new serial, and the tier perks line.
   Writes `blastpass_card.png`.

### 🤖 Returns & Service Assistant

> Thanks, Jordan — you're verified. ✅ Here's everything in one go:
>
> **🪪 Your new digital card is ready** — `blastpass_card.png`
> Save it to your phone and use it in-store and at checkout **right now**.
>
> - **New card:** `BLAST-A1B2-1024` (BlastPass Plus Extra)
> - **Old card:** `BLAST-7F3A-1024` — **now deactivated**, so it can't be used again
> - **Physical card:** on its way by standard mail, arriving around **2026-XX-XX**
> - **Reissue confirmation:** `BPX-CARD-1024-20260XXX`
>
> Your membership, tier, term, and BlastPoints are all unchanged. Anything else I
> can help with? 🎮

> **Why one turn for this:** verification, the state change (`reissue_card`), and
> the file generation are a single uninterrupted beat once identity is confirmed —
> the member asked one thing ("send me a new card") and gets one clean resolution,
> not a wall of options.

---

## Expected output

**`blastpass_card.png`** — a tier-colored (purple "PLUS EXTRA") digital membership
card showing the member's name, the **new** card serial, member ID, console serial,
dates, and perks. The old serial never appears (it's deactivated server-side).

## Why it's the right opener

- **One of every pillar that scales:** a single MCP lookup → an identity check → a
  single state-changing MCP call → one skill that emits a file. The same shape the
  harder scenarios repeat and stack.
- **Self-serve, member voice:** no associate, no policy, no math — the audience
  learns the moving parts (MCP → verify → MCP → skill → file) before later
  scenarios add connected agents and calculations.
- **Clarity-first pacing:** Turn 1 only verifies; Turn 2 only resolves. Nothing is
  mutated before the member is confirmed, and each turn spotlights exactly one
  capability instead of dumping everything at once.
- **Reacts to real data:** the card's accent color, perks line, and barcode are all
  driven by the MCP's `tier_code` and the freshly minted `new_card_serial` — not a
  static template.

## Notes

- Identity is verified with **two factors**: the member's **name** (`member_name`)
  and the **last 4 of the console serial** (`console_serial`), both from
  `get_membership`. The agent must not reveal either value while asking, and must
  only proceed if **both** match.
- `reissue_card` is deliberately **distinct from `cancel_membership`**: it leaves
  the membership active and only swaps the card. Never use `cancel_membership` for
  a lost/stolen/damaged card.
- All data is mock; serials, confirmation numbers, and dates above are illustrative
  (the real values are minted at run time).
