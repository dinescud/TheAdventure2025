using Silk.NET.Maths;
using Silk.NET.SDL;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using TheAdventure.Models;
using Point = Silk.NET.SDL.Point;
namespace TheAdventure;

public unsafe class GameRenderer
{
    private Sdl _sdl;
    private Renderer* _renderer;
    private GameWindow _window;
    private Camera _camera;
    private Dictionary<int, IntPtr> _texturePointers = new();
    private Dictionary<int, TextureData> _textureData = new();
    private int _textureId;

    private IntPtr _font = IntPtr.Zero;
    private int _heartTex;
    private int _heartW;
    private int _heartH;

    [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl)]
    private static extern int TTF_Init();
    [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr TTF_OpenFont(string file, int ptsize);
    [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr TTF_RenderText_Blended(IntPtr font, string text, SDL_Color fg);
    [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl)]
    private static extern void TTF_CloseFont(IntPtr font);
    [DllImport("SDL2_ttf", CallingConvention = CallingConvention.Cdecl)]
    private static extern void TTF_Quit();

    [StructLayout(LayoutKind.Sequential)]
    public struct SDL_Color { public byte r, g, b, a; }

    public GameRenderer(Sdl sdl, GameWindow window)
    {
        _sdl = sdl;
        _renderer = (Renderer*)window.CreateRenderer();
        _sdl.SetRenderDrawBlendMode(_renderer, BlendMode.Blend);
        _window = window;
        var size = window.Size;
        _camera = new Camera(size.Width, size.Height);

        if (TTF_Init() != 0)
            throw new Exception("SDL2_ttf initialization failed.");
        _font = TTF_OpenFont("Assets/arial.ttf", 16);
        if (_font == IntPtr.Zero)
            throw new Exception("Failed to open font.");

        _heartTex = LoadTexture("Assets/heart.png", out var td);
        _heartW = td.Width;
        _heartH = td.Height;
    }

    public void SetWorldBounds(Rectangle<int> bounds)
    {
        _camera.SetWorldBounds(bounds);
    }

    public void CameraLookAt(int x, int y)
    {
        // _camera.LookAt(x, y);
    }

    public int LoadTexture(string fileName, out TextureData textureInfo)
    {
        using (var fStream = new FileStream(fileName, FileMode.Open))
        {
            var image = Image.Load<Rgba32>(fStream);
            textureInfo = new TextureData()
            {
                Width = image.Width,
                Height = image.Height
            };
            var imageRAWData = new byte[textureInfo.Width * textureInfo.Height * 4];
            image.CopyPixelDataTo(imageRAWData.AsSpan());
            fixed (byte* data = imageRAWData)
            {
                var imageSurface = _sdl.CreateRGBSurfaceWithFormatFrom(data, textureInfo.Width,
                    textureInfo.Height, 8, textureInfo.Width * 4, (uint)PixelFormatEnum.Rgba32);
                if (imageSurface == null)
                {
                    throw new Exception("Failed to create surface from image data.");
                }
                
                var imageTexture = _sdl.CreateTextureFromSurface(_renderer, imageSurface);
                if (imageTexture == null)
                {
                    _sdl.FreeSurface(imageSurface);
                    throw new Exception("Failed to create texture from surface.");
                }
                
                _sdl.FreeSurface(imageSurface);
                
                _textureData[_textureId] = textureInfo;
                _texturePointers[_textureId] = (IntPtr)imageTexture;
            }
        }

        return _textureId++;
    }

    public void RenderTexture(int textureId, Rectangle<int> src, Rectangle<int> dst,
        RendererFlip flip = RendererFlip.None, double angle = 0.0, Point center = default)
    {
        if (_texturePointers.TryGetValue(textureId, out var imageTexture))
        {
            var translatedDst = _camera.ToScreenCoordinates(dst);
            _sdl.RenderCopyEx(_renderer, (Texture*)imageTexture, in src,
                in translatedDst,
                angle,
                in center, flip);
        }
    }

    public Vector2D<int> ToWorldCoordinates(int x, int y)
    {
        return _camera.ToWorldCoordinates(new Vector2D<int>(x, y));
    }

    public void SetDrawColor(byte r, byte g, byte b, byte a)
    {
        _sdl.SetRenderDrawColor(_renderer, r, g, b, a);
    }
    
    public void DrawUI(int lives, int bombsAvoided)
    {
        const int spacing = 4;
        const int margin  = 10;
        var (w, _) = _window.Size;

        int displayW = _heartW / 4;
        int displayH = _heartH / 4;

        _sdl.SetRenderDrawBlendMode(_renderer, BlendMode.Blend);
        for (int i = 0; i < lives; i++)
        {
            int x = w - margin - (i + 1) * (displayW + spacing);
            int y = margin;

            RenderTexture(_heartTex,
                new Rectangle<int>(0, 0, _heartW, _heartH),
                new Rectangle<int>(x, y, displayW, displayH));
        }
        
        string txt = $"Bombs avoided: {bombsAvoided}";
        SDL_Color c = new SDL_Color { r = 255, g = 255, b = 255, a = 255 };
        IntPtr surf = TTF_RenderText_Blended(_font, txt, c);
        if (surf != IntPtr.Zero)
        {
            Texture* tex = _sdl.CreateTextureFromSurface(_renderer, (Surface*)surf);
            _sdl.FreeSurface((Surface*)surf);
            uint fmt = 0; int acc = 0; int tw = 0, th = 0;
            _sdl.QueryTexture((Texture*)tex, ref fmt, ref acc, ref tw, ref th);
            int tx = w - margin - tw;
            int ty = margin + displayH + 5;
            var srcRect = new Rectangle<int>(0, 0, tw, th);
            var dstRect = new Rectangle<int>(tx, ty, tw, th);
            _sdl.RenderCopy(_renderer, (Texture*)tex, in srcRect, in dstRect);
            _sdl.DestroyTexture((Texture*)tex);
        }
    }
    
    public void ClearScreen()
    {
        _sdl.RenderClear(_renderer);
    }

    public void PresentFrame()
    {
        _sdl.RenderPresent(_renderer);
    }
}
