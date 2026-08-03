using UnityEngine;

public class StudentLook : MonoBehaviour
{
    [SerializeField] SpriteRenderer body;
    [SerializeField] SpriteRenderer clothing;
    [SerializeField] SpriteRenderer hair;

    [SerializeField] Color[] skinTones;
    [SerializeField] Color[] hairColors;
    [SerializeField] Sprite[] hairStyles;
    [SerializeField] Sprite[] outfits;

    public void Randomize()
    {
        if (body != null && skinTones.Length > 0)
            body.color = skinTones[Random.Range(0, skinTones.Length)];

        if (hair != null)
        {
            if (hairColors.Length > 0) hair.color  = hairColors[Random.Range(0, hairColors.Length)];
            if (hairStyles.Length > 0) hair.sprite = hairStyles[Random.Range(0, hairStyles.Length)];
        }

        if (clothing != null && outfits.Length > 0)
            clothing.sprite = outfits[Random.Range(0, outfits.Length)];
    }
}