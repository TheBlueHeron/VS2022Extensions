# Big Bang upgrade to `net10.0` (VS2022Extensions)

## Overview

Atomic upgrade of the `SolutionCleanup` Visual Studio extension project to `net10.0`: convert `SolutionCleanup\SolutionCleanup.csproj` to SDK-style, update `TargetFramework` to `net10.0`, apply package updates per Plan Appendix, restore/build/fix compilation, and produce packaging artifact if supported. Manual UI validation (installing the VSIX) is excluded (non-automatable).

**Progress**: 0/4 tasks complete (0%) ![0%](https://progress-bar.xyz/0)

## Tasks

### [▶] TASK-001: Verify prerequisites (SDK and config)
**References**: Plan §Phase 0 (Preparation), Plan §2.1 (Approach Selection)

- [▶] (1) Verify that the .NET 10 SDK is installed on the upgrade machine using `dotnet --list-sdks`.
- [▶] (2) Confirm `global.json` (if present) allows or points to a .NET 10 SDK per Plan §Phase 0 (update only if safe and explicitly required) (**Verify**).
- [▶] (3) Verify required CLI tooling is available (e.g., `dotnet`, `msbuild`/VS build tools) per Plan §Phase 0 (**Verify**).

### [ ] TASK-002: Atomic project file conversion, target framework change and package updates
**References**: Plan §4 (Project-by-Project Migration Plans), Plan §Appendix (Package Update Reference), Plan §3.2 (Dependency Analysis)

- [ ] (1) Convert `SolutionCleanup\SolutionCleanup.csproj` to SDK-style per Plan §4 (create SDK-style `<Project Sdk="Microsoft.NET.Sdk">` layout, re-add required metadata and non-default includes).
- [ ] (2) Set `<TargetFramework>` to `net10.0` in the new SDK-style project (per Plan §4).
- [ ] (3) Update `PackageReference` entries in the SDK-style project per Plan §Appendix (apply compatible versions listed; for packages marked "no supported version found" document them as blockers per Plan §Appendix) (**Intent**: `MessagePack` keep at `3.1.4`; investigate/record `Community.VisualStudio.*` and `Microsoft.VSSDK.BuildTools` incompatibilities).
- [ ] (4) Run `dotnet restore` for the solution and ensure restore completes successfully (**Verify**).
- [ ] (5) Build the solution and fix all compilation errors strictly per Plan §5 (Breaking Changes Catalog). Rebuild after fixes. (**Verify**: Solution builds with 0 errors)

### [ ] TASK-003: Automated test run (if any) and packaging artifact build
**References**: Plan §6 (Testing and Validation), Plan §Project-specific Notes (VSIX considerations)

- [ ] (1) Per Plan §6 the assessment lists no test projects. Record "no test projects found" in `UPGRADE-VALIDATION.md` (**Verify**). If test projects are explicitly listed in Plan §6, run `dotnet test` only on those named projects (do not perform discovery).
- [ ] (2) Attempt automated packaging build (produce VSIX or packaging artifact) per Plan §Project-specific Notes. Run the repository's packaging/build target (MSBuild/dotnet pack or equivalent) and verify that an expected packaging artifact is produced (**Verify**).
- [ ] (3) If packaging fails due to incompatible VS/VSIX packages (`Community.VisualStudio.*`, `Community.VisualStudio.VSCT`, etc.), document the blocker(s) and error output in `UPGRADE-VALIDATION.md` referencing Plan §Risk Management (do not perform manual install or manual smoke tests in this task) (**Verify**).

### [ ] TASK-004: Commit upgrade changes (single atomic commit)
**References**: Plan §8 (Source Control Strategy), Plan §10 (Actionable Next Steps)

- [ ] (1) Create a single atomic commit containing the project conversion, `TargetFramework` changes, and package updates with message: `"chore(upgrade): migrate solution to net10.0 (atomic)"`.
- [ ] (2) Verify the commit exists and contains the expected file changes (SDK-style `.csproj`, package updates, `UPGRADE-VALIDATION.md`) (**Verify**).
- [ ] (3) Optional: push the local commit to remote and open PR per repo policy (only if repository CI/branch workflow requires it) — treat push/PR as an optional step per Plan §8 (do not change branches in this task).

--- 

Generation notes / constraints applied
- Batching follows Big Bang rules: project conversion + package updates + compilation fixes combined into `TASK-002`.
- Prerequisites and testing/packaging separated into their own tasks.
- Excluded non-automatable/manual UI verification steps (manual VSIX install) per strategy exclusions.
- No dynamic discovery actions: test steps reference Plan §6 (no test projects) or named projects only — do not search the repo at runtime.
- No branch switching or backup creation actions are included in tasks (per generator constraints).