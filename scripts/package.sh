#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/.."
: "${GamePath:?Set GamePath to the game directory after running BepInEx once.}"
dotnet_command="${DOTNET_COMMAND:-dotnet}"
if [[ -z "${DOTNET_COMMAND:-}" ]] && ! command -v dotnet >/dev/null 2>&1 && [[ -x .build/tools/dotnet/dotnet ]]; then
  dotnet_command="$PWD/.build/tools/dotnet/dotnet"
fi
version="${1:-}"
if [[ -z "$version" ]]; then
  version="$("$dotnet_command" msbuild MapAssist.csproj -getProperty:Version -nologo)"
fi
if [[ ! "$version" =~ ^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(-[0-9A-Za-z]+(\.[0-9A-Za-z]+)*)?$ ]]; then
  echo "Invalid version: $version (expected X.Y.Z or X.Y.Z-preview.N)" >&2
  exit 1
fi

"$dotnet_command" build MapAssist.csproj -c Release \
  -p:GamePath="$GamePath" -p:Version="$version"

python3 - "$version" <<'PY'
import hashlib
from pathlib import Path
import sys
import zipfile

version = sys.argv[1]
dist = Path("dist")
dist.mkdir(exist_ok=True)
archive = dist / f"MapSecrets-v{version}.zip"
with zipfile.ZipFile(archive, "w", zipfile.ZIP_DEFLATED) as package:
    package.write("bin/Release/net6.0/MapAssist.dll", "BepInEx/plugins/MapAssist.dll")
    package.write("packaging/INSTALL.txt", "INSTALL.txt")
    for document in ("README.md", "LICENSE"):
        package.write(document, document)
checksum = hashlib.sha256(archive.read_bytes()).hexdigest()
archive.with_suffix(".zip.sha256").write_text(f"{checksum}  {archive.name}\n")
print(f"Created {archive}")
PY

"$dotnet_command" msbuild MapAssist.csproj -t:PackageStandalone -p:Version="$version" -nologo

python3 packaging/validate_package.py "dist/MapSecrets-v$version.zip" MapAssist.dll
python3 packaging/validate_package.py "dist/MapSecrets-v$version.standalone.zip" MapAssist.dll --standalone
