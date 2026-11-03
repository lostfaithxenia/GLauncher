using GLauncher.Core.Games;

namespace GLauncher.Core.Providers;

public sealed record CoverRequest(GameId GameId, string Title, GamePlatform Platform, string? StyleHint);

/// <summary>A generated cover waiting in a temp file. It is NOT applied until the user accepts it.</summary>
public sealed record GeneratedCover(string TempFilePath, int WidthPx, int HeightPx)
{
    public bool IsNineBySixteen => CoverSpec.IsNineBySixteen(WidthPx, HeightPx);
}

public interface IArtworkProvider
{
    ProviderInfo Info { get; }

    /// <summary>
    /// Cancelling the token cancels the request. Implementations must not touch the game's
    /// current cover; replacement happens only after the user accepts (IGameLibraryStore).
    /// </summary>
    Task<ProviderResult<GeneratedCover>> GenerateCoverAsync(CoverRequest request, CancellationToken cancellationToken);
}

public sealed record GameDescription(string Text, string? SourceNote);

public interface IMetadataProvider
{
    ProviderInfo Info { get; }

    Task<ProviderResult<GameDescription>> GetDescriptionAsync(
        string title, GamePlatform? platform, CancellationToken cancellationToken);
}
