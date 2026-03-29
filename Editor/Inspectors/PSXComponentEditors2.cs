// I raged that my scrollwheel was broken while writing this and that's why it's 2 files.


using UnityEngine;
using UnityEditor;
using SplashEdit.RuntimeCode;

namespace SplashEdit.EditorCode
{
    /// <summary>
    /// Custom inspector for PSXAudioClip component.
    /// </summary>
    [CustomEditor(typeof(PSXAudioClip))]
    public class PSXAudioClipEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Header card
            PSXEditorStyles.BeginCard();
            EditorGUILayout.LabelField("PSX Audio Clip", PSXEditorStyles.CardHeaderStyle);

            PSXAudioClip audioClip = (PSXAudioClip)target;

            EditorGUILayout.BeginHorizontal();
            if (audioClip.Clip != null)
                PSXEditorStyles.DrawStatusBadge("Clip Set", PSXEditorStyles.Success, 70);
            else
                PSXEditorStyles.DrawStatusBadge("No Clip", PSXEditorStyles.Warning, 70);

            if (audioClip.Loop)
                PSXEditorStyles.DrawStatusBadge("Loop", PSXEditorStyles.AccentCyan, 50);
            EditorGUILayout.EndHorizontal();

            PSXEditorStyles.EndCard();

            EditorGUILayout.Space(4);

            // Properties card
            PSXEditorStyles.BeginCard();
            EditorGUILayout.LabelField("Clip Settings", PSXEditorStyles.CardHeaderStyle);
            PSXEditorStyles.DrawSeparator(2, 4);

            EditorGUILayout.PropertyField(serializedObject.FindProperty("ClipName"), new GUIContent("Clip Name",
                "Name used to identify this clip in Lua (Audio.Play(\"name\"))."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("Clip"), new GUIContent("Audio Clip",
                "Unity AudioClip to convert to PS1 SPU ADPCM format."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("SampleRate"), new GUIContent("Sample Rate",
                "Target sample rate for the PS1 (lower = smaller, max 44100)."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("Loop"), new GUIContent("Loop",
                "Whether this clip should loop when played."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("DefaultVolume"), new GUIContent("Volume",
                "Default playback volume (0-127)."));

            PSXEditorStyles.EndCard();

            EditorGUILayout.Space(4);

            // Info card
            if (audioClip.Clip != null)
            {
                PSXEditorStyles.BeginCard();
                float duration = audioClip.Clip.length;
                int srcRate = audioClip.Clip.frequency;
                EditorGUILayout.LabelField(
                    $"Source: {srcRate} Hz, {duration:F2}s, {audioClip.Clip.channels}ch\n" +
                    $"Target: {audioClip.SampleRate} Hz SPU ADPCM",
                    PSXEditorStyles.InfoBox);
                PSXEditorStyles.EndCard();
            }

            serializedObject.ApplyModifiedProperties();
        }
    }

    /// <summary>
    /// Custom inspector for PSXPlayer component.
    /// </summary>
    [CustomEditor(typeof(PSXPlayer))]
    public class PSXPlayerEditor : Editor
    {
        private bool _dimensionsFoldout = true;
        private bool _movementFoldout = true;
        private bool _navigationFoldout = true;
        private bool _physicsFoldout = true;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Header card
            PSXEditorStyles.BeginCard();
            EditorGUILayout.LabelField("PSX Player", PSXEditorStyles.CardHeaderStyle);
            EditorGUILayout.LabelField("First-person player controller for PS1", PSXEditorStyles.RichLabel);
            PSXEditorStyles.EndCard();

            EditorGUILayout.Space(4);

            // Dimensions
            _dimensionsFoldout = PSXEditorStyles.DrawFoldoutCard("Player Dimensions", _dimensionsFoldout, () =>
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("playerHeight"), new GUIContent("Height",
                    "Camera eye height above the player's feet."));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("playerRadius"), new GUIContent("Radius",
                    "Collision radius for wall sliding."));
            });

            EditorGUILayout.Space(2);

            // Movement
            _movementFoldout = PSXEditorStyles.DrawFoldoutCard("Movement", _movementFoldout, () =>
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("moveSpeed"), new GUIContent("Walk Speed",
                    "Walk speed in world units per second."));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("sprintSpeed"), new GUIContent("Sprint Speed",
                    "Sprint speed in world units per second."));
            });

            EditorGUILayout.Space(2);

            // Navigation
            _navigationFoldout = PSXEditorStyles.DrawFoldoutCard("Navigation", _navigationFoldout, () =>
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("maxStepHeight"), new GUIContent("Max Step Height",
                    "Maximum height the agent can step up."));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("walkableSlopeAngle"), new GUIContent("Walkable Slope",
                    "Maximum walkable slope angle in degrees."));
                PSXEditorStyles.DrawSeparator(4, 4);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("navCellSize"), new GUIContent("Cell Size (XZ)",
                    "Voxel size in XZ plane (smaller = more accurate but slower)."));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("navCellHeight"), new GUIContent("Cell Height",
                    "Voxel height (smaller = more accurate vertical resolution)."));
            });

            EditorGUILayout.Space(2);

            // Jump & Gravity
            _physicsFoldout = PSXEditorStyles.DrawFoldoutCard("Jump & Gravity", _physicsFoldout, () =>
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("jumpHeight"), new GUIContent("Jump Height",
                    "Peak jump height in world units."));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("gravity"), new GUIContent("Gravity",
                    "Downward acceleration in world units per second squared."));
            });

            serializedObject.ApplyModifiedProperties();
        }
    }

    /// <summary>
    /// Custom inspector for PSXPortalLink component.
    /// </summary>
    [CustomEditor(typeof(PSXPortalLink))]
    public class PSXPortalLinkEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            PSXPortalLink portal = (PSXPortalLink)target;

            // Header card
            PSXEditorStyles.BeginCard();
            EditorGUILayout.LabelField("PSX Portal Link", PSXEditorStyles.CardHeaderStyle);

            EditorGUILayout.BeginHorizontal();
            bool valid = portal.RoomA != null && portal.RoomB != null && portal.RoomA != portal.RoomB;
            if (valid)
                PSXEditorStyles.DrawStatusBadge("Valid", PSXEditorStyles.Success, 55);
            else
                PSXEditorStyles.DrawStatusBadge("Invalid", PSXEditorStyles.Error, 60);
            EditorGUILayout.EndHorizontal();

            PSXEditorStyles.EndCard();

            EditorGUILayout.Space(4);

            // Room references card
            PSXEditorStyles.BeginCard();
            EditorGUILayout.LabelField("Connected Rooms", PSXEditorStyles.CardHeaderStyle);
            PSXEditorStyles.DrawSeparator(2, 4);

            EditorGUILayout.PropertyField(serializedObject.FindProperty("RoomA"), new GUIContent("Room A",
                "First room connected by this portal."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("RoomB"), new GUIContent("Room B",
                "Second room connected by this portal."));

            // Validation warnings
            if (portal.RoomA == null || portal.RoomB == null)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Both Room A and Room B must be assigned for export.", PSXEditorStyles.InfoBox);
            }
            else if (portal.RoomA == portal.RoomB)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Room A and Room B must be different rooms.", PSXEditorStyles.InfoBox);
            }

            PSXEditorStyles.EndCard();

            EditorGUILayout.Space(4);

            // Portal size card
            PSXEditorStyles.BeginCard();
            EditorGUILayout.LabelField("Portal Dimensions", PSXEditorStyles.CardHeaderStyle);
            PSXEditorStyles.DrawSeparator(2, 4);

            EditorGUILayout.PropertyField(serializedObject.FindProperty("PortalSize"), new GUIContent("Size (W, H)",
                "Size of the portal opening (width, height) in world units."));

            PSXEditorStyles.EndCard();

            serializedObject.ApplyModifiedProperties();
        }
    }

    /// <summary>
    /// Custom inspector for PSXRoom component.
    /// </summary>
    [CustomEditor(typeof(PSXRoom))]
    public class PSXRoomEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            PSXRoom room = (PSXRoom)target;

            // Header card
            PSXEditorStyles.BeginCard();
            EditorGUILayout.LabelField("PSX Room", PSXEditorStyles.CardHeaderStyle);
            if (!string.IsNullOrEmpty(room.RoomName))
                EditorGUILayout.LabelField(room.RoomName, PSXEditorStyles.RichLabel);
            PSXEditorStyles.EndCard();

            EditorGUILayout.Space(4);

            // Properties card
            PSXEditorStyles.BeginCard();
            EditorGUILayout.LabelField("Room Settings", PSXEditorStyles.CardHeaderStyle);
            PSXEditorStyles.DrawSeparator(2, 4);

            EditorGUILayout.PropertyField(serializedObject.FindProperty("RoomName"), new GUIContent("Room Name",
                "Optional display name for this room (used in editor gizmos)."));

            PSXEditorStyles.DrawSeparator(4, 4);

            EditorGUILayout.PropertyField(serializedObject.FindProperty("VolumeSize"), new GUIContent("Volume Size",
                "Size of the room volume in local space."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("VolumeOffset"), new GUIContent("Volume Offset",
                "Offset of the volume center relative to the transform position."));

            PSXEditorStyles.EndCard();

            EditorGUILayout.Space(4);

            // Info card
            PSXEditorStyles.BeginCard();
            Bounds wb = room.GetWorldBounds();
            Vector3 size = wb.size;
            EditorGUILayout.LabelField(
                $"World bounds: {size.x:F1} x {size.y:F1} x {size.z:F1}",
                PSXEditorStyles.InfoBox);
            PSXEditorStyles.EndCard();

            serializedObject.ApplyModifiedProperties();
        }
    }
}
