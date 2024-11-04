using System;
using System.Net;
using TMPro;
using UnityEngine;

namespace Lab
{
    public class GameDaemon : MonoBehaviour
    {
        public GameObject GemsOwner;
        public GameObject NPC;
        private TextMeshProUGUI SpeechText;

        private int StageIndex = -1;

        public void Start()
        {
            SpeechText = NPC.transform.GetChild(0).GetChild(0).GetComponent<TextMeshProUGUI>();
            
            GemsOwner.SetActive(false);
            NPC.SetActive(true);
            SpeechText.text = "Please collect the gems";
        }

        public void OnMarkerShown()
        {
            Debug.LogWarning("GAME: Signal Marker Shown!");
            
            GemsOwner.SetActive(false);
            NPC.SetActive(true);
            
            if (StageIndex > 1)
            {
                SpeechText.text = "Game Over!";
            }
            else if (StageIndex > -1)
            {
                SpeechText.text = "Please return the cart!";
            }
        }
        
        public void OnMarkerHidden()
        {
            Debug.LogWarning("GAME: Signal Marker Hidden!");
            
            if (StageIndex > 1)
                return;
            
            GemsOwner.SetActive(true);
            NPC.SetActive(false);

            StageIndex++;
        }
    }
}