#!/usr/bin/env bash
# First installation of the SkyRUS site on the server of the network. Run as root:
#   git clone https://github.com/Anntixs/SkyRUS-site.git ~/SkyRUS-site
#   bash ~/SkyRUS-site/deploy/install.sh skyrus.example.com
set -euo pipefail

DOMAIN=${1:?"Укажите домен сайта SkyRUS, например: bash install.sh skyrus.example.com"}
SRC=~/SkyRUS-site

id skyrus >/dev/null 2>&1 || useradd --system --home /var/lib/skyrus --shell /usr/sbin/nologin skyrus
mkdir -p /var/lib/skyrus /opt/skyrus/site /etc/skyrus
chown skyrus:skyrus /var/lib/skyrus

# Settings with the API key: readable by root only (systemd reads them before starting the site).
if [ ! -f /etc/skyrus/site.env ]; then
  read -rp "Адрес сайта сети SkyNetwork (например https://sky.network.npzy2.us): " NET
  read -rp "Вход через SkyNetwork — client_id (сайт сети: Управление → «Вход на другие сайты»): " CLIENT_ID
  read -rp "Вход через SkyNetwork — секрет (sks_…): " CLIENT_SECRET
  read -rp "Ключ API дивизиона SKYRUS (skd_…, сайт сети: «Заявки на рейтинг» → «Ключи API дивизионов»): " KEY
  read -rp "CID администраторов SkyRUS через запятую: " ADMINS
  umask 077
  cat > /etc/skyrus/site.env <<ENV
Site__NetworkUrl=$NET
Site__ConnectClientId=$CLIENT_ID
Site__ConnectClientSecret=$CLIENT_SECRET
Site__NetworkApiKey=$KEY
Site__Admins=$ADMINS
Site__PublicUrl=https://$DOMAIN
ENV
fi

cp "$SRC/deploy/skyrus-site.service" /etc/systemd/system/
systemctl daemon-reload
systemctl enable skyrus-site

# Caddy: the site behind HTTPS on its own domain (the certificate is issued automatically).
if [ -f /etc/caddy/Caddyfile ] && ! grep -q "^$DOMAIN" /etc/caddy/Caddyfile; then
  printf '\n%s {\n\treverse_proxy 127.0.0.1:8100\n}\n' "$DOMAIN" >> /etc/caddy/Caddyfile
  systemctl reload caddy
fi

bash "$SRC/deploy/update.sh"
