// Rush Week rules on the server: weeks, groups of 30, score limits, and computer rivals for empty seats.
// Same week rules as the game (Assets/Scripts/Core/RushWeek.cs): weeks start on Saturday, day 0 = 2000-01-01.
"use strict";

const GROUP_SIZE = 30;
const DAY_MS = 86400000;
const DAYS_1970_TO_2000 = 10957;
const NAMES = [["Sara","سارا"],["Ali","علی"],["Reza","رضا"],["Maryam","مریم"],["Amir","امیر"],["Neda","ندا"],["Hamid","حمید"],["Leila","لیلا"],["Omid","امید"],["Zahra","زهرا"],
  ["Kian","کیان"],["Nika","نیکا"],["Arash","آرش"],["Shirin","شیرین"],["Babak","بابک"],["Mina","مینا"],["Pouya","پویا"],["Yasaman","یاسمن"],["Dariush","داریوش"],["Elnaz","الناز"],
  ["Farhad","فرهاد"],["Golnar","گلنار"],["Hooman","هومن"],["Parisa","پریسا"],["Saman","سامان"],["Tara","تارا"],["Vahid","وحید"],["Roya","رویا"],["Navid","نوید"],["Bita","بیتا"]];

// Score limits: a player can add at most BURST points at once, refilled at RATE points per minute.
// One win gives about 10-40 points and takes a minute or more, so honest players never hit this.
const LIMITS = { burst: 200, ratePerMinute: 20 };

/** Local day number (days since 2000-01-01) for a UTC offset in minutes (Iran: +210). */
function dayOf(nowMs, offsetMin) { return Math.floor((nowMs + offsetMin * 60000) / DAY_MS) - DAYS_1970_TO_2000; }
function weekOf(nowMs, offsetMin) { return Math.floor(dayOf(nowMs, offsetMin) / 7); }
/** When a week ends (start of the next Saturday, local time), in UTC ms. */
function weekEndMs(week, offsetMin) { return ((week + 1) * 7 + DAYS_1970_TO_2000) * DAY_MS - offsetMin * 60000; }
function weekFraction(nowMs, week, offsetMin) { const end = weekEndMs(week, offsetMin); return Math.min(1, Math.max(0, 1 - (end - nowMs) / (7 * DAY_MS))); }

/** Applies the score limit. Returns the new state { points, allowance, updated }. Points never go down. */
function acceptPoints(state, claimed, nowMs) {
  const minutes = Math.max(0, (nowMs - state.updated) / 60000);
  let allowance = Math.min(LIMITS.burst, state.allowance + minutes * LIMITS.ratePerMinute);
  const delta = Math.max(0, Math.floor(claimed) - state.points);
  const add = Math.min(delta, Math.floor(allowance));
  allowance -= add;
  return { points: state.points + add, allowance, updated: nowMs, clipped: add < delta };
}

/** Small seeded random numbers (same generator as the web preview). */
function rng(seed) { let a = seed >>> 0; return () => { a = (a + 0x6D2B79F5) >>> 0; let t = a; t = Math.imul(t ^ (t >>> 15), t | 1); t ^= t + Math.imul(t ^ (t >>> 7), t | 61); return ((t ^ (t >>> 14)) >>> 0) / 4294967296; }; }

/** Computer rivals for a group's empty seats, fixed by the group's seed. */
function bots(seed, count, fraction) {
  const r = rng(seed), order = NAMES.map((_, i) => i);
  for (let i = order.length - 1; i > 0; i--) { const j = Math.floor(r() * (i + 1)); [order[i], order[j]] = [order[j], order[i]]; }
  const out = [];
  for (let i = 0; i < count; i++) {
    const target = Math.round(40 + Math.pow(r(), 1.5) * 760), speed = 0.6 + r() * 0.8;
    out.push({ names: NAMES[order[i % order.length]], points: Math.floor(target * Math.pow(fraction, speed)) });
  }
  return out;
}

/** Player names: 2-16 letters, digits, spaces, _ or -, and no word from the blocked list. */
function cleanName(name, blocked) {
  if (typeof name !== "string") return null;
  const n = name.normalize("NFC").replace(/\s+/g, " ").trim();
  if (n.length < 2 || n.length > 16 || !/^[\p{L}\p{N} _-]+$/u.test(n)) return null;
  const low = n.toLowerCase();
  if (blocked.some(w => w && low.includes(w))) return null;
  return n;
}

/** Sorted group rows. players: [{id, name, points}], meId: the asking player. Ties keep join order. */
function standings(players, meId, botSeed, fraction, lang) {
  const rows = players.map((p, i) => ({ name: p.name, points: p.points, me: p.id === meId, bot: false, order: i }));
  bots(botSeed, Math.max(0, GROUP_SIZE - players.length), fraction).forEach((b, i) =>
    rows.push({ name: b.names[lang === "fa" ? 1 : 0], points: b.points, me: false, bot: true, order: 1000 + i }));
  rows.sort((a, b) => b.points - a.points || (a.me ? -1 : b.me ? 1 : a.order - b.order));
  return rows.map(({ order, ...row }) => row);
}

module.exports = { GROUP_SIZE, LIMITS, dayOf, weekOf, weekEndMs, weekFraction, acceptPoints, bots, cleanName, standings };
