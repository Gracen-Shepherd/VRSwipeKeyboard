using System.Collections.Generic;
using UnityEngine;

public static class KeyboardLayout
{
    public static readonly Dictionary<char, Vector2> Keys = new Dictionary<char, Vector2>()
    {
        // --- TOP ROW (Y = 2.5) ---
        { 'q', new Vector2(0.50f, 2.5f) },
        { 'w', new Vector2(1.50f, 2.5f) },
        { 'e', new Vector2(2.50f, 2.5f) },
        { 'r', new Vector2(3.50f, 2.5f) },
        { 't', new Vector2(4.50f, 2.5f) },
        { 'y', new Vector2(5.50f, 2.5f) },
        { 'u', new Vector2(6.50f, 2.5f) },
        { 'i', new Vector2(7.50f, 2.5f) },
        { 'o', new Vector2(8.50f, 2.5f) },
        { 'p', new Vector2(9.50f, 2.5f) },

        // --- MIDDLE ROW (Y = 1.5) ---
        { 'a', new Vector2(0.75f, 1.5f) },
        { 's', new Vector2(1.75f, 1.5f) },
        { 'd', new Vector2(2.75f, 1.5f) },
        { 'f', new Vector2(3.75f, 1.5f) },
        { 'g', new Vector2(4.75f, 1.5f) },
        { 'h', new Vector2(5.75f, 1.5f) },
        { 'j', new Vector2(6.75f, 1.5f) },
        { 'k', new Vector2(7.75f, 1.5f) },
        { 'l', new Vector2(8.75f, 1.5f) },

        // --- BOTTOM ROW (Y = 0.5) ---
        { 'z', new Vector2(1.25f, 0.5f) },
        { 'x', new Vector2(2.25f, 0.5f) },
        { 'c', new Vector2(3.25f, 0.5f) },
        { 'v', new Vector2(4.25f, 0.5f) },
        { 'b', new Vector2(5.25f, 0.5f) },
        { 'n', new Vector2(6.25f, 0.5f) },
        { 'm', new Vector2(7.25f, 0.5f) },
        { ',', new Vector2(8.25f, 0.5f) },
        { '.', new Vector2(9.25f, 0.5f) },
    };
}