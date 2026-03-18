#!/usr/bin/env bash

set -euo pipefail

repo_root="$(cd "$(dirname "$0")" && pwd)"
project_file="$repo_root/src/Jellyfin.Plugin.Oscars/Jellyfin.Plugin.Oscars.csproj"
artifacts_dir="$repo_root/artifacts"
publish_dir="$artifacts_dir/plugin"
release_dir="$artifacts_dir/release"

version="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$project_file" | head -n 1)"

if [[ -z "$version" ]]; then
  echo "Unable to determine plugin version from $project_file" >&2
  exit 1
fi

zip_name="jellyfin-oscars-v${version}.zip"
zip_path="$release_dir/$zip_name"
checksum_path="$zip_path.sha256"

rm -rf "$publish_dir" "$release_dir"
mkdir -p "$publish_dir" "$release_dir"

dotnet publish "$project_file" -c Release -o "$publish_dir"

(
  cd "$publish_dir"
  zip -r "$zip_path" . >/dev/null
)

shasum -a 256 "$zip_path" > "$checksum_path"

echo "Publish directory: $publish_dir"
echo "Release zip: $zip_path"
echo "SHA256: $checksum_path"
