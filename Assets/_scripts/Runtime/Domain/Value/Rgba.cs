namespace Yunus.Game.Domain.Value
{
    public readonly struct Rgba
    {
        public readonly float R;
        public readonly float G;
        public readonly float B;
        public readonly float A;

        // 0..1 aralýðýnda RGBA, alpha varsayýlan 1
        public Rgba(float r, float g, float b, float a = 1f)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }
    }
}
