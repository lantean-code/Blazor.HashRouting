# AGENTS.md (root)

> Scope: This file gives high-level context and guardrails for the repository root. Deeper folders may add their own `AGENTS.md` files which take precedence for their subtrees (for example `./src/AGENTS.md` and `./test/AGENTS.md`).

## Project overview
- `Blazor.HashRouting` is a reusable hash-fragment routing engine for browser-hosted Blazor WebAssembly applications.
- Primary goals: correct routing behavior, low overhead, strong test coverage, and NuGet-ready packaging.
- Non-goals: Blazor Server support, Blazor Hybrid support, or app-specific behavior outside generic hash routing.

## Repository layout
- Solution: `Blazor.HashRouting.slnx`
- Projects:
  - `Blazor.HashRouting` — the package source and static web assets.
  - `Blazor.HashRouting.Test` — unit tests for the package.
- Config/conventions: `.editorconfig`, `.gitignore`, `global.json`.

## Build, test, publish
- Prerequisites: .NET SDK version pinned by `global.json`.
  - Agents must verify the pinned SDK is available in the current environment before running restore/build/test commands.
  - Agents must include `--artifacts-path=/tmp/artifacts/blazor-hashrouting` on all `dotnet` commands.
- Restore & build:
  - `dotnet restore --artifacts-path=/tmp/artifacts/blazor-hashrouting`
  - `dotnet build --artifacts-path=/tmp/artifacts/blazor-hashrouting`
- Run tests:
  - `dotnet test --artifacts-path=/tmp/artifacts/blazor-hashrouting`
- Create packages:
  - `dotnet pack --artifacts-path=/tmp/artifacts/blazor-hashrouting`
- After each behavior-affecting set of changes:
  - Run `dotnet test --artifacts-path=/tmp/artifacts/blazor-hashrouting`.
  - Behavior-affecting includes edits to production code, test code, project/package/build configuration, or other runtime-impacting assets.
  - Docs-only/report-only/markdown-only edits do not require restore/build/test unless explicitly requested.

## Coding and test standards
- Source code rules and generation constraints live in `./src/AGENTS.md`.
- Unit test rules live in `./test/AGENTS.md`.
- If rules conflict, the deeper file wins; otherwise, follow both.

## Line endings
- Use CRLF line terminators for any files you write or modify.

## Git permissions
- Agents must not perform git write operations unless the user gives explicit permission in the current conversation.
- Git write operations include (but are not limited to): `commit`, `push`, `pull`, `merge`, `rebase`, `cherry-pick`, `reset`, `revert`, `checkout`/`switch` that changes branch or files, tag creation/deletion, and branch creation/deletion.
- Until explicit permission is granted, only read-only git commands are allowed.

## How to work in this repo
1. Read this file, then the relevant folder `AGENTS.md`.
2. Before modifying code:
   - Confirm SDK target, nullable context, analyzers, and editorconfig rules.
   - Keep the public package surface consistent unless the user explicitly requests a breaking change.
3. When generating code:
   - Follow `./src/AGENTS.md` exactly.
   - Prefer minimal, maintainable changes and avoid churn to unrelated files.
4. When writing tests:
   - Follow `./test/AGENTS.md` exactly.
5. Before opening a PR or preparing a release:
   - Build succeeds and tests are green.
   - Public XML docs are added or updated where required.
   - Package metadata, README, and changelog are consistent with the change.

## PR and review checklist
- [ ] Change is scoped and justified; no unrelated edits.
- [ ] Code adheres to `./src/AGENTS.md` standards.
- [ ] Tests adhere to `./test/AGENTS.md` and preserve full coverage expectations.
- [ ] No secrets, tokens, or user-specific paths are committed.
- [ ] `dotnet restore`, `build`, `test`, and `pack` succeed with the pinned SDK.
- [ ] Error messages and logs are clear and actionable.

## Communication & assumptions
- Do not guess. If any requirement, API contract, or behavior is unclear, ask for clarification.
- Prefer concise diffs and explicit rationale in commit messages and PR descriptions.
