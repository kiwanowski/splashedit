#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SplashEdit.RuntimeCode
{
    [CustomEditor(typeof(PSXCutsceneClip))]
    public class PSXCutsceneEditor : Editor
    {
        // ── Preview state ──
        private bool _showAudioEvents = true;
        private bool _previewing;
        private bool _playing;
        private float _previewFrame;
        private double _playStartEditorTime;
        private float _playStartFrame;
        private HashSet<int> _firedAudioEventIndices = new HashSet<int>();

        // Saved scene-view state so we can restore after preview
        private bool _hasSavedSceneView;
        private Vector3 _savedPivot;
        private Quaternion _savedRotation;
        private float _savedSize;
 
        // Saved object transforms
        private Dictionary<string, Vector3> _savedObjectPositions = new Dictionary<string, Vector3>();
        private Dictionary<string, Quaternion> _savedObjectRotations = new Dictionary<string, Quaternion>();
        private Dictionary<string, bool> _savedObjectActive = new Dictionary<string, bool>();

        // Audio preview
        private Dictionary<string, AudioClip> _audioClipCache = new Dictionary<string, AudioClip>();

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

            PSXCutsceneClip clip = (PSXCutsceneClip)target;
            double elapsed = EditorApplication.timeSinceStartup - _playStartEditorTime;
            _previewFrame = _playStartFrame + (float)(elapsed * 30.0);

            if (_previewFrame >= clip.DurationFrames)
            {
                _previewFrame = clip.DurationFrames;
                _playing = false;
            }

            ApplyPreview(clip);
            Repaint();
        }

        public override void OnInspectorGUI()
        {
            PSXCutsceneClip clip = (PSXCutsceneClip)target;
            Undo.RecordObject(clip, "Edit Cutscene");

            // ── Header ──
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Cutscene Settings", EditorStyles.boldLabel);

            clip.CutsceneName = EditorGUILayout.TextField("Cutscene Name", clip.CutsceneName);
            if (!string.IsNullOrEmpty(clip.CutsceneName) && clip.CutsceneName.Length > 24)
                EditorGUILayout.HelpBox("Name exceeds 24 characters and will be truncated on export.", MessageType.Warning);

            clip.DurationFrames = EditorGUILayout.IntField("Duration (frames)", clip.DurationFrames);
            if (clip.DurationFrames < 1) clip.DurationFrames = 1;

            float seconds = clip.DurationFrames / 30f;
            EditorGUILayout.LabelField($"  = {seconds:F2} seconds at 30fps", EditorStyles.miniLabel);

            // ── Preview Controls ──
            EditorGUILayout.Space(6);
            DrawPreviewControls(clip);

            // Collect scene references for validation
            var exporterNames = new HashSet<string>();
            var audioNames = new HashSet<string>();
            var canvasNames = new HashSet<string>();
            var elementNames = new Dictionary<string, HashSet<string>>(); // canvas → element names
            var exporters = Object.FindObjectsByType<PSXObjectExporter>(FindObjectsSortMode.None);
            foreach (var e in exporters)
                exporterNames.Add(e.gameObject.name);
            var audioSources = Object.FindObjectsByType<PSXAudioClip>(FindObjectsSortMode.None);
            foreach (var a in audioSources)
                if (!string.IsNullOrEmpty(a.ClipName))
                    audioNames.Add(a.ClipName);
            var canvases = Object.FindObjectsByType<PSXCanvas>(FindObjectsSortMode.None);
            foreach (var c in canvases)
            {
                string cName = c.CanvasName ?? "";
                if (!string.IsNullOrEmpty(cName))
                {
                    canvasNames.Add(cName);
                    if (!elementNames.ContainsKey(cName))
                        elementNames[cName] = new HashSet<string>();
                    // Gather all UI element names under this canvas
                    foreach (var box in c.GetComponentsInChildren<PSXUIBox>())
                        if (!string.IsNullOrEmpty(box.ElementName)) elementNames[cName].Add(box.ElementName);
                    foreach (var txt in c.GetComponentsInChildren<PSXUIText>())
                        if (!string.IsNullOrEmpty(txt.ElementName)) elementNames[cName].Add(txt.ElementName);
                    foreach (var bar in c.GetComponentsInChildren<PSXUIProgressBar>())
                        if (!string.IsNullOrEmpty(bar.ElementName)) elementNames[cName].Add(bar.ElementName);
                    foreach (var img in c.GetComponentsInChildren<PSXUIImage>())
                        if (!string.IsNullOrEmpty(img.ElementName)) elementNames[cName].Add(img.ElementName);
                }
            }

            // ── Tracks ──
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Tracks", EditorStyles.boldLabel);

            if (clip.Tracks == null) clip.Tracks = new List<PSXCutsceneTrack>();

            int removeTrackIdx = -1;
            for (int ti = 0; ti < clip.Tracks.Count; ti++)
            {
                var track = clip.Tracks[ti];
                EditorGUILayout.BeginVertical("box");

                EditorGUILayout.BeginHorizontal();
                track.TrackType = (PSXTrackType)EditorGUILayout.EnumPopup("Type", track.TrackType);
                if (GUILayout.Button("Remove", GUILayout.Width(65)))
                    removeTrackIdx = ti;
                EditorGUILayout.EndHorizontal();

                bool isCameraTrack = track.TrackType == PSXTrackType.CameraPosition || track.TrackType == PSXTrackType.CameraRotation;
                bool isUITrack = track.IsUITrack;
                bool isUIElementTrack = track.IsUIElementTrack;

                if (isCameraTrack)
                {
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.TextField("Target", "(camera)");
                    EditorGUI.EndDisabledGroup();
                }
                else if (isUITrack)
                {
                    track.UICanvasName = EditorGUILayout.TextField("Canvas Name", track.UICanvasName);
                    if (!string.IsNullOrEmpty(track.UICanvasName) && !canvasNames.Contains(track.UICanvasName))
                        EditorGUILayout.HelpBox($"No PSXCanvas with name '{track.UICanvasName}' in scene.", MessageType.Error);

                    if (isUIElementTrack)
                    {
                        track.UIElementName = EditorGUILayout.TextField("Element Name", track.UIElementName);
                        if (!string.IsNullOrEmpty(track.UICanvasName) && !string.IsNullOrEmpty(track.UIElementName))
                        {
                            if (elementNames.TryGetValue(track.UICanvasName, out var elNames) && !elNames.Contains(track.UIElementName))
                                EditorGUILayout.HelpBox($"No UI element '{track.UIElementName}' found under canvas '{track.UICanvasName}'.", MessageType.Error);
                        }
                    }
                }
                else
                {
                    track.ObjectName = EditorGUILayout.TextField("Object Name", track.ObjectName);

                    // Validation
                    if (!string.IsNullOrEmpty(track.ObjectName) && !exporterNames.Contains(track.ObjectName))
                        EditorGUILayout.HelpBox($"No PSXObjectExporter found for '{track.ObjectName}' in scene.", MessageType.Error);
                }

                // ── Keyframes ──
                if (track.Keyframes == null) track.Keyframes = new List<PSXKeyframe>();

                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField($"Keyframes ({track.Keyframes.Count})", EditorStyles.miniLabel);

                int removeKfIdx = -1;
                for (int ki = 0; ki < track.Keyframes.Count; ki++)
                {
                    var kf = track.Keyframes[ki];

                    // Row 1: frame number + interp mode + buttons
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Frame", GUILayout.Width(42));
                    kf.Frame = EditorGUILayout.IntField(kf.Frame, GUILayout.Width(60));
                    kf.Interp = (PSXInterpMode)EditorGUILayout.EnumPopup(kf.Interp, GUILayout.Width(80));
                    GUILayout.FlexibleSpace();

                    // Capture from scene
                    if (isCameraTrack)
                    {
                        if (GUILayout.Button("Capture Cam", GUILayout.Width(90)))
                        {
                            var sv = SceneView.lastActiveSceneView;
                            if (sv != null)
                                kf.Value = track.TrackType == PSXTrackType.CameraPosition
                                    ? sv.camera.transform.position : sv.camera.transform.eulerAngles;
                            else Debug.LogWarning("No active Scene View.");
                        }
                    }
                    else if (!isUITrack && (track.TrackType == PSXTrackType.ObjectPosition || track.TrackType == PSXTrackType.ObjectRotation))
                    {
                        // Capture from the named object in scene
                        if (!string.IsNullOrEmpty(track.ObjectName) && GUILayout.Button("From Object", GUILayout.Width(85)))
                        {
                            var go = GameObject.Find(track.ObjectName);
                            if (go != null)
                                kf.Value = track.TrackType == PSXTrackType.ObjectPosition
                                    ? go.transform.position : go.transform.eulerAngles;
                            else Debug.LogWarning($"Object '{track.ObjectName}' not found in scene.");
                        }
                    }

                    if (GUILayout.Button("\u2212", GUILayout.Width(22)))
                        removeKfIdx = ki;
                    EditorGUILayout.EndHorizontal();

                    // Row 2: value on its own line
                    EditorGUI.indentLevel++;
                    switch (track.TrackType)
                    {
                        case PSXTrackType.ObjectActive:
                        case PSXTrackType.UICanvasVisible:
                        case PSXTrackType.UIElementVisible:
                        {
                            string label = track.TrackType == PSXTrackType.ObjectActive ? "Active" : "Visible";
                            bool active = EditorGUILayout.Toggle(label, kf.Value.x > 0.5f);
                            kf.Value = new Vector3(active ? 1f : 0f, 0, 0);
                            break;
                        }
                        case PSXTrackType.ObjectRotation:
                        case PSXTrackType.CameraRotation:
                        {
                            kf.Value = EditorGUILayout.Vector3Field("Rotation\u00b0", kf.Value);
                            break;
                        }
                        case PSXTrackType.UIProgress:
                        {
                            float progress = EditorGUILayout.Slider("Progress %", kf.Value.x, 0f, 100f);
                            kf.Value = new Vector3(progress, 0, 0);
                            break;
                        }
                        case PSXTrackType.UIPosition:
                        {
                            Vector2 pos = EditorGUILayout.Vector2Field("Position (px)", new Vector2(kf.Value.x, kf.Value.y));
                            kf.Value = new Vector3(pos.x, pos.y, 0);
                            break;
                        }
                        case PSXTrackType.UIColor:
                        {
                            // Show as RGB 0-255 integers
                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.LabelField("R", GUILayout.Width(14));
                            float r = EditorGUILayout.IntField(Mathf.Clamp(Mathf.RoundToInt(kf.Value.x), 0, 255), GUILayout.Width(40));
                            EditorGUILayout.LabelField("G", GUILayout.Width(14));
                            float g = EditorGUILayout.IntField(Mathf.Clamp(Mathf.RoundToInt(kf.Value.y), 0, 255), GUILayout.Width(40));
                            EditorGUILayout.LabelField("B", GUILayout.Width(14));
                            float b = EditorGUILayout.IntField(Mathf.Clamp(Mathf.RoundToInt(kf.Value.z), 0, 255), GUILayout.Width(40));
                            EditorGUILayout.EndHorizontal();
                            kf.Value = new Vector3(r, g, b);
                            break;
                        }
                        default:
                            kf.Value = EditorGUILayout.Vector3Field("Value", kf.Value);
                            break;
                    }
                    EditorGUI.indentLevel--;

                    if (ki < track.Keyframes.Count - 1)
                    {
                        EditorGUILayout.Space(1);
                        var rect = EditorGUILayout.GetControlRect(false, 1);
                        EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.3f));
                    }
                }

                if (removeKfIdx >= 0) track.Keyframes.RemoveAt(removeKfIdx);

                // Add keyframe buttons
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("+ Keyframe", GUILayout.Width(90)))
                {
                    int frame = track.Keyframes.Count > 0 ? track.Keyframes[track.Keyframes.Count - 1].Frame + 15 : 0;
                    track.Keyframes.Add(new PSXKeyframe { Frame = frame, Value = Vector3.zero });
                }
                if (isCameraTrack)
                {
                    if (GUILayout.Button("+ from Scene Cam", GUILayout.Width(130)))
                    {
                        var sv = SceneView.lastActiveSceneView;
                        Vector3 val = Vector3.zero;
                        if (sv != null)
                            val = track.TrackType == PSXTrackType.CameraPosition
                                ? sv.camera.transform.position : sv.camera.transform.eulerAngles;
                        int frame = track.Keyframes.Count > 0 ? track.Keyframes[track.Keyframes.Count - 1].Frame + 15 : 0;
                        track.Keyframes.Add(new PSXKeyframe { Frame = frame, Value = val });
                    }
                }
                else if (!isUITrack && (track.TrackType == PSXTrackType.ObjectPosition || track.TrackType == PSXTrackType.ObjectRotation))
                {
                    if (!string.IsNullOrEmpty(track.ObjectName))
                    {
                        if (GUILayout.Button("+ from Object", GUILayout.Width(110)))
                        {
                            var go = GameObject.Find(track.ObjectName);
                            Vector3 val = Vector3.zero;
                            if (go != null)
                                val = track.TrackType == PSXTrackType.ObjectPosition
                                    ? go.transform.position : go.transform.eulerAngles;
                            int frame = track.Keyframes.Count > 0 ? track.Keyframes[track.Keyframes.Count - 1].Frame + 15 : 0;
                            track.Keyframes.Add(new PSXKeyframe { Frame = frame, Value = val });
                        }
                    }
                }
                EditorGUILayout.EndHorizontal();

                EditorGUI.indentLevel--;
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }

            if (removeTrackIdx >= 0) clip.Tracks.RemoveAt(removeTrackIdx);

            if (clip.Tracks.Count < 8)
            {
                if (GUILayout.Button("+ Add Track"))
                    clip.Tracks.Add(new PSXCutsceneTrack());
            }
            else
            {
                EditorGUILayout.HelpBox("Maximum 8 tracks per cutscene.", MessageType.Info);
            }

            // ── Audio Events ──
            EditorGUILayout.Space(8);
            _showAudioEvents = EditorGUILayout.Foldout(_showAudioEvents, "Audio Events", true);
            if (_showAudioEvents)
            {
                if (clip.AudioEvents == null) clip.AudioEvents = new List<PSXAudioEvent>();

                int removeEventIdx = -1;
                for (int ei = 0; ei < clip.AudioEvents.Count; ei++)
                {
                    var evt = clip.AudioEvents[ei];
                    EditorGUILayout.BeginVertical("box");
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Frame", GUILayout.Width(42));
                    evt.Frame = EditorGUILayout.IntField(evt.Frame, GUILayout.Width(60));
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("\u2212", GUILayout.Width(22)))
                        removeEventIdx = ei;
                    EditorGUILayout.EndHorizontal();

                    evt.ClipName = EditorGUILayout.TextField("Clip Name", evt.ClipName);
                    if (!string.IsNullOrEmpty(evt.ClipName) && !audioNames.Contains(evt.ClipName))
                        EditorGUILayout.HelpBox($"No PSXAudioClip with ClipName '{evt.ClipName}' in scene.", MessageType.Error);

                    evt.Volume = EditorGUILayout.IntSlider("Volume", evt.Volume, 0, 128);
                    evt.Pan = EditorGUILayout.IntSlider("Pan", evt.Pan, 0, 127);

                    EditorGUILayout.EndVertical();
                }

                if (removeEventIdx >= 0) clip.AudioEvents.RemoveAt(removeEventIdx);

                if (clip.AudioEvents.Count < 64)
                {
                    if (GUILayout.Button("+ Add Audio Event"))
                        clip.AudioEvents.Add(new PSXAudioEvent());
                }
                else
                {
                    EditorGUILayout.HelpBox("Maximum 64 audio events per cutscene.", MessageType.Info);
                }
            }

            if (GUI.changed)
                EditorUtility.SetDirty(clip);
        }

        // =====================================================================
        // Preview Controls
        // =====================================================================

        private void DrawPreviewControls(PSXCutsceneClip clip)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

            // Transport bar
            EditorGUILayout.BeginHorizontal();

            bool wasPlaying = _playing;
            if (_playing)
            {
                if (GUILayout.Button("\u275A\u275A Pause", GUILayout.Width(70)))
                    _playing = false;
            }
            else
            {
                if (GUILayout.Button("\u25B6 Play", GUILayout.Width(70)))
                {
                    if (!_previewing) StartPreview(clip);
                    _playing = true;
                    _playStartEditorTime = EditorApplication.timeSinceStartup;
                    _playStartFrame = _previewFrame;
                    _firedAudioEventIndices.Clear();
                    // Mark already-passed events so they won't fire again
                    if (clip.AudioEvents != null)
                        for (int i = 0; i < clip.AudioEvents.Count; i++)
                            if (clip.AudioEvents[i].Frame < (int)_previewFrame)
                                _firedAudioEventIndices.Add(i);
                }
            }

            if (GUILayout.Button("\u25A0 Stop", GUILayout.Width(60)))
            {
                _playing = false;
                _previewFrame = 0;
                if (_previewing) StopPreview();
            }

            if (_previewing)
            {
                GUI.color = new Color(1f, 0.4f, 0.4f);
                if (GUILayout.Button("End Preview", GUILayout.Width(90)))
                {
                    _playing = false;
                    StopPreview();
                }
                GUI.color = Color.white;
            }

            EditorGUILayout.EndHorizontal();

            // Timeline scrubber
            EditorGUI.BeginChangeCheck();
            float newFrame = EditorGUILayout.Slider("Frame", _previewFrame, 0, clip.DurationFrames);
            if (EditorGUI.EndChangeCheck())
            {
                if (!_previewing) StartPreview(clip);
                _previewFrame = newFrame;
                _playing = false;
                _firedAudioEventIndices.Clear();
                ApplyPreview(clip);
            }

            float previewSec = _previewFrame / 30f;
            EditorGUILayout.LabelField(
                $"  {(int)_previewFrame} / {clip.DurationFrames}  ({previewSec:F2}s / {seconds(clip):F2}s)",
                EditorStyles.miniLabel);

            if (_previewing)
                EditorGUILayout.HelpBox(
                    "PREVIEWING: Scene View camera & objects are being driven. " +
                    "Click \u201cEnd Preview\u201d or \u201cStop\u201d to restore original positions.",
                    MessageType.Warning);

            EditorGUILayout.EndVertical();
        }

        private static float seconds(PSXCutsceneClip clip) => clip.DurationFrames / 30f;

        // =====================================================================
        // Preview Lifecycle
        // =====================================================================

        private void StartPreview(PSXCutsceneClip clip)
        {
            if (_previewing) return;
            _previewing = true;
            _firedAudioEventIndices.Clear();

            // Save scene view camera
            var sv = SceneView.lastActiveSceneView;
            if (sv != null)
            {
                _hasSavedSceneView = true;
                _savedPivot = sv.pivot;
                _savedRotation = sv.rotation;
                _savedSize = sv.size;
            }

            // Save object transforms
            _savedObjectPositions.Clear();
            _savedObjectRotations.Clear();
            _savedObjectActive.Clear();
            if (clip.Tracks != null)
            {
                foreach (var track in clip.Tracks)
                {
                    if (string.IsNullOrEmpty(track.ObjectName)) continue;
                    bool isCam = track.TrackType == PSXTrackType.CameraPosition || track.TrackType == PSXTrackType.CameraRotation;
                    if (isCam) continue;

                    var go = GameObject.Find(track.ObjectName);
                    if (go == null) continue;

                    if (!_savedObjectPositions.ContainsKey(track.ObjectName))
                    {
                        _savedObjectPositions[track.ObjectName] = go.transform.position;
                        _savedObjectRotations[track.ObjectName] = go.transform.rotation;
                        _savedObjectActive[track.ObjectName] = go.activeSelf;
                    }
                }
            }

            // Build audio clip lookup
            _audioClipCache.Clear();
            var audioSources = Object.FindObjectsByType<PSXAudioClip>(FindObjectsSortMode.None);
            foreach (var a in audioSources)
                if (!string.IsNullOrEmpty(a.ClipName) && a.Clip != null)
                    _audioClipCache[a.ClipName] = a.Clip;
        }

        private void StopPreview()
        {
            if (!_previewing) return;
            _previewing = false;
            _playing = false;

            // Restore scene view camera
            if (_hasSavedSceneView)
            {
                var sv = SceneView.lastActiveSceneView;
                if (sv != null)
                {
                    sv.pivot = _savedPivot;
                    sv.rotation = _savedRotation;
                    sv.size = _savedSize;
                    sv.Repaint();
                }
                _hasSavedSceneView = false;
            }

            // Restore object transforms
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
            Repaint();
        }

        // =====================================================================
        // Apply Preview at Current Frame
        // =====================================================================

        private void ApplyPreview(PSXCutsceneClip clip)
        {
            if (!_previewing) return;
            float frame = _previewFrame;

            var sv = SceneView.lastActiveSceneView;
            Vector3? camPos = null;
            Quaternion? camRot = null;

            if (clip.Tracks != null)
            {
                foreach (var track in clip.Tracks)
                {
                    // Compute initial value for pre-first-keyframe blending
                    Vector3 initialVal = Vector3.zero;
                    switch (track.TrackType)
                    {
                        case PSXTrackType.CameraPosition:
                            if (sv != null)
                                // Recover position from saved pivot/rotation/size
                                initialVal = _savedPivot - _savedRotation * Vector3.forward * _savedSize;
                            break;
                        case PSXTrackType.CameraRotation:
                            initialVal = _savedRotation.eulerAngles;
                            break;
                        case PSXTrackType.ObjectPosition:
                            if (_savedObjectPositions.ContainsKey(track.ObjectName ?? ""))
                                initialVal = _savedObjectPositions[track.ObjectName];
                            break;
                        case PSXTrackType.ObjectRotation:
                            if (_savedObjectRotations.ContainsKey(track.ObjectName ?? ""))
                                initialVal = _savedObjectRotations[track.ObjectName].eulerAngles;
                            break;
                        case PSXTrackType.ObjectActive:
                            if (_savedObjectActive.ContainsKey(track.ObjectName ?? ""))
                                initialVal = new Vector3(_savedObjectActive[track.ObjectName] ? 1f : 0f, 0, 0);
                            break;
                        // UI tracks: initial values stay zero (no scene preview state to capture)
                        case PSXTrackType.UICanvasVisible:
                        case PSXTrackType.UIElementVisible:
                            initialVal = new Vector3(1f, 0, 0); // assume visible by default
                            break;
                        case PSXTrackType.UIProgress:
                        case PSXTrackType.UIPosition:
                        case PSXTrackType.UIColor:
                            break; // zero is fine
                    }

                    Vector3 val = EvaluateTrack(track, frame, initialVal);

                    switch (track.TrackType)
                    {
                        case PSXTrackType.CameraPosition:
                            camPos = val;
                            break;
                        case PSXTrackType.CameraRotation:
                            camRot = Quaternion.Euler(val);
                            break;
                        case PSXTrackType.ObjectPosition:
                        {
                            var go = GameObject.Find(track.ObjectName);
                            if (go != null) go.transform.position = val;
                            break;
                        }
                        case PSXTrackType.ObjectRotation:
                        {
                            var go = GameObject.Find(track.ObjectName);
                            if (go != null) go.transform.rotation = Quaternion.Euler(val);
                            break;
                        }
                        case PSXTrackType.ObjectActive:
                        {
                            var go = GameObject.Find(track.ObjectName);
                            if (go != null) go.SetActive(val.x > 0.5f);
                            break;
                        }
                        // UI tracks: no scene preview, values are applied on PS1 only
                        case PSXTrackType.UICanvasVisible:
                        case PSXTrackType.UIElementVisible:
                        case PSXTrackType.UIProgress:
                        case PSXTrackType.UIPosition:
                        case PSXTrackType.UIColor:
                            break;
                    }
                }
            }

            // Drive scene view camera
            if (sv != null && (camPos.HasValue || camRot.HasValue))
            {
                Vector3 pos = camPos ?? sv.camera.transform.position;
                Quaternion rot = camRot ?? sv.camera.transform.rotation;

                // SceneView needs pivot and rotation set — pivot = position + forward * size
                sv.rotation = rot;
                sv.pivot = pos + rot * Vector3.forward * sv.cameraDistance;
                sv.Repaint();
            }

            // Fire audio events (only during playback, not scrubbing)
            if (_playing && clip.AudioEvents != null)
            {
                for (int i = 0; i < clip.AudioEvents.Count; i++)
                {
                    if (_firedAudioEventIndices.Contains(i)) continue;
                    var evt = clip.AudioEvents[i];
                    if (frame >= evt.Frame)
                    {
                        _firedAudioEventIndices.Add(i);
                        PlayAudioPreview(evt);
                    }
                }
            }
        }

        // =====================================================================
        // Track Evaluation (linear interpolation, matching C++ runtime)
        // =====================================================================

        private static Vector3 EvaluateTrack(PSXCutsceneTrack track, float frame, Vector3 initialValue)
        {
            if (track.Keyframes == null || track.Keyframes.Count == 0)
                return Vector3.zero;

            // Step interpolation tracks: ObjectActive, UICanvasVisible, UIElementVisible
            if (track.TrackType == PSXTrackType.ObjectActive ||
                track.TrackType == PSXTrackType.UICanvasVisible ||
                track.TrackType == PSXTrackType.UIElementVisible)
            {
                if (track.Keyframes.Count > 0 && track.Keyframes[0].Frame > 0 && frame < track.Keyframes[0].Frame)
                    return initialValue;
                return EvaluateStep(track.Keyframes, frame);
            }

            // Find surrounding keyframes
            PSXKeyframe before = null, after = null;
            for (int i = 0; i < track.Keyframes.Count; i++)
            {
                if (track.Keyframes[i].Frame <= frame)
                    before = track.Keyframes[i];
                if (track.Keyframes[i].Frame >= frame && after == null)
                    after = track.Keyframes[i];
            }

            if (before == null && after == null) return Vector3.zero;

            // Pre-first-keyframe: blend from initial value to first keyframe
            if (before == null && after != null && after.Frame > 0 && frame < after.Frame)
            {
                float rawT = frame / after.Frame;
                float t = ApplyInterpCurve(rawT, after.Interp);
                return Vector3.Lerp(initialValue, after.Value, t);
            }

            if (before == null) return after.Value;
            if (after == null) return before.Value;
            if (before == after) return before.Value;

            float span = after.Frame - before.Frame;
            float rawT2 = (frame - before.Frame) / span;
            float t2 = ApplyInterpCurve(rawT2, after.Interp);

            // Linear interpolation for all tracks including rotation.
            // No shortest-path wrapping: a keyframe from 0 to 360 rotates the full circle.
            return Vector3.Lerp(before.Value, after.Value, t2);
        }

        /// <summary>
        /// Apply easing curve to a linear t value (0..1). Matches the C++ applyCurve().
        /// </summary>
        private static float ApplyInterpCurve(float t, PSXInterpMode mode)
        {
            switch (mode)
            {
                default:
                case PSXInterpMode.Linear:
                    return t;
                case PSXInterpMode.Step:
                    return 0f;
                case PSXInterpMode.EaseIn:
                    return t * t;
                case PSXInterpMode.EaseOut:
                    return t * (2f - t);
                case PSXInterpMode.EaseInOut:
                    return t * t * (3f - 2f * t);
            }
        }

        private static Vector3 EvaluateStep(List<PSXKeyframe> keyframes, float frame)
        {
            Vector3 result = Vector3.zero;
            for (int i = 0; i < keyframes.Count; i++)
            {
                if (keyframes[i].Frame <= frame)
                    result = keyframes[i].Value;
            }
            return result;
        }

        // =====================================================================
        // Audio Preview
        // =====================================================================

        private void PlayAudioPreview(PSXAudioEvent evt)
        {
            if (string.IsNullOrEmpty(evt.ClipName)) return;
            if (!_audioClipCache.TryGetValue(evt.ClipName, out AudioClip clip)) return;

            // Use Unity's editor audio playback utility via reflection
            // (PlayClipAtPoint doesn't work in edit mode)
            var unityEditorAssembly = typeof(AudioImporter).Assembly;
            var audioUtilClass = unityEditorAssembly.GetType("UnityEditor.AudioUtil");
            if (audioUtilClass == null) return;

            // Stop any previous preview
            var stopMethod = audioUtilClass.GetMethod("StopAllPreviewClips",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
            stopMethod?.Invoke(null, null);

            // Play the clip
            var playMethod = audioUtilClass.GetMethod("PlayPreviewClip",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public,
                null, new System.Type[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
            playMethod?.Invoke(null, new object[] { clip, 0, false });
        }
    }
}
#endif
