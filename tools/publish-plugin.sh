#!/usr/bin/env bash
# Cut a public Dalamud drop testers can Update against.
# Custom repo: https://pearlgate.194.113.211.29.sslip.io/plugin/pluginmaster.json
# Same listing is committed to linkpearl.json for GitHub raw.
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
host="${PLUGIN_SSH_HOST:-root@194.113.211.29}"
base_url="${PLUGIN_BASE_URL:-https://pearlgate.194.113.211.29.sslip.io}"
remote_root="/opt/pearlgate/plugin-public"
rev_file="$root/tools/.plugin-revision"
dalamud_home="${DALAMUD_HOME:-$HOME/.xlcore/dalamud/Hooks/dev}"

if [[ ! -d "$dalamud_home" ]]; then
  echo "DALAMUD_HOME is not a directory: $dalamud_home" >&2
  exit 1
fi

rev=1
if [[ -f "$rev_file" ]]; then
  rev="$(tr -d '[:space:]' < "$rev_file")"
fi
version="0.1.1.${rev}"
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
    data["Author"] = "Pearlgate"
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

zip_url="$base_url/plugin/Linkpearl.zip"
json_url="$base_url/plugin/pluginmaster.json"
now="$(date +%s)"
python3 - "$stage/pluginmaster.json" "$root/linkpearl.json" "$version" "$zip_url" "$now" <<'PY'
import json, sys
from pathlib import Path

dest, repo, version, zip_url, now = sys.argv[1:]
listing = [
    {
        "Author": "Pearlgate",
        "Name": "Linkpearl",
        "Description": "Pearlgate phone. Sign in with XIVAuth. Disable any other plugin named Linkpearl first.",
        "Punchline": "A pearl in your pocket.",
        "InternalName": "Linkpearl",
        "AssemblyVersion": version,
        "RepoUrl": "https://github.com/Pearlgate-XIV/Linkpearl",
        "ApplicableVersion": "any",
        "DalamudApiLevel": 15,
        "IsHide": False,
        "IsTestingExclusive": False,
        "DownloadLinkInstall": zip_url,
        "DownloadLinkUpdate": zip_url,
        "DownloadLinkTesting": zip_url,
        "LastUpdate": now,
    }
]
text = json.dumps(listing, indent=2) + "\n"
Path(dest).write_text(text)
Path(repo).write_text(text)
PY

ssh -o BatchMode=yes "$host" "mkdir -p '$remote_root'; chmod 755 '$remote_root'"
scp -q "$stage/pluginmaster.json" "$stage/Linkpearl.zip" "$host:$remote_root/"
ssh -o BatchMode=yes "$host" "chmod 644 '$remote_root/pluginmaster.json' '$remote_root/Linkpearl.zip'"

echo "version $version"
echo "repo $json_url"
echo "github https://raw.githubusercontent.com/Pearlgate-XIV/Linkpearl/main/linkpearl.json"
