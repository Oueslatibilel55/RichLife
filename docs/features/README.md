# Feature specs

One Markdown file per cross-cutting change — anything that touches both `RichLife/`
(backend) and `idle-startup-frontend/` (frontend). An agent or developer picking up
either half should be able to implement its side from the spec alone, without the
conversation that produced it.

## Naming

`NNN-short-kebab-title.md`, numbered in the order they were raised. Numbers are never
reused, even if a spec is abandoned.

## Order of work

The monorepo rule from the root `CLAUDE.md` applies: **contract first, then backend,
then frontend.**

1. `docs/api-contract.md` is updated, with the new field or route marked ⏳ pending.
2. The backend lands and matches the contract.
3. The frontend lands and matches the contract.
4. The ⏳ marker is removed from the contract and the spec's status becomes `Done`.

A spec is not finished until step 4. If the backend half ships and the ⏳ marker is
still in the contract, the next person cannot tell what is real.

## What a spec must contain

- **Status** — `Pending backend` / `Pending frontend` / `Done`.
- **Why** — the player-facing reason, in a sentence or two.
- **Backend changes** — exact files, exact edits, and explicitly what is *not* needed
  (migration? domain change? new endpoint?). Being specific about the absence of work is
  as useful as listing the work.
- **Frontend changes** — same.
- **Verification** — how to tell it actually works.

## Index

| # | Spec | Status |
|---|---|---|
| 001 | [All-time earnings on CompanyDto](001-all-time-earnings-on-company.md) | Pending frontend |
| 002 | [Business catalogue in the database, admin editor](002-catalogue-in-database.md) | Done (editor shipped with 006) |
| 003 | [Prestige is a purchase, not a reset](003-prestige-is-a-purchase.md) | Done |
| 004 | [Hire managers (business automation)](004-business-managers.md) | Done |
| 005 | [Business levels](005-business-levels.md) | Done |
| 006 | [Admin panel and dashboard](006-admin-panel.md) | Done |
| 007 | [Player profile and achievements](007-player-profile.md) | Done |
| 008 | [Luxury collection](008-luxury-collection.md) | Done |
