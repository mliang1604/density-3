using System.Collections.Generic;
using UnityEngine;
using Density3.Enemies;

namespace Density3.Core
{
    /// <summary>
    /// Procedural skeletal animation for the Dreg rig — no AnimationClips needed.
    /// Drives the bones every LateUpdate: idle breathing, a speed-driven walk
    /// cycle, weapon recoil, glowing-eye flicker, and a death collapse. Bone
    /// references are serialized in the prefab (assigned by the editor bootstrap)
    /// and the bind pose is captured from the authored pose in Awake.
    ///
    /// All motion is expressed as Euler offsets from each bone's bind pose, so a
    /// sculpted skinned mesh bound to the same bones would animate identically.
    /// </summary>
    public class DregAnimator : MonoBehaviour
    {
        [Header("Skeleton (prefab references)")]
        public Transform pelvis;
        public Transform spine;
        public Transform chest;
        public Transform neck;
        public Transform head;
        public Transform shoulderL;
        public Transform shoulderR;
        public Transform upperArmL;
        public Transform upperArmR;
        public Transform forearmL;
        public Transform forearmR;
        [Tooltip("Second (lower) arm pair — optional.")]
        public Transform lowerUpperArmL;
        public Transform lowerUpperArmR;
        public Transform lowerForearmL;
        public Transform lowerForearmR;
        public Transform thighL;
        public Transform thighR;
        public Transform shinL;
        public Transform shinR;
        public Transform footL;
        public Transform footR;

        [Header("Eyes (prefab references)")]
        public Renderer[] eyeRenderers;

        private Health health;
        private ChaserEnemy ai;

        private readonly Dictionary<Transform, Quaternion> bind = new Dictionary<Transform, Quaternion>();
        private Vector3 pelvisBasePos;
        private Color[] eyeBaseEmission;

        private Vector3 lastPos;
        private float speed;
        private float gait;
        private float recoil;
        private float deathBlend;
        private bool wasDead;
        private bool ready;
        private float time;

        private void Awake()
        {
            health = GetComponent<Health>();
            ai = GetComponent<ChaserEnemy>();
            if (ai != null) ai.Fired += OnFired;

            foreach (var t in AllBones()) CaptureBind(t);
            if (pelvis != null) pelvisBasePos = pelvis.localPosition;

            if (eyeRenderers != null)
            {
                eyeBaseEmission = new Color[eyeRenderers.Length];
                for (int i = 0; i < eyeRenderers.Length; i++)
                    if (eyeRenderers[i] != null)
                        eyeBaseEmission[i] = eyeRenderers[i].material.GetColor("_EmissionColor");
            }

            lastPos = transform.position;
            ready = true;
        }

        private void OnDestroy()
        {
            if (ai != null) ai.Fired -= OnFired;
        }

        private void OnFired() => recoil = 1f;

        /// <summary>Called by DregDeath after a respawn to clear death/motion state
        /// (the animator is disabled during the ragdoll, so it can't self-reset).</summary>
        public void OnRevived()
        {
            deathBlend = 0f;
            speed = 0f;
            gait = 0f;
            recoil = 0f;
            lastPos = transform.position;
        }

        private IEnumerable<Transform> AllBones()
        {
            return new[]
            {
                pelvis, spine, chest, neck, head,
                shoulderL, shoulderR, upperArmL, upperArmR, forearmL, forearmR,
                lowerUpperArmL, lowerUpperArmR, lowerForearmL, lowerForearmR,
                thighL, thighR, shinL, shinR, footL, footR
            };
        }

        private void CaptureBind(Transform t)
        {
            if (t != null && !bind.ContainsKey(t)) bind[t] = t.localRotation;
        }

        private void LateUpdate()
        {
            if (!ready) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            time += dt;

            bool dead = health != null && health.IsDead;
            if (!dead && wasDead) // just respawned — snap back to a live pose
            {
                deathBlend = 0f;
                speed = 0f;
                lastPos = transform.position;
            }
            wasDead = dead;
            deathBlend = Mathf.MoveTowards(deathBlend, dead ? 1f : 0f, dt / (dead ? 0.6f : 0.25f));

            // Planar speed estimated from world movement (the AI moves the root).
            Vector3 p = transform.position;
            Vector3 flat = p - lastPos; flat.y = 0f;
            lastPos = p;
            speed = Mathf.Lerp(speed, flat.magnitude / dt, 12f * dt);
            float move = Mathf.Clamp01(speed / 2.2f);

            recoil = Mathf.MoveTowards(recoil, 0f, dt / 0.18f);

            gait += (2f + speed * 3f) * dt;
            float sw = Mathf.Sin(gait);
            float swCos = Mathf.Cos(gait);
            float breathe = Mathf.Sin(time * 1.6f) * (1f - move);
            float legSwing = sw * 30f * move;
            float armSwing = sw * 18f * move;
            float kick = recoil * 28f;

            // Torso + head: hunch, breathe, subtle sway. Death = forward slump.
            Pose(spine, new Vector3(6f + breathe * 2f, 0f, 0f), new Vector3(55f, 0f, 8f));
            Pose(chest, new Vector3(8f + breathe * 1.5f + Mathf.Abs(sw) * 3f * move, sw * 2.5f * move, 0f), new Vector3(40f, 0f, -6f));
            Pose(neck, new Vector3(-6f, 0f, 0f), new Vector3(35f, 10f, 12f));
            Pose(head, new Vector3(-4f + breathe, swCos * 2f * move, 0f), new Vector3(30f, 22f, 18f));

            // Arms counter-swing the legs; firing kicks them back.
            Pose(upperArmL, new Vector3(-armSwing - kick * 0.6f, 0f, 0f), new Vector3(-55f, 0f, 0f));
            Pose(upperArmR, new Vector3(armSwing - kick * 0.6f, 0f, 0f), new Vector3(-55f, 0f, 0f));
            Pose(forearmL, new Vector3(-kick, 0f, 0f), new Vector3(-75f, 0f, 0f));
            Pose(forearmR, new Vector3(-kick, 0f, 0f), new Vector3(-75f, 0f, 0f));

            // The lower arm pair counter-swings the upper pair, more subtly.
            Pose(lowerUpperArmL, new Vector3(armSwing * 0.6f - kick * 0.3f, 0f, 0f), new Vector3(-45f, 0f, 0f));
            Pose(lowerUpperArmR, new Vector3(-armSwing * 0.6f - kick * 0.3f, 0f, 0f), new Vector3(-45f, 0f, 0f));
            Pose(lowerForearmL, new Vector3(-kick * 0.5f, 0f, 0f), new Vector3(-60f, 0f, 0f));
            Pose(lowerForearmR, new Vector3(-kick * 0.5f, 0f, 0f), new Vector3(-60f, 0f, 0f));

            // Legs: alternating stride; knees fold as each leg passes under.
            float bendL = Mathf.Max(0f, -sw) * 45f * move + 6f * move;
            float bendR = Mathf.Max(0f, sw) * 45f * move + 6f * move;
            Pose(thighL, new Vector3(legSwing, 0f, 0f), new Vector3(75f, 0f, 0f));
            Pose(thighR, new Vector3(-legSwing, 0f, 0f), new Vector3(75f, 0f, 0f));
            Pose(shinL, new Vector3(-bendL, 0f, 0f), new Vector3(-95f, 0f, 0f));
            Pose(shinR, new Vector3(-bendR, 0f, 0f), new Vector3(-95f, 0f, 0f));
            Pose(footL, new Vector3(-legSwing * 0.4f, 0f, 0f), new Vector3(25f, 0f, 0f));
            Pose(footR, new Vector3(legSwing * 0.4f, 0f, 0f), new Vector3(25f, 0f, 0f));

            // Pelvis bob while moving; on death the whole body drops and folds.
            if (pelvis != null && bind.ContainsKey(pelvis))
            {
                float bob = Mathf.Abs(sw) * 0.02f * move + breathe * 0.004f;
                Vector3 alivePos = pelvisBasePos + Vector3.up * bob;
                Vector3 deadPos = pelvisBasePos + new Vector3(0f, -0.5f, 0.12f);
                pelvis.localPosition = Vector3.Lerp(alivePos, deadPos, deathBlend);

                Quaternion aliveRot = bind[pelvis] * Quaternion.Euler(2f * move * sw, 0f, 0f);
                Quaternion deadRot = bind[pelvis] * Quaternion.Euler(72f, 0f, 10f);
                pelvis.localRotation = Quaternion.Slerp(aliveRot, deadRot, deathBlend);
            }

            // Glowing eyes pulse, then fade out as the Dreg dies.
            if (eyeRenderers != null && eyeBaseEmission != null)
            {
                float flick = (0.7f + 0.3f * Mathf.Sin(time * 9f)) * (1f - deathBlend);
                for (int i = 0; i < eyeRenderers.Length; i++)
                    if (eyeRenderers[i] != null)
                        eyeRenderers[i].material.SetColor("_EmissionColor", eyeBaseEmission[i] * flick);
            }
        }

        /// <summary>Blend a bone from its live offset toward a death-pose offset.</summary>
        private void Pose(Transform t, Vector3 liveEuler, Vector3 deathEuler)
        {
            if (t == null || !bind.ContainsKey(t)) return;
            Quaternion b = bind[t];
            Quaternion live = b * Quaternion.Euler(liveEuler);
            Quaternion dead = b * Quaternion.Euler(deathEuler);
            t.localRotation = Quaternion.Slerp(live, dead, deathBlend);
        }
    }
}
