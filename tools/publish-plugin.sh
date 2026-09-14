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
dotnet build "$root/src/Linkpearl.Host" -c Debug -p:EnableWindowsTargeting=true -p:Version="$version"

out=""
for candidate in \
  "$root/src/Linkpearl.Host/bin/Debug/net10.0-windows" \
  "$root/src/Linkpearl.Host/bin/Debug"
do
  if [[ -f "$candidate/LinkpearlDev.dll" ]]; then
    out="$candidate"
    break
  fi
done
if [[ -z "$out" ]]; then
  echo "LinkpearlDev.dll not found under bin/Debug" >&2
  exit 1
fi

stage="$(mktemp -d)"
trap 'rm -rf "$stage"' EXIT
zip_path="$stage/LinkpearlDev.zip"

python3 - "$out" "$zip_path" "$version" <<'PY'
import json, sys, zipfile
from pathlib import Path

out = Path(sys.argv[1])
zip_path = Path(sys.argv[2])
version = sys.argv[3]
for name in ("LinkpearlDev.json", "Linkpearl.json"):
    manifest = out / name
    if not manifest.exists():
        continue
    data = json.loads(manifest.read_text())
    data["Author"] = "Pearlgate"
    data["AssemblyVersion"] = version
    data["InternalName"] = "LinkpearlDev"
    data["Name"] = "Linkpearl"
    manifest.write_text(json.dumps(data, indent=2) + "\n")

skip_suffixes = {".pdb", ".xml"}
skip_names = {
    "libmp3lame.dll",
    "libmp3lame.dylib",
    "libmp3lame.so",
    "EchoMix.AudioHost",
}
with zipfile.ZipFile(zip_path, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as z:
    for path in sorted(out.rglob("*")):
        if not path.is_file():
            continue
        if path.suffix.lower() in skip_suffixes or path.name in skip_names:
            continue
        z.write(path, path.relative_to(out).as_posix())
print("zip", zip_path, zip_path.stat().st_size)
PY

# GitHub release downloads of a 30+ MB zip time out in Dalamud's installer
# (looks like a hang, then "A task was cancelled"). Serve the zip from Pearlgate.
host="${PLUGIN_SSH_HOST:-root@194.113.211.29}"
remote="${PLUGIN_REMOTE:-/opt/pearlgate/plugin-public}"
base_url="${PLUGIN_BASE_URL:-https://pearlgate.194.113.211.29.sslip.io}"
zip_url="$base_url/plugin/LinkpearlDev.zip"
scp -q "$zip_path" "$host:$remote/LinkpearlDev.zip"
ssh -o BatchMode=yes "$host" "chmod 644 '$remote/LinkpearlDev.zip'"
now="$(date +%s)"
python3 - "$root/linkpearl.json" "$version" "$zip_url" "$now" <<'PY'
import json, sys
from pathlib import Path

repo, version, zip_url, now = sys.argv[1:]
listing = [
    {
        "Author": "Pearlgate",
        "Name": "Linkpearl",
        "Punchline": "A pearl in your pocket.",
        "Description": "An in-game communicator for FINAL FANTASY XIV: a docked, always-on handset with a home screen, notifications, and themeable wallpapers.",
        "InternalName": "LinkpearlDev",
        "AssemblyVersion": version,
        "RepoUrl": "https://github.com/Pearlgate-XIV/Linkpearl",
        "ApplicableVersion": "any",
        "Tags": ["phone", "chat", "social"],
        "DalamudApiLevel": 15,
        "LoadPriority": 0,
        "DownloadLinkInstall": zip_url,
        "IconUrl": "https://raw.githubusercontent.com/Pearlgate-XIV/Linkpearl/master/src/Linkpearl.Host/Icons/glyphs/phone.png",
        "IsHide": False,
        "IsTestingExclusive": False,
        "DownloadLinkTesting": zip_url,
        "DownloadLinkUpdate": zip_url,
        "DownloadCount": 0,
        "LastUpdate": str(now),
    }
]
text = json.dumps(listing, indent=2) + "\n"
Path(repo).write_text(text)
Path(repo).with_name("pluginmaster.json").write_text(text)
PY

# Official Dalamud already owns InternalName "Linkpearl". Never leave that zip/listing on the VPS or GitHub.
# Keep GitHub pluginmaster.json too: testers who first installed from that URL only get
# Dalamud updates while InstalledFromUrl still matches a live repo.
scp -q "$root/linkpearl.json" "$host:$remote/pluginmaster.json"
ssh -o BatchMode=yes "$host" "rm -f '$remote/Linkpearl.zip'; chmod 644 '$remote/pluginmaster.json'"

gh release upload dev "$zip_path" --clobber
gh release delete-asset dev Linkpearl.zip --yes 2>/dev/null || true
cp "$root/linkpearl.json" "$stage/pluginmaster.json"
gh release upload dev "$root/linkpearl.json" "$stage/pluginmaster.json" --clobber
gh release edit dev --notes "Current Linkpearl. Add https://raw.githubusercontent.com/Pearlgate-XIV/Linkpearl/master/linkpearl.json as a Dalamud custom repository."
echo "version $version"
echo "repo https://raw.githubusercontent.com/Pearlgate-XIV/Linkpearl/master/linkpearl.json"
git -C "$root" push origin HEAD:master
