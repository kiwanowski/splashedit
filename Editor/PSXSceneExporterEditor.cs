using UnityEngine;
using UnityEditor;
using SplashEdit.RuntimeCode;

namespace SplashEdit.EditorCode
{
    [CustomEditor(typeof(PSXSceneExporter))]
    public class PSXSceneExporterEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            PSXSceneExporter comp = (PSXSceneExporter)target;
            if (GUILayout.Button("Export"))
            {
                comp.Export();
            }

        }
    }
}