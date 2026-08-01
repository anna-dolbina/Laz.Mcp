using System.ComponentModel;
using Laz;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Laz.Mcp;

internal sealed class ScreenTools(LazbotGate gate)
{
    [McpServerTool(Name = "screen_capture")]
    [Description("Captures a rectangular region and returns it as a PNG image.")]
    public ImageContentBlock Capture(
        [Description("Left edge of the capture region")] int x,
        [Description("Top edge of the capture region")] int y,
        [Description("Width of the capture region in pixels")] int width,
        [Description("Height of the capture region in pixels")] int height)
    {
        var (data, capturedWidth, capturedHeight) = gate.Run(bot =>
            bot.Screen.Capture(new Point(x, y), width, height));

        using var image = Image.LoadPixelData<Bgra32>(data, capturedWidth, capturedHeight);
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);

        return ImageContentBlock.FromBytes(stream.ToArray(), "image/png");
    }

    [McpServerTool(Name = "screen_get_color_at")]
    [Description("Reads the color of a single pixel.")]
    public PixelColor GetColorAt(
        [Description("X coordinate of the pixel")] int x,
        [Description("Y coordinate of the pixel")] int y)
    {
        var (r, g, b, a) = gate.Run(bot => bot.Screen.GetColorAt(new Point(x, y)));
        return new PixelColor(r, g, b, a);
    }
}

internal readonly record struct PixelColor(byte R, byte G, byte B, byte A);
