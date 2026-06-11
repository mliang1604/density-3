using UnityEngine;
using FableFPS.Core;

namespace FableFPS.Player
{
    /// <summary>
    /// First-person character controller with Destiny-style movement:
    /// sprint, double jump, crouch, crouch-slide, and weapon-driven recoil.
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
        public float slideFriction = 4.5f;   // m/s² the slide bleeds off
        public float slideSteer = 2.5f;      // rad/s the slide can be steered
        public float slideCameraDip = 0.15f; // extra camera drop while sliding
        public float slideRoll = 7f;         // camera roll (deg) while sliding

        [Header("Jump")]
        public float jumpHeight = 1.3f;
        public float doubleJumpHeight = 2.4f;
        public float gravity = -24f;

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
        private bool hasDoubleJump = true;
        private Vector2 recoil; // x = pitch kick (up), y = yaw kick
        private float recoilRecovery = 8f;

        private bool isCrouching;
        private bool isSliding;
        private float slideTimer;
        private Vector3 slideDir;
        private float crouchBlend;   // 0 = standing, 1 = fully crouched
        private float cameraRoll;
        private float standHeight;
        private float standCenterY;
        private float standCamY;

        public bool IsCrouching => isCrouching;
        public bool IsSliding => isSliding;

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

        public void ResetLook(float newYaw)
        {
            yaw = newYaw;
            pitch = 0f;
            recoil = Vector2.zero;
            isSliding = false;
            isCrouching = false;
            cameraRoll = 0f;
            velocity = Vector3.zero;
        }

        private void LockCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            CursorLockedAt = Time.time;
        }

        private void HandleLook()
        {
            yaw += Input.GetAxis("Mouse X") * mouseSensitivity * SensitivityScale;
            pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity * SensitivityScale;
            pitch = Mathf.Clamp(pitch, -maxPitch, maxPitch);

            recoil = Vector2.Lerp(recoil, Vector2.zero, recoilRecovery * Time.deltaTime);

            transform.rotation = Quaternion.Euler(0f, yaw + recoil.y, 0f);
            if (cameraPivot != null)
                cameraPivot.localRotation = Quaternion.Euler(
                    Mathf.Clamp(pitch - recoil.x, -maxPitch, maxPitch), 0f, cameraRoll);
        }

        private void HandleMovement()
        {
            bool grounded = controller.isGrounded;
            if (grounded)
            {
                hasDoubleJump = true;
                if (velocity.y < 0f) velocity.y = -2f;
            }

            Vector2 input = Vector2.ClampMagnitude(
                new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")), 1f);

            Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
            IsSprinting = Input.GetKey(KeyCode.LeftShift) && input.y > 0.1f && !isCrouching && !isSliding;

            // Crouch pressed while sprinting fast on the ground -> slide.
            if (!isSliding && grounded && Input.GetKeyDown(crouchKey) && horizontal.magnitude >= minSlideSpeed)
                StartSlide(horizontal);

            if (isSliding)
            {
                UpdateSlide(ref horizontal, input, grounded);
            }
            else
            {
                // Hold to crouch; stay crouched if there's no headroom to stand.
                isCrouching = Input.GetKey(crouchKey) || (isCrouching && !CanStand());

                float targetSpeed = (isCrouching ? crouchSpeed
                    : IsSprinting ? sprintSpeed : walkSpeed) * SpeedScale;
                Vector3 wishDir = transform.right * input.x + transform.forward * input.y;
                float control = grounded ? 1f : airControl;
                horizontal = Vector3.MoveTowards(horizontal, wishDir * targetSpeed,
                    acceleration * control * Time.deltaTime);
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
                else if (hasDoubleJump)
                {
                    hasDoubleJump = false;
                    velocity.y = Mathf.Sqrt(doubleJumpHeight * -2f * gravity);
                    SFX.Play2D(SFX.DoubleJumpClip, 0.5f, Random.Range(0.95f, 1.05f));
                }
            }

            velocity.y += gravity * Time.deltaTime;
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

        private void UpdateSlide(ref Vector3 horizontal, Vector2 input, bool grounded)
        {
            slideTimer -= Time.deltaTime;
            float speed = Mathf.Max(0f, horizontal.magnitude - slideFriction * Time.deltaTime);

            // A little steering toward the stick.
            Vector3 wishDir = transform.right * input.x + transform.forward * input.y;
            if (wishDir.sqrMagnitude > 0.01f)
                slideDir = Vector3.RotateTowards(slideDir, wishDir.normalized,
                    slideSteer * Time.deltaTime, 0f).normalized;

            horizontal = slideDir * speed;

            if (slideTimer <= 0f || speed <= crouchSpeed || !grounded)
                EndSlide();
        }

        private void EndSlide()
        {
            isSliding = false;
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

        /// <summary>Lerps controller height, camera height, and camera roll toward
        /// the current crouch/slide state.</summary>
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

            cameraRoll = Mathf.Lerp(cameraRoll, isSliding ? slideRoll : 0f, 8f * Time.deltaTime);
        }
    }
}
