// Services/IShapeScatter.cs
using System.Collections.Generic;
using UnityEngine;
using Yunus.Game.Core;
using Yunus.Game.Gameplay;

namespace Yunus.Game.Services
{
    public sealed class ShapeScatterOptions
    {
        public float MinSpacing = 1f;   // parçalar arasý minimum mesafe (local units)
        public float RectScale = 1f;   // alaný merkezden küçültme (1=full, 0.65=yakýn)
        public float Z = 0f;   // local z
        public int? Seed = null; // deterministik istersen
    }

    public interface IShapeScatter : IService
    {
        /// <summary>
        /// Verilen ShapeData root'larýný, parent local'ýnda "localRect" içine rastgele daðýtýr.
        /// </summary>
        void Scatter(IList<ShapeData> shapes, Rect localRect, ShapeScatterOptions opts = null);
    }
}
