#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SplashEdit.RuntimeCode
{
    [CustomEditor(typeof(PSXAnimationClip))]
    public class PSXAnimationEditor : Editor
    {
        private bool _showSkinAnimEvents = true;
        private bool _previewing;
        private bool _playing;
        private float _previewFrame;
        private double _playStartEditorTime;
        private float _playStartFrame;

        private Dictionary<string, Vector3> _savedObjectPositions = new Dictionary<string, Vector3>();
        private Dictionary<string, Quaternion> _savedObjectRotations = new Dictionary<string, Quaternion>();
        private Dictionary<string, bool> _savedObjectActive = new Dictionary<string, bool>();

        // Whether we started AnimationMode for skin anim preview
        private bool _animModeStarted;
        // Cache: for each skinned exporter name, the resolved Animator (or null for legacy path)
        private Dictionary<string, Animator> _skinAnimatorCache = new Dictionary<string, Animator>();
        private HashSet<string> _skinLegacyClips = new HashSet<string>(); // clips that use legacy path

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
            float durationSec = clip.DurationFrames / 30f;
            EditorGUI.BeginChangeCheck();
            durationSec = EditorGUILayout.FloatField("Duration (s)", durationSec);
            if (EditorGUI.EndChangeCheck())
                clip.DurationFrames = Mathf.Max(1, Mathf.RoundToInt(durationSec * 30f));
            EditorGUILayout.LabelField($"  = {clip.DurationFrames} frames at 30 fps", EditorStyles.miniLabel);

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

                        // Row 1: Time, Interp, Delete
                        EditorGUILayout.BeginHorizontal();
                        {
                            float kfSec = kf.Frame / 30f;
                            EditorGUI.BeginChangeCheck();
                            kfSec = EditorGUILayout.FloatField("Time (s)", kfSec);
                            if (EditorGUI.EndChangeCheck())
                                kf.Frame = Mathf.Max(0, Mathf.RoundToInt(kfSec * 30f));
                        }
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

            // ── Skin Anim Events ──
            EditorGUILayout.Space(8);
            _showSkinAnimEvents = EditorGUILayout.Foldout(_showSkinAnimEvents, "Skin Anim Events", true);
            if (_showSkinAnimEvents)
            {
                if (clip.SkinAnimEvents == null) clip.SkinAnimEvents = new List<PSXSkinAnimEvent>();

                // Gather skinned exporters for validation
                var skinnedExporters = Object.FindObjectsByType<PSXSkinnedObjectExporter>(FindObjectsSortMode.None);
                var skinnedNames = new HashSet<string>();
                var skinnedClipNames = new Dictionary<string, List<string>>();
                foreach (var se in skinnedExporters)
                {
                    string sName = se.gameObject.name;
                    skinnedNames.Add(sName);
                    if (!skinnedClipNames.ContainsKey(sName))
                        skinnedClipNames[sName] = new List<string>();
                    if (se.AnimationClips != null)
                        foreach (var ac in se.AnimationClips)
                            if (ac != null) skinnedClipNames[sName].Add(ac.name);
                }

                int removeSkinEvtIdx = -1;
                for (int ei = 0; ei < clip.SkinAnimEvents.Count; ei++)
                {
                    var evt = clip.SkinAnimEvents[ei];
                    EditorGUILayout.BeginVertical("box");
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Time (s)", GUILayout.Width(52));
                    {
                        float sEvtSec = evt.Frame / 30f;
                        EditorGUI.BeginChangeCheck();
                        sEvtSec = EditorGUILayout.FloatField(sEvtSec, GUILayout.Width(60));
                        if (EditorGUI.EndChangeCheck())
                            evt.Frame = Mathf.Max(0, Mathf.RoundToInt(sEvtSec * 30f));
                    }
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("\u2212", GUILayout.Width(22)))
                        removeSkinEvtIdx = ei;
                    EditorGUILayout.EndHorizontal();

                    evt.TargetObjectName = EditorGUILayout.TextField("Target Object", evt.TargetObjectName);
                    if (!string.IsNullOrEmpty(evt.TargetObjectName) && !skinnedNames.Contains(evt.TargetObjectName))
                        EditorGUILayout.HelpBox($"No PSXSkinnedObjectExporter found for '{evt.TargetObjectName}' in scene.", MessageType.Error);

                    evt.ClipName = EditorGUILayout.TextField("Clip Name", evt.ClipName);
                    if (!string.IsNullOrEmpty(evt.TargetObjectName) && !string.IsNullOrEmpty(evt.ClipName))
                    {
                        if (skinnedClipNames.TryGetValue(evt.TargetObjectName, out var clipNames) && !clipNames.Contains(evt.ClipName))
                            EditorGUILayout.HelpBox($"No AnimationClip named '{evt.ClipName}' on '{evt.TargetObjectName}'.", MessageType.Error);
                    }

                    evt.Loop = EditorGUILayout.Toggle("Loop", evt.Loop);

                    EditorGUILayout.EndVertical();
                }

                if (removeSkinEvtIdx >= 0) clip.SkinAnimEvents.RemoveAt(removeSkinEvtIdx);

                if (clip.SkinAnimEvents.Count < 16)
                {
                    if (GUILayout.Button("+ Add Skin Anim Event"))
                        clip.SkinAnimEvents.Add(new PSXSkinAnimEvent());
                }
                else
                {
                    EditorGUILayout.HelpBox("Maximum 16 skin anim events per animation.", MessageType.Info);
                }
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
                float previewSec = _previewFrame / 30f;
                float durationSecPv = clip.DurationFrames / 30f;
                float newSec = EditorGUILayout.Slider("Time (s)", previewSec, 0, durationSecPv);
                float newFrame = newSec * 30f;
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

            // Prepare skinned mesh anim preview
            _animModeStarted = false;
            _skinAnimatorCache.Clear();
            _skinLegacyClips.Clear();
            if (clip.SkinAnimEvents != null && clip.SkinAnimEvents.Count > 0)
            {
                // Save transforms of skinned objects targeted by events so we can
                // restore them when preview stops (Rebind / SampleAnimation can move them)
                var skinTargetNames = new HashSet<string>();
                foreach (var evt in clip.SkinAnimEvents)
                    if (!string.IsNullOrEmpty(evt.TargetObjectName))
                        skinTargetNames.Add(evt.TargetObjectName);

                bool needsAnimMode = false;
                var skinnedExporters = Object.FindObjectsByType<PSXSkinnedObjectExporter>(FindObjectsSortMode.None);
                foreach (var se in skinnedExporters)
                {
                    string objName = se.gameObject.name;

                    // Save transform for every skin-anim target
                    if (skinTargetNames.Contains(objName) && !_savedObjectPositions.ContainsKey(objName))
                    {
                        _savedObjectPositions[objName] = se.transform.position;
                        _savedObjectRotations[objName] = se.transform.rotation;
                        _savedObjectActive[objName] = se.gameObject.activeSelf;
                    }

                    if (_skinAnimatorCache.ContainsKey(objName)) continue;

                    Animator resolved = ResolveAnimatorForSkinExp(se);
                    _skinAnimatorCache[objName] = resolved;
                    if (resolved != null) needsAnimMode = true;
                }

                if (needsAnimMode && !AnimationMode.InAnimationMode())
                {
                    AnimationMode.StartAnimationMode();
                    _animModeStarted = true;
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

            // Restore skinned mesh poses via AnimationMode
            if (_animModeStarted && AnimationMode.InAnimationMode())
            {
                AnimationMode.StopAnimationMode();
                _animModeStarted = false;
            }

            SceneView.RepaintAll();
        }

        // =====================================================================
        // Resolve the Animator target for a PSXSkinnedObjectExporter
        // Matches the bone hierarchy root detection in PSXSkinnedMeshExporter.
        // Returns null if the clip set is Generic/Legacy (no Animator needed).
        // =====================================================================

        private static Animator ResolveAnimatorForSkinExp(PSXSkinnedObjectExporter skinExp)
        {
            var smr = skinExp.GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr == null || smr.sharedMesh == null) return null;

            bool anyHumanoid = false;
            if (skinExp.AnimationClips != null)
            {
                foreach (var ac in skinExp.AnimationClips)
                {
                    if (ac == null) continue;
                    bool hasTransformCurves = false;
                    foreach (var binding in AnimationUtility.GetCurveBindings(ac))
                    {
                        if (binding.type == typeof(Transform)) { hasTransformCurves = true; break; }
                    }
                    if (!hasTransformCurves) { anyHumanoid = true; break; }
                }
            }

            if (!anyHumanoid) return null;

            Animator animator = skinExp.GetComponentInChildren<Animator>();

            Avatar modelAvatar = null;
            string meshAssetPath = AssetDatabase.GetAssetPath(smr.sharedMesh);
            if (!string.IsNullOrEmpty(meshAssetPath))
            {
                foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(meshAssetPath))
                {
                    if (sub is Avatar a) { modelAvatar = a; break; }
                }
            }

            Transform boneHierarchyRoot = skinExp.transform;
            if (smr.rootBone != null && modelAvatar != null && modelAvatar.isHuman)
            {
                Transform candidate = smr.rootBone.parent;
                while (candidate != null)
                {
                    Animator existingAnim = candidate.GetComponent<Animator>();
                    if (existingAnim != null)
                    {
                        var savedAvatar = existingAnim.avatar;
                        // Save transforms before Rebind to prevent teleportation
                        var probePos = existingAnim.transform.localPosition;
                        var probeRot = existingAnim.transform.localRotation;
                        existingAnim.avatar = modelAvatar;
                        existingAnim.Rebind();
                        bool ok = existingAnim.GetBoneTransform(HumanBodyBones.Hips) != null;
                        existingAnim.avatar = savedAvatar;
                        existingAnim.Rebind(); // rebind with restored avatar
                        existingAnim.transform.localPosition = probePos;
                        existingAnim.transform.localRotation = probeRot;
                        if (ok) { boneHierarchyRoot = candidate; break; }
                    }
                    candidate = candidate.parent;
                }
            }
            else if (smr.rootBone != null)
            {
                Transform t = smr.rootBone;
                while (t.parent != null)
                {
                    t = t.parent;
                    if (smr.transform.IsChildOf(t)) break;
                }
                boneHierarchyRoot = t;
            }

            if (animator == null || !animator.transform.IsChildOf(boneHierarchyRoot))
                animator = boneHierarchyRoot.GetComponentInChildren<Animator>();
            if (animator == null)
                animator = boneHierarchyRoot.GetComponent<Animator>();

            if (animator == null) return null;

            if (animator.avatar == null && modelAvatar != null)
                animator.avatar = modelAvatar;

            // Save transforms before Rebind to prevent teleportation
            var saveExpPos = skinExp.transform.localPosition;
            var saveExpRot = skinExp.transform.localRotation;
            var saveAnimPos = animator.transform.localPosition;
            var saveAnimRot = animator.transform.localRotation;
            animator.Rebind();
            skinExp.transform.localPosition = saveExpPos;
            skinExp.transform.localRotation = saveExpRot;
            animator.transform.localPosition = saveAnimPos;
            animator.transform.localRotation = saveAnimRot;
            return animator;
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

            // Apply skin anim event preview
            if (clip.SkinAnimEvents != null && clip.SkinAnimEvents.Count > 0)
            {
                var skinnedExporters = Object.FindObjectsByType<PSXSkinnedObjectExporter>(FindObjectsSortMode.None);
                var skinExpByName = new Dictionary<string, PSXSkinnedObjectExporter>();
                foreach (var se in skinnedExporters)
                    if (!skinExpByName.ContainsKey(se.gameObject.name))
                        skinExpByName[se.gameObject.name] = se;

                // For each target, find the LAST triggered event (highest frame <= current)
                var activeSkinEvents = new Dictionary<string, PSXSkinAnimEvent>();
                foreach (var evt in clip.SkinAnimEvents)
                {
                    if (string.IsNullOrEmpty(evt.TargetObjectName)) continue;
                    if (evt.Frame > (int)t) continue;
                    activeSkinEvents[evt.TargetObjectName] = evt;
                }

                bool didBeginSampling = false;
                foreach (var kvp in activeSkinEvents)
                {
                    var evt = kvp.Value;
                    if (!skinExpByName.TryGetValue(evt.TargetObjectName, out var skinExp)) continue;
                    if (skinExp.AnimationClips == null) continue;

                    AnimationClip animClip = null;
                    foreach (var ac in skinExp.AnimationClips)
                    {
                        if (ac != null && ac.name == evt.ClipName)
                        {
                            animClip = ac;
                            break;
                        }
                    }
                    if (animClip == null) continue;

                    float elapsedSec = (t - evt.Frame) / 30f;
                    if (evt.Loop && animClip.length > 0f)
                        elapsedSec = elapsedSec % animClip.length;
                    else
                        elapsedSec = Mathf.Min(elapsedSec, animClip.length);

                    // Check if this clip has Transform curves (Generic/Legacy)
                    string clipKey = $"{evt.TargetObjectName}:{evt.ClipName}";
                    bool isLegacy;
                    if (_skinLegacyClips.Contains(clipKey))
                    {
                        isLegacy = true;
                    }
                    else if (_skinAnimatorCache.TryGetValue(evt.TargetObjectName, out var cachedAnim) && cachedAnim != null)
                    {
                        isLegacy = false;
                    }
                    else
                    {
                        bool hasTransformCurves = false;
                        foreach (var binding in AnimationUtility.GetCurveBindings(animClip))
                        {
                            if (binding.type == typeof(Transform)) { hasTransformCurves = true; break; }
                        }
                        isLegacy = hasTransformCurves;
                        if (isLegacy) _skinLegacyClips.Add(clipKey);
                    }

                    if (isLegacy)
                    {
                        // Save root transform so root-motion curves don't teleport the object
                        var savedPos = skinExp.transform.localPosition;
                        var savedRot = skinExp.transform.localRotation;
                        bool wasLegacy = animClip.legacy;
                        animClip.legacy = true;
                        animClip.SampleAnimation(skinExp.gameObject, elapsedSec);
                        animClip.legacy = wasLegacy;
                        skinExp.transform.localPosition = savedPos;
                        skinExp.transform.localRotation = savedRot;
                    }
                    else
                    {
                        if (_animModeStarted && AnimationMode.InAnimationMode())
                        {
                            _skinAnimatorCache.TryGetValue(evt.TargetObjectName, out var animator);
                            if (animator != null)
                            {
                                // Save root transforms so root-motion doesn't teleport the object
                                var savedExpPos = skinExp.transform.localPosition;
                                var savedExpRot = skinExp.transform.localRotation;
                                var savedAnimPos = animator.transform.localPosition;
                                var savedAnimRot = animator.transform.localRotation;

                                if (!didBeginSampling) { AnimationMode.BeginSampling(); didBeginSampling = true; }
                                AnimationMode.SampleAnimationClip(animator.gameObject, animClip, elapsedSec);

                                // Restore root transforms
                                skinExp.transform.localPosition = savedExpPos;
                                skinExp.transform.localRotation = savedExpRot;
                                animator.transform.localPosition = savedAnimPos;
                                animator.transform.localRotation = savedAnimRot;
                            }
                        }
                    }
                }
                if (didBeginSampling) AnimationMode.EndSampling();
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
