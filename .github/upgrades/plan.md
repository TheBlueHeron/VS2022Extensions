# Executive Summary

- Scenario: Upgrade Visual Studio extension project(s) from .NET Framework 4.8 to .NET 10.0 (preview) using a Big Bang approach.
- Scope: Single project: `SolutionCleanup\SolutionCleanup.csproj` (ClassicClassLibrary, 10 files, 514 LOC). Current target: `.NETFramework,Version=v4.8`. Proposed target: `net10.0`.
- Target State: All projects (the single project) converted to SDK-style, `TargetFramework` set to `net10.0`, all package updates applied per assessment, solution builds with zero errors and automated tests (if any) run and passing.

- Selected Strategy: Big Bang Strategy — All projects upgraded simultaneously in a single atomic operation.
  - Rationale: Single-project solution (very small), no inter-project dependencies, suitable for an atomic upgrade.

- Complexity Assessment: High
  - Justification: The project is a classic (non-SDK) Visual Studio extension targeting .NET Framework 4.8. Several NuGet packages used by the project are incompatible with modern .NET and at least two packages show "No supported version found" in the assessment. Visual Studio extension projects frequently must remain on .NET Framework or follow VS SDK migration guidance. Converting a VS extension to net10.0 is high-risk and may be blocked by platform/tooling incompatibilities.

- Critical Issues (blocking):
  1. `Community.VisualStudio.Toolkit.17` (17.0.533) — assessment: incompatible, no supported version found.
  2. `Community.VisualStudio.VSCT` (16.0.29.6) — incompatible, no supported version found.
  3. `Microsoft.VSSDK.BuildTools` (17.14.2101) — assessment suggests `15.7.104` (incompatible current version).
  4. Project is not SDK-style; assessment requires conversion to SDK-style prior to framework update.

- Recommended Approach: Proceed with a Big Bang upgrade only if team accepts the risk of non-trivial refactoring and platform incompatibility. If preserving extension compatibility with Visual Studio is required, evaluate maintaining a separate .NET Framework build or postponing migration.

---

## 2. Migration Strategy

### 2.1 Approach Selection

- Chosen Strategy: Big Bang Strategy — Upgrade the entire solution (the one project) atomically.
- Strategy Rationale: Single-project solution, no project-to-project dependencies, small codebase. Big Bang minimizes iteration overhead and aligns with the instruction to upgrade all projects simultaneously.
- Strategy-specific considerations:
  - All project file conversions (classic → SDK-style), target framework edits, and package updates will be applied in one atomic change set.
  - Because the project is a Visual Studio extension and many VS SDK packages are incompatible, expect blockers; plan includes explicit gating and rollback criteria.

### 2.2 Dependency-Based Ordering

- With one project and zero project dependencies, the dependency ordering is trivial: the only project is upgraded in the atomic operation.
- There are no circular dependencies.

### 2.3 Parallel vs Sequential Execution

- Not applicable — single-project upgrade performed atomically.

---

## 3. Detailed Dependency Analysis

### 3.1 Dependency Graph Summary

- `SolutionCleanup\SolutionCleanup.csproj` — leaf and root; no project references.

### 3.2 Project Groupings

- Phase 0: Preparation and tooling updates (SDK install, global.json validation)
- Phase 1: Atomic upgrade of `SolutionCleanup` (convert to SDK-style, change target framework to `net10.0`, update packages)
- Phase 2: Build, fix compilation issues, run tests, validation

---

## 4. Project-by-Project Migration Plans

### Project: `SolutionCleanup\SolutionCleanup.csproj`

Current State

- Path: `SolutionCleanup\SolutionCleanup.csproj`
- Project Kind: `ClassicClassLibrary` (Visual Studio extension)
- Current Target Framework: `.NETFramework,Version=v4.8`
- SDK-style: `False`
- Dependencies: none (project references)
- Dependants: none
- Files: 10
- LOC: 514

Target State

- Target Framework: `net10.0` (per assessment Proposed Target Framework)
- SDK-style: `True` (project converted to SDK-style format)
- Package updates applied per §Package Update Reference below

Migration Steps (atomic; perform as one coordinated change set)

1. Prerequisites (TASK-000, complete before atomic upgrade):
   - Ensure the .NET 10 SDK is installed on the machine(s) used for the upgrade (validate via `dotnet --list-sdks`).
   - Validate `global.json` (if present) to ensure it allows the .NET 10 SDK, or prepare to update it.
   - Confirm repository state: on branch `master` (user requested) with no pending changes. If pending changes exist, follow chosen action (commit/stash/undo). The analysis indicated `master` is current and there are no pending changes.
   - Create a backup branch or tag from `master` to preserve the pre-upgrade state (recommended) — e.g., `master-before-upgrade-net10`.

2. Framework & project-file updates (part of atomic TASK-001):
   - Convert `SolutionCleanup.csproj` to SDK-style. Suggested steps:
     - Create a temporary copy of the current `.csproj`.
     - Create an SDK-style project file using `<Project Sdk="Microsoft.NET.Sdk">` header.
     - Add `<TargetFramework>net10.0</TargetFramework>` in the new `<PropertyGroup>`.
     - Add required metadata (`PackageId`, `AssemblyName`, `RootNamespace`, `GeneratePackageOnBuild` as needed) and re-add any explicit compile/include items that are not using default SDK globbing.
     - Recreate `PackageReference` entries for packages that are compatible (use versions from assessment). For now, include `MessagePack` at `3.1.4` (assessment showed compatible).
     - Reapply any custom MSBuild properties, imports or VSIX-specific entries. Note: VSIX manifests and VS extension specifics may need special treatment; see Project-specific notes below.

   - If converting to SDK-style is not feasible for VS extension (due to missing SDK support in VSIX tooling), document this as a blocker and evaluate these alternatives:
     - Keep the project on .NET Framework and skip framework migration (create separate plan); or
     - Re-architect extension to use out-of-process components that can be migrated while keeping VS-specific parts on .NET Framework.

3. Package Updates (apply all updates in one batch):
   - From assessment, include these packages (name, current version, suggested target):

   | Package | Current Version | Suggested Version | Projects affected | Notes |
   |---------|-----------------|-------------------|-------------------|-------|
   | Community.VisualStudio.Toolkit.17 | 17.0.533 | (no supported version found) | SolutionCleanup | Incompatible - no supported upgrade; investigate alternative or remove.
   | Community.VisualStudio.VSCT | 16.0.29.6 | (no supported version found) | SolutionCleanup | Incompatible - investigate replacement.
   | Microsoft.VSSDK.BuildTools | 17.14.2101 | 15.7.104 | SolutionCleanup | Assessment suggests older tooling version; confirm compatibility with VS and SDK.
   | MessagePack | 3.1.4 | 3.1.4 | SolutionCleanup | Compatible - re-add as PackageReference.

   - Action guidance:
     - Add or update `PackageReference` entries in the new SDK-style project to the target versions where provided.
     - For packages with no supported version found, either:
       - Identify replacements or newer equivalents that support `net10.0` and the Visual Studio extension scenario, or
       - Keep VS-specific functionality on .NET Framework (rollback option), or
       - Use conditional multi-targeting (see below) only if feasible and supported by the build and VS extension packaging tooling.

4. Conditional multi-targeting (optional, high complexity):
   - If parts of the project must remain on .NET Framework (VSIX host requirements), consider multi-targeting the project (e.g., `TargetFrameworks=net10.0;net48`) and use conditional compilation to maintain VS-specific code on net48 while migrating library code to net10.0.
   - Note: Multi-targeting VS extension projects is non-trivial and may not be supported by VSIX packaging tools — treat as advanced option only.

5. Restore and Build (part of atomic TASK-001):
   - Run `dotnet restore` for the solution.
   - Build the solution. Expect compile-time errors related to API differences and missing/removed packages.
   - Address compilation issues discovered. Typical fixes may include:
     - Replacing or updating APIs removed or changed in newer .NET versions.
     - Adjusting MSBuild imports or conditional properties.
     - Removing or replacing unsupported VS SDK packages.

6. Tests and verification (TASK-002):
   - If any test projects exist (not listed in assessment), execute them. In this solution assessment none were found.
   - Validate extension packaging if applicable (VSIX) with existing packaging tools — confirm that VSIX manifest and packaging work with new project structure.

7. Validation / Acceptance criteria (part of TASK-002):
   - Solution builds with zero errors.
   - No unresolved package compatibility issues remain.
   - Extension packages (if produced) are buildable and installable in target Visual Studio versions (manual/automated smoke tests required).

Project-specific Notes

- Visual Studio extension considerations:
  - Many VS extension components and their NuGet packages are tied to Visual Studio SDK and .NET Framework. Migration to `net10.0` may be infeasible for the VSIX component. Investigate whether the extension can be migrated, or whether only library parts can be migrated while the VSIX layer remains on .NET Framework.
  - `Community.VisualStudio.Toolkit.17` and `Community.VisualStudio.VSCT` are flagged incompatible with no supported version — these are likely blockers for a full migration.
  - `Microsoft.VSSDK.BuildTools` suggests a downgrade; ensure version selection aligns with Visual Studio tooling used for packaging.

Validation Checklist (for `SolutionCleanup`)

- [ ] Backup/branch created (`master-before-upgrade-net10`).
- [ ] .NET 10 SDK installed and validated.
- [ ] Project converted to SDK-style (or documented blocker).
- [ ] `TargetFramework` set to `net10.0`.
- [ ] All `PackageReference` entries updated per assessment.
- [ ] `dotnet restore` completed successfully.
- [ ] Solution builds with zero errors.
- [ ] VSIX packaging succeeds (if applicable) or alternative packaging validated.

---

## 5. Risk Management

### 5.1 High-Risk Changes

| Project | Risk | Mitigation |
|---------|------|------------|
| SolutionCleanup | High — VS-specific packages incompatible or missing net10.0 support; classic project format | Investigate whether VSIX components must remain on .NET Framework; consider multi-targeting or retaining a Framework-only project. Add extra manual validation and create a fallback branch. |

Key mitigations:
- Perform an early spike to confirm whether `Community.VisualStudio.Toolkit.17` and `Community.VisualStudio.VSCT` have replacements or can be removed.
- If incompatible, plan to split code: keep VSIX host on net48 and migrate helper libraries to net10.0, or postpone migration.
- Create a backup tag/branch before atomic changes.

### 5.3 Contingency Plans

- If incompatible packages cannot be resolved:
  - Option A: Revert to pre-upgrade branch and postpone migration.
  - Option B: Implement a hybrid approach where UI/VSIX bits remain on .NET Framework and migrate library components to `net10.0` (requires splitting project or multi-targeting).

---

## 6. Testing and Validation Strategy

### 6.1 Phase-by-Phase Testing

Phase 0 (Preparation):
- Verify .NET 10 SDK presence
- Validate global.json

Phase 1 (Atomic upgrade):
- Restore and build solution
- Resolve compile-time errors

Phase 2 (Validation):
- Run unit tests (if present)
- Validate VSIX packaging (install into VS instance) — manual smoke test recommended
- Run static analysis and security scan for packages

### 6.2 Smoke Tests
- Build succeeds
- No compilation errors
- Extension package created (if applicable)
- Basic extension behavior manually verified in Visual Studio

### 6.3 Comprehensive Validation
- All automated tests pass
- Packaging validated for target Visual Studio versions
- No remaining incompatible packages

---

## 7. Timeline and Effort Estimates

- Preparation (Phase 0): 1–2 hours — install SDK, validate global.json, create backup branch
- Atomic upgrade (Phase 1): 4–16 hours — convert project file, update packages, run restore/build, fix compile issues (high variance due to VS-specific blockers)
- Testing and validation (Phase 2): 2–8 hours — packaging and manual VS verification

Total: 1–3 days (highly dependent on discovery of blockers)

---

## 8. Source Control Strategy

- Main upgrade branch: `master` (user requested). NOTE: This will perform the atomic change directly on `master` — this is risky. Strong recommendation: instead create a temporary branch (e.g., `upgrade/net10` or `master-before-upgrade-net10`) from `master`, perform the atomic changes there, open a PR for review, then merge to `master` after validation.
- Commit Strategy: Single atomic commit for the upgrade changes (project file conversion, TargetFramework update, package updates). Include a backup branch/tag prior to performing this commit.
- Commit message template: `chore(upgrade): migrate solution to net10.0 (atomic)`
- Review & Merge: Require code review and CI build passing before merge to `master`.

---

## 9. Success Criteria

- All projects target their Proposed Target Framework (`net10.0`).
- All package updates from the assessment are applied, or blocked items are documented with remediation plans.
- Solution builds with zero errors.
- Packaging (VSIX) completes or a documented migration alternative is implemented.

---

## 10. Actionable Next Steps (Planning-to-Execution handoff)

1. Create backup branch/tag: `master-before-upgrade-net10`.
2. Validate .NET 10 SDK installation on upgrade machine(s).
3. Perform a spike to evaluate compatibility of `Community.VisualStudio.Toolkit.17` and `Community.VisualStudio.VSCT`.
   - If replacements exist, include their package versions in the package update matrix.
   - If not, decide on hybrid approach or postpone migration.
4. If spike is successful, perform atomic upgrade:
   - Convert project to SDK-style and set `TargetFramework` to `net10.0`.
   - Update `PackageReference` entries per §Package Update Reference.
   - Restore, build, fix compilation issues.
5. Run tests and validate packaging.
6. Merge changes to `master` after review and validation.


---

## Appendix: Package Update Reference

- `Community.VisualStudio.Toolkit.17` — Current: `17.0.533` — Suggested: none (no supported version found). Projects affected: `SolutionCleanup`. Action: investigate replacement or keep VS-specific code on `net48`.
- `Community.VisualStudio.VSCT` — Current: `16.0.29.6` — Suggested: none (no supported version found). Action: investigate replacement.
- `Microsoft.VSSDK.BuildTools` — Current: `17.14.2101` — Suggested: `15.7.104` (per assessment). Action: evaluate appropriate tooling version for VS packaging.
- `MessagePack` — Current: `3.1.4` — Suggested: `3.1.4` (compatible).


---

Notes: This plan is based on the `assessment.md` analysis. It assumes user requested the upgrade to `net10.0` and requested the `master` branch as the upgrade target. The plan flags multiple incompatibilities that may block a full migration. If you want me to adjust the plan to use `net9.0` (STS) or `net8.0` (LTS) instead, or to create a separate upgrade branch instead of performing changes on `master`, tell me and I will update the plan accordingly.
