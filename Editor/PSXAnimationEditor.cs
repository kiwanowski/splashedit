#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SplashEdit.RuntimeCode
{
    [CustomEditor(typeof(PSXAnimationClip))]
    public class PSXAnimationEditor : Editor
    {
        private bool _previewing;
        private bool _playing;
        private float _previewFrame;
        private double _playStartEditorTime;
        private float _playStartFrame;

        private Dictionary<string, Vector3> _savedObjectPositions = new Dictionary<string, Vector3>();
        private Dictionary<string, Quaternion> _savedObjectRotations = new Dictionary<string, Quaternion>();
        private Dictionary<string, bool> _savedObjectActive = new Dictionary<string, bool>();

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            if (_previewing) StopPreview();
        }

        private void OnEditorUpdate()
        {
            if (!_playing) return;

            PSXAnimationClip clip = (PSXAnimationClip)target;
            float elapsed = (float)(EditorApplication.timeSinceStartup - _playStartEditorTime);
            _previewFrame = _playStartFrame + elapsed * 30f;

            if (_previewFrame > clip.DurationFrames)
            {
                _previewFrame = clip.DurationFrames;
                _playing = false;
            }

            ApplyPreview(clip);
            Repaint();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            PSXAnimationClip clip = (PSXAnimationClip)target;

            // Name and duration
            EditorGUILayout.PropertyField(serializedObject.FindProperty("AnimationName"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("DurationFrames"));

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Tracks", EditorStyles.boldLabel);

            // Tracks
            var tracksProp = serializedObject.FindProperty("Tracks");
            if (clip.Tracks != null)
            {
                for (int i = 0; i < clip.Tracks.Count; i++)
                {
                    var track = clip.Tracks[i];
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"Track {i}", EditorStyles.boldLabel, GUILayout.Width(60));

                    // Track type - filter out camera types
                    var newType = (PSXTrackType)EditorGUILayout.EnumPopup("Type", track.TrackType);
                    if (newType == PSXTrackType.CameraPosition || newType == PSXTrackType.CameraRotation)
                    {
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.HelpBox("Camera tracks are not allowed in animations. Use a Cutscene instead.", MessageType.Warning);
                        EditorGUILayout.EndVertical();
                        continue;
                    }
                    track.TrackType = newType;

                    if (GUILayout.Button("X", GUILayout.Width(25)))
                    {
                        clip.Tracks.RemoveAt(i);
                        EditorUtility.SetDirty(clip);
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.EndVertical();
                        break;
                    }
                    EditorGUILayout.EndHorizontal();

                    // Target name
                    bool isUITrack = track.IsUITrack;
                    if (!isUITrack)
                    {
                        track.ObjectName = EditorGUILayout.TextField("Object Name", track.ObjectName);

                        // "From Object" button for position/rotation
                        if (track.TrackType == PSXTrackType.ObjectPosition || track.TrackType == PSXTrackType.ObjectRotation)
                        {
                            if (!string.IsNullOrEmpty(track.ObjectName))
                            {
                                var go = GameObject.Find(track.ObjectName);
                                if (go != null && GUILayout.Button("From Object"))
                                {
                                    var kf = new PSXKeyframe
                                    {
                                        Frame = Mathf.RoundToInt(_previewFrame),
                                        Value = track.TrackType == PSXTrackType.ObjectPosition
                                            ? go.transform.position
                                            : go.transform.eulerAngles,
                                        Interp = PSXInterpMode.Linear
                                    };
                                    track.Keyframes.Add(kf);
                                    EditorUtility.SetDirty(clip);
                                }
                            }
                        }
                    }
                    else
                    {
                        track.UICanvasName = EditorGUILayout.TextField("Canvas Name", track.UICanvasName);
                        if (track.IsUIElementTrack)
                            track.UIElementName = EditorGUILayout.TextField("Element Name", track.UIElementName);
                    }

                    // Keyframes
                    EditorGUI.indentLevel++;
                    for (int ki = 0; ki < track.Keyframes.Count; ki++)
                    {
                        var kf = track.Keyframes[ki];
                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                        // Row 1: Frame, Interp, Delete
                        EditorGUILayout.BeginHorizontal();
                        kf.Frame = EditorGUILayout.IntField("Frame", kf.Frame);
                        kf.Interp = (PSXInterpMode)EditorGUILayout.EnumPopup(kf.Interp, GUILayout.Width(90));
                        if (GUILayout.Button("X", GUILayout.Width(22)))
                        {
                            track.Keyframes.RemoveAt(ki);
                            EditorUtility.SetDirty(clip);
                            EditorGUILayout.EndHorizontal();
                            EditorGUILayout.EndVertical();
                            break;
                        }
                        EditorGUILayout.EndHorizontal();

                        // Row 2: Value
                        switch (track.TrackType)
                        {
                            case PSXTrackType.ObjectActive:
                            case PSXTrackType.UICanvasVisible:
                            case PSXTrackType.UIElementVisible:
                            {
                                string label = track.TrackType == PSXTrackType.ObjectActive ? "Active" : "Visible";
                                bool val = kf.Value.x > 0.5f;
                                val = EditorGUILayout.Toggle(label, val);
                                kf.Value = new Vector3(val ? 1f : 0f, 0, 0);
                                break;
                            }
                            case PSXTrackType.ObjectRotation:
                            case PSXTrackType.ObjectPosition:
                                kf.Value = EditorGUILayout.Vector3Field("Value", kf.Value);
                                break;
                            case PSXTrackType.UIProgress:
                                kf.Value = new Vector3(EditorGUILayout.Slider("Progress", kf.Value.x, 0, 100), 0, 0);
                                break;
                            case PSXTrackType.UIPosition:
                                kf.Value = EditorGUILayout.Vector2Field("Position", kf.Value);
                                break;
                            case PSXTrackType.UIColor:
                                kf.Value = EditorGUILayout.Vector3Field("Color (RGB)", kf.Value);
                                break;
                            default:
                                kf.Value = EditorGUILayout.Vector3Field("Value", kf.Value);
                                break;
                        }

                        track.Keyframes[ki] = kf;
                        EditorGUILayout.EndVertical();
                    }

                    if (GUILayout.Button("+ Add Keyframe"))
                    {
                        track.Keyframes.Add(new PSXKeyframe { Frame = 0, Interp = PSXInterpMode.Linear });
                        EditorUtility.SetDirty(clip);
                    }
                    EditorGUI.indentLevel--;

                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(4);
                }
            }

            if (GUILayout.Button("+ Add Track"))
            {
                if (clip.Tracks == null) clip.Tracks = new List<PSXCutsceneTrack>();
                clip.Tracks.Add(new PSXCutsceneTrack { TrackType = PSXTrackType.ObjectPosition });
                EditorUtility.SetDirty(clip);
            }

            // Preview controls
            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (!_previewing)
            {
                if (GUILayout.Button("Start Preview")) StartPreview(clip);
            }
            else
            {
                if (GUILayout.Button("Stop Preview")) StopPreview();
                if (!_playing && GUILayout.Button("Play")) { _playStartEditorTime = EditorApplication.timeSinceStartup; _playStartFrame = _previewFrame; _playing = true; }
                if (_playing && GUILayout.Button("Pause")) _playing = false;
                if (GUILayout.Button("Reset")) { _previewFrame = 0; _playing = false; ApplyPreview(clip); }
            }
            EditorGUILayout.EndHorizontal();

            if (_previewing)
            {
                float newFrame = EditorGUILayout.Slider("Frame", _previewFrame, 0, clip.DurationFrames);
                if (!Mathf.Approximately(newFrame, _previewFrame))
                {
                    _previewFrame = newFrame;
                    _playing = false;
                    ApplyPreview(clip);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void StartPreview(PSXAnimationClip clip)
        {
            _previewing = true;
            _previewFrame = 0;
            _savedObjectPositions.Clear();
            _savedObjectRotations.Clear();
            _savedObjectActive.Clear();

            if (clip.Tracks == null) return;
            foreach (var track in clip.Tracks)
            {
                bool isCam = track.TrackType == PSXTrackType.CameraPosition || track.TrackType == PSXTrackType.CameraRotation;
                if (isCam || track.IsUITrack) continue;

                if (!string.IsNullOrEmpty(track.ObjectName))
                {
                    var go = GameObject.Find(track.ObjectName);
                    if (go != null)
                    {
                        _savedObjectPositions[track.ObjectName] = go.transform.position;
                        _savedObjectRotations[track.ObjectName] = go.transform.rotation;
                        _savedObjectActive[track.ObjectName] = go.activeSelf;
                    }
                }
            }
        }

        private void StopPreview()
        {
            _previewing = false;
            _playing = false;

            // Restore saved state
            foreach (var kvp in _savedObjectPositions)
            {
                var go = GameObject.Find(kvp.Key);
                if (go == null) continue;
                go.transform.position = kvp.Value;
                if (_savedObjectRotations.ContainsKey(kvp.Key))
                    go.transform.rotation = _savedObjectRotations[kvp.Key];
                if (_savedObjectActive.ContainsKey(kvp.Key))
                    go.SetActive(_savedObjectActive[kvp.Key]);
            }
            _savedObjectPositions.Clear();
            _savedObjectRotations.Clear();
            _savedObjectActive.Clear();
            SceneView.RepaintAll();
        }

        private void ApplyPreview(PSXAnimationClip clip)
        {
            if (clip.Tracks == null) return;
            float t = _previewFrame;

            foreach (var track in clip.Tracks)
            {
                if (track.Keyframes == null || track.Keyframes.Count == 0) continue;

                Vector3 val = EvaluateTrack(track, t, clip.DurationFrames);

                switch (track.TrackType)
                {
                    case PSXTrackType.ObjectPosition:
                    {
                        var go = GameObject.Find(track.ObjectName);
                        if (go != null) go.transform.position = val;
                        break;
                    }
                    case PSXTrackType.ObjectRotation:
                    {
                        var go = GameObject.Find(track.ObjectName);
                        if (go != null)
                        {
                            Vector3 initialRot = _savedObjectRotations.ContainsKey(track.ObjectName ?? "")
                                ? _savedObjectRotations[track.ObjectName].eulerAngles
                                : Vector3.zero;
                            go.transform.eulerAngles = val;
                        }
                        break;
                    }
                    case PSXTrackType.ObjectActive:
                    {
                        var go = GameObject.Find(track.ObjectName);
                        if (go != null) go.SetActive(val.x > 0.5f);
                        break;
                    }
                }
            }
            SceneView.RepaintAll();
        }

        private Vector3 EvaluateTrack(PSXCutsceneTrack track, float frame, int totalFrames)
        {
            var kfs = track.Keyframes;
            if (kfs.Count == 0) return Vector3.zero;
            if (kfs.Count == 1 || frame <= kfs[0].Frame) return kfs[0].Value;
            if (frame >= kfs[kfs.Count - 1].Frame) return kfs[kfs.Count - 1].Value;

            for (int i = 0; i < kfs.Count - 1; i++)
            {
                if (frame >= kfs[i].Frame && frame < kfs[i + 1].Frame)
                {
                    float span = kfs[i + 1].Frame - kfs[i].Frame;
                    float t = span > 0 ? (frame - kfs[i].Frame) / span : 0;
                    return Vector3.Lerp(kfs[i].Value, kfs[i + 1].Value, t);
                }
            }
            return kfs[kfs.Count - 1].Value;
        }
    }
}
#endif
