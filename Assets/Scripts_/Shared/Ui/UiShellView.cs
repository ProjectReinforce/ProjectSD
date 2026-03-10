using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shared.Ui
{
    public sealed class UiShellView : MonoBehaviour
    {
        [SerializeField] private GameObject _modalBlocker;
        [SerializeField] private List<PanelBinding> _panels = new List<PanelBinding>();

        private readonly UiStack _stack = new UiStack();

        private IUiCommandSubscriber _commandBus;
        private Action<UiStackCommand> _onCommand;

        public void Initialize(IUiCommandSubscriber commandBus)
        {
            _commandBus = commandBus;
            _onCommand = HandleCommand;
            _commandBus.Subscribe(_onCommand);
        }

        private void OnDestroy()
        {
            if (_commandBus == null) return;
            _commandBus.Unsubscribe(_onCommand);
        }

        private void HandleCommand(UiStackCommand command)
        {
            switch (command.Type)
            {
                case UiStackCommandType.Push:
                    _stack.Push(command.PanelId);
                    break;
                case UiStackCommandType.Pop:
                    _stack.Pop();
                    break;
                case UiStackCommandType.Clear:
                    _stack.Clear();
                    break;
            }

            Render();
        }

        private void Render()
        {
            var top = _stack.Top;
            var hasTop = !string.IsNullOrEmpty(top);

            for (var i = 0; i < _panels.Count; i++)
            {
                var panel = _panels[i];
                if (panel == null || panel.Root == null)
                {
                    continue;
                }

                panel.Root.SetActive(hasTop && panel.PanelId == top);
            }

            if (_modalBlocker != null)
            {
                _modalBlocker.SetActive(hasTop);
            }
        }

        [Serializable]
        public sealed class PanelBinding
        {
            [SerializeField] private string _panelId;
            [SerializeField] private GameObject _root;

            public string PanelId => (_panelId ?? string.Empty).Trim();
            public GameObject Root => _root;
        }
    }
}
