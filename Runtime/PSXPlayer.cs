using UnityEngine;
using UnityEngine.Serialization;


namespace SplashEdit.RuntimeCode
{
    [Icon("Packages/net.psxsplash.splashedit/Icons/PSXPlayer.png")]
    public class PSXPlayer : MonoBehaviour
    {
        [Header("Player Dimensions")]
        [FormerlySerializedAs("PlayerHeight")]
        [Tooltip("Camera eye height above the player's feet")]
        [SerializeField] private float playerHeight = 1.8f;

        [Tooltip("Collision radius for wall sliding")]
        [SerializeField] private float playerRadius = 0.5f;

        [Header("Movement")]
        [Tooltip("Walk speed in world units per second")]
        [SerializeField] private float moveSpeed = 3.0f;

        [Tooltip("Sprint speed in world units per second")]
        [SerializeField] private float sprintSpeed = 8.0f;

        [Header("Navigation")]
        [Tooltip("Maximum height the agent can step up")]
        [SerializeField] private float maxStepHeight = 0.35f;

        [Tooltip("Maximum walkable slope angle in degrees")]
        [SerializeField] private float walkableSlopeAngle = 46.0f;

        [Tooltip("Voxel size in XZ plane (smaller = more accurate but slower)")]
        [SerializeField] private float navCellSize = 0.05f;

        [Tooltip("Voxel height (smaller = more accurate vertical resolution)")]
        [SerializeField] private float navCellHeight = 0.025f;

        [Header("Jump & Gravity")]
        [Tooltip("Peak jump height in world units")]
        [SerializeField] private float jumpHeight = 2.0f;

        [Tooltip("Downward acceleration in world units per second squared (positive value)")]
        [SerializeField] private float gravity = 20.0f;

        // Public accessors
        public float PlayerHeight => playerHeight;
        public float PlayerRadius => playerRadius;
        public float MoveSpeed => moveSpeed;
        public float SprintSpeed => sprintSpeed;
        public float MaxStepHeight => maxStepHeight;
        public float WalkableSlopeAngle => walkableSlopeAngle;
        public float NavCellSize => navCellSize;
        public float NavCellHeight => navCellHeight;
        public float JumpHeight => jumpHeight;
        public float Gravity => gravity;
        public Vector3 CamPoint { get; protected set; }

        public void FindNavmesh()
        {
            // Raycast down from the transform to find the ground,
            // then place CamPoint at ground + playerHeight
            if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 100f))
            {
                CamPoint = hit.point + new Vector3(0, playerHeight, 0);
            }
            else
            {
                // Fallback: no ground hit, use transform directly
                CamPoint = transform.position + new Vector3(0, playerHeight, 0);
            }
        }

        void OnDrawGizmos()
        {
            FindNavmesh();

            // Red sphere at camera eye point
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(CamPoint, 0.2f);

            // Wireframe sphere at feet showing player radius
            Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
            Vector3 feet = CamPoint - new Vector3(0, playerHeight, 0);
            Gizmos.DrawWireSphere(feet, playerRadius);
        }
    }
}
