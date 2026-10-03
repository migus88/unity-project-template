#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;

namespace Core.Cheats
{
    internal sealed class CheatConsoleModel
    {
        public const int MaxOutputLines = 200;
        public const string Prompt = "> ";

        public string Output => string.Join("\n", _output);

        private bool _isCycling;
        private bool _isDismissed;
        private bool _isSuppressed;
        private int _selected = -1;
        private int _cycleIndex;
        private string _cycleBase = string.Empty;
        private string _lastCompletion = string.Empty;

        private readonly CheatCompleter _completer;
        private readonly CheatHistory _history = new();
        private readonly List<string> _output = new();

        public CheatConsoleModel(CheatRegistry registry)
        {
            _completer = new CheatCompleter(registry);
        }

        public void AddCommand(string line)
        {
            _history.Add(line);
            ResetCompletion();
            ResetSuggestions();
            AppendOutput(CheatReplyFormatter.Escape(Prompt + line.Replace('\n', ' ').Trim()));
        }

        public void AddReply(string reply)
        {
            AppendOutput(CheatReplyFormatter.ToRichText(reply));
        }

        public void ClearOutput()
        {
            _output.Clear();
        }

        public string? RecallPrevious(string currentLine)
        {
            return Recalled(_history.Previous(currentLine));
        }

        public string? RecallNext()
        {
            return Recalled(_history.Next());
        }

        public string Complete(string line)
        {
            if (_isCycling && line == _lastCompletion)
            {
                _cycleIndex++;
            }
            else
            {
                _cycleBase = line;
                _cycleIndex = 0;
            }

            ResetSuggestions();
            var completion = _completer.Complete(_cycleBase, _cycleIndex);
            _isCycling = completion.IsCycle;
            _lastCompletion = completion.Line;
            return completion.Line;
        }

        public CheatSuggestions Suggest(string line)
        {
            if (_isDismissed || _isSuppressed)
            {
                return new CheatSuggestions(Array.Empty<string>(), -1);
            }

            var candidates = _completer.Suggest(ListBase(line));
            return new CheatSuggestions(candidates, Highlighted(line, candidates.Count));
        }

        public bool HasSuggestions(string line)
        {
            return Suggest(line).Values.Count > 0;
        }

        public void MoveSelection(string line, int step)
        {
            var count = Suggest(line).Values.Count;

            if (count == 0)
            {
                return;
            }

            var current = Highlighted(line, count);

            if (current < 0)
            {
                _selected = step > 0 ? 0 : count - 1;
            }
            else
            {
                _selected = (((current + step) % count) + count) % count;
            }
        }

        public string? AcceptSelection(string line)
        {
            if (_selected < 0)
            {
                return null;
            }

            var lineBase = ListBase(line);
            var candidates = _completer.Suggest(lineBase);

            if (_selected >= candidates.Count)
            {
                return null;
            }

            var accepted = _completer.Accept(lineBase, candidates[_selected]);
            ResetCompletion();
            ResetSuggestions();
            return accepted;
        }

        public void DismissSuggestions()
        {
            _isDismissed = true;
            _selected = -1;
        }

        public void EditLine()
        {
            ResetSuggestions();
        }

        private string? Recalled(string? line)
        {
            ResetCompletion();
            ResetSuggestions();
            _isSuppressed = line != null;
            return line;
        }

        private string ListBase(string line)
        {
            return _isCycling && line == _lastCompletion ? _cycleBase : line;
        }

        private int Highlighted(string line, int count)
        {
            if (count == 0)
            {
                return -1;
            }

            if (_selected >= 0)
            {
                return _selected < count ? _selected : -1;
            }

            return _isCycling && line == _lastCompletion ? _cycleIndex % count : -1;
        }

        private void ResetCompletion()
        {
            _isCycling = false;
            _lastCompletion = string.Empty;
        }

        private void ResetSuggestions()
        {
            _selected = -1;
            _isDismissed = false;
            _isSuppressed = false;
        }

        private void AppendOutput(string text)
        {
            if (text.Length == 0)
            {
                return;
            }

            _output.AddRange(text.Split('\n'));

            if (_output.Count > MaxOutputLines)
            {
                _output.RemoveRange(0, _output.Count - MaxOutputLines);
            }
        }
    }
}
#endif
