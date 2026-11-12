# Ready!

> Every good game starts with a sentence.

> [!IMPORTANT]
> **This project is made entirely by AI.**
> Requirements, UI design, implementation and documentation were all produced by an AI coding assistant; the human only set the direction, reviewed the results and made the decisions.
> The code is covered by automated tests and has been verified on real devices, but please treat it as an AI-generated independent project rather than a product with a maintenance team: there is no formal quality guarantee and no security audit. File an issue if something breaks; do not assume anyone is on call.

Ready! is a **local-first** ideation workbench for game creators. **Android is the primary target** (.NET 10 / .NET MAUI + Blazor Hybrid); Windows is served by a separate WinForms host that shares the exact same Blazor UI.

Fully offline: no network calls, no accounts, no telemetry. The Android manifest does not request `INTERNET`.

[![License: MIT](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)
[![CI](https://github.com/ChiEdking123/Ready/actions/workflows/ci.yml/badge.svg)](https://github.com/ChiEdking123/Ready/actions/workflows/ci.yml)

> The primary documentation is in Chinese ([README.md](README.md)). This file is a summary for English readers.

## The problem

When a game idea arrives, it is usually one sentence long. Most note-taking tools make you create a document, pick a title, choose a category first — by the time the structure is in place, the spark is gone.

Ready! opens straight into a text field. The one-sentence summary is the only required input; everything else is optional tags you can tap in seconds. Detailed design sections stay collapsed until you actually want to go deeper.

## Features

- **One-sentence summary + quick tags** — genre, perspective, art style, setting, characters. All multi-select and extensible with custom values.
- **Ordered characters** — listed in the order you pick them; the first one is the protagonist.
- **Details stay out of the way** — references, platform, audience, playtime, core loop, mood, input, scope, resources, non-goals and business model are collapsed by default.
- **Idea library** — search, pin, edit, duplicate, delete and sort. The search bar is the first thing on the page.
- **Local persistence** — JSON file with atomic writes, corrupt-file backup and import/export. Writes are serialized; a failed write rolls back memory and reports the failure honestly instead of claiming success.
- **Appearance** — light / dark / system, five accent palettes, five UI scale steps.
- **Accessible dialogs** — full keyboard operation (`Tab` cycles inside, `Esc` cancels); action buttons stack vertically on mobile with 40px+ touch targets.
- **Chinese IME safe** — inputs sync on blur, never on every keystroke, so composition and candidate windows are never disturbed.

## Download

See [Releases](https://github.com/ChiEdking123/Ready/releases).

- **Android** — `app.ready.gamedesign-*-Signed.apk`, arm64 only, Android 6.0 (API 23) and above.
- **Windows** — build from source for now.

## Build from source

```powershell
dotnet workload install maui-android          # Android only
dotnet build src/Ready.WinForms/Ready.WinForms.csproj   # Windows host
dotnet build src/Ready/Ready.csproj -c Release          # Android, Release
dotnet test  tests/Ready.Tests/Ready.Tests.csproj
```

Requirements: .NET 10 SDK (`global.json` pins 10.0.100), JDK 17 and Android SDK 36 for the Android target. The Windows host needs only WinForms + the WebView2 runtime.

## Architecture notes

```text
src/Ready/            # Android head (net10.0-android)
  Models/             # ideas, settings, tag catalog, export models
  Services/           # persistence, store, source-generated JSON context
  Components/         # shared Blazor UI (linked by the Windows head)
  Platforms/Android/  # native entry point, safe areas, system bar colours
  wwwroot/            # responsive styles and shortcuts
src/Ready.WinForms/   # Windows head (WinForms + WebView2 on HWND)
tests/Ready.Tests/    # compiles models and storage directly; no Android SDK needed
```

Two decisions worth calling out:

- **WinForms, not WPF/WinUI3, on Windows.** Of the three official Blazor hosts, only WinForms hosts WebView2 on a real HWND. The other two use visual hosting, where third-party IMEs cannot obtain a caret rectangle and never draw a candidate window.
- **`AndroidLinkMode=SdkOnly`, never `Full`.** Full saves ~1.5 MB but silently breaks the JS↔.NET bridge on .NET 10 MAUI Blazor Hybrid: the app launches, logs no errors, and never renders past the loading text.

More detail lives in `docs/` (Chinese).

## License

[MIT License](LICENSE). Third-party components: see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
