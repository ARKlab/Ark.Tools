# Ark.Tools Analyzer Diagnostics Documentation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give every Ark.Tools-owned diagnostic a unique, actionable rule message, canonical help link, and dedicated documentation page.

**Architecture:** Keep diagnostics in their existing analyzers and generators. Deduplicate only exact shared rules, assign new IDs to semantically different generator diagnostics, and generate a one-page rule catalog under `docs/analyzer-rules/` linked from each descriptor.

**Tech Stack:** C#, Roslyn analyzers/source generators, Markdown, .NET 10 SDK, xUnit/Reqnroll repository tests.

**Spec:** Approved analyzer diagnostics documentation and message-improvement design in the user request.

## Global Constraints

- Cover every Ark.Tools-owned diagnostic regardless of severity.
- Use canonical `https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzer-rules/<ID>.md` help links.
- Diagnostic messages must state the required correction.
- Preserve exact shared rules; give semantically different duplicate IDs distinct codes.
- Do not add dependencies.
- Validate with `dotnet restore Ark.Tools.slnx`, `dotnet build Ark.Tools.slnx --no-restore`, and `dotnet test Ark.Tools.slnx --no-build`.

---

### Task 1: Establish the final diagnostic inventory

**Files:**
- Create: `docs/analyzer-rules/<diagnostic-id>.md` for every final ID
- Modify: `docs/analyzers.md`
- Modify: all descriptor source files under `src/common`, `src/mediator-framework`, and `src/compliance`

- [ ] Enumerate all descriptor IDs and classify duplicate IDs by semantic rule.
- [ ] Reserve distinct IDs for the MCP and Azure Functions diagnostics currently colliding in `ARKMF030`–`ARKMF040`, and for the HTTP/Rebus collision at `ARKMF020`.
- [ ] Keep `ARKMF011` shared because the handler-kind descriptor and remediation are identical across HTTP, Rebus, and gRPC.
- [ ] Record the final ID, severity, title, message template, source component, and documentation filename in the analyzer index.

### Task 2: Improve descriptor messages and help links

**Files:**
- Modify: every source file containing a `DiagnosticDescriptor`

- [ ] Rewrite each title and message so it says what the developer must declare, change, or remove.
- [ ] Preserve message argument ordering used by existing `Diagnostic.Create` calls and tests.
- [ ] Add or update `helpLinkUri` on every Ark.Tools descriptor to its final rule page.
- [ ] Update code-fix and sink-configuration references when an ID changes.
- [ ] Keep duplicate shared descriptor definitions byte-for-byte semantically aligned.

### Task 3: Add rule documentation pages

**Files:**
- Create: `docs/analyzer-rules/<diagnostic-id>.md` for every final ID

- [ ] Give each page the rule ID, title, severity, category, affected package/component, and concise behavior description.
- [ ] Document the trigger and the required fix.
- [ ] Include a minimal incorrect example and corrected example appropriate to the rule family.
- [ ] Include suppression/configuration guidance where the analyzer supports it.
- [ ] Link related rules where a family has multiple diagnostics.

### Task 4: Update documentation index and tests

**Files:**
- Modify: `docs/analyzers.md`
- Modify: analyzer and generator tests covering changed IDs/messages
- Modify: snapshot or fixture files affected by diagnostic output

- [ ] Replace the summary-only tables with links to every dedicated rule page.
- [ ] Add tests asserting new IDs for formerly colliding diagnostics.
- [ ] Add tests asserting actionable message text and canonical help links for representative analyzer families.
- [ ] Update expected diagnostic snapshots without weakening existing coverage.

### Task 5: Validate and review

- [ ] Run `dotnet restore Ark.Tools.slnx`.
- [ ] Run `dotnet build Ark.Tools.slnx --no-restore`.
- [ ] Run `dotnet test Ark.Tools.slnx --no-build`.
- [ ] Scan all modified files for secrets.
- [ ] Run parallel code review and CodeQL validation.
- [ ] Fix valid findings and repeat validation after substantive changes.
