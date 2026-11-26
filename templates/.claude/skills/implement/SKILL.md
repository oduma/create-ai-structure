---
name: implement
description: Executes a single GitHub Sub-Issue by delegating implementation and TDD verification to isolated .claude/agents/ specialists, committing passing code, and closing the Sub-Issue.
---
Execute the sequential implementation workflow for the provided GitHub Sub-Issue URL or number:

1. **Read Sub-Issue:** Fetch full details via `gh issue view <sub-issue-number> --json number,title,body,state`.
2. **Delegate Architecture (if needed):** If the Sub-Issue introduces new interfaces or domain boundaries, invoke `back-end-architect` (and `sql-developer` if persistence is involved) first.
3. **Delegate Implementation:** Invoke `senior-net-developer` with the Sub-Issue requirements and architectural blueprint. Do not write production code in the main thread.
4. **Delegate TDD & Verification:** Invoke `tdd-specialist` to write/verify unit tests and run `dotnet build` and `dotnet test`. Do not proceed until build and tests pass 100%.
5. **Commit & Close Sub-Issue:** Commit the verified changes to the current worktree branch with a clear commit message referencing `#<sub-issue-number>`, then close the Sub-Issue via `gh issue close <sub-issue-number> --comment "Implemented and verified with passing unit tests."`.