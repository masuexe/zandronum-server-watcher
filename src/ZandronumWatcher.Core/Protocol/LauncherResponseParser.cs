using ZandronumWatcher.Core.Models;

namespace ZandronumWatcher.Core.Protocol;

/// <summary>Parses a decoded launcher (SQF) response from a Zandronum server.</summary>
public static class LauncherResponseParser
{
    public const uint ChallengeAccepted = 5660023;
    public const uint ChallengeDenied = 5660024;
    public const uint ChallengeBanned = 5660025;

    public static ServerInfo Parse(ReadOnlySpan<byte> payload, ServerEndpoint endpoint, TimeSpan latency)
    {
        var reader = new PacketReader(payload);
        var responseCode = reader.ReadUInt32();

        if (responseCode != ChallengeAccepted)
        {
            var reason = responseCode switch
            {
                ChallengeDenied => "server is rate-limiting queries (query ignored)",
                ChallengeBanned => "this IP is banned by the server",
                _ => $"unexpected response code {responseCode}",
            };
            throw new LauncherException(reason, responseCode == ChallengeDenied);
        }

        _ = reader.ReadUInt32(); // server time
        var version = reader.ReadString();
        var flags = (ServerQueryFlags)reader.ReadUInt32();

        string name = "", url = "", mapName = "", gameName = "", iwad = "", country = "";
        string gameModeName = "";
        var gameMode = GameMode.Cooperative;
        int maxClients = 0, maxPlayers = 0;
        var pwads = new List<string>();
        var players = new List<PlayerInfo>();
        var requiresPassword = false;
        var numPlayers = 0;

        if (flags.HasFlag(ServerQueryFlags.Name))
        {
            name = reader.ReadString();
        }

        if (flags.HasFlag(ServerQueryFlags.Url))
        {
            url = reader.ReadString();
        }

        if (flags.HasFlag(ServerQueryFlags.Email))
        {
            _ = reader.ReadString();
        }

        if (flags.HasFlag(ServerQueryFlags.MapName))
        {
            mapName = reader.ReadString();
        }

        if (flags.HasFlag(ServerQueryFlags.MaxClients))
        {
            maxClients = reader.ReadByte();
        }

        if (flags.HasFlag(ServerQueryFlags.MaxPlayers))
        {
            maxPlayers = reader.ReadByte();
        }

        if (flags.HasFlag(ServerQueryFlags.Pwads))
        {
            var count = reader.ReadByte();
            for (var i = 0; i < count; i++)
            {
                pwads.Add(reader.ReadString());
            }
        }

        if (flags.HasFlag(ServerQueryFlags.GameType))
        {
            gameMode = (GameMode)reader.ReadByte();
            _ = reader.ReadByte(); // instagib
            _ = reader.ReadByte(); // buckshot
        }

        if (flags.HasFlag(ServerQueryFlags.GameName))
        {
            gameName = reader.ReadString();
        }

        if (flags.HasFlag(ServerQueryFlags.Iwad))
        {
            iwad = reader.ReadString();
        }

        if (flags.HasFlag(ServerQueryFlags.ForcePassword))
        {
            requiresPassword = reader.ReadByte() != 0;
        }

        if (flags.HasFlag(ServerQueryFlags.ForceJoinPassword))
        {
            requiresPassword |= reader.ReadByte() != 0;
        }

        if (flags.HasFlag(ServerQueryFlags.GameSkill))
        {
            _ = reader.ReadByte();
        }

        if (flags.HasFlag(ServerQueryFlags.BotSkill))
        {
            _ = reader.ReadByte();
        }

        if (flags.HasFlag(ServerQueryFlags.DmFlags))
        {
            _ = reader.ReadUInt32();
            _ = reader.ReadUInt32();
            _ = reader.ReadUInt32();
        }

        if (flags.HasFlag(ServerQueryFlags.Limits))
        {
            var fragLimit = reader.ReadInt16();
            var timeLimit = reader.ReadInt16();
            if (timeLimit > 0)
            {
                _ = reader.ReadInt16(); // time left
            }

            _ = reader.ReadInt16(); // duel limit
            _ = reader.ReadInt16(); // point limit
            _ = reader.ReadInt16(); // win limit
            _ = fragLimit;
        }

        if (flags.HasFlag(ServerQueryFlags.TeamDamage))
        {
            _ = reader.ReadSingle();
        }

        if (flags.HasFlag(ServerQueryFlags.TeamScores))
        {
            _ = reader.ReadInt16();
            _ = reader.ReadInt16();
        }

        if (flags.HasFlag(ServerQueryFlags.NumPlayers))
        {
            numPlayers = reader.ReadByte();
        }

        if (flags.HasFlag(ServerQueryFlags.PlayerData))
        {
            var isTeamGame = gameMode.IsTeamMode();
            for (var i = 0; i < numPlayers; i++)
            {
                var rawName = reader.ReadString();
                var frags = reader.ReadInt16();
                var ping = reader.ReadInt16();
                var isSpectator = reader.ReadByte() != 0;
                var isBot = reader.ReadByte() != 0;
                var team = isTeamGame ? reader.ReadByte() : -1;
                var timeOnServer = reader.ReadByte();

                players.Add(new PlayerInfo
                {
                    Name = rawName,
                    DisplayName = ZandronumColors.Strip(rawName),
                    Frags = frags,
                    Ping = ping,
                    IsSpectator = isSpectator,
                    IsBot = isBot,
                    Team = team,
                    TimeOnServer = timeOnServer,
                });
            }
        }

        // Remaining blocks are optional; read them defensively so a shorter reply never breaks us.
        if (flags.HasFlag(ServerQueryFlags.TeamInfoNumber))
        {
            _ = reader.ReadByte();
        }

        if (flags.HasFlag(ServerQueryFlags.ExtendedInfo) && reader.Remaining >= 4)
        {
            var extended = (ServerQueryFlags2)reader.ReadUInt32();
            if (extended.HasFlag(ServerQueryFlags2.PwadHashes) && reader.Remaining > 0)
            {
                var count = reader.ReadByte();
                for (var i = 0; i < count && reader.Remaining > 0; i++)
                {
                    _ = reader.ReadString();
                }
            }

            if (extended.HasFlag(ServerQueryFlags2.Country) && reader.Remaining >= 3)
            {
                var a = reader.ReadByte();
                var b = reader.ReadByte();
                var c = reader.ReadByte();
                country = new string([(char)a, (char)b, (char)c]);
            }

            if (extended.HasFlag(ServerQueryFlags2.GameModeName) && reader.Remaining > 0)
            {
                gameModeName = reader.ReadString();
            }
        }

        return new ServerInfo
        {
            Address = endpoint.ToString(),
            Host = endpoint.Host,
            Port = endpoint.Port,
            Name = name,
            DisplayName = ZandronumColors.Strip(name),
            Url = url,
            MapName = mapName,
            GameName = gameName,
            Iwad = iwad,
            Version = version,
            Country = country,
            GameMode = gameMode,
            GameModeName = string.IsNullOrEmpty(gameModeName) ? gameMode.ToDisplayName() : gameModeName,
            MaxClients = maxClients,
            MaxPlayers = maxPlayers,
            NumPlayers = numPlayers,
            Pwads = pwads,
            Players = players,
            RequiresPassword = requiresPassword,
            Latency = latency,
            LastUpdated = DateTimeOffset.Now,
        };
    }
}

/// <summary>Raised when a server replies but refuses the query.</summary>
public sealed class LauncherException(string message, bool ignored) : Exception(message)
{
    /// <summary>True when the server answered with "query ignored" rather than failing.</summary>
    public bool IsRateLimited { get; } = ignored;
}
