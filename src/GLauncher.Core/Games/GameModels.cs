using GLauncher.Core.Providers;

namespace GLauncher.Core.Games;

public readonly record struct GameId(Guid Value)
{
    public static GameId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("N");
}

public enum GamePlatform { Ps2, Ps3, Xbox360, XboxSeries, Pc, Mobile, Other }

/// <summary>Covers are 9:16 portrait. This is the single definition of that rule.</summary>
public static class CoverSpec
{
    public const int AspectWidth = 9;
    public const int AspectHeight = 16;
    public const double AspectRatio = (double)AspectWidth / AspectHeight;

    /// <summary>True when width:height is 9:16 within a relative tolerance (default 1%).</summary>
    public static bool IsNineBySixteen(int widthPx, int heightPx, double relativeTolerance = 0.01)
    {
        if (widthPx <= 0 || heightPx <= 0) return false;
        double ratio = (double)widthPx / heightPx;
        return Math.Abs(ratio - AspectRatio) <= AspectRatio * relativeTolerance;
    }
}

/// <summary>How to start a game. Emulator/bootstrap handling is an adapter concern (#2).</summary>
public sealed record LaunchTarget(
    string ExecutablePath,
    string? Arguments = null,
    string? WorkingDirectory = null);

public sealed record GameEntry(
    GameId Id,
    string Title,
    GamePlatform Platform,
    LaunchTarget Launch,
    DateTimeOffset AddedAt,
    string? Description = null,
    string? CoverPath = null,
    DateTimeOffset? LastPlayedAt = null);

/// <summary>Recommendation drawn only from games the user owns. Launchable.</summary>
public sealed record OwnedRecommendation(GameId GameId, double Score, IReadOnlyList<string> Reasons);

public enum OfferKind { PaidPurchase, FreeDownload }

/// <summary>
/// A game the user does NOT own. Deliberately a different type from OwnedRecommendation and
/// carries no play action, so ownership and purchase state cannot be mixed up.
/// </summary>
public sealed record DiscoverRecommendation(
    string Title,
    GamePlatform Platform,
    OfferKind Offer,
    Uri? SourceUrl,
    ProviderInfo Source,
    string? Reason);

public interface IOwnedGameRecommender
{
    /// <summary>Transparent local ranking from library metadata only. Must document its rules.</summary>
    IReadOnlyList<OwnedRecommendation> Recommend(IReadOnlyList<GameEntry> library, int maxCount);
}

public interface IDiscoverRecommender
{
    Task<ProviderResult<IReadOnlyList<DiscoverRecommendation>>> RecommendAsync(
        IReadOnlyList<GameEntry> library, int maxCount, CancellationToken cancellationToken);
}
