using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Gameplay
{
    public class GameMaster : MonoBehaviourPunCallbacks
    { // this is the class that handles game wide information for players.
      // that includes references such as the "what icon is what character" stuff, and also references to
      // all the seats that get used to align the easier to send integer indexes with scene objects
        public List<Participant.Character> deck;
        public int activePlayerNumber;
        public static GameMaster Instance;
        public PhotonView[] playerSlots;
        public Sprite[] influenceImages;

        private PhotonView pv;
        [SerializeField] private GameObject participantObject = null;
        [SerializeField] private GameObject winScreen = null;
        public int remainingPlayers = 0;


        private void Start()
        {
            winScreen.SetActive(false);
            pv = GetComponent<PhotonView>();
            CreateDeck();
            playerSlots = GameObject.FindGameObjectWithTag("PlayerSlots").GetComponentsInChildren<PhotonView>();
            // this is just to save effort (and I think make it more efficient) to regularly look up this object
            Instance = this;
            // this actually instantiates the "player objects" that will hold all the logic and data, I do this
            // here because it needs to be there immediately, and it also gives me control of the execution order
            // of the Starts (as the newly instantiated ones can now reference the GameMaster already)
            PhotonNetwork.Instantiate(participantObject.name, Vector3.zero, Quaternion.identity, 0);
            StartCoroutine(StartGame());
        }

        private void Update()
        { // all I am doing here is to check winconditions
            if (remainingPlayers == 1)
            {
                foreach (var seat in playerSlots)
                {
                    Participant player = seat.GetComponent<PlayerSlot>().player;
                    if(player != null) // the timer is added so players dont win during asynchronous loading
                        if (player.influences.Count > 0 && seat.IsMine && Time.timeSinceLevelLoad > 20f)
                        {
                            winScreen.SetActive(true);
                        }
                }
            }
        }

        IEnumerator StartGame()
        { // the delay is added here to compensate for instantiation time. since I am using the network create
          // functions, I can not guarantee that the Starts all line up well, and I need to reference a player instance here
            yield return new WaitForSeconds(1);
            PassTurn(0);
        }

        public Participant.Character DrawInfluence()
        { // this gets used by the player objects to access the central deck. the fact that each role only exists
          // three times is relevant, so I need a central deck. I use the buffered RPC call to further ensure integrity of the deck
            int randomPick = Random.Range(0, deck.Count);
            Participant.Character returnValue = deck[randomPick];
            pv.RPC("RemoveFromDeck", RpcTarget.AllBuffered,((int)returnValue));
            return returnValue;
        }

        public void ReturnInfluence(Participant.Character card)
        { // some things require cards be returned to the deck, again buffered to ensure integrity
            pv.RPC("AddToDeck", RpcTarget.AllBuffered, (int)card);
        }

        [PunRPC]
        public void RemoveFromDeck(int indexOfCharacter)
        { // this is the actual method that removes from the deck when drawn, using the PunRPC tag, it is
          // executed on all matching instances of this object over the network
            Participant.Character target = (Participant.Character) indexOfCharacter;
            for (int i = 0; i < deck.Count; i++)
            {
                if (deck[i] == target)
                {
                    deck.RemoveAt(i);
                    break;
                }
            }
        }

        [PunRPC]
        public void AddToDeck(int indexOfCharacter)
        { // the method that adds to the deck (again the PunRPC version)
          // I intentionally kept the RPC parts seperate from other functionality even though it is less
          // efficient writing, so I can better differentiate them for myself while I get used to them
          // also it helps me to make sure I only transfer what I need and nothing more
            Participant.Character target = (Participant.Character) indexOfCharacter;
            deck.Add(target);
        }

        private void CreateDeck()
        { // I use this method to essentially declare my deck list. I wanted to keep this bulk out of the
          // variable definition area, so it doesnt get cluttered, and I use an array first instead of a list
          // because I can manually type it out which is easier to double check (afaik you can not do that with lists)
            Participant.Character[] deckContents = new[]
            {
                Participant.Character.Duke, Participant.Character.Duke, Participant.Character.Duke,
                Participant.Character.Assassin, Participant.Character.Assassin, Participant.Character.Assassin,
                Participant.Character.Captain, Participant.Character.Captain, Participant.Character.Captain,
                Participant.Character.Ambassador, Participant.Character.Ambassador, Participant.Character.Ambassador,
                Participant.Character.Contessa, Participant.Character.Contessa, Participant.Character.Contessa
            };
            deck = new List<Participant.Character>(deckContents);
        }

        public void PassTurn(int nextPlayer)
        { // this method gets used by the player instances to pass the turn between one another.
          // this being so important made me put it here, in retrospect it can probably be moved to their methods
          // but honestly, that class already has too much functionality, so I would rather leave it here
            activePlayerNumber = nextPlayer;
            playerSlots[nextPlayer].GetComponent<PlayerSlot>().player.ReceiveTurn();
        }
    }
}
