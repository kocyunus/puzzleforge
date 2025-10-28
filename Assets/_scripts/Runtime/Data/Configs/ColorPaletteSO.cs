using UnityEngine;
using Yunus.Game.Domain.Value;

namespace Yunus.Game.Data
{
    [CreateAssetMenu(menuName = "Game/Color Palette", fileName = "ColorPaletteSO")]
    public class ColorPaletteSO : ScriptableObject
{
    public Color[] colors;
    public Rgba[] ToRgba()
    {
        if (colors == null || colors.Length == 0) return null;

        var arr = new Rgba[colors.Length];
        for (int i = 0; i < colors.Length; i++)
        {
            var c = colors[i];
            arr[i] = new Rgba(c.r, c.g, c.b, c.a);
        }
        return arr;
    }
    }
}
