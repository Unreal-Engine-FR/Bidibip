#!/bin/sh

APP_DIR="/opt/bidibip/app"
PLUGINS_DIR="/opt/bidibip/plugins"
REPO="Unreal-Engine-FR/Bidibip"
ASSET="Bidibip-linux-alpine-x64.tar.gz"

echo "Downloading latest release..."
rm -rf "$APP_DIR"
mkdir -p "$APP_DIR" "$PLUGINS_DIR" /tmp/bidibip-extract

wget -q -O /tmp/bidibip.tar.gz \
    "https://github.com/$REPO/releases/latest/download/$ASSET"

tar xzf /tmp/bidibip.tar.gz -C /tmp/bidibip-extract --strip-components=1

cp -f /tmp/bidibip-extract/Bidibip "$APP_DIR/"
cp -f /tmp/bidibip-extract/version.txt "$APP_DIR/" 2>/dev/null
cp -f /tmp/bidibip-extract/plugins/*.dll "$PLUGINS_DIR/" 2>/dev/null

rm -rf /tmp/bidibip.tar.gz /tmp/bidibip-extract
chmod +x "$APP_DIR/Bidibip"

echo "Starting Bidibip..."
cd /opt/bidibip
exec "$APP_DIR/Bidibip"
