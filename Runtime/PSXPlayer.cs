using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;


namespace SplashEdit.RuntimeCode
{
    public class PSXPlayer : MonoBehaviour
    {
        private const float LookOutDistance = 1000f;

        [FormerlySerializedAs("PlayerHeight")]
        [SerializeField] private float playerHeight;

        public float PlayerHeight => playerHeight;
        public Vector3 CamPoint { get; protected set; }

        public void FindNavmesh()
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, LookOutDistance, NavMesh.AllAreas))
            {
                CamPoint = hit.position + new Vector3(0, PlayerHeight, 0);
            }
        }

        void OnDrawGizmos()
        {
            FindNavmesh();
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(CamPoint, 0.2f);
        }
    }
}
