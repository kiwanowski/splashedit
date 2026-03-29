using System;
using System.Collections.Generic;
using UnityEngine;

namespace SplashEdit.RuntimeCode
{
    /// <summary>
    /// Defines a convex room volume for the portal/room occlusion system.
    /// Place one of these per room in the scene. Geometry is assigned to rooms by
    /// centroid containment during export. Portals between adjacent rooms are detected
    /// automatically.
    /// 
    /// This is independent of the navregion/portal system used for navigation.
    /// </summary>
    [ExecuteInEditMode]
    [Icon("Packages/net.psxsplash.splashedit/Icons/PSXRoom.png")]
    public class PSXRoom : MonoBehaviour
    {
        [Tooltip("Optional display name for this room (used in editor gizmos).")]
        public string RoomName = "";

        [Tooltip("Size of the room volume in local space. Defaults to the object's scale.")]
        public Vector3 VolumeSize = Vector3.one;

        [Tooltip("Offset of the volume center relative to the transform position.")]
        public Vector3 VolumeOffset = Vector3.zero;

        /// <summary>World-space AABB of this room.</summary>
        public Bounds GetWorldBounds()
        {
            Vector3 center = transform.TransformPoint(VolumeOffset);
            // Transform the 8 corners to get a world-space AABB
            Vector3 halfSize = VolumeSize * 0.5f;
            Vector3 wMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 wMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = VolumeOffset + new Vector3(
                    (i & 1) != 0 ? halfSize.x : -halfSize.x,
                    (i & 2) != 0 ? halfSize.y : -halfSize.y,
                    (i & 4) != 0 ? halfSize.z : -halfSize.z
                );
                Vector3 world = transform.TransformPoint(corner);
                wMin = Vector3.Min(wMin, world);
                wMax = Vector3.Max(wMax, world);
            }
            Bounds b = new Bounds();
            b.SetMinMax(wMin, wMax);
            return b;
        }

        /// <summary>Check if a world-space point is inside this room volume.</summary>
        public bool ContainsPoint(Vector3 worldPoint)
        {
            return GetWorldBounds().Contains(worldPoint);
        }

        void OnDrawGizmos()
        {
            var exporter = FindFirstObjectByType<PSXSceneExporter>();
            if (exporter != null && !exporter.PreviewRoomsPortals) return;

            Gizmos.color = new Color(0.2f, 0.8f, 0.4f, 0.15f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(VolumeOffset, VolumeSize);
            Gizmos.color = new Color(0.2f, 0.8f, 0.4f, 0.6f);
            Gizmos.DrawWireCube(VolumeOffset, VolumeSize);
            Gizmos.matrix = Matrix4x4.identity;

#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(RoomName))
            {
                UnityEditor.Handles.Label(transform.TransformPoint(VolumeOffset),
                    RoomName, new GUIStyle { normal = { textColor = Color.green } });
            }
#endif
        }
    }

    /// <summary>
    /// Portal between two PSXRoom volumes, stored during export.
    /// Built from PSXPortalLink scene components.
    /// </summary>
    public struct PSXPortal
    {
        public int roomA;
        public int roomB;
        public Vector3 center;      // World-space portal center (from PSXPortalLink transform)
        public Vector2 portalSize;  // Portal opening size in world units (width, height)
        public Vector3 normal;      // Portal facing direction (from PSXPortalLink transform.forward)
        public Vector3 right;       // Portal local right axis (world space)
        public Vector3 up;          // Portal local up axis (world space)
    }

    /// <summary>
    /// Builds and exports the room/portal system for a scene.
    /// Called during PSXSceneExporter.Export().
    /// Portals are user-defined via PSXPortalLink components instead of auto-detected.
    /// </summary>
    public class PSXRoomBuilder
    {
        private PSXRoom[] _rooms;
        private List<PSXPortal> _portals = new List<PSXPortal>();
        private List<BVH.TriangleRef>[] _roomTriRefs;
        private List<BVH.TriangleRef> _catchAllTriRefs = new List<BVH.TriangleRef>();

        public int RoomCount => _rooms?.Length ?? 0;
        public int PortalCount => _portals?.Count ?? 0;
        public int TotalTriRefCount
        {
            get
            {
                int count = 0;
                if (_roomTriRefs != null)
                    foreach (var list in _roomTriRefs) count += list.Count;
                count += _catchAllTriRefs.Count;
                return count;
            }
        }

        /// <summary>
        /// Build the room system: assign triangles to rooms and read user-defined portals.
        /// </summary>
        /// <param name="rooms">All PSXRoom components in the scene.</param>
        /// <param name="portalLinks">All PSXPortalLink components (user-placed portals).</param>
        /// <param name="exporters">All object exporters (for triangle centroid testing).</param>
        /// <param name="gteScaling">GTE coordinate scaling factor.</param>
        public void Build(PSXRoom[] rooms, PSXPortalLink[] portalLinks,
                          PSXObjectExporter[] exporters, float gteScaling)
        {
            _rooms = rooms;
            if (rooms == null || rooms.Length == 0) return;

            _roomTriRefs = new List<BVH.TriangleRef>[rooms.Length];
            for (int i = 0; i < rooms.Length; i++)
                _roomTriRefs[i] = new List<BVH.TriangleRef>();
            _catchAllTriRefs.Clear();
            _portals.Clear();

            // Assign each triangle to a room by vertex majority containment.
            // For each triangle, test all 3 world-space vertices against each room's AABB
            // (expanded by a margin to catch boundary geometry). The room containing the
            // most vertices wins. Ties broken by centroid. This prevents boundary triangles
            // (doorway walls, floor edges) from being assigned to the wrong room.
            const float ROOM_MARGIN = 0.5f; // expand AABBs by this much for testing
            Bounds[] roomBounds = new Bounds[rooms.Length];
            for (int i = 0; i < rooms.Length; i++)
            {
                roomBounds[i] = rooms[i].GetWorldBounds();
                roomBounds[i].Expand(ROOM_MARGIN * 2f); // Expand in all directions
            }

            for (int objIdx = 0; objIdx < exporters.Length; objIdx++)
            {
                var exporter = exporters[objIdx];
                if (!exporter.IsActive) continue;
                MeshFilter mf = exporter.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                Mesh mesh = mf.sharedMesh;
                Vector3[] vertices = mesh.vertices;
                int[] indices = mesh.triangles;
                Matrix4x4 worldMatrix = exporter.transform.localToWorldMatrix;

                for (int i = 0; i < indices.Length; i += 3)
                {
                    Vector3 v0 = worldMatrix.MultiplyPoint3x4(vertices[indices[i]]);
                    Vector3 v1 = worldMatrix.MultiplyPoint3x4(vertices[indices[i + 1]]);
                    Vector3 v2 = worldMatrix.MultiplyPoint3x4(vertices[indices[i + 2]]);

                    // Test all 3 vertices against each room, pick room with most hits.
                    int bestRoom = -1;
                    int bestHits = 0;
                    float bestDist = float.MaxValue;
                    for (int r = 0; r < rooms.Length; r++)
                    {
                        int hits = 0;
                        if (roomBounds[r].Contains(v0)) hits++;
                        if (roomBounds[r].Contains(v1)) hits++;
                        if (roomBounds[r].Contains(v2)) hits++;

                        if (hits > bestHits)
                        {
                            bestHits = hits;
                            bestRoom = r;
                            bestDist = (roomBounds[r].center - (v0 + v1 + v2) / 3f).sqrMagnitude;
                        }
                        else if (hits == bestHits && hits > 0)
                        {
                            // Tie-break: pick room whose center is closest to centroid
                            float dist = (roomBounds[r].center - (v0 + v1 + v2) / 3f).sqrMagnitude;
                            if (dist < bestDist)
                            {
                                bestRoom = r;
                                bestDist = dist;
                            }
                        }
                    }

                    var triRef = new BVH.TriangleRef(objIdx, i / 3);
                    if (bestRoom >= 0)
                        _roomTriRefs[bestRoom].Add(triRef);
                    else
                        _catchAllTriRefs.Add(triRef);
                }
            }

            // Build portals from user-placed PSXPortalLink components.
            // (Must happen before boundary duplication so we know which rooms are adjacent.)
            BuildPortals(portalLinks);

            // Phase 3: Duplicate boundary triangles into both rooms at portal boundaries.
            // When a triangle has vertices in multiple rooms, it was assigned to only
            // the "best" room. For triangles near a portal, also add them to the adjacent
            // room so doorway/boundary geometry is visible from both sides.
            DuplicateBoundaryTriangles(exporters, roomBounds);

            // Sort each room's tri-refs by objectIndex for GTE matrix batching.
            for (int i = 0; i < _roomTriRefs.Length; i++)
                _roomTriRefs[i].Sort((a, b) => a.objectIndex.CompareTo(b.objectIndex));
            _catchAllTriRefs.Sort((a, b) => a.objectIndex.CompareTo(b.objectIndex));

            Debug.Log($"PSXRoomBuilder: {rooms.Length} rooms, {_portals.Count} portals, " +
                      $"{TotalTriRefCount} tri-refs ({_catchAllTriRefs.Count} catch-all)");
        }

        /// <summary>
        /// For each portal, find triangles assigned to one adjacent room whose vertices
        /// also touch the other adjacent room. Duplicate those triangles into the other
        /// room so boundary geometry (doorway walls, floor edges) is visible from both sides.
        /// </summary>
        private void DuplicateBoundaryTriangles(PSXObjectExporter[] exporters, Bounds[] roomBounds)
        {
            if (_portals.Count == 0) return;

            int duplicated = 0;
            // Build a set of existing tri-refs per room for O(1) duplicate checking
            var roomSets = new HashSet<long>[_rooms.Length];
            for (int i = 0; i < _rooms.Length; i++)
            {
                roomSets[i] = new HashSet<long>();
                foreach (var tr in _roomTriRefs[i])
                    roomSets[i].Add(((long)tr.objectIndex << 16) | tr.triangleIndex);
            }

            foreach (var portal in _portals)
            {
                int rA = portal.roomA, rB = portal.roomB;
                if (rA < 0 || rA >= _rooms.Length || rB < 0 || rB >= _rooms.Length) continue;

                // For each triangle in room A, check if any vertex is inside room B's AABB.
                // If so, add a copy to room B (and vice versa).
                DuplicateDirection(rA, rB, exporters, roomBounds, roomSets, ref duplicated);
                DuplicateDirection(rB, rA, exporters, roomBounds, roomSets, ref duplicated);
            }

            if (duplicated > 0)
                Debug.Log($"PSXRoomBuilder: Duplicated {duplicated} boundary triangles across portal edges.");
        }

        private void DuplicateDirection(int srcRoom, int dstRoom,
            PSXObjectExporter[] exporters, Bounds[] roomBounds,
            HashSet<long>[] roomSets, ref int duplicated)
        {
            var srcList = new List<BVH.TriangleRef>(_roomTriRefs[srcRoom]);
            foreach (var triRef in srcList)
            {
                long key = ((long)triRef.objectIndex << 16) | triRef.triangleIndex;
                if (roomSets[dstRoom].Contains(key)) continue;  // Already in dst

                if (triRef.objectIndex >= exporters.Length) continue;
                var exporter = exporters[triRef.objectIndex];
                MeshFilter mf = exporter.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                Mesh mesh = mf.sharedMesh;
                Vector3[] vertices = mesh.vertices;
                int[] indices = mesh.triangles;
                Matrix4x4 worldMatrix = exporter.transform.localToWorldMatrix;

                int triStart = triRef.triangleIndex * 3;
                if (triStart + 2 >= indices.Length) continue;
                Vector3 v0 = worldMatrix.MultiplyPoint3x4(vertices[indices[triStart]]);
                Vector3 v1 = worldMatrix.MultiplyPoint3x4(vertices[indices[triStart + 1]]);
                Vector3 v2 = worldMatrix.MultiplyPoint3x4(vertices[indices[triStart + 2]]);

                // Check if any vertex is inside the destination room's AABB
                if (roomBounds[dstRoom].Contains(v0) ||
                    roomBounds[dstRoom].Contains(v1) ||
                    roomBounds[dstRoom].Contains(v2))
                {
                    _roomTriRefs[dstRoom].Add(triRef);
                    roomSets[dstRoom].Add(key);
                    duplicated++;
                }
            }
        }

        /// <summary>
        /// Convert PSXPortalLink components into PSXPortal entries.
        /// Maps PSXRoom references to room indices, validates, and stores center positions.
        /// </summary>
        private void BuildPortals(PSXPortalLink[] portalLinks)
        {
            if (portalLinks == null) return;

            // Build a fast lookup: PSXRoom instance → index.
            var roomIndex = new Dictionary<PSXRoom, int>();
            for (int i = 0; i < _rooms.Length; i++)
                roomIndex[_rooms[i]] = i;

            foreach (var link in portalLinks)
            {
                if (link == null) continue;
                if (link.RoomA == null || link.RoomB == null)
                {
                    Debug.LogWarning($"PSXPortalLink '{link.name}' has unassigned room references — skipped.");
                    continue;
                }
                if (link.RoomA == link.RoomB)
                {
                    Debug.LogWarning($"PSXPortalLink '{link.name}' references the same room twice — skipped.");
                    continue;
                }
                if (!roomIndex.TryGetValue(link.RoomA, out int idxA))
                {
                    Debug.LogWarning($"PSXPortalLink '{link.name}': RoomA '{link.RoomA.name}' is not a known PSXRoom — skipped.");
                    continue;
                }
                if (!roomIndex.TryGetValue(link.RoomB, out int idxB))
                {
                    Debug.LogWarning($"PSXPortalLink '{link.name}': RoomB '{link.RoomB.name}' is not a known PSXRoom — skipped.");
                    continue;
                }

                _portals.Add(new PSXPortal
                {
                    roomA = idxA,
                    roomB = idxB,
                    center = link.transform.position,
                    portalSize = link.PortalSize,
                    normal = link.transform.forward,
                    right = link.transform.right,
                    up = link.transform.up
                });
            }
        }

        /// <summary>
        /// Write room/portal data to the splashpack binary.
        /// </summary>
        public void WriteToBinary(System.IO.BinaryWriter writer, float gteScaling)
        {
            if (_rooms == null || _rooms.Length == 0) return;

            // Per-room data (32 bytes each): AABB (24) + firstTriRef (2) + triRefCount (2) + pad (4)
            int runningTriRefOffset = 0;
            for (int i = 0; i < _rooms.Length; i++)
            {
                Bounds wb = _rooms[i].GetWorldBounds();
                // PS1 coordinate space (negate Y, swap min/max Y)
                writer.Write(PSXTrig.ConvertWorldToFixed12(wb.min.x / gteScaling));
                writer.Write(PSXTrig.ConvertWorldToFixed12(-wb.max.y / gteScaling));
                writer.Write(PSXTrig.ConvertWorldToFixed12(wb.min.z / gteScaling));
                writer.Write(PSXTrig.ConvertWorldToFixed12(wb.max.x / gteScaling));
                writer.Write(PSXTrig.ConvertWorldToFixed12(-wb.min.y / gteScaling));
                writer.Write(PSXTrig.ConvertWorldToFixed12(wb.max.z / gteScaling));

                writer.Write((ushort)runningTriRefOffset);
                writer.Write((ushort)_roomTriRefs[i].Count);
                writer.Write((uint)0); // padding
                runningTriRefOffset += _roomTriRefs[i].Count;
            }

            // Catch-all room (always rendered) — written as an extra "room" entry
            {
                // Catch-all AABB: max world extents
                writer.Write(PSXTrig.ConvertWorldToFixed12(-1000f / gteScaling));
                writer.Write(PSXTrig.ConvertWorldToFixed12(-1000f / gteScaling));
                writer.Write(PSXTrig.ConvertWorldToFixed12(-1000f / gteScaling));
                writer.Write(PSXTrig.ConvertWorldToFixed12(1000f / gteScaling));
                writer.Write(PSXTrig.ConvertWorldToFixed12(1000f / gteScaling));
                writer.Write(PSXTrig.ConvertWorldToFixed12(1000f / gteScaling));
                writer.Write((ushort)runningTriRefOffset);
                writer.Write((ushort)_catchAllTriRefs.Count);
                writer.Write((uint)0);
            }

            // Per-portal data (40 bytes each):
            // roomA(2) + roomB(2) + center(12) + halfW(2) + halfH(2) +
            // normal(6) + pad(2) + right(6) + up(6)
            foreach (var portal in _portals)
            {
                writer.Write((ushort)portal.roomA);
                writer.Write((ushort)portal.roomB);
                // Center of portal (PS1 coords: negate Y)
                writer.Write(PSXTrig.ConvertWorldToFixed12(portal.center.x / gteScaling));
                writer.Write(PSXTrig.ConvertWorldToFixed12(-portal.center.y / gteScaling));
                writer.Write(PSXTrig.ConvertWorldToFixed12(portal.center.z / gteScaling));
                // Portal half-size in GTE units (fp12)
                float halfW = portal.portalSize.x * 0.5f;
                float halfH = portal.portalSize.y * 0.5f;
                writer.Write((short)Mathf.Clamp(Mathf.RoundToInt(halfW / gteScaling * 4096f), 1, 32767));
                writer.Write((short)Mathf.Clamp(Mathf.RoundToInt(halfH / gteScaling * 4096f), 1, 32767));
                // Portal facing normal (PS1 coords: negate Y) - 4.12 fixed-point unit vector
                writer.Write((short)Mathf.Clamp(Mathf.RoundToInt(portal.normal.x * 4096f), -32768, 32767));
                writer.Write((short)Mathf.Clamp(Mathf.RoundToInt(-portal.normal.y * 4096f), -32768, 32767));
                writer.Write((short)Mathf.Clamp(Mathf.RoundToInt(portal.normal.z * 4096f), -32768, 32767));
                writer.Write((short)0); // pad
                // Portal right axis (PS1 coords: negate Y) - 4.12 fixed-point unit vector
                writer.Write((short)Mathf.Clamp(Mathf.RoundToInt(portal.right.x * 4096f), -32768, 32767));
                writer.Write((short)Mathf.Clamp(Mathf.RoundToInt(-portal.right.y * 4096f), -32768, 32767));
                writer.Write((short)Mathf.Clamp(Mathf.RoundToInt(portal.right.z * 4096f), -32768, 32767));
                // Portal up axis (PS1 coords: negate Y) - 4.12 fixed-point unit vector
                writer.Write((short)Mathf.Clamp(Mathf.RoundToInt(portal.up.x * 4096f), -32768, 32767));
                writer.Write((short)Mathf.Clamp(Mathf.RoundToInt(-portal.up.y * 4096f), -32768, 32767));
                writer.Write((short)Mathf.Clamp(Mathf.RoundToInt(portal.up.z * 4096f), -32768, 32767));
            }

            // Triangle refs (4 bytes each) — rooms in order, then catch-all
            for (int i = 0; i < _rooms.Length; i++)
            {
                foreach (var triRef in _roomTriRefs[i])
                {
                    writer.Write(triRef.objectIndex);
                    writer.Write(triRef.triangleIndex);
                }
            }
            foreach (var triRef in _catchAllTriRefs)
            {
                writer.Write(triRef.objectIndex);
                writer.Write(triRef.triangleIndex);
            }
        }

        public int GetBinarySize()
        {
            if (_rooms == null || _rooms.Length == 0) return 0;
            int roomDataSize = (_rooms.Length + 1) * 32; // +1 for catch-all
            int portalDataSize = _portals.Count * 40;
            int triRefSize = TotalTriRefCount * 4;
            return roomDataSize + portalDataSize + triRefSize;
        }
    }
}
