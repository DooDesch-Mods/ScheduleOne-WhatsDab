// WhatsDab. The whole interface: pick a thread, read it, write into it.
//
// The mod behind this never touches a widget. It answers six calls and raises one event; everything below decides
// what that means on screen. Rewriting the layout is an edit to app.css and a reload, not a rebuild.
//
// Sideload runs Jint with every feature enabled, so this is ordinary modern JavaScript - classes with private
// fields, destructuring, optional chaining, template literals. Nothing here is transpiled or polyfilled.

const $ = (id) => document.getElementById(id);

/** One stable colour per person, so a name looks the same in every thread it appears in. */
const tint = (name = '') => {
  let hash = 0;
  for (const ch of name) hash = (hash * 31 + ch.charCodeAt(0)) & 0x7fffffff;
  return hash % 6;
};

const initial = (name) => (name?.trim()?.[0] ?? '?').toUpperCase();

/** The one conversation that is always there, whoever else is in the lobby. */
const GROUP = 'everyone';

/**
 * Shorten a name that would otherwise decide how wide a bubble is. A bubble is sized by its widest line, so an
 * unusually long persona name above a two-word message stretches the bubble across the conversation for no reason.
 *
 * The dots are part of the budget, not added on top of it - a clamp that returns more characters than it was asked
 * for is not a clamp. Three dots rather than the ellipsis character: the game's TMP atlases stop at Latin-1, so
 * U+2026 draws as an empty box.
 */
const clamp = (text, max) => (text.length <= max ? text : `${text.slice(0, max - 3).trimEnd()}...`);

/** Build an element in one expression - the shape almost every line below needs. */
const el = (tag, className, text) => {
  const node = document.createElement(tag);
  if (className) node.className = className;
  if (text != null) node.textContent = String(text);
  return node;
};

/**
 * The bridge to the mod. Every call crosses as a string; anything structured is JSON. Wrapping it in one class keeps
 * the parse-or-complain in a single place instead of at every call site.
 */
class Chat {
  #call(name, argument = '') {
    const raw = s1.call(name, argument);
    if (!raw) return null;

    try {
      return JSON.parse(raw);
    } catch {
      console.error(`${name} returned junk:`, raw);
      return null;
    }
  }

  get threads() { return this.#call('chat.threads') ?? []; }

  get self() { return s1.call('chat.self'); }

  /**
   * Whether there is a transport at all, and how many other people are on it. A missing answer counts as online with
   * an unknown number of peers: hiding somebody's conversations because one call came back empty is the worse of the
   * two failures, and a made-up peer count would put a "nobody else is here" notice on a lobby that is full.
   */
  get status() { return this.#call('chat.status') ?? { online: true, peers: -1 }; }

  thread(id) { return this.#call('chat.thread', id); }

  read(id) { s1.call('chat.read', id); }

  /** Returns the mod's verdict: 'ok', or 'error' when it refused the message. */
  send(id, text) { return s1.call('chat.send', `${id}\n${text}`); }
}

/** The app: what is on screen, and what to do when it changes. */
class WhatsDab {
  #chat = new Chat();
  #current = s1.storage.get('thread', GROUP);
  #filter = '';

  /** Portrait only: the two panes cannot both fit, so one of them is on screen at a time. */
  #pane = 'list';

  /** Set while the mod reports no transport, and then nothing else is on screen. */
  #offline = false;

  /**
   * How many other people are reachable, as of the last render. Kept on the instance rather than passed down, because
   * the search box redraws the list on its own without going through render() - and a keystroke must not be able to
   * make "you're the only one here" appear or vanish. Starts unknown rather than zero, so the notice cannot flash up
   * before the mod has been asked anything.
   */
  #peers = -1;

  /**
   * What the conversation looked like the last time it was pinned to its newest message: which thread, and how many
   * messages were in it. Re-pinning on every render would drag the player back down mid-scroll every time anything at
   * all changed - a search keystroke, an unread badge, someone starting to type in a different thread.
   */
  #pinned = null;

  #app = $('app');
  #threads = $('threads');
  #messages = $('messages');
  #entry = $('entry');
  #total = $('total');

  start() {
    $('send').addEventListener('click', () => this.#send());

    // Enter is how anyone actually sends a message; the button is the fallback, not the other way round.
    this.#entry.addEventListener('keydown', (e) => {
      if (e.key === 'Enter') this.#send();
    });
    $('search').addEventListener('input', (e) => {
      this.#filter = e.value ?? '';
      this.#renderThreads();
    });

    $('back').addEventListener('click', () => this.#show('list'));

    // Right-click and Escape both arrive here, exactly as they do in the vanilla apps: inside a conversation they
    // step back to the list, and only from the list do they close WhatsDab. Not calling preventDefault is what
    // lets the app close, so the branch below is the whole contract.
    //
    // Landscape shows both panes, so there is nothing to step back FROM and the press must close the app - without
    // the orientation check it would silently switch a pane nobody can see and the app would stop closing. The
    // offline screen is the same case: it replaces both panes, so there is no list underneath it to return to.
    document.addEventListener('back', (e) => {
      if (this.#offline || s1.orientation !== 'portrait' || this.#pane !== 'chat') return;
      e.preventDefault();
      this.#show('list');
    });

    // Landscape always has the conversation on screen, so turning to portrait has to land on it. Landing on the
    // list would throw away what the player was reading in order to show them something they were not looking at.
    document.addEventListener('orientationchange', (e) => {
      if (e.value === 'portrait') this.#show('chat');
      this.render();
    });

    // Raised by the mod whenever anything changed: a reply arrived, a message was confirmed, someone started typing.
    s1.on('chat.changed', () => this.render());

    this.#show(this.#pane);
    this.render();
    console.log('WhatsDab ready for', this.#chat.self, 'in', s1.orientation);
  }

  /** Which pane portrait shows. Landscape ignores the class entirely and shows both. */
  #show(pane) {
    this.#pane = pane;
    this.#paint();
  }

  /** The app's whole visual state in one attribute: which pane portrait is on, and whether there is a chat at all. */
  #paint() {
    this.#app.className = this.#offline
      ? 'app on-offline'
      : this.#pane === 'chat' ? 'app on-chat' : 'app';
  }

  /**
   * Is the conversation actually in front of the player? Landscape always has it on screen; portrait only while the
   * chat pane is pushed. It is the difference between reading a message and merely having the app open, and the old
   * WhatsDab drew the same line - a message that arrives while you are looking at the thread list is unread.
   */
  #watching() { return !this.#offline && (s1.orientation !== 'portrait' || this.#pane === 'chat'); }

  render() {
    const { online, peers } = this.#chat.status;
    const offline = !online;

    if (offline !== this.#offline) {
      this.#offline = offline;
      this.#paint();
    }

    // Nothing behind the offline screen is on show, so nothing behind it is worth asking the mod about.
    if (this.#offline) return;

    this.#peers = peers;

    // Reading a conversation you are looking at is what "read" means. Without this the badge on the open thread
    // keeps climbing while you are actively replying in it, which is the one place a chat must never nag.
    // MarkRead only raises an event when something really changed, so this settles after one extra render.
    if (this.#watching()) this.#chat.read(this.#current);

    this.#renderThreads();
    this.#renderThread();
  }

  // ------------------------------------------------------------------ sidebar --

  /**
   * What a row says under the name. The mod sends an empty preview for a conversation nobody has spoken in and leaves
   * the sentence to the page, because what an empty thread means differs by kind: the group is where everyone is, a
   * private thread is one person. Searching matches this text rather than the raw preview, so what you can see and
   * what you can find are the same thing.
   */
  #preview({ group, preview }) {
    return preview || (group ? 'Everyone in the lobby' : 'Private chat');
  }

  #matches(thread) {
    const needle = this.#filter.toLowerCase();
    if (!needle) return true;

    return thread.name.toLowerCase().includes(needle) || this.#preview(thread).toLowerCase().includes(needle);
  }

  #renderThreads() {
    const threads = this.#chat.threads;
    const shown = threads.filter((t) => this.#matches(t));

    this.#threads.replaceChildren();

    for (const thread of shown) {
      const { id, name, group, unread, time, typing } = thread;

      const row = el('div', ['thread', unread > 0 && 'unread', id === this.#current && 'on'].filter(Boolean).join(' '));
      row.appendChild(el('div', `avatar ${group ? 'a-group' : `a${tint(name)}`}`, initial(name)));

      const body = el('div', 'thread-body');
      const top = el('div', 'thread-top');
      top.appendChild(el('div', 'thread-name', name));
      top.appendChild(el('div', 'thread-time', time));
      body.appendChild(top);

      // Someone typing outranks the last message: it is the reason to open this thread rather than read it later.
      body.appendChild(typing
        ? el('div', 'thread-preview typing-now', 'typing...')
        : el('div', 'thread-preview', this.#preview(thread)));
      row.appendChild(body);

      if (unread > 0) row.appendChild(el('div', 'pill', unread > 99 ? '99+' : unread));

      // `id` is bound per iteration, so the handler closes over THIS thread rather than the loop's last one.
      row.addEventListener('click', () => this.#open(id));

      this.#threads.appendChild(row);
    }

    if (shown.length === 0) {
      this.#threads.appendChild(el('div', 'empty', `Nothing matches "${this.#filter}"`));
    } else if (this.#peers === 0 && !this.#filter) {
      // A lobby of one still has a group thread, so the list is not empty - it is just pointless, and saying nothing
      // leaves the player wondering whether their friends failed to show up or the app failed to list them.
      this.#threads.appendChild(el('div', 'empty', "You're the only one here so far."));
    }

    const total = threads.reduce((sum, t) => sum + t.unread, 0);
    this.#total.textContent = total > 99 ? '99+' : String(total);
    this.#total.className = total > 0 ? 'badge' : 'badge empty';
  }

  // ------------------------------------------------------------------- thread --

  #renderThread() {
    let thread = this.#chat.thread(this.#current);

    // The stored conversation can be gone: thread ids are the people you were in a lobby with, and the next lobby
    // has different people. Falling back to the group is the only answer that always exists - without it the header
    // keeps its markup, the pane stays blank, and the app looks broken until you happen to click a row.
    if (!thread?.id && this.#current !== GROUP) {
      this.#current = GROUP;
      s1.storage.set('thread', GROUP);
      thread = this.#chat.thread(GROUP);
    }

    if (!thread?.id) {
      $('head-avatar').textContent = '';
      $('head-avatar').className = 'avatar a-group';
      $('head-name').textContent = '';
      $('head-sub').textContent = '';
      this.#messages.replaceChildren();
      return;
    }

    const avatar = $('head-avatar');
    avatar.textContent = initial(thread.name);
    avatar.className = `avatar ${thread.group ? 'a-group' : `a${tint(thread.name)}`}`;

    $('head-name').textContent = thread.name;
    $('head-sub').textContent = thread.typing ? 'typing...' : (thread.group ? 'group chat' : 'online');

    this.#messages.replaceChildren();

    const items = thread.messages ?? [];
    if (items.length === 0) {
      this.#messages.appendChild(el('div', 'empty', 'No messages yet. Say something.'));
      this.#pin(thread.id, 0);
      return;
    }

    for (const item of items) {
      this.#messages.appendChild(item.kind === 'day' ? el('div', 'day', item.text) : this.#bubble(thread, item));
    }

    if (thread.typing) this.#messages.appendChild(el('div', 'typing', `${clamp(thread.name, 24)} is typing...`));

    this.#pin(thread.id, items.length);
  }

  /**
   * Jump to the newest message, but only when there is a newer one to jump to. The host keeps a box's scroll offset
   * across a rebuild, so doing nothing leaves the player exactly where they were reading - which is what somebody
   * scrolled halfway up a conversation wants, and what the old WhatsDab achieved by checking whether they were
   * already at the bottom. That check is not available here (a page cannot read a scroll offset), so the test is
   * "did this conversation actually gain something", which is the case that has to win.
   *
   * The typing row is deliberately not counted. It appears and disappears on its own schedule, and yanking somebody
   * down to watch a "typing..." line is the version of this feature nobody asked for.
   */
  #pin(id, count) {
    if (this.#pinned?.id === id && this.#pinned.count === count) return;

    this.#pinned = { id, count };
    this.#messages.scrollToEnd();
  }

  #bubble(thread, { from, text, time, mine, pending }) {
    const bubble = el('div', mine ? 'bubble mine' : 'bubble');

    // The sender line only earns its space in a group chat, and never above your own messages.
    if (thread.group && !mine) bubble.appendChild(el('div', `sender c${tint(from)}`, clamp(from, 24)));

    bubble.appendChild(el('div', 'body', text));

    // A pending message is one the transport has not confirmed. The dot is the whole difference, so it sits where
    // the eye already is - next to the timestamp.
    bubble.appendChild(el('div', 'meta', `${time}${pending ? '  .' : ''}`));

    return bubble;
  }

  // -------------------------------------------------------------------- input --

  #open(id) {
    this.#current = id;
    s1.storage.set('thread', id);
    this.#chat.read(id);
    this.#show('chat');
    this.render();
  }

  #send() {
    const text = this.#entry.value?.trim();
    if (!text) return;

    // Only clear the field once the mod has actually taken the message. Throwing away what somebody typed because
    // the far side said no is the one unforgivable bug in a chat app.
    if (this.#chat.send(this.#current, text) !== 'ok') {
      console.error('the message was refused:', text);
      return;
    }

    this.#entry.value = '';
    this.render();
  }
}

new WhatsDab().start();
