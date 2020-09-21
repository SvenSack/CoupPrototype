using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerList : MonoBehaviourPunCallbacks
{
    [SerializeField] private Button startButton;
    
    private void Start()
    { // this ensures that the local player gets the first instance
        if(PhotonNetwork.IsMasterClient)
            AddNewPlayer(PhotonNetwork.LocalPlayer);
    }

    public override void OnJoinedRoom()
    { // when new players join the room, they get assigned a player aswell. to make things easier, they
      // can not start the game proper, which works here because it gets called for each locally once
        base.OnJoinedRoom();
        if (!PhotonNetwork.IsMasterClient)
        {
            startButton.interactable = false;
            AddNewPlayer(PhotonNetwork.LocalPlayer);
        }
    }

    private void AddNewPlayer(Player player)
    { // just the method call to assign players to their nameplates (see that script for details)
        Debug.Log("Assigning new name for new player");
        transform.GetChild(PhotonNetwork.CurrentRoom.PlayerCount -1).GetComponent<PlayerListing>().Assign(player);
    }

    public void LoadNewRoom()
    { // this gets called by the start button press, and moves everyone to the game scene
        PhotonNetwork.LoadLevel("Game Scene");
    }
}
