using System.Collections.Generic;

namespace Shared.Ui
{
    public sealed class UiStack
    {
        private readonly List<string> _panels = new List<string>();

        public IReadOnlyList<string> Panels => _panels;

        public string Top => _panels.Count > 0 ? _panels[_panels.Count - 1] : string.Empty;

        public bool Push(string panelId)
        {
            if (string.IsNullOrWhiteSpace(panelId))
            {
                return false;
            }

            var key = panelId.Trim();
            _panels.RemoveAll(id => id == key);
            _panels.Add(key);
            return true;
        }

        public bool Pop()
        {
            if (_panels.Count == 0)
            {
                return false;
            }

            _panels.RemoveAt(_panels.Count - 1);
            return true;
        }

        public void Clear()
        {
            _panels.Clear();
        }
    }
}
