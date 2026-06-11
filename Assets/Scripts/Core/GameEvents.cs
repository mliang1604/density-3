using System;
using UnityEngine;

namespace Density3.Core
{
    public static class GameEvents
    {
        public static event Action EnemyKilled;

        /// <summary>Announce an enemy kill: plays the death sting at the corpse
        /// and raises EnemyKilled, so kill feedback stays in sync everywhere.</summary>
        public static void AnnounceEnemyKilled(Vector3 position)
        {
            SFX.Play3D(SFX.EnemyDeathClip, position, 0.9f);
            EnemyKilled?.Invoke();
        }
    }
}
