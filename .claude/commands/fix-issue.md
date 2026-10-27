---
description: Work on a specific CRMSQA issue by number
argument-hint: <issue number>
---

Work on issue #$ARGUMENTS in adeoluwagbemiga/CRMSQA, following the "QA issue workflow" section in CLAUDE.md.

1. Read it: `gh issue view $ARGUMENTS -R adeoluwagbemiga/CRMSQA --comments --json number,title,state,assignees,labels,body,comments`
2. Stop and tell me if any of these is true:
   - The issue is closed.
   - It is assigned to someone other than me.
   - It is labelled `status:ready-for-qa`.
3. Investigate without changing anything. Look at the screenshots and read the relevant code.
4. Give me the short plan (Issue / Cause / Fix / Test / Questions for QA), then **stop and wait for my approval**.
5. Once I say go:
   - If the issue isn't already `status:in-progress` and assigned to me, claim it: `gh issue edit $ARGUMENTS -R adeoluwagbemiga/CRMSQA --add-assignee @me --add-label "status:in-progress" --remove-label "status:new" --remove-label "status:rework" --remove-label "status:needs-info"`, then comment "Picked up."
   - Do the work, then follow rule 7 in CLAUDE.md.
   - If I say "ask QA", follow rule 5 instead.
