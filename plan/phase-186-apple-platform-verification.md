# Phase 186 — Apple Platform Verification (iOS, Mac Catalyst, macOS)

> **Status**: Mac Catalyst and iOS simulator verified by CI; real iPhone needs a Mac (Vic)
> **Related**: [README platform support table](../README.md#platform-support), [platform smoke test](../tests/KernSmith.PlatformSmoke/README.md)

## Why

HeckYeah_Chase asked whether KernSmith works on iOS. The README platform × rasterizer table now shows iOS StbTrueType and Native as ✅: the `ios-simulator` CI job (`macos-26`, iOS 26.4 simulator, iossimulator-arm64, Release, no JIT) passes both. The simulator does not reproduce a device's full ahead-of-time compilation, and the iOS Simulator and Xcode only run on macOS, so a Windows machine cannot do the device check.

This phase's remaining purpose is the real-iPhone confirmation (full device AOT) and the FreeType-on-iOS libpng follow-up.

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
| iOS simulator | **pass** (CI) | not built | **pass** (CI) |
| iOS device | pass | not built | pass |
| Mac Catalyst | **pass** (CI) | **fail** (CI) | **pass** (CI) |

- **FreeType is left out of the iOS build.** An iOS app that references the FreeType backend fails to link: FreeTypeSharp 3.1.0's static iOS `libfreetype.a` references libpng symbols that nothing provides. The smoke app drops FreeType from its iOS build automatically, so iOS runs StbTrueType and Native only.
- **Mac Catalyst is done.** CI ran it in Release with full AOT: StbTrueType and Native pass; FreeType fails because FreeTypeSharp has no Mac Catalyst native library.

## Setup (once)

1. Install Xcode from the App Store and launch it once so it installs its components. Use the Xcode version the .NET iOS workload requires; if it's wrong, the build error names the version it wants.
2. Install the .NET 10 SDK and the workloads:
   ```bash
   dotnet workload install ios maccatalyst
   ```
3. Clone the repo (the smoke app is on `main`):
   ```bash
   git clone https://github.com/kaltinril/Kernsmith.git && cd Kernsmith
   ```

Always build **Release**. That is where iOS AOT-compiles and trims, which is what this phase exists to exercise.

## Steps

Run in order and stop to report if anything is surprising.

### 1. macOS desktop (sanity check, ~1 min)

```bash
dotnet run --project tests/KernSmith.PlatformSmoke -c Release -f net10.0 -p:SmokeTarget=net10.0
```

### 2. iOS simulator (optional — CI runs this)

```bash
bash tests/KernSmith.PlatformSmoke/run-ios-sim.sh
```

It builds, boots an iPhone simulator (or uses one that's already booted), installs, launches, and prints the `KSMOKE` lines. The exit code is 0 on pass.

### 3. Mac Catalyst (optional — CI runs this)

```bash
KSMOKE_EXPECT_FAIL=FreeType bash tests/KernSmith.PlatformSmoke/run-maccatalyst.sh
```

FreeType reports `XFAIL`; StbTrueType and Native should pass.

### 4. Real iPhone (the one that matters)

Steps 2 and 3 are automated in CI, so this is the essential step. The simulator doesn't reproduce a device's full ahead-of-time compilation, so a real device is the final word for iOS. It tests StbTrueType and Native; FreeType is excluded from the iOS build automatically.

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

- Kaltinril reflects the real-iPhone result in the README table footnote (and the iOS row, `COMPARISON.md`, the backend READMEs and `project_facts.md` if it fails).
- Follow-up: investigate FreeType on iOS (the libpng link failure) — either supply a libpng for iOS or file an upstream FreeTypeSharp issue asking for an iOS build without PNG support or with libpng bundled.
- The `ios-simulator` and `maccatalyst` jobs in `.github/workflows/mobile-smoke.yml` cover steps 2–3 automatically on every PR. The real-device step stays manual.

## Done when

- Step 4 (real iPhone) has been run and reported.
- Its result is reflected in the README platform table's iOS footnote.
