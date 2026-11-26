# create-ai-structure — Antigravity Workspace Rules

## 1. Self-Hosting Guardrail
- The live workflow files in this repository (`.claude/`, `.agents/`, `CLAUDE.md`, `AGENTS.md`, `t3.json`, `.workflow-gates.json`, `docs/constitution/`) are **read-only reference infrastructure**. Never modify them during feature work.
- Any files meant to be scaffolded into target repositories must live under `templates/` (e.g., `templates/base/`, `templates/overlays/webApp/`).

## 2. Shared Orchestration Rules
- **GitHub Issues are the single source of truth:** All agent-to-agent handoffs happen through GitHub Issues, Sub-Issues, labels, and Pull Requests via the `gh` CLI—never through raw chat transcripts.
- **Respect `.workflow-gates.json`:** Always read `.workflow-gates.json` before dispatching downstream work or enforcing human approval gates.
- **State-Machine Labels:**
  - `epic` — Marks a DAG Phase parent issue.
  - `status:backlog` — Waiting on upstream dependencies or a free WIP slot.
  - `status:in-progress` — Active Epic counting toward `max_active_epics`.
  - `stage:po-spec` — PO agent writing detailed requirements and Acceptance Criteria.
  - `stage:ready-for-arch` — Spec finalized and waiting on Gate 1 or Claude Architect pickup.
  - `stage:implementing` — Claude Architect and subagents executing Sub-Issues in a worktree.
  - `stage:qa` — PR open; Antigravity QA agent running verification.
  - `stage:human-review` — QA report attached to PR and Epic; awaiting human decision.