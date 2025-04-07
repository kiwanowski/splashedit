using UnityEngine;
using Unity.AI.Navigation;
using UnityEngine.AI;
using System.Collections.Generic;

namespace SplashEdit.RuntimeCode
{
    public struct PSXNavMeshTri
    {
        public PSXNavmeshVertex v0, v1, v2;
    }

    public struct PSXNavmeshVertex
    {
        public short vx, vy, vz;
    }

    [RequireComponent(typeof(NavMeshSurface))]
    public class PSXNavMesh : MonoBehaviour
    {

        Mesh mesh;

        [HideInInspector]
        public List<PSXNavMeshTri> Navmesh { get; set; }

        public void CreateNavmesh(float GTEScaling)
        {
            mesh = new Mesh();
            Navmesh = new List<PSXNavMeshTri>();
            NavMeshSurface navMeshSurface = GetComponent<NavMeshSurface>();
            navMeshSurface.overrideTileSize = true;
            navMeshSurface.tileSize = 16;
            navMeshSurface.overrideVoxelSize = true;
            navMeshSurface.voxelSize = 0.1f;
            navMeshSurface.BuildNavMesh();
            NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
            navMeshSurface.overrideTileSize = false;
            navMeshSurface.overrideVoxelSize = false;

            int[] triangles = triangulation.indices;
            Vector3[] vertices = triangulation.vertices;

            mesh.vertices = vertices;
            mesh.triangles = triangles;

            mesh.RecalculateNormals();

            for (int i = 0; i < triangles.Length; i += 3)
            {
                int vid0 = triangles[i];
                int vid1 = triangles[i + 1];
                int vid2 = triangles[i + 2];

                PSXNavMeshTri tri = new PSXNavMeshTri();

                tri.v0.vx = PSXTrig.ConvertCoordinateToPSX(vertices[vid0].x, GTEScaling);
                tri.v0.vy = PSXTrig.ConvertCoordinateToPSX(-vertices[vid0].y, GTEScaling);
                tri.v0.vz = PSXTrig.ConvertCoordinateToPSX(vertices[vid0].z, GTEScaling);

                tri.v1.vx = PSXTrig.ConvertCoordinateToPSX(vertices[vid1].x, GTEScaling);
                tri.v1.vy = PSXTrig.ConvertCoordinateToPSX(-vertices[vid1].y, GTEScaling);
                tri.v1.vz = PSXTrig.ConvertCoordinateToPSX(vertices[vid1].z, GTEScaling);

                tri.v2.vx = PSXTrig.ConvertCoordinateToPSX(vertices[vid2].x, GTEScaling);
                tri.v2.vy = PSXTrig.ConvertCoordinateToPSX(-vertices[vid2].y, GTEScaling);
                tri.v2.vz = PSXTrig.ConvertCoordinateToPSX(vertices[vid2].z, GTEScaling);

                Navmesh.Add(tri);
            }
        }
        

        public void OnDrawGizmos()
        {
            if (mesh == null) return;
            Gizmos.DrawMesh(mesh);
            Gizmos.color = Color.green;

            var vertices = mesh.vertices;
            var triangles = mesh.triangles;

            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 v0 = vertices[triangles[i]];
                Vector3 v1 = vertices[triangles[i + 1]];
                Vector3 v2 = vertices[triangles[i + 2]];

                Gizmos.DrawLine(v0, v1);
                Gizmos.DrawLine(v1, v2);
                Gizmos.DrawLine(v2, v0);
            }
        }
    }
}