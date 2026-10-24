using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
	public class StarterAssetsInputs : MonoBehaviour
	{
		[Header("Character Input Values")]
		public Vector2 move;
		public Vector2 look;
		public bool jump;
		public bool sprint;

		[Header("Combat Input Values")]
		public bool fire;
		public bool aim;
		public bool reload;
		public bool interact;
		public bool drop;
		public bool switchWeapon;
		public bool roll;
		public bool pause;
		public bool crouch;
		public bool melee;
		public bool shove;
		public bool swapShoulder;
		// held while aiming a throw, released to throw
		public bool useItem;
		public bool nextItem;
		public bool flashlight;
		// held; they only lean while aiming, otherwise the same keys keep their usual job
		public bool leanLeft;
		public bool leanRight;
		// the crouch button held down (prone, when available)
		public bool proneHeld;
		// pressed: open/close the tactical map (M) and the backpack (Tab)
		public bool map;
		public bool inventory;

		[Header("Movement Settings")]
		public bool analogMovement;

		[Header("Mouse Cursor Settings")]
		public bool cursorLocked = true;
		public bool cursorInputForLook = true;



#if ENABLE_INPUT_SYSTEM



		public void OnMove(InputValue value)
		{
			MoveInput(value.Get<Vector2>());
		}

		public void OnLook(InputValue value)
		{
			if(cursorInputForLook)
			{
				LookInput(value.Get<Vector2>());
			}
		}

		public void OnJump(InputValue value)
		{
			JumpInput(value.isPressed);
		}

		public void OnSprint(InputValue value)
		{
			SprintInput(value.isPressed);
		}

		public void OnFire(InputValue value)
		{
			fire = value.isPressed;
		}

		public void OnAim(InputValue value)
		{
			aim = value.isPressed;
		}

		// one-shot actions stay true until the combat code consumes them
		public void OnReload(InputValue value)
		{
			if (value.isPressed) reload = true;
		}

		public void OnInteract(InputValue value)
		{
			if (value.isPressed) interact = true;
		}

		public void OnDrop(InputValue value)
		{
			if (value.isPressed) drop = true;
		}

		public void OnSwitchWeapon(InputValue value)
		{
			if (value.isPressed) switchWeapon = true;
		}

		public void OnRoll(InputValue value)
		{
			if (value.isPressed) roll = true;
		}

		public void OnPause(InputValue value)
		{
			if (value.isPressed) pause = true;
		}

		// crouch toggles on each press
		public void OnCrouch(InputValue value)
		{
			if (value.isPressed) crouch = true;
		}

		public void OnMelee(InputValue value)
		{
			if (value.isPressed) melee = true;
		}

		public void OnShove(InputValue value)
		{
			if (value.isPressed) shove = true;
		}

		public void OnSwapShoulder(InputValue value)
		{
			if (value.isPressed) swapShoulder = true;
		}

		public void OnUseItem(InputValue value)
		{
			useItem = value.isPressed;
		}

		public void OnNextItem(InputValue value)
		{
			if (value.isPressed) nextItem = true;
		}

		public void OnFlashlight(InputValue value)
		{
			if (value.isPressed) flashlight = true;
		}

		public void OnMap(InputValue value)
		{
			if (value.isPressed) map = true;
		}

		public void OnInventory(InputValue value)
		{
			if (value.isPressed) inventory = true;
		}

		public void OnLeanLeft(InputValue value)
		{
			leanLeft = value.isPressed;
		}

		public void OnLeanRight(InputValue value)
		{
			leanRight = value.isPressed;
		}

		public void OnProne(InputValue value)
		{
			proneHeld = value.isPressed;
		}
#endif

			private void Awake()
	{
		SetCursorState(cursorLocked);
		Cursor.visible = false;


	}

		public void MoveInput(Vector2 newMoveDirection)
		{
			move = newMoveDirection;
		}

		public void LookInput(Vector2 newLookDirection)
		{
			look = newLookDirection;
		}

		public void JumpInput(bool newJumpState)
		{
			jump = newJumpState;
		}

		public void SprintInput(bool newSprintState)
		{
			sprint = newSprintState;
		}

		private void OnApplicationFocus(bool hasFocus)
		{
			SetCursorState(cursorLocked);
		}

		// menus free the cursor, gameplay locks it
		public void SetCursorLocked(bool locked)
		{
			cursorLocked = locked;
			cursorInputForLook = locked;
			if (!locked) look = Vector2.zero;
			SetCursorState(locked);
		}

		private void SetCursorState(bool newState)
		{
			Cursor.lockState = newState ? CursorLockMode.Locked : CursorLockMode.None;
			Cursor.visible = !newState;


		}
	}

}
