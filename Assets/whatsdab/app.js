// WhatsDab. The whole interface: pick a thread, read it, write into it.
//
// The mod behind this never touches a widget. It answers five calls and raises one event; everything below decides
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

  thread(id) { return this.#call('chat.thread', id); }

  read(id) { s1.call('chat.read', id); }

  /** Returns the mod's verdict: 'ok', or 'error' when it refused the message. */
  send(id, text) { return s1.call('chat.send', `${id}\n${text}`); }
}

/** The app: what is on screen, and what to do when it changes. */
class WhatsDab {
  #chat = new Chat();
  #current = s1.storage.get('thread', 'everyone');
  #filter = '';

  /** Portrait only: the two panes cannot both fit, so one of them is on screen at a time. */
  #pane = 'list';

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
    // the orientation check it would silently switch a pane nobody can see and the app would stop closing.
    document.addEventListener('back', (e) => {
      if (s1.orientation !== 'portrait' || this.#pane !== 'chat') return;
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

    this.#chat.read(this.#current);
    this.#show(this.#pane);
    this.render();
    console.log('WhatsDab ready for', this.#chat.self, 'in', s1.orientation);
  }

  /** Which pane portrait shows. Landscape ignores the class entirely and shows both. */
  #show(pane) {
    this.#pane = pane;
    this.#app.className = pane === 'chat' ? 'app on-chat' : 'app';
  }

  render() {
    // Reading a conversation you are looking at is what "read" means. Without this the badge on the open thread
    // keeps climbing while you are actively replying in it, which is the one place a chat must never nag.
    // MarkRead only raises an event when something really changed, so this settles after one extra render.
    this.#chat.read(this.#current);

    this.#renderThreads();
    this.#renderThread();
  }

  // ------------------------------------------------------------------ sidebar --

  #matches({ name, preview }) {
    const needle = this.#filter.toLowerCase();
    return !needle || name.toLowerCase().includes(needle) || preview.toLowerCase().includes(needle);
  }

  #renderThreads() {
    const threads = this.#chat.threads;
    const shown = threads.filter((t) => this.#matches(t));

    this.#threads.replaceChildren();

    for (const { id, name, group, unread, preview, time, typing } of shown) {
      const row = el('div', ['thread', unread > 0 && 'unread', id === this.#current && 'on'].filter(Boolean).join(' '));
      row.appendChild(el('div', `avatar ${group ? 'a2' : `a${tint(name)}`}`, initial(name)));

      const body = el('div', 'thread-body');
      const top = el('div', 'thread-top');
      top.appendChild(el('div', 'thread-name', name));
      top.appendChild(el('div', 'thread-time', time));
      body.appendChild(top);

      // Someone typing outranks the last message: it is the reason to open this thread rather than read it later.
      body.appendChild(typing
        ? el('div', 'thread-preview typing-now', 'typing...')
        : el('div', 'thread-preview', preview));
      row.appendChild(body);

      if (unread > 0) row.appendChild(el('div', 'pill', unread > 99 ? '99+' : unread));

      // `id` is bound per iteration, so the handler closes over THIS thread rather than the loop's last one.
      row.addEventListener('click', () => this.#open(id));

      this.#threads.appendChild(row);
    }

    if (shown.length === 0) this.#threads.appendChild(el('div', 'empty', `Nothing matches "${this.#filter}"`));

    const total = threads.reduce((sum, t) => sum + t.unread, 0);
    this.#total.textContent = total > 99 ? '99+' : String(total);
    this.#total.className = total > 0 ? 'badge' : 'badge empty';
  }

  // ------------------------------------------------------------------- thread --

  #renderThread() {
    const thread = this.#chat.thread(this.#current);
    if (!thread?.id) return;

    const avatar = $('head-avatar');
    avatar.textContent = initial(thread.name);
    avatar.className = 'avatar';

    $('head-name').textContent = thread.name;
    $('head-sub').textContent = thread.typing ? 'typing...' : (thread.group ? 'group chat' : 'online');

    this.#messages.replaceChildren();

    const items = thread.messages ?? [];
    if (items.length === 0) {
      this.#messages.appendChild(el('div', 'empty', 'No messages yet. Say something.'));
      return;
    }

    for (const item of items) {
      this.#messages.appendChild(item.kind === 'day' ? el('div', 'day', item.text) : this.#bubble(thread, item));
    }

    if (thread.typing) this.#messages.appendChild(el('div', 'typing', `${thread.name} is typing...`));

    // The newest message is the one you want to see, every time.
    this.#messages.scrollToEnd();
  }

  #bubble(thread, { from, text, time, mine, pending }) {
    const bubble = el('div', mine ? 'bubble mine' : 'bubble');

    // The sender line only earns its space in a group chat, and never above your own messages.
    if (thread.group && !mine) bubble.appendChild(el('div', `sender c${tint(from)}`, from));

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
