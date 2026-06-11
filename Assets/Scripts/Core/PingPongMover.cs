using UnityEngine;

namespace Density3.Core
{
    /// <summary>Moves an object back and forth between two points (moving target).</summary>
    public class PingPongMover : MonoBehaviour
    {
        public Vector3 pointA;
        public Vector3 pointB;
        public float speed = 3f;

        private void Update()
        {
            float length = Vector3.Distance(pointA, pointB);
            if (length < 0.01f) return;
            float t = Mathf.PingPong(Time.time * speed / length, 1f);
            transform.position = Vector3.Lerp(pointA, pointB, t);
        }
    }
}
