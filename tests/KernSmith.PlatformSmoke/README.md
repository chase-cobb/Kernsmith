# KernSmith.PlatformSmoke

A small app that proves KernSmith actually runs on a platform: it generates a bitmap font from embedded Roboto with the StbTrueType, FreeType, and Native backends (registered explicitly via `RasterizerFactory.Register`, as trimmed/AOT apps must) and checks outline stroke, SDF, and variable fonts where the backend supports them, plus that system-font lookup does not throw. It is the evidence behind the [platform support table](../../README.md#platform-support), and CI runs it per platform in `.github/workflows/mobile-smoke.yml`.

It is not in `KernSmith.sln` because the mobile targets need the android/ios/maccatalyst workloads. Targets: `net10.0` and `net10.0-android` everywhere, plus `net10.0-ios` and `net10.0-maccatalyst` when built on macOS. Pass `-p:SmokeTarget=<tfm>` to restore and build only one target, so you only need that one workload.

**Always build Release.** That is where Android trims and iOS AOT-compiles; Debug hides exactly the failures this app exists to catch.

## Output

One line per check, then a final verdict:

```
KSMOKE INFO os=... rid=android-arm64 arch=Arm64 framework=.NET 10.0.x dynamicCode=False
KSMOKE PASS StbTrueType generate -- ...
KSMOKE SKIP Native sdf -- backend has no SDF
KSMOKE FAIL FreeType create -- DllNotFoundException: ...
KSMOKE RESULT FAIL
```

`SKIP` means the backend does not claim the capability; only `FAIL` fails the run. Look for `KSMOKE RESULT PASS`.

## Running

**Desktop** (Windows, Linux, macOS):

```bash
dotnet run --project tests/KernSmith.PlatformSmoke -c Release -f net10.0 -p:SmokeTarget=net10.0
```

**Android** (Windows, Linux, or macOS with the Android SDK and one emulator or device attached):

```bash
bash tests/KernSmith.PlatformSmoke/run-android.sh
```

Builds Release, installs, launches, waits for `KSMOKE RESULT` in logcat (tag `KSMOKE`), and checks every packaged `libfreetype.so` is 16KB page-aligned. Needs the Android SDK platform API 36; install it with `dotnet build tests/KernSmith.PlatformSmoke -t:InstallAndroidDependencies -f net10.0-android -p:SmokeTarget=net10.0-android -p:AndroidSdkDirectory=<sdk path>`. Set `ANDROID_HOME` if the SDK is not auto-detected.

**iOS simulator** (macOS + Xcode):

```bash
bash tests/KernSmith.PlatformSmoke/run-ios-sim.sh        # Mono runtime
bash tests/KernSmith.PlatformSmoke/run-ios-sim.sh --aot  # NativeAOT
```

**Mac Catalyst** (macOS + Xcode):

```bash
bash tests/KernSmith.PlatformSmoke/run-maccatalyst.sh
```

**iOS device** (macOS + Xcode, a connected iPhone, and an Apple developer account for signing):

```bash
dotnet build tests/KernSmith.PlatformSmoke -c Release -f net10.0-ios -p:SmokeTarget=net10.0-ios \
  -p:RuntimeIdentifier=ios-arm64 -p:CodesignKey="Apple Development: Your Name (TEAMID)" -t:Run
```

The device needs a provisioning profile for `com.kernsmith.platformsmoke`; pass `-p:CodesignProvision="<profile name>"` if more than one matches, or let Xcode automatic signing create one. Results show on the app's screen and in the device console (Xcode > Devices and Simulators > Open Console, filter on `KSMOKE`).
