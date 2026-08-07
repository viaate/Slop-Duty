using System.Collections.Generic;
using UnityEngine;

// Builds a kid out of stacked sprite layers.
//
// Rewritten to match the art that actually exists. The previous version tinted a white
// body with a skin tone and tinted white hair with a hair color, which is the right
// approach when the source art is greyscale. Selena's layers are already colored: five
// separate skin sprites, five hair sprites that each have their own color baked in, five
// multi-color shirts. So every layer here is a sprite swap and nothing is tinted.
//
// That matters most for hair. Two of the five are black or near-black, and tinting
// multiplies, so black times any color is still black. Tinting would have given three
// working hair colors and two permanently black ones.
public class StudentLook : MonoBehaviour
{
    [Header("Layers, back to front")]
    [SerializeField] private SpriteRenderer skin;
    [SerializeField] private SpriteRenderer pants;
    [SerializeField] private SpriteRenderer shirt;
    [SerializeField] private SpriteRenderer hair;
    [SerializeField] private SpriteRenderer face;

    [Header("Options")]
    [SerializeField] private Sprite[] skins;
    [SerializeField] private Sprite[] trousers;
    [SerializeField] private Sprite[] shirts;
    [SerializeField] private Sprite[] hairStyles;

    [Header("Expressions")]
    [SerializeField] private Sprite normalFace;
    [SerializeField] private Sprite pukeFace;

    // Shared across every student, because the point is to stop two kids standing next to
    // each other with the same hair. Keyed by name rather than by the array itself, since
    // each instantiated prefab gets its own copy of the array and the references differ.
    private static readonly Dictionary<string, List<int>> bags = new Dictionary<string, List<int>>();

    // True when this kid is wearing somebody's face instead of a rolled outfit.
    private bool impersonating;

    public void Randomize()
    {
        // Name yourself after somebody in the cast and the whole canteen turns into them.
        //
        // Purely a joke, but it has to switch off the layers rather than draw on top of
        // them: these characters are one finished picture each, and a rolled shirt or a
        // rolled hairstyle underneath would show around the edges of it.
        if (Impersonate()) return;

        impersonating = false;

        Deal(skin, skins, "skin");
        Deal(pants, trousers, "pants");
        Deal(shirt, shirts, "shirt");
        Deal(hair, hairStyles, "hair");

        if (face != null && normalFace != null) face.sprite = normalFace;
    }

    private bool Impersonate()
    {
        Sprite face = BossDirector.FaceFor(HighScores.PlayerName);
        if (face == null) return false;

        impersonating = true;

        // The whole person goes on the skin layer, since that is the one at the bottom.
        if (skin != null) skin.sprite = face;

        Hide(pants);
        Hide(shirt);
        Hide(hair);
        Hide(this.face);

        return true;
    }

    private static void Hide(SpriteRenderer r)
    {
        if (r != null) r.enabled = false;
    }

    // Called when a kid is failed, so they leave wearing the right expression.
    public void ShowPukeFace()
    {
        // Nothing doing while impersonating: the face layer is switched off, so turning it
        // back on would stamp a loose pair of eyes over somebody's finished portrait.
        if (impersonating || face == null || pukeFace == null) return;

        face.sprite = pukeFace;
    }

    // Deals from a shuffled deck instead of rolling fresh every time, so all five options
    // appear before any repeats. Plain random with five options and four kids on screen
    // gives a better than 80% chance that two of them match, which reads as a bug even
    // though it is working exactly as asked.
    private static void Deal(SpriteRenderer target, Sprite[] options, string key)
    {
        if (target == null || options == null || options.Length == 0) return;

        if (options.Length == 1)
        {
            if (options[0] != null) target.sprite = options[0];
            return;
        }

        if (!bags.TryGetValue(key, out List<int> bag) || bag == null)
        {
            bag = new List<int>();
            bags[key] = bag;
        }

        // Refilled on empty, and also whenever the array size changed underneath it, which
        // happens if someone adds a sprite while the game is running.
        if (bag.Count == 0)
        {
            for (int i = 0; i < options.Length; i++) bag.Add(i);
        }

        int slot = Random.Range(0, bag.Count);
        int index = Mathf.Clamp(bag[slot], 0, options.Length - 1);
        bag.RemoveAt(slot);

        if (options[index] != null) target.sprite = options[index];
    }
}
