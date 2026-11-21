# Cargo Sort server (Rush Week leaderboard)

A small server that puts players into Rush Week groups of 30 and keeps their points. Empty seats in a
group are filled with computer rivals (marked `bot`), so a group always has 30 rows. The game works
without it: when the server can't be reached, the game shows its own computer rivals.

Plain Node.js 22.13 or newer, using Node's built-in SQLite. There are no packages to install.

## Run it on your computer

```
cd server
npm start          # http://localhost:8080/health -> {"ok":true}
npm test           # 6 tests
```

## Run it with Docker (for a cloud host)

```
docker build -t cargo-sort-server server
docker run -d --name cargo -p 8080:8080 -v cargo-data:/data --restart unless-stopped cargo-sort-server
```

The database is one file, `/data/cargo.db`, on the `cargo-data` volume. Back that file up.

Most Iranian cloud hosts (for example Liara or ArvanCloud) can run this Dockerfile directly. Put the
server behind HTTPS (the host usually gives you a domain with a certificate), set `TRUST_PROXY=1`
when it runs behind their proxy, and keep one instance (SQLite is a single file).

## Connect the game

- Unity: set `RushOnline.ServerUrl` in `Assets/Scripts/Game/RushOnline.cs` to your address, e.g.
  `https://cargo.example.ir`, and build.
- Web preview: open it with `?server=https://cargo.example.ir` at the end of the address.

## Settings

| Variable | Default | Meaning |
|---|---|---|
| `PORT` | 8080 | Port |
| `DB_PATH` | `./data/cargo.db` | Database file |
| `UTC_OFFSET` | 210 | Minutes from UTC; weeks start Saturday 00:00 at this offset (210 = Iran) |
| `BLOCKED_WORDS` | `./blocked-words.txt` | Words not allowed in player names, one per line |
| `TRUST_PROXY` | off | `1` behind a reverse proxy, to read the player's address from `X-Forwarded-For` |

## API

All answers are JSON. Every call except `/v1/join` needs the token that `/v1/join` returned.

| Call | Body or query | Answer |
|---|---|---|
| `POST /v1/join` | `{name, lang}` | `{token, name}` (a bad or empty name gets "Driver 1234") |
| `POST /v1/name` | `{token, name}` | `{name}`, or 400 `bad_name` |
| `POST /v1/score` | `{token, week, points, lang}` | the group (below); 409 if `week` is not this week |
| `GET /v1/group` | `?token=&lang=` | `{week, endsInSec, rank, points, rows: [{name, points, me, bot}]}` |
| `GET /v1/result` | `?token=&week=` | `{week, rank, points}` for a finished week (rank 0 = didn't play) |

## Cheating limits

Points come from the phone, so the server only accepts them at a believable speed: at most 200 at once,
refilled at 20 per minute (a win gives about 10–40 points). Points never go down. Each address can
create at most 30 players an hour. Prizes are paid by the game from the rank the server reports.

## Privacy

The server stores a random token, the player's name and their weekly points. It stores no device id,
account or contact details. Mention the name and points in the game's privacy policy.
