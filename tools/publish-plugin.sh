#!/usr/bin/env bash
# Cut a Dalamud drop and attach it to the GitHub `dev` release.
# Testers add: https://raw.githubusercontent.com/Pearlgate-XIV/Linkpearl/master/linkpearl.json
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
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

zip_url="https://github.com/Pearlgate-XIV/Linkpearl/releases/download/dev/Linkpearl.zip"
now="$(date +%s)"
python3 - "$root/linkpearl.json" "$version" "$zip_url" "$now" <<'PY'
import json, sys
from pathlib import Path

repo, version, zip_url, now = sys.argv[1:]
listing = [
    {
        "Author": "Pearlgate",
        "Name": "Linkpearl",
        "Description": "An in-game communicator for FINAL FANTASY XIV: a docked, always-on handset with a home screen, notifications, and themeable wallpapers.",
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
Path(repo).write_text(json.dumps(listing, indent=2) + "\n")
PY

gh release upload dev "$zip_path" --clobber
echo "version $version"
echo "repo https://raw.githubusercontent.com/Pearlgate-XIV/Linkpearl/master/linkpearl.json"
