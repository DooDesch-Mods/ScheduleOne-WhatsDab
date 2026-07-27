// Browser stand-in for the Sideload host. Load this BEFORE app.js in preview.html and the same, unmodified app runs
// in Chrome - same DOM calls, same s1 bridge, same JSON shapes.
//
// It exists so layout work does not cost a game launch. What it deliberately does NOT do is emulate the renderer:
// Chrome lays the page out with its own engine, so this proves the app's LOGIC and roughly its look, never the exact
// pixels. The engine's own layout is covered by Workspace/Tests/Sideload.Tests.
//
// The chat data here mirrors WhatsDab/Chat/ChatModel.cs. If the two drift apart, the preview lies - so the
// seed below is deliberately short and boring, and anything subtle belongs in a test rather than here.

(() => {
  const minutesAgo = (m) => new Date(Date.now() - m * 60000);
  const clock = (d) => `${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`;

  const dayLabel = (d) => {
    const midnight = new Date(); midnight.setHours(0, 0, 0, 0);
    if (d >= midnight) return 'Today';
    if (d >= new Date(midnight.getTime() - 86400000)) return 'Yesterday';
    return d.toLocaleDateString('en-GB', { day: 'numeric', month: 'long' });
  };

  const msg = (from, text, mins, mine = false) => ({ from, text, at: minutesAgo(mins), mine, pending: false });

  const threads = [
    { id: 'everyone', name: 'Everyone', group: true, unread: 1, messages: [
      msg('coolpaca', 'anyone seen my van keys', 46),
      msg('wesconsin', 'they were in the ignition. again.', 44),
      msg('SirTidez', 'meeting at the docks at nine, bring the van', 12),
    ]},
    { id: 'tidez', name: 'SirTidez', group: false, unread: 0, messages: [
      msg('SirTidez', 'you at the warehouse?', 180),
      msg('You', 'on my way, five minutes', 176, true),
      msg('SirTidez', 'nice, door is open', 175),
    ]},
    { id: 'bars', name: 'Bars', group: false, unread: 2, messages: [
      msg('Bars', 'we are two short for tonight', 9),
      msg('Bars', 'can you bring someone?', 8),
    ]},
    { id: 'eternidox', name: 'Eternidox', group: false, unread: 0, messages: [
      msg('You', 'storage is full, we need a second place', 320, true),
      msg('Eternidox', 'i know a guy who knows a guy', 318, true),
    ]},
  ];

  const replies = [
    'on my way',
    'give me two minutes, dealing with a customer',
    'did you restock the west van?',
    'cops were on Marina Ave, take the back road',
  ];

  let replyIndex = 0;
  let typingIn = null;
  let online = true;
  let orientation = window.innerHeight > window.innerWidth ? 'portrait' : 'landscape';
  const listeners = {};

  const find = (id) => threads.find((t) => t.id.toLowerCase() === String(id).toLowerCase()) ?? null;
  const changed = () => (listeners['chat.changed'] ?? []).forEach((fn) => fn(String(totalUnread())));
  const totalUnread = () => threads.reduce((sum, t) => sum + t.unread, 0);

  const conversation = (id) => {
    const thread = find(id);
    if (!thread) return JSON.stringify({ id: '', name: '' });

    const items = [];
    let day = null;
    for (const m of thread.messages) {
      const label = dayLabel(m.at);
      if (label !== day) { items.push({ kind: 'day', text: label }); day = label; }
      items.push({ kind: 'message', from: m.from, text: m.text, time: clock(m.at), mine: m.mine, pending: m.pending });
    }

    return JSON.stringify({
      id: thread.id, name: thread.name, group: thread.group,
      typing: typingIn === thread.id, messages: items,
    });
  };

  const send = (argument) => {
    const split = argument.indexOf('\n');
    if (split <= 0) return 'error';

    const id = argument.slice(0, split);
    const text = argument.slice(split + 1);
    if (text.includes('\n') || !text.trim()) return 'error';

    const thread = find(id);
    if (!thread) return 'error';

    thread.messages.push({ from: 'You', text: text.trim(), at: new Date(), mine: true, pending: true });
    changed();

    // The same scripted round trip the mod performs, so the pending mark and the reply are visible here too.
    setTimeout(() => { thread.messages.at(-1).pending = false; changed(); }, 900);
    setTimeout(() => { typingIn = thread.id; changed(); }, 1800);
    setTimeout(() => {
      typingIn = null;
      thread.messages.push({
        from: thread.group ? 'Benji Coleman' : thread.name,
        text: replies[replyIndex++ % replies.length],
        at: new Date(), mine: false, pending: false,
      });
      thread.unread++;
      changed();
    }, 4000);

    return 'ok';
  };

  window.s1 = {
    call(name, argument = '') {
      switch (name) {
        case 'chat.self': return 'You';
        case 'chat.status':
          return JSON.stringify({ online, peers: threads.filter((t) => !t.group).length });
        case 'chat.threads':
          return JSON.stringify(threads.map((t) => {
            const last = t.messages.at(-1);
            return {
              id: t.id, name: t.name, group: t.group, unread: t.unread,
              typing: typingIn === t.id,
              // Empty stays empty, exactly as the mod leaves it: what an unused thread should say is the page's call.
              preview: last ? `${last.mine ? 'You: ' : t.group ? `${last.from}: ` : ''}${last.text}` : '',
              time: last ? clock(last.at) : '',
            };
          }));
        case 'chat.thread': return conversation(argument);
        case 'chat.send': return send(argument);
        case 'chat.read': {
          const thread = find(argument);
          if (thread) { thread.unread = 0; changed(); }
          return 'ok';
        }
        default:
          console.warn(`[s1-mock] no handler for ${name}`);
          return '';
      }
    },

    on(name, handler) { (listeners[name] ??= []).push(handler); },

    // The host turns the real phone. A page cannot turn a browser window, and `@media (orientation: ...)` in Chrome
    // answers to the WINDOW rather than to any element - so here the value is only recorded, and the preview shell
    // follows the window instead. Make the browser window taller than it is wide to see the portrait layout.
    get orientation() { return orientation; },

    setOrientation(value) {
      const next = value === 'portrait' ? 'portrait' : 'landscape';
      if (next === orientation) return;
      orientation = next;
      console.info(`[s1-mock] orientation is now ${orientation}. In Chrome, resize the WINDOW to see the layout change.`);
      raiseOrientationChange();
    },

    storage: {
      get: (key, fallback = '') => localStorage.getItem(`sideload:${key}`) ?? fallback,
      set: (key, value) => localStorage.setItem(`sideload:${key}`, value),
      remove: (key) => localStorage.removeItem(`sideload:${key}`),
    },
  };

  // The states the game reaches by itself and a browser cannot: the transport going away, and the lobby emptying out.
  // Without a handle on them the offline screen and the lobby-of-one note are the two parts of the app that can only
  // be looked at in the game, which is the trip the preview exists to avoid. Call them from the Chrome console.
  window.s1mock = {
    setOnline(value) { online = !!value; changed(); },
    emptyLobby() { threads.length = 1; changed(); },
  };

  // The one DOM method the host adds and a browser does not have.
  Element.prototype.scrollToEnd = function scrollToEnd() {
    requestAnimationFrame(() => { this.scrollTop = this.scrollHeight; });
  };

  const page = () => document.querySelector('#viewport .app') ?? document.body;

  // In the game a right-click or Escape raises a cancellable `back` at the page, and the app closes unless a
  // handler takes it. A browser has no app to close, so the preview raises the same event and stops there - enough
  // to prove the handler navigates, which is the part that can be wrong.
  const raiseBack = (source) => {
    const event = new CustomEvent('back', { bubbles: true, cancelable: true });
    event.source = source;
    page().dispatchEvent(event);
    return event.defaultPrevented;
  };

  // The host raises this after it has laid the page out at the new shape. Here the window is the shape, so the
  // window's own resize is what stands in for the turn.
  const raiseOrientationChange = () => {
    const event = new CustomEvent('orientationchange', { bubbles: true });
    event.value = orientation;
    page().dispatchEvent(event);
  };

  window.addEventListener('resize', () => {
    const shape = window.innerHeight > window.innerWidth ? 'portrait' : 'landscape';
    if (shape === orientation) return;
    orientation = shape;
    raiseOrientationChange();
  });

  document.addEventListener('contextmenu', (e) => {
    if (!e.target.closest('#viewport')) return;
    e.preventDefault();                       // the game has no context menu either
    if (!raiseBack('rightClick')) console.info('[s1-mock] nobody took the back - the game would close the app here.');
  });

  document.addEventListener('keydown', (e) => {
    if (e.key !== 'Escape') return;
    if (!raiseBack('escape')) console.info('[s1-mock] nobody took the back - the game would close the app here.');
  });
})();
