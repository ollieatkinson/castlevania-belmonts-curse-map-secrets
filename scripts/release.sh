#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/.."
if [[ $# -ne 1 ]]; then
  echo "Usage: GamePath='/path/to/game' bash scripts/release.sh v0.1.0" >&2
  exit 1
fi
version="${1#v}"
if [[ ! "$version" =~ ^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(-[0-9A-Za-z]+(\.[0-9A-Za-z]+)*)?$ ]]; then
  echo "Invalid version: $1 (expected vX.Y.Z or vX.Y.Z-preview.N)" >&2
  exit 1
fi
tag="v$version"

if [[ -n "$(git status --porcelain)" ]]; then
  echo 'Commit and push your changes before releasing.' >&2
  exit 1
fi
repository="$(gh repo view --json nameWithOwner --jq .nameWithOwner)"
commit="$(git rev-parse HEAD)"
if ! gh api "repos/$repository/commits/$commit" --silent; then
  echo 'Push the current commit to GitHub before releasing.' >&2
  exit 1
fi
if gh release view "$tag" --repo "$repository" >/dev/null 2>&1; then
  echo "Release $tag already exists." >&2
  exit 1
fi
if remote_commit="$(gh api "repos/$repository/commits/$tag" --jq .sha 2>/dev/null)"; then
  if [[ "$remote_commit" != "$commit" ]]; then
    echo "Tag $tag already points to a different commit." >&2
    exit 1
  fi
fi

bash scripts/package.sh "$version"

release_flags=()
if [[ "$version" == *-* ]]; then
  release_flags+=(--prerelease --latest=false)
fi
if [[ -f "releases/$tag.md" ]]; then
  release_flags+=(--notes-file "releases/$tag.md")
else
  release_flags+=(--generate-notes)
fi
gh release create "$tag" \
  "dist/MapSecrets-$tag.zip" "dist/MapSecrets-$tag.zip.sha256" \
  "dist/MapSecrets-$tag.standalone.zip" "dist/MapSecrets-$tag.standalone.zip.sha256" \
  --repo "$repository" --target "$commit" \
  --title "$tag — Map Secrets" \
  "${release_flags[@]}"
