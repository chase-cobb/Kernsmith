#!/usr/bin/env bash
# Build the smoke app for Mac Catalyst, run it, and fail unless it reports
# "KSMOKE RESULT PASS". macOS + Xcode + the .NET maccatalyst workload required.
set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
arch=$(uname -m | sed 's/x86_64/x64/')
rid="maccatalyst-$arch"

dotnet build "$here" -c Release -f net10.0-maccatalyst -p:SmokeTarget=net10.0-maccatalyst -p:RuntimeIdentifier="$rid"
app=$(find "$here/bin/Release/net10.0-maccatalyst/$rid" -maxdepth 1 -name '*.app' | head -1)
[ -n "$app" ] || { echo "KSMOKE FAIL no .app produced"; exit 1; }

log=$(mktemp)
KSMOKE_EXIT=1 "$app/Contents/MacOS/KernSmith.PlatformSmoke" > "$log" 2>&1 &
pid=$!
for _ in $(seq 1 90); do
  if grep -q "KSMOKE RESULT" "$log"; then break; fi
  if ! kill -0 "$pid" 2>/dev/null; then break; fi
  sleep 2
done
kill "$pid" 2>/dev/null || true
cat "$log"
grep -q "KSMOKE RESULT PASS" "$log"
