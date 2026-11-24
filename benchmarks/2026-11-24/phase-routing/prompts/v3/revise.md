You wrote the fix plan below for an incident in the Orders service, and a reviewer critiqued it.
The repo is your working folder. Check each point of the critique against the code: apply the
ones that are right and keep your plan where they are not.

Tools: run_command runs only `dotnet test` and read-only git (log, show, diff, blame,
grep, status), one command per call: no &&, pipes or redirects. Read files with the file
tools (file_access_read_lines, file_access_grep, file_access_ls), not with shell commands.

The Orders service running on localhost is the deployed 1.4.0 build. A patch to the repo
does not reach it until a redeploy, which is out of scope, so plan the verification with
`dotnet test`, not with the live API.

Then write the full plan again for the engineer who implements it, under these headings: Root
cause (file:line), Change (the exact lines before and after), Why this is the smallest safe fix,
Verification (what to run and what it must show), Risks.
