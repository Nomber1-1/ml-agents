using UnityEngine;

namespace CustomWalkingSoccerTwos
{
    // Attach this to the Ball GameObject to improve bounce behavior on walls
    public class BallBounceHelper : MonoBehaviour
    {
        [Header("Bounce Tweaks")]
        [Tooltip("Extra impulse scale applied when colliding with walls.")]
        public float wallBounceImpulseScale = 1.1f; // 10% extra impulse

        [Tooltip("Minimum normal coefficient to treat as a wall (avoid floor).")]
        public float minWallNormalY = 0.2f; // walls have mostly horizontal normals

        [Tooltip("Clamp for very shallow hits so ball doesn't stick.")]
        public float minOutgoingSpeed = 1.5f;

        private Rigidbody rb;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
        }

        void OnCollisionEnter(Collision collision)
        {
            // Treat any collider tagged "Wall" or with near-horizontal normal as a wall
            bool isWallTag = collision.collider.CompareTag("wall");
            ContactPoint cp = collision.GetContact(0);
            bool isWallNormal = Mathf.Abs(cp.normal.y) <= minWallNormalY;
            if (!isWallTag && !isWallNormal)
                return;

            Vector3 v = rb.linearVelocity;
            // Reflect velocity about the contact normal
            Vector3 reflected = Vector3.Reflect(v, cp.normal);

            // Apply a small amplification to counter energy loss
            reflected *= wallBounceImpulseScale;

            // Ensure a minimum outgoing speed to avoid sticking at shallow angles
            float speed = reflected.magnitude;
            if (speed < minOutgoingSpeed)
            {
                reflected = reflected.normalized * minOutgoingSpeed;
            }

            rb.linearVelocity = reflected;
        }
    }
}
