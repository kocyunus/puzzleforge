using System.Collections.Generic;
using UnityEngine;
using Yunus.Game.Core;
using Yunus.Game.Gameplay;

namespace Yunus.Game.Services
{
    public sealed class ShapeScatterOptions
    {
        public float MinSpacing = 1f;  // minimum gap between pieces (local units)
        public float RectScale = 1f;   // shrink the area toward its centre (1 = full, 0.65 = tight)
        public float Z = 0f;           // local z for the placed pieces
    }

    public interface IShapeScatter : IService
    {
        /// <summary>
        /// Randomly lays out the given <see cref="ShapeData"/> roots inside <paramref name="localRect"/>
        /// (in the shapes' parent local space). Layout is cosmetic and always randomised.
        /// </summary>
        void Scatter(IList<ShapeData> shapes, Rect localRect, ShapeScatterOptions opts = null);
    }
}
