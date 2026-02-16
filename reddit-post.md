# Title:
I built a plugin that learns your clients and reduces unnecessary transcoding — TuneCast

# Post:

Hey everyone,

I've been running Jellyfin on a Raspberry Pi 4 for a while now, and one thing that's always bugged me is how often playback falls back to transcoding when the client can clearly handle direct play. The stock device profiles are static — they don't adapt, and they don't learn from what actually works.

So I built **TuneCast** — a plugin that watches your playback sessions, builds per-client confidence scores for codecs and containers, and then shapes device profiles so Jellyfin's own engine makes better decisions.

**GitHub:** https://github.com/gunnard/TuneCast

## What it does

- Tracks what each client can actually play (not just what its static profile says)
- Builds confidence scores for every client+codec pair based on real playback outcomes
- Shapes device profiles at runtime so Jellyfin steers toward direct play when it's safe
- Falls back gracefully — it never blocks playback, just nudges Jellyfin in the right direction
- Includes a built-in dashboard showing client stats, confidence bars, telemetry, and an intervention log

## What it doesn't do

- It doesn't replace Jellyfin's playback engine — it just feeds it better data
- It doesn't require any client-side changes
- It doesn't phone home or collect anything outside your server

## How it works (the short version)

1. You install the plugin and leave it in observe-only mode (default)
2. It watches every playback session and records what happened — direct play, transcode, success, failure
3. Over time it builds a picture of what each client can handle
4. When you're ready, flip on "Enable Dynamic Profiles" and it starts actively shaping device profiles
5. Playback outcomes feed back into the model, so confidence gets better over time

There's also a conservative mode (on by default) that defers to Jellyfin's defaults on anything uncertain. The idea is you can leave it running for a few days to build confidence before it starts making active decisions.

## Install

Add this repository URL in **Admin → Plugins → Repositories → Add**:

```
https://raw.githubusercontent.com/gunnard/TuneCast/main/manifest.json
```

Then install from the catalog. That's it.

## The nerdy details

- Written in C# targeting .NET 9.0 / Jellyfin 10.11.x
- Uses LiteDB for local storage (zero-config, single file)
- Decision engine runs a pipeline of rules: container/codec compatibility, audio passthrough, bitrate caps, bit depth, HDR support
- Each rule contributes to a PlaybackPolicy with allow/deny flags, bitrate cap, confidence score, and human-readable reasoning
- 155+ unit tests
- GPLv3 licensed

I've been running this on my Pi for a couple weeks and it's been solid. My Android TV client went from transcoding about 40% of the time to almost always direct playing. The dashboard is genuinely useful for understanding *why* Jellyfin is making the choices it makes, even if you never turn on active profile shaping.

This is my first Jellyfin plugin so I'd really appreciate any feedback — especially around edge cases with specific clients. If you run into issues, open an issue on GitHub and I'll take a look.

Cheers!
