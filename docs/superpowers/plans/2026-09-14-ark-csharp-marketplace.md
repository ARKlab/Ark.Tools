# Ark CSharp Marketplace Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create and locally install a single-package APM marketplace containing only the Reqnroll skill and existing C# expert agent.

**Architecture:** Keep the repository as the marketplace root. Store the package under `agents-plugins/ark-csharp` with APM primitives under `.apm/`; reference it from the root marketplace using a local `./agents-plugins/ark-csharp` source.

**Tech Stack:** APM CLI, YAML, Markdown, Claude marketplace metadata.

**Spec:** `docs/superpowers/specs/2026-09-14-ark-csharp-marketplace-design.md`

## Global Constraints

- Package name: `ark-csharp`.
- Package contains only `ark-reqnroll` and the existing `ark-csharp-expert` agent.
- No new dependency.
- Use `.apm/skills/<name>/` and `.apm/agents/*.agent.md` package layout.
- Use APM commands for marketplace generation and local installation.

---

### Task 1: Scaffold Marketplace Metadata

**Files:**
- Modify: `apm.yml`
- Create: `.claude-plugin/marketplace.json` via `apm pack`

- [ ] Add a `marketplace:` block with owner metadata and one local package entry:

```yaml
marketplace:
  owner:
    name: Ark.Tools
  plugins:
    - name: ark-csharp
      description: Ark CSharp development skills and agents
      source: ./agents-plugins/ark-csharp
      version: 1.0.0
```

- [ ] Run `apm marketplace check --offline` and require exit code 0.
- [ ] Run `apm pack` and require `.claude-plugin/marketplace.json` to be generated.

### Task 2: Create the Local Package

**Files:**
- Create: `agents-plugins/ark-csharp/apm.yml`
- Create: `agents-plugins/ark-csharp/.apm/skills/ark-reqnroll/SKILL.md`
- Create: `agents-plugins/ark-csharp/.apm/agents/ark-csharp-expert.agent.md`

- [ ] Create package metadata with `name: ark-csharp`, `version: 1.0.0`, package description, and `includes: auto`.
- [ ] Copy the complete existing `ark-reqnroll` skill into the package skill path.
- [ ] Copy the complete existing C# agent content into `.agent.md` form without adding other primitives.
- [ ] Inspect the package tree and require exactly one `SKILL.md` and one `*.agent.md`.

### Task 3: Self-Install and Verify

**Files:**
- Modify: `apm.lock.yaml` through APM installation
- Create/update: target deployment files through APM installation

- [ ] Run `apm install ./agents-plugins/ark-csharp --target copilot`.
- [ ] Require exit code 0 and confirm the lockfile records `ark-csharp` as a local package.
- [ ] Run `apm install --frozen --target copilot --only apm` and require exit code 0.
- [ ] Verify no package files contain primitives outside `ark-reqnroll` and `ark-csharp-expert`.
