using System;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gameplay
{
    public class UIMan : MonoBehaviour
    { // this moloch of a class is just supposed to handle UI interactions. For this reason it is
      // keeping a reference to most of the UI elements that ever get changed, and some methods get
      // placed here that could be part of the Participant, but used to be just UI bound for testing,
      // and the bloat in the Participant made me keep them here ultimately.
        [SerializeField] private GameObject playerActions = null;
        [SerializeField] private GameObject playerSelections = null;
        [SerializeField] private GameObject influenceSelections = null;
        [SerializeField] private GameObject influenceSelections2 = null;
        [SerializeField] private TextMeshProUGUI hideToggleText = null;
        [SerializeField] private GameObject hideToggleButton = null;
        [SerializeField] private Button assassinateOption = null;
        [SerializeField] private Button coupOption = null;
        [SerializeField] private GameObject[] selectionButtons = new GameObject[3];
        [SerializeField] private Toggle[] influenceButtons = new Toggle[4];
        [SerializeField] private Image[] influenceIcons = new Image[4];
        [SerializeField] private Image[] influenceIcons2 = new Image[2];
        [SerializeField] private Image[] queryImages = new Image[3];
        [SerializeField] private TextMeshProUGUI[] queryTexts = new TextMeshProUGUI[3];
        [SerializeField] private GameObject querySelection = null;
        [SerializeField] private GameObject loseScreen = null;
        private List<int> opponentRefIndex = new List<int>();
        
        private Participant.Character[] exchangeOptions = new Participant.Character[4];
        private List<Toggle> currentExchangeSelection;
        private Participant localPlayer;
        private bool isSelecting = false;
        private SelectionReason currentReason;
        
        private int askingPlayerIndex;
        
        private enum SelectionReason
        { // this enum helps me keep the reasons for selections straight, 3/4 of them use a similar interface
            Coup, Assassinate, Steal, Exchange
        }

        private void Start()
        {
            foreach (var button in selectionButtons)
            {
                button.SetActive(false);
            }
            
            playerActions.SetActive(false);
            playerSelections.SetActive(false);
            influenceSelections.SetActive(false);
            influenceSelections2.SetActive(false);
            hideToggleButton.SetActive(false);
            querySelection.SetActive(false);
            loseScreen.SetActive(false);
        }

        public void SetActivePlayer(Participant player)
        { // this gets called by the participant at the very start, it is just a setter, I never
          // remember how to do the get/set-only variable declaration...
            localPlayer = player;
        }

        public void SetOpponent(String name, int playerNumber)
        { // this gets called by all non local Participants, and then helps the UI
          // keep track both of their names aswell as their player number (which is important for reference)
          // the two seperate arrays that map onto each other is really ugly, but I had no better idea at the time
          // they are in essence a dictionary with button objects as the key which...yeah...thats why this implementation
            Debug.Log("Opponent Set as " + name);
            playerSelections.SetActive(true);
            for (int i = 0; i < 3; i++)
            {
                if (!selectionButtons[i].activeSelf)
                {
                    selectionButtons[i].SetActive(true);
                    selectionButtons[i].GetComponentInChildren<TextMeshProUGUI>().text = name;
                    break;
                }
            }
            playerSelections.SetActive(false);
            opponentRefIndex.Add(playerNumber);
        }

        public void EndTurn()
        { // just a time saving method that hides away UI. effectively the game has no real turns,
          // but since players cant do anything without this UI, this makes it seem like it has
            playerActions.SetActive(false);
            playerSelections.SetActive(false);
            influenceSelections.SetActive(false);
            hideToggleButton.SetActive(false);
        }

        public void StartTurn(Participant player)
        { // see above for turn passing, this also disabled the buttons for actions you dont have enough
          // money for. The missing feature was left out due to time vs reward logic that got me to finish this
          // before the weekend. it is also only a concern with multiple players that are scared (the situation where the rule comes up)
            playerActions.SetActive(true);
            hideToggleButton.SetActive(true);
            if (player.coins < 3)
            {
                assassinateOption.interactable = false;
                coupOption.interactable = false;
            }
            else if(player.coins < 7)
            {
                assassinateOption.interactable = true;
                coupOption.interactable = false;
            }
            else if(player.coins > 9)
            {
                // TODO: only enable coup
            }
            else
            {
                assassinateOption.interactable = true;
                coupOption.interactable = true;
            }
        }

        public void HideToggle()
        { // this gets called by the view board/view options button to do exactly that
            if (hideToggleText.text == "View Board")
            {
                hideToggleText.text = "View Options";
                playerActions.SetActive(false);
                playerSelections.SetActive(false);
                influenceSelections.SetActive(false);
            }
            else
            {
                hideToggleText.text = "View Board";
                if(!isSelecting)
                    playerActions.SetActive(true);
                else
                {
                    if(currentReason != SelectionReason.Exchange)
                        playerSelections.SetActive(true);
                    else
                        influenceSelections.SetActive(true);
                }
            }
        }

        public void SendQuery(int queryIndex)
        { // this gets called by some of the action buttons to do their confirmation query (the ones with no targets to be exact)
            localPlayer.pv.RPC("SendQuery", RpcTarget.All, queryIndex, -1, localPlayer.playerNumber);
            EndTurn();
        }
        
        public void ShowQuery(Participant.ResponseQuery query, int targetPlayer, int askingPlayer)
        { // this is the nightmare that gets the query questions to show up correctly.
          // the implementation through switch really does little to make it more efficient, but it does help
          // with organizing it a bit. essentially depending on the nature of the query, the response texts and images get changed
            Debug.Log("Showing query");
            querySelection.SetActive(true);
            string askingPlayerName = PhotonNetwork.PlayerList[askingPlayer].NickName;
            string targetPlayerName = "";
            if(targetPlayer != -1)
                targetPlayerName = PhotonNetwork.PlayerList[targetPlayer].NickName;
            askingPlayerIndex = askingPlayer;
            switch (query)
            {
                case Participant.ResponseQuery.Aid:
                    queryImages[0].gameObject.SetActive(false);
                    queryImages[1].gameObject.SetActive(false);
                    queryImages[2].gameObject.SetActive(true);
                    queryImages[2].sprite = GameMaster.Instance.influenceImages[(int) Participant.Character.Duke];
                    queryTexts[0].text = askingPlayerName +
                                         " wants to take foreign aid, do you want to claim to have a Duke to block it?";
                    queryTexts[1].text = "No";
                    queryTexts[2].text = "Yes";
                    break;
                case Participant.ResponseQuery.Assassinate:
                    queryImages[0].gameObject.SetActive(true);
                    queryImages[0].sprite = GameMaster.Instance.influenceImages[(int) Participant.Character.Assassin];
                    queryImages[1].gameObject.SetActive(false);
                    queryImages[2].gameObject.SetActive(false);
                    queryTexts[0].text = askingPlayerName +
                                         " claims they have an Assassin and wants to assassinate " + targetPlayerName + ", do you believe them?";
                    queryTexts[1].text = "Yes";
                    queryTexts[2].text = "No";
                    break;
                case Participant.ResponseQuery.Exchange:
                    queryImages[0].gameObject.SetActive(true);
                    queryImages[0].sprite = GameMaster.Instance.influenceImages[(int) Participant.Character.Ambassador];
                    queryImages[1].gameObject.SetActive(false);
                    queryImages[2].gameObject.SetActive(false);
                    queryTexts[0].text = askingPlayerName +
                                         " claims to have an Ambassador, and wants to exchange Influences with the deck, do you believe them?";
                    queryTexts[1].text = "Yes";
                    queryTexts[2].text = "No";
                    break;
                case Participant.ResponseQuery.Steal:
                    queryImages[0].gameObject.SetActive(true);
                    queryImages[0].sprite = GameMaster.Instance.influenceImages[(int) Participant.Character.Captain];
                    queryImages[1].gameObject.SetActive(false);
                    queryImages[2].gameObject.SetActive(false);
                    queryTexts[0].text = askingPlayerName +
                                         " claims to have a Captain, and wants to steal from " + targetPlayerName + ", do you believe them?";
                    queryTexts[1].text = "Yes";
                    queryTexts[2].text = "No";
                    break;
                case Participant.ResponseQuery.Tax:
                    queryImages[0].gameObject.SetActive(true);
                    queryImages[0].sprite = GameMaster.Instance.influenceImages[(int) Participant.Character.Duke];
                    queryImages[1].gameObject.SetActive(false);
                    queryImages[2].gameObject.SetActive(false);
                    queryTexts[0].text = askingPlayerName +
                                         " claims to have a Duke, and wants to collect a tax, do you believe them?";
                    queryTexts[1].text = "Yes";
                    queryTexts[2].text = "No";
                    break;
                case Participant.ResponseQuery.BlockAid:
                    queryImages[0].gameObject.SetActive(true);
                    queryImages[0].sprite = GameMaster.Instance.influenceImages[(int) Participant.Character.Duke];
                    queryImages[1].gameObject.SetActive(false);
                    queryImages[2].gameObject.SetActive(false);
                    queryTexts[0].text = askingPlayerName +
                                         " claims to have a Duke, and wants to block the foreign aid for " + targetPlayerName + ", do you believe them?";
                    queryTexts[1].text = "Yes";
                    queryTexts[2].text = "No";
                    break;
                case Participant.ResponseQuery.BlockAssassinate:
                    queryImages[0].gameObject.SetActive(false);
                    queryImages[1].gameObject.SetActive(false);
                    queryImages[2].gameObject.SetActive(true);
                    queryImages[2].sprite = GameMaster.Instance.influenceImages[(int) Participant.Character.Contessa];
                    queryTexts[0].text = askingPlayerName +
                                         " wants to take assassinate " + targetPlayerName + ", do you want to claim to have a Contessa to block it?";
                    queryTexts[1].text = "No";
                    queryTexts[2].text = "Yes";
                    break;
                case Participant.ResponseQuery.BlockAssassinateC:
                    queryImages[0].gameObject.SetActive(true);
                    queryImages[0].sprite = GameMaster.Instance.influenceImages[(int) Participant.Character.Contessa];
                    queryImages[1].gameObject.SetActive(false);
                    queryImages[2].gameObject.SetActive(false);
                    queryTexts[0].text = askingPlayerName +
                                         " claims to have a Contessa, and wants to block the assassination from " + targetPlayerName + ", do you believe them?";
                    queryTexts[1].text = "yes";
                    queryTexts[2].text = "No";
                    break;
                case Participant.ResponseQuery.BlockSteal:
                    queryImages[0].gameObject.SetActive(false);
                    queryImages[1].gameObject.SetActive(true);
                    queryImages[1].sprite = GameMaster.Instance.influenceImages[(int) Participant.Character.Ambassador];
                    queryImages[2].gameObject.SetActive(true);
                    queryImages[2].sprite = GameMaster.Instance.influenceImages[(int) Participant.Character.Captain];
                    queryTexts[0].text = askingPlayerName +
                                         " wants to steal from " + targetPlayerName + ", do you want to claim to have an Ambassador or Captain to block it?";
                    queryTexts[1].text = "No";
                    queryTexts[2].text = "Yes";
                    break;
                case Participant.ResponseQuery.BlockStealA:
                    queryImages[0].gameObject.SetActive(true);
                    queryImages[0].sprite = GameMaster.Instance.influenceImages[(int) Participant.Character.Ambassador];
                    queryImages[1].gameObject.SetActive(false);
                    queryImages[2].gameObject.SetActive(false);
                    queryTexts[0].text = askingPlayerName +
                                         " claims to have an Ambassador, and wants to block the stealing of " + targetPlayerName + ", do you believe them?";
                    queryTexts[1].text = "Yes";
                    queryTexts[2].text = "No";
                    break;
                case Participant.ResponseQuery.BlockStealC:
                    queryImages[0].gameObject.SetActive(true);
                    queryImages[0].sprite = GameMaster.Instance.influenceImages[(int) Participant.Character.Captain];
                    queryImages[1].gameObject.SetActive(false);
                    queryImages[2].gameObject.SetActive(false);
                    queryTexts[0].text = askingPlayerName +
                                         " claims to have a Captain, and wants to block the stealing of " + targetPlayerName + ", do you believe them?";
                    queryTexts[1].text = "Yes";
                    queryTexts[2].text = "No";
                    break;
            }
        }

        public void AnswerQuery(bool answer)
        { // this method gets called by the two buttons on the queries to pass along the RPC answer
            localPlayer.pv.RPC("AnswerQuery", RpcTarget.Others, answer, localPlayer.playerNumber, askingPlayerIndex, false);
            querySelection.SetActive(false);
        }
        
        public void AnswerQueryAmbassador(bool answer)
        { // this method gets called by the stupid ambassador button, the nature of which still upsets me.
          // basically, all queries are boolean answers, except for the steal-defense, as that has two seperate false cases.
          // this extra button and the seperate script and boolean handles it, but is very ugly and inefficient.
          // probably an int or byte would have been smarter use instead of the first boolean, but I did not see this issue until too late
            localPlayer.pv.RPC("AnswerQuery", RpcTarget.Others, answer, localPlayer.playerNumber, askingPlayerIndex, true);
            querySelection.SetActive(false);
        }

        public void Income()
        { // simple method that gets called by the income button
            localPlayer.AddCoins(1);
            localPlayer.EndTurn();
            EndTurn();
        }

        public void InitiateCoup()
        { // also simple method that gets called by the coup button and opens the selection interface
            playerActions.SetActive(false);
            playerSelections.SetActive(true);
            isSelecting = true;
            currentReason = SelectionReason.Coup;
        }
        
        public void InitiateAssassinate()
        { // also simple method that gets called by the assassinate button and opens the selection interface
            playerActions.SetActive(false);
            playerSelections.SetActive(true);
            isSelecting = true;
            currentReason = SelectionReason.Assassinate;
        }

        public void InitiateSteal()
        { // also simple method that gets called by the steal button and opens the selection interface
            playerActions.SetActive(false);
            playerSelections.SetActive(true);
            isSelecting = true;
            currentReason = SelectionReason.Steal;
        }
        public void InitiateExchange()
        { // a bit more complex, this method gets called by the exchange button, and it then sets up the
          // behind the scenes stuff for drawing extra cards and assigning that Ui. the ugly if statements
          // are there to cover the eventuality of having only one influence when taking the action
          // I would have liked to just use a null in the array, but apparently you cant do that with an array of enums (?)
          // so yeah...thats how we got this
            playerActions.SetActive(false);
            influenceSelections.SetActive(true);
            isSelecting = true;
            currentReason = SelectionReason.Exchange;
            exchangeOptions[0] = localPlayer.influences[0];
            if(localPlayer.influences.Count > 1)
                exchangeOptions[1] = localPlayer.influences[1];
            exchangeOptions[2] = GameMaster.Instance.DrawInfluence();
            exchangeOptions[3] = GameMaster.Instance.DrawInfluence();
            for (int i = 0; i < 4; i++)
            {
                if(localPlayer.influences.Count > 1 && i == 1)
                    influenceButtons[i].gameObject.SetActive(false);
                else
                    influenceIcons[i].sprite = GameMaster.Instance.influenceImages[(int) exchangeOptions[i]];
            }
            currentExchangeSelection = new List<Toggle>();
            currentExchangeSelection.Add(influenceButtons[0]);
            if(localPlayer.influences.Count > 1)
                currentExchangeSelection.Add(influenceButtons[1]);
        }

        public void ExchangeToggleGroupToggle(Toggle target)
        { // this method ensures the functionality of a toggle group but with more than 1 concurrently active
            if (target.isOn)
            {
                currentExchangeSelection.Add(target);
                if (currentExchangeSelection.Count > localPlayer.influences.Count)
                {
                    currentExchangeSelection[0].isOn = false;
                    currentExchangeSelection.RemoveAt(0);
                }
            }
            else
            {
                currentExchangeSelection.Remove(target);
            }
        }

        public void Exchange()
        { // this is the method that gets called by the confirm button on the exchange selection screen.
          // it essentially transfers the relevant values to the Participant and then cleans up a bit
            int j = 0;
            for (int i = 0; i < 4; i++)
            {
                if (currentExchangeSelection.Contains(influenceButtons[i]))
                {
                    localPlayer.influences[j] = exchangeOptions[i];
                    localPlayer.UpdateInfluence(j);
                    j++;
                }
                else
                {
                    GameMaster.Instance.ReturnInfluence(exchangeOptions[i]);
                }
            }

            isSelecting = false;
            localPlayer.EndTurn();
            EndTurn();
        }

        public void Selection(Button targetButton)
        { // this is the method behind the selection screen for player targeting. Not too difficult tbh,
          // the main issue here is that the only thing constant about the button assignment is in the reference sheet
          // which gets assigned at runtime. so rather than figure out how to change the variable in function calls on buttons, I went
          // with my button objects instead.
            int target = -1;
            for (int i = 0; i < 3; i++)
            {
                if (selectionButtons[i].gameObject == targetButton.gameObject)
                {
                    target = opponentRefIndex[i];
                }
            }
            switch (currentReason)
            { 
                case SelectionReason.Coup:
                    localPlayer.pv.RPC("LoseInfluenceRPC", RpcTarget.Others, target);
                    localPlayer.RemoveCoins(7);
                    localPlayer.EndTurn();
                    EndTurn();
                    break;
                case SelectionReason.Assassinate:
                    localPlayer.pv.RPC("SendQuery", RpcTarget.All, (int) Participant.ResponseQuery.Assassinate,
                        target, localPlayer.playerNumber);
                    EndTurn();
                    break;
                case SelectionReason.Steal:
                    localPlayer.pv.RPC("SendQuery", RpcTarget.All, (int) Participant.ResponseQuery.Steal,
                        target, localPlayer.playerNumber);
                    EndTurn();
                    break;
            }
            isSelecting = false;
            
        }

        public void ForeignAid()
        { // this one and the following 2 are functionalities of the Participant that just started here and never got moved
            localPlayer.AddCoins(2);
            localPlayer.EndTurn();
            EndTurn();
        }

        public void Tax()
        {
            localPlayer.AddCoins(3);
            localPlayer.EndTurn();
            EndTurn();
        }

        public void Assassinate(int targetIndex)
        {
            localPlayer.pv.RPC("LoseInfluenceRPC", RpcTarget.Others, targetIndex);
            localPlayer.RemoveCoins(3);
            localPlayer.EndTurn();
            EndTurn();
        }

        public void InitiateLoseInfluence()
        { // this is called whenever a player would lose influence, since they get to chose which one, they need new UI
            if (localPlayer.influences.Count > 1)
            {
                influenceSelections2.SetActive(true);
                for (int i = 0; i < 2; i++)
                {
                    influenceIcons2[i].sprite = GameMaster.Instance.influenceImages[(int) localPlayer.influences[i]];
                }
            }
            else
            {
                LoseInfluence(0);
                localPlayer.pv.RPC("PlayerEliminated", RpcTarget.All);
                loseScreen.SetActive(true);
            }
        }

        public void LoseInfluence(int influenceIndex)
        {
            localPlayer.RemoveInfluence(influenceIndex);
            influenceSelections2.SetActive(false);
        }
    }
}
