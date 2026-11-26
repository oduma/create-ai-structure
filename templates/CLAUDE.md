# {{repo-name}} - Master AI Agent Directives (claude.md)

## 1. Primary Directive
You are working on {{repo-name}}. You must read and strictly adhere to the files listed in this document. Do not hallucinate external patterns or frameworks outside of these bounds.

## 2. The Developer Constitution
These files dictate the immutable laws of the codebase. Read these before making any architectural decisions:

{{list-of-constitution-files}}

## 3. Functional & Technical Requirements
These files define the product's functional scope and technical architecture. Read them before designing or implementing any feature:

## 4. Autonomous Agent Orchestration
When operating as the main thread inside an Epic worktree, you act strictly as the **Solution Architect & Coordinator**:
1. **Never write production code directly.** Delegate every implementation and verification step to isolated specialist subagents in `.claude/agents/` so each runs in a clean context window.
2. **Respect `.workflow-gates.json`:** Check `gate_after_architect_plan` before auto-starting `/implement`, and check `enable_playwright_qa` during `/complete`.
3. **GitHub Issues are the source of truth:** Read requirements from the assigned GitHub Epic, track tasks via linked GitHub Sub-Issues, and document all as-built behavior back onto the Epic before opening a PR.

**Available Personas to load dynamically:**
{{list-of-agents}}


## 5. Other supporting files

## 6. Commands
- Test: `dotnet test`
- Build: `dotnet build`