namespace GLauncher.Core.Providers;

/// <summary>Whether a provider talks to a real service or produces fixture/demo data.</summary>
public enum ProviderKind { Real, Demo }

/// <summary>Where a piece of data came from. UI must label Demo data as demo.</summary>
public enum DataProvenance { Real, Demo, UserCreated, LauncherGenerated }

public sealed record ProviderInfo(
    string Id,
    string DisplayName,
    ProviderKind Kind,
    bool RequiresCredentials,
    string? CostNote);

public enum ProviderStatus { Succeeded, NotConfigured, Cancelled, Failed }

/// <summary>
/// Honest provider outcome. A provider that is not configured, cancelled or failed never
/// returns a fabricated value; Value is only meaningful when Status is Succeeded.
/// </summary>
public sealed record ProviderResult<T>(
    ProviderStatus Status,
    ProviderInfo Provider,
    T? Value = default,
    string? Message = null)
{
    public bool IsSuccess => Status == ProviderStatus.Succeeded;

    public static ProviderResult<T> Success(ProviderInfo provider, T value) =>
        new(ProviderStatus.Succeeded, provider, value);

    public static ProviderResult<T> NotConfigured(ProviderInfo provider, string message) =>
        new(ProviderStatus.NotConfigured, provider, default, message);

    public static ProviderResult<T> Cancelled(ProviderInfo provider) =>
        new(ProviderStatus.Cancelled, provider, default, "Cancelled.");

    public static ProviderResult<T> Failure(ProviderInfo provider, string message) =>
        new(ProviderStatus.Failed, provider, default, message);
}
