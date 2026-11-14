using Godot;
using Net.Codecrete.QrCodeGenerator;

namespace Ares.Systems.Controllers;

/// <summary>Builds QR codes for the phone join URL without touching the relay.</summary>
public static class ControllerQr
{
    public const int DefaultScale = 6;
    public const int DefaultBorder = 4;

    public static byte[] CreatePng(string text, int scale = DefaultScale, int border = DefaultBorder)
    {
        var qr = Encode(text);
        if (qr == null) return null;

        Image image = Render(qr, scale, border);
        return image?.SavePngToBuffer();
    }

    public static ImageTexture CreateTexture(string text, int scale = DefaultScale, int border = DefaultBorder)
    {
        var qr = Encode(text);
        if (qr == null) return null;

        Image image = Render(qr, scale, border);
        return image == null ? null : ImageTexture.CreateFromImage(image);
    }

    private static QrCode Encode(string text) =>
        string.IsNullOrWhiteSpace(text) ? null : QrCode.EncodeText(text, QrCode.Ecc.Medium);

    private static Image Render(QrCode qr, int scale, int border)
    {
        scale = Mathf.Max(1, scale);
        border = Mathf.Max(0, border);

        int modules = qr.Size + border * 2;
        int size = modules * scale;
        var pixels = new byte[size * size * 4];

        for (int y = 0; y < size; y++)
        {
            int moduleY = y / scale - border;
            for (int x = 0; x < size; x++)
            {
                int moduleX = x / scale - border;
                bool dark = moduleX >= 0 && moduleY >= 0 && moduleX < qr.Size && moduleY < qr.Size &&
                            qr.GetModule(moduleX, moduleY);
                byte value = dark ? (byte)0 : (byte)255;
                int index = (y * size + x) * 4;
                pixels[index] = value;
                pixels[index + 1] = value;
                pixels[index + 2] = value;
                pixels[index + 3] = 255;
            }
        }

        return Image.CreateFromData(size, size, false, Image.Format.Rgba8, pixels);
    }
}
