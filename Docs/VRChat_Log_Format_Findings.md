# VRChat Log Format Findings

These findings are based on local VRChat `output_log_*.txt` samples inspected on May 15, 2026. This document intentionally avoids real usernames, user IDs, room IDs, local paths, URLs with private IDs, or copied log payloads.

## Filename Shape

Observed VRChat logs use:

```text
output_log_YYYY-MM-DD_HH-MM-SS.txt
```

VLIT derives the log start timestamp from that filename. The file modification time is used as last activity.

## Entry Shape

Most entries follow this shape:

```text
YYYY.MM.DD HH:MM:SS Severity    -  Message
```

Observed severities include:

- `Debug`
- `Warning`
- `Error`

The parser accepts any non-whitespace severity token in the same position and treats unknown values as displayable severities.

## Multiline Entries

Continuation lines do not start with a VRChat timestamp. They are grouped into the preceding timestamped entry. This covers:

- managed stack traces
- environment or settings blocks
- indented exception details
- wrapped diagnostic payloads

In recent sample logs, multiline entries were common but a minority of parsed entries.

## Useful Filter Seeds

Good starting regex filters for VRChat development sessions:

```text
Udon|UdonSharp
Exception|Error
Warning
\[Behaviour\]
\[API\]
RPC|Network|NetworkTransport
StyleEngine
SteamVR|OpenXR
```

## Privacy Guidance

Raw VRChat logs can contain local filesystem paths, launch arguments, private room or world identifiers, account identifiers, usernames, API endpoints, request payload fragments, and device details. Do not commit raw logs, copied timeline exports, screenshots containing private logs, or sample files derived from private logs unless they have been deliberately anonymized.
