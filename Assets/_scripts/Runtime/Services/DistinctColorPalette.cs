using Yunus.Game.Domain.Ports;
using Yunus.Game.Domain.Value;

namespace Yunus.Game.Domain.Services
{
    /// <summary>
    /// A fixed list of colours handed out by index or in cycle order. <see cref="Shuffle"/>
    /// reorders the list using a caller-supplied RNG, so colour assignment is reproducible when
    /// the caller wants it to be.
    /// </summary>
    public sealed class DistinctColorPalette : IColorPalette
    {
        private static readonly Rgba[] DefaultPalette =
        {
            new Rgba(1f, 0f, 0f), // red
            new Rgba(0f, 1f, 0f), // green
            new Rgba(0f, 0f, 1f), // blue
        };

        private readonly Rgba[] palette;
        private int cursor;

        public DistinctColorPalette(Rgba[] palette)
        {
            this.palette = (palette == null || palette.Length == 0) ? DefaultPalette : palette;
            cursor = 0;
        }

        public DistinctColorPalette() : this(null) { }

        public void Initialize() => cursor = 0;
        public void Clean() => cursor = 0;

        public int Count => palette?.Length ?? 0;

        public Rgba GetByIndex(int index)
        {
            if (Count == 0) return new Rgba(1f, 1f, 1f, 1f);

            int modIndex = ((index % Count) + Count) % Count;
            return palette[modIndex];
        }

        public Rgba Next()
        {
            if (Count == 0) return new Rgba(1f, 1f, 1f, 1f);

            Rgba color = palette[cursor];
            cursor = (cursor + 1) % Count;
            return color;
        }

        public void ResetCycle() => cursor = 0;

        public void Shuffle(System.Random rng)
        {
            cursor = 0;
            if (palette == null || palette.Length <= 1) return;

            rng ??= new System.Random();
            for (int i = palette.Length - 1; i > 0; i--)
            {
                int j = rng.Next(0, i + 1);
                (palette[i], palette[j]) = (palette[j], palette[i]);
            }
        }
    }
}
