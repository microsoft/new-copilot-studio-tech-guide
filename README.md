# Copilot Studio Technical Guide

A showcase site and deployable sample for the new Agents and Workflows experience in Microsoft Copilot Studio.

**Live site**: https://microsoft.github.io/new-copilot-studio-tech-guide/

## What's in this repo

| Folder | Description |
|---|---|
| `src/` | Astro site — landing page, scenario walkthroughs, and documentation |
| `deploy/` | Scripted, repeatable deploy of the sample into a Power Platform environment |
| `sample/` | Deployable sample — the Copilot Studio solution (`sample/solution/`) plus archived earlier iterations (`sample/archive/`) |

## Deploying the sample

The sample is a portable Copilot Studio solution with **four agents** (a flagship Store
Associate Assistant and a self-serve Returns & Service Assistant, plus two connected
agents) and **four inline MCP connectors** (Membership, Order Management, Policy RAG, and
Warehouse). Everything runs inside the Power Platform: the connectors use inline custom
code, so no external servers are needed.

The fastest way to stand it up is the scripted deploy, one cross-platform Node script that
imports both solutions, deploys the connector code, publishes, creates the connections,
and publishes the agents, then prints the single ~2-minute manual UI step:

```bash
node deploy/deploy.mjs            # guided: pick profile, pick env, deploy
node deploy/deploy.mjs --help     # all options
```

```
deploy/             Scripted, repeatable deploy (deploy.mjs + README)
sample/
  solution/         The two solution zips + unpacked source + connector code + skills
  archive/          Earlier iterations (connectors, store-solution, chat UI, exports)
```

See [`deploy/README.md`](./deploy/README.md) for the full walkthrough and the manual
re-attach step, and [`sample/solution/README.md`](./sample/solution/README.md) for what's
in the solution and the two demo scenarios (Self-Serve Card Reissue and Block Party
Trade-Up).

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
