﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq; // Enables LINQ queries, which will be explained soon

// The player can either be human or an AI
public enum PlayerType
{
    human,
    ai
}

[System.Serializable]
public class Player
{
    public PlayerType type = PlayerType.ai;
    public int playerNum;
    public SlotDef handSlotDef;
    public CardBartok[] hand; // The cards in this player's hand

    // Add a card to the hand
    public CardBartok AddCard(CardBartok eCB)
    {
        if (hand == null) hand = new CardBartok[4];

        // Add the card to the hand
        bool CardAdded = false;
        for (int i = 0; i < 4; i++)
        {
            if (hand[i] == null && !CardAdded)
            {
                hand[i] = eCB;
                CardAdded = true;
            }
        }
        //if (hand.Length != 4)
        //{
        //    hand[hand.Length] = eCB;
        //}


        //Sort the cards by rank using LINQ if this is a human
        //if (type == PlayerType.human)
        //{
        //    // This is the LINQ call
        //    hand = hand.OrderBy(cd => cd.rank).ToArray();
        //    // Note: LINQ operations can be a bit slow (like it could take a
        //    // couple of milliseconds), but since we're only doing it once
        //    // every round, it isn't a problem.
        //}

        eCB.SetSortingLayerName("10"); // Sorts the moving card to the top
        eCB.eventualSortLayer = handSlotDef.layerName;

        FanHand();
        return (eCB);
    }

    // Remove a card from the hand
    public CardBartok RemoveCard(CardBartok cb)
    {
        // If hand is null or doesn't contain cb, return null
        if (hand == null || !hand.Contains(cb)) return null;

        for (int i = 0; i < hand.Length; i++)
        {
            if (hand[i] == cb)
            {
                hand[i] = null;
            }

        }
        FanHand();
        return (cb);
    }

    public void FanHand()
    {
        // startRot is the rotation about Z of the first card
        float startRot = 0;
        startRot = handSlotDef.rot;
        if (hand.Length > 1)
        {
            startRot += Bartok.S.handFanDegrees * (hand.Length - 1) / 2;
        }

        // Move all the cards to their new positions
        Vector3 pos;
        float rot;
        Quaternion rotQ;
        switch (playerNum)
        {
            case 1:
                rotQ = Quaternion.Euler(0, 0, 0);
                break;
            case 2:
                rotQ = Quaternion.Euler(0, 0, -90);
                break;
            case 3:
                rotQ = Quaternion.Euler(0, 0, 180);
                break;
            case 4:
                rotQ = Quaternion.Euler(0, 0, -270);
                break;
            default:
                rotQ = Quaternion.Euler(0, 0, 0);
                break;
        }
        for (int i = 0; i < hand.Length; i++)
        {
            if (hand[i] != null)
            {
                rot = startRot - Bartok.S.handFanDegrees * i;
                //rotQ = Quaternion.Euler(0, 0, rot);


                //pos = Vector3.up * CardBartok.CARD_HEIGHT / 2f;

                //pos = rotQ * pos;

                // Add the base position of the player's hand (which will be at the
                // bottom-center of the fan of the cards)
                pos = handSlotDef.pos;
                //pos.z = -0.5f * i;

                switch (playerNum)
                {
                    case 1:
                        pos.x = -4.5f;
                        pos.x += 3 * i;
                        break;
                    case 2:
                        pos.y = 4.5f;
                        pos.y -= 3 * i;
                        break;
                    case 3:
                        pos.x = -4.5f;
                        pos.x += 3 * i;
                        break;
                    case 4:
                        pos.y = -4.5f;
                        pos.y += 3 * i;
                        break;
                    default:
                        pos.x = -4.5f;
                        pos.x += 3 * i;
                        break;
                }

                // If not the initial deal, start moving the card immediately.
                if (Bartok.S.phase != TurnPhase.idle)
                {
                    hand[i].timeStart = 0;
                }

                // Set the localPosition and rotation of the ith card in the hand
                hand[i].MoveTo(pos, rotQ); // Tell CardBartok to interpolate
                hand[i].state = CBState.toHand;
                // After the move, CardBartok will set the state to CBState.hand

                /* <= This begins a multiline comment
                hand[i].transform.localPosition = pos;
                hand[i].transform.rotation = rotQ;
                hand[i].state = CBState.hand; 
                This ends the multiline comment => */

                //hand[i].faceUp = (type == PlayerType.human);

                // Set the SortOrder of the cards so that they overlap properly
                hand[i].eventualSortOrder = i * 4;
                //hand[i].SetSortOrder(i * 4);
            }

        }
    }

    public void Flip()
    {
        hand[0].faceUp = !hand[0].faceUp;
        hand[3].faceUp = !hand[3].faceUp;

    }

    // The TakeTurn() function enables the AI of the computer Players
    public void TakeTurn()
    {
        Utils.tr("Player.TakeTurn");

        // Don't need to do anything if this is the human player.
        if (type == PlayerType.human) return;

        Bartok.S.phase = TurnPhase.waiting;

        CardBartok cb;

        // If this is an AI player, need to make a choice about what to play
        // Find valid plays
        List<CardBartok> validCards = new List<CardBartok>();
        foreach (CardBartok tCB in hand)
        {
            if (Bartok.S.ValidPlay(tCB))
            {
                validCards.Add(tCB);
            }
        }

        // So, there is a card or more to play, so pick one
        cb = validCards[Random.Range(0, validCards.Count)];

        Bartok.S.AI_TakeTurn();

    }

    public void CBCallback(CardBartok tCB)
    {
        //Utils.tr("Player.CBCallback()", tCB.name, "Player " + playerNum);
        // The card is done moving, so pass the turn
        Bartok.S.PassTurn();
    }
}
