using System.Collections.Generic;
using UnityEngine;
using Yunus.Game.Gameplay;

namespace Yunus.Game.Services
{
    public sealed class ShapeScatterService : IShapeScatter
    {
        private System.Random _rng;

        public void Initialize() => _rng = new System.Random();
        public void Clean() => _rng = null;

        public void Scatter(IList<ShapeData> shapes, Rect localRect, ShapeScatterOptions opts = null)
        {
            if (shapes == null || shapes.Count == 0) return;
            opts ??= new ShapeScatterOptions();

            // Shrink the area toward its centre for a tighter layout.
            if (opts.RectScale > 0f && opts.RectScale < 1f)
            {
                var c = localRect.center;
                var w = localRect.width * opts.RectScale;
                var h = localRect.height * opts.RectScale;
                localRect = new Rect(c.x - w * 0.5f, c.y - h * 0.5f, w, h);
            }

            var rng = _rng ?? new System.Random();
            var placed = new List<Vector2>(shapes.Count);
            const int MaxTries = 64;
            float minSq = opts.MinSpacing * opts.MinSpacing;

            foreach (var s in shapes)
            {
                if (s == null) continue;

                Vector2 chosen = default;
                bool ok = false;

                for (int t = 0; t < MaxTries; t++)
                {
                    float rx = (float)rng.NextDouble();
                    float ry = (float)rng.NextDouble();
                    var p = new Vector2(
                        localRect.xMin + rx * localRect.width,
                        localRect.yMin + ry * localRect.height
                    );

                    ok = true;
                    for (int i = 0; i < placed.Count; i++)
                    {
                        if ((placed[i] - p).sqrMagnitude < minSq) { ok = false; break; }
                    }
                    if (ok) { chosen = p; break; }
                }

                if (!ok) chosen = localRect.center; // fallback

                placed.Add(chosen);
                s.transform.localPosition = new Vector3(chosen.x, chosen.y, opts.Z);
            }
        }
    }
}
