using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GemCollision : MonoBehaviour
{
    public float respawnTime = 2.0f; // Time to wait before respawning
   // private bool isRespawning = false; // Flag to check if the gem is respawning
    private Rigidbody gemRigidbody; // Reference to the gem's Rigidbody

    private void Start()
    {
        gemRigidbody = GetComponent<Rigidbody>(); 
    }

    private void OnCollisionEnter(Collision collision)
    {
        Debug.Log("Collision detected with: " + collision.gameObject.name);
        if (collision.gameObject.CompareTag("MineCart") )
        {
            Debug.Log("Gem collided with MineCart");
            StartCoroutine(RespawnBall());
        }
    }

    IEnumerator RespawnBall()
    {
       // isRespawning = true; // Set the respawning flag
        Debug.Log("Starting RespawnBall Coroutine");

        // Set the Rigidbody to kinematic to stop movement
       // gemRigidbody.isKinematic = true; 

        // Disable the gem's visual representation (but keep the script active)
        GetComponent<Renderer>().enabled = false; 
        // Disable any other components if necessary
        GetComponent<BoxCollider>().enabled = false; 

        // Wait for the specified pause duration
        yield return new WaitForSeconds(respawnTime);

        Debug.Log("Reactivating gem after respawn time");

        // Reactivate the gem's visual representation
        GetComponent<Renderer>().enabled = true; 
        // Re-enable any other components that were disabled
        GetComponent<BoxCollider>().enabled = true; 

        // Set the Rigidbody back to non-kinematic to allow movement again
        //gemRigidbody.isKinematic = false; 

        //isRespawning = false; // Reset the respawning flag
    }
}