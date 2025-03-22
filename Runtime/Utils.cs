using System.Runtime.InteropServices;
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
    public static class PSXTrig
    {

        public static short ConvertCoordinateToPSX(float value)
        {
            return (short)(Mathf.Clamp(value, -4f, 3.999f) * 4096);
        }

        public static short ConvertRadiansToPSX(float value)
        {
            return (short)(Mathf.Clamp(value, -4f, 3.999f) * 4096f / Mathf.PI);
        }

        public static int[,] ConvertRotationToPSXMatrix(Quaternion rotation)
        {
            float xx = rotation.x * rotation.x;
            float yy = rotation.y * rotation.y;
            float zz = rotation.z * rotation.z;
            float xy = rotation.x * rotation.y;
            float xz = rotation.x * rotation.z;
            float yz = rotation.y * rotation.z;
            float wx = rotation.w * rotation.x;
            float wy = rotation.w * rotation.y;
            float wz = rotation.w * rotation.z;

            // Create the 3x3 rotation matrix
            int[,] psxMatrix = new int[3, 3]
            {
        { ConvertToFixed12(1.0f - 2.0f * (yy + zz)), ConvertToFixed12(2.0f * (xy - wz)), ConvertToFixed12(2.0f * (xz + wy)) },
        { ConvertToFixed12(2.0f * (xy + wz)), ConvertToFixed12(1.0f - 2.0f * (xx + zz)), ConvertToFixed12(2.0f * (yz - wx)) },
        { ConvertToFixed12(2.0f * (xz - wy)), ConvertToFixed12(2.0f * (yz + wx)), ConvertToFixed12(1.0f - 2.0f * (xx + yy)) }
            };

            return psxMatrix;
        }

        private static int ConvertToFixed12(float value)
        {
            return (int)(value * 4096.0f); // 2^12 = 4096
        }
    }
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TPageAttr
    {
        public ushort info;

        public TPageAttr SetPageX(byte x)
        {
            info &= 0xFFF0; // Clear lower 4 bits
            x &= 0x0F; // Ensure only lower 4 bits are used
            info |= x;
            return this;
        }

        public TPageAttr SetPageY(byte y)
        {
            info &= 0xFFEF; // Clear bit 4
            y &= 0x01; // Ensure only lower 1 bit is used
            info |= (ushort)(y << 4);
            return this;
        }

        public TPageAttr Set(SemiTrans trans)
        {
            info &= 0xFF9F; // Clear bits 5 and 6
            uint t = (uint)trans;
            info |= (ushort)(t << 5);
            return this;
        }

        public TPageAttr Set(ColorMode mode)
        {
            info &= 0xFE7F; // Clear bits 7 and 8
            uint m = (uint)mode;
            info |= (ushort)(m << 7);
            return this;
        }

        public TPageAttr SetDithering(bool dithering)
        {
            if (dithering)
                info |= 0x0200;
            else
                info &= 0xFDFF;
            return this;
        }

        public TPageAttr DisableDisplayArea()
        {
            info &= 0xFBFF; // Clear bit 10
            return this;
        }

        public TPageAttr EnableDisplayArea()
        {
            info |= 0x0400; // Set bit 10
            return this;
        }

        public override string ToString() => $"Info: 0x{info:X4}";
    }

    // Define the enums for SemiTrans and ColorMode (assuming their values)
    public enum SemiTrans : uint
    {
        None = 0,
        Type1 = 1,
        Type2 = 2,
        Type3 = 3
    }

    public enum ColorMode : uint
    {
        Mode4Bit = 0,
        Mode8Bit = 1,
        Mode16Bit = 2
    }

}

