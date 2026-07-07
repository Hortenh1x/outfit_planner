# Deploy via Cloudflare Tunnel (home machine, no static IP)

This runs the Docker stack locally and exposes it through a **Cloudflare Tunnel**. The tunnel
makes an **outbound** connection to Cloudflare, so you need **no static IP, no port forwarding, and
no DNS-to-IP records**. If your home IP changes, `cloudflared` reconnects automatically and the site
stays up — there is nothing to update (no Dynamic DNS needed).

There are two ways to run `cloudflared` itself. **This machine currently uses option A** (a host
systemd service pointed at the dev stack) — that's the setup actually serving `outfitplanner.net`
today. Option B (the Docker sidecar + production nginx containers) is documented below too, since
the compose files for it still exist and it remains a valid alternative if you'd rather keep
`cloudflared` inside Docker.

## Option A — host systemd `cloudflared` → dev stack (active setup)

```
Browser → https://outfitplanner.net → Cloudflare edge (public TLS) → Tunnel (outbound from this host)
       → cloudflared (systemd service, host network) → https://localhost:5173
       → Vite dev server (frontend container, self-signed dev cert) → /api proxy
       → ASP.NET api container (dev) → Postgres/Redis/MinIO/rembg (dev containers)
```

`cloudflared` runs as a **host-level systemd unit**, not a container:

```bash
systemctl status cloudflared      # should be active (running)
journalctl -u cloudflared -f      # live tunnel/origin logs
systemctl cat cloudflared         # shows the unit; ExecStart embeds the tunnel token directly
                                   # (this is what `sudo cloudflared service install <TOKEN>` writes —
                                   #  no .env / TUNNEL_TOKEN involved for this path, and no local
                                   #  /etc/cloudflared/config.yml either; the Public Hostname route is
                                   #  configured remotely in the Zero Trust dashboard)
```

Because `cloudflared` runs on the **host** network (not the Docker Compose network), it cannot
resolve Docker service DNS names like `frontend`. That's why its Public Hostname route targets
`https://localhost:5173` — the port `docker-compose.dev.yml`'s `frontend` service publishes to the
host — instead of `frontend:443`, which only resolves inside the Docker network (see Option B).

**Bring the stack up:**

```bash
docker compose -f docker-compose.dev.yml -f docker-compose.selfhost.override.yml up -d
```

This is the same command documented in the main [README](README.md) for the self-host dev stack: it
adds the `rembg` service for real AI garment background removal and wires FASHN/Google/Apple
credentials from `.env` on top of the plain dev stack (Postgres, Redis, MinIO, API, Vite frontend on
`5173`).

Wait for the frontend to actually finish starting before hitting the domain — `frontend` runs
`npm ci && npm run dev:docker`, which takes a while on a cold `frontend_node_modules` volume:

```bash
docker compose -f docker-compose.dev.yml -f docker-compose.selfhost.override.yml logs -f frontend
# wait for Vite to report it's ready (e.g. "ready in ...ms" / "Local: https://...")
```

Then open `https://outfitplanner.net/api/health` → `{"status":"ok"}`, and `https://outfitplanner.net`
for the app.

### Diagnosing "Bad gateway" (Cloudflare error 502)

Cloudflare's error page tells you where the failure is: **Cloudflare edge = Working** but
**Host = Error** means the tunnel reached this machine fine, but `cloudflared` couldn't get a valid
response from `https://localhost:5173`. Confirm and fix:

1. `journalctl -u cloudflared -n 50 --no-pager` — look for `Unable to reach the origin service` /
   `connection reset by peer` / `EOF` on `originService=https://localhost:5173`. This means nothing
   valid was listening on `5173` at that moment.
2. `docker compose -f docker-compose.dev.yml -f docker-compose.selfhost.override.yml ps` — confirm
   the `frontend` container is actually `Up`. If there are no containers at all, the stack was never
   brought up (or was stopped) — run the `up -d` command above.
3. If `frontend` is up but still failing, check its own logs for a crash/restart loop
   (`... logs frontend`) rather than treating it as a tunnel problem — `cloudflared` itself is not
   the faulty part in this failure mode.
4. This is a startup race, not a compose bug: right after `up -d`, give `frontend` time to finish
   `npm ci` and boot Vite before expecting the public domain to respond.

## Option B — Docker sidecar `cloudflared` → production containers (alternative, not currently used)

```
Browser → https://<your-host> → Cloudflare edge (public TLS) → Tunnel (outbound from your PC)
       → cloudflared container → https://frontend:443 (nginx) → React SPA + /api → ASP.NET → Postgres/Redis/MinIO
```

- `docker-compose.cloudflared.yml` adds the `cloudflared` sidecar (token-based) to the **production**
  compose stack (`docker-compose.yml`), whose `frontend` service is an nginx container serving the
  built React app and proxying `/api`/`/uploads` on ports `80`/`443`.
- `.secrets/tls/{fullchain,privkey}.pem` — self-signed cert for the **internal** cloudflared→nginx hop
  only (users never see it; Cloudflare provides the real public certificate). Gitignored.
- `TUNNEL_TOKEN` in `.env` — used only by this Docker-sidecar path (the systemd path above does not
  read `.env` at all; its token lives in the unit file).

### What you do on the Cloudflare website (for this option)

1. **Add your domain to Cloudflare** (skip if already there): dash.cloudflare.com → *Add a site* → enter your domain → Free plan → update your registrar's nameservers to the two Cloudflare nameservers shown → wait until the zone is **Active**.
2. **Create the tunnel**: go to **Zero Trust** (one-time: pick a team name) → **Networks → Tunnels → Create a tunnel** → connector **Cloudflared** → name it e.g. `outfit-planner` → **Save**.
3. **Copy the token**: on the "Install connector" screen, copy the long token out of the shown
   `cloudflared ... run <TOKEN>` command (just the `<TOKEN>` part). Paste it into `.env`:
   ```
   TUNNEL_TOKEN=eyJ...your-token...
   ```
   (Do **not** run the install command they show on the host — that installs the systemd path from
   Option A instead. For this Docker-sidecar option, let the compose file run `cloudflared` for you.)
4. **Add the public hostname**: in the tunnel → **Public Hostname → Add a public hostname**:
   - Subdomain: e.g. `outfit` (or leave blank for the apex domain)
   - Domain: your domain
   - Path: leave blank
   - **Service → Type: HTTPS**, **URL: `frontend:443`**
   - **Additional application settings → TLS → No TLS Verify: ON**
   - Save. (This auto-creates the proxied DNS record — no IP involved.)

### Bring it up (for this option)

```bash
docker compose -f docker-compose.yml -f docker-compose.cloudflared.yml up -d --build
# Optional: add FASHN/Google/Apple from .env via the self-host overrides:
# docker compose -f docker-compose.yml -f docker-compose.selfhost.override.yml -f docker-compose.cloudflared.yml up -d --build
docker compose -f docker-compose.yml -f docker-compose.cloudflared.yml logs -f cloudflared   # should show "Registered tunnel connection"
```
Then open `https://<your-host>/api/health` → `{"status":"ok"}`, and `https://<your-host>` for the app.

## Notes (both options)

- **Google/Apple sign-in**: set `PUBLIC_ORIGIN=https://<your-host>` in `.env`, run with the self-host
  override, and register `https://<your-host>/api/auth/external/google/callback` in Google Cloud
  Console.
- **Real garment background removal**: `docker-compose.selfhost.override.yml` already wires the
  `rembg` service for AI cutouts; the plain production stack (`docker-compose.yml` alone) falls back
  to a low-quality "simple" remover.
- Cloudflare Free caps uploads at ~100 MB; the app already caps photos at 50 MB, so you are fine.
- Whichever option is active on a given host, `cloudflared`'s Public Hostname target (`localhost:5173`
  vs `frontend:443`) must match where `cloudflared` actually runs (host network vs Docker network) —
  mixing them up produces exactly the "Bad gateway" symptom described above.
- **To stop Option A**: `docker compose -f docker-compose.dev.yml -f docker-compose.selfhost.override.yml down` (add `-v` to also wipe data volumes); the systemd `cloudflared` service keeps running independently (`sudo systemctl stop cloudflared` to stop the tunnel itself).
- **To stop Option B**: `docker compose -f docker-compose.yml -f docker-compose.cloudflared.yml down` (add `-v` to also wipe data volumes).
