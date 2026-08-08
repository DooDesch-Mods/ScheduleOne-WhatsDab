// The stand-in data and handlers for WhatsDab's browser preview.
//
// Everything around it - the stage, the fenced DOM, the hot reload, the back and orientation events - is shared and
// lives in sideload-preview.js. Only the parts that are WhatsDab are here.
//
// The chat data mirrors WhatsDab/Chat/ChatModel.cs. If the two drift apart the preview lies, so the seed below is
// deliberately short and boring; anything subtle belongs in a test rather than here.

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

// The bridge, handed over by the shell before the page runs. It is how this file pushes at the page instead of only
// answering it - which is what the mod does when a message arrives.
let host = null;
export const ready = (s1) => { host = s1; };

const changed = () => host?.emit('chat.changed', String(totalUnread()));

const find = (id) => threads.find((t) => t.id.toLowerCase() === String(id).toLowerCase()) ?? null;
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

export function call(name, argument = '') {
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
      console.warn(`[preview] no stand-in for s1.call("${name}")`);
      return '';
  }
}

// The states the game reaches by itself and a browser cannot: the transport going away, and the lobby emptying out.
// Without a handle on them the offline screen and the lobby-of-one note are the two parts of the app that can only
// be looked at in the game, which is the trip the preview exists to avoid.
export const scenarios = {
  'in a lobby': () => { online = true; changed(); },
  'offline': () => { online = false; changed(); },
  'lobby of one': () => { threads.length = 1; changed(); },

  // What Enter does in the game: the mod raises the phone and names the thread to land in. There is no phone to
  // raise here, so the preview delivers the second half - which is the half the page owns and can get wrong.
  'compose to Everyone': (s1) => s1.emit('chat.compose', 'everyone'),
};
