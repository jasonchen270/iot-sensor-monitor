#!/usr/bin/env bash
# Deploy to EC2 Ubuntu 22/24. Run on a fresh VM as root or with sudo.
# Usage: HOST=ec2-x.compute.amazonaws.com KEY=~/.ssh/key.pem ./deploy.sh
set -euo pipefail

HOST="${HOST:?set HOST}"
KEY="${KEY:?set KEY}"
SSH="ssh -i $KEY ubuntu@$HOST"
SCP="scp -i $KEY -r"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"

echo "==> building service"
( cd "$ROOT/service" && dotnet publish -c Release -o "$ROOT/service/publish" )

echo "==> building web"
( cd "$ROOT/web" && npm run build )

echo "==> remote bootstrap"
$SSH 'sudo apt-get update && sudo apt-get install -y nginx postgresql ca-certificates && \
  curl -sSL https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb -o /tmp/ms.deb && \
  sudo dpkg -i /tmp/ms.deb && sudo apt-get update && sudo apt-get install -y dotnet-runtime-10.0 || true; \
  sudo -u postgres psql -tc "SELECT 1 FROM pg_database WHERE datname=\"sensors\"" | grep -q 1 || \
    sudo -u postgres psql -c "CREATE USER sensors WITH PASSWORD '\''CHANGEME'\''; CREATE DATABASE sensors OWNER sensors;"'

echo "==> uploading"
$SSH 'sudo mkdir -p /opt/sensor-monitor /var/www/sensor-monitor && sudo chown -R ubuntu /opt/sensor-monitor /var/www/sensor-monitor'
$SCP "$ROOT/service/publish/." ubuntu@$HOST:/opt/sensor-monitor/
$SCP "$ROOT/web/dist/." ubuntu@$HOST:/var/www/sensor-monitor/
$SCP "$ROOT/deploy/nginx.conf" ubuntu@$HOST:/tmp/sensor-monitor.nginx
$SCP "$ROOT/deploy/sensor-monitor.service" ubuntu@$HOST:/tmp/sensor-monitor.service

echo "==> activating"
$SSH 'sudo mv /tmp/sensor-monitor.nginx /etc/nginx/sites-available/sensor-monitor && \
  sudo ln -sf /etc/nginx/sites-available/sensor-monitor /etc/nginx/sites-enabled/sensor-monitor && \
  sudo rm -f /etc/nginx/sites-enabled/default && \
  sudo nginx -t && sudo systemctl reload nginx && \
  sudo mv /tmp/sensor-monitor.service /etc/systemd/system/sensor-monitor.service && \
  sudo systemctl daemon-reload && sudo systemctl enable --now sensor-monitor && \
  sudo systemctl restart sensor-monitor'

echo "==> done. http://$HOST"
