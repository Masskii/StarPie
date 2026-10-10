---
name: regression-testing
description: Design and run StarPie regression verification tied to the exact candidate, including Release builds, plugin self-tests, static guards, UIA boundaries, mutation checks, and human acceptance. Use after implementation or when strengthening tests.
---

# StarPie Regression Testing

Read `docs/testing.md` and the domain document for the changed code.

## Build the matrix

Cover:

- the reported scenario or requested behavior;
- neighboring existing behavior likely to regress;
- empty, missing, invalid and legacy data;
- disabled/uninstalled plugin and failed dependency states where relevant;
- language/theme/DPI states for UI work;
- cancellation, timeout and release for asynchronous plugin work;
- scope and packaging boundaries.

## Evidence rules

- Bind all results to a worktree plus commit/diff identity.
- Record exact commands, exit codes and important output.
- Re-run gates yourself; do not accept implementation summaries as proof.
- A source-text presence check is a structural smoke test, not behavior coverage.
- New assertions should demonstrate a red state through the base version, a safe mutation, or a concrete counterexample.
- Do not weaken or delete tests, alter warning policy, or update baselines merely to make a run green.

## Safe execution

- Automated tests must not send real input, run user commands, alter windows/hardware, or invoke live plugin actions.
- Use `--plugin-selftest --skip-invoke` and safe sentinel inputs.
- Do not run GUI tests unless the user explicitly requests it; provide the command and manual acceptance checklist instead.

Report pass/fail/blocked separately for automated gates, independent review and human gates. Never call the task complete while a required gate is pending.
