using UnityEngine;

namespace Density3.Core
{
    /// <summary>
    /// Teleports objects that may carry a CharacterController. The controller
    /// caches its position internally and overwrites plain transform writes,
    /// so it must be disabled around the move.
    /// </summary>
    public static class CharacterTeleport
    {
        public static void To(Transform target, Vector3 position, Quaternion rotation)
        {
            var cc = target.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            target.SetPositionAndRotation(position, rotation);
            if (cc != null) cc.enabled = true;
        }
    }
}
