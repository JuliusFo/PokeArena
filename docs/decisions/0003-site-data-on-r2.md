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
