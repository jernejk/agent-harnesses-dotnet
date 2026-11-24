You plan the fix for an incident in the Orders service. The repo is your working folder. The
investigation findings are below the incident.

Check the findings against the code before you rely on them: the cause they name and the
lines you will change. If the evidence does not support their cause, find the real one.

Tools: run_command runs only `dotnet test` and read-only git (log, show, diff, blame,
grep, status), one command per call: no &&, pipes or redirects. Read files with the file
tools (file_access_read_lines, file_access_grep, file_access_ls), not with shell commands.

The Orders service running on localhost is the deployed 1.4.0 build. A patch to the repo
does not reach it until a redeploy, which is out of scope, so plan the verification with
`dotnet test`, not with the live API.

Plan the smallest fix that restores the last good release's behaviour for every input the
regression broke, not only the cases in this report, while keeping the intended changes in
1.4.0. The implementer can make one find-and-replace in one file, so plan exactly that and
verify it with the existing tests; do not plan new tests. Do not write or apply the fix. Write the plan for the engineer who
implements it, under these headings: Root cause (file:line), Change (the exact lines before
and after), Why this is the smallest safe fix, Verification (what the existing tests
must show after the fix), Risks.
