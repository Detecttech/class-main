# WAN Deployment

The server (`/server`) needs no code changes to run over the internet instead of a
classroom LAN — `httpServer.listen(config.httpPort)` already binds all network
interfaces, and the Unity Connect screen already accepts any host, not just a LAN IP.
"WAN mode" is a deployment choice, not a different codebase.

## Option A — Cloud VM (recommended for a real remote class)

1. Provision a small Linux VM (1 vCPU / 1GB RAM is plenty for classroom scale) with a
   public IP or DNS name, and Node.js 20+ installed.
2. Copy the repo (or `git clone`) to the VM, then:
   ```
   cd server
   npm install
   npm run build          # builds the web-portal, served by this same process
   cd web-portal && npm install && npm run build && cd ..
   ```
3. Set environment variables before starting (see **Configuration** below) — at minimum
   change `JWT_SECRET`.
4. Start it: `npm start` (or run it under `pm2`/`systemd` so it survives disconnects and
   restarts on crash — see **Keeping it running** below).
5. Open the port in the VM's firewall/security group: `PORT` (default 7777) for both the
   web portal and the game's WebSocket traffic. `DISCOVERY_PORT` (7778/UDP) only matters
   for LAN discovery and can stay closed for a pure WAN deployment.
6. Teachers open `http://<public-host>:7777` for the web portal; students enter
   `<public-host>` and port `7777` on the app's Connect screen.

## Option B — Teacher's own machine + router port-forward

Same server, run locally (`npm run dev` or `npm start`) on the teacher's laptop, with the
home/school router configured to forward the chosen `PORT` to that laptop's LAN IP. Works
for a single remote session without paying for a VM, but the server is only reachable
while that laptop and its network connection stay up — the cloud VM option is more
reliable for anything beyond an occasional one-off.

## Configuration

All via environment variables (see `server/src/config.ts`):

| Variable | Default | Notes |
|---|---|---|
| `PORT` | `7777` | HTTP + WebSocket port |
| `DISCOVERY_PORT` | `7778` | UDP LAN-discovery responder; irrelevant for WAN |
| `MODE` | `lan` | Set to `wan` — informational today (surfaced in `/api/server/info` and the discovery response) but set it correctly so future client UI can rely on it |
| `JWT_SECRET` | `dev-secret-change-me` | **Must** be overridden for anything beyond local dev — this signs every teacher/student auth token |
| `DB_PATH` | `server/data/quizbattle.db` | Point this at a persistent disk/volume on the VM so data survives restarts |
| `SERVER_NAME` | `Classroom QuizBattle` | Shown in `/api/server/info` and the LAN discovery response |

## HTTPS/WSS — known v1 limitation

The server currently speaks plain HTTP/WS, not HTTPS/WSS. This is fine for LAN use and
for quick WAN tests, but two things to know before a real public deployment:

- Some networks and OS-level policies block or warn on plain `ws://` traffic to a
  non-local host, and Android's default network security config disallows cleartext
  HTTP for apps targeting recent API levels unless explicitly allowed.
- For a production WAN deployment, put a TLS-terminating reverse proxy in front of the
  Node process (Caddy or nginx are both simple to point at `localhost:7777`, and Caddy
  will fetch/renew a Let's Encrypt certificate automatically for a real domain name), or
  use a tunneling service (e.g. Cloudflare Tunnel) that provides TLS without managing
  certificates yourself. Either way, the app's Connect screen would then use `wss://` —
  a small follow-up change to `SessionManager.WsUrl` / `HttpBaseUrl` (currently hardcoded
  to `ws://`/`http://`) to support a `https`/`wss` toggle.

## Keeping it running

For anything beyond a quick test, run the server under a process manager so it
auto-restarts on crash or VM reboot:

```
npm install -g pm2
pm2 start dist/index.js --name quizbattle
pm2 save
pm2 startup   # prints the systemd command to enable auto-start on boot
```

(`npm run build` first, since `pm2` here runs the compiled output rather than `tsx`.)
