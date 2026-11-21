"use strict";
const test = require("node:test");
const assert = require("node:assert");
const http = require("node:http");
const { createApp, openDb } = require("../src/server");
const rush = require("../src/rush");

// A server on a random port with an in-memory database and a clock the test can move.
async function start(joinLimit = 1000) {
  const clock = { t: Date.UTC(2026, 10, 21, 10, 0, 0) }; // a Saturday
  const app = createApp({ db: openDb(":memory:"), now: () => clock.t, blocked: ["badword"], joinLimit });
  const server = http.createServer(app);
  await new Promise(r => server.listen(0, r));
  const base = `http://127.0.0.1:${server.address().port}`;
  const call = async (method, path, body) => {
    const res = await fetch(base + path, { method, headers: { "Content-Type": "application/json" }, body: body && JSON.stringify(body) });
    return { status: res.status, body: await res.json() };
  };
  return { clock, call, close: () => server.close() };
}

test("weeks start on Saturday in Iran time", () => {
  const sat = Date.UTC(2026, 10, 20, 20, 30); // Friday 20:30 UTC = Saturday 00:00 in Iran
  assert.strictEqual(rush.weekOf(sat, 210), rush.weekOf(sat - 1, 210) + 1);
  assert.strictEqual(rush.weekEndMs(rush.weekOf(sat, 210), 210), sat + 7 * 86400000);
});

test("score limit: points never go down and big jumps are clipped", () => {
  let s = { points: 0, allowance: 200, updated: 0 };
  s = rush.acceptPoints(s, 50, 0); assert.strictEqual(s.points, 50);
  s = rush.acceptPoints(s, 30, 1000); assert.strictEqual(s.points, 50);
  s = rush.acceptPoints(s, 100000, 2000); assert.ok(s.clipped); assert.ok(s.points <= 250);
  const later = rush.acceptPoints(s, 100000, 2000 + 10 * 60000); // 10 minutes refill 200
  assert.ok(later.points > s.points && later.points <= s.points + 200);
});

test("names are cleaned", () => {
  assert.strictEqual(rush.cleanName("  Sara  K ", []), "Sara K");
  assert.strictEqual(rush.cleanName("سارا", []), "سارا");
  assert.strictEqual(rush.cleanName("a", []), null);
  assert.strictEqual(rush.cleanName("<script>", []), null);
  assert.strictEqual(rush.cleanName("BadWord99", ["badword"]), null);
});

test("join, score, group of 30 with bots, result after the week", async () => {
  const s = await start();
  try {
    const a = (await s.call("POST", "/v1/join", { name: "Sara", lang: "en" })).body;
    const b = (await s.call("POST", "/v1/join", { name: "badword", lang: "fa" })).body;
    assert.match(b.name, /^راننده \d{4}$/);
    const week = rush.weekOf(s.clock.t, 210);
    let g = (await s.call("POST", "/v1/score", { token: a.token, week, points: 40, lang: "en" })).body;
    assert.strictEqual(g.rows.length, 30);
    assert.strictEqual(g.points, 40);
    assert.strictEqual(g.rows.filter(r => !r.bot).length, 1);
    g = (await s.call("POST", "/v1/score", { token: b.token, week, points: 60 })).body;
    assert.strictEqual(g.rows.filter(r => !r.bot).length, 2); // same group
    assert.ok(g.rows.findIndex(r => r.me) < g.rows.findIndex(r => r.name === "Sara"));
    assert.strictEqual((await s.call("POST", "/v1/score", { token: a.token, week: week - 1, points: 5 })).status, 409);
    assert.strictEqual((await s.call("GET", "/v1/group?token=nope")).status, 401);
    assert.strictEqual((await s.call("GET", `/v1/result?token=${a.token}&week=${week}`)).status, 400);
    s.clock.t += 7 * 86400000;
    const r = (await s.call("GET", `/v1/result?token=${a.token}&week=${week}`)).body;
    assert.ok(r.rank >= 1 && r.rank <= 30 && r.points === 40);
    const fresh = (await s.call("GET", `/v1/group?token=${a.token}`)).body;
    assert.strictEqual(fresh.week, week + 1);
    assert.strictEqual(fresh.points, 0);
  } finally { s.close(); }
});

test("too many joins from one address are refused", async () => {
  const s = await start(2);
  try {
    assert.strictEqual((await s.call("POST", "/v1/join", {})).status, 200);
    assert.strictEqual((await s.call("POST", "/v1/join", {})).status, 200);
    assert.strictEqual((await s.call("POST", "/v1/join", {})).status, 429);
  } finally { s.close(); }
});

test("a full group of 30 makes a new group; names can change", async () => {
  const s = await start();
  try {
    const week = rush.weekOf(s.clock.t, 210), tokens = [];
    for (let i = 0; i < 31; i++) tokens.push((await s.call("POST", "/v1/join", { name: "P" + i })).body.token);
    for (const t of tokens) await s.call("POST", "/v1/score", { token: t, week, points: 1 });
    const last = (await s.call("GET", `/v1/group?token=${tokens[30]}`)).body;
    assert.strictEqual(last.rows.filter(r => !r.bot).length, 1);
    const first = (await s.call("GET", `/v1/group?token=${tokens[0]}`)).body;
    assert.strictEqual(first.rows.filter(r => !r.bot).length, 30);
    assert.strictEqual((await s.call("POST", "/v1/name", { token: tokens[0], name: "x" })).status, 400);
    assert.strictEqual((await s.call("POST", "/v1/name", { token: tokens[0], name: "Kian" })).body.name, "Kian");
  } finally { s.close(); }
});
