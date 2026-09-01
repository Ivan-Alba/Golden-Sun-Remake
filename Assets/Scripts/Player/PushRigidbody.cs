using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PushRigidbody : MonoBehaviour
{

    public float pushPower = 2.0f;
    private float targetMass;
    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Rigidbody otherRigidbody = hit.collider.attachedRigidbody;

        if (otherRigidbody == null || otherRigidbody.isKinematic)
        {
            return;
        }

        if (hit.moveDirection.y < -0.3f)
        {
            return;
        }

        targetMass = otherRigidbody.mass;
        Vector3 direction = new Vector3(hit.moveDirection.x, 0, hit.moveDirection.z);

        otherRigidbody.linearVelocity = direction * pushPower / targetMass;
    }
}
