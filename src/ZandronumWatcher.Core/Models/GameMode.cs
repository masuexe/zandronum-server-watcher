namespace ZandronumWatcher.Core.Models;

/// <summary>Zandronum game modes as reported in the launcher response.</summary>
public enum GameMode
{
    Cooperative = 0,
    Survival = 1,
    Invasion = 2,
    Deathmatch = 3,
    Teamplay = 4,
    Duel = 5,
    Terminator = 6,
    LastManStanding = 7,
    TeamLms = 8,
    Possession = 9,
    TeamPossession = 10,
    TeamGame = 11,
    Ctf = 12,
    OneFlagCtf = 13,
    Skulltag = 14,
    Domination = 15,
}

public static class GameModeExtensions
{
    private static readonly HashSet<GameMode> TeamModes =
    [
        GameMode.Teamplay, GameMode.TeamLms, GameMode.TeamPossession,
        GameMode.TeamGame, GameMode.Ctf, GameMode.OneFlagCtf,
        GameMode.Skulltag, GameMode.Domination,
    ];

    public static bool IsTeamMode(this GameMode mode) => TeamModes.Contains(mode);

    public static string ToDisplayName(this GameMode mode) => mode switch
    {
        GameMode.Cooperative => "Cooperative",
        GameMode.Survival => "Survival",
        GameMode.Invasion => "Invasion",
        GameMode.Deathmatch => "Deathmatch",
        GameMode.Teamplay => "Teamplay",
        GameMode.Duel => "Duel",
        GameMode.Terminator => "Terminator",
        GameMode.LastManStanding => "Last Man Standing",
        GameMode.TeamLms => "Team LMS",
        GameMode.Possession => "Possession",
        GameMode.TeamPossession => "Team Possession",
        GameMode.TeamGame => "Team Game",
        GameMode.Ctf => "Capture the Flag",
        GameMode.OneFlagCtf => "One Flag CTF",
        GameMode.Skulltag => "Skulltag",
        GameMode.Domination => "Domination",
        _ => mode.ToString(),
    };
}
