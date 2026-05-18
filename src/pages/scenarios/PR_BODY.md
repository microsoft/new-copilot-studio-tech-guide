## Summary
- Adds a fifth scenario: **Brand template generation** — preserves the customer's PPT/Word template shapes, logos, formatting, and layout while filling placeholders from contextual inputs.
- Registers the scenario card on the landing page.
- Ships the available contributed `PPTWORDTemplateGenerator` solution zip and `templatize_docx.py` helper under `sample/templates/`.

## Why
Existing "make me a deck" flows regenerate slides from scratch and lose brand fidelity. This scenario shows how ETC can chain a templatize step with contextual placeholder filling so the source file's shapes, logos, fonts, and layout stay untouched.

## How it works
1. `templatize_*` walks the file, preserves brand elements, and replaces editable text with named placeholders.
2. The agent uses ETC to decide which placeholders need clarifying questions vs. tool calls vs. inference.
3. A fill step writes values back in place so the output preserves the original document design outside placeholder text.

## Test plan
- [x] `npm run build` succeeds
- [x] New `/scenarios/brand-template-generation/` route builds
- [x] New card is registered on the home page
- [ ] Solution zip downloads from the linked path after PR is published
- [ ] Add/confirm companion fill script and verify end-to-end with ETC enabled in a Power Platform Early Release environment

## Notes
- Local contribution files included `PPTWORDTemplateGenerator_1_0_0_2.zip` and `templatize_docx.py`; the companion fill script referenced by the scenario was not present locally.
- Early Release wording follows the existing sample README language.

## Credit
Contributed by Betty Le (Microsoft, Cloud Solution Architect).
