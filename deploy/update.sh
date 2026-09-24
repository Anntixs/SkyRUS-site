#!/usr/bin/env bash
# Updates the SkyRUS site from GitHub and restarts it. Run as root:
#   bash ~/SkyRUS-site/deploy/update.sh
set -euo pipefail

SRC=~/SkyRUS-site
git -C "$SRC" fetch -q origin main
git -C "$SRC" checkout -q -B main origin/main
echo "== SkyRUS: $(git -C "$SRC" log --oneline -1)"

rm -rf ~/skyrus-build
dotnet publish "$SRC/src/SkyRus.Site" -c Release -o ~/skyrus-build --nologo -v q
# Stopped first: overwriting the DLLs of a running site crashes it.
systemctl stop skyrus-site || true
mkdir -p /opt/skyrus/site
cp -r ~/skyrus-build/. /opt/skyrus/site/
# The site runs as the skyrus user, so it must be able to read its files whatever root's umask is.
chmod -R a+rX /opt/skyrus/site
systemctl start skyrus-site
sleep 4
systemctl is-active skyrus-site
