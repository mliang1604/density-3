using UnityEngine;
using UnityEngine.Serialization;
using Density3.Core;

namespace Density3.Player
{
    /// <summary>Class movement identity, applied by ClassLoadout: Warlocks
    /// glide; the strafe/triple jump styles are kept for the Hunter kit.</summary>
    public enum JumpStyle
    {
        StrafeJump,
        TripleJump,
        Glide
    }

    /// <summary>
    /// First-person character controller with Destiny-style movement:
    /// sprint, class-styled jumps (strafe/triple leaps or Warlock glide),
    /// crouch, crouch-slide, and weapon-driven recoil.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        public float walkSpeed = 6.5f;
        public float sprintSpeed = 11.5f;
        [Range(0.2f, 1f)] public float adsSpeedScale = 0.55f;
        public float acceleration = 40f;
        [Range(0f, 1f)] public float airControl = 0.5f;

        [Header("Crouch / Slide")]
        public KeyCode crouchKey = KeyCode.C;
        public float crouchSpeed = 3.4f;
        public float crouchHeight = 1.0f;
        public float crouchLerp = 12f;
        public float minSlideSpeed = 7.5f;   // must be roughly sprinting to slide
        public float slideSpeed = 14f;       // initial slide burst
        public float slideDuration = 1.4f;
        public float slideUncrouchGrace = 0.25f; // releases earlier than this don't cancel, so a tap slides fully
        public float slideCooldown = 0.9f;   // D2-style internal cooldown after a slide ends
        public float slideFriction = 4.5f;   // m/s² the slide bleeds off
        public float slideCameraDip = 0.15f; // extra camera drop while sliding

        [Header("Jump")]
        [Tooltip("Set by ClassLoadout per class. Warlocks glide; strafe/triple stay implemented for the Hunter kit (M3).")]
        public JumpStyle jumpStyle = JumpStyle.StrafeJump;
        public float jumpHeight = 3.5f;
        [Tooltip("Air jumps for the leap styles: 1 = strafe (one big boosted leap), 2 = triple (two lower hops).")]
        [Range(1, 2)] public int airJumps = 1;
        [FormerlySerializedAs("doubleJumpHeight")]
        public float strafeJumpHeight = 2.5f; // 3.5 ground + 2.5 = 6 m max stack
        [Tooltip("Horizontal burst of the single strafe jump — a long forward lunge.")]
        public float strafeJumpBoost = 8f;
        [Tooltip("Triple-jump hops are lower and gentler, trading the strafe lunge for flexibility.")]
        public float tripleJumpHeight = 1.75f; // 3.5 + 1.75 + 1.75 = 7 m max stack
        public float tripleJumpBoost = 2f;
        public float gravity = -24f;

        [Header("Glide (Warlock)")]
        public float glideGravity = -4f;
        public float glideMaxFallSpeed = -2.5f;
        [Tooltip("Strafe Glide's signature: near-full air control while gliding.")]
        [Range(0f, 1f)] public float glideAirControl = 1f;

        [Header("Look")]
        public float mouseSensitivity = 2.2f;
        public float maxPitch = 89f;

        [Header("Wiring (prefab references)")]
        public Camera playerCamera;
        public Transform cameraPivot;

        public bool MovementLocked { get; set; }
        public bool IsSprinting { get; private set; }
        public float SpeedScale { get; set; } = 1f;
        public float SensitivityScale { get; set; } = 1f;
        public float CursorLockedAt { get; private set; }

        private CharacterController controller;
        private Vector3 velocity;
        private float yaw;
        private float pitch;
        private int airJumpsLeft;
        private Vector2 recoil; // x = pitch kick (up), y = yaw kick
        private float recoilRecovery = 8f;

        private bool isCrouching;
        private bool isSliding;
        private float slideTimer;
        private float lastSlideEnd = -999f; // far past so the first slide is never gated
        private Vector3 slideDir;
        private Vector3 overrideVelocity;
        private float overrideTimer;
        private bool isGliding;
        private float crouchBlend;   // 0 = standing, 1 = fully crouched
        private float standHeight;
        private float standCenterY;
        private float standCamY;

        public bool IsCrouching => isCrouching;
        public bool IsSliding => isSliding;
        public bool IsGliding => isGliding;
        public bool MovementOverridden => overrideTimer > 0f;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            yaw = transform.eulerAngles.y;
            standHeight = controller.height;
            standCenterY = controller.center.y;
        }

        private void Start()
        {
            LockCursor();
            if (cameraPivot != null) standCamY = cameraPivot.localPosition.y;
        }

        private void Update()
        {
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                if (Input.GetMouseButtonDown(0)) LockCursor();
                return;
            }
            if (MovementLocked) return;

            HandleLook();
            HandleMovement();
        }

        public void AddRecoil(float pitchKick, float yawKick)
        {
            recoil.x += pitchKick;
            recoil.y += Random.Range(-yawKick, yawKick);
        }

        public void SetRecoilRecovery(float speed) => recoilRecovery = speed;

        /// <summary>Instantaneous velocity change (super slams, knockback).</summary>
        public void AddImpulse(Vector3 impulse) => velocity += impulse;

        /// <summary>
        /// Drives horizontal movement at a fixed velocity for a short window
        /// (dodges, shoulder charges), suspending input control. Gravity and
        /// jumps still apply; the velocity's y component is ignored. Cancels
        /// any slide in progress.
        /// </summary>
        public void OverrideMove(Vector3 moveVelocity, float seconds)
        {
            overrideVelocity = moveVelocity;
            overrideTimer = seconds;
            if (isSliding) EndSlide();
        }

        public void ResetLook(float newYaw)
        {
            yaw = newYaw;
            pitch = 0f;
            recoil = Vector2.zero;
            isSliding = false;
            isCrouching = false;
            isGliding = false;
            velocity = Vector3.zero;
            overrideTimer = 0f;
        }

        private void LockCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            CursorLockedAt = Time.time;
        }

        private void HandleLook()
        {
            // Swallow mouse deltas briefly after locking the cursor — the OS
            // pointer snap on lock arrives as one huge delta that would pitch
            // the camera into the ground on scene start (and on re-lock).
            if (Time.time - CursorLockedAt > 0.15f)
            {
                yaw += Input.GetAxis("Mouse X") * mouseSensitivity * SensitivityScale;
                pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity * SensitivityScale;
            }
            pitch = Mathf.Clamp(pitch, -maxPitch, maxPitch);

            recoil = Vector2.Lerp(recoil, Vector2.zero, recoilRecovery * Time.deltaTime);

            transform.rotation = Quaternion.Euler(0f, yaw + recoil.y, 0f);
            if (cameraPivot != null)
                cameraPivot.localRotation = Quaternion.Euler(
                    Mathf.Clamp(pitch - recoil.x, -maxPitch, maxPitch), 0f, 0f);
        }

        private void HandleMovement()
        {
            bool grounded = controller.isGrounded;
            if (grounded)
            {
                airJumpsLeft = airJumps;
                isGliding = false;
                if (velocity.y < 0f) velocity.y = -2f;
            }

            Vector2 input = Vector2.ClampMagnitude(
                new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")), 1f);

            Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
            IsSprinting = Input.GetKey(KeyCode.LeftShift) && input.y > 0.1f && !isCrouching && !isSliding;

            if (overrideTimer > 0f)
            {
                // Ability-driven movement (dodge, shoulder charge) owns the
                // horizontal until its window ends.
                overrideTimer -= Time.deltaTime;
                horizontal = new Vector3(overrideVelocity.x, 0f, overrideVelocity.z);
            }
            else
            {
                // Crouch pressed while sprinting fast on the ground -> slide,
                // unless the previous slide ended too recently.
                if (!isSliding && grounded && Input.GetKeyDown(crouchKey)
                    && horizontal.magnitude >= minSlideSpeed
                    && Time.time - lastSlideEnd >= slideCooldown)
                    StartSlide(horizontal);

                if (isSliding)
                {
                    UpdateSlide(ref horizontal, grounded);
                }
                else
                {
                    // Hold to crouch; stay crouched if there's no headroom to stand.
                    isCrouching = Input.GetKey(crouchKey) || (isCrouching && !CanStand());

                    float targetSpeed = (isCrouching ? crouchSpeed
                        : IsSprinting ? sprintSpeed : walkSpeed) * SpeedScale;
                    Vector3 wishDir = transform.right * input.x + transform.forward * input.y;
                    float control = grounded ? 1f : isGliding ? glideAirControl : airControl;
                    horizontal = Vector3.MoveTowards(horizontal, wishDir * targetSpeed,
                        acceleration * control * Time.deltaTime);
                }
            }

            velocity.x = horizontal.x;
            velocity.z = horizontal.z;

            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (grounded)
                {
                    if (isSliding) EndSlide();
                    velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                }
                else if (jumpStyle == JumpStyle.Glide)
                {
                    // Warlock Strafe Glide: toggle a floaty, fully steerable
                    // descent. Space again (or landing) ends it.
                    isGliding = !isGliding;
                    if (isGliding && velocity.y < 0f) velocity.y *= 0.3f; // catch the fall
                }
                else if (airJumpsLeft > 0)
                {
                    airJumpsLeft--;
                    bool triple = jumpStyle == JumpStyle.TripleJump;

                    // Hunter-bound styles, kept implemented for M3.
                    // Strafe mode: one tall leap with a big lunge and a generous
                    // speed cap — covers more ground than triple jump's two
                    // lower, gently-boosted hops (which win on flexibility).
                    velocity.y = Mathf.Sqrt((triple ? tripleJumpHeight : strafeJumpHeight) * -2f * gravity);

                    Vector3 boostDir = transform.right * input.x + transform.forward * input.y;
                    boostDir = boostDir.sqrMagnitude > 0.01f ? boostDir.normalized : transform.forward;
                    Vector3 boosted = new Vector3(velocity.x, 0f, velocity.z)
                        + boostDir * (triple ? tripleJumpBoost : strafeJumpBoost);
                    float maxAirSpeed = sprintSpeed * (triple ? 1.15f : 1.7f);
                    if (boosted.magnitude > maxAirSpeed) boosted = boosted.normalized * maxAirSpeed;
                    velocity.x = boosted.x;
                    velocity.z = boosted.z;

                    SFX.Play2D(SFX.StrafeJumpClip, 0.5f, Random.Range(0.95f, 1.05f));
                }
            }

            velocity.y += (isGliding ? glideGravity : gravity) * Time.deltaTime;
            if (isGliding) velocity.y = Mathf.Max(velocity.y, glideMaxFallSpeed);
            controller.Move(velocity * Time.deltaTime);

            UpdateCrouchPose();
        }

        private void StartSlide(Vector3 horizontal)
        {
            isSliding = true;
            isCrouching = true;
            slideTimer = slideDuration;
            slideDir = horizontal.sqrMagnitude > 0.01f ? horizontal.normalized : transform.forward;
            float startSpeed = Mathf.Max(slideSpeed, horizontal.magnitude);
            velocity.x = slideDir.x * startSpeed;
            velocity.z = slideDir.z * startSpeed;
            SFX.Play2D(SFX.SlideClip, 0.6f);
        }

        private void UpdateSlide(ref Vector3 horizontal, bool grounded)
        {
            slideTimer -= Time.deltaTime;
            float speed = Mathf.Max(0f, horizontal.magnitude - slideFriction * Time.deltaTime);

            // The slide is committed to the direction it started in.
            horizontal = slideDir * speed;

            // Ends on timeout, losing speed, leaving the ground, or letting go
            // of crouch to stand up out of it. A release inside the grace
            // window doesn't count, so a quick tap still gives a full slide.
            bool uncrouched = Input.GetKeyUp(crouchKey)
                && slideDuration - slideTimer > slideUncrouchGrace;
            if (slideTimer <= 0f || speed <= crouchSpeed || !grounded || uncrouched)
                EndSlide();
        }

        private void EndSlide()
        {
            isSliding = false;
            lastSlideEnd = Time.time;
            isCrouching = Input.GetKey(crouchKey) || !CanStand();
        }

        /// <summary>Is there headroom to return to full standing height?</summary>
        private bool CanStand()
        {
            float need = standHeight - controller.height;
            if (need <= 0.01f) return true;
            Vector3 top = transform.position + Vector3.up * (controller.height * 0.5f - controller.radius);
            // Exclude layer 2 (the player itself and any corpses) from the check.
            return !Physics.SphereCast(top, controller.radius * 0.9f, Vector3.up,
                out _, need + 0.1f, ~(1 << 2), QueryTriggerInteraction.Ignore);
        }

        /// <summary>Lerps controller height and camera height toward the current
        /// crouch/slide state.</summary>
        private void UpdateCrouchPose()
        {
            float target = (isCrouching || isSliding) ? 1f : 0f;
            crouchBlend = Mathf.MoveTowards(crouchBlend, target, crouchLerp * Time.deltaTime);

            float h = Mathf.Lerp(standHeight, crouchHeight, crouchBlend);
            controller.height = h;
            var c = controller.center;
            c.y = standCenterY - (standHeight - h) * 0.5f; // keep the feet planted
            controller.center = c;

            if (cameraPivot != null)
            {
                float camY = standCamY - (standHeight - h) * 0.65f - (isSliding ? slideCameraDip : 0f);
                var lp = cameraPivot.localPosition;
                lp.y = Mathf.Lerp(lp.y, camY, crouchLerp * Time.deltaTime);
                cameraPivot.localPosition = lp;
            }
        }
    }
}
