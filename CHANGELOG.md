# Changelog

All notable changes to WhatsDab are documented here. This project adheres to
[Semantic Versioning](https://semver.org/).

## [1.0.5] - 2026-08-07

### Fixed

- Spamming the chat no longer freezes the game. Every message rebuilt the whole app, and a conversation was kept
  without end, so each rebuild cost more than the last. Redraws are collapsed into one per burst and a thread keeps
  its most recent 200 messages.
- The unread badge counts again. A thread you had open marked itself read whenever a message arrived, phone in your
  pocket or not - so nothing was ever unread.

## [1.0.4] - 2026-08-07

### Fixed

- One-to-one chats are back. The member list was never rebuilt, so there was nobody to write to and only the group
  thread worked. 1.0.3 fixed the same broken lobby lookup in the transport and missed a second copy of it here.
- A message cut at the 500 character limit can no longer be cut through the middle of an emoji.

## [1.0.3] - 2026-08-06

### Fixed

- Messages send again. WhatsDab read the lobby id from a game property that Schedule I stopped filling in 0.4.6,
  so every message was dropped before it left your machine - no error, no hint in the app. It now reads the id
  from where the game keeps it since that update.

## [1.0.2] - 2026-07-28

### Fixed

- A long player name no longer breaks the timestamp. The name and the time share one line, and the name could
  take all of it, so "02:55" wrapped onto two lines inside a row 58 pixels tall. The name is cut with an
  ellipsis instead and the time is never squeezed. The conversation header clamps the same way.

## [1.0.1] - 2026-07-27

### Fixed

- Opening the app could show a conversation header with nothing under it. WhatsDab reopens whichever thread you
  last read, and thread ids are the people you were in a lobby with - so the next lobby has different people and
  the stored id resolves to nothing. It now falls back to the group chat, which always exists, and the header no
  longer carries placeholder markup that made the empty pane look like a rendering failure.
- Notifications are back for the case that matters: a message arriving with the phone closed. Needs Sideload
  1.0.1, where the underlying check was fixed.

## [1.0.0] - 2026-07-27

First release, alongside Sideload 1.0.0. WhatsDab is the reference app: a complete chat client for the in
game phone, written as `index.html` / `app.css` / `app.js` and rendered by Sideload. It exists to prove that
somebody else can build with the framework without knowing its internals.

### Added

- **WhatsDab on the in game phone.** Contact list and conversation side by side in the phone's landscape
  panel, a search filter, unread counts per thread and in total, coloured avatars with an initial, day
  separators, bubbles carrying the sender's name in group threads, timestamps, a delivery tick, a typing
  indicator in both the conversation and the contact list, and a send bar.
- **Both orientations, one stylesheet.** The app declares both and the player turns the phone with the
  game's rotate keys - no button of its own. Landscape keeps the two pane split; portrait becomes push
  navigation - the list, then the conversation, with a back arrow - driven entirely by
  `@media (orientation: portrait)` in `app.css`. The choice outlives a reload.
- **Right-click goes back**, exactly as it does in the vanilla Messages app: out of a conversation first,
  and only from the list does it close WhatsDab. Two lines in `app.js`, on Sideload's cancellable `back`
  event, and the visible `<` button in the portrait header does the same thing.
- **Real messages between players**, over the Steam lobby chat the game already uses for its own control
  traffic. No FishNet, and nothing from any other mod - a lobby is a lobby, however it was opened. Messages
  are session-only and in memory; nothing is written to your save. One-to-one is filter-level privacy: the
  bytes reach every member and non-recipients drop them, which is the honest description of game chat.
- A scripted other side in development builds: a contact starts typing shortly after you send and answers
  shortly after that, so the whole app is exercisable from a single-player save.
- The whole mod as a worked example. `Core.cs` is one `Apps.Register` call plus data.
  `Chat/ChatBackend.cs` is the entire seam to the interface, five calls in and one event out, JSON strings
  both ways. `Chat/ChatModel.cs` is the only file holding sample data, so swapping in a real transport
  leaves the handlers and the whole interface untouched.
- No Unity assembly, no IL2CPP interop and no reference to Sideload itself. Only MelonLoader plus the
  single file `Sideload.Api` shim compiled in as source, so the mod ships as one DLL and does nothing rather
  than throwing when Sideload is absent.
- `preview.html` + `s1-mock.js` next to the app: the shipped `app.js` and `app.css` run in Chrome in a
  733x400 frame with a mock bridge, so layout and logic can be iterated without launching the game. Both are
  excluded from the embedded bundle.
- File overrides: anything under `Mods/whatsdab/` beats the copy embedded in the DLL, which is the same
  mechanism players use to reskin an app.
