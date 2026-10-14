#!/usr/bin/env bash
# Build the smoke app for the iOS simulator, run it, and fail unless it reports
# "KSMOKE RESULT PASS". macOS + Xcode + the .NET ios workload required.
#
#   run-ios-sim.sh          Mono (the default iOS runtime)
#   run-ios-sim.sh --aot    NativeAOT, the closest the simulator gets to a device's full AOT
set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
bundle=com.kernsmith.platformsmoke
arch=$(uname -m | sed 's/x86_64/x64/')
rid="iossimulator-$arch"

# NativeAOT only applies on publish; the .app still lands in the normal bin folder.
if [ "${1:-}" = "--aot" ]; then
  dotnet publish "$here" -c Release -f net10.0-ios -p:SmokeTarget=net10.0-ios -p:RuntimeIdentifier="$rid" -p:SmokeAot=true
else
  dotnet build "$here" -c Release -f net10.0-ios -p:SmokeTarget=net10.0-ios -p:RuntimeIdentifier="$rid"
fi
app=$(find "$here/bin/Release/net10.0-ios/$rid" -maxdepth 1 -name '*.app' | head -1)
[ -n "$app" ] || { echo "KSMOKE FAIL no .app produced"; exit 1; }

# Use a booted simulator if there is one, else boot the first available iPhone.
udid=$(xcrun simctl list devices booted -j | python3 -c '
import json,sys
d=json.load(sys.stdin)["devices"]
print(next((x["udid"] for v in d.values() for x in v), ""))')
if [ -z "$udid" ]; then
  udid=$(xcrun simctl list devices available -j | python3 -c '
import json,sys
d=json.load(sys.stdin)["devices"]
print(next(x["udid"] for k,v in d.items() if "iOS" in k for x in v if x["name"].startswith("iPhone")))')
  xcrun simctl boot "$udid"
fi
xcrun simctl bootstatus "$udid" -b >/dev/null

xcrun simctl uninstall "$udid" "$bundle" >/dev/null 2>&1 || true
xcrun simctl install "$udid" "$app"

# KSMOKE_EXIT makes the app quit once it has reported; SIMCTL_CHILD_ passes it through.
log=$(mktemp)
SIMCTL_CHILD_KSMOKE_EXIT=1 xcrun simctl launch --console-pty --terminate-running-process "$udid" "$bundle" \
  > "$log" 2>&1 &
pid=$!
for _ in $(seq 1 150); do
  if grep -q "KSMOKE RESULT" "$log"; then break; fi
  if ! kill -0 "$pid" 2>/dev/null; then break; fi
  sleep 2
done
kill "$pid" 2>/dev/null || true
cat "$log"
grep -q "KSMOKE RESULT PASS" "$log"
