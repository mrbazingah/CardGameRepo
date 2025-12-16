using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Deterministic deck generator using a seed.
/// Both host and client generate the same deck order from the same seed.
/// </summary>
public static class DeterministicDeck
{
    /// <summary>
    /// Generates a deterministic deck from a seed.
    /// Returns a list of card values (2-14, where 14 is Ace).
    /// </summary>
    public static List<byte> GenerateDeck(int seed, bool removeSpecialCards = false)
    {
        // Set Unity's random seed for deterministic generation
        Random.InitState(seed);
        
        List<byte> deck = new List<byte>();
        
        // Create a standard 52-card deck (values 2-14, 4 suits)
        for (int suit = 0; suit < 4; suit++)
        {
            for (byte v = 2; v <= 14; v++)
            {
                if ((v == 2 || v == 10) && removeSpecialCards)
                    continue;
                    
                deck.Add(v);
            }
        }
        
        // Shuffle using deterministic random
        Shuffle(deck);
        
        return deck;
    }
    
    /// <summary>
    /// Deterministic shuffle using Unity's Random (which uses the seed).
    /// </summary>
    static void Shuffle<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int r = Random.Range(i, list.Count);
            (list[i], list[r]) = (list[r], list[i]);
        }
    }
    
    /// <summary>
    /// Draws a card from the deck (removes and returns the first card).
    /// </summary>
    public static byte DrawCard(List<byte> deck)
    {
        if (deck.Count == 0)
            return 0;
            
        byte card = deck[0];
        deck.RemoveAt(0);
        return card;
    }
    
    /// <summary>
    /// Draws multiple cards from the deck.
    /// </summary>
    public static List<byte> DrawCards(List<byte> deck, int count)
    {
        List<byte> cards = new List<byte>();
        for (int i = 0; i < count && deck.Count > 0; i++)
        {
            cards.Add(DrawCard(deck));
        }
        return cards;
    }
}

