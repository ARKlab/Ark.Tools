# Ark CSharp Marketplace Design

## Goal
Create a local APM marketplace for this repository with one publishable package, `ark-csharp`, containing only the existing `ark-reqnroll` skill and `ark-csharp-expert` agent.

## Design
The repository remains the marketplace root. Its `apm.yml` receives a `marketplace:` block with one local plugin entry whose source is `./agents-plugins/ark-csharp`. The package is self-contained under `agents-plugins/ark-csharp` and uses the symmetric APM layout: `.apm/skills/ark-reqnroll/SKILL.md` and `.apm/agents/ark-csharp-expert.agent.md`.

The package metadata declares `name: ark-csharp`, version `1.0.0`, and no dependencies. The marketplace is built with `apm pack`; local installation uses `apm install ./agents-plugins/ark-csharp`, which exercises the package independently of the marketplace index.

## Scope
- Add one `marketplace:` block to the root `apm.yml`.
- Add the `ark-csharp` package manifest and exactly two package primitives.
- Generate the marketplace artifact with APM.
- Self-install the local package and verify its lock/deployment state.
- Do not add new dependencies or create a new agent; reuse the existing C# expert agent under its new `ark-csharp-expert` identifier.

## Validation
- `apm marketplace check --offline` validates marketplace schema and local source.
- `apm pack` generates `.claude-plugin/marketplace.json`.
- `apm install ./agents-plugins/ark-csharp --target copilot` installs the package locally.
- File inspection confirms exactly one skill and one agent are packaged.
