using System.Collections.Generic;
using UnityEngine;

// Palette swap: plays a character's animations in different colours without new art.
// Runs after the Animator each frame and replaces the sprite it picked with a recoloured copy.
// Each sprite frame is recoloured once and cached. The rules live in a ColorPalette asset;
// with no palette set, the sprite is left alone.
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteRecolor : MonoBehaviour
{
    [SerializeField] private ColorPalette palette;

    private SpriteRenderer sr;
    private readonly List<Texture2D> textures = new List<Texture2D>();   // one per recoloured frame
    private readonly Dictionary<Texture2D, Color32[]> sourcePixels = new Dictionary<Texture2D, Color32[]>();
    private bool warnedTight;
    private readonly Dictionary<Sprite, Sprite> sprites = new Dictionary<Sprite, Sprite>();
    private readonly Dictionary<Sprite, Sprite> originals = new Dictionary<Sprite, Sprite>();   // copy -> original

    private void Awake() => sr = GetComponent<SpriteRenderer>();

    private void Start()
    {
        if (palette != null) Prewarm();   // a palette set in the Inspector
    }

    // Switch palette at runtime (MatchSetup uses this for player 2 in a mirror match). Null turns recolouring off.
    public void SetPalette(ColorPalette newPalette)
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (sr.sprite != null && originals.TryGetValue(sr.sprite, out Sprite original)) sr.sprite = original;
        ClearCache();
        palette = newPalette;
        if (palette != null && Application.isPlaying) Prewarm();
    }

    // Recolours every frame of every animation now, so nothing has to be made mid-fight.
    // Making a frame is slow (it reads the texture back from the GPU), and a slow frame during a fight
    // makes the game skip ahead to catch up, which looks like a fighter teleporting.
    public void Prewarm()
    {
        Animator anim = GetComponent<Animator>();
        if (palette == null || anim == null || anim.runtimeAnimatorController == null) return;

        Sprite current = sr.sprite;
        foreach (AnimationClip clip in anim.runtimeAnimatorController.animationClips)
        {
            int frames = Mathf.Max(1, Mathf.CeilToInt(clip.length * clip.frameRate));
            for (int f = 0; f <= frames; f++)
            {
                clip.SampleAnimation(gameObject, Mathf.Min(f / clip.frameRate, clip.length));
                Sprite s = sr.sprite;
                if (s == null || originals.ContainsKey(s) || sprites.ContainsKey(s)) continue;
                Sprite copy = MakeSprite(s);
                sprites[s] = copy;
                originals[copy] = s;
            }
        }
        sr.sprite = current;
    }

    private void LateUpdate()
    {
        Sprite s = sr.sprite;
        if (palette == null || s == null || originals.ContainsKey(s)) return;

        if (!sprites.TryGetValue(s, out Sprite copy))
        {
            copy = MakeSprite(s);
            sprites[s] = copy;
            originals[copy] = s;   // when a frame can't be recoloured, copy is the original itself
        }
        sr.sprite = copy;
    }

    private Sprite MakeSprite(Sprite s)
    {
        // In Play mode sprites can be packed into a Sprite Atlas: s.texture is then the atlas, and the frame's
        // pixels sit at s.textureRect (possibly trimmed, shifted by textureRectOffset), not at s.rect.
        // Tight-packed sprites aren't rectangles in the atlas, so they can't be copied this way.
        if (s.packed && s.packingMode == SpritePackingMode.Tight)
        {
            if (!warnedTight)
            {
                Debug.LogWarning(name + ": can't recolour tight-packed sprite " + s.name +
                                 ". Turn off Tight Packing on its Sprite Atlas, or untick Generate Sprite Atlas on the .aseprite file.");
                warnedTight = true;
            }
            return s;
        }

        Texture2D src = s.texture;
        if (!sourcePixels.TryGetValue(src, out Color32[] all))
        {
            all = ReadPixels(src);
            sourcePixels[src] = all;
        }

        Rect from = s.packed ? s.textureRect : s.rect;
        Vector2 offset = s.packed ? s.textureRectOffset : Vector2.zero;
        int w = Mathf.RoundToInt(s.rect.width), h = Mathf.RoundToInt(s.rect.height);
        Color32[] px = new Color32[w * h];   // starts fully transparent
        int fx = Mathf.RoundToInt(from.x), fy = Mathf.RoundToInt(from.y);
        int fw = Mathf.RoundToInt(from.width), fh = Mathf.RoundToInt(from.height);
        int ox = Mathf.RoundToInt(offset.x), oy = Mathf.RoundToInt(offset.y);
        for (int y = 0; y < fh; y++)
            for (int x = 0; x < fw; x++)
            {
                int tx = ox + x, ty = oy + y;
                if (tx < 0 || ty < 0 || tx >= w || ty >= h) continue;
                px[ty * w + tx] = all[(fy + y) * src.width + fx + x];
            }

        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = src.filterMode;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.SetPixels32(Recolor(px, w, h));
        tex.Apply();
        tex.name = s.name + " (recolor)";
        textures.Add(tex);

        // Same size and pivot as the original frame, so it lines up exactly
        Sprite copy = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(s.pivot.x / s.rect.width, s.pivot.y / s.rect.height),
            s.pixelsPerUnit, 0, SpriteMeshType.FullRect, s.border);
        copy.name = s.name + " (recolor)";
        return copy;
    }

    // Recolours one frame's pixels (w x h, row 0 = bottom) and returns them
    private Color32[] Recolor(Color32[] px, int w, int h)
    {

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
            foreach (ColorPalette.ColorSwap sw in palette.swaps)
            {
                if (!palette.Close(orig[i], sw.from) || !Matches(sw, p, h, orig, i, w)) continue;
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

    private bool Matches(ColorPalette.ColorSwap sw, PartInfo p, int height, Color32[] px, int i, int w)
    {
        if (sw.where == ColorPalette.Where.Everywhere) return true;
        if (sw.where == ColorPalette.Where.NextToColor) return TouchesColor(px, i, w, sw.with);
        if (p == null) return false;   // colour isn't in any material, so it has no part
        bool top = p.top < palette.topLimit * height;
        bool bottom = p.top >= palette.bottomLimit * height && p.bottom >= palette.bottomReach * height;
        switch (sw.where)
        {
            case ColorPalette.Where.TopPart: return top;
            case ColorPalette.Where.BottomPart: return bottom && !top;
            case ColorPalette.Where.MiddlePart: return !top && !bottom;
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
                if (n.a > 0 && palette.Close(n, color)) return true;
            }
        return false;
    }

    private int MaterialOf(Color32 c)
    {
        if (c.a == 0) return -1;
        for (int m = 0; m < palette.materials.Count; m++)
            if (palette.materials[m].colors.Exists(mc => palette.Close(mc, c))) return m;
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

    private void ClearCache()
    {
        foreach (KeyValuePair<Sprite, Sprite> pair in sprites)
            if (pair.Value != pair.Key) Destroy(pair.Value);   // never destroy an original we passed through unchanged
        foreach (Texture2D t in textures) Destroy(t);
        sprites.Clear();
        originals.Clear();
        textures.Clear();
        sourcePixels.Clear();
    }

    private void OnDestroy() => ClearCache();
}
