#!/usr/bin/env bash
# Cut a private Dalamud tester drop and scp it onto Pearlgate.
# Testers add: https://api.pearlgate.alphachannel.duckdns.org/plugin-beta/<token>/pluginmaster.json
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
host="${BETA_SSH_HOST:-root@194.113.211.29}"
base_url="${BETA_BASE_URL:-https://api.pearlgate.alphachannel.duckdns.org}"
remote_root="/opt/pearlgate/plugin-beta"
token_file="$root/tools/.beta-token"
rev_file="$root/tools/.beta-revision"
url_file="$root/tools/.beta-url"
dalamud_home="${DALAMUD_HOME:-$HOME/.xlcore/dalamud/Hooks/dev}"

if [[ ! -d "$dalamud_home" ]]; then
  echo "DALAMUD_HOME is not a directory: $dalamud_home" >&2
  exit 1
fi

if [[ ! -f "$token_file" ]]; then
  openssl rand -hex 24 > "$token_file"
  chmod 600 "$token_file"
fi
token="$(tr -d '[:space:]' < "$token_file")"
if [[ ${#token} -lt 24 ]]; then
  echo "tools/.beta-token is too short" >&2
  exit 1
fi

rev=1
if [[ -f "$rev_file" ]]; then
  rev="$(tr -d '[:space:]' < "$rev_file")"
fi
version="0.1.0.${rev}"
echo "$((rev + 1))" > "$rev_file"

export DALAMUD_HOME="$dalamud_home"
dotnet build "$root/src/Linkpearl.Host" -c Release -p:EnableWindowsTargeting=true -p:Version="$version"

out=""
for candidate in \
  "$root/src/Linkpearl.Host/bin/Release/net10.0-windows" \
  "$root/src/Linkpearl.Host/bin/Release"
do
  if [[ -f "$candidate/Linkpearl.dll" ]]; then
    out="$candidate"
    break
  fi
done
if [[ -z "$out" ]]; then
  echo "Linkpearl.dll not found under bin/Release" >&2
  exit 1
fi

stage="$(mktemp -d)"
trap 'rm -rf "$stage"' EXIT
zip_path="$stage/Linkpearl.zip"

python3 - "$out" "$zip_path" "$version" <<'PY'
import json, sys, zipfile
from pathlib import Path

out = Path(sys.argv[1])
zip_path = Path(sys.argv[2])
version = sys.argv[3]
manifest = out / "Linkpearl.json"
if manifest.exists():
    data = json.loads(manifest.read_text())
    data["AssemblyVersion"] = version
    data["InternalName"] = "Linkpearl"
    data["Name"] = "Linkpearl"
    manifest.write_text(json.dumps(data, indent=2) + "\n")

skip_suffixes = {".pdb"}
skip_names = {"libmp3lame.dll", "libmp3lame.dylib", "libmp3lame.so"}
with zipfile.ZipFile(zip_path, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as z:
    for path in sorted(out.rglob("*")):
        if not path.is_file():
            continue
        if path.suffix.lower() in skip_suffixes or path.name in skip_names:
            continue
        z.write(path, path.relative_to(out).as_posix())
print("zip", zip_path, zip_path.stat().st_size)
PY

zip_url="$base_url/plugin-beta/${token}/Linkpearl.zip"
json_url="$base_url/plugin-beta/${token}/pluginmaster.json"
now="$(date +%s)"
cat > "$stage/pluginmaster.json" <<EOF
[
  {
    "Author": "Pearlgate",
    "Name": "Linkpearl",
    "Description": "Pearlgate beta. Sign in with XIVAuth. Disable any other plugin named Linkpearl first.",
    "Punchline": "A pearl in your pocket.",
    "InternalName": "Linkpearl",
    "AssemblyVersion": "$version",
    "RepoUrl": "https://github.com/Pearlgate-XIV/Linkpearl",
    "ApplicableVersion": "any",
    "DalamudApiLevel": 15,
    "IsHide": false,
    "IsTestingExclusive": false,
    "DownloadLinkInstall": "$zip_url",
    "DownloadLinkUpdate": "$zip_url",
    "DownloadLinkTesting": "$zip_url",
    "LastUpdate": "$now"
  }
]
EOF

ssh -o BatchMode=yes "$host" "umask 077; mkdir -p '$remote_root/$token'; chmod 755 '$remote_root' '$remote_root/$token'"
scp -q "$stage/pluginmaster.json" "$stage/Linkpearl.zip" "$host:$remote_root/$token/"
ssh -o BatchMode=yes "$host" "chmod 644 '$remote_root/$token/pluginmaster.json' '$remote_root/$token/Linkpearl.zip'"

printf '%s\n' "$json_url" > "$url_file"
chmod 600 "$url_file"
echo "version $version"
echo "repo $json_url"
