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
    static bool PowerCard_DrawTwo_active = false;
    static bool PowerCard_DrawTwo_card2 = false;
    static bool PowerCard_Peek_bool = false;
    static bool PowerCard_Swap_bool = false;
    static bool PowerCard_Swap_myHand = false;
    static bool PowerCard_Swap_yourHand = false;
    static bool targetSelected = false;
    public bool timerComplete = false;
    static bool gameStarted = false;
    static bool lastround = false;
    static public Player winner = null;

    [Header("Set in Inspector")]
    public TextAsset deckXML;
    public TextAsset layoutXML;
    public Vector3 layoutCenter = Vector3.zero;
    public float handFanDegrees = 10f;
    public int numStartingCards = 4;
    public float drawTimeStagger = 0.1f;
    public int TURN_COUNTER = 0;
    public int TURN_LIMIT = 32;
    public Material[] materials;
    public GameObject background;

    [Header("Set Dynamically")]
    public Deck deck;
    public List<CardBartok> drawPile;
    public List<CardBartok> discardPile;
    public List<Player> players;
    public CardBartok targetCard,selectedCard,activePC;
    static CardBartok PowerCard_Peek_card, PowerCard_Swap_mycard, PowerCard_Swap_yourcard;
    public TurnPhase phase = TurnPhase.idle;

    private BartokLayout layout;
    private Transform layoutAnchor;
    public GameObject selected;
    public GameObject active_powercard;

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

    public void flipPlayerCards()
    {
        foreach (Player p in players)
        {
            if (p.type == PlayerType.human)
            {
                p.Flip();
            }
        }
    }

    public void Ratatcat()
    {
        if (TURN_COUNTER > 3 && !lastround)
        {
            TURN_LIMIT = TURN_COUNTER + 3;
            lastround = true;
        }
        
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

        players[0].hand[0].faceUp = true;
        players[0].hand[3].faceUp = true;

    }

    public void startgame()
    {
        players[0].hand[0].faceUp = false;
        players[0].hand[3].faceUp = false;

        players[0].hand[0].playerHandSlot = 1;
        players[0].hand[1].playerHandSlot = 2;
        players[0].hand[2].playerHandSlot = 3;
        players[0].hand[3].playerHandSlot = 4;

        gameStarted = true;
        Invoke("DrawFirstTarget", 1 /*drawTimeStagger * (numStartingCards * 4 + 4)*/);
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
            if (TURN_COUNTER == TURN_LIMIT)
            {
                if (CheckGameOver())
                {
                    TURN_LIMIT = 32;
                    TURN_COUNTER = 0;
                    return;
                }
            }
            
        }
        CURRENT_PLAYER = players[num];
        phase = TurnPhase.pre;

        CURRENT_PLAYER.TakeTurn();

        // Report the turn passing
        Utils.tr("Bartok:PassTurn()", "Old: " + lastPlayerNum, "New: " + CURRENT_PLAYER.playerNum);

        //HACK: deal with singleton randomly getting lost
        if (S == null)
        {
            S = this;
        }

        TURN_COUNTER++;


    }

    public bool CheckGameOver()
    {
        //// See if we need to reshuffle the discard pile into the draw pile
        //if(drawPile.Count == 0)
        //{
        //    List<Card> cards = new List<Card>();
        //    foreach (CardBartok cb in discardPile)
        //    {
        //        cards.Add(cb);
        //    }
        //    discardPile.Clear();
        //    Deck.Shuffle(ref cards);
        //    drawPile = UpgradeCardsList(cards);
        //    ArrangeDrawPile();
        //}

        //// Check to see if the current player has won
        //if(CURRENT_PLAYER.hand.Length == 0)
        //{
        //    // The player that just played has won!
        //    phase = TurnPhase.gameOver;
        //    Invoke("RestartGame", 1);
        //    return (true);
        //}
        //return (false);

        lastround = false;

        int p1_score = 0;
        int p2_score = 0;
        int p3_score = 0;
        int p4_score = 0;

        for(int p = 0;p < players.Count; p++)
        {
            for (int c = 0; c < players[p].hand.Length; c++)
            {
                if (players[p].hand[c].suit == "P")
                {
                    CardBartok tCB;
                    do
                    {
                        tCB = DrawFromDrawPile();
                    }
                    while (tCB.suit == "P");

                    players[p].hand[c] = tCB;
                }

                switch (p)
                {
                    case 0:
                        p1_score += DetermineCardScore(players[p].hand[c]);
                        break;
                    case 1:
                        p2_score += DetermineCardScore(players[p].hand[c]);
                        break;
                    case 2:
                        p3_score += DetermineCardScore(players[p].hand[c]);
                        break;
                    case 3:
                        p4_score += DetermineCardScore(players[p].hand[c]);
                        break;
                }
            }
        }

        if (p1_score < p2_score && p1_score < p3_score && p1_score < p4_score)
        {
            winner = players[0];
            phase = TurnPhase.gameOver;
            Utils.tr("Player 1 has won");
            Invoke("RestartGame", 1);
            return true;
        }
        else if(p2_score < p1_score && p2_score < p3_score && p2_score < p4_score)
        {
            winner = players[1];
            phase = TurnPhase.gameOver;
            Utils.tr("Player 2 has won");
            Invoke("RestartGame", 1);
            return true;
        }
        else if (p3_score < p1_score && p3_score < p2_score && p3_score < p4_score)
        {
            winner = players[2];
            phase = TurnPhase.gameOver;
            Utils.tr("Player 3 has won");
            Invoke("RestartGame", 1);
            return true;
        }
        else if (p4_score < p1_score && p4_score < p2_score && p4_score < p3_score)
        {
            winner = players[3];
            phase = TurnPhase.gameOver;
            Utils.tr("Player 4 has won");
            Invoke("RestartGame", 1);
            return true;
        }

        return false;

    }

    int DetermineCardScore(CardBartok cb)
    {
        int score = 0;

        switch (cb.suit)
        {
            case "N":
                switch (cb.rank)
                {
                    case 0:
                        score = 0;
                        break;
                    case 1:
                        score = 1;
                        break;
                    case 2:
                        score = 2;
                        break;
                    case 3:
                        score = 3;
                        break;
                    case 4:
                        score = 4;
                        break;
                    case 5:
                        score = 5;
                        break;
                    case 6:
                        score = 6;
                        break;
                    case 7:
                        score = 7;
                        break;
                    case 8:
                        score = 8;
                        break;

                    case 9:
                        score = 0;
                        break;
                    case 10:
                        score = 1;
                        break;
                    case 11:
                        score = 2;
                        break;
                    case 12:
                        score = 3;
                        break;
                    case 13:
                        score = 4;
                        break;
                    case 14:
                        score = 5;
                        break;
                    case 15:
                        score = 6;
                        break;
                    case 16:
                        score = 7;
                        break;
                    case 17:
                        score = 8;
                        break;

                    case 18:
                        score = 0;
                        break;
                    case 19:
                        score = 1;
                        break;
                    case 20:
                        score = 2;
                        break;
                    case 21:
                        score = 3;
                        break;
                    case 22:
                        score = 4;
                        break;
                    case 23:
                        score = 5;
                        break;
                    case 24:
                        score = 6;
                        break;
                    case 25:
                        score = 7;
                        break;
                    case 26:
                        score = 8;
                        break;

                    case 27:
                        score = 0;
                        break;
                    case 28:
                        score = 1;
                        break;
                    case 29:
                        score = 2;
                        break;
                    case 30:
                        score = 3;
                        break;
                    case 31:
                        score = 4;
                        break;
                    case 32:
                        score = 5;
                        break;
                    case 33:
                        score = 6;
                        break;
                    case 34:
                        score = 7;
                        break;
                    case 35:
                        score = 8;
                        break;
                }
                break;
            case "C":
                score = 9;
                break;
        }

        return score;
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
        tCB.transform.localPosition = selected.transform.position;
        tCB.callbackPlayer = null;
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

    public void setActivePowerCard(CardBartok tCB)
    {
        activePC = tCB;
        activePC.state = CBState.activePC;
        activePC.SetSortingLayerName(layout.discardPile.layerName);
        activePC.transform.localPosition = active_powercard.transform.position;
        activePC.callbackPlayer = null;
    }
    
    public void RestackDrawPile()
    {
        // If the drawPile is now empty
        // We need to shuffle the discards into the drawPile
        int ndx;
        while (discardPile.Count > 0)
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

    // The Draw function will pull a single card from the drawPile and return it
    public CardBartok DrawFromDrawPile()
    {
        CardBartok cd = MoveToSelected(drawPile[0]); // Pull the 0th CardBartok
        if (drawPile.Count == 0)
        {
            RestackDrawPile();
        }
        drawPile.RemoveAt(0); // Then remove it from List<> drawPile
        return (cd); // And return it
    }

    public void CardClicked(CardBartok tCB)
    {
        //if (!gameStarted) startgame();
        if (PowerCard_Peek_card != null)
        {
            PowerCard_Peek_card.faceUp = false;
            PowerCard_Peek_card = null;
            PowerCard_Peek_bool = false;
            MoveToDiscard(activePC);
            activePC = null;
            PassTurn(1);
            return;
        }
        if (!CardBelongsToPlayer(tCB) && !PowerCard_Swap_yourHand) return;
        if (CURRENT_PLAYER.type != PlayerType.human) return;
        if (phase == TurnPhase.waiting) return;

        switch (tCB.state)
        {
            //DRAW FROM DRAW PILE
            case CBState.drawpile:
                if (!Waiting_For_Hand_Slot_Selection && !PowerCard_DrawTwo_active)//Checks if the program is waiting for the hand slot to be chosen
                {
                    //selectedCard.callbackPlayer = CURRENT_PLAYER;
                    selectedCard = DrawFromDrawPile(); //selects the card at the top of the draw pile
                    selectedCard.faceUp = true;
                    selectedCard.callbackPlayer = null;
                    Waiting_For_Hand_Slot_Selection = true;  //we are now waiting for the hand slot to be selected

                    if (selectedCard.suit == "P")
                    {//Todo: fix power card draw two
                        if (selectedCard.def.rank <= 2)
                        {
                            PowerCard_DrawTwo();
                        }
                        else if (selectedCard.def.rank <= 5)
                        {
                            PowerCard_Peek_bool = true;

                            setActivePowerCard(selectedCard);
                            selectedCard = null;

                        }
                        else if (selectedCard.def.rank <= 8)
                        {
                            PowerCard_Swap_bool = true;
                            Waiting_For_Hand_Slot_Selection = true;
                            PowerCard_Swap_myHand = true;

                            setActivePowerCard(selectedCard);
                            selectedCard = null;
                            //PowerCard_DrawTwo();
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
                    if (PowerCard_DrawTwo_active)
                    {
                        selectedCard.callbackPlayer = null;
                    }
                    else
                    {
                        selectedCard.callbackPlayer = CURRENT_PLAYER;
                    }
                    
                    MoveToTarget(selectedCard);

                    selectedCard = null;

                    Waiting_For_Hand_Slot_Selection = false;//the hand slot selected

                    if (PowerCard_DrawTwo_active && !PowerCard_DrawTwo_card2)
                    {
                        PowerCard_DrawTwo_card2 = true;

                        PowerCard_DrawTwo();
                    } 
                    else if (PowerCard_DrawTwo_active && PowerCard_DrawTwo_card2)
                    {
                        PowerCard_DrawTwo_active = false;
                        PowerCard_DrawTwo_card2 = false;

                    }
                    else
                    {
                        phase = TurnPhase.waiting;
                    }
                }
                else
                {
                    if (targetSelected) return;

                    selectedCard = MoveToSelected(targetCard);
                    selectedCard.callbackPlayer = null;
                    targetCard = null;

                    Waiting_For_Hand_Slot_Selection = true;
                    targetSelected = true;
                }
                break;


            case CBState.hand:
                if (Waiting_For_Hand_Slot_Selection)
                {
                    //tCB: the card that will be swapped out / the slot of the card that will be removed and where the selected card will be added
                    //selectedCard: the card that will be swapped in
                    if (PowerCard_Peek_bool && CardBelongsToPlayer(tCB))
                    {
                        tCB.faceUp = true;
                        PowerCard_Peek_card = tCB;
                        Waiting_For_Hand_Slot_Selection = false;
                    }
                    else
                    {
                        if (targetSelected) targetSelected = false;
                        if (!(PowerCard_Swap_myHand || PowerCard_Swap_yourHand))
                        {
                            SwapCard(tCB);
                        }
                    }

                    if (PowerCard_Swap_myHand && CardBelongsToPlayer(tCB))
                    {
                        background.GetComponent<Renderer>().material = materials[tCB.playerHandSlot];
                        PowerCard_Swap_mycard = tCB;
                        PowerCard_Swap_myHand = false;
                        PowerCard_Swap_yourHand = true;
                    }else if (PowerCard_Swap_yourHand && !CardBelongsToPlayer(tCB))
                    {
                        PowerCard_Swap_yourcard = tCB;
                        PowerCard_Swap_yourHand = false;
                        PowerCard_Swap();
                        background.GetComponent<Renderer>().material = materials[0];

                    }
                    

                    if (PowerCard_DrawTwo_active)
                    {
                        PowerCard_DrawTwo_active = false;
                        MoveToDiscard(activePC);
                        activePC = null;
                    }
                }
                //else if (CardBelongsToPlayer(tCB) && PowerCard_Swap_bool)
                //{

                //}
                break;
        }
    }

    void SwapCard(CardBartok tCB)
    {
        selectedCard.playerHandSlot = tCB.playerHandSlot;
        tCB.playerHandSlot = -1;

        CURRENT_PLAYER.RemoveCard(tCB);//Removes the card from the hand
        tCB.callbackPlayer = null;
        MoveToTarget(tCB);

        selectedCard.faceUp = false;
        CURRENT_PLAYER.AddCard(selectedCard);//Adds the selected card to the hand
        selectedCard.callbackPlayer = CURRENT_PLAYER;
        selectedCard = null;

        phase = TurnPhase.waiting;
        Waiting_For_Hand_Slot_Selection = false;//the hand slot selected
    }

    public void AI_TakeTurn()
    {
        int x = Random.Range(0, 2);
        //switch (x)
        //{
        //    case 0://draw from drawpile
                selectedCard = DrawFromDrawPile(); //selects the card at the top of the draw pile
                selectedCard.callbackPlayer = null;
            //    break;
            //case 1://draw from discard
            //    selectedCard = MoveToSelected(targetCard);
            //    selectedCard.callbackPlayer = null;
            //    targetCard = null;
            //    break;
        //}
        //if (/*selectedCard.suit == "P"*/false)
        //{
        //    if (selectedCard.def.rank <= 2)
        //    {
        //        setActivePowerCard(selectedCard);
        //        selectedCard = null;

        //        selectedCard = DrawFromDrawPile(); //selects the card at the top of the draw pile
        //        selectedCard.callbackPlayer = null;
        //    }
        //    else if (selectedCard.def.rank <= 5)
        //    {
        //        //setActivePowerCard(selectedCard);
        //        //selectedCard = null;

        //    }
        //    else if (selectedCard.def.rank <= 8)
        //    {
        //        //setActivePowerCard(selectedCard);
        //        //selectedCard = null;
        //    }
        //}
        //else
        //{
            SwapCard(CURRENT_PLAYER.hand[Random.Range(0, 3)]);
        //}
    }

    void PowerCard_DrawTwo()
    {
        if (!PowerCard_DrawTwo_active)
        {
            //remove selectedcard
            setActivePowerCard(selectedCard);
            selectedCard = null;

            //draw new card
            selectedCard = DrawFromDrawPile(); //selects the card at the top of the draw pile
            selectedCard.faceUp = true;

            Waiting_For_Hand_Slot_Selection = true;
            PowerCard_DrawTwo_active = true;
        }else if (PowerCard_DrawTwo_active)
        {
            //if player did not swap, draw a second card
            selectedCard = DrawFromDrawPile(); //selects the card at the top of the draw pile
            selectedCard.faceUp = true;

            Waiting_For_Hand_Slot_Selection = true;
        }
    }
    void PowerCard_Swap()
    {
        int myCard_player_index = -1;
        int myCard_card_index = -1;
        int yourCard_player_index = -1;
        int yourCard_card_index = -1;

        for (int p = 0; p < players.Count; p++)
        {
            for (int c = 0; c < players[p].hand.Length; c++)
            {
                if (players[p].hand[c] == PowerCard_Swap_mycard)
                {
                    myCard_player_index = p;
                    myCard_card_index = c;
                    break;
                }
            }
        }

        for (int p = 0;p < players.Count; p++)
        {
            for (int c = 0;c < players[p].hand.Length; c++)
            {
                if (players[p].hand[c] == PowerCard_Swap_yourcard)
                {
                    yourCard_player_index = p;
                    yourCard_card_index = c;
                    break;
                }
            }
        }

        players[yourCard_player_index].hand[yourCard_card_index] = null;
        players[yourCard_player_index].AddCard(PowerCard_Swap_mycard);
        PowerCard_Swap_mycard.callbackPlayer = null;
        PowerCard_Swap_mycard = null;

        players[myCard_player_index].hand[myCard_card_index] = null;
        CURRENT_PLAYER.AddCard(PowerCard_Swap_yourcard);
        PowerCard_Swap_yourcard.callbackPlayer = CURRENT_PLAYER;
        PowerCard_Swap_yourcard = null;

        MoveToDiscard(activePC);
        activePC = null;
        PowerCard_Swap_bool = false;
    }

    bool CardBelongsToPlayer(CardBartok tCB)
    {
        if (CURRENT_PLAYER == null) return false;

        foreach (CardBartok CB in players[0].hand)
        {
            if (CB == tCB)
            {
                return true;
            }
        }
        if ((tCB.state == CBState.drawpile || tCB.state == CBState.target) && (!PowerCard_Peek_bool && !PowerCard_Swap_bool)) return true;

        return false;
    }
}
