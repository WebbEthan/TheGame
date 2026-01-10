using System;
using UnityEngine;
using UnityEngine.InputSystem;





public class LocalPlayerControler : MonoBehaviour
{
    public PhysicsController physicsController;
    public AttributeSet Attributes;
    private void Start()
    {
        Rigidbody2D physicsInteractor = GetComponent<Rigidbody2D>();
        physicsController = new PhysicsController(physicsInteractor, Attributes);
    }

    private int lastXDir = 0;
    private int lastYDir = 0;
    private bool jumpKeyWasHeld;
    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        // Horizontal
        if (keyboard.aKey.wasPressedThisFrame) lastXDir = -1;
        if (keyboard.dKey.wasPressedThisFrame) lastXDir = 1;

        // Reset if keys are released
        if (!keyboard.aKey.isPressed && lastXDir == -1) lastXDir = 0;
        if (!keyboard.dKey.isPressed && lastXDir == 1) lastXDir = 0;

        // If the "last" key was released but the other is still held, switch to the other, prevents input bias
        if (lastXDir == 0)
        {
            if (keyboard.aKey.isPressed) lastXDir = -1;
            else if (keyboard.dKey.isPressed) lastXDir = 1;
        }

        // Verticle
        if (keyboard.wKey.wasPressedThisFrame) lastYDir = 1;
        if (keyboard.sKey.wasPressedThisFrame) lastYDir = -1;

        if (!keyboard.wKey.isPressed && lastYDir == 1) lastYDir = 0;
        if (!keyboard.sKey.isPressed && lastYDir == -1) lastYDir = 0;

        if (lastYDir == 0)
        {
            if (keyboard.wKey.isPressed) lastYDir = 1;
            else if (keyboard.sKey.isPressed) lastYDir = -1;
        }

        // A new jump can only start if 'W' is pressed this frame 
        // OR if we just transitioned from not-holding to holding.
        bool jumpKeyPressedThisFrame = keyboard.wKey.wasPressedThisFrame;

        // Safety check: if they were holding it last frame and are still holding it, 
        // it's not a "new" jump.
        bool isNewJumpIntent = jumpKeyPressedThisFrame && !jumpKeyWasHeld;

        Vector2 inputVector = new Vector2(lastXDir, lastYDir);
        physicsController.MoveVector = inputVector;

        // Pass the gap check into PhysicsUpdate
        physicsController.PhysicsUpdate(isNewJumpIntent);

        // Store current state for next frame
        jumpKeyWasHeld = keyboard.wKey.isPressed;
    }
}
