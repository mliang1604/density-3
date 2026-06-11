using UnityEngine;
using Density3.Core;

namespace Density3.Abilities
{
    /// <summary>
    /// Framework smoke test: charges like any ability and, with autoActivate
    /// on, fires itself the moment the bar fills — log line plus a chime —
    /// then starts recharging. Drop it on any object with an AbilityData
    /// asset to watch the full charge -> activate -> reset loop before the
    /// input router exists. Not part of any class kit.
    /// </summary>
    public class DebugAbility : AbilityBase
    {
        public bool autoActivate = true;

        protected override void Update()
        {
            base.Update();
            if (autoActivate && IsReady) TryActivate();
        }

        protected override void OnActivate()
        {
            Debug.Log("DebugAbility: " + data.displayName + " activated (" + data.element + ")");
            SFX.Play2D(SFX.ReloadEndClip, 0.5f, 1.5f);
        }
    }
}
