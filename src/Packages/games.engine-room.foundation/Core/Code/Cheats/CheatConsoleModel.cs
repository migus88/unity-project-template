#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;

namespace Core.Cheats
{
    internal sealed class CheatConsoleModel
    {
        public const int MaxOutputLines = 200;
        public const string Prompt = "> ";

        public string Output => string.Join("\n", _output);

        private bool _isCycling;
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
            ResetCompletion();
            return _history.Previous(currentLine);
        }

        public string? RecallNext()
        {
            ResetCompletion();
            return _history.Next();
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

            var completion = _completer.Complete(_cycleBase, _cycleIndex);
            _isCycling = completion.IsCycle;
            _lastCompletion = completion.Line;
            return completion.Line;
        }

        public CheatSuggestions Suggest(string line)
        {
            if (_isCycling && line == _lastCompletion)
            {
                var candidates = _completer.Suggest(_cycleBase);
                return new CheatSuggestions(candidates, candidates.Count == 0 ? -1 : _cycleIndex % candidates.Count);
            }

            return new CheatSuggestions(_completer.Suggest(line), -1);
        }

        private void ResetCompletion()
        {
            _isCycling = false;
            _lastCompletion = string.Empty;
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
