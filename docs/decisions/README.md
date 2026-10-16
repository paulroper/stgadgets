# Decision records

Short records of significant decisions: what we chose, why, and what we gave up. Records aren't edited after they're accepted. If a decision changes, a new record replaces the old one.

| # | Decision | Status |
|---|---|---|
| [0001](0001-dotnet.md) | Build with .NET 10 | Accepted |
| [0002](0002-retroarch-first.md) | RetroArch is the first emulator | Accepted (confirmed by 0009) |
| [0003](0003-transparent-overlay-window.md) | Draw in a transparent window over the emulator | Accepted |
| [0004](0004-providers-use-official-emulator-apis.md) | Memory maps use the original hardware's addresses; providers use official emulator APIs | Accepted |
| [0005](0005-macos-first.md) | Develop for macOS first | Accepted |
| [0006](0006-react-gadgets-in-a-webview.md) | Gadgets are React components running in a webview | Accepted |
| [0007](0007-native-overlay-host.md) | The overlay host is a native window with the system webview | Proposed |
| [0008](0008-providers-push-frame-snapshots.md) | Providers push frame snapshots | Accepted |
| [0009](0009-first-provider-retroarch.md) | RetroArch (via READ_CORE_RAM) is the first provider | Accepted |
| [0010](0010-game-packs.md) | Everything game-specific lives in game packs | Accepted (recordings amended by 0011) |
| [0011](0011-recordings-are-local.md) | Recordings are local; recording tools live in a skill | Accepted |

## Template

```markdown
# NNNN. Title

- **Status:** Proposed | Accepted | Superseded by NNNN
- **Date:** YYYY-MM-DD

## Context
## Decision
## Consequences
```
