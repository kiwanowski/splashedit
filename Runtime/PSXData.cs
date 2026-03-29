using System.Collections.Generic;
using UnityEngine;

namespace SplashEdit.RuntimeCode
{

    [CreateAssetMenu(fileName = "PSXData", menuName = "PSXSplash/PS1 Project Data")]
    [Icon("Packages/net.psxsplash.splashedit/Icons/PSXData.png")]
    public class PSXData : ScriptableObject
    {

        // Texture packing settings
        public Vector2 OutputResolution = new Vector2(320, 240);
        public bool DualBuffering = true;
        public bool VerticalBuffering = true;
        public List<ProhibitedArea> ProhibitedAreas = new List<ProhibitedArea>();
    }
}