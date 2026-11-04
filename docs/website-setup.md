# Website setup (one-time)

How to bring the website online (WP-06, ADR-0003 addendum). Placeholders:
`<domain>` is the user's Cloudflare zone, `arena.<domain>` the site,
`arena-daten.<domain>` the data host. Both must be **first-level** subdomains
(the free Universal SSL certificate covers `*.<domain>` only). Dashboard
labels are from memory and may differ slightly.

## 1. Cloudflare R2 (user's account, the one holding the zone)

1. **Bucket:** R2 → Create bucket, e.g. `pokearena`, location automatic.
   Leave **public access via `r2.dev` disabled** — it is uncached and every
   read is billed.
2. **Custom domain:** bucket → Settings → Custom Domains → Connect domain →
   `arena-daten.<domain>`. Cloudflare creates the DNS record (proxied).
3. **CORS:** bucket → Settings → CORS policy:

   ```json
   [
     {
       "AllowedOrigins": ["https://arena.<domain>", "http://localhost:5180"],
       "AllowedMethods": ["GET", "HEAD"],
       "AllowedHeaders": ["*"],
       "MaxAgeSeconds": 3600
     }
   ]
   ```

   `localhost:5180` lets a local `dotnet run` of the site read the real data.
4. **API token:** R2 → Manage API tokens → Create → permission **Object Read &
   Write**, restricted to this bucket. Note the Access Key ID, the Secret
   Access Key (shown once) and the S3 endpoint
   `https://<account-id>.r2.cloudflarestorage.com`. Keep the keys in a password
   manager — they go into the desktop app (step 4), nowhere else.

## 2. Cloudflare zone rules (data host only)

1. **Cache rule** (Caching → Cache Rules → Create): when *Hostname equals*
   `arena-daten.<domain>` →
   - Cache eligibility: **Eligible for cache** (`.json` is not cached by
     default).
   - Edge TTL: **Use cache-control header if present**; the bot sends
     `max-age=10` for lists and a day for fights (a deleted trainer's
     name must drop out of the cache, spec §11).
   - Status code TTL: **404 → no store**. A battle link posted in chat can be
     opened a second before its upload; a cached 404 would hide the fight for
     minutes.
   - Browser TTL: respect origin.
2. **Rate-limiting rule** (Security → WAF → Rate limiting rules; the free plan
   has one per zone, currently unused). The free plan cannot match the
   hostname, only the path: when *URI Path contains* `.json`, counted per IP,
   **more than 50 requests in 10 seconds** → block for 10 seconds. It applies
   to the whole zone, but other sites on it (HorizonNET) fetch hardly any
   `.json`. A viewer with the site open makes about one request per 15
   seconds plus two per page opened, so only scripts hit the limit.
3. Optional: Notifications → check whether a usage/billing alert for R2
   exists and enable it.

## 3. GitHub Pages

1. Cloudflare DNS: `CNAME arena → juliusfo.github.io`, **DNS only** (grey
   cloud) — GitHub issues the certificate itself.
2. GitHub account settings → Pages → **Verified domains**: verify `<domain>`
   (protects the subdomain from takeover by another repository).
3. Repository → Settings → Pages: Source **GitHub Actions**; custom domain
   `arena.<domain>`; once the certificate exists, **Enforce HTTPS**.
4. Repository → Settings → Secrets and variables → Actions → **Variables**:
   `DATA_URL = https://arena-daten.<domain>/`.
5. Run the workflow "Website" once by hand (Actions tab) or push to `main`.

## 4. Desktop app

1. Settings (`appsettings.local.json` on the dev machine; on the streamer's PC
   next to the program until WP-08 decides installation):

   ```json
   {
     "PokeArena": {
       "Website": {
         "SiteUrl": "https://arena.<domain>",
         "R2Endpoint": "https://<account-id>.r2.cloudflarestorage.com",
         "Bucket": "pokearena"
       }
     }
   }
   ```

2. Start the app → Bot → **Website**: enter Access Key ID and Secret Access
   Key → „Speichern“ (stored in the Windows Credential Manager as
   `PokeArena:r2-access-key-id` / `PokeArena:r2-secret-access-key`).
3. „Alles neu veröffentlichen“ uploads everything already in the database.
4. OBS: add a browser source `http://localhost:5299/overlay` (size about
   400 × 400; the port is `PokeArena:Overlay:Port`).

## 5. Checks

- `dotnet test tests/PokeArena.Integration.Tests` — the R2 test now runs
  instead of being skipped.
- `https://arena-daten.<domain>/rangliste.json` shows JSON; a second request
  has `cf-cache-status: HIT` (browser dev tools, Network tab).
- `https://arena.<domain>/` shows the leaderboard without CORS errors in the
  browser console. If cached responses lack the CORS header, add a response
  header transform rule for the data host
  (`Access-Control-Allow-Origin: https://arena.<domain>`).
- A fight in chat posts a link that opens the battle page, and a direct link
  to `/kampf/<id>` or `/trainer/<name>` works after a reload.
