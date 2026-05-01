using UnityEngine;

namespace SwDreams.Features.UI.Adapter.Indicator
{
    public interface IWorldIndicatorTarget
    {
        Transform Transform { get; }
        string DisplayName { get; }
        Color IndicatorColor { get; }
        IndicatorPolicy Policy { get; }
        bool IsActive { get; }
    }
}
