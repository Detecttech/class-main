#!/usr/bin/env bash
set -e

# ==============================================================================
# QuizBattle - GCE VM Automated Startup & Provisioning Script
# ==============================================================================

export DEBIAN_FRONTEND=noninteractive

echo "==> Updating package repository and installing prerequisites..."
apt-get update -y
apt-get install -y ca-certificates curl gnupg git python3 make g++ debian-keyring debian-archive-keyring apt-transport-https

echo "==> Installing Node.js 20 LTS..."
mkdir -p /etc/apt/keyrings
curl -fsSL https://deb.nodesource.com/gpgkey/nodesource-repo.gpg.key | gpg --dearmor -o /etc/apt/keyrings/nodesource.gpg --yes
echo "deb [signed-by=/etc/apt/keyrings/nodesource.gpg] https://deb.nodesource.com/node_20.x nodistro main" | tee /etc/apt/sources.list.d/nodesource.list
apt-get update -y
apt-get install -y nodejs

echo "==> Installing Caddy Web Server (automated TLS/HTTPS reverse proxy)..."
curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/gpg.key' | gpg --dearmor -o /usr/share/keyrings/caddy-stable-archive-keyring.gpg --yes
curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt' | tee /etc/apt/sources.list.d/caddy-stable.list
apt-get update -y
apt-get install -y caddy

echo "==> Installing PM2 process manager..."
npm install -g pm2

# QuizBattle Application Directory
APP_DIR="/opt/quizbattle"
mkdir -p "$APP_DIR"

if [ -d "$APP_DIR/server" ]; then
    echo "==> Building QuizBattle web portal and server..."
    cd "$APP_DIR/server/web-portal" && npm install && npm run build
    cd "$APP_DIR/server" && npm install && npm run build

    # Create persistent data directory
    mkdir -p "$APP_DIR/server/data"

    echo "==> Configuring PM2 ecosystem..."
    cat << 'EOF' > "$APP_DIR/ecosystem.config.cjs"
module.exports = {
  apps: [{
    name: 'quizbattle',
    script: 'dist/index.js',
    cwd: '/opt/quizbattle/server',
    env: {
      NODE_ENV: 'production',
      PORT: '7777',
      MODE: 'wan',
      SERVER_NAME: 'Classroom QuizBattle',
      DB_PATH: '/opt/quizbattle/server/data/quizbattle.db'
    },
    restart_delay: 3000,
    max_restarts: 10
  }]
};
EOF

    pm2 start "$APP_DIR/ecosystem.config.cjs"
    pm2 save
    pm2 startup systemd -u root --hp /root || true
fi

echo "==> Configuring Caddy reverse proxy for HTTP and WebSocket..."
cat << 'EOF' > /etc/caddy/Caddyfile
:80 {
    reverse_proxy localhost:7777
}

:443 {
    tls internal
    reverse_proxy localhost:7777
}
EOF

systemctl restart caddy
systemctl enable caddy

echo "==> QuizBattle startup provisioning complete!"
