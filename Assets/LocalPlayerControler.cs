using System;
using UnityEngine;





public class LocalPlayerControler : MonoBehaviour
{
    private PhysicsController physicsController;
    public AttributeSet Attributes;
    private void Start()
    {
        Rigidbody2D physicsInteractor = GetComponent<Rigidbody2D>();
        physicsController = new PhysicsController(physicsInteractor, Attributes);
    }

    private void Update()
    {
        // builds a vector based on the inputs
        Vector2 inputVector = Vector2.zero;
        if (Input.GetKey("w"))
        {
            inputVector.y = 1;
        }
        else if (Input.GetKey("s"))
        {
            inputVector.y = -1;
        }
        if (Input.GetKey("a"))
        {
            inputVector.x = -1;
        }
        else if (Input.GetKey("d"))
        {
            inputVector.x = 1;
        }
        // Sets the move move force vector
        physicsController.MoveVector = inputVector;
        physicsController.PhysicsUpdate();
    }
}
