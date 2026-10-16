# Pacing simulator

Plays the catalogue like an **active player** — 4 sessions a day (35 min each), managers
rehired at the end of each session, the 4-hour offline cap, saving for prestige, always buying
the purchase that pays back fastest — and prints the day each prestige level is reached.
Used to tune `features/016-pacing.md`. Real players are less optimal: expect them a bit slower.

```bash
export PATH="/c/Program Files/nodejs:$PATH"     # system Node is too old for some tools; plain node works here
npx -y -p node@20 node tools/pacing/sim.mjs '{"valueLevels":true,"g":1.2,"h":1.4,"passive":0.1,"prices":[1e5,5e8,2.2e9,9e9,3e10,1.2e11]}'
```

`catalogue.json` is the catalogue as it was **before** the 016 income scaling (straight out of
migration `CatalogueLikeRoadToRich`), so the run above also passes the scaling:
`"scale":[0,0.15,0.07,0.12,0.15,0.15,0.15,0.15]`. To simulate a live database instead, export it
again (the `json_agg` query in the 016 session) and use `"scale"` all ones.

Knobs: `g` unit price growth, `h` place price growth, `valueLevels` level prices from invested
value (on in the game since 016), `scale[p]` income × factor by prestige, `passive` base passive
income, `prices` prestige prices P1→P2 … P6→P7, `sessions` `[[hour, minutes], …]`, `days`,
`trace` (prints the top businesses each morning).

Result with the shipped settings (and the scale above): P2 day 4.5 · P3 10.3 · P4 16.8 · P5 28.3 ·
P6 43.8 · P7 59.8. Casual player (`"sessions":[[9,15],[20,15]]`): P2 day 10, P7 day 156.
