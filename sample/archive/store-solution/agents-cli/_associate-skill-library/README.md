# Associate-facing skill library (backup)

Backed up from the **Returns & Service Assistant** agent before scoping it down to
**customer-facing only** (BlastPass membership & cards self-serve). These pieces
are the associate / returns-desk machinery, kept here to seed a future
**associate-facing agent**.

## Contents
- `behaviors/prorated-refund-calculator.mcs.yml` — inline skill: prorated BlastPass
  refund + upgrade settlement math (Python).
- `behaviors/slip-pdf-generator.mcs.yml` — inline skill: printable return/RMA/
  exchange slip as a PDF (reportlab).
- `connected-agents/Default_InventoryFullfilmentAgent_X-w2GP.mcs.yml` — ConnectedAgentTool ref → Inventory & Fulfillment Agent.
- `connected-agents/Default_StorePolicyAgent_s9s-u8.mcs.yml` — ConnectedAgentTool ref → Store Policy Agent.

> The connected-agent **agents themselves** still exist in the environment; these
> files are only the *references* this agent held. The inline skills' full source
> lives in their `content` blocks here.

> Use at your own peril — these are demo artifacts, not production config.
