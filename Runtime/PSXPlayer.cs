using UnityEngine;
using UnityEngine.AI;


namespace SplashEdit.RuntimeCode
{
    public class PSXPlayer : MonoBehaviour
    {
        public float PlayerHeight;

        [HideInInspector]
        public Vector3 camPoint;
        float maxDistance = 1000f;

        public void FindNavmesh()
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(transform.position, out hit, maxDistance, NavMesh.AllAreas))
            {
                camPoint = hit.position + new Vector3(0, PlayerHeight, 0);
            }
        }
        void OnDrawGizmos()
        {
            FindNavmesh();
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(camPoint, 0.2f);
        }
    }
}
