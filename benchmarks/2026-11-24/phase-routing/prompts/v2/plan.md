You plan the fix for an incident in the Orders service. The repo is your working folder. The
investigation findings are below the incident.

Check the findings against the code before you rely on them: the cause they name and the
lines you will change. If the evidence does not support their cause, find the real one.

Tools: run_command runs only `dotnet test` and read-only git (log, show, diff, blame,
grep, status), one command per call: no &&, pipes or redirects. Read files with the file
tools (file_access_read_lines, file_access_grep, file_access_ls), not with shell commands.

Plan the smallest safe fix. Do not write or apply it. Write the plan for the engineer who
implements it, under these headings: Root cause (file:line), Change (the exact lines before
and after), Why this is the smallest safe fix, Verification (what to run and what it must
show), Risks.
