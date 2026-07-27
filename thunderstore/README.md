# WhatsDab - Chat on the Schedule I Phone

> 🛟 **Need help or found a bug?** Get support at [support.doodesch.de/whatsdab](https://support.doodesch.de/whatsdab).

> **A chat app on the in game phone, written as three web files.** WhatsDab exists to show what
> [Sideload](https://thunderstore.io/c/schedule-i/p/DooDesch/Sideload/) does: install it to see the
> framework working, read its source to copy the pattern into your own mod.

![Version](https://img.shields.io/badge/version-1.0.0-blue)
![Game](https://img.shields.io/badge/game-Schedule%20I-purple)
![MelonLoader](https://img.shields.io/badge/MelonLoader-0.7.3+-green)
![Sideload](https://img.shields.io/badge/Sideload-required-orange)

**[Source](https://github.com/DooDesch-Mods/ScheduleOne-WhatsDab)** · **[Sideload](https://github.com/DooDesch-Mods/ScheduleOne-Sideload)** · **[Wiki / docs](https://github.com/DooDesch-Mods/ScheduleOne-Sideload/wiki)** · **[Support](https://support.doodesch.de/whatsdab)**

## Players

WhatsDab shows up as its own app on the in game phone: a contact list and a conversation side by side, a
search filter, unread counts, coloured avatars, day separators, timestamps, a delivery tick, a typing
indicator and a send bar. The other side answers on a script, so it is alive without a lobby.

The messages are made up and live only in memory. Nothing is saved, nothing is sent anywhere, nothing
touches your game. It is a demo, and it is safe to remove at any time.

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
and JavaScript here, with more features. The difference is not the count, it is that one of them computes a
chat bubble's height from text measurements and the other writes `align-self` and `max-width`.

Any file under `Mods/whatsdab/` overrides the copy embedded in the DLL, so you can edit the app without
rebuilding. `preview.html` in the source tree runs the shipped app in Chrome without launching the game.

Guide, the exact CSS subset and the layout rules that differ from a browser:
**[the Sideload wiki](https://github.com/DooDesch-Mods/ScheduleOne-Sideload/wiki)**.

## Requirements

**Schedule I** (IL2CPP) with **MelonLoader 0.7.3+** and
**[Sideload](https://thunderstore.io/c/schedule-i/p/DooDesch/Sideload/) 1.0.0+** (pulled in as a dependency).

## Settings

None. This mod has nothing to configure.

## License

MIT. See the included LICENSE.md.
