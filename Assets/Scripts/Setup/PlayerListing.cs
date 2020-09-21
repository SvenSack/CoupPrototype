using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;

public class PlayerListing : MonoBehaviourPunCallbacks
{
    private TextMeshProUGUI tmpUI;
    public PhotonView pv;

    private void Awake()
    {
        tmpUI = GetComponent<TextMeshProUGUI>();
        pv = gameObject.GetComponent<PhotonView>();
    }

    private void Update()
    {// in a cheap scene like this it is smarter to assign the name locally when values change instead of
     // using extra network commands to synchronize things
        if (pv.Owner != null)
            if (tmpUI.text != pv.Owner.NickName)
            {
                Debug.Log("Adjusted Name of Object " + gameObject.name);
                tmpUI.text = pv.Owner.NickName;
            }
    }

    public void Assign(Player player)
    { // a simple method that gets called by the player list to assign players to the labels.
      // could probably be done better, but I had trouble instantiating UI through the network instantiate function,
      // and this was my workaround
        Debug.Log("Transfer Ownership");
        pv.TransferOwnership(player);
    }
}
