using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using SplashEdit.RuntimeCode;

namespace SplashEdit.RuntimeCode
{
    /// <summary>
    /// Bounding Volume Hierarchy for PS1 frustum culling.
    /// Unlike BSP, BVH doesn't split triangles - it groups them by spatial locality.
    /// This is better for PS1 because:
    /// 1. No additional triangles created (memory constrained)
    /// 2. Simple AABB tests are fast on 33MHz CPU
    /// 3. Natural hierarchical culling
    /// </summary>
    public class BVH : IPSXBinaryWritable
    {
        // Configuration
        private const int MAX_TRIANGLES_PER_LEAF = 64;  // PS1 can handle batches of this size
        private const int MAX_DEPTH = 16;               // Prevent pathological cases
        private const int MIN_TRIANGLES_TO_SPLIT = 8;   // Don't split tiny groups
        
        private List<PSXObjectExporter> _objects;
        private BVHNode _root;
        private List<BVHNode> _allNodes;  // Flat list for export
        private List<TriangleRef> _allTriangleRefs;  // Triangle references for export
        
        public int NodeCount => _allNodes?.Count ?? 0;
        public int TriangleRefCount => _allTriangleRefs?.Count ?? 0;
        
        /// <summary>
        /// Reference to a triangle - doesn't copy data, just points to it
        /// </summary>
        public struct TriangleRef
        {
            public ushort objectIndex;      // Which GameObject
            public ushort triangleIndex;    // Which triangle in that object's mesh
            
            public TriangleRef(int objIdx, int triIdx)
            {
                objectIndex = (ushort)objIdx;
                triangleIndex = (ushort)triIdx;
            }
        }
        
        /// <summary>
        /// BVH Node - 32 bytes when exported
        /// </summary>
        public class BVHNode
        {
            public Bounds bounds;
            public BVHNode left;
            public BVHNode right;
            public List<TriangleRef> triangles;  // Only for leaf nodes
            public int depth;
            
            // Export indices (filled during serialization)
            public int nodeIndex = -1;
            public int leftIndex = -1;   // -1 = no child (leaf check)
            public int rightIndex = -1;
            public int firstTriangleIndex = -1;
            public int triangleCount = 0;
            
            public bool IsLeaf => left == null && right == null;
        }
        
        /// <summary>
        /// Triangle with bounds for building
        /// </summary>
        private struct TriangleWithBounds
        {
            public TriangleRef reference;
            public Bounds bounds;
            public Vector3 centroid;
        }
        
        public BVH(List<PSXObjectExporter> objects)
        {
            _objects = objects;
            _allNodes = new List<BVHNode>();
            _allTriangleRefs = new List<TriangleRef>();
        }
        
        public void Build()
        {
            _allNodes.Clear();
            _allTriangleRefs.Clear();
            
            // Extract all triangles with their bounds
            List<TriangleWithBounds> triangles = ExtractTriangles();
            
            if (triangles.Count == 0)
            {
                Debug.LogWarning("BVH: No triangles to process");
                return;
            }
            
            // Build the tree
            _root = BuildNode(triangles, 0);
            
            // Flatten for export
            FlattenTree();
        }
        
        private List<TriangleWithBounds> ExtractTriangles()
        {
            var result = new List<TriangleWithBounds>();
            
            for (int objIdx = 0; objIdx < _objects.Count; objIdx++)
            {
                var exporter = _objects[objIdx];
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
                    
                    // Calculate bounds
                    Bounds triBounds = new Bounds(v0, Vector3.zero);
                    triBounds.Encapsulate(v1);
                    triBounds.Encapsulate(v2);
                    
                    result.Add(new TriangleWithBounds
                    {
                        reference = new TriangleRef(objIdx, i / 3),
                        bounds = triBounds,
                        centroid = (v0 + v1 + v2) / 3f
                    });
                }
            }
            
            return result;
        }
        
        private BVHNode BuildNode(List<TriangleWithBounds> triangles, int depth)
        {
            if (triangles.Count == 0)
                return null;
            
            var node = new BVHNode { depth = depth };
            
            // Calculate bounds encompassing all triangles
            node.bounds = triangles[0].bounds;
            foreach (var tri in triangles)
            {
                node.bounds.Encapsulate(tri.bounds);
            }
            
            // Create leaf if conditions met
            if (triangles.Count <= MAX_TRIANGLES_PER_LEAF || 
                depth >= MAX_DEPTH ||
                triangles.Count < MIN_TRIANGLES_TO_SPLIT)
            {
                node.triangles = triangles.Select(t => t.reference).ToList();
                return node;
            }
            
            // Find best split axis (longest extent)
            Vector3 extent = node.bounds.size;
            int axis = 0;
            if (extent.y > extent.x && extent.y > extent.z) axis = 1;
            else if (extent.z > extent.x && extent.z > extent.y) axis = 2;
            
            // Sort by centroid along chosen axis
            triangles.Sort((a, b) => 
            {
                float va = axis == 0 ? a.centroid.x : (axis == 1 ? a.centroid.y : a.centroid.z);
                float vb = axis == 0 ? b.centroid.x : (axis == 1 ? b.centroid.y : b.centroid.z);
                return va.CompareTo(vb);
            });
            
            // Find split plane position at median centroid
            int mid = triangles.Count / 2;
            if (mid == 0) mid = 1;
            if (mid >= triangles.Count) mid = triangles.Count - 1;
            
            float splitPos = axis == 0 ? triangles[mid].centroid.x : 
                            (axis == 1 ? triangles[mid].centroid.y : triangles[mid].centroid.z);
            
            // Partition triangles - allow overlap for triangles spanning the split plane
            var leftTris = new List<TriangleWithBounds>();
            var rightTris = new List<TriangleWithBounds>();
            
            foreach (var tri in triangles)
            {
                float triMin = axis == 0 ? tri.bounds.min.x : (axis == 1 ? tri.bounds.min.y : tri.bounds.min.z);
                float triMax = axis == 0 ? tri.bounds.max.x : (axis == 1 ? tri.bounds.max.y : tri.bounds.max.z);
                
                // Triangle spans split plane - add to BOTH children (spatial split)
                // This fixes large triangles at screen edges being culled incorrectly
                if (triMin < splitPos && triMax > splitPos)
                {
                    leftTris.Add(tri);
                    rightTris.Add(tri);
                }
                // Triangle entirely on left side
                else if (triMax <= splitPos)
                {
                    leftTris.Add(tri);
                }
                // Triangle entirely on right side
                else
                {
                    rightTris.Add(tri);
                }
            }
            
            // Check if split is beneficial (prevents infinite recursion on coincident triangles)
            if (leftTris.Count == 0 || rightTris.Count == 0 ||
                (leftTris.Count == triangles.Count && rightTris.Count == triangles.Count))
            {
                node.triangles = triangles.Select(t => t.reference).ToList();
                return node;
            }
            
            node.left = BuildNode(leftTris, depth + 1);
            node.right = BuildNode(rightTris, depth + 1);
            
            return node;
        }
        
        /// <summary>
        /// Flatten tree to arrays for export
        /// </summary>
        private void FlattenTree()
        {
            _allNodes.Clear();
            _allTriangleRefs.Clear();
            
            if (_root == null) return;
            
            // BFS to assign indices
            var queue = new Queue<BVHNode>();
            queue.Enqueue(_root);
            
            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                node.nodeIndex = _allNodes.Count;
                _allNodes.Add(node);
                
                if (node.left != null) queue.Enqueue(node.left);
                if (node.right != null) queue.Enqueue(node.right);
            }
            
            // Second pass: fill in child indices and triangle data
            foreach (var node in _allNodes)
            {
                if (node.left != null)
                    node.leftIndex = node.left.nodeIndex;
                if (node.right != null)
                    node.rightIndex = node.right.nodeIndex;
                
                if (node.IsLeaf && node.triangles != null && node.triangles.Count > 0)
                {
                    // Sort tri-refs by objectIndex within each leaf so the C++ renderer
                    // can batch consecutive refs and avoid redundant GTE matrix reloads.
                    node.triangles.Sort((a, b) => a.objectIndex.CompareTo(b.objectIndex));
                    node.firstTriangleIndex = _allTriangleRefs.Count;
                    node.triangleCount = node.triangles.Count;
                    _allTriangleRefs.AddRange(node.triangles);
                }
            }
        }
        
        /// <summary>
        /// Export BVH to binary writer
        /// Format:
        /// - uint16 nodeCount
        /// - uint16 triangleRefCount
        /// - BVHNode[nodeCount] (32 bytes each)
        /// - TriangleRef[triangleRefCount] (4 bytes each)
        /// </summary>
        public void WriteToBinary(BinaryWriter writer, float gteScaling)
        {
            // Note: counts are already in the file header (bvhNodeCount, bvhTriangleRefCount)
            // Don't write them again here - C++ reads BVH data directly after colliders
            
            // Write nodes (32 bytes each)
            foreach (var node in _allNodes)
            {
                // AABB bounds (24 bytes)
                Vector3 min = node.bounds.min;
                Vector3 max = node.bounds.max;
                
                writer.Write(PSXTrig.ConvertWorldToFixed12(min.x / gteScaling));
                writer.Write(PSXTrig.ConvertWorldToFixed12(-max.y / gteScaling)); // Y flipped 
                writer.Write(PSXTrig.ConvertWorldToFixed12(min.z / gteScaling));
                writer.Write(PSXTrig.ConvertWorldToFixed12(max.x / gteScaling));
                writer.Write(PSXTrig.ConvertWorldToFixed12(-min.y / gteScaling)); // Y flipped
                writer.Write(PSXTrig.ConvertWorldToFixed12(max.z / gteScaling));
                
                // Child indices (4 bytes) - 0xFFFF means no child
                writer.Write((ushort)(node.leftIndex >= 0 ? node.leftIndex : 0xFFFF));
                writer.Write((ushort)(node.rightIndex >= 0 ? node.rightIndex : 0xFFFF));
                
                // Triangle data (4 bytes)
                writer.Write((ushort)(node.firstTriangleIndex >= 0 ? node.firstTriangleIndex : 0));
                writer.Write((ushort)node.triangleCount);
            }
            
            // Write triangle references (4 bytes each)
            foreach (var triRef in _allTriangleRefs)
            {
                writer.Write(triRef.objectIndex);
                writer.Write(triRef.triangleIndex);
            }
        }
        
        /// <summary>
        /// Get total bytes that will be written
        /// </summary>
        public int GetBinarySize()
        {
            // Just nodes + triangle refs, counts are in file header
            return (_allNodes.Count * 32) + (_allTriangleRefs.Count * 4);
        }
        
        /// <summary>
        /// Draw gizmos for debugging
        /// </summary>
        public void DrawGizmos(int maxDepth = 999)
        {
            if (_root == null) return;
            DrawNodeGizmos(_root, maxDepth);
        }
        
        private void DrawNodeGizmos(BVHNode node, int maxDepth)
        {
            if (node == null || node.depth > maxDepth) return;
            
            // Color by depth
            Color c = Color.HSVToRGB((node.depth * 0.12f) % 1f, 0.7f, 0.9f);
            c.a = node.IsLeaf ? 0.3f : 0.1f;
            Gizmos.color = c;
            
            // Draw bounds
            Gizmos.DrawWireCube(node.bounds.center, node.bounds.size);
            
            if (node.IsLeaf)
            {
                // Draw leaf as semi-transparent
                Gizmos.color = new Color(c.r, c.g, c.b, 0.1f);
                Gizmos.DrawCube(node.bounds.center, node.bounds.size);
            }
            
            // Recurse
            DrawNodeGizmos(node.left, maxDepth);
            DrawNodeGizmos(node.right, maxDepth);
        }
        
        /// <summary>
        /// Get statistics for debugging
        /// </summary>
        public string GetStatistics()
        {
            if (_root == null) return "BVH not built";
            
            int leafCount = 0;
            int maxDepth = 0;
            int totalTris = 0;
            
            void CountNodes(BVHNode node)
            {
                if (node == null) return;
                if (node.depth > maxDepth) maxDepth = node.depth;
                if (node.IsLeaf)
                {
                    leafCount++;
                    totalTris += node.triangleCount;
                }
                CountNodes(node.left);
                CountNodes(node.right);
            }
            
            CountNodes(_root);
            
            return $"Nodes: {_allNodes.Count}, Leaves: {leafCount}, Max Depth: {maxDepth}, Triangle Refs: {totalTris}";
        }
    }
}
