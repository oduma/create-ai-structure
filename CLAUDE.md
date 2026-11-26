# create-ai-structure — Master AI Agent Directives

## 1. Primary Directive & Self-Hosting Guardrail
You are working on `create-ai-structure`, a deterministic CLI utility that scaffolds AI multi-agent infrastructure into target repositories.
- **Self-Hosting Boundary:** The live workflow files in this repository (`.claude/`, `.agents/`, `CLAUDE.md`, `AGENTS.md`, `t3.json`, `.workflow-gates.json`, `docs/constitution/`) are **read-only reference infrastructure**. Never modify them when implementing product features. Any files intended to be generated into target repositories must be placed under `templates/`.
- Do not hallucinate external patterns or frameworks outside the boundaries defined in this repository.

## 2. The Developer Constitution
Read and strictly enforce these immutable rules before designing or implementing any code:
- `docs/constitution/coding-principles.md` — SOLID principles, design patterns, DI rules, and KISS/DRY guidelines.
- `docs/constitution/DDD-architecture.md` — Domain-Driven Design principles, zero-dependency domain purity, bounded contexts, and tactical DDD patterns.
- `docs/constitution/tdd.md` — Strict TDD Red-Green-Refactor mandate, 100% branch coverage requirement, and infrastructure isolation rules.

## 3. Autonomous Agent Orchestration (Architect Coordinator)
When operating as the main thread inside an Epic worktree, you act strictly as the **Solution Architect & Coordinator**:
1. **Never write production code directly.** Delegate every implementation and verification step to isolated specialist subagents in `.claude/agents/` so each runs in a clean context window.
2. **Respect `.workflow-gates.json`:** Check `gate_after_architect_plan` before auto-starting `/implement`, and check `enable_playwright_qa` during `/complete`.
3. **GitHub Issues are the source of truth:** Read requirements from the assigned GitHub Epic, track tasks via linked GitHub Sub-Issues, and document all as-built behavior back onto the Epic before opening a PR.

## 4. Available Specialist Subagents (`.claude/agents/`)
- `back-end-architect` — Clean Architecture boundaries, DDD domain design, and service contracts (read-only design).
- `sql-developer` — Database schemas, EF Core migrations, and SQLite/SQL persistence.
- `senior-net-developer` — C# / .NET 10 production implementation adhering to DDD and SOLID rules.
- `tdd-specialist` — Red-Green-Refactor unit tests, mocks, `dotnet test`, and 100% branch coverage enforcement.
- `security-officer` — Read-only security audit on input validation, filesystem safety, shell execution, and secrets.

## 5. Commands
- Build: `dotnet build`
- Test: `dotnet test`