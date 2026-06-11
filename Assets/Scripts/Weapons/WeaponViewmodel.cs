using UnityEngine;

namespace FableFPS.Weapons
{
    /// <summary>
    /// One first-person weapon model. Each hand cannon frame has its own
    /// viewmodel under the shared sway/kick root; HandCannon activates the one
    /// matching the equipped frame and fires from its muzzle.
    /// </summary>
    public class WeaponViewmodel : MonoBehaviour
    {
        public Transform muzzlePoint;
        public Light muzzleLight;
    }
}
