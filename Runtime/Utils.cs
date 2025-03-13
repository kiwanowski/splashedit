using UnityEngine;

namespace PSXSplash.RuntimeCode
{
    public class ProhibitedArea
    {
        public int X;
        public int Y;
        public int Width;
        public int Height;

        public static ProhibitedArea FromUnityRect(Rect rect)
        {
            return new ProhibitedArea
            {
                X = Mathf.RoundToInt(rect.x),
                Y = Mathf.RoundToInt(rect.y),
                Width = Mathf.RoundToInt(rect.width),
                Height = Mathf.RoundToInt(rect.height)
            };
        }

        public Rect ToUnityRect()
        {
            return new Rect(X, Y, Width, Height);
        }
    }
}