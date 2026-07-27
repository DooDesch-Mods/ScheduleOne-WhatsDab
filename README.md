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

WhatsDab appears on the in game phone as its own app, and it texts the people actually in your Steam lobby.
One thread per player plus a group thread for everyone, with a contact list and the conversation side by
side, a search filter, unread counts per thread and in total, coloured avatars, day separators, bubbles with
the sender's name in groups, timestamps, a delivery tick, a typing indicator in both the conversation and
the contact list, and a send bar.

Any lobby works - plain co-op, one a gamemode opened, one from the main menu. It rides the same Steam lobby
chat the game already uses for its own messages, so there is no server, no FishNet and no other mod in the
way. Side Hustle is not required.

The phone turns with the game's own rotate keys - the app has no button for it, and the key strip at the
bottom left says which key. Landscape keeps the two panes side by side; portrait switches to push
navigation - the list, then the conversation, with a back arrow. That is one `@media (orientation:
portrait)` block in `app.css`, not a second layout.

Right-click goes back the way it does everywhere else on the phone: out of a conversation first, and only
from the list does it close the app.

Conversations live in memory for the session and are deliberately never written to disk: a chat log on
someone's drive is not what people expect from a lobby chat, and the name shown is whatever a player already
goes by in game, so a privacy mod that renames them is respected here too. Leave the lobby and the
conversations are gone.

A development build swaps the lobby for a scripted conversation, so the whole interface is exercisable from
a single player save without a second machine.

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

What sits in those lines matters more than how many there are: the uGUI version spends 60 of them computing
a chat bubble's width, line count and height from text measurements. In CSS the same bubble is `align-self`,
`max-width` and `padding`.

## Layout of the mod

```
WhatsDab/
  Core.cs                 MelonMod: one Register call, then only data
  Chat/IChatSource.cs     where conversations come from - the seam between the two halves below
  Chat/ChatBackend.cs     the ENTIRE seam to the interface: 6 calls in, 1 event out
  Chat/DemoSource.cs      the scripted conversation, so the app works with no lobby running
  Chat/ChatModel.cs       threads, messages, unread counts, all in memory
  Chat/Json.cs            a tiny JSON writer (no parser needed on the C# side)
  Lobby/LobbySource.cs    the real one: everyone in your Steam lobby
  Lobby/ChatTransport.cs  send and receive over Steam lobby chat
  Lobby/ChatEnvelope.cs   the wire format
  Lobby/ChatStore.cs      conversations, unread counts, echo reconciliation
  Lobby/Contacts.cs       lobby members, resolved to display names
  Assets/whatsdab/        index.html, app.css, app.js, icon.png - the app itself
```

The two halves meet at `IChatSource`, and that seam has already paid off once: the lobby transport was added
under it without `ChatBackend` or a single line of the interface changing. `Chat/` compiles without any game
reference, which is what lets the headless suite run the page's JavaScript against the real handlers in a
second.

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
| [Sideload](https://github.com/DooDesch-Mods/ScheduleOne-Sideload) | `1.0.1+` - the framework that renders the app |

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
- Nothing is added to the world and nothing is written to a save. What does travel between players is chat
  text, over the Steam lobby's own chat channel - so everyone in the lobby needs WhatsDab to take part, and
  players without it are unaffected.
- Without Sideload installed the shim binds nothing, every call is a no op and the mod simply does nothing
  rather than throwing.

## Credits

- **DooDesch** - mod author.
- Built on **[Sideload](https://github.com/DooDesch-Mods/ScheduleOne-Sideload)**.

## License

Provided as-is under the [MIT License](LICENSE.md).
