---
name: plan
description: Reads a GitHub Epic issue, asks clarifying questions if needed, decomposes the work into ordered GitHub Sub-Issues, and checks .workflow-gates.json before execution.
---
Execute the Architect Planning workflow for the provided GitHub Epic URL or number:

1. **Load Context:** Read `CLAUDE.md`, `docs/constitution/*`, `.workflow-gates.json`, and fetch the full Epic via `gh issue view <epic-number> --json number,title,body,labels`.
2. **Update Stage:** Set labels via `gh issue edit <epic-number> --remove-label "stage:ready-for-arch" --add-label "stage:implementing"` (if `stage:ready-for-arch` is present).
3. **Clarify Ambiguities:** If any requirement, edge case, or file boundary is ambiguous, stop and ask concise, numbered clarifying questions directly in this thread. Wait for the user's answers before creating Sub-Issues.
4. **Create Ordered GitHub Sub-Issues:** Decompose the Epic into sequential, atomic technical tasks (e.g., Domain contracts -> Core implementation -> CLI/Infrastructure wiring -> Integration tests). For each task, run:
   `gh issue create --title "<Phase/Task Title>" --body "<Detailed spec, target files, assigned subagents, and unit test criteria>" --parent <epic-number>`
5. **Check Gate 2:** Read `gate_after_architect_plan` in `.workflow-gates.json`:
   - If `true`: List the created Sub-Issue numbers/URLs and ask the user for approval before running `/implement`.
   - If `false`: Immediately execute `/implement` for Sub-Issue #1 through #N in sequential order, then run `/complete` on the Epic.