# WhatsDab - Chat on the Schedule I Phone

> 🛟 **Need help or found a bug?** Get support at [support.doodesch.de/whatsdab](https://support.doodesch.de/whatsdab).

> Text the people in your lobby from the phone you already carry. Threads, unread counts, group chat and
> one-to-one, in both orientations.
>
> Built on [Sideload](https://github.com/DooDesch-Mods/ScheduleOne-Sideload), which is a hard requirement -
> the whole interface is three web files rather than hand-built panels, and the mod's own C# is one
> `Apps.Register` call plus the chat itself.

![Version](https://img.shields.io/badge/version-1.0.0-blue)
![Game](https://img.shields.io/badge/game-Schedule%20I-purple)
![MelonLoader](https://img.shields.io/badge/MelonLoader-0.7.3+-green)
![Sideload](https://img.shields.io/badge/Sideload-required-orange)
![Multiplayer](https://img.shields.io/badge/multiplayer-co--op-blue)

**[Sideload](https://github.com/DooDesch-Mods/ScheduleOne-Sideload)** · **[Wiki / docs](https://github.com/DooDesch-Mods/ScheduleOne-Sideload/wiki)** · **[Support](https://support.doodesch.de/whatsdab)**

## What you get

WhatsDab appears on the in game phone as its own app. It is a working chat client with a contact list and a
conversation side by side, a search filter, unread counts per thread and in total, coloured avatars, day
separators, bubbles with the sender's name in groups, timestamps, a delivery tick, a typing indicator in
both the conversation and the contact list, and a send bar. The other side answers on a script, so the app
is alive without a lobby.

The phone turns with the game's own rotate keys - the app has no button for it, and the key strip at the
bottom left says which key. Landscape keeps the two panes side by side; portrait switches to push
navigation - the list, then the conversation, with a back arrow. That is one `@media (orientation:
portrait)` block in `app.css`, not a second layout.

Right-click goes back the way it does everywhere else on the phone: out of a conversation first, and only
from the list does it close the app.

The messages are made up and stay in memory. Nothing is saved, nothing is sent anywhere, nothing touches
your game. It is a demo you can play with and a codebase you can lift.

## Why it exists

To answer one question: can somebody else build with this framework without knowing its internals? The
project references **no** Unity assembly, **no** IL2CPP interop and **not Sideload itself** - only
MelonLoader plus the single file `Sideload.Api` shim, which finds the host by reflection. That is the whole
point. A mod author writes game logic in C# and the entire interface in web files, and ships one DLL.

The same screen exists in Side Hustle as hand built uGUI, which makes the comparison concrete:

| | uGUI (SideHustle Messenger) | Sideload (this mod) |
|---|---|---|
| Interface | 781 lines of C# | 518 lines of HTML/CSS/JS |
| Logic + data | 489 lines of C# | 475 lines of C# |
| Changing the layout | rebuild, restart the game | save the file |

The line count is not the point. What is in the lines is: the uGUI version spends 60 lines computing a chat
bubble's width, line count and height from text measurements. In CSS the same bubble is `align-self`,
`max-width` and `padding`.

## Layout of the mod

```
WhatsDab/
  Core.cs                 MelonMod: one Register call, then only data
  Chat/ChatModel.cs       threads, messages, unread counts, all in memory
  Chat/ChatBackend.cs     the ENTIRE seam to the interface: 5 calls in, 1 event out
  Chat/Json.cs            a tiny JSON writer (no parser needed on the C# side)
  Assets/whatsdab/        index.html, app.css, app.js, icon.png - the app itself
```

`ChatModel` is deliberately the only file holding sample data. Swapping it for a real transport leaves
`ChatBackend` and the entire interface untouched.

`Assets/whatsdab/` also carries `preview.html` and `s1-mock.js`. Those two are authoring tools, not part of
the app: open `preview.html` in Chrome and the shipped `app.js` and `app.css` run in a 733x400 frame without
launching the game. They are excluded from the embedded bundle rather than shipped as dead weight.

## The seam, in full

```csharp
AppHandle app = Apps.Register("whatsdab", "WhatsDab.Assets.whatsdab", title: "WhatsDab");

app.OnCall("chat.threads", _ => Threads())        // answers s1.call('chat.threads') in the page
   .OnCall("chat.thread", Conversation)
   .OnCall("chat.send", Send);

app.Emit("chat.changed", unread.ToString());      // reaches s1.on('chat.changed', fn) in the page
```

Strings both ways, JSON for anything structured. No CLR object reaches the script and no script object
reaches the mod, which is why `app.css` can be rewritten from scratch without touching `ChatBackend.cs`.

One detail worth stealing: the event is pushed from the update loop when a revision counter changes, not
from inside a handler. Emitting from inside an `OnCall` re-enters the script engine while it is still on the
stack.

## Requirements

| Component | Version / Source |
|-----------|------------------|
| Schedule I | IL2CPP (current Steam public build) |
| MelonLoader | `0.7.3+` |
| [Sideload](https://github.com/DooDesch-Mods/ScheduleOne-Sideload) | `1.0.0+` - the framework that renders the app |

## Installation

### Recommended: a Thunderstore mod manager

Install with a mod manager (r2modman / Gale) from the Schedule I community; MelonLoader and Sideload are
pulled in automatically.

### Manual

1. Install **MelonLoader 0.7.3** for Schedule I.
2. Install **[Sideload](https://github.com/DooDesch-Mods/ScheduleOne-Sideload)** and its support libraries.
3. Drop **`WhatsDab.dll`** into your Schedule I `Mods/` folder.
4. Open the phone in game. WhatsDab is on the home screen.

## Configuration

None. This mod registers no MelonPreferences entries and has nothing to tune. Sideload's own developer
settings live under `Sideload_01_Main`; see the
[Sideload README](https://github.com/DooDesch-Mods/ScheduleOne-Sideload#configuration).

## Editing the app without rebuilding

Any file under `Mods/whatsdab/` overrides the copy embedded in the DLL. Copy `index.html`, `app.css` and
`app.js` into that folder, edit them, and the changes are picked up the next time the page builds. With
Sideload's Chrome DevTools switched on, `Page.reload` in the inspector rebuilds from disk on the spot. That
is also the mechanism players use to reskin an app.

## Compatibility

- IL2CPP build only (current Steam public branch).
- Client local. It adds nothing to the world and nothing that travels between players.
- Without Sideload installed the shim binds nothing, every call is a no op and the mod simply does nothing
  rather than throwing.

## Credits

- **DooDesch** - mod author.
- Built on **[Sideload](https://github.com/DooDesch-Mods/ScheduleOne-Sideload)**.

## License

Provided as-is under the [MIT License](LICENSE.md).
