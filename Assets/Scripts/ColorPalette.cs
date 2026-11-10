using System.Collections.Generic;
using UnityEngine;

// A set of colour-swap rules for SpriteRecolor, e.g. "Myrmillo Gold".
// Create with Assets > Create > Fighting Game > Color Palette; a new one starts filled with the gold Myrmillo rules.
//
// Rules can depend on which part of the body a pixel is in. A "part" is a group of touching pixels
// whose colours are all in the same material (for example the three metal greys), so the helmet,
// the blade and the shin guard are separate parts even though they use the same colours.
[CreateAssetMenu(menuName = "Fighting Game/Color Palette", fileName = "NewPalette")]
public class ColorPalette : ScriptableObject
{
    public enum Where
    {
        Everywhere,
        TopPart,        // part starts near the top of the sprite (helmet)
        MiddlePart,     // part is neither top nor bottom (blade)
        BottomPart,     // part starts low and reaches the feet (shin guard)
        NextToColor,    // the pixel touches the "with" colour, diagonals included (arm wrap is light and shadow mixed)
    }

    [System.Serializable]
    public class PartMaterial
    {
        public string name;
        public List<Color32> colors = new List<Color32>();
    }

    [System.Serializable]
    public class ColorSwap
    {
        public Color32 from;
        public Color32 to;
        public Where where;
        public Color32 with;   // only used by NextToColor
    }

    [Tooltip("Colours that count as one material when finding parts")]
    public List<PartMaterial> materials = new List<PartMaterial>();
    [Tooltip("Checked in order, the first rule that matches a pixel wins. Pixels with no matching rule keep their colour.")]
    public List<ColorSwap> swaps = new List<ColorSwap>();
    [Range(0, 16)] public int tolerance = 2;   // per channel, 0-255, in case of compression

    [Header("part position (fraction of sprite height, from the top)")]
    [Range(0f, 1f)] public float topLimit = 0.25f;      // a part starting above this is a top part
    [Range(0f, 1f)] public float bottomLimit = 0.65f;   // a bottom part starts below this...
    [Range(0f, 1f)] public float bottomReach = 0.85f;   // ...and reaches below this (the feet)

    // Filled in when the asset is created (or Reset from its menu): Myrmillo's helmet, arm guard and shin guard
    // turn gold, and the lower body cloth turns red. Everything else (blade, shield, ankle wrap, skin) keeps its colour.
    private void Reset()
    {
        Color32 metalLight = C(0x939393), metalMid = C(0x757575), metalDark = C(0x5B5B5B);
        Color32 armLight = C(0xDCD5CB), armShadow = C(0xBEB5A7);
        Color32 gold = C(0xD4B21F), darkGold = C(0xA98D17), darkestGold = C(0x7D6A16), red = C(0xCF2424);

        // Each metal shade is its own material, so the grey shield in the damaged frames doesn't join up with the shin guard
        materials = new List<PartMaterial>
        {
            new PartMaterial { name = "metal light", colors = new List<Color32> { metalLight } },
            new PartMaterial { name = "metal mid",   colors = new List<Color32> { metalMid } },
            new PartMaterial { name = "metal dark",  colors = new List<Color32> { metalDark } },
            new PartMaterial { name = "cloth",       colors = new List<Color32> { armLight, armShadow } },
        };
        swaps = new List<ColorSwap>
        {
            // helmet
            new ColorSwap { from = metalLight, to = gold,        where = Where.TopPart },
            new ColorSwap { from = metalMid,   to = darkGold,    where = Where.TopPart },
            new ColorSwap { from = metalDark,  to = darkestGold, where = Where.TopPart },
            // shin guard
            new ColorSwap { from = metalLight, to = gold,        where = Where.BottomPart },
            new ColorSwap { from = metalMid,   to = darkGold,    where = Where.BottomPart },
            new ColorSwap { from = metalDark,  to = darkestGold, where = Where.BottomPart },
            // arm guard: light and shadow pixels alternate, so its light pixels always touch a shadow pixel
            new ColorSwap { from = armLight,  to = gold,     where = Where.NextToColor, with = armShadow },
            new ColorSwap { from = armShadow, to = darkGold, where = Where.Everywhere },
            // lower body cloth: the light cloth left in the middle of the body (the ankle wrap is a bottom part, so it stays)
            new ColorSwap { from = armLight,  to = red,      where = Where.MiddlePart },
        };
    }

    private static Color32 C(int rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);

    public bool Close(Color32 a, Color32 b) =>
        Mathf.Abs(a.r - b.r) <= tolerance && Mathf.Abs(a.g - b.g) <= tolerance && Mathf.Abs(a.b - b.b) <= tolerance;
}
