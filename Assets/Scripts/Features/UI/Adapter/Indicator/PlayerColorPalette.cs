using UnityEngine;

namespace SwDreams.Features.UI.Adapter.Indicator
{
    public static class PlayerColorPalette
    {
        private static readonly Color[] Palette =
        {
            new Color(0.30f, 0.69f, 1.00f),
            new Color(1.00f, 0.45f, 0.45f),
            new Color(0.45f, 0.85f, 0.45f),
            new Color(1.00f, 0.85f, 0.30f),
        };

        public static Color Get(int actorNumber)
            => Palette[((actorNumber - 1) % Palette.Length + Palette.Length) % Palette.Length];
    }
}
