using UnityEngine;
using UnityEditor;
using PSXSplash.RuntimeCode;

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