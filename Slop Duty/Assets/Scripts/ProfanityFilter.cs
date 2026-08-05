using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

// Screens names before they reach a public scoreboard.
//
// Two things make a naive blocklist useless. People stretch letters (fuuuck) and swap in
// digits and symbols (f4ck, sh!t), so the raw string almost never matches the word. And a
// plain substring check on short words blocks innocent names, the classic case being
// "Scunthorpe", or here, anyone called CLASSIC being rejected for containing a three
// letter word. So:
//
//   1. The name is normalized first: lowercased, leetspeak mapped back to letters, and
//      everything that is not a letter stripped out.
//   2. Each blocked word becomes a pattern where every letter may repeat, so "fuuuuck"
//      still matches "fuck" without the blocklist needing every spelling.
//   3. Words of four letters or fewer only match if they are the WHOLE name. Longer words
//      match anywhere. That catches the deliberate cases without punishing real names.
public static class ProfanityFilter
{
    // Kept deliberately short. This is a name box on a student project, not a chat client,
    // and an enormous list mostly adds false positives.
    private static readonly string[] Blocked =
    {
        "fuck", "shit", "cunt", "bitch", "bastard", "wanker", "bollocks",
        "dick", "cock", "penis", "vagina", "pussy", "tits", "boobs",
        "arse", "ass", "asshole", "arsehole", "piss", "crap", "damn",
        "slut", "whore", "rape", "nazi", "hitler", "nigger", "nigga",
        "faggot", "fag", "retard", "spastic", "chink", "spic", "kike",
        "tranny", "queer", "dyke", "porn", "sex", "cum", "jizz", "anal",
    };

    public static bool IsClean(string raw) => Matched(raw) == null;

    // Returns the word that tripped it, or null when the name is fine. Handy for logging,
    // but do not show it to the player: it tells them exactly what to work around.
    public static string Matched(string raw)
    {
        string name = Normalize(raw);
        if (string.IsNullOrEmpty(name)) return null;

        for (int i = 0; i < Blocked.Length; i++)
        {
            string word = Blocked[i];
            string pattern = Stretchy(word);

            // Short words have to be the entire name, or every third name with "as" or
            // "cum" buried in it gets rejected.
            string full = word.Length <= 4 ? $"^{pattern}$" : pattern;

            if (Regex.IsMatch(name, full)) return word;
        }

        return null;
    }

    // "fuck" becomes "f+u+c+k+", so any amount of letter stretching still matches.
    private static string Stretchy(string word)
    {
        StringBuilder sb = new StringBuilder(word.Length * 2);

        for (int i = 0; i < word.Length; i++)
        {
            sb.Append(word[i]);
            sb.Append('+');
        }

        return sb.ToString();
    }

    private static string Normalize(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;

        StringBuilder sb = new StringBuilder(raw.Length);

        for (int i = 0; i < raw.Length; i++)
        {
            char c = char.ToLowerInvariant(raw[i]);

            switch (c)
            {
                case '4': case '@': c = 'a'; break;
                case '3': c = 'e'; break;
                case '1': case '!': case '|': c = 'i'; break;
                case '0': c = 'o'; break;
                case '$': case '5': c = 's'; break;
                case '7': c = 't'; break;
                case '8': c = 'b'; break;
                case '2': c = 'z'; break;
                case '9': c = 'g'; break;
            }

            // Anything that is still not a letter is dropped, which also collapses the
            // dots and underscores people use to break words up: f.u.c.k
            if (c >= 'a' && c <= 'z') sb.Append(c);
        }

        return sb.ToString();
    }
}
