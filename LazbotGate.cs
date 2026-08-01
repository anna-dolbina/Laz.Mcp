using System.Threading;
using Laz;

namespace Laz.Mcp;

/// <summary>
/// Holds the single process-lifetime <see cref="Lazbot"/> instance and serializes all calls
/// into it, since it manipulates global OS input/cursor state and interleaved calls (e.g. a
/// click landing mid-drag) would be a real bug, not just a theoretical race.
/// </summary>
internal sealed class LazbotGate
{
    private readonly Lazbot _lazbot = new();
    private readonly Lock _gate = new();

    public T Run<T>(Func<Lazbot, T> action)
    {
        lock (_gate)
        {
            return action(_lazbot);
        }
    }

    public void Run(Action<Lazbot> action)
    {
        lock (_gate)
        {
            action(_lazbot);
        }
    }
}
