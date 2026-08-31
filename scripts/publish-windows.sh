#!/usr/bin/env bash
# Publish self-contained win-x64 package (avoids needing shared .NET 10 runtime on server).
# IIS still needs the .NET 10 Hosting Bundle for AspNetCoreModuleV2.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
VERSION="$(sed -n 's/.*<Version>\([^<]*\)<\/Version>.*/\1/p' "$ROOT/Directory.Build.props" | head -n 1)"
PROJ="$ROOT/src/Icewireless.AccountServiceDashboard.Web/Icewireless.AccountServiceDashboard.Web.csproj"
OUT="$ROOT/artifacts/publish/win-x64"
ZIP_DIR="$ROOT/artifacts"
ZIP_NAME="Icewireless.AccountServiceDashboard-win-x64-${VERSION}.zip"
ZIP_ALIAS="Icewireless.AccountServiceDashboard-win-x64.zip"
DEPLOY="$ROOT/deploy/windows"

rm -rf "$OUT"
mkdir -p "$OUT" "$ZIP_DIR"

dotnet publish "$PROJ" \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -o "$OUT" \
  /p:PublishSingleFile=false

# Deploy helpers (overwrite publish defaults where we need stdout logging / templates)
cp "$DEPLOY/DEPLOY-WINDOWS.md" "$OUT/DEPLOY-WINDOWS.md"
cp "$ROOT/RELEASE-NOTES.md" "$OUT/RELEASE-NOTES.md"
cp "$DEPLOY/start-kestrel.ps1" "$OUT/start-kestrel.ps1"
cp "$DEPLOY/set-connection-string.ps1" "$OUT/set-connection-string.ps1"
cp "$DEPLOY/web.config" "$OUT/web.config"
# Never ship a real Production json — extracting the zip would wipe the server connection string.
rm -f "$OUT/appsettings.Production.json"
cp "$DEPLOY/appsettings.Production.json" "$OUT/appsettings.Production.json.example"
if [[ -f "$ROOT/artifacts/user-guide/Dashboard-User-Guide.html" ]]; then
  cp "$ROOT/artifacts/user-guide/Dashboard-User-Guide.html" "$OUT/Dashboard-User-Guide.html"
fi

printf '%s\n' "$VERSION" > "$OUT/VERSION.txt"

rm -f "$ZIP_DIR/$ZIP_NAME" "$ZIP_DIR/$ZIP_ALIAS"
(
  cd "$OUT"
  zip -r -q "$ZIP_DIR/$ZIP_NAME" .
)
cp "$ZIP_DIR/$ZIP_NAME" "$ZIP_DIR/$ZIP_ALIAS"
cp "$OUT/DEPLOY-WINDOWS.md" "$ZIP_DIR/DEPLOY-WINDOWS.md"
cp "$ROOT/RELEASE-NOTES.md" "$ZIP_DIR/RELEASE-NOTES.md"

echo "Version: $VERSION"
echo "Published: $OUT"
echo "Zip: $ZIP_DIR/$ZIP_NAME"
ls -lh "$ZIP_DIR/$ZIP_NAME"
