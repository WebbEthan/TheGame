using System;
using System.Collections.Generic;
using UnityEngine;

// Holds a basic set of info for moving object
public interface Attributes
{
    public float Speed { get; }
    public float JumpStrength { get; }
    public int JumpCount { get; }
    public bool CanClimb { get; }
    public float Gravity { get; }
    public float ClimbingFallSpeed { get; }
    public float ClimbJumpOut { get; }

    public float MaxJumpTime { get; }
}
[Serializable]
public struct AttributeTypes
{
    public float Speed;
    public float JumpStrength;
    public int JumpCount;
    public bool CanClimb;
    public float Gravity;
    public float ClimbingFallSpeed;
    public float ClimbJumpOut;
    public float MaxJumpTime;
}
[Serializable]
public class AttributeSet : Attributes 
{
    // Summation Getters
    public float Speed =>  BaseAttributes.Speed + ModifiedAttributes.Speed;
    public float JumpStrength => BaseAttributes.JumpStrength + ModifiedAttributes.JumpStrength;
    public int JumpCount => BaseAttributes.JumpCount + ModifiedAttributes.JumpCount;
    public bool CanClimb => BaseAttributes.CanClimb;
    public float Gravity => BaseAttributes.Gravity + ModifiedAttributes.Gravity;
    public float ClimbingFallSpeed => BaseAttributes.ClimbingFallSpeed + ModifiedAttributes.ClimbingFallSpeed;
    public float ClimbJumpOut => BaseAttributes.ClimbJumpOut + ModifiedAttributes.ClimbJumpOut;

    public float MaxJumpTime => BaseAttributes.MaxJumpTime + ModifiedAttributes.MaxJumpTime;

    // Base Attributes
    public AttributeTypes BaseAttributes = new AttributeTypes() { 
        Speed = 10,
        JumpStrength = 10,
        JumpCount = 1
    };
    // Modified Attributes
    public AttributeTypes ModifiedAttributes;
}

// Instanceable systems to move physics object programaticlly without losing collision or physics attributes
[Serializable]
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
    public bool Touching;
    public bool TouchingPhysicalObject { get { Touching = collider.IsTouchingLayers(physicsObjects); return Touching; } }
    private const float groundCheckBuffer = 0.05f;
    // Returns -1 if touching wall to the left and 1 if touching wall to the right
    int TouchedWalls;
    public int GetWalls
    {
        get
        {
            // Check if we are even touching anything in the physics mask
            if (!TouchingPhysicalObject) return 0;

            // 2. Get the horizontal extent (half-width) of the collider
            float distanceToSideEdge = collider.bounds.extents.x;

            // 3. Check Left (-1)
            RaycastHit2D leftHit = Physics2D.Raycast(physicsInteractor.position, Vector2.left, distanceToSideEdge + groundCheckBuffer, physicsObjects);
            if (leftHit.collider != null && Math.Abs(leftHit.distance - distanceToSideEdge) <= groundCheckBuffer)
            {
                return -1;
            }

            // 4. Check Right (1)
            RaycastHit2D rightHit = Physics2D.Raycast(physicsInteractor.position, Vector2.right, distanceToSideEdge + groundCheckBuffer, physicsObjects);
            if (rightHit.collider != null && Math.Abs(rightHit.distance - distanceToSideEdge) <= groundCheckBuffer)
            {
                return 1;
            }
            return 0;
        }
    }
    public bool OnGround;
    public bool IsGrounded
    {
        get
        {
            // If we aren't touching ANY layers in the mask, we definitely aren't grounded
            if (!TouchingPhysicalObject) return false;

            // Pivot/Center to edge distance
            float distanceToBottomEdge = collider.bounds.extents.y;

            // Perform Raycast
            RaycastHit2D hit = Physics2D.Raycast(
                physicsInteractor.position,
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
    public void PhysicsUpdate(bool AllowJumpStart)
    {
        OnGround = IsGrounded;
        TouchedWalls = GetWalls;

        Vector2 attributedVector = Vector2.zero;
        if (OnGround)
        {
            RemainingJumps = attributes.JumpCount;
            Inertia.y = 0;
            if (MoveVector.y > 0 && AllowJumpStart)
            {
                RemainingJumps--;
                RemainingJumpTime = attributes.MaxJumpTime;
            }
            if (TouchedWalls * MoveVector.x != 1)
            {
                attributedVector.x = MoveVector.x * attributes.Speed;
            }
        }
        else
        {
            // Checks if the player is climbing
            if (TouchedWalls * MoveVector.x == 1)
            {
                Inertia.y = attributes.ClimbingFallSpeed;
                if (MoveVector.y > 0 && AllowJumpStart)
                {
                    RemainingJumpTime = attributes.MaxJumpTime;
                    Inertia.x = MoveVector.x * -attributes.ClimbJumpOut;
                }
            }
            else if (RemainingJumpTime == 0)
            {
                attributedVector.x = MoveVector.x * attributes.Speed;
                Inertia.y += attributes.Gravity;
            }
        }
        if (RemainingJumpTime > 0 && MoveVector.y > 0)
        {
            RemainingJumpTime -= Time.deltaTime;
            attributedVector.y = attributes.JumpStrength;
        }
        else
        {
            RemainingJumpTime = 0;
        }
        // Drag and Inertia ending
        Inertia /= 1.1f;
        if (Inertia.magnitude < 0.1f) Inertia = Vector2.zero;
        Vector3 velocity = Inertia + attributedVector;
        velocity.x = Mathf.Clamp(velocity.x, -attributes.Speed, attributes.Speed);
        if (velocity.x * Inertia.x < 0) Inertia.x = 0;
        // Sets the final Velocity
        physicsInteractor.linearVelocity = Inertia + attributedVector;
    }
}
