using UnityEngine;
using System.Collections;

public class WeaponDropper : MonoBehaviour
{
    public void StartDrop(Rigidbody rb)
    {
        StartCoroutine(DropSequence(rb));
    }
    
    IEnumerator DropSequence(Rigidbody rb)
    {
        yield return new WaitForSeconds(0.1f);
        
        rb.isKinematic = false;
        rb.useGravity = true;
        
        rb.AddForce(Vector3.up * 2f + transform.forward * 1f, ForceMode.Impulse);
        
        yield return new WaitForSeconds(1f);
        
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
        rb.useGravity = false;
    }
}