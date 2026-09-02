using Yunus.Game.Core;
using Yunus.Game.Domain.Value;
namespace Yunus.Game.Domain.Ports
{
    public interface IColorPalette : IService
    {
        Rgba Next();                     // next colour, advances the cursor
        Rgba GetByIndex(int index);      // index modulo palette length
        void ResetCycle();               // reset the cursor to the start
        void Shuffle(System.Random rng); // shuffle the palette with the given RNG, reset the cursor
        int Count { get; }               // number of entries
    }
}
