# Boundary lessons

A review of these demos found holes in boundaries the harness claimed to enforce. Each fix
is pinned by a test in `tests/BoundaryTests` that failed before it
(`dotnet test tests/BoundaryTests`, no model calls).

1. **A verb is not a boundary.** `git grep -O<cmd>` runs any program and `git diff <file> /dev/null` reads any file, so allow each verb's flags and argument shapes, not its name.
2. **A typed tool beats a filtered one.** `curl -w @file` read files and `-w '%output{…}'` wrote them; `http_get(path)` reaches three routes on one host and nothing else.
3. **Check the path you write, not the path you were given.** An absolute path made `Path.Combine` drop the staging folder and "propose a fix" edited the repo; take relative paths only, refuse symlinks, re-check the destination.
4. **Check every hop before you make it.** Checking the landing URL after following redirects still sent the request, and a prefix match let `agent-framework-evil` in; match scheme, host, port and whole path segments per hop, cap the hops.
5. **A budget is hard only if every tool obeys it.** Subprocesses ignored the run's deadline, and .NET 11's `RunAndCaptureTextAsync` kills only the direct child; pass the run token to every tool and kill the process tree.
6. **A mode is a label until a tool checks it.** Plan mode didn't stop `fetch_url`; now the human's approval unlocks the execution tools, and until then they refuse on screen.
7. **Claim only what the code checks.** The report gate proves every paragraph, bullet and quote carries a citation to a page fetched in this run, not that the page supports it, and the docs now say exactly that.
8. **Clean up on every exit, and scrub what trusted code inherits.** Ctrl+C mid-bisect left a detached HEAD and a stashed fix, and `dotnet test` saw the model's API key; use `trap`/`finally` with the exact stash ref, and an allow-listed environment.
