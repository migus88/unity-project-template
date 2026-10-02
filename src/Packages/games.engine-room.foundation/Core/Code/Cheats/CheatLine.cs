#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Text;

namespace Core.Cheats
{
    public sealed record CheatLine
    {
        public const char Quote = '"';

        public string Text { get; }

        public IReadOnlyList<CheatToken> Tokens { get; }

        public bool EndsWithSpace { get; }

        public string Name => Tokens.Count == 0 ? string.Empty : Tokens[0].Value.ToLowerInvariant();

        public bool IsEmpty => Tokens.Count == 0;

        private CheatLine(string text, IReadOnlyList<CheatToken> tokens, bool endsWithSpace)
        {
            Text = text;
            Tokens = tokens;
            EndsWithSpace = endsWithSpace;
        }

        public static CheatLine Parse(string? text)
        {
            var clean = (text ?? string.Empty).Replace("`", string.Empty);
            var tokens = new List<CheatToken>();
            var index = 0;

            while (index < clean.Length)
            {
                if (char.IsWhiteSpace(clean[index]))
                {
                    index++;
                    continue;
                }

                var token = clean[index] == Quote ? ReadQuoted(clean, index) : ReadPlain(clean, index);
                tokens.Add(token);
                index = token.End;
            }

            var lastEnd = tokens.Count == 0 ? 0 : tokens[^1].End;
            return new CheatLine(clean, tokens, clean.Length > lastEnd);
        }

        public string JoinValues(int firstToken)
        {
            if (firstToken < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(firstToken));
            }

            var joined = new StringBuilder();

            for (var i = firstToken; i < Tokens.Count; i++)
            {
                if (joined.Length > 0)
                {
                    joined.Append(' ');
                }

                joined.Append(Tokens[i].Value);
            }

            return joined.ToString();
        }

        private static CheatToken ReadQuoted(string text, int start)
        {
            var close = text.IndexOf(Quote, start + 1);

            if (close < 0)
            {
                return new CheatToken(text.Substring(start + 1), start, text.Length, true);
            }

            return new CheatToken(text.Substring(start + 1, close - start - 1), start, close + 1, true);
        }

        private static CheatToken ReadPlain(string text, int start)
        {
            var end = start;

            while (end < text.Length && !char.IsWhiteSpace(text[end]))
            {
                end++;
            }

            return new CheatToken(text.Substring(start, end - start), start, end, false);
        }
    }
}
#endif
