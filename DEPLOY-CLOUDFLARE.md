# Deploy via Cloudflare Tunnel (home machine, no static IP)

This runs the full Docker stack locally and exposes it through a **Cloudflare Tunnel**. The tunnel
makes an **outbound** connection to Cloudflare, so you need **no static IP, no port forwarding, and
no DNS-to-IP records**. If your home IP changes, `cloudflared` reconnects automatically and the site
stays up — there is nothing to update (no Dynamic DNS needed).

```
Browser → https://<your-host> → Cloudflare edge (public TLS) → Tunnel (outbound from your PC)
       → cloudflared container → https://frontend:443 (nginx) → React SPA + /api → ASP.NET → Postgres/Redis/MinIO
```

## Already set up locally (by the assistant)
- `docker-compose.cloudflared.yml` — adds the `cloudflared` sidecar (token-based).
- `.secrets/tls/{fullchain,privkey}.pem` — self-signed cert for the **internal** cloudflared→nginx hop only (users never see it; Cloudflare provides the real public certificate). Gitignored.
- `.env` — added `OBJECT_STORAGE_SIGNING_SECRET` (generated) and an empty `TUNNEL_TOKEN` for you to fill.

## What you do on the Cloudflare website
1. **Add your domain to Cloudflare** (skip if already there): dash.cloudflare.com → *Add a site* → enter your domain → Free plan → update your registrar's nameservers to the two Cloudflare nameservers shown → wait until the zone is **Active**.
2. **Create the tunnel**: go to **Zero Trust** (one-time: pick a team name) → **Networks → Tunnels → Create a tunnel** → connector **Cloudflared** → name it e.g. `outfit-planner` → **Save**.
3. **Copy the token**: on the "Install connector" screen, copy the long token out of the shown
   `cloudflared ... run <TOKEN>` command (just the `<TOKEN>` part). Paste it into `.env`:
   ```
   TUNNEL_TOKEN=eyJ...your-token...
   ```
   (Do **not** run the install command they show — our Docker sidecar runs cloudflared for you.)
4. **Add the public hostname**: in the tunnel → **Public Hostname → Add a public hostname**:
   - Subdomain: e.g. `outfit` (or leave blank for the apex domain)
   - Domain: your domain
   - Path: leave blank
   - **Service → Type: HTTPS**, **URL: `frontend:443`**
   - **Additional application settings → TLS → No TLS Verify: ON**
   - Save. (This auto-creates the proxied DNS record — no IP involved.)

## Bring it up
```bash
docker compose -f docker-compose.yml -f docker-compose.cloudflared.yml up -d --build
# Optional: add FASHN/Google/Apple from .env via the self-host overrides:
# docker compose -f docker-compose.yml -f docker-compose.selfhost.override.yml -f docker-compose.cloudflared.yml up -d --build
docker compose -f docker-compose.yml -f docker-compose.cloudflared.yml logs -f cloudflared   # should show "Registered tunnel connection"
```
Then open `https://<your-host>/api/health` → `{"status":"ok"}`, and `https://<your-host>` for the app.

## Notes
- The tunnel ignores the host ports `80/443` published by `frontend` — those are not used and can clash with other local services; remove them from `docker-compose.yml` if needed.
- **Google/Apple sign-in**: set `PUBLIC_ORIGIN=https://<your-host>` in `.env`, run with the self-host override, and register `https://<your-host>/api/auth/external/google/callback` in Google Cloud Console.
- **Real garment background removal**: the base stack falls back to a low-quality "simple" remover; for AI cutouts add the rembg service (see `docker-compose.rembg.dev.yml` / self-host override wiring).
- Cloudflare Free caps uploads at ~100 MB; the app already caps photos at 50 MB, so you are fine.
- To stop: `docker compose -f docker-compose.yml -f docker-compose.cloudflared.yml down` (add `-v` to also wipe data volumes).
