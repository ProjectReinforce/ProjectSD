using System;

namespace Shared.Ui
{
    public enum UiStackCommandType
    {
        Push = 0,
        Pop = 1,
        Clear = 2
    }

    public readonly struct UiStackCommand
    {
        public UiStackCommand(UiStackCommandType type, string panelId)
        {
            Type = type;
            PanelId = panelId ?? string.Empty;
        }

        public UiStackCommandType Type { get; }
        public string PanelId { get; }
    }

    public interface IUiCommandPublisher
    {
        void Push(string panelId);
        void Pop();
        void Clear();
    }

    public interface IUiCommandSubscriber
    {
        void Subscribe(Action<UiStackCommand> handler);
        void Unsubscribe(Action<UiStackCommand> handler);
    }

    public sealed class UiStackCommandBus : IUiCommandPublisher, IUiCommandSubscriber
    {
        private Action<UiStackCommand> _commandPublished;

        public void Subscribe(Action<UiStackCommand> handler)
        {
            _commandPublished += handler;
        }

        public void Unsubscribe(Action<UiStackCommand> handler)
        {
            _commandPublished -= handler;
        }

        public void Push(string panelId)
        {
            Publish(new UiStackCommand(UiStackCommandType.Push, panelId));
        }

        public void Pop()
        {
            Publish(new UiStackCommand(UiStackCommandType.Pop, string.Empty));
        }

        public void Clear()
        {
            Publish(new UiStackCommand(UiStackCommandType.Clear, string.Empty));
        }

        private void Publish(UiStackCommand command)
        {
            _commandPublished?.Invoke(command);
        }
    }
}
