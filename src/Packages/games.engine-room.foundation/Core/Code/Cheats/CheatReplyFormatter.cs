#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Core.Cheats
{
    internal static class CheatReplyFormatter
    {
        public const float CharacterWidthEm = 0.55f;
        public const float GapEm = 1.5f;
        public const int MaxColumnCharacters = 32;

        public static string ToRichText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var lines = text.Split('\n');
            var positions = ColumnPositions(lines);
            var rich = new StringBuilder();

            for (var i = 0; i < lines.Length; i++)
            {
                if (i > 0)
                {
                    rich.Append('\n');
                }

                AppendLine(rich, lines[i], positions);
            }

            return rich.ToString();
        }

        public static string Escape(string text)
        {
            return text.Length == 0 ? string.Empty : string.Concat("<noparse>", text, "</noparse>");
        }

        private static void AppendLine(StringBuilder rich, string line, IReadOnlyList<float> positions)
        {
            var cells = line.Split(CheatHelpFormatter.Column);
            rich.Append(Escape(cells[0]));

            for (var column = 1; column < cells.Length; column++)
            {
                if (cells[column - 1].Length > MaxColumnCharacters)
                {
                    rich.Append(' ');
                }
                else
                {
                    rich.Append("<pos=").Append(positions[column].ToString("0.##", CultureInfo.InvariantCulture)).Append("em>");
                }

                rich.Append(Escape(cells[column]));
            }
        }

        private static IReadOnlyList<float> ColumnPositions(string[] lines)
        {
            var widths = new List<int>();

            foreach (var line in lines)
            {
                var cells = line.Split(CheatHelpFormatter.Column);

                for (var column = 0; column < cells.Length - 1; column++)
                {
                    if (widths.Count <= column)
                    {
                        widths.Add(0);
                    }

                    widths[column] = Math.Max(widths[column], Math.Min(cells[column].Length, MaxColumnCharacters));
                }
            }

            var positions = new float[widths.Count + 1];

            for (var column = 1; column < positions.Length; column++)
            {
                positions[column] = positions[column - 1] + widths[column - 1] * CharacterWidthEm + GapEm;
            }

            return positions;
        }
    }
}
#endif
