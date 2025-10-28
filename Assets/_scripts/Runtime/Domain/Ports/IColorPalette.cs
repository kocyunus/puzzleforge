using Yunus.Game.Core;
using Yunus.Game.Domain.Value;
namespace Yunus.Game.Domain.Ports
{
    public interface IColorPalette : IService
    {
        Rgba Next();                 // sýradaki rengi verir, imleci ilerletir
        Rgba GetByIndex(int index);  // index mod palet uzunluðu
        void ResetCycle();           // imleci baþa al
        void Shuffle();            // paleti karýþtýrýr, imleci baþa alýr
        int Count { get; }           // palet eleman sayýsý
    }
}

