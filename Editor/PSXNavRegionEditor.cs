using UnityEngine;
using UnityEditor;
using SplashEdit.RuntimeCode;
using System.Collections.Generic;

namespace SplashEdit.EditorCode
{
    /// <summary>
    /// Editor window for PS1 navigation mesh generation.
    /// Uses DotRecast (C# Recast) to voxelize scene geometry and build
    /// convex navigation regions for the PS1 runtime.
    /// All nav settings live on the PSXPlayer component so the editor
    /// preview and the scene export always use the same values.
    /// </summary>
    public class PSXNavRegionEditor : EditorWindow
    {
        private PSXNavRegionBuilder _builder;
        private bool _previewRegions = true;
        private bool _previewPortals = true;
        private bool _previewLabels = true;
        private int _selectedRegion = -1;
        private bool _showAdvanced = false;

        [MenuItem("PlayStation 1/Nav Region Builder")]
        public static void ShowWindow()
        {
            GetWindow<PSXNavRegionEditor>("Nav Region Builder");
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI; 
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(5); 
            GUILayout.Label("PSX Nav Region Builder", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            var players = FindObjectsByType<PSXPlayer>(FindObjectsSortMode.None);

            if (players.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    "No PSXPlayer in scene. Add a PSXPlayer component to configure navigation settings.",
                    MessageType.Warning);
                return;
            }

            var player = players[0];
            var so = new SerializedObject(player);
            so.Update();

            // Info
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.HelpBox(
                    "Uses DotRecast (Recast voxelization) to build PS1 nav regions.\n" +
                    "Settings are on the PSXPlayer component so editor preview\n" +
                    "and scene export always match.\n" +
                    "1. Configure settings below (saved on PSXPlayer)\n" +
                    "2. Click 'Build Nav Regions' to preview\n" +
                    "3. Results export automatically with the scene",
                    MessageType.Info);
            }

            EditorGUILayout.Space(5);

            // Agent settings (from PSXPlayer serialized fields)
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("Agent Settings (PSXPlayer)", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(so.FindProperty("playerHeight"),
                    new GUIContent("Agent Height", "Camera eye height above feet"));
                EditorGUILayout.PropertyField(so.FindProperty("playerRadius"),
                    new GUIContent("Agent Radius", "Collision radius for wall sliding"));
                EditorGUILayout.PropertyField(so.FindProperty("maxStepHeight"),
                    new GUIContent("Max Step Height", "Maximum height the agent can step up"));
                EditorGUILayout.PropertyField(so.FindProperty("walkableSlopeAngle"),
                    new GUIContent("Max Slope", "Maximum walkable slope angle in degrees"));
            }

            EditorGUILayout.Space(5);

            // Advanced settings
            _showAdvanced = EditorGUILayout.Foldout(_showAdvanced, "Advanced Settings");
            if (_showAdvanced)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.PropertyField(so.FindProperty("navCellSize"),
                        new GUIContent("Cell Size", "Voxel size in XZ plane. Smaller = more accurate but slower."));
                    EditorGUILayout.PropertyField(so.FindProperty("navCellHeight"),
                        new GUIContent("Cell Height", "Voxel height. Smaller = more accurate vertical resolution."));

                    EditorGUILayout.Space(3);
                    float cs = player.NavCellSize;
                    float ch = player.NavCellHeight;
                    int walkH = (int)System.Math.Ceiling(player.PlayerHeight / ch);
                    int walkR = (int)System.Math.Ceiling(player.PlayerRadius / cs);
                    int walkC = (int)System.Math.Floor(player.MaxStepHeight / ch);
                    EditorGUILayout.LabelField("Voxel walkable height", $"{walkH} cells");
                    EditorGUILayout.LabelField("Voxel walkable radius", $"{walkR} cells");
                    EditorGUILayout.LabelField("Voxel walkable climb", $"{walkC} cells ({walkC * ch:F3} units)");
                }
            }

            so.ApplyModifiedProperties();

            EditorGUILayout.Space(5);

            // Build button
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("Generation", EditorStyles.boldLabel);

                GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
                if (GUILayout.Button("Build Nav Regions", GUILayout.Height(35)))
                {
                    BuildNavRegions(player);
                }
                GUI.backgroundColor = Color.white;

                if (_builder != null && _builder.RegionCount > 0)
                {
                    EditorGUILayout.Space(3);
                    if (GUILayout.Button("Clear Regions"))
                    {
                        _builder = null;
                        _selectedRegion = -1;
                        SceneView.RepaintAll();
                    }
                }
            }

            EditorGUILayout.Space(5);

            // Visualization
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("Visualization", EditorStyles.boldLabel);
                _previewRegions = EditorGUILayout.Toggle("Show Regions", _previewRegions);
                _previewPortals = EditorGUILayout.Toggle("Show Portals", _previewPortals);
                _previewLabels = EditorGUILayout.Toggle("Show Labels", _previewLabels);
            }

            EditorGUILayout.Space(5);

            // Statistics
            if (_builder != null && _builder.RegionCount > 0)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    GUILayout.Label("Statistics", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("Regions", _builder.RegionCount.ToString());
                    EditorGUILayout.LabelField("Portals", _builder.PortalCount.ToString());

                    var rooms = new HashSet<byte>();
                    for (int i = 0; i < _builder.RegionCount; i++)
                        rooms.Add(_builder.Regions[i].roomIndex);
                    EditorGUILayout.LabelField("Rooms", rooms.Count.ToString());

                    int exportSize = _builder.GetBinarySize();
                    EditorGUILayout.LabelField("Export Size",
                        $"{exportSize:N0} bytes ({exportSize / 1024f:F1} KB)");

                    int flat = 0, ramp = 0, stairs = 0;
                    for (int i = 0; i < _builder.RegionCount; i++)
                    {
                        switch (_builder.Regions[i].surfaceType)
                        {
                            case NavSurfaceType.Flat: flat++; break;
                            case NavSurfaceType.Ramp: ramp++; break;
                            case NavSurfaceType.Stairs: stairs++; break;
                        }
                    }
                    EditorGUILayout.LabelField("Types",
                        $"{flat} flat, {ramp} ramp, {stairs} stairs");

                    if (_selectedRegion >= 0 && _selectedRegion < _builder.RegionCount)
                    {
                        EditorGUILayout.Space(3);
                        GUILayout.Label($"Selected Region #{_selectedRegion}",
                            EditorStyles.miniLabel);
                        var region = _builder.Regions[_selectedRegion];
                        EditorGUILayout.LabelField("  Vertices",
                            region.vertsXZ.Count.ToString());
                        EditorGUILayout.LabelField("  Portals",
                            region.portalCount.ToString());
                        EditorGUILayout.LabelField("  Surface",
                            region.surfaceType.ToString());
                        EditorGUILayout.LabelField("  Room",
                            region.roomIndex.ToString());
                        EditorGUILayout.LabelField("  Floor Y",
                            $"{region.planeD:F2} (A={region.planeA:F3}, B={region.planeB:F3})");
                    }
                }

                ValidateRegions();
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "No nav regions built. Click 'Build Nav Regions' to generate.",
                    MessageType.Warning);
            }
        }

        // ====================================================================
        // Build
        // ====================================================================

        private void BuildNavRegions(PSXPlayer player)
        {
            EditorUtility.DisplayProgressBar("Nav Region Builder", "Building nav regions with DotRecast...", 0.3f);

            _builder = new PSXNavRegionBuilder();
            _builder.AgentHeight = player.PlayerHeight;
            _builder.AgentRadius = player.PlayerRadius;
            _builder.MaxStepHeight = player.MaxStepHeight;
            _builder.WalkableSlopeAngle = player.WalkableSlopeAngle;
            _builder.CellSize = player.NavCellSize;
            _builder.CellHeight = player.NavCellHeight;

            Vector3 playerSpawn = player.transform.position;
            player.FindNavmesh();
            playerSpawn = player.CamPoint;

            PSXObjectExporter[] exporters =
                FindObjectsByType<PSXObjectExporter>(FindObjectsSortMode.None);

            _builder.Build(exporters, playerSpawn);

            _selectedRegion = -1;
            EditorUtility.ClearProgressBar();
            SceneView.RepaintAll();
        }

        // ====================================================================
        // Validation
        // ====================================================================

        private void ValidateRegions()
        {
            if (_builder == null) return;

            List<string> warnings = new List<string>();

            for (int i = 0; i < _builder.RegionCount; i++)
            {
                var region = _builder.Regions[i];
                if (region.vertsXZ.Count < 3)
                    warnings.Add($"Region {i}: degenerate ({region.vertsXZ.Count} verts)");
                if (region.portalCount == 0 && _builder.RegionCount > 1)
                    warnings.Add($"Region {i}: isolated (no portals)");
                if (region.vertsXZ.Count > 8)
                    warnings.Add($"Region {i}: too many verts ({region.vertsXZ.Count} > 8)");
            }

            int exportSize = _builder.GetBinarySize();
            if (exportSize > 8192)
                warnings.Add($"Export size {exportSize} bytes is large for PS1 (> 8KB)");

            if (warnings.Count > 0)
            {
                EditorGUILayout.Space(5);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    GUILayout.Label("Warnings", EditorStyles.boldLabel);
                    foreach (string w in warnings)
                        EditorGUILayout.LabelField(w, EditorStyles.miniLabel);
                }
            }
        }

        // ====================================================================
        // Scene view drawing
        // ====================================================================

        private static readonly Color[] RoomColors = new Color[]
        {
            new Color(0.2f, 0.8f, 0.2f),
            new Color(0.2f, 0.6f, 0.9f),
            new Color(0.9f, 0.7f, 0.1f),
            new Color(0.8f, 0.2f, 0.8f),
            new Color(0.1f, 0.9f, 0.9f),
            new Color(0.9f, 0.5f, 0.2f),
            new Color(0.5f, 0.9f, 0.3f),
            new Color(0.9f, 0.3f, 0.5f),
            new Color(0.4f, 0.4f, 0.9f),
            new Color(0.7f, 0.9f, 0.7f),
            new Color(0.9f, 0.9f, 0.4f),
            new Color(0.6f, 0.3f, 0.6f),
            new Color(0.3f, 0.7f, 0.7f),
            new Color(0.8f, 0.6f, 0.4f),
            new Color(0.4f, 0.8f, 0.6f),
            new Color(0.7f, 0.4f, 0.4f),
        };

        private void OnSceneGUI(SceneView sceneView)
        {
            if (_builder == null || _builder.RegionCount == 0) return;

            var regions = _builder.Regions;

            if (_previewRegions)
            {
                for (int i = 0; i < regions.Count; i++)
                {
                    var region = regions[i];
                    bool selected = (i == _selectedRegion);

                    Color baseColor = RoomColors[region.roomIndex % RoomColors.Length];
                    float fillAlpha = selected ? 0.4f : 0.15f;

                    if (region.vertsXZ.Count >= 3)
                    {
                        Vector3[] worldVerts = new Vector3[region.vertsXZ.Count];
                        for (int v = 0; v < region.vertsXZ.Count; v++)
                        {
                            float y = region.planeA * region.vertsXZ[v].x +
                                      region.planeB * region.vertsXZ[v].y +
                                      region.planeD;
                            worldVerts[v] = new Vector3(
                                region.vertsXZ[v].x, y + 0.05f, region.vertsXZ[v].y);
                        }

                        Handles.color = selected
                            ? Color.white
                            : new Color(baseColor.r, baseColor.g, baseColor.b, 0.8f);
                        for (int v = 0; v < worldVerts.Length; v++)
                            Handles.DrawLine(worldVerts[v],
                                worldVerts[(v + 1) % worldVerts.Length]);

                        Handles.color = new Color(baseColor.r, baseColor.g, baseColor.b,
                            fillAlpha);
                        for (int v = 1; v < worldVerts.Length - 1; v++)
                            Handles.DrawAAConvexPolygon(
                                worldVerts[0], worldVerts[v], worldVerts[v + 1]);

                        if (_previewLabels)
                        {
                            Vector3 center = Vector3.zero;
                            foreach (var wv in worldVerts) center += wv;
                            center /= worldVerts.Length;

                            string label = $"R{i}";
                            if (region.roomIndex != 0xFF)
                                label += $"\nRm{region.roomIndex}";
                            Handles.Label(center, label, EditorStyles.whiteBoldLabel);

                            if (Handles.Button(center, Quaternion.identity,
                                0.2f, 0.3f, Handles.SphereHandleCap))
                            {
                                _selectedRegion = i;
                                Repaint();
                            }
                        }
                    }
                }
            }

            if (_previewPortals && _builder.Portals != null)
            {
                for (int i = 0; i < regions.Count; i++)
                {
                    var region = regions[i];
                    int pStart = region.portalStart;
                    int pCount = region.portalCount;

                    for (int p = pStart;
                         p < pStart + pCount && p < _builder.Portals.Count; p++)
                    {
                        var portal = _builder.Portals[p];

                        float yA = region.planeA * portal.a.x +
                                   region.planeB * portal.a.y + region.planeD;
                        float yB = region.planeA * portal.b.x +
                                   region.planeB * portal.b.y + region.planeD;

                        Vector3 worldA = new Vector3(portal.a.x, yA + 0.08f, portal.a.y);
                        Vector3 worldB = new Vector3(portal.b.x, yB + 0.08f, portal.b.y);

                        if (Mathf.Abs(portal.heightDelta) <= 0.35f)
                            Handles.color = new Color(1f, 1f, 1f, 0.9f);
                        else
                            Handles.color = new Color(1f, 0.9f, 0.2f, 0.9f);

                        Handles.DrawLine(worldA, worldB, 3f);
                    }
                }
            }
        }
    }
}
