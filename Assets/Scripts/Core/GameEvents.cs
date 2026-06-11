using System;

namespace Density3.Core
{
    public static class GameEvents
    {
        public static event Action EnemyKilled;

        public static void RaiseEnemyKilled() => EnemyKilled?.Invoke();
    }
}
