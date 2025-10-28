using Yunus.Game.Domain.Value;
using Yunus.Game.Domain.Ports;
using System;
namespace Yunus.Game.Domain.Services
{
    public sealed class DistinctColorPalette : IColorPalette
    {
       private Rgba[] palette;
       private int cursor;

        public DistinctColorPalette(Rgba[] palette)
        {

            if (palette == null || palette.Length == 0)
            {
                this.palette = new[]
                {
                    new Rgba(1f, 0f, 0f), // Kýrmýzý
                    new Rgba(0f, 1f, 0f), // Yeþil
                    new Rgba(0f, 0f, 1f), // Mavi
                };
            }
            else
            {
                this.palette = palette;
            }

            cursor = 0;
        }

        // Parametresiz: default paletle baþlat.
        public DistinctColorPalette() : this(null) { }

        // IService — yaþam döngüsü
        public void Initialize() => cursor = 0;
        public void Clean() => cursor = 0;

        public int Count => (palette == null) ? 0 : palette.Length;

        public Rgba GetByIndex(int index) 
        {
            if (Count == 0)
                return new Rgba(1f, 1f, 1f, 1f); // beyaz

            int modIndex = ((index % Count) + Count) % Count; // her zaman 0..Count-1
            return palette[modIndex];
        }
        public Rgba Next()
        {
            if (Count == 0)
                return new Rgba(1f, 1f, 1f, 1f); // beyaz
            Rgba color = palette[cursor];
            cursor = (cursor + 1) % Count; // döngüsel ilerle
            return color;
        }
        public void ResetCycle() => cursor = 0;

        public void Shuffle()
        {
            if (palette == null || palette.Length <= 1) { cursor = 0; return; }

            var rng = new Random(unchecked(Environment.TickCount ^ GetHashCode()));

            for (int i = palette.Length - 1; i > 0; i--)
            {
                int j = rng.Next(0, i + 1);
                if (j != i)
                    (palette[i], palette[j]) = (palette[j], palette[i]);
            }

            cursor = 0;
        }
    }

}
