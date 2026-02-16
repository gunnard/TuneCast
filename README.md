# TuneCast

> **Adaptive playback intelligence plugin for [Jellyfin](https://jellyfin.org).** Learns your clients, shapes device profiles, and steers playback toward direct play — reducing unnecessary transcoding without breaking anything.

> [!NOTE]
> **This is a third-party plugin.** It is not affiliated with or endorsed by the Jellyfin project.

---

## What does this plugin do?

Jellyfin decides how to play media for each client (direct play, direct stream, or transcode). It makes good decisions most of the time, but it doesn't learn from experience, and its device profiles are static.

**TuneCast sits between client sessions and Jellyfin's playback engine.** It:

1. **Identifies each client** and tracks what codecs/containers it can actually handle
2. **Builds confidence scores** for each client+codec pair based on real playback outcomes
3. **Computes a playback policy** (allow direct play? force transcode? cap bitrate?) using rules + confidence data
4. **Shapes the device profile** so Jellyfin's native engine makes the right call
5. **Records what happened** and feeds outcomes back into the confidence model

The result: clients that *can* direct play *do* direct play. Clients that can't get clean, predictable transcodes. Your server does less unnecessary work.
<img width="1101" height="683" alt="image" src="https://github.com/user-attachments/assets/8de1133b-02f8-4c00-a388-ef1271afd5d0" />

### What it does NOT do

- It does **not** replace Jellyfin's playback engine — it feeds it better data
- It does **not** require any client-side changes
- It does **not** phone home or collect any data outside your server
- It does **not** modify your media files

---

## Install

### Option 1: Plugin Repository (recommended)

Add the TuneCast repository to Jellyfin — the plugin will appear in your catalog for one-click install and automatic updates.

1. Go to **Admin Dashboard → Plugins → Repositories**
2. Click **Add** and paste this URL:
   ```
   https://raw.githubusercontent.com/gunnard/TuneCast/main/manifest.json
   ```
3. Go to **Catalog → General → TuneCast → Install**
4. **Restart Jellyfin**

### Option 2: Manual Install

1. Download `TuneCast.zip` from the [latest release](https://github.com/gunnard/TuneCast/releases/latest)
2. Extract into your Jellyfin plugins directory:

   | Platform | Path |
   |----------|------|
   | **Linux** | `~/.local/share/jellyfin/data/plugins/TuneCast_1.0.0.0/` |
   | **Docker** | `/config/data/plugins/TuneCast_1.0.0.0/` |
   | **Windows** | `%APPDATA%\jellyfin\data\plugins\TuneCast_1.0.0.0\` |

3. **Restart Jellyfin**

### Option 3: Build from Source

Requires **.NET SDK 9.0** and **Jellyfin Server 10.11.x+**.

```bash
git clone https://github.com/gunnard/TuneCast.git
cd TuneCast
dotnet build src/TuneCast/TuneCast.csproj -c Release
```

Output: `src/TuneCast/bin/Release/net9.0/TuneCast.dll` — copy it and `LiteDB.dll` into your plugins directory and restart Jellyfin.

### Run Tests

```bash
dotnet test
```

155+ unit tests covering the decision engine, rules, profile builder, learning system, and intelligence services.

---

## How It Works

```
Client connects
    │
    ▼
┌─────────────────────┐
│  Client Intelligence │  ← Identifies device, resolves capabilities
└─────────┬───────────┘
          │
          ▼
┌─────────────────────┐
│   Media Intelligence │  ← Analyzes media: codecs, bitrate, HDR, etc.
└─────────┬───────────┘
          │
          ▼
┌─────────────────────┐
│   Decision Engine    │  ← Runs rules + confidence → PlaybackPolicy
│                     │     (direct play? transcode? bitrate cap?)
└─────────┬───────────┘
          │
          ▼
┌─────────────────────┐
│   Profile Manager    │  ← Shapes DeviceProfile for Jellyfin
│                     │     Sends toast notification to client
│                     │     Records intervention to DB
└─────────┬───────────┘
          │
          ▼
┌─────────────────────┐
│   Jellyfin Engine    │  ← Makes final playback decision using
│   (native)          │     the shaped profile
└─────────┬───────────┘
          │
          ▼
┌─────────────────────┐
│   Telemetry          │  ← Records outcome (success/failure/method)
└─────────┬───────────┘
          │
          ▼
┌─────────────────────┐
│   Learning System    │  ← Adjusts confidence scores based on
│                     │     what actually happened
└─────────────────────┘
```

### Decision Rules

The decision engine evaluates a pipeline of rules for each playback request:

| Rule | What it checks |
|------|----------------|
| **ContainerCodecCompatibility** | Can the client handle this container+codec combo? |
| **AudioPassthrough** | Can audio be passed through or does it need transcoding? |
| **BitrateCapRule** | Should bitrate be capped based on client/server constraints? |
| **BitDepthCompatibility** | Can the client handle 10-bit/12-bit video? |
| **HdrCompatibility** | Does the client support HDR10/Dolby Vision/HLG? |

Each rule contributes to the final `PlaybackPolicy`, which includes:
- `AllowDirectPlay` / `AllowDirectStream` / `AllowTranscoding` flags
- `BitrateCap` (optional)
- `Confidence` score (0.0–1.0)
- `Reasoning` (human-readable explanation of why)

---

## Configuration

Access via **Admin Dashboard → Plugins → TuneCast**.

| Setting | Default | Description |
|---------|---------|-------------|
| **Conservative Mode** | ON | Defer to Jellyfin defaults on uncertain decisions. Recommended while the plugin builds confidence. |
| **Enable Dynamic Profiles** | OFF | When ON, the plugin actively shapes device profiles. When OFF, it only observes and logs what it *would* do (dry run mode). |
| **Enable Learning** | OFF | When ON, playback outcomes adjust client confidence scores automatically. |
| **Server Load Bias** | 0.3 | How much server load influences decisions (0.0 = ignore, 1.0 = heavily weight). |
| **Global Max Bitrate** | — | Optional server-wide bitrate cap in Mbps. Leave empty to defer to client/server defaults. |
| **Verbose Logging** | OFF | Enables detailed decision reasoning in Jellyfin logs and on-screen toast notifications during playback. |
| **Telemetry Retention** | 90 days | How long playback records are kept before automatic pruning. |

### Recommended Setup Path

1. **Install and leave defaults** — the plugin observes all playback sessions and builds confidence
2. **After a few days**, check the Dashboard to see client confidence scores and playback stats
3. **Enable Learning** — confidence scores begin self-adjusting from real outcomes
4. **Enable Dynamic Profiles** — the plugin starts actively shaping device profiles
5. **Disable Conservative Mode** (optional) — allows the plugin to be more aggressive on confident decisions

---

## Dashboard

The plugin includes a built-in admin dashboard accessible from the config page (or directly at `configurationpage?name=TuneCast%20Dashboard`).

### What the dashboard shows

- **Server Stats** — total clients, direct play rate, failure count (7-day window)
- **Plugin Config** — current settings at a glance
- **Active Sessions** — live playback sessions with play method
- **Client Cards** — each known client with codec/container confidence bars and SVG device icons
- **Telemetry Table** — recent playback events with method, result, duration, and transcode reasons
- **Intervention Log** — every time TuneCast influenced (or would have influenced) a playback decision, with ACTIVE/DRY RUN status, policy flags, confidence, and reasoning

### On-Screen Toast Notifications

When **Verbose Logging** is enabled and the plugin actively shapes a profile, a brief popup appears on the playing client:

> **TuneCast** — Optimized for Direct Play (confidence: 85%)

This uses Jellyfin's native `DisplayMessage` command and works on supported clients (web, Android, iOS). Auto-dismisses after 5 seconds.

---

## How to Tell If TuneCast Is Working

### In Jellyfin Logs

Search for `TuneCast` in Admin → Logs. Key entries:

| Log Tag | Meaning |
|---------|---------|
| `[ACTIVE]` | A device profile was shaped and applied — TuneCast changed playback behavior |
| `[DRY RUN]` | TuneCast *would have* changed behavior, but `EnableDynamicProfiles` is OFF |
| `Policy is default pass-through` | TuneCast evaluated the session but decided no intervention was needed |

### On the Dashboard

The **Intervention Log** section shows every policy decision with full reasoning. If the list is empty, TuneCast hasn't needed to intervene (Jellyfin's defaults were already correct for your clients).

---

## Project Structure

```
src/TuneCast/
├── Api/                    # REST API controller (dashboard data)
├── Configuration/          # Plugin settings + admin UI pages
├── Decision/               # Policy engine + rule pipeline
│   └── Rules/              # Individual playback rules
├── Intelligence/           # Client + media identification
├── Learning/               # Confidence adjustment from outcomes
├── Models/                 # Domain models (ClientModel, PlaybackPolicy, etc.)
├── Profiles/               # Device profile building + shaping
├── Storage/                # LiteDB persistent data store
├── Telemetry/              # Playback event handling + outcome recording
├── Plugin.cs               # Plugin entry point
└── PluginServiceRegistrator.cs  # DI registration

tests/TuneCast.Tests/
├── Decision/               # Decision engine + rule tests
├── Intelligence/           # Client/media resolution tests
├── Learning/               # Learning system tests
└── Profiles/               # Profile builder tests
```

## Data Storage

All plugin data is stored in:

```
{JellyfinConfigPath}/plugins/configurations/TuneCast/tunecast.db
```

This is a [LiteDB](https://www.litedb.org/) database containing:
- **Client models** — device capabilities and codec confidence scores
- **Playback outcomes** — telemetry from every session
- **Intervention records** — policy decisions and reasoning

Data is **never** stored alongside your media files. Telemetry is automatically pruned based on the retention setting.

---

## FAQ

**Q: Will this break my existing playback?**
A: No. Out of the box, the plugin only observes. It doesn't change anything until you explicitly enable Dynamic Profiles. Even then, Conservative Mode ensures it defers to Jellyfin on any uncertain decision.

**Q: Does this work with hardware transcoding?**
A: Yes. TuneCast doesn't manage transcoding itself — it tells Jellyfin whether transcoding is needed. Your existing hardware transcoding setup (VAAPI, QSV, NVENC) works exactly as before.

**Q: What clients are supported?**
A: All Jellyfin clients. The plugin identifies clients by their session metadata (app name, device ID, capabilities) and builds per-client intelligence over time.

**Q: How much overhead does this add?**
A: Negligible. The decision engine runs once per playback start (a few milliseconds of CPU). The LiteDB database is typically a few MB. There is no background polling.

**Q: Can I use this on a Raspberry Pi?**
A: Yes. Tested on Raspberry Pi 4 running Jellyfin in Docker (arm64).

**Q: What happens if I uninstall the plugin?**
A: Jellyfin reverts to its default playback behavior. Shaped device profiles are cleared on restart. The LiteDB database file remains in the config directory until you manually delete it.

---

## Tech Stack

| Component | Version |
|-----------|---------|
| Target Framework | .NET 9.0 |
| Jellyfin API | 10.11.3 (`Jellyfin.Controller` + `Jellyfin.Model`) |
| Persistent Storage | LiteDB 5.0.21 |
| Test Framework | xUnit + Moq |

## License

**GPLv3** — consistent with Jellyfin's licensing.
