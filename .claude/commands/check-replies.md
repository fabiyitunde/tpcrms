---
description: Find CRMSQA issues where QA has answered my questions
---

1. Get my login: `gh api user --jq .login`
2. List the waiting issues:
   `gh issue list -R adeoluwagbemiga/CRMSQA --assignee @me --label "status:needs-info" --json number,title,comments`
3. For each one, check who wrote the latest comment.
   - **Someone other than me:** QA has replied.
   - **Me:** it's still waiting.
4. Report two lists:
   - **Replied:** issue number, title, and a one-line summary of QA's answer
   - **Still waiting:** issue number, title, and how long since my question
5. Ask me which replied issue to resume. When I pick one:
   - Set it back to in progress: `gh issue edit <n> -R adeoluwagbemiga/CRMSQA --add-label "status:in-progress" --remove-label "status:needs-info"`
   - Continue the work under the "QA issue workflow" rules in CLAUDE.md.

Don't change any labels until I've chosen.
