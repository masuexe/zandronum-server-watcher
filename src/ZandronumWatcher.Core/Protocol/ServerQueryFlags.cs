namespace ZandronumWatcher.Core.Protocol;

/// <summary>Fields a client may ask a Zandronum server to return (SQF_*).</summary>
[Flags]
public enum ServerQueryFlags : uint
{
    None = 0,
    Name = 0x00000001,
    Url = 0x00000002,
    Email = 0x00000004,
    MapName = 0x00000008,
    MaxClients = 0x00000010,
    MaxPlayers = 0x00000020,
    Pwads = 0x00000040,
    GameType = 0x00000080,
    GameName = 0x00000100,
    Iwad = 0x00000200,
    ForcePassword = 0x00000400,
    ForceJoinPassword = 0x00000800,
    GameSkill = 0x00001000,
    BotSkill = 0x00002000,
    DmFlags = 0x00004000,
    Limits = 0x00010000,
    TeamDamage = 0x00020000,
    TeamScores = 0x00040000,
    NumPlayers = 0x00080000,
    PlayerData = 0x00100000,
    TeamInfoNumber = 0x00200000,
    TeamInfoName = 0x00400000,
    TeamInfoColor = 0x00800000,
    TeamInfoScore = 0x01000000,
    TestingServer = 0x02000000,
    DataMd5Sum = 0x04000000,
    AllDmFlags = 0x08000000,
    SecuritySettings = 0x10000000,
    OptionalWads = 0x20000000,
    Deh = 0x40000000,
    ExtendedInfo = 0x80000000,

    /// <summary>The field set this app requests: enough for an overlay card.</summary>
    Overlay = Name | MapName | MaxClients | MaxPlayers | Pwads | GameType | GameName | Iwad
              | NumPlayers | PlayerData | ExtendedInfo,
}

/// <summary>Extended (second word) response flags (SQF2_*).</summary>
[Flags]
public enum ServerQueryFlags2 : uint
{
    None = 0,
    PwadHashes = 0x00000001,
    Country = 0x00000002,
    GameModeName = 0x00000004,
    GameModeShortName = 0x00000008,
}
