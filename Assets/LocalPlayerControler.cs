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

    private int pressCounter = 0;
    private int lastW, lastA, lastS, lastD;

    public Vector2 inputVector;

    private float xDir;
    private float yDir;
    private bool jumpRequested;

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        // 1. Capture Raw States
        bool wHeld = keyboard.wKey.isPressed;
        bool sHeld = keyboard.sKey.isPressed;
        bool aHeld = keyboard.aKey.isPressed;
        bool dHeld = keyboard.dKey.isPressed;

        // 2. Handle Reset Logic
        if (!wHeld && !sHeld && !aHeld && !dHeld)
        {
            pressCounter = 0;
            lastW = lastA = lastS = lastD = 0;
        }

        // 3. Update Press Order (SOCD)
        if (keyboard.wKey.wasPressedThisFrame) lastW = ++pressCounter;
        if (keyboard.sKey.wasPressedThisFrame) lastS = ++pressCounter;
        if (keyboard.aKey.wasPressedThisFrame) lastA = ++pressCounter;
        if (keyboard.dKey.wasPressedThisFrame) lastD = ++pressCounter;

        // 4. Calculate Directions once per frame
        // Horizontal
        if (aHeld && dHeld) xDir = (lastA > lastD) ? -1 : 1;
        else if (aHeld) xDir = -1;
        else if (dHeld) xDir = 1;
        else xDir = 0;

        // Vertical
        if (wHeld && sHeld) yDir = (lastW > lastS) ? 1 : -1;
        else if (wHeld) yDir = 1;
        else if (sHeld) yDir = -1;
        else yDir = 0;

        // 5. Catch Jump (Accumulate if FixedUpdate is slow)
        if (keyboard.wKey.wasPressedThisFrame) jumpRequested = true;
    }

    private void FixedUpdate()
    {
        // Simply use the values calculated in Update
        inputVector = new Vector2(xDir, yDir);

        physicsController.MoveVector = inputVector;
        physicsController.PhysicsUpdate(jumpRequested);

        // Consume the jump trigger so it doesn't fire again until a new press
        jumpRequested = false;
    }
}
