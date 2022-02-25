using Mirror;
using Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MyPokemon
{
	[RequireComponent(typeof(CharacterController))]
	[RequireComponent(typeof(PlayerInput))]
	public class MultiplayerThirdPersonController : NetworkBehaviour
	{
		[Header("Player")]
		[Tooltip("Move speed of the character in m/s")]
		public float moveSpeed = 2.0f;
		[Tooltip("Sprint speed of the character in m/s")]
		public float sprintSpeed = 5.335f;
		[Tooltip("How fast the character turns to face movement direction")]
		[Range(0.0f, 0.3f)]
		public float rotationSmoothTime = 0.12f;
		[Tooltip("Acceleration and deceleration")]
		public float speedChangeRate = 10.0f;

		[Space(10)]
		[Tooltip("The height the player can jump")]
		public float jumpHeight = 1.2f;
		[Tooltip("The character uses its own gravity value. The engine default is -9.81f")]
		public float gravity = -15.0f;

		[Space(10)]
		[Tooltip("Time required to pass before being able to jump again. Set to 0f to instantly jump again")]
		public float jumpTimeout = 0.50f;
		[Tooltip("Time required to pass before entering the fall state. Useful for walking down stairs")]
		public float fallTimeout = 0.15f;

		[Header("Player Grounded")]
		[Tooltip("If the character is grounded or not. Not part of the CharacterController built in grounded check")]
		public bool grounded = true;
		[Tooltip("Useful for rough ground")]
		public float groundedOffset = -0.14f;
		[Tooltip("The radius of the grounded check. Should match the radius of the CharacterController")]
		public float groundedRadius = 0.28f;
		[Tooltip("What layers the character uses as ground")]
		public LayerMask groundLayers;

		[Header("Crouch")]
		[Tooltip("The center of the character controller when the player is crouching")]
		public Vector3 crouchCenter = new Vector3(0.0f, 0.54f, 0);
		[Tooltip("The height of the character controller when the player is crouching")]
		public float crouchHeight = 1.05f;
		[Tooltip("The layers to use when we check if the player has the space to stand up")]
		public LayerMask collisionLayers;
		[Tooltip("Intensity of the vignette displayed when crouched")]
		public float vignetteIntensity = 0.25f;

		[Header("Cinemachine")]
		[Tooltip("The follow target set in the Cinemachine Virtual Camera that the camera will follow")]
		public GameObject cinemachineCameraTarget;
		[Tooltip("How far in degrees can you move the camera up")]
		public float topClamp = 70.0f;
		[Tooltip("How far in degrees can you move the camera down")]
		public float bottomClamp = -30.0f;
		[Tooltip("Additional degress to override the camera. Useful for fine tuning camera position when locked")]
		public float cameraAngleOverride = 0.0f;
		[Tooltip("For locking the camera position on all axis")]
		public bool lockCameraPosition = false;

		// cinemachine
		private float cinemachineTargetYaw;
		private float cinemachineTargetPitch;

		// player
		private float speed;
		private float animationSpeed;
		private float animationSpeedX;
		private float animationSpeedZ;
		private float targetRotation = 0.0f;
		private float rotationVelocity;
		private float verticalVelocity;
		private float terminalVelocity = 53.0f;

		// crouch
		private bool crouched = false;
		private Vector3 originalCenter;
		private float originalHeight;
		[SyncVar(hook = nameof(OnCrouchChange))]
		private bool crouchedSync = false;

		// camera
		private GameObject playerCameraRoot;
		private Animator playerCameraRootAnimator;

		// timeout deltatime
		private float jumpTimeoutDelta;
		private float fallTimeoutDelta;

		// animation layers index
		private int animLayerAim;

		// animation IDs
		private int animIDSpeed;
		private int animIDSpeedX;
		private int animIDSpeedZ;
		private int animIDGrounded;
		private int animIDJump;
		private int animIDFreeFall;
		private int animIDMotionSpeed;
		private int animIDCrouch;

		private Animator animator;
		private CharacterController controller;
		private PlayerInputs input;
		private GameObject mainCamera;

		private const float _threshold = 0.01f;

		private bool _hasAnimator;

		private void Awake()
		{
			// get a reference to our main camera
			if (mainCamera == null)
			{
				mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
			}
		}

        private void Start()
        {
			_hasAnimator = TryGetComponent(out animator);
			controller = GetComponent<CharacterController>();
			input = GetComponent<PlayerInputs>();

			AssignAnimationIDs();
			AssignAnimationLayersIndex();

			// reset our timeouts on start
			jumpTimeoutDelta = jumpTimeout;
			fallTimeoutDelta = fallTimeout;

			// save the original height and center of the character controller
			originalCenter = controller.center;
			originalHeight = controller.height;

			// 
			Transform PlayerCameraRootTransform = transform.Find("PlayerCameraRoot");
			if (PlayerCameraRootTransform)
            {
				playerCameraRoot = PlayerCameraRootTransform.gameObject;
				playerCameraRootAnimator = playerCameraRoot.GetComponent<Animator>();
			}
        }

        public override void OnStartLocalPlayer()
		{
			// enable the input system
			GetComponent<PlayerInput>().enabled = true;

			// update camera to follow the local player
			CinemachineVirtualCamera virtualCamera = FindObjectOfType<CinemachineVirtualCamera>();
			if (virtualCamera)
				virtualCamera.Follow = transform.Find("PlayerCameraRoot").transform;
		}

		private void Update()
		{
			if (isLocalPlayer)
				LocalPlayerUpdate();
		}

		private void LocalPlayerUpdate()
		{
			_hasAnimator = TryGetComponent(out animator);

			Crouch();
			JumpAndGravity();
			GroundedCheck();
			Aim();
			Move();
		}

		private void LateUpdate()
		{
			if (isLocalPlayer)
				CameraRotation();
		}

		private void AssignAnimationIDs()
		{
			animIDSpeed = Animator.StringToHash("Speed");
			animIDSpeedX = Animator.StringToHash("SpeedX");
			animIDSpeedZ = Animator.StringToHash("SpeedZ");
			animIDGrounded = Animator.StringToHash("Grounded");
			animIDJump = Animator.StringToHash("Jump");
			animIDFreeFall = Animator.StringToHash("FreeFall");
			animIDMotionSpeed = Animator.StringToHash("MotionSpeed");
			animIDCrouch = Animator.StringToHash("Crouched");
		}

		private void AssignAnimationLayersIndex()
        {
			animLayerAim = animator.GetLayerIndex("Aim");
		}

		private void GroundedCheck()
		{
			// set sphere position, with offset
			Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - groundedOffset, transform.position.z);
			grounded = Physics.CheckSphere(spherePosition, groundedRadius, groundLayers, QueryTriggerInteraction.Ignore);

			// update animator if using character
			if (_hasAnimator)
			{
				animator.SetBool(animIDGrounded, grounded);
			}
		}

		private void CameraRotation()
		{
			// if there is an input and camera position is not fixed
			if (input.look.sqrMagnitude >= _threshold && !lockCameraPosition)
			{
				cinemachineTargetYaw += input.look.x * Time.deltaTime;
				cinemachineTargetPitch += input.look.y * Time.deltaTime;
			}

			// clamp our rotations so our values are limited 360 degrees
			cinemachineTargetYaw = ClampAngle(cinemachineTargetYaw, float.MinValue, float.MaxValue);
			cinemachineTargetPitch = ClampAngle(cinemachineTargetPitch, bottomClamp, topClamp);

			// Cinemachine will follow this target
			cinemachineCameraTarget.transform.rotation = Quaternion.Euler(cinemachineTargetPitch + cameraAngleOverride, cinemachineTargetYaw, 0.0f);
		}

		private void Crouch()
        {
			if (input.crouch)
            {
				input.crouch = false;

				if (!grounded) return;

				// check if the player has the space to stand up
				if (crouched && !CanStandUp()) return;

				// inverse crouch value
				crouched = !crouched;

				// synchronize with server
				CmdChangeCrouched(crouched);

				// add or remove the vignette effect
				if (crouched) PostProcessing.instance.AddVignette(vignetteIntensity);
				else PostProcessing.instance.RemoveVignette();

				// move the camera depending on the crouch value
				if (playerCameraRootAnimator) playerCameraRootAnimator.SetBool("Crouch", crouched);

				// change the character controller collider depending on the crouch value
				controller.height = crouched ? crouchHeight : originalHeight;
				controller.center = crouched ? crouchCenter : originalCenter;

				if (_hasAnimator) animator.SetBool(animIDCrouch, crouched);
            }
		}

		private void Aim()
        {
			float AimLayerWeight = Mathf.Lerp(animator.GetLayerWeight(animLayerAim), input.aim ? 1 : 0, Time.deltaTime * speedChangeRate);
			animator.SetLayerWeight(animLayerAim, AimLayerWeight);
		}

		private void Move()
		{
			// set target speed based on move speed, sprint speed and if sprint is pressed
			float targetSpeed = moveSpeed;

			// check if the player is crouched and if he has the space to stand up
			if (input.sprint && !input.aim && (!crouched || (crouched && CanStandUp())))
			{
				targetSpeed = sprintSpeed;

				if (crouched)
					ResetPlayerState();
			}

			// a simplistic acceleration and deceleration designed to be easy to remove, replace, or iterate upon

			// note: Vector2's == operator uses approximation so is not floating point error prone, and is cheaper than magnitude
			// if there is no input, set the target speed to 0
			if (input.move == Vector2.zero) targetSpeed = 0.0f;

			// a reference to the players current horizontal velocity
			float currentHorizontalSpeed = new Vector3(controller.velocity.x, 0.0f, controller.velocity.z).magnitude;

			float speedOffset = 0.1f;
			float inputMagnitude = input.analogMovement ? input.move.magnitude : 1f;

			// accelerate or decelerate to target speed
			if (currentHorizontalSpeed < targetSpeed - speedOffset || currentHorizontalSpeed > targetSpeed + speedOffset)
			{
				// creates curved result rather than a linear one giving a more organic speed change
				speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude, Time.deltaTime * speedChangeRate);

				// round speed to 3 decimal places
				speed = Mathf.Round(speed * 1000f) / 1000f;
			}
			else
			{
				speed = targetSpeed;
			}
			animationSpeed = Mathf.Lerp(animationSpeed, targetSpeed, Time.deltaTime * speedChangeRate);
			animationSpeedX = Mathf.Lerp(animationSpeedX, animationSpeed * input.move.x, Time.deltaTime * speedChangeRate);
			animationSpeedZ = Mathf.Lerp(animationSpeedZ, animationSpeed * input.move.y, Time.deltaTime * speedChangeRate);

			// normalise input direction
			Vector3 inputDirection = new Vector3(input.move.x, 0.0f, input.move.y).normalized;

			// note: Vector2's != operator uses approximation so is not floating point error prone, and is cheaper than magnitude
			// if there is a move input rotate player when the player is moving
			if (input.move != Vector2.zero)
			{
				targetRotation = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg + mainCamera.transform.eulerAngles.y;
				float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetRotation, ref rotationVelocity, rotationSmoothTime);

				// rotate to face input direction relative to camera position
				if (!input.aim)
					transform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);
			}

			Vector3 targetDirection = Quaternion.Euler(0.0f, targetRotation, 0.0f) * Vector3.forward;

			// move the player
			controller.Move(targetDirection.normalized * (speed * Time.deltaTime) + new Vector3(0.0f, verticalVelocity, 0.0f) * Time.deltaTime);

			// update animator if using character
			if (_hasAnimator)
			{
				animator.SetFloat(animIDSpeed, animationSpeed);
				animator.SetFloat(animIDSpeedX, animationSpeedX);
				animator.SetFloat(animIDSpeedZ, animationSpeedZ);
				animator.SetFloat(animIDMotionSpeed, inputMagnitude);
			}
		}

		private void JumpAndGravity()
		{
			if (grounded)
			{
				// reset the fall timeout timer
				fallTimeoutDelta = fallTimeout;

				// update animator if using character
				if (_hasAnimator)
				{
					animator.SetBool(animIDJump, false);
					animator.SetBool(animIDFreeFall, false);
				}

				// stop our velocity dropping infinitely when grounded
				if (verticalVelocity < 0.0f)
				{
					verticalVelocity = -2f;
				}

				// Jump
				if (input.jump && jumpTimeoutDelta <= 0.0f)
				{
					// the square root of H * -2 * G = how much velocity needed to reach desired height
					verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

					ResetPlayerState();

					// update animator if using character
					if (_hasAnimator)
					{
						animator.SetBool(animIDJump, true);
					}
				}

				// jump timeout
				if (jumpTimeoutDelta >= 0.0f)
				{
					jumpTimeoutDelta -= Time.deltaTime;
				}
			}
			else
			{
				// reset the jump timeout timer
				jumpTimeoutDelta = jumpTimeout;

				// fall timeout
				if (fallTimeoutDelta >= 0.0f)
				{
					fallTimeoutDelta -= Time.deltaTime;
				}
				else
				{
					ResetPlayerState();

					// update animator if using character
					if (_hasAnimator)
					{
						animator.SetBool(animIDFreeFall, true);
					}
				}

				// if we are not grounded, do not jump
				input.jump = false;
			}

			// apply gravity over time if under terminal (multiply by delta time twice to linearly speed up over time)
			if (verticalVelocity < terminalVelocity)
			{
				verticalVelocity += gravity * Time.deltaTime;
			}
		}

		private bool CanStandUp()
        {
			Vector3 p1 = transform.position + Vector3.up * (controller.radius + Physics.defaultContactOffset);
			Vector3 p2 = transform.position + Vector3.up * (originalHeight - controller.radius);
			if (Physics.CheckCapsule(p1, p2, controller.radius - Physics.defaultContactOffset, collisionLayers, QueryTriggerInteraction.Ignore))
				return false;
			return true;
		}

		private void ResetPlayerState()
        {
			// Crouch
			crouched = false;
			CmdChangeCrouched(crouched);
			if (playerCameraRootAnimator) playerCameraRootAnimator.SetBool("Crouch", crouched);
			PostProcessing.instance.RemoveVignette();

			if (_hasAnimator)
			{
				animator.SetBool(animIDCrouch, false);
			}
		}

		private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
		{
			if (lfAngle < -360f) lfAngle += 360f;
			if (lfAngle > 360f) lfAngle -= 360f;
			return Mathf.Clamp(lfAngle, lfMin, lfMax);
		}

        #region OnEvents

        private void OnCrouchChange(bool oldValue, bool newValue)
        {
			crouchedSync = newValue;
			controller.height = crouchedSync ? crouchHeight : originalHeight;
			controller.center = crouchedSync ? crouchCenter : originalCenter;
		}

		#endregion

		#region Commands

		[Command]
		private void CmdChangeCrouched(bool newValue)
		{
			crouchedSync = newValue;
		}

		#endregion

		private void OnDrawGizmosSelected()
		{
			Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
			Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);

			if (grounded) Gizmos.color = transparentGreen;
			else Gizmos.color = transparentRed;
			
			// when selected, draw a gizmo in the position of, and matching radius of, the grounded collider
			Gizmos.DrawSphere(new Vector3(transform.position.x, transform.position.y - groundedOffset, transform.position.z), groundedRadius);
		}
	}
}
