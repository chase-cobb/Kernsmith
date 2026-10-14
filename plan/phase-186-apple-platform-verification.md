# Phase 186 — Apple Platform Verification (iOS, Mac Catalyst, macOS)

> **Status**: Not started — needs a Mac (Vic)
> **Branch**: `feature/mobile-platform-smoke`
> **Related**: [README platform support table](../README.md#platform-support), [platform smoke test](../tests/KernSmith.PlatformSmoke/README.md)

## Why

HeckYeah_Chase asked whether KernSmith works on iOS. The README now has a platform × rasterizer table, but every iOS and Mac Catalyst cell is ⏳ (expected, not verified). Android was verified on Windows with the emulator. Apple platforms cannot be: the iOS Simulator and Xcode only run on macOS, and a Windows machine cannot build a runnable iOS app.

This phase turns the Apple ⏳ cells into ✅ or ❌, with evidence.

## What is being tested

`tests/KernSmith.PlatformSmoke` is a small app. On launch it generates a bitmap font from an embedded Roboto with three backends (StbTrueType, FreeType, Native), checks each result, and prints one line per check:

```
KSMOKE PASS FreeType outline -- chars=95 kerning=143 pages=1 inkedPixels=21324
KSMOKE SKIP Native sdf -- backend has no SDF
KSMOKE RESULT PASS (0 failed)
```

The output appears on the app's screen and on stdout. `SKIP` means the backend doesn't have that feature, which is not a failure. The last line is the verdict.

## Expected results

| Target | StbTrueType | FreeType | Native |
|---|:-:|:-:|:-:|
| macOS desktop | pass | pass | pass |
| iOS simulator | pass | **unknown** | pass |
| iOS device | pass | **unknown** | pass |
| Mac Catalyst | pass | **fail** (expected) | pass |

- **FreeType on iOS is the real question.** FreeTypeSharp ships FreeType for iOS as a static library. Our own extra bindings in `src/KernSmith.Rasterizers.FreeType/FreeTypeNative.cs` use `DllImport("freetype")`. On iOS, statically linked code is normally reached through `"__Internal"`, so the outline, variable-font and color-font calls may fail even if basic FreeType works. Whatever happens, report the exact exception.
- **Mac Catalyst FreeType is expected to fail.** FreeTypeSharp has no Mac Catalyst native library. Confirming that is still useful.

## Setup (once)

1. Install Xcode from the App Store and launch it once so it installs its components. Use the Xcode version the .NET iOS workload requires; if it's wrong, the build error names the version it wants.
2. Install the .NET 10 SDK and the workloads:
   ```bash
   dotnet workload install ios maccatalyst
   ```
3. Check out the branch:
   ```bash
   git clone https://github.com/kaltinril/Kernsmith.git && cd Kernsmith
   git checkout feature/mobile-platform-smoke
   ```

Always build **Release**. That is where iOS AOT-compiles and trims, which is what this phase exists to exercise.

## Steps

Run in order and stop to report if anything is surprising.

### 1. macOS desktop (sanity check, ~1 min)

```bash
dotnet run --project tests/KernSmith.PlatformSmoke -c Release -f net10.0 -p:SmokeTarget=net10.0
```

### 2. iOS simulator, default runtime

```bash
bash tests/KernSmith.PlatformSmoke/run-ios-sim.sh
```

It builds, boots an iPhone simulator (or uses one that's already booted), installs, launches, and prints the `KSMOKE` lines. The exit code is 0 on pass.

### 3. iOS simulator, NativeAOT

```bash
bash tests/KernSmith.PlatformSmoke/run-ios-sim.sh --aot
```

This is closer to how a real device compiles the app.

### 4. Mac Catalyst

```bash
bash tests/KernSmith.PlatformSmoke/run-maccatalyst.sh
```

Expect FreeType to fail here; that is the confirmation we want. The script exits non-zero because of it.

### 5. Real iPhone (the one that matters most)

The simulator doesn't reproduce a device's full ahead-of-time compilation, so this step is the final word for iOS.

1. On the iPhone, turn on Developer Mode (Settings → Privacy & Security → Developer Mode) and connect it by cable.
2. You need a signing identity. A free Apple ID works: add it in Xcode → Settings → Accounts. The simplest way to get a provisioning profile is to create an empty iOS app in Xcode with the same bundle ID and run it on the phone once.
3. Build and deploy:
   ```bash
   dotnet build tests/KernSmith.PlatformSmoke -c Release -f net10.0-ios -p:SmokeTarget=net10.0-ios \
     -p:RuntimeIdentifier=ios-arm64 \
     -p:CodesignKey="Apple Development: Your Name (TEAMID)" \
     -t:Run
   ```
   If `com.kernsmith.platformsmoke` is already taken for your team, add `-p:ApplicationId=com.<you>.kssmoke` and use the same ID in step 2.
4. The results appear on the app's screen. A screenshot is enough.

## What to report back

For each step:
- a screenshot or pasted `KSMOKE` lines, including the first `INFO` line (it records OS, architecture and runtime)
- the full exception text for any `FAIL`
- the Xcode, macOS, iOS and .NET SDK versions (`dotnet --version`, `xcodebuild -version`)

Build failures that happen before the app runs are just as useful: paste the first error.

## After the results

- Kaltinril updates the README table, `COMPARISON.md`, the backend READMEs and `project_facts.md` from ⏳ to ✅/❌.
- If FreeType fails on iOS, the likely fix is resolving our `"freetype"` imports to the main program on Apple platforms. It gets a failing smoke test first, then the fix, then a re-run here.
- If FreeType on Mac Catalyst fails as expected, the smoke test marks it as an expected failure there so the CI job goes green.
- The `ios-simulator` and `maccatalyst` jobs in `.github/workflows/mobile-smoke.yml` cover steps 2–4 automatically on every PR. The real-device step stays manual.

## Done when

- Steps 1–5 have been run and reported.
- The README platform table has no ⏳ left in the iOS or Mac Catalyst rows.
