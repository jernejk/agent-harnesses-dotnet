# Excluded runs

- `ollama-inkling-low-2` (24 Nov 2026, 00:37): no demo ran. The boundary test
  `BudgetTests.The_run_budget_stops_git_inside_write_proposal` failed with
  `FileNotFoundException: parent.pid` after 5 s, while two other agents were building and testing
  .NET code on the same Mac. The test waits for a child process to write its pid before the budget
  kills it; under CPU load the child was too slow. It passed in every other run that night, so this
  is a timing-sensitive test, not a regression. The run was repeated later as `ollama-inkling-low-3`.
- `foundry-local-qwen3-4b-2` (24 Nov 2026, 00:46): no demo ran. The same boundary test failed the
  same way while a 340 MB container image was being pulled and another agent was building .NET code.
  Two failures in one night under load make this a finding in its own right: the test's timing
  assumption is too tight for a busy machine. Repeated later as `foundry-local-qwen3-4b-3`.
- `lmstudio-inkling-low-1` (24 Nov 2026, 01:07): no model call was made. LM Studio 0.4.28 could not
  load its copy of inkling-2-mini (GGUF Q4_K_M): "error loading model: unknown model architecture:
  'inkling2_loop'". Every demo then got HTTP 400 from LM Studio. This is a runtime gap, not a result
  for the model: the same model runs on Ollama 0.40.17's MLX runner. Recorded as a finding instead.
