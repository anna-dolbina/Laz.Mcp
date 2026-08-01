# Laz.Mcp

An MCP (Model Context Protocol) server that exposes the [Laz](https://github.com/tinfoil-herald/laz)
library's mouse, keyboard, and screen-capture automation as tools an LLM client can call.

Laz simulates real OS-level input (mouse, keyboard) and reads real pixels off the screen on
Windows, Linux (X11/XWayland), and macOS. This server just wraps that library's public API 1:1
(plus a couple of small ergonomic conveniences) as MCP tools — it does not add new automation
capabilities beyond what Laz already does.

**This grants a connected LLM control of your actual mouse and keyboard.** Only run it in a
session/user context you're comfortable handing that control to, and prefer running it in a
disposable VM or sandboxed desktop session for anything you don't fully trust.

## Why this one

There are several other MCP servers for desktop automation (RobotJS-, PyAutoGUI-, AutoHotkey-, or
native-backed). Most converge on the same base — click/move/drag, keypress/type, screenshot — with
window management, clipboard access, or OCR bolted on as extras, and most that claim "cross-platform"
are really Windows- or macOS-first with the other platforms added on top.

Laz.Mcp stays narrow on purpose and differs on two things:

- **Natural mouse movement.** `mouse_move_smooth` and `mouse_drag_and_drop` travel a linear or
  bezier path under a choice of 9 easing curves, rather than a flat duration parameter or an
  instant jump. Relevant if the target app (or a bot-detection layer) treats motion trajectory as
  a signal.
- **One implementation across Windows, Linux (X11/XWayland), and macOS**, from Laz's native input
  handling on each platform, rather than a single-OS core with other platforms shimmed in.

It doesn't do window management, clipboard access, or OCR — this stays a thin, reliable primitives
layer over mouse, keyboard, and screen, meant to be a building block for whatever agent logic you
put on top of it, not a full computer-use framework.

## Transport

Stdio only. The server is a console app launched by an MCP client (Claude Desktop, Claude Code,
etc.) over stdin/stdout — no network listener.

## Coordinates

All positions are integer `(x, y)` screen pixels in Laz's coordinate space (see platform notes
in the [Laz README](https://github.com/tinfoil-herald/laz#readme) — DPI-awareness matters on
Windows, and Wayland/XWayland HiDPI setups can behave unexpectedly on Linux). The server does not know the screen resolution
(Laz exposes no such API) — the client must supply valid coordinates/regions itself, e.g. by
first calling `screen_capture` over a guessed region and adjusting, or by asking the user.

## Tools

### Mouse

| Tool | Description | Parameters |
|---|---|---|
| `mouse_get_position` | Returns the current cursor position. | — |
| `mouse_jump_to` | Instantly moves the cursor to a position (teleport, no intermediate motion). | `x`, `y` |
| `mouse_move_smooth` | Moves the cursor from its current position (or an explicit `startX`/`startY`) to a target over time, along a linear or bezier path with an easing curve — looks like natural human movement instead of a teleport. | `targetX`, `targetY`, `startX?`, `startY?`, `durationMs?` (default 1500), `path?` (`Linear`\|`Bezier`, default `Linear`), `easing?` (`Linear`, `EaseInOutCubic`, `EaseOutCubic`, `EaseOutQuartic`, `EaseOutExpo`, `EaseInQuad`, `EaseInOutQuad`, `EaseInOutSine`, `EaseOutCircular`, `EaseOutElastic`, `EaseOutBounce`; default `Linear`) |
| `mouse_press` | Presses and holds a button down until `mouse_release`. | `button?` (`Primary`\|`Middle`\|`Secondary`, default `Primary`) |
| `mouse_release` | Releases a previously pressed button. | `button?` |
| `mouse_click` | Presses and releases a button once. | `button?` |
| `mouse_double_click` | Two clicks in quick succession. | `button?` |
| `mouse_scroll` | Spins the scroll wheel. Positive scrolls up, negative scrolls down. | `notches` |
| `mouse_drag_and_drop` | Presses at `start`, moves naturally to `target`, releases. Optional `dragPreamble` adds a short downward nudge first, which some apps need to recognize a drag gesture. | `startX`, `startY`, `targetX`, `targetY`, `durationMs?`, `path?`, `easing?`, `dragPreamble?` (default `false`) |

`mouse_move_smooth` and `mouse_drag_and_drop` are synchronous and block for the full
`durationMs` (default 1.5s) while they play out the motion — that's inherent to Laz's move
implementation, not an artifact of the server.

### Keyboard

| Tool | Description | Parameters |
|---|---|---|
| `keyboard_key_down` | Presses and holds a key until `keyboard_key_up`. Used to build modifier-held sequences. | `key` |
| `keyboard_key_up` | Releases a previously held key. | `key` |
| `keyboard_stroke` | Presses and releases a single key. | `key` |
| `keyboard_combo` | Convenience helper (not a direct Laz method): holds down a list of keys in order, then releases them in reverse order — e.g. `["Control", "Shift", "Esc"]` for a shortcut chord. | `keys` (array of key names, 1+) |
| `keyboard_type` | Types arbitrary text as a sequence of keystrokes, handling dead keys and shifted characters automatically. Characters that can't be produced by the active keyboard layout fail unless `clipboardFallback` is set. | `text`, `clipboardFallback?` (default `false`, Windows/macOS only), `useCtrlInsert?` (default `false`, Windows only — uses Ctrl/Shift+Insert instead of Ctrl+C/V for the clipboard fallback) |

`key` accepts the case-insensitive name of a `Laz.Key` enum member, e.g. `A`-`Z`, `Zero`-`Nine`,
`F1`-`F24`, `Enter`, `Tab`, `Esc`, `Space`, `Backspace`, `Delete`, `Insert`, `Left`/`Right`/`Up`/`Down`,
`Home`, `End`, `PageUp`, `PageDown`, `Shift`, `Control`, `Alt`, `LeftShift`/`RightShift`/
`LeftControl`/`RightControl`/`LeftAlt`/`RightAlt`, `LeftWin`/`RightWin`, `CapsLock`, `NumLock`,
`ScrollLock`, `Numpad0`-`Numpad9` and `NumpadPlus`/`NumpadMinus`/`NumpadMultiply`/`NumpadDivide`/
`NumpadDecimal`/`NumpadEquals`/`NumpadSeparator`, punctuation (`Semicolon`, `Equal`, `Comma`,
`Minus`, `Dot`, `Slash`, `Grave`, `OpenBracket`, `Backslash`, `CloseBracket`, `Apostrophe`), and
media/browser/volume keys. An invalid name returns a tool error listing this guidance rather than
throwing a raw exception.

### Screen

| Tool | Description | Parameters |
|---|---|---|
| `screen_capture` | Captures a rectangular region and returns it as a PNG image content block (viewable inline by the client). | `x`, `y`, `width`, `height` |
| `screen_get_color_at` | Reads the color of a single pixel. | `x`, `y` → `{ r, g, b, a }` |

macOS requires the Screen Recording permission to be granted to the host process for both
`screen_capture` and `screen_get_color_at`; the OS prompts on first use. macOS and Linux also
require Accessibility / an active X11-or-XWayland session respectively for mouse and keyboard
tools — see the Laz README's per-platform remarks for each method.

## Configuration

- `LAZ_DELAY_MS` env var — overrides Laz's default 50ms inter-event delay (used between chained
  actions like `keyboard_stroke` and between characters in `keyboard_type`). Set before the
  server process starts.

## Implementation notes (for the build)

- .NET 10, `ModelContextProtocol` + `Microsoft.Extensions.Hosting` for the stdio server host.
- A single `Laz.Lazbot` instance is held for the process lifetime and all calls into it are
  serialized behind one lock, since it manipulates global OS input/cursor state and two tool
  calls interleaving mid-gesture (e.g. a click landing in the middle of a drag) would be a real
  bug, not just a theoretical race.
- `screen_capture`'s raw BGRA bytes are encoded to PNG (via SixLabors.ImageSharp, for
  cross-platform support — `System.Drawing` is Windows-only) before being returned as an
  `ImageContentBlock`.
- Enum-like parameters (`key`, `button`, `path`, `easing`) are accepted as strings and parsed
  with `Enum.TryParse(ignoreCase: true)`, throwing `McpException` with a specific message on
  failure, rather than relying on the default JSON-schema enum-as-integer serialization — this
  keeps error messages legible to the model instead of a generic "invocation failed".
