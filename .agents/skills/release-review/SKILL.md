---
name: release-review
description: Review a StarPie release candidate for version consistency, package boundaries, plugin distribution rules, evidence completeness, upgrade behavior, and authorization before merge, tags, installers, or GitHub Release publication.
---

# StarPie Release Review

Read the root `AGENTS.md`, `docs/release.md`, `docs/testing.md`, and any domain document touched by the candidate.

## Review inputs

- exact candidate commit and target branch;
- diff and commit trail;
- version target and changelog entry;
- build/test receipts tied to the candidate;
- human acceptance status;
- intended artifacts and external operations.

## Checks

- All version synchronization points agree.
- No unrelated dirty files or worktree artifacts enter the candidate.
- Main packages do not contain official plugin DLLs or the legacy plugin source directory.
- Clean data startup performs no silent network/install; installed plugins still work offline.
- Lightweight, Standalone and Setup outputs match naming and content rules.
- SHA-256 values are generated from final artifacts, not pre-final files.
- Upgrade/install paths preserve user configuration and do not silently enable plugins.
- Required independent review and human gates are complete.

## Authorization boundary

Review and preparation do not authorize commit, push, merge, Tag, installer publication or GitHub Release. Identify the next external action and obtain explicit authorization for it. If the requested version is wrong or an artifact is already public, stop and propose recovery; never silently overwrite release history.

Return a verdict of approve, request changes, or blocked, with concrete evidence and unverified items.
