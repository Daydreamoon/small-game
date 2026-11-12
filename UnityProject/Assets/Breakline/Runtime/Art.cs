using UnityEngine;

namespace Breakline
{
    /// <summary>
    /// All M1 art is generated in code so the greybox has zero external assets.
    /// Textures are intentionally tiny, point-filtered and paletted.
    /// </summary>
    public sealed class Art
    {
        public const int TilePixels = 16;
        public const int AtlasTiles = 5; // dirt, concrete, support, indestructible, white
        public readonly int[] TileUvIndex = { 0, 0, 1, 2, 3 };

        public Texture2D TileAtlas { get; private set; }
        public Texture2D White { get; private set; }
        public Texture2D UICircle { get; private set; }
        public Texture2D UIRing { get; private set; }
        public Font UIFont { get; private set; }

        public Sprite PlayerSprite { get; private set; }
        public Sprite DummySprite { get; private set; }
        public Sprite PatrolSprite { get; private set; }
        public Sprite ArmoredSprite { get; private set; }
        public Sprite BarrelSprite { get; private set; }
        public Sprite CrateSprite { get; private set; }
        public Sprite GrenadeSprite { get; private set; }
        public Sprite GoalSprite { get; private set; }
        public Sprite RingSprite { get; private set; }
        public Sprite FlashSprite { get; private set; }
        public Sprite SlashSprite { get; private set; }
        public Sprite WhiteSprite { get; private set; }
        public Sprite[] TileSprites { get; private set; } // index 0 dirt, 1 concrete, 2 support, 3 indestructible, 4 white

        public Material TerrainMaterial { get; private set; }
        public Material DebrisMaterial { get; private set; }
        public Material LineMaterial { get; private set; }

        public void Build()
        {
            BuildTileAtlas();
            BuildUiTextures();
            PlayerSprite = MakeSprite(12, 20, DrawPlayer);
            DummySprite = MakeSprite(12, 18, DrawDummy);
            PatrolSprite = MakeSprite(12, 18, DrawPatrol);
            ArmoredSprite = MakeSprite(18, 22, DrawArmored);
            BarrelSprite = MakeSprite(10, 14, DrawBarrel);
            CrateSprite = MakeSprite(12, 12, DrawCrate);
            GrenadeSprite = MakeSprite(6, 6, DrawGrenade);
            GoalSprite = MakeSprite(16, 26, DrawGoal);
            RingSprite = MakeRing(64, 2f);
            FlashSprite = MakeFlash(32);
            SlashSprite = MakeSlash(28);
            WhiteSprite = Sprite.Create(White, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 16f, 0, SpriteMeshType.FullRect);

            UIFont = Resources.Load<Font>("Fonts/NotoSansSC");
            if (UIFont == null)
                UIFont = Font.CreateDynamicFontFromOSFont(new[] { "Noto Sans CJK SC", "Microsoft YaHei", "sans-serif" }, 32);
            if (UIFont == null)
                UIFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Unlit/Transparent");
            if (shader == null)
                shader = Shader.Find("Unlit/Texture");

            TerrainMaterial = new Material(shader) { name = "TerrainAtlasMat", mainTexture = TileAtlas };
            DebrisMaterial = new Material(shader) { name = "DebrisAtlasMat", mainTexture = TileAtlas };
            LineMaterial = new Material(shader) { name = "TracerMat", mainTexture = White };
            LineMaterial.renderQueue = 3000;

            TileSprites = new Sprite[AtlasTiles];
            for (int i = 0; i < AtlasTiles; i++)
            {
                Rect r = new Rect(i * TilePixels, 0, TilePixels, TilePixels);
                TileSprites[i] = Sprite.Create(TileAtlas, r, new Vector2(0.5f, 0.5f), 16f, 0, SpriteMeshType.FullRect);
            }
        }

        private void BuildTileAtlas()
        {
            const int w = TilePixels * AtlasTiles;
            const int h = TilePixels;
            Color32[] px = new Color32[w * h];
            DrawDirt(px, 0);
            DrawConcrete(px, TilePixels);
            DrawSupport(px, TilePixels * 2);
            DrawIndestructible(px, TilePixels * 3);
            DrawWhite(px, TilePixels * 4);

            TileAtlas = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "M1TileAtlas",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            TileAtlas.SetPixels32(px);
            TileAtlas.Apply(false, false);
        }

        private void BuildUiTextures()
        {
            White = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "White2", filterMode = FilterMode.Point };
            White.SetPixels32(new[] { new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255) });
            White.Apply(false, false);

            UICircle = new Texture2D(64, 64, TextureFormat.RGBA32, false) { name = "UICircle", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            UIRing = new Texture2D(64, 64, TextureFormat.RGBA32, false) { name = "UIRing", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            Color32[] circle = new Color32[64 * 64];
            Color32[] ring = new Color32[64 * 64];
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    float dx = x - 31.5f;
                    float dy = y - 31.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    byte a = d <= 31f ? (byte)255 : (byte)0;
                    circle[y * 64 + x] = new Color32(255, 255, 255, a);
                    byte ra = d >= 25f && d <= 31f ? (byte)255 : (byte)0;
                    ring[y * 64 + x] = new Color32(255, 255, 255, ra);
                }
            }
            UICircle.SetPixels32(circle);
            UICircle.Apply(false, false);
            UIRing.SetPixels32(ring);
            UIRing.Apply(false, false);
        }

        private static void Fill(Color32[] px, int atlasW, int x0, int y0, int w, int h, Color32 c)
        {
            int tw = TilePixels;
            for (int y = y0; y < y0 + h; y++)
            {
                if (y < 0 || y >= 16) continue;
                int row = y * atlasW;
                for (int x = x0; x < x0 + w; x++)
                {
                    if (x < 0 || x >= tw) continue;
                    px[row + x] = c;
                }
            }
        }

        private static void Pixel(Color32[] px, int atlasW, int x, int y, Color32 c)
        {
            if (x < 0 || x >= 16 || y < 0 || y >= 16) return;
            px[y * atlasW + x] = c;
        }

        private static void DrawDirt(Color32[] px, int offset)
        {
            Color32 bg = new Color32(82, 56, 38, 255);
            Color32 top = new Color32(121, 81, 49, 255);
            Color32 dark = new Color32(53, 38, 30, 255);
            Color32 light = new Color32(146, 102, 62, 255);
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                    px[y * (TilePixels * AtlasTiles) + offset + x] = bg;
            for (int x = 0; x < 16; x++)
            {
                px[15 * (TilePixels * AtlasTiles) + offset + x] = top;
                px[14 * (TilePixels * AtlasTiles) + offset + x] = top;
            }
            for (int i = 0; i < 14; i++)
            {
                int x = (i * 7 + 3) % 16;
                int y = (i * 5 + 2) % 15;
                px[y * (TilePixels * AtlasTiles) + offset + x] = (i & 1) == 0 ? dark : light;
            }
            for (int x = 0; x < 16; x++)
                px[offset + x] = dark;
            for (int y = 0; y < 16; y++)
            {
                px[y * (TilePixels * AtlasTiles) + offset] = dark;
                px[y * (TilePixels * AtlasTiles) + offset + 15] = dark;
            }
        }

        private static void DrawConcrete(Color32[] px, int offset)
        {
            Color32 bg = new Color32(91, 95, 103, 255);
            Color32 dark = new Color32(54, 58, 66, 255);
            Color32 light = new Color32(132, 137, 143, 255);
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                    px[y * (TilePixels * AtlasTiles) + offset + x] = bg;
            for (int i = 0; i < 4; i++)
            {
                int y = 3 + i * 3;
                for (int x = 2; x < 14; x++)
                {
                    if ((x + i * 3) % 5 != 0)
                        px[y * (TilePixels * AtlasTiles) + offset + x] = dark;
                }
            }
            for (int x = 0; x < 16; x++)
            {
                px[15 * (TilePixels * AtlasTiles) + offset + x] = light;
                px[offset + x] = dark;
            }
            for (int y = 0; y < 16; y++)
            {
                px[y * (TilePixels * AtlasTiles) + offset] = dark;
                px[y * (TilePixels * AtlasTiles) + offset + 15] = dark;
            }
            for (int i = 0; i < 12; i++)
            {
                int x = (i * 11 + 5) % 16;
                int y = (i * 3 + 4) % 16;
                px[y * (TilePixels * AtlasTiles) + offset + x] = light;
            }
        }

        private static void DrawSupport(Color32[] px, int offset)
        {
            Color32 bg = new Color32(57, 59, 64, 255);
            Color32 stripe = new Color32(235, 187, 48, 255);
            Color32 dark = new Color32(30, 31, 35, 255);
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                    px[y * (TilePixels * AtlasTiles) + offset + x] = bg;
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    if (((x + y) % 8) < 4)
                        px[y * (TilePixels * AtlasTiles) + offset + x] = stripe;
                }
            }
            for (int x = 0; x < 16; x++)
            {
                px[offset + x] = dark;
                px[15 * (TilePixels * AtlasTiles) + offset + x] = dark;
            }
            for (int y = 0; y < 16; y++)
            {
                px[y * (TilePixels * AtlasTiles) + offset] = dark;
                px[y * (TilePixels * AtlasTiles) + offset + 15] = dark;
            }
        }

        private static void DrawIndestructible(Color32[] px, int offset)
        {
            Color32 bg = new Color32(45, 57, 70, 255);
            Color32 dark = new Color32(26, 32, 40, 255);
            Color32 light = new Color32(75, 91, 105, 255);
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                    px[y * (TilePixels * AtlasTiles) + offset + x] = bg;
            for (int y = 0; y < 16; y++)
            {
                px[y * (TilePixels * AtlasTiles) + offset] = dark;
                px[y * (TilePixels * AtlasTiles) + offset + 15] = dark;
            }
            for (int x = 0; x < 16; x++)
            {
                px[offset + x] = dark;
                px[15 * (TilePixels * AtlasTiles) + offset + x] = dark;
            }
            for (int ry = 0; ry < 3; ry++)
            {
                for (int rx = 0; rx < 2; rx++)
                {
                    int x = 4 + rx * 8;
                    int y = 4 + ry * 5;
                    px[y * (TilePixels * AtlasTiles) + offset + x] = light;
                    px[(y + 1) * (TilePixels * AtlasTiles) + offset + x] = dark;
                }
            }
        }

        private static void DrawWhite(Color32[] px, int offset)
        {
            Color32 white = new Color32(255, 255, 255, 255);
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                    px[y * (TilePixels * AtlasTiles) + offset + x] = white;
        }

        private static void DrawPlayer(Color32[] px, int w, int h)
        {
            Color32 outline = new Color32(14, 20, 28, 255);
            Color32 suit = new Color32(41, 99, 135, 255);
            Color32 suitLight = new Color32(67, 155, 181, 255);
            Color32 skin = new Color32(215, 159, 110, 255);
            Color32 visor = new Color32(255, 181, 56, 255);
            Color32 gun = new Color32(36, 39, 45, 255);
            Color32 boot = new Color32(24, 28, 35, 255);
            RectC(px, w, h, 3, 1, 6, 3, boot);
            RectC(px, w, h, 3, 4, 2, 8, outline);
            RectC(px, w, h, 7, 4, 2, 8, outline);
            RectC(px, w, h, 4, 4, 4, 8, suit);
            RectC(px, w, h, 4, 9, 4, 2, suitLight);
            RectC(px, w, h, 2, 7, 2, 5, skin);
            RectC(px, w, h, 8, 8, 3, 2, skin);
            RectC(px, w, h, 8, 8, 4, 2, outline);
            RectC(px, w, h, 8, 8, 4, 2, gun);
            RectC(px, w, h, 3, 13, 6, 5, outline);
            RectC(px, w, h, 4, 14, 4, 4, skin);
            RectC(px, w, h, 4, 17, 4, 2, suitLight);
            RectC(px, w, h, 6, 15, 2, 1, visor);
            RectC(px, w, h, 3, 12, 6, 1, outline);
            RectC(px, w, h, 9, 7, 3, 1, gun);
            RectC(px, w, h, 10, 6, 1, 3, gun);
        }

        private static void DrawDummy(Color32[] px, int w, int h)
        {
            Color32 o = new Color32(30, 22, 24, 255);
            Color32 body = new Color32(178, 72, 62, 255);
            Color32 light = new Color32(231, 119, 84, 255);
            Color32 pale = new Color32(238, 213, 181, 255);
            Color32 target = new Color32(255, 236, 190, 255);
            RectC(px, w, h, 3, 1, 2, 5, o);
            RectC(px, w, h, 7, 1, 2, 5, o);
            RectC(px, w, h, 2, 5, 8, 7, body);
            RectC(px, w, h, 3, 11, 6, 2, light);
            for (int i = 0; i < 8; i += 2)
                RectC(px, w, h, 3 + i % 4, 5, 1, 1, i % 4 == 0 ? target : o);
            RectC(px, w, h, 4, 1, 4, 1, o);
            RectC(px, w, h, 2, 14, 8, 3, o);
            RectC(px, w, h, 3, 15, 6, 2, pale);
            RectC(px, w, h, 3, 15, 1, 1, o);
            RectC(px, w, h, 8, 15, 1, 1, o);
            RectC(px, w, h, 0, 7, 2, 3, o);
            RectC(px, w, h, 10, 7, 2, 3, o);
        }

        private static void DrawPatrol(Color32[] px, int w, int h)
        {
            Color32 o = new Color32(14, 26, 22, 255);
            Color32 body = new Color32(45, 112, 87, 255);
            Color32 light = new Color32(91, 180, 128, 255);
            Color32 skin = new Color32(206, 165, 118, 255);
            Color32 eye = new Color32(255, 85, 64, 255);
            RectC(px, w, h, 3, 1, 2, 5, o);
            RectC(px, w, h, 7, 1, 2, 5, o);
            RectC(px, w, h, 2, 5, 8, 7, body);
            RectC(px, w, h, 3, 11, 6, 1, light);
            RectC(px, w, h, 2, 14, 8, 3, o);
            RectC(px, w, h, 3, 15, 6, 2, skin);
            RectC(px, w, h, 4, 15, 2, 1, eye);
            RectC(px, w, h, 0, 7, 2, 3, o);
            RectC(px, w, h, 10, 6, 2, 1, o);
            RectC(px, w, h, 11, 7, 1, 2, o);
        }

        private static void DrawArmored(Color32[] px, int w, int h)
        {
            Color32 o = new Color32(25, 27, 30, 255);
            Color32 steel = new Color32(86, 91, 98, 255);
            Color32 light = new Color32(130, 138, 144, 255);
            Color32 yellow = new Color32(224, 180, 44, 255);
            Color32 dark = new Color32(54, 58, 64, 255);
            Color32 red = new Color32(194, 65, 48, 255);
            RectC(px, w, h, 3, 1, 4, 4, o);
            RectC(px, w, h, 11, 1, 4, 4, o);
            RectC(px, w, h, 2, 5, 14, 10, o);
            RectC(px, w, h, 3, 6, 12, 8, steel);
            RectC(px, w, h, 3, 12, 12, 2, light);
            RectC(px, w, h, 1, 5, 16, 2, dark);
            for (int x = 3; x < 15; x += 4)
                RectC(px, w, h, x, 7, 2, 3, yellow);
            RectC(px, w, h, 5, 15, 8, 5, o);
            RectC(px, w, h, 6, 16, 6, 3, dark);
            RectC(px, w, h, 8, 17, 2, 1, red);
            RectC(px, w, h, 0, 8, 2, 3, o);
            RectC(px, w, h, 16, 8, 2, 3, o);
            RectC(px, w, h, 6, 3, 6, 1, yellow);
        }

        private static void DrawBarrel(Color32[] px, int w, int h)
        {
            Color32 o = new Color32(32, 24, 22, 255);
            Color32 red = new Color32(161, 57, 42, 255);
            Color32 light = new Color32(213, 89, 54, 255);
            Color32 yellow = new Color32(226, 182, 49, 255);
            RectC(px, w, h, 1, 1, 8, 12, o);
            RectC(px, w, h, 2, 2, 6, 10, red);
            RectC(px, w, h, 2, 10, 6, 2, light);
            RectC(px, w, h, 2, 5, 6, 2, yellow);
            RectC(px, w, h, 2, 3, 1, 7, light);
            RectC(px, w, h, 1, 0, 8, 1, o);
        }

        private static void DrawCrate(Color32[] px, int w, int h)
        {
            Color32 o = new Color32(38, 29, 22, 255);
            Color32 orange = new Color32(170, 106, 51, 255);
            Color32 light = new Color32(219, 153, 74, 255);
            Color32 dark = new Color32(111, 66, 34, 255);
            RectC(px, w, h, 1, 1, 10, 10, o);
            RectC(px, w, h, 2, 2, 8, 8, orange);
            for (int i = 0; i < 8; i++)
            {
                RectC(px, w, h, 2 + i, 2 + i, 1, 1, dark);
                RectC(px, w, h, 9 - i, 2 + i, 1, 1, dark);
            }
            RectC(px, w, h, 2, 9, 8, 1, light);
            RectC(px, w, h, 4, 5, 4, 2, dark);
        }

        private static void DrawGrenade(Color32[] px, int w, int h)
        {
            Color32 o = new Color32(20, 28, 20, 255);
            Color32 green = new Color32(87, 120, 60, 255);
            Color32 light = new Color32(156, 183, 86, 255);
            Color32 metal = new Color32(175, 173, 156, 255);
            RectC(px, w, h, 1, 1, 4, 4, o);
            RectC(px, w, h, 2, 2, 2, 2, green);
            RectC(px, w, h, 2, 3, 1, 1, light);
            RectC(px, w, h, 4, 3, 1, 1, metal);
            RectC(px, w, h, 4, 4, 1, 1, metal);
            PixelC(px, w, h, 5, 4, metal);
        }

        private static void DrawGoal(Color32[] px, int w, int h)
        {
            Color32 o = new Color32(15, 28, 24, 255);
            Color32 green = new Color32(63, 154, 112, 255);
            Color32 light = new Color32(105, 223, 153, 255);
            Color32 yellow = new Color32(232, 197, 78, 255);
            RectC(px, w, h, 2, 1, 12, 24, o);
            RectC(px, w, h, 3, 2, 10, 22, green);
            RectC(px, w, h, 4, 4, 8, 8, o);
            RectC(px, w, h, 5, 5, 6, 6, light);
            RectC(px, w, h, 6, 6, 4, 2, green);
            RectC(px, w, h, 6, 9, 4, 2, green);
            RectC(px, w, h, 5, 14, 6, 1, yellow);
            RectC(px, w, h, 5, 17, 6, 1, yellow);
            RectC(px, w, h, 6, 20, 4, 2, yellow);
            RectC(px, w, h, 1, 0, 14, 1, o);
        }

        private static Sprite MakeSprite(int w, int h, System.Action<Color32[], int, int> draw)
        {
            Color32[] px = new Color32[w * h];
            draw(px, w, h);
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "sprite_" + w + "x" + h,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 16f, 0, SpriteMeshType.FullRect);
        }

        private static Sprite MakeRing(int size, float thickness)
        {
            Color32[] px = new Color32[size * size];
            float c = (size - 1) * 0.5f;
            float outer = size * 0.5f - 1f;
            float inner = outer - thickness;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - c;
                    float dy = y - c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d <= outer && d >= inner)
                        px[y * size + x] = new Color32(255, 255, 255, 180);
                }
            }
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 16f, 0, SpriteMeshType.FullRect);
        }

        private static Sprite MakeFlash(int size)
        {
            Color32[] px = new Color32[size * size];
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - c) / c;
                    float dy = (y - c) / c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d);
                    a *= a;
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 16f, 0, SpriteMeshType.FullRect);
        }

        private static Sprite MakeSlash(int size)
        {
            Color32[] px = new Color32[size * size];
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - c;
                    float dy = y - c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                    bool arc = d > c * 0.55f && d < c * 0.95f && angle > -70f && angle < 70f;
                    if (arc)
                    {
                        float a = Mathf.Clamp01(1f - Mathf.Abs(angle) / 75f);
                        px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 210f));
                    }
                }
            }
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 16f, 0, SpriteMeshType.FullRect);
        }

        private static void RectC(Color32[] px, int w, int h, int x0, int y0, int rw, int rh, Color32 c)
        {
            for (int y = y0; y < y0 + rh; y++)
            {
                if (y < 0 || y >= h) continue;
                for (int x = x0; x < x0 + rw; x++)
                {
                    if (x < 0 || x >= w) continue;
                    px[y * w + x] = c;
                }
            }
        }

        private static void PixelC(Color32[] px, int w, int h, int x, int y, Color32 c)
        {
            if (x >= 0 && x < w && y >= 0 && y < h)
                px[y * w + x] = c;
        }
    }
}
