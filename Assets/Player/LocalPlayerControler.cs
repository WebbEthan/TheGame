using System;
using UnityEngine;
using UnityEngine.InputSystem;





public class LocalPlayerControler : MonoBehaviour
{
    public PlayerPhysicsController physicsController;
    public AttributeSet Attributes;
    private void Start()
    {
        Rigidbody2D physicsInteractor = GetComponent<Rigidbody2D>();
        physicsController = new PlayerPhysicsController(physicsInteractor, Attributes);
    }
    private int pressCounter = 0;
    private int lastW, lastA, lastS, lastD;

    public Vector2 inputVector;

    private float xDir;
    private float yDir;

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        // 1. Capture Raw States
        bool wHeld = keyboard.wKey.isPressed;
        bool sHeld = keyboard.sKey.isPressed;
        bool aHeld = keyboard.aKey.isPressed;
        bool dHeld = keyboard.dKey.isPressed;

        // 2. Reset press order if neutral
        if (!wHeld && !sHeld && !aHeld && !dHeld)
        {
            pressCounter = 0;
            lastW = lastA = lastS = lastD = 0;
        }

        // 3. Update press order (SOCD resolution)
        if (keyboard.wKey.wasPressedThisFrame) lastW = ++pressCounter;
        if (keyboard.sKey.wasPressedThisFrame) lastS = ++pressCounter;
        if (keyboard.aKey.wasPressedThisFrame) lastA = ++pressCounter;
        if (keyboard.dKey.wasPressedThisFrame) lastD = ++pressCounter;

        // 4. Resolve axis directions
        if (aHeld && dHeld) xDir = (lastA > lastD) ? -1f : 1f;
        else if (aHeld) xDir = -1f;
        else if (dHeld) xDir = 1f;
        else xDir = 0f;

        if (wHeld && sHeld) yDir = (lastW > lastS) ? 1f : -1f;
        else if (wHeld) yDir = 1f;
        else if (sHeld) yDir = -1f;
        else yDir = 0f;

        inputVector.x = xDir;
        inputVector.y = yDir;

        // 5. Edge-trigger jump
        bool jumpPressed = keyboard.wKey.wasPressedThisFrame;

        // 6. Drive controller directly (dt-safe)
        physicsController.MoveVector = inputVector;
        physicsController.PhysicsUpdate(jumpPressed);
    }

}
