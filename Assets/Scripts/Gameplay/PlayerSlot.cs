using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gameplay
{
    public class PlayerSlot : MonoBehaviour
    { // this class serves as a databank for the player information. basically since
      // I have a fixed number of players (2-4), I can get away with hard setting their properties here
      // and then only pointing them to this once they join
      // it handles stuff like camera perspective and client side UI, aswell as stuff I could probably
      // make more efficient (like the transforms as Vector coordinate placeholders), but I personally
      // like the increased usability I get from having scene objects I can easily move instead of numbers
        public Transform[] cardSlots;
        public List<Image> influences;
        public Transform moneySlot;
        public Camera perspective;
        public TextMeshProUGUI namePlate;
        public TextMeshProUGUI coinCounter;

        public Participant player;

        private void Awake()
        {
            perspective = GetComponent<Camera>();
            perspective.enabled = false;
            foreach (var image in influences)
            {
                image.gameObject.SetActive(false);
            }
            namePlate.gameObject.SetActive(false);
            coinCounter.gameObject.SetActive(false);
        }
    }
}
