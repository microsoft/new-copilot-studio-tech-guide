# Brand template generation sample

This folder contains the contributed assets for the brand template generation scenario.

## Contents

| Path | Description |
|---|---|
| `PPTWORDTemplateGenerator_1_0_0_2.zip` | Power Platform solution package for the template generation scenario. |
| `scripts/templatize_docx.py` | Python helper that converts editable Word document text into named placeholders while preserving document structure and formatting. |

## Setup

1. Import `PPTWORDTemplateGenerator_1_0_0_2.zip` into a Power Platform environment with Copilot Studio on the **Early Release** channel.
2. Turn on Enhanced Task Completion in the agent's Generative AI settings.
3. Add the scripts in `scripts/` as template-processing tools or knowledge-backed code assets.

The companion fill script described in the scenario was not present with the local contribution files used for this PR. Add it to `scripts/` before validating the full templatize-and-fill flow end to end.
