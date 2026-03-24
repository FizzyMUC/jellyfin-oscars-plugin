#!/usr/bin/env bash

set -euo pipefail

# Public repo is the distribution/release repo. Artifacts stay here.
repo_root="$(cd "$(dirname "$0")" && pwd)"
# Private repo is the source of truth for the plugin code and version.
private_repo_root="/Users/pascalmarter/dev/jellyfin-oscars"
project_file="$private_repo_root/src/Jellyfin.Plugin.Oscars/Jellyfin.Plugin.Oscars.csproj"
artifacts_dir="$repo_root/artifacts"
publish_dir="$artifacts_dir/plugin"
release_dir="$artifacts_dir/release"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "dotnet is required but was not found in PATH" >&2
  exit 1
fi

if ! command -v shasum >/dev/null 2>&1; then
  echo "shasum is required but was not found in PATH" >&2
  exit 1
fi

if [[ ! -d "$private_repo_root" ]]; then
  echo "Private source repo not found: $private_repo_root" >&2
  exit 1
fi

if [[ ! -f "$project_file" ]]; then
  echo "Private source project file not found: $project_file" >&2
  exit 1
fi

version="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$project_file" | head -n 1)"

if [[ -z "$version" ]]; then
  echo "Unable to determine plugin version from $project_file" >&2
  exit 1
fi

release_version="${version}"
if [[ "$release_version" =~ ^([0-9]+\.[0-9]+\.[0-9]+)\.0$ ]]; then
  release_version="${BASH_REMATCH[1]}"
fi

zip_name="jellyfin-oscars-v${release_version}.zip"
zip_path="$release_dir/$zip_name"
checksum_path="$zip_path.sha256"

rm -rf "$publish_dir" "$release_dir"
mkdir -p "$publish_dir" "$release_dir"

dotnet publish "$project_file" -c Release -o "$publish_dir"

(
  cd "$publish_dir"
  zip -r "$zip_path" . >/dev/null
)

checksum_value="$(shasum -a 256 "$zip_path" | awk '{print $1}')"

printf '%s\n' "$checksum_value" > "$checksum_path"

echo "Public repo: $repo_root"
echo "Private repo: $private_repo_root"
echo "Publish directory: $publish_dir"
echo "Release zip: $zip_path"
echo "SHA256: $checksum_path"
