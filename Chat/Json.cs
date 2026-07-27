using System.Text;

namespace WhatsDab.Chat
{
    /// <summary>
    /// Just enough JSON to answer an <c>s1.call</c>. Strings are the only thing that crosses the bridge, so a mod
    /// needs a writer; it does not need a parser, because the page has <c>JSON.parse</c> built in and sends anything
    /// structured back the same way.
    ///
    /// Kept in the example on purpose: this is the shape of the glue a real mod writes once, and it is thirty lines
    /// rather than a dependency.
    /// </summary>
    internal sealed class Json
    {
        private readonly StringBuilder _sb = new StringBuilder();
        private bool _empty = true;
        private string _closed;

        private Json(char open) => _sb.Append(open);

        internal static Json Object() => new Json('{');

        internal static Json Array() => new Json('[');

        internal Json Add(string key, string value)
        {
            Separate();
            Quote(key);
            _sb.Append(':');
            Quote(value);
            return this;
        }

        internal Json Add(string key, long value)
        {
            Separate();
            Quote(key);
            _sb.Append(':').Append(value);
            return this;
        }

        internal Json Add(string key, bool value)
        {
            Separate();
            Quote(key);
            _sb.Append(':').Append(value ? "true" : "false");
            return this;
        }

        /// <summary>Nest a finished object or array under a key.</summary>
        internal Json Add(string key, Json value)
        {
            Separate();
            Quote(key);
            _sb.Append(':').Append(value.Close());
            return this;
        }

        /// <summary>Append to an array.</summary>
        internal Json Item(Json value)
        {
            Separate();
            _sb.Append(value.Close());
            return this;
        }

        /// <summary>
        /// Finish the document and hand back the text. Idempotent: nesting a builder calls this, and so does
        /// ToString(), so a value that is closed twice must not grow a second bracket - which is a corruption that
        /// only shows up as a parse error on the far side of the bridge.
        /// </summary>
        internal string Close()
        {
            if (_closed != null) return _closed;

            _sb.Append(_sb[0] == '{' ? '}' : ']');
            return _closed = _sb.ToString();
        }

        public override string ToString() => Close();

        private void Separate()
        {
            // Writing into a finished document would silently produce something that is no longer JSON.
            if (_closed != null)
                throw new InvalidOperationException("this JSON value is already closed and cannot be added to");

            if (!_empty) _sb.Append(',');
            _empty = false;
        }

        private void Quote(string value)
        {
            _sb.Append('"');
            foreach (char c in value ?? "")
            {
                switch (c)
                {
                    case '"': _sb.Append("\\\""); break;
                    case '\\': _sb.Append("\\\\"); break;
                    case '\n': _sb.Append("\\n"); break;
                    case '\r': _sb.Append("\\r"); break;
                    case '\t': _sb.Append("\\t"); break;
                    default:
                        if (c < ' ') _sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else _sb.Append(c);
                        break;
                }
            }
            _sb.Append('"');
        }
    }
}
