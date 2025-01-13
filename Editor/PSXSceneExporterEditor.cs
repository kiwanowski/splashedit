using UnityEngine;
using UnityEditor;
using PSXSplash.Runtime;

[CustomEditor(typeof(PSXSceneExporter))]
public class PSXSceneExporterEditor : Editor {
    public override void OnInspectorGUI() {
        base.OnInspectorGUI();
        DrawDefaultInspector();
        
        PSXSceneExporter comp = (PSXSceneExporter)target;
        if(GUILayout.Button("Export")) {
            comp.Export();
        }

    }
}