// RichLife pacing simulator — a greedy "active player" on the real catalogue.
// Usage: node sim.mjs '{"g":1.07,"h":1.15,"prices":[...]}'
import { readFileSync } from 'node:fs';

const cat = JSON.parse(readFileSync(new URL('./catalogue.json', import.meta.url)));
const P = JSON.parse(process.argv[2] ?? '{}');
const g = P.g ?? 1;            // unit price × g per unit of the same type already owned
const h = P.h ?? 1;            // extra place price × h per place already owned
const prices = P.prices ?? [25e3, 2e5, 2e6, 2e7, 2.5e8, 3e9];   // P1→P2 … P6→P7
const sessions = P.sessions ?? [[8, 35], [13, 35], [18, 35], [22, 35]]; // [hour, minutes]
const days = P.days ?? 200;
const valueLevels = P.valueLevels ?? false; // price levels from what is invested, not the opening price
const bizShare = P.bizShare ?? 0.25, placeShare = P.placeShare ?? 0.25;
const tierMult = [0, 1, 1.5, 2.2];
const TAX = 0.93, BREAK = 0.95;
const scale = P.scale ?? [1, 1, 1, 1, 1, 1, 1, 1]; // income × scale[prestige] (index 1..7)
const passiveBase = P.passive ?? 3;
for (const c of cat) { const f = scale[c.prestige]; for (const k of c.kinds) k.income *= f; for (const u of c.units) u.income *= f; }

const levelMult = (l) => (1 + 0.1 * (l - 1)) * [10, 25, 50].reduce((m, x) => (l >= x ? m * 2 : m), 1);

let cash = 0, L = 1, n = 0;
const owned = new Map(); // id -> { c, level, places:[{k, lvl}], units: Map(unitId -> count), placesUnits:[[income...]] }
const mult = () => 1 + 0.18 * n;
const passive = () => passiveBase * mult();

function placeMult(p) { return tierMult[p.k.tier] * (1 + 0.25 * (p.lvl - 1)); }
function placeCap(b, p) { return b.c.cap + b.c.capLvl * (p.lvl - 1); }
function bizIncome(b) {
  let s = 0;
  for (const p of b.places) s += (p.base + p.units.reduce((a, x) => a + x, 0)) * placeMult(p);
  return s * levelMult(b.level);
}
function income() {
  let s = passive();
  for (const b of owned.values()) s += bizIncome(b) * mult() * TAX * BREAK;
  return s;
}

// Every purchasable option: { cost, gain, apply }
function options() {
  const out = [];
  const m = mult() * TAX * BREAK;
  for (const c of cat) {
    if (c.prestige > L) continue;
    const b = owned.get(c.id);
    if (!b) {
      const k = c.kinds[0];
      out.push({ cost: c.opening, gain: k.income * tierMult[k.tier] * m, apply: () => {
        owned.set(c.id, { c, level: 1, places: [{ k, lvl: 1, base: k.income, units: [] }], counts: new Map(), total: 0 });
      } });
      continue;
    }
    const lm = levelMult(b.level);
    // units
    const roomy = b.places.filter((p) => p.units.length < placeCap(b, p));
    if (roomy.length) {
      const best = roomy.reduce((a, p) => (placeMult(p) > placeMult(a) ? p : a));
      for (const u of c.units) {
        if (b.total < u.unlock) continue;
        const cnt = b.counts.get(u.id) ?? 0;
        const cost = u.price * Math.pow(g, cnt);
        out.push({ cost, gain: u.income * placeMult(best) * lm * m, apply: () => {
          best.units.push(u.income); best.value = (best.value ?? 0) + cost; b.counts.set(u.id, cnt + 1); b.total++;
        } });
      }
    }
    // new place
    if (b.places.length < 10) {
      for (const k of c.kinds) {
        const cost = k.price * Math.pow(h, b.places.length - 1);
        out.push({ cost, gain: k.income * tierMult[k.tier] * lm * m, apply: () => b.places.push({ k, lvl: 1, base: k.income, units: [] }) });
      }
    }
    // place level
    for (const p of b.places) {
      if (p.lvl >= 10) continue;
      const basis = valueLevels ? p.k.price + (p.value ?? 0) : p.k.price;
      const cost = basis * placeShare * Math.pow(1.35, p.lvl - 1);
      const before = (p.base + p.units.reduce((a, x) => a + x, 0)) * placeMult(p);
      const after = before / placeMult(p) * tierMult[p.k.tier] * (1 + 0.25 * p.lvl);
      out.push({ cost, gain: (after - before) * lm * m, apply: () => { p.lvl++; } });
    }
    // business level
    if (b.level < 100) {
      const invested = c.opening + b.places.reduce((a, p) => a + (p.value ?? 0) + (p === b.places[0] ? 0 : p.k.price), 0);
      const cost = (valueLevels ? Math.max(c.opening, invested * bizShare) : c.opening) * Math.pow(1.25, b.level - 1);
      const raw = bizIncome(b) / lm;
      out.push({ cost, gain: raw * (levelMult(b.level + 1) - lm) * m, apply: () => { b.level++; } });
    }
  }
  return out;
}

function shop() {
  for (let guard = 0; guard < 2000; guard++) {
    if (L < 7 && cash >= prices[L - 1]) { cash -= prices[L - 1]; L++; n++; reached.push([L, t / 86400]); continue; }
    const inc = income();
    // Save for prestige: only buy what pays back faster than the wait for the prestige price.
    const wait = L < 7 ? Math.max(0, prices[L - 1] - cash) / inc : Infinity;
    let best = null;
    for (const o of options()) {
      if (o.cost > cash || o.gain <= 0) continue;
      const pb = o.cost / o.gain;
      if (pb > wait && L < 7) continue;
      if (!best || pb < best.pb) best = { ...o, pb };
    }
    if (!best) return;
    cash -= best.cost; best.apply();
  }
}

// Managers: at the end of a session, hire where 4 h of income beats the shift's price.
function hireManagers() {
  const managed = [];
  for (const b of owned.values()) {
    const earn = bizIncome(b) * mult() * TAX * BREAK * 4 * 3600;
    const cost = 2 * b.c.opening;
    if (earn > cost * 1.2 && cash >= cost) { cash -= cost; managed.push(b); }
  }
  return managed;
}

const reached = [];
let t = 0;
for (let d = 0; d < days && L < 7; d++) {
  for (let s = 0; s < sessions.length; s++) {
    const [hour, minutes] = sessions[s];
    t = d * 86400 + hour * 3600;
    for (let mnt = 0; mnt < minutes; mnt++) { cash += income() * 60; t += 60; shop(); }
    if (P.trace && s === 0) { const top = [...owned.values()].map((b) => [b.c.id, b.level, b.places.length, b.total, Math.round(bizIncome(b))]).sort((a, b2) => b2[4] - a[4]).slice(0, 4); console.log('day', d, 'P' + L, 'inc', income().toExponential(2), 'cash', cash.toExponential(2), JSON.stringify(top)); }
    const managed = hireManagers();
    const next = s + 1 < sessions.length ? sessions[s + 1][0] + 24 * 0 : 24 + sessions[0][0];
    const gap = Math.min(4 * 3600, (next - hour) * 3600 - minutes * 60);
    let off = passive() * gap;
    for (const b of managed) off += bizIncome(b) * mult() * TAX * BREAK * Math.min(gap, 4 * 3600);
    cash += off;
  }
}
const out = reached.map(([lvl, day]) => `P${lvl}: day ${day.toFixed(1)}`).join(' | ');
console.log(out || 'no prestige', `| businesses ${owned.size} | income ${income().toExponential(2)}/s`);
