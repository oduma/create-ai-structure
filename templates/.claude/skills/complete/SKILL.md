---
name: complete
description: Finalizes a completed Epic by running a security audit, appending an As-Built Implementation Summary to the GitHub Epic, opening an assigned PR, and delegating to the Antigravity QA agent.
---
Execute the Epic completion workflow for the provided GitHub Epic URL or number:

1. **Verify Sub-Issues:** Run `gh issue view <epic-number> --json subIssues` (or check linked Sub-Issues) and confirm every Sub-Issue is `CLOSED` and `dotnet test` passes on the worktree branch.
2. **Security Audit:** Invoke `security-officer` to review `git diff main...HEAD`. Resolve any findings before continuing.
3. **Update GitHub Epic As-Built:** Append an `## As-Built Implementation Summary` section to the GitHub Epic body (via `gh issue edit <epic-number>`) detailing:
   - Implemented classes, commands, and file paths.
   - How each Acceptance Criterion was satisfied.
   - Any assumptions or edge-case decisions made during implementation.
4. **Push & Open PR:** Push the worktree branch (`git push -u origin HEAD`) and create a Pull Request assigned to the repository owner:
   `gh pr create --assignee @me --title "<Epic Title>" --body "Closes #<epic-number>\n\n<Concise summary of changes and test results>"`
5. **Transition to QA:** Update Epic labels via `gh issue edit <epic-number> --remove-label "stage:implementing" --add-label "stage:qa"`, then delegate the Epic URL and PR URL to the Antigravity `qa-verifier` agent.