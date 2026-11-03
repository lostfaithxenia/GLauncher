using GLauncher.Core.Games;
using GLauncher.Core.Providers;
using GLauncher.Core.Sessions;

namespace GLauncher.Core.Tests;

public class ContractTests
{
    [Theory]
    [InlineData(1080, 1920, true)]
    [InlineData(900, 1600, true)]
    [InlineData(1024, 1820, true)]   // within 1% tolerance
    [InlineData(1000, 1000, false)]
    [InlineData(1920, 1080, false)]  // landscape
    [InlineData(0, 1920, false)]
    public void Cover_spec_recognises_nine_by_sixteen(int w, int h, bool expected) =>
        Assert.Equal(expected, CoverSpec.IsNineBySixteen(w, h));

    [Fact]
    public void Process_identity_is_not_just_a_pid()
    {
        var t = DateTimeOffset.UtcNow;
        Assert.NotEqual(new ProcessIdentity(4242, t), new ProcessIdentity(4242, t.AddSeconds(5)));
        Assert.Equal(new ProcessIdentity(4242, t), new ProcessIdentity(4242, t));
    }

    [Fact]
    public void Tracking_uncertain_counts_as_possibly_alive_but_exited_does_not()
    {
        SessionSnapshot Make(SessionState s) => new(
            SessionId.New(), GameId.New(), s, null, Array.Empty<ProcessIdentity>(),
            TrackingQuality.RootOnly, TerminationCapability.Unsupported, DateTimeOffset.UtcNow);

        Assert.True(Make(SessionState.TrackingUncertain).IsPossiblyAlive);
        Assert.True(Make(SessionState.Launching).IsPossiblyAlive);
        Assert.False(Make(SessionState.Exited).IsPossiblyAlive);
        Assert.False(Make(SessionState.LaunchFailed).IsPossiblyAlive);
    }

    [Fact]
    public void Provider_result_only_succeeds_when_it_really_did()
    {
        var info = new ProviderInfo("demo", "Demo", ProviderKind.Demo, false, null);
        Assert.True(ProviderResult<string>.Success(info, "x").IsSuccess);
        Assert.False(ProviderResult<string>.NotConfigured(info, "no key").IsSuccess);
        Assert.False(ProviderResult<string>.Cancelled(info).IsSuccess);
        Assert.False(ProviderResult<string>.Failure(info, "boom").IsSuccess);
        Assert.Null(ProviderResult<string>.Failure(info, "boom").Value);
    }

    [Fact]
    public void Generated_cover_reports_whether_it_is_nine_by_sixteen()
    {
        Assert.True(new GeneratedCover("a.png", 1080, 1920).IsNineBySixteen);
        Assert.False(new GeneratedCover("a.png", 1024, 1024).IsNineBySixteen);
    }

    [Fact]
    public void Core_has_no_windows_or_ui_framework_references()
    {
        var refs = typeof(GameId).Assembly.GetReferencedAssemblies()
            .Select(a => a.Name ?? "").ToList();
        string[] banned = { "Microsoft.UI", "Microsoft.Windows", "WindowsBase", "PresentationFramework", "System.Windows", "WinRT" };
        Assert.DoesNotContain(refs, r => banned.Any(b => r.StartsWith(b, StringComparison.OrdinalIgnoreCase)));
    }
}
