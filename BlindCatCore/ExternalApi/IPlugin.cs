namespace BlindCatCore.ExternalApi;

public interface IPlugin
{
    string Name { get; }
    string Description { get; }
    string Author { get; }

    Task EntryPoint();
    Task OnActivated(IBlindCatApi api, CancellationToken cancellationToken);
}