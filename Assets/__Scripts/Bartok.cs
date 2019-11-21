using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum TurnPhase
{
    idle,
    pre,
    waiting,
    post,
    gameOver
}

public class Bartok : MonoBehaviour {
    static public Bartok S;
    static public Player CURRENT_PLAYER;
    static bool Waiting_For_Hand_Slot_Selection = false;
    static bool PowerCard_DrawTwo_bool = false;
    static bool PowerCard_Peek_bool = false;
    static bool PowerCard_Swap_bool = false;

    [Header("Set in Inspector")]
    public TextAsset deckXML;
    public TextAsset layoutXML;
    public Vector3 layoutCenter = Vector3.zero;
    public float handFanDegrees = 10f;
    public int numStartingCards = 4;
    public float drawTimeStagger = 0.1f;
    public GameObject[] Position;

    [Header("Set Dynamically")]
    public Deck deck;
    public List<CardBartok> drawPile;
    public List<CardBartok> discardPile;
    public List<CardBartok> hand;
    public List<Player> players;
    public CardBartok targetCard,selectedCard;
    public TurnPhase phase = TurnPhase.idle;

    private BartokLayout layout;
    private Transform layoutAnchor;
    public GameObject selected;

    private void Awake()
    {
        S = this;
    }

    private void Start()
    {
        deck = GetComponent<Deck>(); // Get the Deck
        deck.InitDeck(deckXML.text); // Pass DeckXML to it
        Deck.Shuffle(ref deck.cards); // This shuffles the deck

        layout = GetComponent<BartokLayout>(); // Get the Layout
        layout.ReadLayout(layoutXML.text); // Pass LayoutXML to it

        drawPile = UpgradeCardsList(deck.cards);
        LayoutGame();
    }

    List<CardBartok> UpgradeCardsList(List<Card> lCD)
    {
        List<CardBartok> lCB = new List<CardBartok>();
        foreach(Card tCD in lCD)
        {
            lCB.Add(tCD as CardBartok);
        }
        return (lCB);
    }

    // Position all the cards in the drawPile properly
    public void ArrangeDrawPile()
    {
        CardBartok tCB;

        for (int i=0; i<drawPile.Count; i++)
        {
            tCB = drawPile[i];
            tCB.transform.SetParent(layoutAnchor);
            tCB.transform.localPosition = layout.drawPile.pos;
            // Rotation should start at 0
            tCB.faceUp = false;
            tCB.SetSortingLayerName(layout.drawPile.layerName);
            tCB.SetSortOrder(-i * 4); // Order them front-to-back
            tCB.state = CBState.drawpile;
        }
    }

    //Perform the initial game layout
    void LayoutGame()
    {
        // Create an empty GameObject to serve as the tableau's anchor
        if(layoutAnchor == null)
        {
            GameObject tGO = new GameObject("_LayoutAnchor");
            layoutAnchor = tGO.transform;
            layoutAnchor.transform.position = layoutCenter;
        }

        // Position the drawPile cards
        ArrangeDrawPile();

        // Set up the players
        Player pl;
        players = new List<Player>();
        foreach (SlotDef tSD in layout.slotDefs)
        {
            pl = new Player();
            pl.handSlotDef = tSD;
            players.Add(pl);
            pl.playerNum = tSD.player;
        }
        players[0].type = PlayerType.human; // Make only the 0th player human

        CardBartok tCB;
        // Deal seven cards to each player
        for (int i=0; i<numStartingCards; i++)
        {
            for (int j=0; j<4; j++)
            {
                tCB = DrawFromDrawPile(); // Draw a card
                // Stagger the draw time a bit.
                tCB.timeStart = Time.time + drawTimeStagger * (i * 4 + j);

                players[(j + 1) % 4].AddCard(tCB);
            }
        }

        Invoke("DrawFirstTarget", drawTimeStagger * (numStartingCards * 4 + 4));
    }

    public void DrawFirstTarget()
    {
        // Flip up the first target card from the DrawPile
        CardBartok tCB = MoveToTarget(DrawFromDrawPile());
        // Set the CardBartok to call CBCallback on this Bartok when it is done
        tCB.reportFinishTo = this.gameObject;
    }

    // This callback is used by the last card to be dealt at the beginning
    public void CBCallback(CardBartok cb)
    {
        // You sometimes want to have reporting of method calls like this
        Utils.tr("Bartok:CBCallback()", cb.name);
        StartGame(); // Start the Game
    }

    public void StartGame()
    {
        // Pick the player to the left of the human to go first.
        PassTurn(1);
    }

    public void PassTurn(int num = -1)
    {
        // If no number was passed in, pick the next player
        if (num == -1)
        {
            int ndx = players.IndexOf(CURRENT_PLAYER);
            num = (ndx + 1) % 4;
        }
        int lastPlayerNum = -1;
        if(CURRENT_PLAYER != null)
        {
            lastPlayerNum = CURRENT_PLAYER.playerNum;
            // Check for Game Over and need to reshuffle discards
            if (CheckGameOver())
            {
                return;
            }
        }
        CURRENT_PLAYER = players[num];
        phase = TurnPhase.pre;

        CURRENT_PLAYER.TakeTurn();

        // Report the turn passing
        Utils.tr("Bartok:PassTurn()", "Old: " + lastPlayerNum, "New: " + CURRENT_PLAYER.playerNum);

        if (S == null)
        {
            S = this;
        }
    }

    public bool CheckGameOver()
    {
        // See if we need to reshuffle the discard pile into the draw pile
        if(drawPile.Count == 0)
        {
            List<Card> cards = new List<Card>();
            foreach (CardBartok cb in discardPile)
            {
                cards.Add(cb);
            }
            discardPile.Clear();
            Deck.Shuffle(ref cards);
            drawPile = UpgradeCardsList(cards);
            ArrangeDrawPile();
        }
        
        // Check to see if the current player has won
        if(CURRENT_PLAYER.hand.Length == 0)
        {
            // The player that just played has won!
            phase = TurnPhase.gameOver;
            Invoke("RestartGame", 1);
            return (true);
        }
        return (false);
    }

    public void RestartGame()
    {
        CURRENT_PLAYER = null;
        SceneManager.LoadScene("__Bartok_Scene_0");
    }

    // ValidPlay verifies that the card chosen can be played on the discard pile
    public bool ValidPlay(CardBartok cb)
    {
        //any play is valid as long as the card isn't null
        if (cb == null)
        {
            return (false);
        }
        else
        {
            return (true);
        }
    }

    // This makes a new card the target
    public CardBartok MoveToTarget(CardBartok tCB)
    {
        tCB.timeStart = 0;
        tCB.MoveTo(layout.discardPile.pos + Vector3.back);
        tCB.state = CBState.toTarget;
        tCB.faceUp = true;

        tCB.SetSortingLayerName("10");
        tCB.eventualSortLayer = layout.target.layerName;
        if(targetCard != null)
        {
            MoveToDiscard(targetCard);
        }

        targetCard = tCB;

        return tCB;
    }

    public CardBartok MoveToSelected(CardBartok tCB)
    {
        tCB.state = CBState.selected;
        //selectedCard = tCB;
        tCB.SetSortingLayerName(layout.discardPile.layerName);
        tCB.SetSortOrder(discardPile.Count * 4);
        tCB.transform.localPosition = selected.transform.position;
        tCB.callbackPlayer = CURRENT_PLAYER;
        return tCB;
    }

    public CardBartok MoveToDiscard(CardBartok tCB)
    {
        tCB.state = CBState.discard;
        discardPile.Add(tCB);
        tCB.SetSortingLayerName(layout.discardPile.layerName);
        tCB.SetSortOrder(discardPile.Count * 4);
        tCB.transform.localPosition = layout.discardPile.pos + Vector3.back / 2;

        return tCB;
    }

    // The Draw function will pull a single card from the drawPile and return it
    public CardBartok DrawFromDrawPile()
    {
        CardBartok cd = MoveToSelected(drawPile[0]); // Pull the 0th CardBartok

        if(drawPile.Count == 0)
        {
            // If the drawPile is now empty
            // We need to shuffle the discards into the drawPile
            int ndx;
            while(discardPile.Count > 0)
            {
                // Pull a random card from the discard pile
                ndx = Random.Range(0, discardPile.Count);
                drawPile.Add(discardPile[ndx]);
                discardPile.RemoveAt(ndx);
            }
            ArrangeDrawPile();
            // Show the cards moving to the drawPile
            float t = Time.time;
            foreach (CardBartok tCB in drawPile)
            {
                tCB.transform.localPosition = layout.discardPile.pos;
                tCB.callbackPlayer = null;
                tCB.MoveTo(layout.drawPile.pos);
                tCB.timeStart = t;
                t += 0.02f;
                tCB.state = CBState.toDrawpile;
                tCB.eventualSortLayer = "0";
            }
        }

        drawPile.RemoveAt(0); // Then remove it from List<> drawPile
        return (cd); // And return it
    }

    public void CardClicked(CardBartok tCB)
    {
        if (CURRENT_PLAYER.type != PlayerType.human) return;
        if (phase == TurnPhase.waiting) return;

        switch (tCB.state)
        {
            //DRAW FROM DRAW PILE
            case CBState.drawpile:
                if (!Waiting_For_Hand_Slot_Selection)//Checks if the program is waiting for the hand slot to be chosen
                {
                    //selectedCard.callbackPlayer = CURRENT_PLAYER;
                    selectedCard = DrawFromDrawPile(); //selects the card at the top of the draw pile
                    Waiting_For_Hand_Slot_Selection = true;  //we are now waiting for the hand slot to be selected

                    if (selectedCard.suit == "P")
                    {
                        if (selectedCard.def.rank <= 2)
                        {
                            PowerCard_DrawTwo();
                        }
                        else if (selectedCard.def.rank <= 5)
                        {
                            PowerCard_Peek_bool = true;

                        }
                        else if (selectedCard.def.rank <= 8)
                        {
                            PowerCard_Swap_bool = true;
                        }
                    }
                }
                break;


            //DRAW FROM DISCARD PILE

            case CBState.target:
                if (Waiting_For_Hand_Slot_Selection)//Checks if the program is waiting for the hand slot to be chosen
                {
                    //CURRENT_PLAYER.AddCard(selectedCard);//Adds the selected card to the hand
                    //selectedCard.callbackPlayer = CURRENT_PLAYER;

                    //CURRENT_PLAYER.RemoveCard(selectedCard);//Removes the card from the hand
                    selectedCard.callbackPlayer = CURRENT_PLAYER;
                    MoveToTarget(selectedCard);

                    selectedCard = null;

                    phase = TurnPhase.waiting;
                    Waiting_For_Hand_Slot_Selection = false;//the hand slot selected
                }
                else
                {
                        // Draw the top card, not necessarily the one clicked.
                        //CardBartok Dcb = CURRENT_PLAYER.AddCard(Draw());
                        //Dcb.callbackPlayer = CURRENT_PLAYER;
                        //Utils.tr("Bartok:CardClicked()", "Draw", Dcb.name);
                        //phase = TurnPhase.waiting;
                }
                break;


            case CBState.hand:
                if (Waiting_For_Hand_Slot_Selection)
                {
                    //tCB: the card that will be swapped out / the slot of the card that will be removed and where the selected card will be added
                    //selectedCard: the card that will be swapped in

                    SwapCard(tCB);
                }
                //else if (CardBelongsToPlayer(tCB) && PowerCard_Swap_bool)
                //{

                //}
                break;
        }
    }

    void SwapCard(CardBartok tCB)
    {
        CURRENT_PLAYER.RemoveCard(tCB);//Removes the card from the hand
        tCB.callbackPlayer = CURRENT_PLAYER;
        MoveToTarget(tCB);

        CURRENT_PLAYER.AddCard(selectedCard);//Adds the selected card to the hand
        selectedCard.callbackPlayer = CURRENT_PLAYER;
        selectedCard = null;

        phase = TurnPhase.waiting;
        Waiting_For_Hand_Slot_Selection = false;//the hand slot selected
    }

    public void SwapCard_AI(CardBartok tCB)
    {
        CardBartok old_target = targetCard;

        CURRENT_PLAYER.RemoveCard(tCB);//Removes the card from the hand
        tCB.callbackPlayer = CURRENT_PLAYER;
        MoveToTarget(tCB);

        CURRENT_PLAYER.AddCard(old_target);//Adds the selected card to the hand
        old_target.callbackPlayer = CURRENT_PLAYER;
        old_target = null;

        phase = TurnPhase.waiting;
        Waiting_For_Hand_Slot_Selection = false;//the hand slot selected
    }

    void PowerCard_DrawTwo()
    {
        //remove selectedcard

        //draw new card

        //wait for click to discard or swap

        //if player did not swap, draw a second card

        //wait for click to discard or swap
    }

    void PowerCard_Peek()
    {
        //removed selectedcard

        //wait for player to click card to peek at
        //--check if the card they clicked is in their hand
    }

    void PowerCard_Swap()
    {
        //removed selectedcard

        //wait for player to click card to swap with
        //--check if the card they clicked is NOT in their hand
    }

    bool CardBelongsToPlayer(CardBartok tCB)
    {
        foreach (CardBartok CB in CURRENT_PLAYER.hand)
        {
            if (CB == selectedCard)
            {
                return true;
            }
        }

        return false;
    }
}
