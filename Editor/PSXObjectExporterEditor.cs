using UnityEngine;
using UnityEditor;
using PSXSplash.RuntimeCode;
using System.IO;

namespace PSXSplash.EditorCode
{

    [CustomEditor(typeof(PSXObjectExporter))]
    public class PSXObjectExporterEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            /*
            PSXObjectExporter comp = (PSXObjectExporter)target;
            serializedObject.Update();

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.PropertyField(serializedObject.FindProperty("Mesh"));
            if (GUILayout.Button("Export mesh"))
            {
                comp.Mesh.Export(comp.gameObject);
                GUIUtility.ExitGUI();
            }
            EditorGUILayout.EndVertical();


            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.PropertyField(serializedObject.FindProperty("Texture"));
            if (GUILayout.Button("Export texture"))
            {
                ushort[] textureData = comp.Texture.ExportTexture(comp.gameObject);
                string path = EditorUtility.SaveFilePanel(
                    "Save texture data",
                    "",
                    "texture_data",
                    "bin"
                );

                if (!string.IsNullOrEmpty(path))
                {
                    using (FileStream fileStream = new FileStream(path, FileMode.Create, FileAccess.Write))
                    using (BinaryWriter writer = new BinaryWriter(fileStream))
                    {
                        foreach (ushort value in textureData)
                        {
                            writer.Write(value);
                        }
                    }
                }
                GUIUtility.ExitGUI();
            }

            if (comp.Texture.TextureType != PSXTextureType.TEX16_BPP)
            {
                if (GUILayout.Button("Export clut"))
                {
                    ushort[] clutData = comp.Texture.ExportClut(comp.gameObject);
                    string path = EditorUtility.SaveFilePanel(
                    "Save clut data",
                    "",
                    "clut_data",
                    "bin"
                );

                if (!string.IsNullOrEmpty(path))
                {
                    using (FileStream fileStream = new FileStream(path, FileMode.Create, FileAccess.Write))
                    using (BinaryWriter writer = new BinaryWriter(fileStream))
                    {
                        foreach (ushort value in clutData)
                        {
                            writer.Write(value);
                        }
                    }
                }
                    GUIUtility.ExitGUI();
                }
            }
            EditorGUILayout.EndVertical();

            serializedObject.ApplyModifiedProperties();
        */
        }
        
    }
}