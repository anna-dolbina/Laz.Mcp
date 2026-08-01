using System.ComponentModel;
using Laz;
using Laz.Extensions.Mouse;
using ModelContextProtocol.Server;

namespace Laz.Mcp;

internal sealed class MouseTools(LazbotGate gate)
{
    [McpServerTool(Name = "mouse_get_position")]
    [Description("Returns the current cursor position.")]
    public Point GetPosition()
        => gate.Run(bot => bot.Mouse.GetPosition());

    [McpServerTool(Name = "mouse_jump_to")]
    [Description("Instantly moves the cursor to a position (teleport, no intermediate motion).")]
    public void JumpTo(
        [Description("Target X coordinate")] int x,
        [Description("Target Y coordinate")] int y)
        => gate.Run(bot => bot.Mouse.JumpTo(new Point(x, y)));

    [McpServerTool(Name = "mouse_move_smooth")]
    [Description("Moves the cursor from its current position (or an explicit start) to a target over time, along a linear or bezier path with an easing curve - looks like natural human movement instead of a teleport.")]
    public void MoveSmooth(
        [Description("Target X coordinate")] int targetX,
        [Description("Target Y coordinate")] int targetY,
        [Description("Optional start X coordinate; defaults to the current cursor position")] int? startX = null,
        [Description("Optional start Y coordinate; defaults to the current cursor position")] int? startY = null,
        [Description("Duration of the movement in milliseconds; defaults to 1500")] int? durationMs = null,
        [Description("Path shape: Linear or Bezier; defaults to Linear")] string path = "Linear",
        [Description("Timing easing curve; defaults to Linear")] string easing = "Linear")
    {
        var moveFunction = EnumParsing.Parse<MouseMoveFunction>(path, nameof(path));
        var easingFunction = EnumParsing.Parse<MoveEasingFunction>(easing, nameof(easing));
        var duration = durationMs is int ms ? TimeSpan.FromMilliseconds(ms) : (TimeSpan?)null;

        gate.Run(bot =>
        {
            var start = startX is int sx && startY is int sy
                ? new Point(sx, sy)
                : bot.Mouse.GetPosition();
            var target = new Point(targetX, targetY);
            bot.Mouse.MoveTo(start, target, duration, moveFunction, easingFunction);
        });
    }

    [McpServerTool(Name = "mouse_press")]
    [Description("Presses and holds a button down until mouse_release.")]
    public void Press(
        [Description("Button to press: Primary, Middle, or Secondary; defaults to Primary")] string button = "Primary")
    {
        var mouseButton = EnumParsing.Parse<MouseButton>(button, nameof(button));
        gate.Run(bot => bot.Mouse.Press(mouseButton));
    }

    [McpServerTool(Name = "mouse_release")]
    [Description("Releases a previously pressed button.")]
    public void Release(
        [Description("Button to release: Primary, Middle, or Secondary; defaults to Primary")] string button = "Primary")
    {
        var mouseButton = EnumParsing.Parse<MouseButton>(button, nameof(button));
        gate.Run(bot => bot.Mouse.Release(mouseButton));
    }

    [McpServerTool(Name = "mouse_click")]
    [Description("Presses and releases a button once.")]
    public void Click(
        [Description("Button to click: Primary, Middle, or Secondary; defaults to Primary")] string button = "Primary")
    {
        var mouseButton = EnumParsing.Parse<MouseButton>(button, nameof(button));
        gate.Run(bot => bot.Mouse.Click(mouseButton));
    }

    [McpServerTool(Name = "mouse_double_click")]
    [Description("Two clicks in quick succession.")]
    public void DoubleClick(
        [Description("Button to double-click: Primary, Middle, or Secondary; defaults to Primary")] string button = "Primary")
    {
        var mouseButton = EnumParsing.Parse<MouseButton>(button, nameof(button));
        gate.Run(bot => bot.Mouse.DoubleClick(mouseButton));
    }

    [McpServerTool(Name = "mouse_scroll")]
    [Description("Spins the scroll wheel. Positive scrolls up, negative scrolls down.")]
    public void Scroll(
        [Description("Number of notches to scroll; positive scrolls up, negative scrolls down")] int notches)
        => gate.Run(bot => bot.Mouse.Scroll(notches));

    [McpServerTool(Name = "mouse_drag_and_drop")]
    [Description("Presses at the start position, moves naturally to the target, and releases. Optional dragPreamble adds a short downward nudge first, which some apps need to recognize a drag gesture.")]
    public void DragAndDrop(
        [Description("Start X coordinate")] int startX,
        [Description("Start Y coordinate")] int startY,
        [Description("Target X coordinate")] int targetX,
        [Description("Target Y coordinate")] int targetY,
        [Description("Duration of the movement in milliseconds; defaults to 1500")] int? durationMs = null,
        [Description("Path shape: Linear or Bezier; defaults to Linear")] string path = "Linear",
        [Description("Timing easing curve; defaults to Linear")] string easing = "Linear",
        [Description("If true, adds a short downward nudge before the drag; defaults to false")] bool dragPreamble = false)
    {
        var moveFunction = EnumParsing.Parse<MouseMoveFunction>(path, nameof(path));
        var easingFunction = EnumParsing.Parse<MoveEasingFunction>(easing, nameof(easing));
        var duration = durationMs is int ms ? TimeSpan.FromMilliseconds(ms) : (TimeSpan?)null;

        gate.Run(bot =>
        {
            var start = new Point(startX, startY);
            var target = new Point(targetX, targetY);
            bot.Mouse.DragAndDrop(start, target, duration, moveFunction, easingFunction, dragPreamble);
        });
    }
}
