# Enhanced Task Completion

A showcase site and deployable sample for **Enhanced Task Completion** in Microsoft Copilot Studio.

**Live site**: https://microsoft.github.io/new-copilot-studio-tech-guide/

## What's in this repo

| Folder | Description |
|---|---|
| `src/` | Astro site — landing page, scenario walkthroughs, and documentation |
| `sample/` | Deployable sample — the Copilot Studio solution (`sample/solution/`) plus archived earlier iterations (`sample/archive/`) |

## Deploying the sample

The sample is a portable Copilot Studio solution with **four agents** (a flagship Store
Associate Assistant and a self-serve Returns & Service Assistant, plus two connected
agents) and **four inline MCP connectors** (Membership, Order Management, Policy RAG, and
Warehouse). Everything runs inside the Power Platform — the connectors use inline custom
code, so no external servers are needed.

```
sample/
  solution/          Solution zip + unpacked source + import README
  archive/           Earlier iterations (connectors, store-solution, chat UI, exports)
```

See [`sample/solution/README.md`](./sample/solution/README.md) for step-by-step setup,
including the two demo scenarios (Self-Serve Card Reissue and Block Party Trade-Up).

## Site development

```bash
npm install
npm run dev        # http://localhost:4321/new-copilot-studio-tech-guide/
npm run build      # Build to ./dist/
```

## Contributing

This project welcomes contributions and suggestions. See [CONTRIBUTING](https://github.com/microsoft/new-copilot-studio-tech-guide/blob/main/CONTRIBUTING.md) for details.

## License

MIT License. Copyright (c) Microsoft Corporation.
