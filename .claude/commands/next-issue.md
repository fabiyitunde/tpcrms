---
description: Claim and work on the next QA issue from CRMSQA
---

Follow the "QA issue workflow" section in CLAUDE.md.

1. Check that nothing is already in progress:
   `gh issue list -R adeoluwagbemiga/CRMSQA --assignee @me --label "status:in-progress" --json number,title`
   If an issue is returned, tell me which one and stop. Don't claim a second one.
2. Find the next issue, in this order, stopping at the first that returns a result:
   - `gh issue list -R adeoluwagbemiga/CRMSQA --assignee @me --label "status:rework" --json number,title,createdAt`
   - `gh issue list -R adeoluwagbemiga/CRMSQA --search "no:assignee label:status:new label:priority:high sort:created-asc" --json number,title`
   - `gh issue list -R adeoluwagbemiga/CRMSQA --search "no:assignee label:status:new sort:created-asc" --json number,title`
   If none are found, tell me the queue is empty and stop.
3. Read the issue in full: `gh issue view <n> -R adeoluwagbemiga/CRMSQA --comments`
4. Investigate without changing anything. Look at the screenshots and read the relevant code to find the likely cause.
5. Give me a short plan, then **stop and wait for my approval**:
   - **Issue:** what QA reported, in one or two lines
   - **Cause:** where the cause is (CRMS rule, CRMS → Portal data, or Farmers Portal) and why
   - **Fix:** what you'll change, and which files
   - **Test:** how you'll check it
   - **Questions for QA:** anything unclear, or "None"
6. Then act on my reply:
   - **Go:** claim the issue (`gh issue edit <n> -R adeoluwagbemiga/CRMSQA --add-assignee @me --add-label "status:in-progress" --remove-label "status:new" --remove-label "status:rework"`, then comment "Picked up."). Do the work, then follow rule 7 in CLAUDE.md.
   - **Ask QA:** follow rule 5 in CLAUDE.md.
   - **Changes:** revise the plan and show me again.
   - **Skip:** leave the issue untouched.
