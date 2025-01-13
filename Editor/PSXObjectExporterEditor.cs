using UnityEngine;
using UnityEditor;
using PSXSplash.Runtime;

[CustomEditor(typeof(PSXObjectExporter))]
public class PSXObjectExporterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        DrawDefaultInspector();

        PSXObjectExporter comp = (PSXObjectExporter)target;
        if (GUILayout.Button("Export"))
        {
            comp.Export();
        }
    }
}
