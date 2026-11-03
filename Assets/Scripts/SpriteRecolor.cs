using System.Collections.Generic;
using UnityEngine;

// Palette swap: plays another character's animations in different colours without new art.
// Runs after the Animator each frame and replaces the sprite it picked with a recoloured copy.
// Each sprite frame is recoloured once and cached.
//
// Rules can depend on which part of the body a pixel is in. A "part" is a group of touching pixels
// whose colours are all in the same material (for example the three metal greys), so the helmet,
// the blade and the shin guard are separate parts even though they use the same colours.
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteRecolor : MonoBehaviour
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
    [SerializeField] private List<PartMaterial> materials = new List<PartMaterial>();
    [Tooltip("Checked in order, the first rule that matches a pixel wins. Pixels with no matching rule keep their colour.")]
    [SerializeField] private List<ColorSwap> swaps = new List<ColorSwap>();
    [SerializeField, Range(0, 16)] private int tolerance = 2;   // per channel, 0-255, in case of compression

    [Header("part position (fraction of sprite height, from the top)")]
    [SerializeField, Range(0f, 1f)] private float topLimit = 0.25f;      // a part starting above this is a top part
    [SerializeField, Range(0f, 1f)] private float bottomLimit = 0.65f;   // a bottom part starts below this...
    [SerializeField, Range(0f, 1f)] private float bottomReach = 0.85f;   // ...and reaches below this (the feet)

    private SpriteRenderer sr;
    private readonly Dictionary<Texture2D, Texture2D> textures = new Dictionary<Texture2D, Texture2D>();
    private readonly Dictionary<Texture2D, Color32[]> sourcePixels = new Dictionary<Texture2D, Color32[]>();
    private readonly Dictionary<Sprite, Sprite> sprites = new Dictionary<Sprite, Sprite>();
    private readonly HashSet<Sprite> recolored = new HashSet<Sprite>();

    private void Awake() => sr = GetComponent<SpriteRenderer>();

    // Filled in when the component is first added: Myrmillo's helmet, arm guard and shin guard turn gold,
    // and the lower body cloth turns red. Everything else (blade, shield, ankle wrap, skin) keeps its colour.
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

    private void LateUpdate()
    {
        Sprite s = sr.sprite;
        if (s == null || recolored.Contains(s)) return;

        if (!sprites.TryGetValue(s, out Sprite copy))
        {
            copy = MakeSprite(s);
            sprites[s] = copy;
            recolored.Add(copy);
        }
        sr.sprite = copy;
    }

    private Sprite MakeSprite(Sprite s)
    {
        Texture2D src = s.texture;
        if (!textures.TryGetValue(src, out Texture2D tex))
        {
            Color32[] px = ReadPixels(src);
            sourcePixels[src] = px;
            tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
            tex.filterMode = src.filterMode;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixels32(px);
            tex.name = src.name + " (recolor)";
            textures[src] = tex;
        }

        RectInt r = new RectInt((int)s.rect.x, (int)s.rect.y, (int)s.rect.width, (int)s.rect.height);
        tex.SetPixels32(r.x, r.y, r.width, r.height, Recolor(sourcePixels[src], src.width, r));
        tex.Apply();

        Sprite copy = Sprite.Create(tex, s.rect, new Vector2(s.pivot.x / s.rect.width, s.pivot.y / s.rect.height),
            s.pixelsPerUnit, 0, SpriteMeshType.FullRect, s.border);
        copy.name = s.name + " (recolor)";
        return copy;
    }

    // Recolours one sprite's rectangle and returns its pixels (row 0 = bottom, like the texture)
    private Color32[] Recolor(Color32[] all, int texWidth, RectInt r)
    {
        int w = r.width, h = r.height;
        Color32[] px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = all[(r.y + y) * texWidth + r.x + x];

        // Label parts: flood fill touching pixels of the same material
        int[] material = new int[px.Length];
        int[] part = new int[px.Length];
        for (int i = 0; i < px.Length; i++) { material[i] = MaterialOf(px[i]); part[i] = -1; }

        var parts = new List<PartInfo>();
        var stack = new Stack<int>();
        for (int start = 0; start < px.Length; start++)
        {
            if (material[start] < 0 || part[start] >= 0) continue;
            var info = new PartInfo { top = int.MaxValue, bottom = int.MinValue };
            int id = parts.Count;
            part[start] = id;
            stack.Push(start);
            while (stack.Count > 0)
            {
                int i = stack.Pop();
                int row = h - 1 - i / w;   // rows counted from the top
                info.top = Mathf.Min(info.top, row);
                info.bottom = Mathf.Max(info.bottom, row);
                int x = i % w;
                if (x > 0) Visit(i - 1);
                if (x < w - 1) Visit(i + 1);
                if (i >= w) Visit(i - w);
                if (i < px.Length - w) Visit(i + w);
            }
            parts.Add(info);

            void Visit(int n)
            {
                if (part[n] >= 0 || material[n] != material[start]) return;
                part[n] = id;
                stack.Push(n);
            }
        }

        Color32[] orig = (Color32[])px.Clone();   // neighbour checks look at the colours before recolouring
        for (int i = 0; i < px.Length; i++)
        {
            if (px[i].a == 0) continue;
            PartInfo p = part[i] >= 0 ? parts[part[i]] : null;
            foreach (ColorSwap sw in swaps)
            {
                if (!Close(orig[i], sw.from) || !Matches(sw, p, h, orig, i, w)) continue;
                px[i] = new Color32(sw.to.r, sw.to.g, sw.to.b, px[i].a);
                break;
            }
        }
        return px;
    }

    private class PartInfo
    {
        public int top, bottom;   // highest and lowest row of the part, counted from the top of the sprite
    }

    private bool Matches(ColorSwap sw, PartInfo p, int height, Color32[] px, int i, int w)
    {
        if (sw.where == Where.Everywhere) return true;
        if (sw.where == Where.NextToColor) return TouchesColor(px, i, w, sw.with);
        if (p == null) return false;   // colour isn't in any material, so it has no part
        bool top = p.top < topLimit * height;
        bool bottom = p.top >= bottomLimit * height && p.bottom >= bottomReach * height;
        switch (sw.where)
        {
            case Where.TopPart: return top;
            case Where.BottomPart: return bottom && !top;
            case Where.MiddlePart: return !top && !bottom;
        }
        return false;
    }

    private bool TouchesColor(Color32[] px, int i, int w, Color32 color)
    {
        int h = px.Length / w, x = i % w, y = i / w;
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if ((dx == 0 && dy == 0) || nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                Color32 n = px[ny * w + nx];
                if (n.a > 0 && Close(n, color)) return true;
            }
        return false;
    }

    private int MaterialOf(Color32 c)
    {
        if (c.a == 0) return -1;
        for (int m = 0; m < materials.Count; m++)
            if (materials[m].colors.Exists(mc => Close(mc, c))) return m;
        return -1;
    }

    // Uses the texture directly if Read/Write is on, otherwise copies it through a RenderTexture
    private static Color32[] ReadPixels(Texture2D src)
    {
        if (src.isReadable) return src.GetPixels32();

        RenderTexture rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        RenderTexture prev = RenderTexture.active;
        Graphics.Blit(src, rt);
        RenderTexture.active = rt;
        Texture2D tmp = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
        tmp.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
        tmp.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);

        Color32[] px = tmp.GetPixels32();
        Destroy(tmp);
        return px;
    }

    private bool Close(Color32 a, Color32 b) =>
        Mathf.Abs(a.r - b.r) <= tolerance && Mathf.Abs(a.g - b.g) <= tolerance && Mathf.Abs(a.b - b.b) <= tolerance;

    private void OnDestroy()
    {
        foreach (Sprite s in recolored) Destroy(s);
        foreach (Texture2D t in textures.Values) Destroy(t);
    }
}
