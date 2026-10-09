using StarPie.Plugin;
namespace InteractionFixture;
public sealed class RecorderPlugin : IStarPiePlugin, IInteractionContribution
{
    private IDisposable? _token;
    private int _count;
    public int Count => Volatile.Read(ref _count);
    public string LastReason { get; private set; } = "";
    public InteractionDescriptor Descriptor => new() { Id = "record" };
    public void Initialize(IPluginContext context)
    {
        _token = ((IInteractionPluginContext)context).Interactions.Register(this);
        if (context.Settings.GetBool("failInit")) throw new InvalidOperationException("sentinel Initialize failure");
    }
    public ValueTask OnInteractionAsync(InteractionEvent input, CancellationToken cancellationToken)
    { LastReason = input.Reason; Interlocked.Increment(ref _count); return ValueTask.CompletedTask; }
    public void Shutdown() { _token?.Dispose(); _token = null; }
}
