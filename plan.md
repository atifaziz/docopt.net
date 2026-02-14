## Plan: Multi-Roslyn Configuration Support via Build Script

**TL;DR**: Add a `RoslynVersion` MSBuild property to [src/DocoptNet/DocoptNet.csproj](src/DocoptNet/DocoptNet.csproj) that conditionally switches the `Microsoft.CodeAnalysis.CSharp` reference (3.10.0 baseline → 4.4.0) and defines `ROSLYN4_4`. The default build remains unchanged. A new PowerShell script (`build.ps1`) with `Build`, `Test`, and `Pack` parameter sets orchestrates multi-Roslyn builds. The NuGet package ships both analyzer DLLs: unversioned (baseline) and `roslyn4.4/` (new). A guard target in the `.csproj` prevents packing without the Roslyn 4.4 output.

### Assumptions

- **Roslyn 4.4.0** is the correct target for C# 11 raw/interpolated string literal support (ships with .NET 7 SDK / VS 17.4).
- The `roslyn{version}` analyzer path convention (`analyzers/dotnet/roslyn4.4/cs/`) is supported by .NET SDK 6.0.4+. Users on older SDKs automatically fall back to the unversioned `analyzers/dotnet/cs/` path.
- Only the `netstandard2.0` TFM needs to be built with Roslyn 4.4 (the `analyzers/` DLL). The `lib/` TFMs (`netstandard2.0`, `netstandard2.1`, `net47`) are unaffected.
- The `RoslynVersion` property is **not** passed by default — a bare `dotnet build` produces the baseline. Only the script (or explicit `-p:RoslynVersion=4.4`) triggers the variant build.
- The test project [tests/DocoptNet.Tests/DocoptNet.Tests.csproj](tests/DocoptNet.Tests/DocoptNet.Tests.csproj) will also accept `RoslynVersion` passthrough (since it project-references `DocoptNet.csproj`), enabling Roslyn-4.4-specific test coverage.
- The existing [tests/Integration/run.ps1](tests/Integration/run.ps1) and its wrappers remain unchanged — they already invoke `dotnet pack` and `dotnet test`, and will work with the packed NuGet that contains both analyzer variants.
- PowerShell script execution in CI/local commands can rely on the pinned local tool (`dotnet pwsh`) and therefore assumes `dotnet tool restore` has been run first.

---

**Steps**

### 1. Modify [src/DocoptNet/DocoptNet.csproj](src/DocoptNet/DocoptNet.csproj)

**a) Redirect output paths when `RoslynVersion=4.4`**

Add a `PropertyGroup` conditioned on `$(RoslynVersion)` that overrides `BaseOutputPath` and `BaseIntermediateOutputPath` to isolate the Roslyn 4.4 build artifacts from the default build. Also define `ROSLYN4_4` for conditional compilation.

**b) Split the Roslyn `PackageReference` into two conditions**

Replace the current unconditional `Microsoft.CodeAnalysis.CSharp` 3.10.0 reference with:
- 3.10.0 when `$(RoslynVersion)` is empty or unset (baseline)
- 4.4.0 when `$(RoslynVersion)` is `4.4`

Both retain the existing `Condition="'$(TargetFramework)' != 'net47'"` guard.

**c) Add the Roslyn 4.4 analyzer to pack items**

Add a second `<None Pack="true" PackagePath="analyzers/dotnet/roslyn4.4/cs" />` item pointing to the Roslyn 4.4 build output. Use project-relative MSBuild properties (`$(MSBuildProjectDirectory)`, `$(BaseOutputPath)`, etc.) for robust path construction instead of hard-coded Windows separators.

**d) Add a guard target `_ValidateRoslyn44AnalyzerOutput`**

Runs `BeforeTargets="GenerateNuspec"`. Emits an `<Error>` if the Roslyn 4.4 DLL doesn't exist, with a message like:

> *Roslyn 4.4 analyzer output not found at '...'. Build all Roslyn variants first by running: ./build.ps1 -Pack*

This prevents `dotnet pack` from producing an incomplete NuGet package.

### 2. Create `build.ps1` at the repository root

A single PowerShell script with three parameter sets:

**`Build` (default)**
- Parameters: `-Configuration` (default `Release`)
- Actions:
  1. `dotnet build` the solution (all projects, all TFMs, baseline Roslyn 3.10)
  2. `dotnet build src/DocoptNet/DocoptNet.csproj -f netstandard2.0 -p:RoslynVersion=4.4` (Roslyn 4.4 variant, single TFM only)

**`Test`**
- Parameters: `-Configuration` (default `Release`), `-NoBuild` switch
- Actions:
  1. If not `-NoBuild`: invoke `Build` logic first
  2. `dotnet test --no-build` the solution (tests against baseline Roslyn)
  3. `dotnet test` the test project with `-p:RoslynVersion=4.4` (tests against Roslyn 4.4 — always built in this step because outputs differ)

`-NoBuild` behavior clarification: this switch applies to baseline solution tests only. The Roslyn 4.4 test pass still performs build work as needed for the alternate output path.

**`Pack`**
- Parameters: `-Configuration` (default `Release`), `-VersionSuffix` (optional)
- Actions:
  1. Invoke `Build` logic first
  2. `dotnet pack src/DocoptNet/DocoptNet.csproj --no-build` with optional `--version-suffix`

All parameter sets pass `-c $Configuration` through. The script uses `$ErrorActionPreference = 'Stop'` and checks `$LASTEXITCODE` after each `dotnet` invocation.

To make invocation directory-independent, the script should `Push-Location` to `$PSScriptRoot` at startup and `Pop-Location` in a `finally` block so cleanup always runs on success or failure.

### 3. Update [.github/workflows/ci.yml](.github/workflows/ci.yml)

**a) Replace the `Build` step**

Change from `dotnet build --configuration Release` to `dotnet pwsh ./build.ps1 -Configuration Release`.

**b) Replace the `Test` step**

Change from `dotnet test --no-build --configuration Release` to `dotnet pwsh ./build.ps1 -Test -NoBuild -Configuration Release`.

**c) Update the `Pack` step**

The existing pack step has version-suffix logic. Keep that logic but replace the `dotnet pack` invocation with `dotnet pwsh ./build.ps1 -Pack -Configuration Release -VersionSuffix $versionSuffix`. The guard target ensures the Roslyn 4.4 output is present.

**d) Consider Linux builds**

The CI matrix includes `ubuntu-22.04`. The build step there also needs to run the PowerShell script. Since this plan uses `dotnet pwsh`, ensure `dotnet tool restore` runs before Build/Test/Pack steps so the pinned PowerShell tool is available on both platforms.

### 4. Add `.gitignore` entry (if needed)

The redirected output path `src/DocoptNet/bin/roslyn4.4/` and `src/DocoptNet/obj/roslyn4.4/` should already be covered by the existing `bin/` and `obj/` ignore patterns. Verify this.

### 5. Documentation update (independent final step)

After all code/CI/verification work is complete, perform documentation updates as a separate, standalone step:

- Update [README.md](README.md) (or CONTRIBUTING if preferred) with `build.ps1` usage for `Build`, `Test`, and `Pack`.
- Explicitly state that plain `dotnet pack src/DocoptNet/DocoptNet.csproj` is expected to fail the guard unless Roslyn 4.4 artifacts have been built.
- Include cross-platform examples that use `dotnet pwsh ./build.ps1` and note the prerequisite `dotnet tool restore`.

---

**Verification**

1. `dotnet build` at the root still works and produces the baseline build (no regression)
2. `dotnet pack src/DocoptNet/DocoptNet.csproj` **fails** with the guard target error message pointing to `build.ps1`
3. `dotnet pwsh ./build.ps1` builds both Roslyn variants successfully
4. `dotnet pwsh ./build.ps1 -Test` runs tests for both variants
5. `dotnet pwsh ./build.ps1 -Pack` produces a `.nupkg` in `dist/` that contains:
   - `lib/netstandard2.0/DocoptNet.dll`
   - `lib/netstandard2.1/DocoptNet.dll`
   - `lib/net47/DocoptNet.dll`
   - `analyzers/dotnet/cs/DocoptNet.dll` (Roslyn 3.10 baseline)
   - `analyzers/dotnet/roslyn4.4/cs/DocoptNet.dll` (Roslyn 4.4)
   - `build/netstandard2.0/docopt.net.targets`
6. Inspect the package with `dotnet nuget verify` or extract and check folder structure
7. CI workflow passes on both Windows and Linux
8. Documentation is updated only after steps 1–7 are complete (as an independent final step)

**Decisions**

- **Configuration-driven, not multi-project**: avoids code duplication and shared project complexity; the tradeoff is that `dotnet build`/`dotnet pack` alone only handle the baseline
- **Guard target over silent omission**: using `Condition="Exists(...)"` on the pack item would silently skip the Roslyn 4.4 DLL; an explicit `<Error>` is safer and more informative
- **Script at repo root**: `build.ps1` matches convention alongside existing `mark-shipped.ps1`; cross-platform via PowerShell Core (already used in CI)
- **Only `netstandard2.0` for Roslyn 4.4 variant**: the other TFMs are for the runtime library only and don't include generator code, so building them again with Roslyn 4.4 is unnecessary
