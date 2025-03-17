using System.Collections.Generic;
using UnityEngine;

namespace SplashEdit.RuntimeCode
{
    [CreateAssetMenu(fileName = "PSXData", menuName = "Scriptable Objects/PSXData")]
    public class PSXData : ScriptableObject
    {
        public Vector2 OutputResolution = new Vector2(320, 240);
        public bool DualBuffering = true;
        public bool VerticalBuffering = true;
        public List<ProhibitedArea> ProhibitedAreas = new List<ProhibitedArea>();
    }
}