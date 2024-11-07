using System;
using System.Collections;
using System.Collections.Generic;
using Oculus.Platform;
using UnityEngine;

public class GemCollision : MonoBehaviour
{
    public float respawnTime = 2.0f; // Time to wait before respawning
   // private bool isRespawning = false; // Flag to check if the gem is respawning

    private GameObject NPC;
    private GameObject Gems;
    
    private AudioSource AudioSource;

    private void Start()
    {
        AudioSource = GetComponent<AudioSource>();
        if (AudioSource == null)
            AudioSource = gameObject.AddComponent<AudioSource>();
        
        AudioSource.clip = Resources.Load<AudioClip>("Audio/PlinkSound");
    }

    private void OnTriggerEnter(Collider collider)
    {
        Debug.Log("Collision detected with: " + collider.gameObject.name);
        if (collider.gameObject.name == "MineCart")
        {
            AudioSource.Play();
            
            Debug.Log("Gem collided with MineCart");
            StartCoroutine(RespawnBall());
        }
    }

    IEnumerator RespawnBall()
    {
       // isRespawning = true; // Set the respawning flag
        Debug.Log("Starting RespawnBall Coroutine");
        

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
    }
}