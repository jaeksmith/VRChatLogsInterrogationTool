# VLIT - VRChat Logs Interrogation Tool

VLIT is a Windows 11 desktop tool for interrogating VRChat client logs during multiplayer and UdonSharp development. It focuses on correlating multiple running clients, live log observation, regex filtering/search, marker-driven review, and checklist-style test workflows that are easy to paste into GenAI debugging sessions.

## Current Features

- Native WPF desktop app targeting .NET 8.
- Multiple VRChat log source directories with source tags, colors, availability state, persistence, and file watchers.
- Discovery of `output_log_*.txt` files with filename start time and filesystem last-activity time.
- Include/show controls, aliases, colors, multi-select delete with confirmation, and lifetime visualization.
- Merged chronological timeline across included logs.
- VRChat timestamp parsing with multiline continuation grouping.
- Severity badges for Debug, Warning, Error, Log, Marker, and fallback severities.
- Multiple independent regex filters with OR behavior and colored tokens.
- Regex search with first/previous/next/last navigation and search-to-filter conversion.
- Per-line checkboxes, drag selection, copy, hide, show-hidden toggle, and reviewed-up-to marker placement.
- Manual timeline markers and checklist-driven marker insertion.
- Right-side checklist runner for action steps, ordered/unordered groups, regex observations, and marker steps.
- User state stored under `%AppData%\VLIT\settings.json`, not in the repository.

## Build

```powershell
dotnet build VRChatLogsInterrogationTool.sln
```

## Run

```powershell
dotnet run --project src\VLIT\VLIT.csproj
```

The built executable is produced at:

```text
src\VLIT\bin\Debug\net8.0-windows\VLIT.exe
```

## Smoke Tests

Synthetic parser check:

```powershell
dotnet run --project tests\VLIT.SmokeTests\VLIT.SmokeTests.csproj
```

Optional local real-log check:

```powershell
dotnet run --project tests\VLIT.SmokeTests\VLIT.SmokeTests.csproj -- <VRChat log directory>
```

The smoke test reports counts only. Do not commit real VRChat logs or copied private log excerpts.

## Privacy Notes

This project is intended to be public. The app displays real log content because that is necessary for debugging, but the repo should not contain private VRChat logs, user IDs, usernames, launch arguments, local paths, room IDs, API payloads, or copied timeline exports. Runtime settings are local user data and live in AppData.

## Crash Logs

If VLIT hits an unhandled UI/runtime exception, it writes details to:

```text
%AppData%\VLIT\crash.log
```

## Docs

- [Original specification](Docs/VRChat_Logs_Interrogation_Tool_Specification.md)
- [Checklist/script format](Docs/Checklist_Script_Format.md)
- [Anonymized VRChat log findings](Docs/VRChat_Log_Format_Findings.md)
