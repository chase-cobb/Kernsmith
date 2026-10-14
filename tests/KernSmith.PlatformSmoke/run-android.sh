#!/usr/bin/env bash
# Build the smoke app in Release, run it on the connected Android device/emulator, and
# fail unless it reports "KSMOKE RESULT PASS". Also checks every packaged libfreetype.so
# is 16KB page-aligned (required by Android 15+ / Google Play).
#
# Needs: .NET 10 SDK + android workload, Android SDK with platform API 36, adb on PATH,
# and exactly one device/emulator attached. Set ANDROID_HOME if the SDK is not auto-detected.
set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
pkg=com.kernsmith.platformsmoke
sdk_arg=()
if [ -n "${ANDROID_HOME:-}" ]; then sdk_arg=("-p:AndroidSdkDirectory=$ANDROID_HOME"); fi

dotnet build "$here" -c Release -f net10.0-android -p:SmokeTarget=net10.0-android ${sdk_arg[@]+"${sdk_arg[@]}"}
apk="$here/bin/Release/net10.0-android/$pkg-Signed.apk"

"$(command -v python3 || command -v python)" - "$apk" <<'EOF'
import struct, sys, zipfile
bad = 0
with zipfile.ZipFile(sys.argv[1]) as z:
    for name in z.namelist():
        if not name.endswith("/libfreetype.so"):
            continue
        b = z.read(name)
        phoff = struct.unpack_from("<Q", b, 0x20)[0]
        phentsize, phnum = struct.unpack_from("<HH", b, 0x36)
        aligns = [struct.unpack_from("<Q", b, phoff + i * phentsize + 0x30)[0]
                  for i in range(phnum)
                  if struct.unpack_from("<I", b, phoff + i * phentsize)[0] == 1]
        ok = min(aligns) >= 16384
        bad += not ok
        print(f"KSMOKE {'PASS' if ok else 'FAIL'} 16KB alignment {name} -- LOAD align {sorted(set(aligns))}")
sys.exit(1 if bad else 0)
EOF

adb uninstall "$pkg" >/dev/null 2>&1 || true
adb install "$apk"
adb logcat -c
adb shell monkey -p "$pkg" -c android.intent.category.LAUNCHER 1 >/dev/null

for _ in $(seq 1 120); do
  if adb logcat -d -s KSMOKE:I | grep -q "KSMOKE RESULT"; then break; fi
  sleep 2
done

# logcat prefixes each line with "date time pid tid I KSMOKE  : "; strip it.
adb logcat -d -s KSMOKE:I | sed -n 's/^.*KSMOKE *: //p'
adb logcat -d -s KSMOKE:I | grep -q "KSMOKE RESULT PASS"
