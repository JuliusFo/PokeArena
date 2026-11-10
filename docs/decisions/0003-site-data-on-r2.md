# ADR-0003: Website data on Cloudflare R2

Status: accepted · Date: 2026-10-11 · Supersedes the GitHub Contents API
approach of the original spec (2026-09-03)

## Context

The original spec committed every website data file to this repository via
the GitHub Contents API and redeployed GitHub Pages on each change. The design
review (2026-10-11) found:

- A public repository keeps deleted trainers' names in git history forever,
  so the deletion promise (spec §11) could not be kept.
- One commit per file and per change (≈ 13 for a boss with 10 players), each
  triggering a full Pages deploy (~1 min), plus SHA conflicts and GitHub's
  secondary write limits.

User decisions on 2026-10-11: adopt R2 for data; the user has no domain on
Cloudflare, so v1 uses the `r2.dev` subdomain.

## Decision

- The Blazor app stays on GitHub Pages, deployed only on code changes.
- All data JSON files live in a Cloudflare R2 bucket, public via `r2.dev`,
  CORS allowing `GET` from the Pages origin. The data base URL is a config
  value of the web app.
- The bot uploads through the S3-compatible API (`AWSSDK.S3`) with an API
  token scoped to the bucket (`Object Read & Write`), stored in the Windows
  credential store.
- `kampf/<id>.json` is uploaded with a long `Cache-Control` max-age; all other
  files with a short one (≤ poll interval).

Deliberately **not**: player data in any git branch; a custom domain in v1
(backlog); GitHub PAT.

## Consequences

- Uploads are live within seconds; deletion removes the object for real.
- A second provider (Cloudflare account, token, CORS) needs a one-time setup.
- `r2.dev` is rate-limited and uncached — acceptable at ~30 viewers (≈ 2 req/s
  of polling). Moving to a custom domain is a config change.

## Addendum 2026-11-01 (WP-06): custom domains instead of `r2.dev`

Every read through `r2.dev` is a billed Class B operation once the free
10 million per month are used up, R2 has no spending cap, and `r2.dev` cannot
be cached. Anyone who scripts requests against the public URL turns that into
a bill — the free storage (10 GB) is irrelevant by comparison, since only the
bot can write. The user already has a domain whose DNS is on Cloudflare
(HorizonNET). User decisions:

- The bucket lives in the **user's Cloudflare account** (the account that
  holds the zone — a custom domain requires that). Moving it to the streamer
  later means a new bucket, token and base URL plus "republish everything".
- **Data** is served from a subdomain of that zone connected to the bucket as
  an R2 custom domain (placeholder `arena-daten.<domain>`). `r2.dev` stays
  disabled, so no uncached path exists.
- A **cache rule** makes the data host eligible for caching and respects the
  origin's `Cache-Control` (Cloudflare does not cache `.json` by default).
  Cache hits do not reach R2 and cost nothing.
- One **rate-limiting rule** (the free plan allows one per zone) on the data
  host blocks single clients that poll far above the site's own rate.
- The **site** keeps GitHub Pages, now under a subdomain of the same zone
  (placeholder `arena.<domain>`, CNAME to `juliusfo.github.io`, DNS only — no
  proxy, GitHub issues the certificate). It is served from `/`, and the chat
  links become short. CORS on the bucket allows that origin.
- Both hostnames are **first-level** subdomains: the free Universal SSL
  certificate covers `*.<domain>` only, not `*.arena.<domain>`.

This supersedes "a custom domain in v1 (backlog)" above; the rest stands.

## Addendum 2026-11-10: site deployed with the release tag

User decision: the Pages workflow (`pages.yml`) runs on release tags `v*`
(and by hand) instead of on pushes to `main`. "Deployed only on code changes"
above becomes "deployed with each release": `main` may hold website changes
and „Neuigkeiten“ lines that are not released yet, and they go live together
with the desktop release. The `github-pages` environment needs the tag rule
`v*` (`docs/website-setup.md` §3).
