using System;
using System.Collections.Generic;
using System.Xml.Linq;
using UnityEngine;

// Holds a basic set of info for moving object
public interface Attributes
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

    public float CoyoteTime { get; }
}
[Serializable]
public struct AttributeTypes
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
    public float CoyoteTime;
}
[Serializable]
public class AttributeSet : Attributes 
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

    public float CoyoteTime => BaseAttributes.CoyoteTime + ModifiedAttributes.CoyoteTime;

    // Base Attributes
    public AttributeTypes BaseAttributes = new AttributeTypes() {
        Speed = 10,
        JumpStrength = 10,
        ExtraJumpCount = 1,
        NaturalDrag = 1
    };
    // Modified Attributes
    public AttributeTypes ModifiedAttributes;
}

// Instanceable systems to move physics object programaticlly without losing collision or physics attributes

#if DEBUGGING
[Serializable]
#endif
public class PhysicsController
{
    private LayerMask physicsObjects = 1 | (1 << 6);
    private Rigidbody2D physicsInteractor;
    private Collider2D collider;

    public Attributes attributes;
    public PhysicsController(Rigidbody2D rigidbody2D, Attributes attributeSet)
    {
        physicsInteractor = rigidbody2D;
        collider = rigidbody2D.gameObject.GetComponent<Collider2D>();
        attributes = attributeSet;
    }
    // System to store the collistion info

#if DEBUGGING
    public bool Touching;
    public bool TouchingPhysicalObject { get { Touching = collider.IsTouchingLayers(physicsObjects); return Touching; } }
#else
    public bool TouchingPhysicalObject => collider.IsTouchingLayers(physicsObjects);
#endif
    private const float groundCheckBuffer = 0.05f;
    // Returns -1 if touching wall to the left and 1 if touching wall to the right
    int TouchedWalls;
    public int GetWalls
    {
        get
        {
            if (!TouchingPhysicalObject) return 0;

            // Box height is slightly shorter to avoid hitting floors/ceilings
            Vector2 boxSize = new Vector2(0.01f, collider.bounds.size.y * 0.9f);
            float distanceToSideEdge = collider.bounds.extents.x;

            // Check Left
            RaycastHit2D leftHit = Physics2D.BoxCast(physicsInteractor.position, boxSize, 0f, Vector2.left, distanceToSideEdge + groundCheckBuffer, physicsObjects);
            if (leftHit.collider != null && Math.Abs(leftHit.distance - distanceToSideEdge) <= groundCheckBuffer) return -1;

            // Check Right
            RaycastHit2D rightHit = Physics2D.BoxCast(physicsInteractor.position, boxSize, 0f, Vector2.right, distanceToSideEdge + groundCheckBuffer, physicsObjects);
            if (rightHit.collider != null && Math.Abs(rightHit.distance - distanceToSideEdge) <= groundCheckBuffer) return 1;

            return 0;
        }
    }
    public bool OnGround;
    public bool IsGrounded
    {
        get
        {
            if (!TouchingPhysicalObject) return false;

            // Calculate the size for our box
            // We make the width slightly smaller (90%) so it doesn't hit walls
            Vector2 boxSize = new Vector2(collider.bounds.size.x * 0.9f, 0.01f);
            float distanceToBottomEdge = collider.bounds.extents.y;

            // Perform BoxCast downwards
            RaycastHit2D hit = Physics2D.BoxCast(
                physicsInteractor.position,
                boxSize,
                0f,
                Vector2.down,
                distanceToBottomEdge + groundCheckBuffer,
                physicsObjects
            );

            if (hit.collider != null)
            {
                float distanceToGround = hit.distance;
                return Math.Abs(distanceToGround - distanceToBottomEdge) <= groundCheckBuffer;
            }
            return false;
        }
    }


    public Vector2 MoveVector;
    public Vector2 Inertia = Vector2.zero;
    private int RemainingJumps = 0;
    public float RemainingJumpTime = 0;
    private float CoyoteTimer = 0;
    public void PhysicsUpdate(bool AllowJumpStart)
    {
        OnGround = IsGrounded;
        TouchedWalls = GetWalls;

        // Update Coyote Timer
        if (OnGround) CoyoteTimer = attributes.CoyoteTime;
        else CoyoteTimer -= Time.deltaTime;

        // Stop moving on x axis if not touching anything and no input
        if (TouchedWalls != 0 || MoveVector.x == 0) Inertia.x = 0;

        Vector2 attributedVector = Vector2.zero;

        // CENTRAL JUMP INPUT HANDLING
        if (AllowJumpStart && MoveVector.y > 0)
        {
            // PRIORITIZE WALL JUMP
            // Check if we are airborne and touching a wall we are pushing into
            if (!OnGround && TouchedWalls != 0 && TouchedWalls * MoveVector.x == 1)
            {
                // Set jump time
                RemainingJumpTime = attributes.MaxJumpTime;

                // Kick away from wall
                Inertia.x = -MoveVector.x * attributes.ClimbJumpOut;

                Inertia.y = 0;
                CoyoteTimer = 0;
            }
            // REGULAR JUMP (Ground or Coyote)
            else if (OnGround || CoyoteTimer > 0 || RemainingJumps > 0)
            {
                if (!OnGround && CoyoteTimer <= 0) RemainingJumps--;

                RemainingJumpTime = attributes.MaxJumpTime;
                CoyoteTimer = 0;
                Inertia.y = 0; // Clear any downward velocity
            }
        }

        // MOVEMENT & STATES
        if (OnGround)
        {
            RemainingJumps = attributes.ExtraJumpCount;
            Inertia.y = 0;

            if (TouchedWalls * MoveVector.x != 1)
                attributedVector.x = MoveVector.x * attributes.Speed;
        }
        else
        {
            if (TouchedWalls * MoveVector.x == 1 && RemainingJumpTime <= 0) // CLIMBING
            {
                Inertia.y = attributes.ClimbingFallSpeed;
            }
            else // FREE FALL
            {
                attributedVector.x = MoveVector.x * attributes.Speed;

                if (RemainingJumpTime <= 0)
                    attributedVector.y = physicsInteractor.linearVelocity.y + (attributes.Gravity * Time.deltaTime * 10);
            }
        }

        // VARIABLE JUMP HEIGHT (Holding the button)
        if (RemainingJumpTime > 0 && MoveVector.y > 0)
        {
            RemainingJumpTime -= Time.deltaTime;
            attributedVector.y = attributes.JumpStrength;
        }
        else
        {
            RemainingJumpTime = 0;
        }

        // 5. DRAG & FINAL VELOCITY
        float dragCoeff = OnGround ? 0.001f : 0.1f;
        float decay = Mathf.Pow(dragCoeff * attributes.NaturalDrag, Time.deltaTime);

        Inertia.x *= decay;
        if (Inertia.y > 0) Inertia.y *= decay;
        if (Inertia.magnitude < 0.1f) Inertia = Vector2.zero;

        Vector2 finalVelocity = Inertia + attributedVector;

        float maxHorizontal = Mathf.Max(attributes.Speed, Mathf.Abs(Inertia.x));
        finalVelocity.x = Mathf.Clamp(finalVelocity.x, -maxHorizontal, maxHorizontal);

        physicsInteractor.linearVelocity = finalVelocity;
    }
}
