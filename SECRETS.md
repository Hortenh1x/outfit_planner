# Secrets Inventory and Rotation

All live credentials stay in the repo-root `.env` (gitignored, `600` permissions) and are
consumed by the compose stacks and by bare `dotnet run` through the startup dotenv aliases.
`.env` has never been committed and none of the current values appear anywhere in git
history (verified by a full-history value scan on 2026-07-08); `.env.example` carries
placeholders only. Keep it that way: never paste values into tracked files, commit
messages, or docs — refer to variable names only.

## Rotation tool

`tools/rotate-secret.sh` swaps a value in `.env` without ever printing it:

```bash
tools/rotate-secret.sh list                                        # names + lengths only
tools/rotate-secret.sh rotate OBJECT_STORAGE_SIGNING_SECRET --generate
tools/rotate-secret.sh rotate FASHN_API_KEY --stdin [--restart]    # paste hidden; validated against FASHN before the swap
```

It backs up the previous file to `.env.bak.<timestamp>` (gitignored, `600`), appends a
value-free line to `.secrets-rotation.log`, re-asserts `600` on `.env`, validates
`FASHN_API_KEY` against the FASHN credits endpoint before committing the swap (no credits
spent; `--no-verify` to skip), and with `--restart` recreates the compose `api` service
and waits for `/api/health`. Running processes keep the old value until restarted.

## Inventory

| Variable | Kind | Issued at | How to rotate |
| --- | --- | --- | --- |
| `FASHN_API_KEY` | dashboard-issued secret | [app.fashn.ai](https://app.fashn.ai) → Settings → API keys | Create a NEW key in the dashboard → `rotate FASHN_API_KEY --stdin --restart` → revoke the OLD key in the dashboard. |
| `GOOGLE_CLIENT_SECRET` | dashboard-issued secret | [Google Cloud console](https://console.cloud.google.com/apis/credentials) → OAuth client | Add a new client secret on the same OAuth client → `rotate GOOGLE_CLIENT_SECRET --stdin --restart` → delete the old secret in the console. (`GOOGLE_CLIENT_ID` is a public identifier, not a secret.) |
| `APPLE_CLIENT_SECRET` | short-lived signed JWT | [developer.apple.com](https://developer.apple.com/account/resources/authkeys/list) key | Currently a placeholder (Apple sign-in not configured). When in use: regenerate the JWT from the Apple key; if the signing key leaked, revoke the key itself. |
| `TUNNEL_TOKEN` | dashboard-issued token | Cloudflare Zero Trust → Networks → Tunnels | Rotate the token in Zero Trust → `rotate TUNNEL_TOKEN --stdin`. Note: the public domain is currently fronted by a host-level `cloudflared` systemd service with its own copy of the token — refresh that service too (`systemctl restart cloudflared` after updating its config), not just `.env`. |
| `OBJECT_STORAGE_SIGNING_SECRET` | self-issued HMAC secret | this machine | `rotate OBJECT_STORAGE_SIGNING_SECRET --generate` — nothing to revoke; outstanding signed URLs go invalid and are re-signed transparently on the next read. |

## Zero-downtime order for dashboard-issued keys

1. Create/issue the NEW credential at the issuer (both keys briefly coexist).
2. `tools/rotate-secret.sh rotate <KEY> --stdin --restart` — validates (FASHN), swaps `.env`, recreates the `api` service, waits for health.
3. Revoke the OLD credential at the issuer.

If step 2 fails, `.env` is untouched (validation failures) or restorable from the
timestamped backup.

## Cadence

Rotate dashboard-issued keys roughly quarterly and immediately after any suspected
exposure (a key pasted into a chat/log/screenshot, a machine handed over, a tunnel token
reused elsewhere). A recurring reminder exists as a Claude scheduled task; the rotation
log records when each key last changed.
