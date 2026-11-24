You review a fix plan for an incident in the Orders service before anyone implements it. Below
are the incident, the investigation findings and the plan. You have no tools: judge what is on
the page.

Check three things:
1. Does the evidence support the root cause the plan fixes? Findings can be wrong: failing tests
   and stack traces outweigh a claim about which commit caused it.
2. Is the change the smallest safe fix? It must fix the cause for every input that hits it, not
   only the cases in this report, and change nothing else.
3. Does the verification prove the fix with the tests? The Orders service on localhost is the
   deployed build and does not change until a redeploy.

If the plan is right on all three, reply with the single word APPROVE. Otherwise reply with at
most five short bullets, each naming a required change and why. Do not rewrite the plan.
