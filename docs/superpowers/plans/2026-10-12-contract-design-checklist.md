# Contract Design Checklist Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Publish one contract design checklist in the Mediator Framework user guide and make the `ark-mediator-framework` skill enforce it as a gate before contract code is written.

**Architecture:** `docs/mediator-framework/contract-design-checklist.md` is the single source. Existing docs link to it. The skill fetches it by raw GitHub URL and keeps an index of section names plus hard defaults. The skill-creator eval loop compares the old skill with the new one.

**Tech Stack:** Markdown docs, APM 0.33.0 + PowerShell 7 (`apm install`, `apm run plugins:build|plugins:check`, `apm audit --ci`), the skill-creator scripts (`/root/.claude/skills/synced/411ebc25-a5fc-4bd0-935d-bb575e0fa46c_de247a70-3cf7-4a10-b080-bff2c9adcd8f/skill-creator`).

**Spec:** `docs/superpowers/specs/2026-10-12-contract-design-checklist-design.md`

## Global Constraints

- Consumer docs must not link to `docs/plans/` or `docs/superpowers/` (AGENTS.md documentation clusters).
- No .NET code changes, so no `dotnet build`/`test`. Validate Markdown links and run the APM checks instead (AGENTS.md).
- Do not hand-edit generated agent files (`.claude/`, `.agents/`, `.github/skills/`, `agents-plugins/published/`); regenerate them with APM.
- Skill description stays scoped to consuming Ark.Tools.MediatorFramework.
- Legal items (purpose, lawful basis, retention period, DPIA trigger) are `ASK` whenever personal data is carried and changed (otherwise "N/A: no personal-data change"), owned by a human, and verified with the DPO/legal.
- Personal data in messages: messages crossing a service boundary carry only `[Pseudonymous]` identifiers; messages between processes of the same service may carry classified personal data; special-category (Art. 9) and criminal-offence (Art. 10) data never. Queues have a TTL where the broker supports it; every dead-letter queue and outbox has explicit retention and cleanup. Platform encryption at rest is the baseline; per-message or per-column encryption is on top.
- Examples use placeholders ("Customer A"), never real personal data.
- Commits: Conventional Commits, footer `Assisted-by: Claude`, no `Co-authored-by`.
- `ark-csharp` plugin version becomes `1.5.0`.

## Review Focus

1. **Fetch failure:** an agent that cannot reach `raw.githubusercontent.com` must still gate on the 18 sections. The skill index must list every section with its default (Task 3 check).
2. **No human in the loop:** with nobody to answer, an agent must stop after the brief instead of assuming answers. The skill says so, and eval assertion `no-code-written` checks it.
3. **Trivial changes:** an agent changing a single XML doc comment should not run 18 sections of questions. The checklist must say which changes need the full brief and which need only the affected sections (Task 1, section 0).
4. **Drift between doc and skill index:** a section renamed in the doc but not in the skill. Task 3 has a check that compares headings.
5. **Agent derives a legal basis from code** (e.g. "consent, because there's a checkbox"). The skill red flags and eval assertion `legal-items-ask` cover it.

---

### Task 1: Checklist document

**Files:**
- Create: `docs/mediator-framework/contract-design-checklist.md`

**Interfaces:**
- Produces: these exact `##` headings, which Task 3 copies into the skill index and the eval assertions:
  `## 0. How to use this checklist`, `## 1. Operation`, `## 2. Fields and updatability`, `## 3. Types and representation`, `## 4. Validation`, `## 5. Business rules`, `## 6. Failure contract`, `## 7. Concurrency and idempotency`, `## 8. API design`, `## 9. Lists, streaming and files`, `## 10. Limits and abuse`, `## 11. Transport exposure and serialization`, `## 12. Authorization`, `## 13. Messaging`, `## 14. Versioning and compatibility`, `## 15. Privacy (GDPR)`, `## 16. Audit and observability`, `## 17. Documentation`, `## 18. Tests`, `## Brief template`.

- [ ] **Step 1: Write the document.** For each section, write the questions as a table `Question | Default / rule | Answer source`, taking the content from spec Part 1 and the decision table. Fixed content:
  - **§0:** the draft/tag/ask/confirm loop; `derived (source)` vs `ASK`; "N/A" needs a reason; brief goes to chat and PR; non-attribute decisions go to XML docs; no human means stop after the brief. **Scope rule:** a new contract needs all sections; a change needs only the sections whose answers it alters, plus §14 and §15 always.
  - **§2:** the per-field table template with columns `Field | Type | Updatability | Required | Null meaning | Default | Concurrency | Validation | Personal-data class`; the five updatability labels with their model (`Input`, `Create : Input`, `Output`, context, envelope); "PUT is a full replace including nulls; absent means default; a partial update is its own operation".
  - **§8:** the defaults `/api/v{version}/<plural-kebab-resource>/{id}/<sub-resource>`, action sub-paths, gRPC `[GrpcService("<PluralResource>")]` + `[GrpcMethod("<VerbNoun>")]`, using the sample `Books`/`DownloadBookCover` as the example.
  - **§12:** the 403/404 rule with GUID vs int reasoning.
  - **§14:** `EvolvableEnum<T>` with `NOT_SET = 0`; the exceptions (externally controlled enums, `[Flags]`); additive first.
  - **§15:** the legal `ASK` items with the "verify with your DPO/legal team" note; the message rule from Global Constraints; an "Ark.Tools.Compliance mapping" sub-table (`[PersonalData]`, `[SensitivePersonalData]`, sensitive value objects, `ARKPII*`, `ArkComplianceSurface.txt`, `[SqlColumnPolicy]`), plus "Without Ark.Tools.Compliance" manual checks. Link `../compliance/README.md`.
  - **Brief template:** a fenced Markdown block with one `###` per section and the §2 table.
  - Each section links the existing guide that owns the details (e.g. §6 → `errors.md`, §11 → `grpc.md`/`mcp.md`/`serialization.md`, §9 → `streaming.md`/`attachments.md`). No links to `docs/plans` or `docs/superpowers`.

- [ ] **Step 2: Verify the headings**

Run: `grep -c '^## ' docs/mediator-framework/contract-design-checklist.md`
Expected: `20`

- [ ] **Step 3: Verify the links** (this check is reused in later tasks; `FILES` lists the files touched)

Run:
```bash
FILES="docs/mediator-framework/contract-design-checklist.md" python3 -c '
import os,re,sys
bad=[]
for f in os.environ["FILES"].split():
    for l in re.findall(r"\]\(([^)#\s]+)", open(f).read()):
        if not l.startswith("http") and not os.path.exists(os.path.join(os.path.dirname(f), l)): bad.append((f,l))
    if "/plans/" in open(f).read() and f.startswith("docs/mediator-framework"): bad.append((f,"links docs/plans"))
print(bad); sys.exit(bool(bad))'
```
Expected: `[]`, exit 0

- [ ] **Step 4: Commit**

```bash
git add docs/mediator-framework/contract-design-checklist.md
git commit -m "docs(MediatorFramework): add contract design checklist" -m "Assisted-by: Claude"
```

### Task 2: Link the checklist from existing docs

**Files:**
- Modify: `docs/mediator-framework/contract-design-workflow.md` (the "Required contract brief" section)
- Modify: `docs/mediator-framework/versioning.md` (the "Add an enum entry" row of the compatibility table)
- Modify: `docs/mediator-framework/README.md` (learning path row 3a)
- Modify: `docs/plans/mediator-framework/tasks/aspnetcore/NET-03-json-patch.md` (Steps and Acceptance)
- Modify: `CHANGELOG.md` (`## [Unreleased]`)

**Interfaces:**
- Consumes: `contract-design-checklist.md` from Task 1.

- [ ] **Step 1: `contract-design-workflow.md`:** replace the "Required contract brief" table with one paragraph that links `contract-design-checklist.md` and says nothing is created or changed until the brief is confirmed. Keep the decision tree and the other sections. Add the checklist to References.
- [ ] **Step 2: `versioning.md`:** split the enum row into "Add a plain enum entry | No | Strict clients may reject an unknown value." and "Add an `EvolvableEnum<T>` entry | Yes | Clients receive `Value == null` and take the fallback arm." Leave the closing sentence about evolvable enums as it is.
- [ ] **Step 3: `README.md`:** change row 3a to `[Contract design checklist](contract-design-checklist.md), [Contract design workflow](contract-design-workflow.md)`.
- [ ] **Step 4: NET-03:** add step 6, "Update `docs/mediator-framework/contract-design-checklist.md` §2 (partial-update semantics) and the `ark-mediator-framework` skill index for PATCH", plus a matching acceptance checkbox.
- [ ] **Step 5: `CHANGELOG.md`:** under `## [Unreleased]` → `### Added`, add one line: "Mediator Framework: a contract design checklist (fields, validation, business rules, API design, authorization, versioning, privacy and more), and the `ark-mediator-framework` agent skill now drafts and confirms a contract brief from it before writing code."
- [ ] **Step 6: Verify the links:** run the Task 1 Step 3 command with `FILES` set to the three `docs/mediator-framework` files above. Expected: `[]`.
- [ ] **Step 7: Commit**

```bash
git add docs/mediator-framework CHANGELOG.md docs/plans/mediator-framework/tasks/aspnetcore/NET-03-json-patch.md
git commit -m "docs(MediatorFramework): link contract design checklist" -m "Assisted-by: Claude"
```

### Task 3: Rewrite the skill

**Files:**
- Modify: `agents-plugins/ark-csharp/.apm/skills/ark-mediator-framework/SKILL.md`
- Modify: `agents-plugins/ark-csharp/apm.yml` (`version: 1.5.0`)
- Regenerated (by APM, not by hand): `.claude/skills/ark-mediator-framework/`, `.agents/skills/ark-mediator-framework/`, `agents-plugins/published/ark-csharp/**`, `.claude-plugin/marketplace.json`, `apm.lock.yaml` (only if APM changes it)

**Interfaces:**
- Consumes: the Task 1 headings.
- Produces: the skill text that Task 4 evaluates. Snapshot path: `$SCRATCH/ark-mediator-framework-workspace/skill-snapshot/`, where `SCRATCH=/tmp/claude-0/-workspace-Ark-Tools/9def409b-6ec2-5036-9f28-ced3d6db5a40/scratchpad`.

- [ ] **Step 1: Snapshot the old skill** (the eval baseline must be taken before editing)

Run: `mkdir -p $SCRATCH/ark-mediator-framework-workspace && git show HEAD:agents-plugins/ark-csharp/.apm/skills/ark-mediator-framework/SKILL.md > /dev/null && cp -r agents-plugins/ark-csharp/.apm/skills/ark-mediator-framework $SCRATCH/ark-mediator-framework-workspace/skill-snapshot`
Expected: `skill-snapshot/SKILL.md` exists and matches `git show HEAD:...`.

- [ ] **Step 2: Rewrite `SKILL.md`** (target ≤ 160 lines):
  - Frontmatter description: `Use when consuming Ark.Tools.MediatorFramework from NuGet to design, add, change or review a contract (query, request, command, event or message) — its fields, routes, gRPC methods, validators, business rules, authorization, versioning, enums or personal data — or its handler, participant or endpoint.`
  - `## Contract design gate`: steps from spec Part 2, with the checklist URL `https://raw.githubusercontent.com/ARKlab/Ark.Tools/master/docs/mediator-framework/contract-design-checklist.md`. Use `master`: the existing `main` links in the skill point to a branch that doesn't exist, so fix those too.
  - `## Checklist index`: 18 numbered lines, each `N. <heading text> — <hard default>`.
  - Keep `## Generator boundary` and `## Transactional messaging` unchanged. Trim `## Workflow` to the steps not covered by the gate. Keep `## Guardrails`.
  - `## Red flags - stop`: keep the existing items and add the six from spec Part 2.
  - `## Common rationalizations`: add the two new rows.
- [ ] **Step 3: Verify the index against the doc headings**

Run:
```bash
diff <(grep -oP '^## \d+\. \K.*' docs/mediator-framework/contract-design-checklist.md) \
     <(grep -oP '^\d+\. \K.*?(?= — )' agents-plugins/ark-csharp/.apm/skills/ark-mediator-framework/SKILL.md)
```
Expected: no output (section 0 is not in the index; the 18 names match).

- [ ] **Step 4: Bump the version and regenerate**

Run: `sed -i 's/^version: 1.4.0/version: 1.5.0/' agents-plugins/ark-csharp/apm.yml && apm install && apm run plugins:build`
Expected: exit 0; `git status` shows the regenerated skill copies and `agents-plugins/published/ark-csharp` changes.

- [ ] **Step 5: Verify**

Run: `apm run plugins:check && apm audit --ci && git diff --exit-code -- apm.lock.yaml`
Expected: all exit 0. If `apm.lock.yaml` changes only because of the local package hash, commit it and note that in the commit body.

- [ ] **Step 6: Commit**

```bash
git add -A agents-plugins .claude .agents .github .claude-plugin apm.lock.yaml
git commit -m "feat(ark-csharp): gate mediator contracts on the design checklist" -m "Assisted-by: Claude"
```

### Task 4: Skill-creator eval loop (old vs new)

**Files (scratchpad only, never committed):**
- Create: `$SCRATCH/ark-mediator-framework-workspace/evals/evals.json`
- Create: `$SCRATCH/ark-mediator-framework-workspace/iteration-1/<eval-name>/{with_skill,old_skill}/outputs/brief.md`, plus `eval_metadata.json`, `timing.json`, `grading.json`, `benchmark.json`
- Create: `$SCRATCH/ark-mediator-framework-workspace/iteration-1/review.html`

**Interfaces:**
- Consumes: the new skill from Task 3 and `skill-snapshot/` from Task 3 Step 1.

- [ ] **Step 1: Write `evals.json`** with three evals, each named for its directory: `crm-contact-update`, `book-status-archived-event`, `rename-author-to-authors`. Prompts are as in the spec Eval section, each ending: "Assume the app is in a repo using the Mediator Framework sample conventions. Produce the contract brief and the list of questions you would ask me, save it to outputs/brief.md, and do not write any code."
- [ ] **Step 2: Spawn 6 subagents in one turn**: with_skill (pointing at `agents-plugins/ark-csharp/.apm/skills/ark-mediator-framework`) and old_skill (pointing at `skill-snapshot`) for each eval, using the skill-creator run template. Save `timing.json` from each completion notification.
- [ ] **Step 3: While they run, add assertions to `eval_metadata.json` and `evals.json`:**
  - `all-sections-present`: 18/18 headings in brief.md
  - `answers-tagged`: every answer is tagged `derived` or `ASK`
  - `legal-items-ask`: purpose, lawful basis, retention and DPIA are all `ASK`
  - `field-table`: a field table with the 5 updatability labels
  - `no-code-written`: no `.cs` files in outputs
  - `crm-contact-update`: `pii-classified` (email and phone classified), `deny-by-id-type` (403/404 reasoned from the id type)
  - `book-status-archived-event`: `evolvable-enum`, `event-ids-only` (cross-app event without personal data), `queue-ttl`
  - `rename-author-to-authors`: `additive-first` (optional `Authors` field plus keeping `Author`, or a reasoned new version)
- [ ] **Step 4: Grade.** Check `all-sections-present`, `answers-tagged` and `no-code-written` with a script; grade the rest with a grader subagent using `agents/grader.md`. Write `grading.json` with fields `text`, `passed`, `evidence`.
- [ ] **Step 5: Aggregate and build the viewer**

Run: `cd <skill-creator> && python -m scripts.aggregate_benchmark $SCRATCH/ark-mediator-framework-workspace/iteration-1 --skill-name ark-mediator-framework && python eval-viewer/generate_review.py $SCRATCH/ark-mediator-framework-workspace/iteration-1 --skill-name ark-mediator-framework --benchmark $SCRATCH/ark-mediator-framework-workspace/iteration-1/benchmark.json --static $SCRATCH/ark-mediator-framework-workspace/iteration-1/review.html`
Expected: `benchmark.json` with `with_skill` pass rate above `old_skill`; `review.html` written.

- [ ] **Step 6: Send `review.html` to the user** with SendUserFile and wait for `feedback.json`. Iterate: change the skill (and the checklist if a section proves unclear), rerun into `iteration-N+1` with `--previous-workspace`, and repeat until the user accepts. Commit each skill/doc change with `fix(ark-csharp): …` or `docs(MediatorFramework): …`, regenerating as in Task 3 Steps 4–5.

### Task 5: Finish the PR

**Files:**
- None new. Updates the PR #1099 description.

- [ ] **Step 1: Offer description optimisation** (skill-creator `scripts.run_loop`, model `claude-opus-5-5`). Run it only if the user accepts; apply `best_description` and regenerate as in Task 3 Steps 4–5.
- [ ] **Step 2: Final verification**

Run: the Task 1 Step 3 link check over every changed `docs/mediator-framework/*.md` file, then `apm run plugins:check && apm audit --ci`.
Expected: `[]` and exit 0.

- [ ] **Step 3: Push and update the PR body** with a summary, the eval results table (pass rate old vs new per eval), and the follow-ups listed in the spec's Out of scope section.

Run: `git push -u origin ccr-7993adcb-ffvrmm`
Expected: the branch is up to date with origin; CI green on #1099.
