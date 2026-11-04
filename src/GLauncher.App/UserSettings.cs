namespace GLauncher.App;

internal sealed record UserRole(string Name, string Color);
internal sealed record UserSettings
{
    public string ProfileName { get; init; } = "SYN";
    public string? ProfilePicture { get; init; }
    public UserRole[] Roles { get; init; } = [new("Mythical", "#C9A7FF"), new("SYN", "#76DEEB")];
    public bool LightMode { get; init; }
    public bool AnimationEnabled { get; init; } = true;
    public double Glow { get; init; } = .7;
    public double MotionSpeed { get; init; } = 1;
    public string? BackgroundPath { get; init; }
    public bool IntroEnabled { get; init; } = true;
    public bool EmergencyEnabled { get; init; } = true;
    public double EmergencyHoldSeconds { get; init; } = 1;
}
