---
name: feature-planning
description: Plan bounded StarPie features with architecture placement, compatibility rules, verification gates, worktree scope, and GPT-to-Gemini handoff contracts. Use before multi-file implementation or delegated development.
---

# StarPie Feature Planning

## Read selectively

Always read the root `AGENTS.md` and `docs/architecture.md`. Then load only the affected domain documents listed in the root routing table.

## Produce a bounded plan

Define:

- user outcome and non-goals;
- repository, isolated worktree, base ref and existing dirty state;
- owning layer and why the work belongs there;
- allowed and forbidden paths;
- compatibility, latency, memory, security/capability and localization impacts;
- implementation stages with observable deliverables;
- automated gates, independent review and human acceptance;
- maximum repair cycles, deadline, stop conditions and minimum useful incomplete outcome.

Prefer additive, reversible changes. Reuse existing fact sources, renderers, forms, host services and action queues instead of creating parallel systems.

## GPT to Gemini handoff

The handoff must include exact worktree, base commit, model profile, task ID/revision, allowed scope, required commands, evidence format and prohibited actions. Do not authorize commit, push, merge, Tag or Release unless the user separately did so.

After the implementation request is visibly delivered and work starts, end the Codex turn. Do not poll, sleep, monitor logs or spend tokens on routine status. Resume only after the user reports completion, asks for progress, or requests troubleshooting.

Do not reset budget by renaming or splitting a task. If a repair would exceed the contract or deadline, stop and request a new bounded authorization.
