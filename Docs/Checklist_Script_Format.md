# VLIT Checklist / Script Format

The checklist format is intentionally static and line-oriented. It is meant to be easy for humans and GenAI tools to generate, paste, review, and revise.

## Syntax

```text
title: Optional title ignored by the parser

ordered: Join smoke test
  action: Start client 1 => marker: Client 1 started
  action: Start client 2 => marker: Client 2 started
  expect: /\[Behaviour\].*network/i
  unordered: Bootstrap warnings that may arrive in any order
    expect: /SteamVR/
    expect: /StyleEngine/
  marker: Finished bootstrap review
```

Supported node types:

- `ordered:` or `sequence:` creates a group whose children must complete in order.
- `unordered:`, `any:`, or `group:` creates a group whose children may complete in any order.
- `action:` or `step:` creates a manual human step.
- `expect:`, `log:`, or `match:` creates a regex log observation.
- `marker:` or `mark:` creates a manual marker step.
- `title:` is accepted and ignored.

Indentation controls nesting. Two spaces equals one level. Lines may also start with `- ` for list-like readability.

## Regexes

`expect:` accepts either raw regex text or slash-delimited regex text:

```text
expect: /Udon.*Exception/
expect: \[Behaviour\].*RPC
```

Matching is case-insensitive in the initial implementation.

## Marker Injection

Manual action and log expectation nodes may include marker injection:

```text
action: Click the green button => marker: Green button clicked
expect: /ButtonState.*Ready/ => marker: Ready state observed
```

For actions, the marker is inserted when the user checks the item. For expectations, the marker is inserted when VLIT first observes the matching log entry.

## Completion Semantics

Ordered groups activate the first incomplete child and advance as each child completes. Unordered groups watch all children independently. Nested ordered and unordered groups are supported.

Manual items are completed by the user. Expectation items are completed automatically when a matching log entry appears in the merged timeline.
