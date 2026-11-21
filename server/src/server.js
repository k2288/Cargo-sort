// Cargo Sort server: Rush Week groups and leaderboard.
// Plain Node.js (22.13+) with its built-in SQLite; no packages to install.
//
// Settings (environment variables):
//   PORT          port to listen on (default 8080)
//   DB_PATH       SQLite file (default ./data/cargo.db; use a mounted volume in Docker)
//   UTC_OFFSET    minutes from UTC for the week start (default 210 = Iran, Saturday 00:00)
//   BLOCKED_WORDS path to a file of blocked name words, one per line (default ./blocked-words.txt)
//   TRUST_PROXY   set to 1 behind a reverse proxy, to read the player's IP from X-Forwarded-For
//
// API (JSON). Every call except /join needs the player's token (from /join).
//   POST /v1/join   {name, lang}            -> {token, name}
//   POST /v1/name   {token, name}           -> {name}
//   POST /v1/score  {token, week, points, lang} -> group (below)
//   GET  /v1/group?token=..&lang=en|fa      -> group: {week, endsInSec, rank, points, rows:[{name, points, me, bot}]}
//   GET  /v1/result?token=..&week=N         -> {week, rank, points} for a finished week (rank 0 = not in a group)
//   GET  /health                            -> {ok: true}
"use strict";

const http = require("node:http");
const fs = require("node:fs");
const path = require("node:path");
const crypto = require("node:crypto");
const { DatabaseSync } = require("node:sqlite");
const rush = require("./rush");

const PORT = +(process.env.PORT || 8080);
const DB_PATH = process.env.DB_PATH || path.join(__dirname, "..", "data", "cargo.db");
const UTC_OFFSET = +(process.env.UTC_OFFSET ?? 210);
const BLOCKED_PATH = process.env.BLOCKED_WORDS || path.join(__dirname, "..", "blocked-words.txt");
const NEW_GROUP_AFTER_MS = 24 * 3600 * 1000; // players who join a day apart go to different groups
const JOINS_PER_IP_HOUR = 30;                  // limits fake accounts from one address

function openDb(file) {
  if (file !== ":memory:") fs.mkdirSync(path.dirname(file), { recursive: true });
  const db = new DatabaseSync(file);
  db.exec(`
    PRAGMA journal_mode = WAL;
    CREATE TABLE IF NOT EXISTS players (id INTEGER PRIMARY KEY, token TEXT UNIQUE NOT NULL, name TEXT NOT NULL, created INTEGER NOT NULL);
    CREATE TABLE IF NOT EXISTS groups (id INTEGER PRIMARY KEY, week INTEGER NOT NULL, created INTEGER NOT NULL, seed INTEGER NOT NULL);
    CREATE TABLE IF NOT EXISTS members (
      week INTEGER NOT NULL, player_id INTEGER NOT NULL, group_id INTEGER NOT NULL,
      points INTEGER NOT NULL DEFAULT 0, allowance REAL NOT NULL, updated INTEGER NOT NULL, joined INTEGER NOT NULL,
      PRIMARY KEY (week, player_id));
    CREATE INDEX IF NOT EXISTS members_group ON members (group_id);
    CREATE INDEX IF NOT EXISTS groups_week ON groups (week, created);
  `);
  return db;
}

function loadBlocked(file) {
  try { return fs.readFileSync(file, "utf8").split(/\r?\n/).map(s => s.trim().toLowerCase()).filter(s => s && !s.startsWith("#")); }
  catch { return []; }
}

/** The app: request handler plus helpers, so tests can run it on an in-memory database with a fake clock. */
function createApp({ db, now = () => Date.now(), offset = UTC_OFFSET, blocked = [], trustProxy = process.env.TRUST_PROXY === "1", joinLimit = JOINS_PER_IP_HOUR }) {
  const joins = new Map(); // ip -> [times]
  const q = {
    playerByToken: db.prepare("SELECT id, name FROM players WHERE token = ?"),
    addPlayer: db.prepare("INSERT INTO players (token, name, created) VALUES (?, ?, ?)"),
    setName: db.prepare("UPDATE players SET name = ? WHERE id = ?"),
    member: db.prepare("SELECT * FROM members WHERE week = ? AND player_id = ?"),
    openGroup: db.prepare(`SELECT g.id FROM groups g WHERE g.week = ? AND g.created > ?
      AND (SELECT COUNT(*) FROM members m WHERE m.group_id = g.id) < ? ORDER BY g.created DESC LIMIT 1`),
    addGroup: db.prepare("INSERT INTO groups (week, created, seed) VALUES (?, ?, ?)"),
    addMember: db.prepare("INSERT INTO members (week, player_id, group_id, points, allowance, updated, joined) VALUES (?, ?, ?, 0, ?, ?, ?)"),
    setPoints: db.prepare("UPDATE members SET points = ?, allowance = ?, updated = ? WHERE week = ? AND player_id = ?"),
    group: db.prepare("SELECT seed FROM groups WHERE id = ?"),
    groupPlayers: db.prepare(`SELECT p.id, p.name, m.points FROM members m JOIN players p ON p.id = m.player_id
      WHERE m.group_id = ? ORDER BY m.joined, p.id`),
  };

  /** The player's membership for a week, joining a group (newest open one, else a new one) if needed. */
  function membership(playerId, week) {
    let m = q.member.get(week, playerId);
    if (m) return m;
    const t = now();
    let g = q.openGroup.get(week, t - NEW_GROUP_AFTER_MS, rush.GROUP_SIZE);
    const groupId = g ? g.id : Number(q.addGroup.run(week, t, crypto.randomInt(1, 2 ** 31 - 1)).lastInsertRowid);
    q.addMember.run(week, playerId, groupId, rush.LIMITS.burst, t, t);
    return q.member.get(week, playerId);
  }

  function groupView(player, week, lang) {
    const m = membership(player.id, week);
    const seed = q.group.get(m.group_id).seed;
    const fraction = rush.weekFraction(now(), week, offset);
    const rows = rush.standings(q.groupPlayers.all(m.group_id), player.id, seed, fraction, lang);
    return {
      week, endsInSec: Math.max(0, Math.round((rush.weekEndMs(week, offset) - now()) / 1000)),
      rank: rows.findIndex(r => r.me) + 1, points: m.points, rows,
    };
  }

  const routes = {
    "POST /v1/join": body => {
      const name = rush.cleanName(body.name, blocked) || (body.lang === "fa" ? "راننده" : "Driver") + " " + crypto.randomInt(1000, 9999);
      const token = crypto.randomBytes(24).toString("base64url");
      q.addPlayer.run(token, name, now());
      return { token, name };
    },
    "POST /v1/name": (body, player) => {
      const name = rush.cleanName(body.name, blocked);
      if (!name) return [400, { error: "bad_name" }];
      q.setName.run(name, player.id);
      return { name };
    },
    "POST /v1/score": (body, player) => {
      const week = rush.weekOf(now(), offset);
      if (body.week !== week) return [409, { error: "wrong_week", week }]; // points belong to another week
      const m = membership(player.id, week);
      const next = rush.acceptPoints({ points: m.points, allowance: m.allowance, updated: m.updated }, +body.points || 0, now());
      q.setPoints.run(next.points, next.allowance, next.updated, week, player.id);
      return groupView(player, week, body.lang);
    },
    "GET /v1/group": (query, player) => groupView(player, rush.weekOf(now(), offset), query.lang),
    "GET /v1/result": (query, player) => {
      const week = +query.week;
      if (!Number.isInteger(week) || week >= rush.weekOf(now(), offset)) return [400, { error: "week_not_finished" }];
      const m = q.member.get(week, player.id);
      if (!m) return { week, rank: 0, points: 0 };
      const seed = q.group.get(m.group_id).seed;
      const rows = rush.standings(q.groupPlayers.all(m.group_id), player.id, seed, 1, "en");
      return { week, rank: rows.findIndex(r => r.me) + 1, points: m.points };
    },
    "GET /health": () => ({ ok: true }),
  };

  return async function handle(req, res) {
    const headers = { "Content-Type": "application/json; charset=utf-8", "Access-Control-Allow-Origin": "*",
      "Access-Control-Allow-Headers": "Content-Type", "Access-Control-Allow-Methods": "GET, POST, OPTIONS", "Cache-Control": "no-store" };
    const send = (status, obj) => { res.writeHead(status, headers); res.end(JSON.stringify(obj)); };
    if (req.method === "OPTIONS") return send(204, {});
    const url = new URL(req.url, "http://x");
    const route = routes[`${req.method} ${url.pathname}`];
    if (!route) return send(404, { error: "not_found" });
    if (url.pathname === "/v1/join") {
      const ip = (trustProxy && String(req.headers["x-forwarded-for"] || "").split(",")[0].trim()) || req.socket.remoteAddress || "?";
      const recent = (joins.get(ip) || []).filter(t => now() - t < 3600 * 1000);
      if (recent.length >= joinLimit) return send(429, { error: "too_many" });
      recent.push(now()); joins.set(ip, recent);
      if (joins.size > 50000) joins.clear();
    }
    let input = Object.fromEntries(url.searchParams);
    if (req.method === "POST") {
      let raw = "";
      for await (const chunk of req) { raw += chunk; if (raw.length > 4096) return send(413, { error: "too_large" }); }
      try { input = JSON.parse(raw || "{}"); } catch { return send(400, { error: "bad_json" }); }
    }
    let player = null;
    if (url.pathname !== "/v1/join" && url.pathname !== "/health") {
      player = typeof input.token === "string" ? q.playerByToken.get(input.token) : null;
      if (!player) return send(401, { error: "bad_token" });
    }
    try {
      const out = route(input, player);
      return Array.isArray(out) ? send(out[0], out[1]) : send(200, out);
    } catch (e) {
      console.error(e);
      return send(500, { error: "server_error" });
    }
  };
}

if (require.main === module) {
  const app = createApp({ db: openDb(DB_PATH), blocked: loadBlocked(BLOCKED_PATH) });
  http.createServer(app).listen(PORT, () => console.log(`Cargo Sort server on :${PORT}, database ${DB_PATH}`));
}

module.exports = { createApp, openDb };
