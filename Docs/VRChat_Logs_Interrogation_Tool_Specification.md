# VRChat Logs Interrogation Tool (VLIT)

## Project Overview

**Project Name:** VRChat Logs Interrogation Tool  
**Abbreviation:** VLIT

The VRChat Logs Interrogation Tool is a Windows 11 GUI application intended primarily for VRChat multiplayer/UdonSharp development workflows. The tool focuses on:

- Multi-client log viewing
- Multi-machine log aggregation
- Real-time/live log observation
- Intelligent filtering
- Correlated timeline viewing
- Test/checklist execution support
- GenAI-assisted debugging workflows
- Fast extraction/copying of useful debugging information
- Human annotations and markers
- Structured log interrogation and review

This project is intended to be public on GitHub and should include:
- A good/intelligent project name
- README.md
- A documented specification for the checklist/script format
- Clean repository structure

---

# Core Goals

## Primary Use Cases

### Multiplayer VRChat Development
VRChat creates separate logs per client instance. The tool should support simultaneously viewing and correlating multiple logs from multiple running clients.

### Live Debugging
The tool is intended to be used while VRChat is actively running.

### GenAI-Assisted Development
GenAI systems frequently instruct the user to:
- run tests
- perform actions
- observe logs
- verify sequences

The tool should support structured workflows for these activities.

---

# Platform

## Operating System
- Windows 11

## Application Type
- Native GUI desktop application

---

# Log Sources

## Multiple Log Directories

The application must support one or more VRChat log directories.

Examples:
- Local VRChat logs
- Remote machine logs
- Network share logs
- Multiple computers simultaneously

Each source directory effectively represents a:
- VRChat run directory

---

# Source Directory Management

## Source Directory List

The UI should support adding and removing log source directories.

Each source entry should include:

- Source token/tag (example: S1, S2, S3)
- Source color
- Editable directory path field
- Directory browse/select button
- Remove button

## Source Directory Persistence

Source directories should persist across application restarts.

## Missing/Unavailable Sources

If a source is unavailable:
- keep the entry
- indicate unavailable/offline state visually
- do not remove automatically

## Removing Sources

Removing a source directory should:
- require confirmation
- ideally remove associated cached information

---

# Log File Discovery

## VRChat Log Files

VRChat log files contain:
- embedded timestamps in filenames
- filesystem modification timestamps

## Log File Metadata

The system should extract:

### Start Timestamp
Derived from:
- filename

### Last Activity Timestamp
Derived from:
- file modification timestamp

---

# Live Monitoring

## Real-Time Operation

The application must support live monitoring of logs.

## Preferred Approach

Prefer:
- Windows file/directory change notification APIs

Avoid pure polling if possible.

## Polling Fallback

If polling is required:

### Adaptive Polling
- Poll active files more frequently
- Poll inactive files less frequently
- Poll directories loosely

### Dynamic Backoff
- Increase polling rate for active logs
- Back off for inactive logs/directories

## Manual Refresh

The UI should support:
- refresh button(s)

Potentially:
- one global refresh button

Refresh should:
- re-scan directories
- re-check files
- restore watchers if needed

---

# Log File List

## Purpose

Display discovered VRChat log files.

## Sorting

Files should be sorted:
- newest to oldest by creation/start timestamp

## File Controls

Each log file entry should support:

### Include Checkbox
Controls whether:
- file participates in merged timeline

### Show/Hide Checkbox
Controls visibility of lines from included files.

Behavior:
- disabled if file not included
- visible by default if included

## File Alias / Client Label

Each file should support:
- editable alias/tag

Examples:
- Client 1
- Client 2
- Quest
- Host
- Desktop

## File Colors

Each file should support:
- assigned color

Purpose:
- identify log source visually in merged view

Defaults:
- auto-assigned
- attempt uniqueness
- especially among recent files

## File Deletion

The log file list should support:
- deleting log files

Operations:
- single delete
- multi-select delete

Deletion should:
- require confirmation

---

# Log Lifetime Visualization

## Purpose

Visualize:
- overlap duration
- concurrent execution
- active sessions

## Visualization Concept

Each file should display a graphical lifetime trace.

Suggested design:
- L-shaped or bent traces
- horizontal spike outward
- vertical line upward

This shows:
- how long files existed
- overlap between logs
- active sessions

## Scalability

The visualization area may need:
- expandable width
- spacing between traces

Example:
- many overlapping files may require large width allocation

---

# Main Log View

## Unified Timeline

The primary display should show:
- interleaved log lines
- merged across files
- sorted by timestamp

## Ordering

Display order:
- oldest → newest

New entries:
- appended at bottom

## Auto-Scroll Modes

Support:

### Off
Never auto-scroll.

### Always On
Always follow newest entries.

### Follow If At Bottom
Only auto-scroll if user was already at bottom.

Default:
- Follow If At Bottom

---

# Log Parsing

## Timestamp Parsing

Each log entry should contain:
- parsed timestamp

## Multi-Line Entries

Need support for:
- grouped multi-line entries
- stack traces
- continuation lines

Implementation should inspect actual VRChat logs to determine:
- grouping rules
- multiline structure

## Multi-Line UI

Potential behavior:
- collapsed by default
- expandable/collapsible
- disclosure arrows
- detail panel
- double-click view

---

# Line Appearance

## Source Coloring

Entire lines may be colored based on:
- originating file/client

## Entry Separation

Need visual separators between entries.

Potential implementations:
- subtle divider
- single-pixel separator
- grouped containers

## Severity Indicators

Investigate VRChat log formatting for:
- warning
- error
- info
- debug

Potential UI:
- icons
- colored indicators
- red/yellow/gray/green markers

---

# Filters

## Regex Filters

Support multiple regex-based filters.

Each filter contains:
- regex pattern
- name
- color
- enabled checkbox

Defaults:
- Filter 1
- Filter 2
- Filter 3

## Filter Matching

Filters act independently.

Semantics:
- OR behavior

If a line matches:
- attach filter token/badge

## Multiple Matches

Lines may display:
- multiple tokens if multiple filters match

## Filter Tokens

Displayed tokens should include:
- filter name
- filter color

---

# Filter Visibility Semantics

## Show Unfiltered Lines

Separate toggle/filter:
- show lines that did not match filters

Behavior:
- when filters exist, unfiltered lines hidden by default

Enabling:
- restores unmatched lines

## Hidden Lines

Users should be able to:
- right-click lines
- hide selected lines

Separate visibility toggle:
- show hidden lines

Default:
- hidden lines remain hidden

## Selected Lines Filter

Need ability to:
- show/hide selected lines

---

# Search

## Regex Search

Provide regex-based search field.

## Navigation Buttons

Buttons:
- First
- Previous
- Next
- Last

## Enter Key Behavior

Pressing Enter:
- navigates to first match

## Convert Search To Filter

Provide button to:
- convert current search into a permanent filter

Resulting filter should:
- get token/color/name

---

# Line Selection

## Per-Line Checkbox

Each line should support:
- selection checkbox

## Drag Selection

Support:
- dragging across lines to select

## Selection Operations

Right-click operations on selected lines:

### Copy
Copy selected content to clipboard.

Copied data should include:
- source tags
- filter tokens
- markers
- identifying metadata

### Hide
Hide selected lines.

### Other Future Operations
Potential future context actions.

---

# Manual Markers

## Synthetic Timeline Entries

Users should be able to insert:
- marker lines
- annotation entries

Purpose:
- indicate actions/events during testing

Examples:
- “Pressed button”
- “Started test”
- “Clicked green button”

## Marker Placement

Preferred behavior:
- insert after selected line

Implementation:
- selected line timestamp + epsilon

Purpose:
- stable ordering

## Marker Visibility

Need:
- show/hide markers toggle

Default:
- visible

## Marker Persistence

Markers become part of:
- viewing history/timeline

Persistence behavior unresolved:
- session only?
- saved?
- exportable?

---

# Review Marker

## Reviewed-Up-To Marker

Provide draggable marker that lives between log lines.

Purpose:
- track review progress

## Interaction

### Dragging
Marker should support:
- dragging via nub/handle

### Right-Click
Users should be able to:
- right-click line
- choose “Reviewed up to here”

---

# Copy/Paste Workflow

## GenAI Integration Workflow

Major workflow:

1. Run tests
2. Filter logs
3. Insert markers
4. Select important lines
5. Copy to clipboard
6. Paste into GenAI

Purpose:
- evaluation
- debugging
- issue analysis

---

# Checklist / Script System

## Purpose

Support GenAI-generated:
- testing procedures
- observation scripts
- expected sequences

## UI

Pasting/loading a spec should open:
- thinner right-side panel

Panel behaves like:
- checklist/test runner

---

# Checklist Item Types

## Human Action Steps

Examples:
- “Start VRChat”
- “Click green button”

User should:
- manually check these

Checking may:
- insert markers into timeline

## Log Observation Steps

Examples:
- “Watch for XYZ log”
- “Expect ABC sequence”

These may:
- auto-complete when matched

---

# Spec Language

## Goals

The specification language should be:
- simple
- static/spec-like
- NOT a general programming language

## Requirements

Support:

### Ordered Sequences
Things that must happen in order.

### Unordered Groups
Things that may occur in any order.

### Nested Structures
Ordered groups inside unordered groups and vice versa.

### Marker Injection
Ability to:
- insert markers into timeline
- indicate sequence start/end

### Expected Log Matches
Watch for expected events.

### Human Steps
User action items.

---

# Checklist Visualization

Potential features:
- collapsible groups
- progress tracking
- auto-checking
- sequence highlighting

Potential future support:
- unexpected-condition tracking

---

# UI Layout

## Left Side

### Top Left
Source directory list.

### Bottom Left
Discovered log file list.

Includes:
- file controls
- lifetimes
- colors
- tokens
- overlap visualization

## Right Side

### Main Area
Merged log timeline view.

### Top Area
Filter/search controls.

### Optional Right Panel
Checklist/spec execution panel.

---

# Persistence

## Persist Across Restarts

Persist:
- source directories
- file aliases
- colors
- filters
- markers
- hidden lines
- layout state
- checklist state (possibly)

## Missing Files

Log files may:
- disappear
- rotate
- be deleted externally

Tool should:
- handle gracefully

---

# UI/UX Notes

## Buttons

Use:
- compact symbol buttons where meaningful

Avoid:
- ambiguous symbols

## Tooltips

All symbolic controls should include:
- tooltips

---

# Technical Notes

## Performance

Potentially large logs require:
- efficient rendering
- virtualization
- incremental updates

## Ordering Stability

Need deterministic ordering for:
- identical timestamps
- multiline entries
- synthetic markers

## Network Shares

Must support:
- remote/network share directories

Need robust handling for:
- intermittent connectivity

---

# Open Questions / Unresolved Areas

## Multi-Line Detection
Need actual VRChat log inspection.

## Severity Parsing
Need actual VRChat log inspection.

## Marker Persistence
Need design decision.

## Unexpected Condition Detection
Need specification semantics.

## Timeline Scalability
Need UI experimentation.

## Filter Logic Extensions
Potential future:
- AND logic
- exclusion logic
- grouped filters

## Checklist Language Syntax
Still undefined.

## Exact Marker UI
Still unresolved.

## Hidden Line Persistence
Need decision.

## Export Formats
Not yet specified.

## Session Storage Format
Not yet specified.

