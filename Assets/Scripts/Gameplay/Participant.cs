using System;
using Photon.Pun;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Random = UnityEngine.Random;

namespace Gameplay
{ 
    public class Participant : MonoBehaviourPunCallbacks, IPunObservable
    {// this admittedly bloated class is what handles all the player specific stuff, I would usually have called it
     // player, but that name is already covered by PUN, so I would rather avoid having to deal with namespaces...
     // what it does is handle all player internal stuff, basically taking the input from the UIman and computing it.
     // it also serves as the RPC caller for all things player, a functionality I would have liked to split up, but
     // that would have required another PhotonView component (as they are mandatory for that), and those look expensive...)
        public int coins = 0;
        public List<Character> influences = new List<Character>();
        public PhotonView pv;
        private PlayerSlot mySlot;
        public int playerNumber = -1;
        private List<GameObject> myCoins = new List<GameObject>();
        public List<GameObject> myInfluenceCards = new List<GameObject>();

        private UIMan uiMan;

        [SerializeField] private GameObject coinObject = null;
        [SerializeField] private GameObject cardObject = null;

        public List<bool> responses;
        private bool awaitingResponse;
        private ResponseQuery currentQuery;
        private int contestingPlayer;
        private int targetedPlayer;
        public bool ambassadorUsed;

        public enum ResponseQuery
        { // this is the terribly long enum that keeps track of a queries prupose
            Tax,
            Assassinate,
            Steal,
            Exchange,
            Aid,
            BlockStealC,
            BlockStealA,
            BlockAssassinate,
            BlockAid,
            BlockAssassinateC,
            BlockSteal
        }

        public enum Character
        { // this enum keeps track of the character cards, it should maybe have been on the GameMaster, I dont know...
            Duke,
            Assassin,
            Captain,
            Ambassador,
            Contessa
        }


        private void Start()
        {
            pv = GetComponent<PhotonView>();
            uiMan = FindObjectOfType<UIMan>();

            if (pv.IsMine)
            { // this part only happen when it is your local instance, so it does a bit more admin stuff
                influences.Add(GameMaster.Instance.DrawInfluence());
                influences.Add(GameMaster.Instance.DrawInfluence());
                FindSlot(true);
                GameSetup();
                uiMan.SetActivePlayer(this);
            }
            else
            { // this only happens to ensure some public variables are assigned
                FindSlot(false);
                uiMan.SetOpponent(pv.Controller.NickName, playerNumber);
            }
        }


        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        { // this actually keeps the coin value constantly up to date. I could have done different implementations,
          // but since this is basically the only thing except for the deck-state that feels very sensitive, and caching all money
          // changes seems excessive, this felt like the best one to me.
            if (stream.IsWriting)
            {
                stream.SendNext(coins);
            }
            else
            {
                coins = (int) stream.ReceiveNext();
            }
        }

        private void Update()
        {
            if (mySlot.coinCounter.text != "" + coins)
                mySlot.coinCounter.text = "" + coins;

            if (awaitingResponse && responses.Count == GameMaster.Instance.remainingPlayers - 1)
            { // this is where the fun happens (and where like 50% of the lines of this class lie...)
                ResolveQuery();
            }

        }

        private void ResolveQuery()
        { // this function handles most of the query logic, which is most of the game logic. I needed a flowchart to make this...
            if (!responses.Contains(false))
            { // this is the situation where no one contests our query, so all the "positive" outcomes happen
                switch (currentQuery)
                { // it then gets handled by query type, most of them creating new queries. This is because players can technically
                  // react to almost all actions of other players which is nightmarish to keep track of, so I made this rather clunky system for it
                    case ResponseQuery.Aid:
                        uiMan.ForeignAid();
                        break;
                    case ResponseQuery.Assassinate:
                        pv.RPC("SendQuery", RpcTarget.All, (int) ResponseQuery.BlockAssassinate,
                            targetedPlayer, playerNumber);
                        break;
                    case ResponseQuery.Exchange:
                        uiMan.InitiateExchange();
                        break;
                    case ResponseQuery.Steal:
                        pv.RPC("SendQuery", RpcTarget.All, (int) ResponseQuery.BlockSteal,
                            targetedPlayer, playerNumber);
                        break;
                    case ResponseQuery.Tax:
                        uiMan.Tax();
                        break;
                    case ResponseQuery.BlockAid:
                        pv.RPC("EndTurnRPC", RpcTarget.Others, targetedPlayer);
                        break;
                    case ResponseQuery.BlockAssassinate:
                        uiMan.Assassinate(targetedPlayer);
                        break;
                    case ResponseQuery.BlockStealA:
                        pv.RPC("EndTurnRPC", RpcTarget.Others, targetedPlayer);
                        break;
                    case ResponseQuery.BlockStealC:
                        pv.RPC("EndTurnRPC", RpcTarget.Others, targetedPlayer);
                        break;
                    case ResponseQuery.BlockAssassinateC:
                        pv.RPC("EndTurnRPC", RpcTarget.Others, targetedPlayer);
                        break;
                    case ResponseQuery.BlockSteal:
                        StealCoins(2, targetedPlayer);
                        break;
                }
            }
            else
            { // if the other half looked bad, this one is worse, since the role comparing logic happens here.
              // I should have probably spent 10m trying to make a method out of that comparison, because it looks very repetetive,
              // but I instead went for the bad copy+paste approach...
              // another problem I see here is that I have multiple instance of two separate RPC calls right after one another.
              // this is obviously inefficient, but I had a hard time fitting it better without making it worse
                switch (currentQuery)
                {
                    case ResponseQuery.Aid:
                        pv.RPC("CounterQuery", RpcTarget.All, (int) ResponseQuery.BlockAid,
                            -1, contestingPlayer);
                        break;
                    case ResponseQuery.Assassinate:
                        if (influences.Contains(Character.Assassin))
                        {
                            pv.RPC("LoseInfluenceRPC", RpcTarget.Others, contestingPlayer);
                            LoseInfluence(Character.Assassin);
                            pv.RPC("SendQuery", RpcTarget.All, (int) ResponseQuery.BlockAssassinate,
                                targetedPlayer, playerNumber);
                        }
                        else
                        {
                            uiMan.InitiateLoseInfluence();
                            EndTurn();
                            uiMan.EndTurn();
                        }

                        break;
                    case ResponseQuery.Exchange:
                        if (influences.Contains(Character.Ambassador))
                        {
                            pv.RPC("LoseInfluenceRPC", RpcTarget.Others, contestingPlayer);
                            LoseInfluence(Character.Ambassador);
                            uiMan.InitiateExchange();
                        }
                        else
                        {
                            uiMan.InitiateLoseInfluence();
                            EndTurn();
                            uiMan.EndTurn();
                        }

                        break;
                    case ResponseQuery.Steal:
                        if (influences.Contains(Character.Captain))
                        {
                            pv.RPC("LoseInfluenceRPC", RpcTarget.Others, contestingPlayer);
                            LoseInfluence(Character.Captain);
                            pv.RPC("SendQuery", RpcTarget.All, (int) ResponseQuery.BlockSteal,
                                targetedPlayer, playerNumber);
                        }
                        else
                        {
                            uiMan.InitiateLoseInfluence();
                            EndTurn();
                            uiMan.EndTurn();
                        }

                        break;
                    case ResponseQuery.Tax:
                        if (influences.Contains(Character.Duke))
                        {
                            pv.RPC("LoseInfluenceRPC", RpcTarget.Others, contestingPlayer);
                            LoseInfluence(Character.Duke);
                            uiMan.Tax();
                        }
                        else
                        {
                            uiMan.InitiateLoseInfluence();
                            EndTurn();
                            uiMan.EndTurn();
                        }

                        break;
                    case ResponseQuery.BlockAid:
                        if (influences.Contains(Character.Duke))
                        {
                            pv.RPC("LoseInfluenceRPC", RpcTarget.Others, contestingPlayer);
                            LoseInfluence(Character.Duke);
                            pv.RPC("EndTurnRPC", RpcTarget.Others, targetedPlayer);
                        }
                        else
                        {
                            uiMan.InitiateLoseInfluence();
                            pv.RPC("ResumeAction", RpcTarget.Others, 0);
                        }

                        break;
                    case ResponseQuery.BlockAssassinate:
                        pv.RPC("CounterQuery", RpcTarget.All, (int) ResponseQuery.BlockAssassinateC,
                            playerNumber, contestingPlayer);
                        
                        break;
                    case ResponseQuery.BlockStealA:
                        if (influences.Contains(Character.Ambassador))
                        {
                            pv.RPC("LoseInfluenceRPC", RpcTarget.Others, contestingPlayer);
                            LoseInfluence(Character.Ambassador);
                            pv.RPC("EndTurnRPC", RpcTarget.Others, targetedPlayer);
                        }
                        else
                        {
                            uiMan.InitiateLoseInfluence();
                            pv.RPC("ResumeAction", RpcTarget.Others, 1);
                        }
                        ambassadorUsed = false;

                        break;
                    case ResponseQuery.BlockStealC:
                        if (influences.Contains(Character.Captain))
                        {
                            pv.RPC("LoseInfluenceRPC", RpcTarget.Others, contestingPlayer);
                            LoseInfluence(Character.Captain);
                            pv.RPC("EndTurnRPC", RpcTarget.Others, targetedPlayer);
                        }
                        else
                        {
                            uiMan.InitiateLoseInfluence();
                            pv.RPC("ResumeAction", RpcTarget.Others, 1);
                        }

                        break;
                    case ResponseQuery.BlockAssassinateC:
                        if (influences.Contains(Character.Contessa))
                        {
                            pv.RPC("LoseInfluenceRPC", RpcTarget.Others, contestingPlayer);
                            LoseInfluence(Character.Contessa);
                            pv.RPC("EndTurnRPC", RpcTarget.Others, targetedPlayer);
                            pv.RPC("RemoveCoinsRPC", RpcTarget.Others, 3, targetedPlayer);
                        }
                        else
                        {
                            uiMan.InitiateLoseInfluence();
                            pv.RPC("ResumeAction", RpcTarget.Others, 2);
                        }

                        break;
                    case ResponseQuery.BlockSteal:
                        if(!ambassadorUsed)
                            pv.RPC("CounterQuery", RpcTarget.All, (int) ResponseQuery.BlockStealC,
                                playerNumber, contestingPlayer);
                        else
                            pv.RPC("CounterQuery", RpcTarget.All, (int) ResponseQuery.BlockStealA,
                                playerNumber, contestingPlayer);
                        break;
                }
            }
            if(responses.Count == GameMaster.Instance.remainingPlayers -1)
                awaitingResponse = false; // if there are no new queries we need to reset this value
        }

        private void FindSlot(bool claimSlot)
        { // this is what gets us our player seat at the start
            for (int i = 0; i < PhotonNetwork.CurrentRoom.PlayerCount; i++)
            {
                if (PhotonNetwork.PlayerList[i].Equals(pv.Controller))
                    playerNumber = i;
            }

            PhotonView slot = GameMaster.Instance.playerSlots[playerNumber];
            if (claimSlot)
            {
                slot.TransferOwnership(pv.Controller);
            }
            mySlot = slot.GetComponent<PlayerSlot>();
            mySlot.player = this;
            GameMaster.Instance.remainingPlayers++;
        }

        public void ReceiveTurn()
        { // this both makes sure we can get turns, aswell as that we get skipped if we are dead
            if (pv.IsMine)
            {
                if(influences.Count > 0)
                    uiMan.StartTurn(this);
                else
                {
                    EndTurn();
                }
            }
        }

        public void LoseInfluence(Character revealedCharacter)
        { // this gets called when we had to reveal our character due to a challenge, it is then redrawn from the deck
            for (int i = 0; i < 2; i++)
            {
                if (influences[i] == revealedCharacter)
                {
                    influences[i] = GameMaster.Instance.DrawInfluence();
                    GameMaster.Instance.ReturnInfluence(revealedCharacter);
                    UpdateInfluence(i);
                    break;
                }
            }
        }

        public void AddCoins(int amount)
        { // simple function that just adds to an int. the visual coins are a nice and easy touch, they are instantiated
          // over the network, but do not track their positions over it, since it doesnt matter, and is cheaper like this
            for (int i = 0; i < amount; i++)
            {
                Vector3 offset = new Vector3(Random.Range(-.1f, .1f), (i + coins) * .5f, Random.Range(-.1f, .1f));
                GameObject inst = PhotonNetwork.Instantiate(coinObject.name, mySlot.moneySlot.position + offset,
                    mySlot.moneySlot.rotation, 0);
                myCoins.Add(inst);
            }

            coins += amount;
        }

        public void RemoveCoins(int amount)
        { // opposite of above
            for (int i = 0; i < amount; i++)
            {
                PhotonNetwork.Destroy(myCoins[myCoins.Count - 1]);
                myCoins.RemoveAt(myCoins.Count - 1);
            }

            coins -= amount;
        }

        public void StealCoins(int amount, int targetIndex)
        { // this gets used for the steal actions resolution, it also ensures that we dont steal coins that arent there
            Participant targetParticipant =
                GameMaster.Instance.playerSlots[targetIndex].GetComponent<PlayerSlot>().player;
            if (targetParticipant.coins >= amount)
            {
                pv.RPC("RemoveCoinsRPC", RpcTarget.Others, amount, targetIndex);
                AddCoins(amount);
            }
            else if (targetParticipant.coins > 0)
            {
                pv.RPC("RemoveCoinsRPC", RpcTarget.Others, targetParticipant.coins, targetIndex);
                AddCoins(targetParticipant.coins);
            }
            
            EndTurn();
            uiMan.EndTurn();
        }

        public void EndTurn()
        { // just the separated function caller for the RPC even, as I already wrote, I wanted to keep the separate
            int nextPlayer;
            if (GameMaster.Instance.activePlayerNumber + 1 == PhotonNetwork.CurrentRoom.PlayerCount)
                nextPlayer = 0;
            else
                nextPlayer = GameMaster.Instance.activePlayerNumber + 1;
            pv.RPC("PassTurn", RpcTarget.All, nextPlayer);
        }

        public void UpdateInfluence(int index)
        { // could have made this an update, but it happens so rarely, it seemed like a waste
            mySlot.influences[index].sprite = GameMaster.Instance.influenceImages[(int) influences[index]];
        }

        public void RemoveInfluence(int index)
        { // this gets called by the selection buttons of the lose influence bit of the UI manager
            PhotonNetwork.Destroy(myInfluenceCards[index]);
            influences.RemoveAt(index);
            Destroy(mySlot.influences[index].gameObject);
            mySlot.influences.RemoveAt(index);
        }

        private void GameSetup()
        { // this gets called at the start to ensure the basic gamestate (2 cards, 2 coins, etc...)
            myInfluenceCards.Add(PhotonNetwork.Instantiate(cardObject.name, mySlot.cardSlots[0].position, mySlot.cardSlots[0].rotation, 0));
            myInfluenceCards.Add(PhotonNetwork.Instantiate(cardObject.name, mySlot.cardSlots[1].position, mySlot.cardSlots[1].rotation, 0));
            AddCoins(2);

            mySlot.perspective.enabled = true;
            mySlot.influences[0].gameObject.SetActive(true);
            mySlot.influences[1].gameObject.SetActive(true);
            UpdateInfluence(0);
            UpdateInfluence(1);

            pv.RPC("SetUpSeat", RpcTarget.AllBuffered, playerNumber);
            mySlot.coinCounter.transform.Rotate(Vector3.up, 180);
            mySlot.namePlate.gameObject.SetActive(false);
        }

        [PunRPC]
        public void SetUpSeat(int playerSlotIndex)
        { // the RPC part of the setup where we enable the coin counter and nameplate for each player
            PlayerSlot seat = GameMaster.Instance.playerSlots[playerSlotIndex].GetComponent<PlayerSlot>();
            seat.coinCounter.gameObject.SetActive(true);
            seat.namePlate.gameObject.SetActive(true);
            seat.namePlate.text = seat.GetComponent<PhotonView>().Controller.NickName;
        }

        [PunRPC]
        public void PassTurn(int nextPlayer)
        { // the RPC part of passing the turn
            GameMaster.Instance.PassTurn(nextPlayer);
        }

        [PunRPC]
        public void RemoveCoinsRPC(int amount, int targetIndex)
        { // the RPC part of removing coins, this is used to make sure we keep track of coins properly and only the owner ever removes them
            Participant target = GameMaster.Instance.playerSlots[targetIndex].GetComponent<PlayerSlot>().player;
            if (target.pv.IsMine)
            {
                target.RemoveCoins(amount);
            }
        }

        [PunRPC]
        public void EndTurnRPC(int targetPlayer)
        { // the forceful ending of the turn done by someone else through RPC (gets used in the query logic)
            Participant target = GameMaster.Instance.playerSlots[targetPlayer].GetComponent<PlayerSlot>().player;
            if (target.pv.IsMine)
            {
                EndTurn();
                uiMan.EndTurn();
            }
        }

        [PunRPC]
        public void SendQuery(int query, int targetPlayer, int askingPlayer)
        { // the start of any query is this. if you sent it, you prepare to receive answers, if you didnt, you show the query
            if(!pv.IsMine)
                uiMan.ShowQuery((ResponseQuery) query, targetPlayer, askingPlayer);
            else
            {
                awaitingResponse = true;
                responses = new List<bool>();
                targetedPlayer = targetPlayer;
                currentQuery = (ResponseQuery)query;
            }
        }

        [PunRPC]
        public void AnswerQuery(bool answer, int answeringPlayer, int askingPlayer, bool usesAmbassador)
        { // the structure for the answers to queries, if you are the one who asked before, you then process the results
            Participant target = GameMaster.Instance.playerSlots[askingPlayer].GetComponent<PlayerSlot>().player;
            if (target.pv.IsMine)
            {
                if (!answer && !target.responses.Contains(false))
                    target.contestingPlayer = answeringPlayer;
                target.responses.Add(answer);
                if (usesAmbassador)
                    target.ambassadorUsed = true;
            }
        }

        [PunRPC]
        public void CounterQuery(int query, int targetPlayer, int askingPlayer)
        { // this is a helper RPC to make sure we keep the queries straight. since only the owner of a given multiplayer object may
          // send queries the usual way, this forces them to do it for us when we see them as the one contesting our plan
            Participant target = GameMaster.Instance.playerSlots[askingPlayer].GetComponent<PlayerSlot>().player;
            if (target.pv.IsMine)
            {
                target.pv.RPC("SendQuery", RpcTarget.All, query, targetPlayer, askingPlayer);
            }
        }

        [PunRPC]
        public void LoseInfluenceRPC(int targetIndex)
        { // forcing others to lose influence, used a lot in the resolution of the queries
            Participant target = GameMaster.Instance.playerSlots[targetIndex].GetComponent<PlayerSlot>().player;
            if (target.pv.IsMine)
            {
                target.uiMan.InitiateLoseInfluence();
            }
        }

        [PunRPC]
        public void ResumeAction(int actionIndex)
        { // this is to ensure that the counter query only interrupts not stops the original query
            Participant target = GameMaster.Instance.playerSlots[GameMaster.Instance.activePlayerNumber].GetComponent<PlayerSlot>().player;
            if (target.pv.IsMine)
            {
                switch (actionIndex)
                {
                    case 0: // resume foreign aid
                        uiMan.ForeignAid();
                        break;
                    case 1: // resume stealing
                        target.StealCoins(2, target.targetedPlayer);
                        break;
                    case 2: // resume assassination
                        uiMan.Assassinate(target.targetedPlayer);
                        break;
                }
            }
        }

        [PunRPC]
        public void PlayerEliminated()
        { // game over for one player
            GameMaster.Instance.remainingPlayers--;
        }
    }
}
