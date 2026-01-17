using System;
using System.Collections.Generic;
using System.Xml.Linq;
using UnityEngine;

// Holds a basic set of info for moving object
public interface PhysicsAttributes
{
    public float Speed { get; }
    public float JumpStrength { get; }
    public int ExtraJumpCount { get; }
    public bool CanClimb { get; }
    public float Gravity { get; }
    public float ClimbingFallSpeed { get; }
    public float ClimbJumpOut { get; }
    public float MaxJumpTime { get; }
    public float NaturalDrag { get; }
    public float StepHeight { get; }
    public float CoyoteTime { get; }
}
[Serializable]
public struct PhysicsAttributeTypes
{
    public float Speed;
    public float JumpStrength;
    public int ExtraJumpCount;
    public bool CanClimb;
    public float Gravity;
    public float ClimbingFallSpeed;
    public float ClimbJumpOut;
    public float MaxJumpTime;
    public float NaturalDrag;
    public float StepHeight_NOTIMPLEMENTED;
    public float CoyoteTime;
}
[Serializable]
public class PhysicsAttributeSet : PhysicsAttributes 
{
    // Summation Getters
    public float Speed =>  BaseAttributes.Speed + ModifiedAttributes.Speed;
    public float JumpStrength => BaseAttributes.JumpStrength + ModifiedAttributes.JumpStrength;
    public int ExtraJumpCount => BaseAttributes.ExtraJumpCount + ModifiedAttributes.ExtraJumpCount;
    public bool CanClimb => BaseAttributes.CanClimb;
    public float Gravity => BaseAttributes.Gravity + ModifiedAttributes.Gravity;
    public float ClimbingFallSpeed => BaseAttributes.ClimbingFallSpeed + ModifiedAttributes.ClimbingFallSpeed;
    public float ClimbJumpOut => BaseAttributes.ClimbJumpOut + ModifiedAttributes.ClimbJumpOut;
    public float MaxJumpTime => BaseAttributes.MaxJumpTime + ModifiedAttributes.MaxJumpTime;
    public float NaturalDrag => BaseAttributes.NaturalDrag + ModifiedAttributes.NaturalDrag;
    public float StepHeight => BaseAttributes.StepHeight_NOTIMPLEMENTED + ModifiedAttributes.StepHeight_NOTIMPLEMENTED;
    public float CoyoteTime => BaseAttributes.CoyoteTime + ModifiedAttributes.CoyoteTime;

    // Base Attributes
    public PhysicsAttributeTypes BaseAttributes = new PhysicsAttributeTypes() {
        Speed = 10,
        JumpStrength = 10,
        ExtraJumpCount = 1,
        NaturalDrag = 1
    };
    // Modified Attributes
    public PhysicsAttributeTypes ModifiedAttributes;
}

// Instanceable systems to move physics object programaticlly without losing collision or physics attributes

public class PlayerPhysicsController
{
    #region Cached Info
    // Cached collider geometry (world space)
    private Vector2 colliderSize;
    private float halfWidth;
    private float halfHeight;

    // Cached cast boxes
    private Vector2 groundBoxSize;
    private Vector2 wallBoxSize;

    // Cached cast distances
    private float groundCastDistance;
    private float wallCastDistance;
    public void CacheColliderInfo()
    {
        colliderSize = collider.bounds.size;

        halfWidth = colliderSize.x * 0.5f;
        halfHeight = colliderSize.y * 0.5f;

        // Slightly inset to avoid false positives
        groundBoxSize = new Vector2(colliderSize.x * 0.9f, 0.01f);
        wallBoxSize = new Vector2(0.01f, colliderSize.y * 0.9f);

        groundCastDistance = halfHeight + groundCheckBuffer;
        wallCastDistance = halfWidth + groundCheckBuffer;
        ThreadManager.MainLog.LogItem("Cached Player Collider Info");
    }
    #endregion

    private LayerMask physicsObjects = 1 | (1 << 6);
    private Rigidbody2D physicsInteractor;
    private Collider2D collider;

    public PhysicsAttributes attributes;
    public PlayerPhysicsController(Rigidbody2D rigidbody2D, PhysicsAttributes attributeSet)
    {
        physicsInteractor = rigidbody2D;
        collider = rigidbody2D.gameObject.GetComponent<Collider2D>();
        attributes = attributeSet;
    }
    #region ColisionChecks
    private const float groundCheckBuffer = 0.05f;
    // Collision state (updated once per physics step)
    public bool TouchingPhysicalObject { get; private set; }
    public bool OnGround { get; private set; }
    // -1 = left wall, 1 = right wall, 0 = none
    public int TouchedWalls { get; private set; }
    private void UpdateCollisionState()
    {
        TouchingPhysicalObject = collider.IsTouchingLayers(physicsObjects);

        OnGround = false;
        TouchedWalls = 0;

        if (!TouchingPhysicalObject)
            return;

        Vector2 position = physicsInteractor.position;

        // Ground
        RaycastHit2D groundHit = Physics2D.BoxCast(
            position,
            groundBoxSize,
            0f,
            Vector2.down,
            groundCastDistance,
            physicsObjects
        );

        if (groundHit.collider != null &&
            Mathf.Abs(groundHit.distance - halfHeight) <= groundCheckBuffer)
        {
            OnGround = true;
        }

        // Walls
        RaycastHit2D leftHit = Physics2D.BoxCast(
            position,
            wallBoxSize,
            0f,
            Vector2.left,
            wallCastDistance,
            physicsObjects
        );

        if (leftHit.collider != null &&
            Mathf.Abs(leftHit.distance - halfWidth) <= groundCheckBuffer)
        {
            TouchedWalls = -1;
            return;
        }

        RaycastHit2D rightHit = Physics2D.BoxCast(
            position,
            wallBoxSize,
            0f,
            Vector2.right,
            wallCastDistance,
            physicsObjects
        );

        if (rightHit.collider != null &&
            Mathf.Abs(rightHit.distance - halfWidth) <= groundCheckBuffer)
        {
            TouchedWalls = 1;
        }
    }
    #endregion


    public Vector2 MoveVector;
    public Vector2 Inertia = Vector2.zero;
    private int RemainingJumps = 0;
    public float RemainingJumpTime = 0;
    private float CoyoteTimer = 0;
    // Used for system consistancy
    private const float wallJumpImpulseTime = 0.1f;
    private float wallJumpTime = 0f;
    private bool NeedWallGap = false;
    public void PhysicsUpdate(bool AllowJumpStart)
    {
        // Update Collision states
        UpdateCollisionState();
        // Stop moving on x axis if not touching anything and no input ensures wall jumps do not false cancel with collition detection between frames
        if (NeedWallGap)
        {
            if (TouchedWalls == 0)
            {
                if (wallJumpTime > 0f) wallJumpTime -= Time.deltaTime;
                else NeedWallGap = false;
            }
        }
        else if (TouchedWalls != 0 || MoveVector.x == 0) Inertia.x = 0;

        Vector2 attributedVector = Vector2.zero;
        float dragCoeff;

        if (OnGround)
        {
            dragCoeff = 10;
            // Reset jump states
            CoyoteTimer = attributes.CoyoteTime;
            RemainingJumps = attributes.ExtraJumpCount;
            Inertia.y = 0;

            if (TouchedWalls * MoveVector.x != 1)
                attributedVector.x = MoveVector.x * attributes.Speed;
            
            // Jump from ground
            if (AllowJumpStart && MoveVector.y > 0)
            {
                RemainingJumpTime = attributes.MaxJumpTime;
                CoyoteTimer = 0;
                Inertia.y = 0;
            }
        }
        else
        {
            dragCoeff = 2;
            // Handles Climbing and Gravity
            if (TouchedWalls * MoveVector.x == 1) // Wall Collision
            {
                if (RemainingJumpTime <= 0) // Climbing
                {
                    if (AllowJumpStart && MoveVector.y > 0)
                    {
                        RemainingJumpTime = attributes.MaxJumpTime;
                        CoyoteTimer = 0;
                        Inertia.y = 0;
                        // Kick away from wall
                        Inertia.x = -MoveVector.x * attributes.ClimbJumpOut;
                        wallJumpTime = wallJumpImpulseTime;
                        NeedWallGap = true;

                    }
                    else
                    {
                        Inertia.y = attributes.ClimbingFallSpeed;
                    }
                }
            }
            else if (RemainingJumpTime <= 0) // Free Fall
            {
                attributedVector.x = MoveVector.x * attributes.Speed;
                Inertia.y += attributes.Gravity * Time.deltaTime * 10;
            }
            else
            {
                attributedVector.x = MoveVector.x * attributes.Speed;
            }
            // Handles Coyote and Double Jump
            CoyoteTimer -= Time.deltaTime;
            if (AllowJumpStart && MoveVector.y > 0 && !NeedWallGap)
            {
                if (CoyoteTimer > 0)
                {
                    RemainingJumpTime = attributes.MaxJumpTime;
                    CoyoteTimer = 0;

                    Inertia.y = 0;
                }
                else if (RemainingJumps > 0)
                {
                    RemainingJumpTime = attributes.MaxJumpTime;
                    RemainingJumps--;
                    CoyoteTimer = 0;

                    Inertia.y = 0;
                }
            }
        }

        // Variable Jump Height Handling (Holding the button)
        if (RemainingJumpTime > 0 && MoveVector.y > 0)
        {
            RemainingJumpTime -= Time.deltaTime;
            attributedVector.y = attributes.JumpStrength;
        }
        else
        {
            RemainingJumpTime = 0;
        }

        // Apply Exponential Drag V(D*ND)^T => V(e^(D*ND)) => V(1-(D*ND))
        float drag = dragCoeff * attributes.NaturalDrag;
        float decay = Mathf.Max(0f, 1f - (drag * Time.deltaTime));
        Inertia.x *= decay; 

        if (Inertia.magnitude < 0.1f) Inertia = Vector2.zero; 

        Vector2 finalVelocity = Inertia + attributedVector;
        float maxHorizontal = Mathf.Max(attributes.Speed, Mathf.Abs(Inertia.x));
        finalVelocity.x = Mathf.Clamp(finalVelocity.x, -maxHorizontal, maxHorizontal);

        physicsInteractor.linearVelocity = finalVelocity;
    }
}
