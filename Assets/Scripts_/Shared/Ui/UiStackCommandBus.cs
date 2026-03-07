using System;

namespace Shared.Ui
{
    public enum UiStackCommandType
    {
        Push = 0,
        Pop = 1,
        Clear = 2
    }

    public struct UiStackCommand
    {
        public UiStackCommand(UiStackCommandType type, string panelId)
        {
            Type = type;
            PanelId = panelId ?? string.Empty;
        }

        public UiStackCommandType Type { get; }
        public string PanelId { get; }
    }

    public static class UiStackCommandBus
    {
        public static event Action<UiStackCommand> CommandPublished;

        public static void Publish(UiStackCommand command)
        {
            var handler = CommandPublished;
            if (handler != null)
            {
                handler(command);
            }
        }

        public static void Push(string panelId)
        {
            Publish(new UiStackCommand(UiStackCommandType.Push, panelId));
        }

        public static void Pop()
        {
            Publish(new UiStackCommand(UiStackCommandType.Pop, string.Empty));
        }

        public static void Clear()
        {
            Publish(new UiStackCommand(UiStackCommandType.Clear, string.Empty));
        }
    }
}
