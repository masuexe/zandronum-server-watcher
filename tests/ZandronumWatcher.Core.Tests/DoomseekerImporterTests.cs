using ZandronumWatcher.Core.Import;
using Xunit;

namespace ZandronumWatcher.Core.Tests;

public class DoomseekerImporterTests
{
    [Fact]
    public void Reads_the_current_single_line_format()
    {
        const string ini = """
            [Doomseeker]
            CustomServers="(Zandronum;183.131.51.108;10667);(Zandronum;183.131.51.108;10666)"
            CustomServers2="(Zandronum;183.131.51.108;10667;1);(Zandronum;183.131.51.108;10666;1)"
            """;

        var result = DoomseekerImporter.Parse(ini);

        Assert.Equal(2, result.Servers.Count);
        Assert.Contains(result.Servers, s => s.ToString() == "183.131.51.108:10667");
        Assert.Contains(result.Servers, s => s.ToString() == "183.131.51.108:10666");
    }

    [Fact]
    public void Duplicates_between_the_two_keys_collapse()
    {
        const string ini = """
            CustomServers="(Zandronum;1.2.3.4;10666)"
            CustomServers2="(Zandronum;1.2.3.4;10666;1)"
            """;

        Assert.Single(DoomseekerImporter.Parse(ini).Servers);
    }

    [Fact]
    public void Ignores_entries_for_other_engines_but_reports_them()
    {
        const string ini = """
            CustomServers2="(Zandronum;1.2.3.4;10666;1);(Odamex;5.6.7.8;15000;1);(Q-Zandronum;9.9.9.9;15300;0)"
            """;

        var result = DoomseekerImporter.Parse(ini);

        var server = Assert.Single(result.Servers);
        Assert.Equal("1.2.3.4:10666", server.ToString());
        Assert.Contains("Odamex", result.SkippedEngines);
        Assert.Contains("Q-Zandronum", result.SkippedEngines);
    }

    [Fact]
    public void Handles_unquoted_values()
    {
        const string ini = "CustomServers=(Zandronum;1.2.3.4;10666)";

        Assert.Single(DoomseekerImporter.Parse(ini).Servers);
    }

    [Fact]
    public void Ignores_qvariant_placeholders()
    {
        const string ini = """
            Buddies=@Invalid()
            CustomServers=@Invalid()
            """;

        var result = DoomseekerImporter.Parse(ini);

        Assert.Empty(result.Servers);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Reports_malformed_entries_without_throwing()
    {
        const string ini = """
            CustomServers="(Zandronum;only-two-fields);(Zandronum;1.2.3.4;notaport);(Zandronum;1.2.3.5;70000);(Zandronum;;10666)"
            """;

        var result = DoomseekerImporter.Parse(ini);

        Assert.Empty(result.Servers);
        Assert.Equal(4, result.Warnings.Count);
    }

    [Fact]
    public void Missing_key_yields_nothing()
    {
        const string ini = """
            [Doomseeker]
            PlayerName=Player
            """;

        Assert.Empty(DoomseekerImporter.Parse(ini).Servers);
    }

    [Fact]
    public void Handles_windows_and_unix_line_endings()
    {
        const string ini = "CustomServers=(Zandronum;1.2.3.4;10666)\r\nCustomServers2=(Zandronum;1.2.3.5;10667;1)\r\n";

        var result = DoomseekerImporter.Parse(ini);

        Assert.Equal(2, result.Servers.Count);
    }

    [Fact]
    public void Tolerates_extra_fields_after_the_port()
    {
        const string ini = "CustomServers2=\"(Zandronum;1.2.3.4;10666;1;something-else)\"";

        Assert.Single(DoomseekerImporter.Parse(ini).Servers);
    }
}
