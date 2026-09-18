# Documentation Reorganization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Separate consumer documentation from design history and transient implementation tracking without breaking repository links.

**Architecture:** Consumer documentation remains under its existing product-oriented paths, except that the Mediator Framework guide is flattened into `docs/mediator-framework/`. Design records move under `docs/design/`; plans, progress, and task boards move under `docs/plans/`. `docs/superpowers/` remains unchanged as an explicit exception.

**Tech Stack:** Markdown, Git path moves, repository-wide relative-link validation.

**Spec:** Approved documentation reorganization request and follow-up requirements.

## Global Constraints

- Preserve consumer-document paths wherever possible.
- Flatten `docs/mediator-framework/guide/` into `docs/mediator-framework/`.
- Keep `docs/superpowers/` in place.
- Update every tracked repository link affected by a move, including links outside `docs/`.
- Do not rewrite document content beyond navigation and classification.
- Consumer documents must not link to task or implementation-plan documents.

---

### Task 1: Move documentation into stable clusters

- [ ] Move Mediator Framework guides into `docs/mediator-framework/`.
- [ ] Move Mediator Framework designs and research into `docs/design/mediator-framework/`.
- [ ] Move Mediator Framework progress and task files into `docs/plans/mediator-framework/`.
- [ ] Move SDK design records into `docs/design/sdk/`.
- [ ] Move SDK progress and task files into `docs/plans/sdk/`.
- [ ] Move OTel design/research records into `docs/design/otel/`.
- [ ] Move OTel implementation and progress records into `docs/plans/otel/`.
- [ ] Move OpenAPI design/research into `docs/design/openapi/`.
- [ ] Move OpenAPI implementation plans into `docs/plans/openapi/`.
- [ ] Move completed performance records into `docs/design/performance/`.
- [ ] Leave `docs/superpowers/` untouched.

### Task 2: Repair navigation and consumer boundaries

- [ ] Add indexes for `docs/`, `docs/design/`, and `docs/plans/`.
- [ ] Update moved-document relative links.
- [ ] Update links in `README.md`, samples, source READMEs, agent plugins, and other files outside `docs/`.
- [ ] Remove consumer-document links to task boards and implementation plans, replacing them with stable guidance or design references.

### Task 3: Document the structure

- [ ] Add repository guidance to `AGENTS.md` defining consumer, design/decision, and plan/task clusters.
- [ ] Record `docs/superpowers/` as an intentional exception.
- [ ] State that consumer docs must not depend on transient plans.

### Task 4: Validate

- [ ] Check every Markdown link resolves to an existing file or valid external URL.
- [ ] Search consumer documentation for plan/task references.
- [ ] Confirm no files moved under `docs/superpowers/`.
- [ ] Scan changed files for secrets.
- [ ] Run documentation-focused repository checks and final validation.
