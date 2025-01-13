using UnityEngine;
using UnityEditor;
using PSXSplash.RuntimeCode;

namespace PSXSplash.EditorCode
{

    [CustomEditor(typeof(PSXObjectExporter))]
    public class PSXObjectExporterEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            PSXObjectExporter comp = (PSXObjectExporter)target;
            serializedObject.Update();

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.PropertyField(serializedObject.FindProperty("Mesh"));
            if (GUILayout.Button("Export mesh"))
            {
                comp.Mesh.Export(comp.gameObject);
            }
            EditorGUILayout.EndVertical();


            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.PropertyField(serializedObject.FindProperty("Texture"));
            if (GUILayout.Button("Export texture"))
            {
                comp.Texture.Export(comp.gameObject);
            }
            EditorGUILayout.EndVertical();

            serializedObject.ApplyModifiedProperties();

        }
    }
}