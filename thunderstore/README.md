# WhatsDab - Chat on the Schedule I Phone

> 🛟 **Need help or found a bug?** Get support at [support.doodesch.de/whatsdab](https://support.doodesch.de/whatsdab).

> **Text the people in your lobby from the phone you already carry.** Threads, unread counts, group and one
> to one chat, in both orientations. Written as three web files on
> [Sideload](https://thunderstore.io/c/schedule-i/p/DooDesch/Sideload/), which is also the mod to read if you
> want to build your own phone app.

![Version](https://img.shields.io/badge/version-1.0.0-blue)
![Game](https://img.shields.io/badge/game-Schedule%20I-purple)
![MelonLoader](https://img.shields.io/badge/MelonLoader-0.7.3+-green)
![Sideload](https://img.shields.io/badge/Sideload-required-orange)

**[Source](https://github.com/DooDesch-Mods/ScheduleOne-WhatsDab)** · **[Sideload](https://github.com/DooDesch-Mods/ScheduleOne-Sideload)** · **[Wiki / docs](https://github.com/DooDesch-Mods/ScheduleOne-Sideload/wiki)** · **[Support](https://support.doodesch.de/whatsdab)**

## Players

WhatsDab shows up as its own app on the in game phone and texts the people actually in your Steam lobby. One
thread per player plus a group thread, with a contact list and the conversation side by side, a search
filter, unread counts, coloured avatars, day separators, timestamps, a delivery tick, a typing indicator and
a send bar. Turn the phone with the game's own rotate keys and it switches to portrait, one pane at a time.

Any lobby works - plain co-op, one a gamemode opened, one from the main menu. It rides the same Steam lobby
chat the game already uses, so there is no server and no other mod needed. Everyone in the lobby needs
WhatsDab to take part.

Conversations stay in memory for the session and are never written to disk. Leave the lobby and they are
gone. It is safe to remove at any time.

## Mod authors

This is the mod to copy. It references **no** Unity assembly, **no** IL2CPP interop and **not Sideload
itself** - only MelonLoader plus the single file `Sideload.Api` shim, which finds the host by reflection.
Game logic in C#, the entire interface in `index.html` / `app.css` / `app.js`, shipped as one DLL.

```
Core.cs                 one Apps.Register call, then only data
Chat/ChatBackend.cs     the ENTIRE seam to the interface: 5 calls in, 1 event out
Assets/whatsdab/        the app
```

The same screen exists as hand built uGUI elsewhere: 781 lines of C# there against 518 lines of HTML, CSS
and JavaScript here, with more features. What the lines hold matters more than how many: one version computes
a chat bubble's height from text measurements, the other writes `align-self` and `max-width`.

Any file under `Mods/whatsdab/` overrides the copy embedded in the DLL, so you can edit the app without
rebuilding. `preview.html` in the source tree runs the shipped app in Chrome without launching the game.

Guide, the exact CSS subset and the layout rules that differ from a browser:
**[the Sideload wiki](https://github.com/DooDesch-Mods/ScheduleOne-Sideload/wiki)**.

## Requirements

**Schedule I** (IL2CPP) with **MelonLoader 0.7.3+** and
**[Sideload](https://thunderstore.io/c/schedule-i/p/DooDesch/Sideload/) 1.0.1+** (pulled in as a dependency).

## Settings

None. This mod has nothing to configure.

## License

MIT. See the included LICENSE.md.
