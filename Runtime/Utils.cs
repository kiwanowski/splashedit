using UnityEngine;

namespace SplashEdit.RuntimeCode
{
    /// <summary>
    /// Represents a prohibited area in PlayStation 2D VRAM where textures should not be packed.
    /// This class provides conversion methods to and from Unity's Rect structure.
    /// </summary>
    public class ProhibitedArea
    {
        // X and Y coordinates of the prohibited area in VRAM.
        public int X;
        public int Y;
        // Width and height of the prohibited area.
        public int Width;
        public int Height;

        /// <summary>
        /// Creates a ProhibitedArea instance from a Unity Rect.
        /// The floating-point values of the Rect are rounded to the nearest integer.
        /// </summary>
        /// <param name="rect">The Unity Rect representing the prohibited area.</param>
        /// <returns>A new ProhibitedArea with integer dimensions.</returns>
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

        /// <summary>
        /// Converts the ProhibitedArea back into a Unity Rect.
        /// </summary>
        /// <returns>A Unity Rect with the same area as defined by this ProhibitedArea.</returns>
        public Rect ToUnityRect()
        {
            return new Rect(X, Y, Width, Height);
        }
    }
}
