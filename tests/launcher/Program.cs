using System.IO.Compression;
using UntilYouFall.Launcher.Core;
using UntilYouFall.Shared;

var failed = 0;
void Check(string name, bool condition)
{
    Console.WriteLine((condition ? "PASS " : "FAIL ") + name);
    if (!condition)
        failed++;
}
void Throws(string name, Action action)
{
    try
    {
        action();
        Check(name, false);
    }
    catch
    {
        Check(name, true);
    }
}

var options = OptionsCatalog.Load();
Check("manifest game identity", OptionsCatalog.Game == GameIdentity.Name && GameIdentity.Name == "Until You Fall");
LauncherResourceTests.Run(Check);
var defaults = OptionsCatalog.Defaults(options);
var defaultYaml = OptionsCatalog.BuildYaml("Player1", defaults, options);
Check(
    "manifest game identity in YAML",
    defaultYaml.Contains($"\ngame: {GameIdentity.Name}\n") && defaultYaml.Contains($"\n{GameIdentity.Name}:\n")
);
Check("quest hint cost default", defaults["quest_hint_cost"] == "500");
Check("quest hint cost range", options.Single(option => option.Key == "quest_hint_cost") is { Min: 0, Max: 5000 });
Check(
    "free quest hint YAML",
    OptionsCatalog
        .BuildYaml("Player1", new Dictionary<string, string>(defaults) { ["quest_hint_cost"] = "0" }, options)
        .Contains("  quest_hint_cost: 0\n")
);
Check(
    "common YAML list quoting",
    OptionsCatalog.YamlList(" Captain's Warhammer, ,Shop Unlock ") == "[\"Captain's Warhammer\", \"Shop Unlock\"]"
);
Check("common YAML empty list", OptionsCatalog.YamlList("  ") == "[]");
Check(
    "common YAML export",
    OptionsCatalog
        .BuildYaml(
            "Player1",
            defaults,
            options,
            new Dictionary<string, string> { ["accessibility"] = "minimal", ["local_items"] = "[]" }
        )
        .Contains("  accessibility: minimal\n  local_items: []\n")
);
var trapWeights = options.Single(option => option.Key == "trap_weights");
Check("trap weights exported as weights", trapWeights.Type == "weights" && trapWeights.Keys.Count == 5);
Check("trap weights default even split", defaults["trap_weights"].StartsWith("{\"Enemy Spawn Trap\": 20,"));
Check(
    "trap weights YAML export",
    OptionsCatalog
        .BuildYaml(
            "Player1",
            new Dictionary<string, string>(defaults)
            {
                ["trap_weights"] = "{\"Enemy Spawn Trap\": 80, \"Dodge Only Trap\": 20}",
            },
            options
        )
        .Contains("  trap_weights: {\"Enemy Spawn Trap\": 80, \"Dodge Only Trap\": 20}\n")
);
var allZero = new Dictionary<string, string>(defaults)
{
    ["trap_percent"] = "10",
    ["trap_weights"] = "{\"Enemy Spawn Trap\": 0}",
};
Check("all-zero trap weights rejected", OptionsCatalog.Validate("Player1", allZero, options) != null);
allZero["trap_percent"] = "0";
Check("all-zero trap weights fine without traps", OptionsCatalog.Validate("Player1", allZero, options) == null);
Check("catalog has options", options.Count >= 12);
Check("defaults: goal quests", defaults["goal"] == "quests");
Check("defaults: quest board true", defaults["quest_board"] == "true");
Check("defaults: goal runs 5", defaults["goal_runs"] == "5");
Check("defaults: death link deaths door", defaults["death_link_mode"] == "deaths_door");
Check(
    "death link choices preserve IDs and append deaths door",
    options
        .Single(option => option.Key == "death_link_mode")
        .Choices.SequenceEqual(new[] { "hard", "soft", "trap", "off", "deaths_door" })
);
Check(
    "death link YAML export",
    OptionsCatalog
        .BuildYaml("Player1", new Dictionary<string, string>(defaults) { ["death_link_mode"] = "deaths_door" }, options)
        .Contains("  death_link_mode: deaths_door\n")
);
Check("defaults: starting difficulty challenging", defaults["starting_difficulty"] == "challenging");
Check("defaults: no second starting weapon", defaults["second_starting_weapon"] == "none");
Check("defaults: early hand first half", defaults["early_hand"] == "first_50_percent_of_checks");
Check(
    "early hand choices",
    options
        .Single(option => option.Key == "early_hand")
        .Choices.SequenceEqual(new[] { "first_25_percent_of_checks", "first_50_percent_of_checks", "unrestricted" })
);
var duplicateStarters = new Dictionary<string, string>(defaults)
{
    ["starting_weapon"] = "mistcutter",
    ["second_starting_weapon"] = "mistcutter",
};
Check("duplicate starting weapons rejected", OptionsCatalog.Validate("Player", duplicateStarters, options) != null);
duplicateStarters["starting_weapon"] = "random";
Check("random plus fixed starter accepted", OptionsCatalog.Validate("Player", duplicateStarters, options) == null);
Check("defaults validate", OptionsCatalog.Validate("Player", defaults, options) == null);
var twoHanded = new Dictionary<string, string>(defaults)
{
    ["starting_weapon"] = "captains_warhammer",
    ["starting_hands"] = "left_only",
};
Check("two-handed starter with one hand rejected", OptionsCatalog.Validate("Player", twoHanded, options) != null);
twoHanded["starting_hands"] = "both";
Check("two-handed starter with both hands accepted", OptionsCatalog.Validate("Player", twoHanded, options) == null);
twoHanded["starting_hands"] = "right_only";
twoHanded["starting_weapon"] = "mistcutter";
Check("one-handed starter with one hand accepted", OptionsCatalog.Validate("Player", twoHanded, options) == null);
twoHanded["starting_weapon"] = "random";
twoHanded["second_starting_weapon"] = "cold_iron_greataxe";
Check(
    "two-handed second starter with one hand accepted",
    OptionsCatalog.Validate("Player", twoHanded, options) == null
);
var questSet = options.Single(option => option.Key == "required_quests");
string[] Hard(int count) =>
    questSet.Values.Where(value => value.Tier == 3).Take(count).Select(value => value.Name).ToArray();
IReadOnlyDictionary<string, IReadOnlyList<string>> Sets(string[] required, string[] excluded) =>
    new Dictionary<string, IReadOnlyList<string>> { ["required_quests"] = required, ["excluded_quests"] = excluded };
Check(
    "quest sets default to an empty YAML list",
    OptionsCatalog.BuildYaml("Player1", defaults, options).Contains("  required_quests: []\n")
);
Check(
    "6 required hard quests accepted",
    OptionsCatalog.Validate("Player", defaults, options, null, Sets(Hard(6), [])) == null
);
Check(
    "7 required hard quests rejected",
    OptionsCatalog.Validate("Player", defaults, options, null, Sets(Hard(7), [])) != null
);
Check(
    "required and excluded overlap rejected",
    OptionsCatalog.Validate("Player", defaults, options, null, Sets(Hard(1), Hard(1))) != null
);
Check(
    "excluding too many easy quests rejected",
    OptionsCatalog.Validate(
        "Player",
        defaults,
        options,
        null,
        Sets(
            [],
            questSet
                .Values.Where(value => value.Tier == 1)
                .Take(questSet.Values.Count(easy => easy.Tier == 1) - 5)
                .Select(value => value.Name)
                .ToArray()
        )
    ) != null
);
Check(
    "quest sets ignored without Quest Board",
    OptionsCatalog.Validate(
        "Player",
        new Dictionary<string, string>(defaults) { ["quest_board"] = "false" },
        options,
        null,
        Sets(Hard(7), [])
    ) == null
);
Check(
    "quest names YAML-quoted",
    OptionsCatalog.YamlNames(["Dodge, Duck, Dip, Dive and Dodge"]) == "[\"Dodge, Duck, Dip, Dive and Dodge\"]"
);
Check("name too long", OptionsCatalog.Validate(new string('a', 17), defaults, options) != null);
Check("name empty", OptionsCatalog.Validate("", defaults, options) != null);
var bad = new Dictionary<string, string>(defaults)
{
    ["starting_difficulty"] = "nightmare",
    ["max_difficulty"] = "challenging",
};
Check("start harder than max rejected", OptionsCatalog.Validate("P", bad, options) != null);
bad = new(defaults) { ["starting_difficulty"] = "random", ["max_difficulty"] = "relaxed" };
Check("random start needs max above relaxed", OptionsCatalog.Validate("P", bad, options) != null);
bad = new(defaults) { ["starting_difficulty"] = "random", ["max_difficulty"] = "nightmare" };
Check("random start with nightmare max ok", OptionsCatalog.Validate("P", bad, options) == null);

var yaml = OptionsCatalog.BuildYaml("My \"Slot\"", defaults, options);
Check("yaml header", yaml.StartsWith("name: \"My \\\"Slot\\\"\"\ngame: Until You Fall\n"));
Check("yaml section", yaml.Contains("\nUntil You Fall:\n  starting_weapon: random\n"));
Check("yaml every option", options.All(option => yaml.Contains("  " + option.Key + ": ")));
Check("yaml toggle bool", yaml.Contains("  quest_board: true\n"));
Check("yaml second starter none", yaml.Contains("  second_starting_weapon: none\n"));
Check("yaml early hand default", yaml.Contains("  early_hand: first_50_percent_of_checks\n"));
foreach (var choice in options.Single(option => option.Key == "early_hand").Choices)
{
    var handOptions = new Dictionary<string, string>(defaults) { ["early_hand"] = choice };
    Check(
        "yaml early hand " + choice,
        OptionsCatalog.BuildYaml("Player", handOptions, options).Contains("  early_hand: " + choice + "\n")
    );
}

var vdf =
    "\"libraryfolders\"\n{\n\t\"0\"\n\t{\n\t\t\"path\"\t\t\"C:\\\\Program Files (x86)\\\\Steam\"\n\t}\n\t\"1\"\n\t{\n\t\t\"path\"\t\t\"D:\\\\Games\"\n\t}\n}";
Check(
    "steam libraries parsed",
    GameLocator.LibraryPaths(vdf).SequenceEqual(new[] { @"C:\Program Files (x86)\Steam", @"D:\Games" })
);
Check(
    "appmanifest installdir parsed",
    GameLocator.InstallDirectory(
        "\"AppState\"\n{\n\t\"appid\"\t\t\"858260\"\n\t\"installdir\"\t\t\"Until You Fall\"\n}"
    ) == "Until You Fall"
);
Check("appmanifest without installdir", GameLocator.InstallDirectory("\"AppState\" { }") == null);
foreach (
    var (input, expected) in new[]
    {
        (@"C:\Games\UntilYouFall", @"C:\Games\UntilYouFall"),
        ("  \"C:\\Games\\UntilYouFall\\\"  ", @"C:\Games\UntilYouFall"),
        (@"C:\Games\UntilYouFall\UntilYouFall.exe", @"C:\Games\UntilYouFall"),
        (@"C:\Games\UntilYouFall\UntilYouFall_Data", @"C:\Games\UntilYouFall"),
        ("", ""),
    }
)
    Check(
        "game dir normalized: " + input.Trim(),
        GameLocator.Normalize(input.Replace('\\', Path.DirectorySeparatorChar))
            == expected.Replace('\\', Path.DirectorySeparatorChar)
    );
Check("game dir normalize handles null", GameLocator.Normalize(null) == "");

Check("linux launch option", LinuxSupport.LaunchOption == "WINEDLLOVERRIDES=\"version=n,b\" %command%");
Check(
    "linux steam roots",
    LinuxSupport
        .SteamRoots("/home/player")
        .Select(path => path.Replace('\\', '/'))
        .SequenceEqual(
            new[]
            {
                "/home/player/.steam/steam",
                "/home/player/.local/share/Steam",
                "/home/player/.var/app/com.valvesoftware.Steam/.local/share/Steam",
                "/home/player/snap/steam/common/.local/share/Steam",
            }
        )
);
Check(
    "proton log folder next to the game library",
    LinuxSupport.ProtonUnityLogFolder("/mnt/games/SteamLibrary/steamapps/common/UntilYouFall/").Replace('\\', '/')
        == "/mnt/games/SteamLibrary/steamapps/compatdata/858260/pfx/drive_c/users/steamuser/AppData/LocalLow/Schell Games/UntilYouFall"
);
Check(
    "linux game process detected from proton command line",
    LinuxSupport.IsGameCommandLine("proton\0waitforexitandrun\0/lib/steamapps/common/UntilYouFall/UntilYouFall.exe\0")
        && !LinuxSupport.IsGameCommandLine("/opt/UYFArchipelagoLauncher\0")
);

var temp = Path.Combine(Path.GetTempPath(), "uyf-launcher-test-" + Guid.NewGuid().ToString("N"));
{
    var steamRoot = Path.Combine(temp, "linux-steam");
    var otherLibrary = Path.Combine(temp, "linux-library");
    Directory.CreateDirectory(Path.Combine(steamRoot, "config"));
    Directory.CreateDirectory(Path.Combine(otherLibrary, "steamapps"));
    var libraryPath = otherLibrary.Replace('\\', '/');
    File.WriteAllText(
        Path.Combine(steamRoot, "config", "libraryfolders.vdf"),
        "\"libraryfolders\"\n{\n\t\"1\"\n\t{\n\t\t\"path\"\t\t\"" + libraryPath + "\"\n\t}\n}"
    );
    File.WriteAllText(
        Path.Combine(otherLibrary, "steamapps", "appmanifest_858260.acf"),
        "\"AppState\"\n{\n\t\"installdir\"\t\t\"Until You Fall\"\n}"
    );
    var candidates = GameLocator.Candidates(new[] { steamRoot }).ToList();
    Check(
        "locator reads libraries with forward slashes and the manifest install dir",
        candidates.Contains(Path.Combine(libraryPath, "steamapps", "common", "Until You Fall"))
            && candidates.Contains(Path.Combine(libraryPath, "steamapps", "common", "UntilYouFall"))
            && candidates.Contains(Path.Combine(steamRoot, "steamapps", "common", "UntilYouFall"))
    );
}
var game = Path.Combine(temp, "game");
var payload = Path.Combine(temp, "payload");
Directory.CreateDirectory(Path.Combine(payload, "Mods"));
Directory.CreateDirectory(Path.Combine(payload, "UserLibs"));
Directory.CreateDirectory(game);
{
    File.WriteAllText(Path.Combine(game, "UntilYouFall.exe"), "test game");
    var versions = VersionInfo.Read(game, payload);
    Check(
        "versions: launcher comes from its assembly",
        versions.Single(entry => entry.Component == "Launcher").Package == VersionInfo.LauncherVersion
    );
    Check(
        "versions: APWorld comes from manifest",
        Version.TryParse(versions.Single(entry => entry.Component == "APWorld").Package, out _)
    );
    Check(
        "versions: minimum AP version is explicit",
        versions.Any(entry => entry.Component == "Archipelago (minimum)" && Version.TryParse(entry.Package, out _))
    );
    Check(
        "versions: missing mod is explicit",
        versions.Single(entry => entry.Component == "Game mod").Installed == "Not installed"
    );
    Check(
        "versions: APWorld installation is not guessed",
        versions.Single(entry => entry.Component == "APWorld").Installed == "-"
    );
    var relative = Path.Combine("Mods", "UntilYouFall.Archipelago.dll");
    File.Copy(typeof(OptionsCatalog).Assembly.Location, Path.Combine(payload, relative));
    var expected = VersionInfo.LauncherVersion;
    Check(
        "versions: reads packaged DLL without loading it",
        VersionInfo.Read(game, payload).Single(entry => entry.Component == "Game mod").Package == expected
    );
    Directory.CreateDirectory(Path.Combine(game, "Mods"));
    File.Copy(typeof(OptionsCatalog).Assembly.Location, Path.Combine(game, relative));
    Check(
        "versions: reads installed DLL",
        VersionInfo.Read(game, payload).Single(entry => entry.Component == "Game mod").Installed == expected
    );
    File.WriteAllText(Path.Combine(game, relative), "not a versioned DLL");
    Check(
        "versions: invalid DLL has no invented version",
        VersionInfo.Read(game, payload).Single(entry => entry.Component == "Game mod").Installed == "Unknown"
    );
    Check(
        "versions: no game folder is explicit",
        VersionInfo.Read("", payload).Single(entry => entry.Component == "Game mod").Installed
            == "Game folder not selected"
    );
    File.Delete(Path.Combine(payload, relative));
    File.Delete(Path.Combine(game, relative));
    File.Delete(Path.Combine(game, "UntilYouFall.exe"));
}
Directory.CreateDirectory(GameLink.StatusPath(game));
Check("status: inaccessible file does not crash polling", GameLink.ReadStatus(game) == null);
Directory.Delete(GameLink.StatusPath(game));
Directory.CreateDirectory(Path.Combine(game, "UserData"));
File.WriteAllText(GameLink.StatusPath(game), "invalid\nnot a result");
Check("status: unknown state is ignored", GameLink.ReadStatus(game) == null);
File.WriteAllText(GameLink.StatusPath(game), "connected\nready");
Check(
    "status: connected result",
    (await GameLink.WaitForStatusAsync(game, CancellationToken.None)).State == ConnectionState.Connected
);
File.WriteAllText(GameLink.StatusPath(game), "disconnected\nDisconnected by launcher");
Check(
    "status: disconnected result",
    (await GameLink.WaitForStatusAsync(game, CancellationToken.None)).State == ConnectionState.Disconnected
);
File.WriteAllText(GameLink.StatusPath(game), "error\nLogin rejected: InvalidPassword");
Check(
    "status: login error preserved",
    (await GameLink.WaitForStatusAsync(game, CancellationToken.None)).Detail == "Login rejected: InvalidPassword"
);
File.WriteAllText(GameLink.StatusPath(game), "connecting\nwaiting");
using (var cancellation = new CancellationTokenSource())
{
    var waiting = Task.Run(() => GameLink.WaitForStatusAsync(game, cancellation.Token));
    cancellation.Cancel();
    try
    {
        await waiting.ConfigureAwait(false);
        Check("status: polling cancels", false);
    }
    catch (OperationCanceledException)
    {
        Check("status: polling cancels", true);
    }
}
Throws(
    "protocol: newline in password rejected",
    () => LauncherProtocol.ConnectCommand("localhost:1", "Player1", "secret\nextra")
);
Throws(
    "protocol: unsupported scheme rejected before saving",
    () => LauncherProtocol.ConnectCommand("ftp://localhost", "Player1", "")
);
Throws(
    "protocol: invalid address rejected before saving",
    () => LauncherProtocol.ConnectCommand("ws://host:invalid", "Player1", "")
);
Check("protocol: disconnect is a single-line command", LauncherProtocol.DisconnectCommand == "Disconnect");
Check(
    "protocol: empty password is a fourth line",
    LauncherProtocol.ConnectCommand("localhost:1", "Player1", "").Split('\n').Length == 4
);
var atomicPath = Path.Combine(temp, "atomic.txt");
File.WriteAllText(atomicPath, "keep");
Throws(
    "atomic write: failed writer",
    () =>
        AtomicFile.Write(
            atomicPath,
            temporary =>
            {
                File.WriteAllText(temporary, "partial");
                throw new IOException("test failure");
            }
        )
);
Check(
    "atomic write: original preserved and temporary cleaned",
    File.ReadAllText(atomicPath) == "keep" && !Directory.EnumerateFiles(temp, "*.tmp").Any()
);
Throws(
    "atomic write: no overwrite",
    () => AtomicFile.Write(atomicPath, temporary => File.WriteAllText(temporary, "new"), overwrite: false)
);
Check(
    "atomic write: failed move preserves original and cleans temporary",
    File.ReadAllText(atomicPath) == "keep" && !Directory.EnumerateFiles(temp, "*.tmp").Any()
);
File.WriteAllText(Path.Combine(payload, "Mods", "m.dll"), "mod");
File.WriteAllText(Path.Combine(payload, "UserLibs", "l.dll"), "lib");
if (ModInstaller.GameRunning())
    Console.WriteLine("SKIP installer tests: game is running");
else
{
    Check("status: all missing", ModInstaller.Status(game, payload).All(entry => entry.State == FileState.Missing));
    Check("install copies both", ModInstaller.Install(game, payload) == 2);
    Check("status: all current", ModInstaller.Status(game, payload).All(entry => entry.State == FileState.Current));
    Check("install again is a no-op", ModInstaller.Install(game, payload) == 0);
    File.WriteAllText(Path.Combine(game, "Mods", "m.dll"), "tampered");
    Check(
        "status: detects changed file",
        ModInstaller.Status(game, payload).Any(entry => entry.State == FileState.Different)
    );
    Check(
        "repair restores it",
        ModInstaller.Install(game, payload) == 1 && File.ReadAllText(Path.Combine(game, "Mods", "m.dll")) == "mod"
    );
    File.WriteAllText(Path.Combine(game, "Mods", "other-mod.dll"), "keep");
    Check(
        "uninstall removes only ours",
        ModInstaller.Uninstall(game, payload) == 2 && File.Exists(Path.Combine(game, "Mods", "other-mod.dll"))
    );
    Throws("install with empty payload fails", () => ModInstaller.Install(game, Path.Combine(temp, "missing")));
}

var iconGame = Path.Combine(temp, "icon-game");
byte[] Sha1Bytes(string text) => System.Text.Encoding.ASCII.GetBytes(text);
string Sha1Hex(byte[] bytes) =>
    Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(bytes)).ToLowerInvariant();
var goodHash = Sha1Hex(Sha1Bytes("good"));
var testIcons = new Dictionary<string, string> { ["ACH_GOOD"] = goodHash, ["ACH_BAD"] = new string('a', 40) };
var fakeCalls = 0;
Task<byte[]> FakeCdn(string url, CancellationToken token)
{
    fakeCalls++;
    if (url.Contains("cdn.akamai"))
        throw new HttpRequestException("blocked");

    return Task.FromResult(url.Contains(goodHash) ? Sha1Bytes("good") : Sha1Bytes("wrong"));
}
var iconResult = await SteamIconInstaller.InstallAsync(iconGame, FakeCdn, icons: testIcons);
Check("steam icons: good icon downloaded, wrong content rejected", iconResult is { Downloaded: 1, Failed: 1 });
Check("steam icons: rejected icon is reported missing", SteamIconInstaller.MissingCount(iconGame, testIcons) == 1);
Check("steam icons: next host is used after the first fails", fakeCalls == 1 + 1 + 4);
Check(
    "steam icons: an invalid file is never saved",
    !File.Exists(Path.Combine(SteamIconInstaller.IconFolder(iconGame), "ACH_BAD.jpg"))
);
var callsBefore = fakeCalls;
await SteamIconInstaller.InstallAsync(iconGame, FakeCdn, icons: testIcons);
Check("steam icons: valid file is not downloaded again", fakeCalls - callsBefore == 4);
Check(
    "steam icons: url uses the game app id and hash",
    SteamIconInstaller.Url("host", "abc") == "https://host/steamcommunity/public/images/apps/858260/abc.jpg"
);
Check(
    "steam icons: shipped hashes are SHA1",
    SteamQuestIcons.Sha1ByIconId.Values.All(hash =>
        hash.Length == 40 && hash.All(Uri.IsHexDigit) && hash == hash.ToLowerInvariant()
    )
);
var offlineGame = Path.Combine(temp, "offline-game");
var offlineCalls = 0;
var offlineResult = await SteamIconInstaller.InstallAsync(
    offlineGame,
    (url, token) =>
    {
        offlineCalls++;
        throw new HttpRequestException("no network");
    },
    icons: new Dictionary<string, string>
    {
        ["ACH_A"] = new string('b', 40),
        ["ACH_B"] = new string('c', 40),
        ["ACH_C"] = new string('d', 40),
    }
);
Check(
    "steam icons: offline stops after the first icon",
    offlineCalls == 4 && offlineResult is { Downloaded: 0, Failed: 3 }
);
SteamIconInstaller.Uninstall(iconGame);
Check("steam icons: uninstall removes the folder", !Directory.Exists(SteamIconInstaller.IconFolder(iconGame)));

var fake = Path.Combine(temp, "ml.zip");
using (var archive = ZipFile.Open(fake, ZipArchiveMode.Create))
    archive.CreateEntry("version.dll");
Throws("melonloader: wrong hash is rejected", () => MelonLoaderInstaller.Extract(fake, game));
Check("melonloader: nothing extracted on hash failure", !File.Exists(Path.Combine(game, "version.dll")));
Check("melonloader: not installed in empty dir", !MelonLoaderInstaller.IsInstalled(game));

Check(
    "password redacted",
    !LogExporter.RedactPreferences("[X]\nAPServer = \"a\"\nAPPassword = \"secret\"\n").Contains("secret")
);
{
    var root = Path.Combine(temp, "log-export");
    Directory.CreateDirectory(root);
    var privateLog = """
        Logs exported to C:\Users\PrivateWindowsUser\Desktop\report.zip
        Game folder: D:/Users/Previous User/Games/UntilYouFall
        Linux folder: /home/privatelinuxuser/.local/share/Steam/steamapps/common/UntilYouFall
        Wine folder: Z:\home\privatelinuxuser\.steam\steam
        Not a user folder: https://example.com/home/page
        Escaped path: C:\\Users\\PrivateWindowsUser\\Desktop\\report.zip
        [AP] Connecting to wss://private-ap.example:58880; slot=Private Slot
        Connected to 192.0.2.7:38281 as Private Slot.
        Connecting to ws://[2001:db8::7]:38281 as Private Slot...
        Error (SocketException): Timed out connecting to private-ap.example:58880
        Error (SocketException): No such host is known. (private-ap.example:58880)
        Error (SocketException): Connection refused by 192.0.2.7:38281
        Error (SocketException): Connection refused by [2001:db8::7]:38281
        Error (WebSocketException): wss://private-ap.example:58880/socket
        at Export in D:\source\LogExporter.cs:42
        [APConsole] [Hint]: Other Player's Sword is at Some Location.
        [APStartingTraits] slot=2; instance=123
        """;
    var launcherLog = Path.Combine(root, "launcher.log");
    File.WriteAllText(launcherLog, privateLog);
    File.WriteAllText(launcherLog + ".old", privateLog);
    var exportGame = Path.Combine(root, "game");
    Directory.CreateDirectory(Path.Combine(exportGame, "MelonLoader", "Logs"));
    Directory.CreateDirectory(Path.Combine(exportGame, "UserData", "ArchipelagoLogs"));
    File.WriteAllText(Path.Combine(exportGame, GameLocator.Executable), "");
    File.WriteAllText(Path.Combine(exportGame, "MelonLoader", "Latest.log"), privateLog);
    File.WriteAllText(Path.Combine(exportGame, "MelonLoader", "Logs", "session.log"), privateLog);
    File.WriteAllText(Path.Combine(exportGame, "UserData", "ArchipelagoLogs", "session.log"), privateLog);
    File.WriteAllText(
        GameLink.StatusPath(exportGame),
        "Connected\nConnected to private-ap.example:58880 as Private Slot."
    );
    var preferencesPath = Path.Combine(exportGame, "UserData", "MelonPreferences.cfg");
    var preferences =
        "[UntilYouFallArchipelago]\r\nAPServer = \"wss://private-ap.example:58880\"\r\nAPSlot = \"Private Slot\"\r\nAPPassword = \"private-password\"\r\n";
    File.WriteAllText(preferencesPath, preferences);
    var zipPath = Path.Combine(root, "out.zip");
    LogExporter.Export(exportGame, Path.Combine(root, "payload"), zipPath, launcherLog, "0.0");
    using var archive = ZipFile.OpenRead(zipPath);
    Check(
        "export has info and launcher log",
        archive.GetEntry("info.txt") != null && archive.GetEntry("launcher/launcher.log") != null
    );
    foreach (
        var name in new[]
        {
            "launcher/launcher.log",
            "launcher/launcher.log.old",
            "game/Latest.log",
            "game/Logs/session.log",
            "game/ArchipelagoLogs/session.log",
        }
    )
    {
        using var reader = new StreamReader(archive.GetEntry(name)!.Open());
        var exportedLog = reader.ReadToEnd();
        Check(
            "export masks Windows profile names: " + name,
            !exportedLog.Contains("PrivateWindowsUser")
                && !exportedLog.Contains("Previous User")
                && exportedLog.Contains("<UserName>")
        );
        Check(
            "export masks Linux profile names: " + name,
            !exportedLog.Contains("privatelinuxuser")
                && exportedLog.Contains("/home/<UserName>/.local/share/Steam")
                && exportedLog.Contains(@"Z:\home\<UserName>\.steam")
                && exportedLog.Contains("https://example.com/home/page")
        );
        Check(
            "export masks connection details: " + name,
            !exportedLog.Contains("private-ap.example")
                && !exportedLog.Contains("58880")
                && !exportedLog.Contains("Private Slot")
                && !exportedLog.Contains("192.0.2.7")
                && !exportedLog.Contains("2001:db8::7")
                && !exportedLog.Contains("38281")
        );
        Check(
            "export preserves AP messages and diagnostic details: " + name,
            exportedLog.Contains("[APConsole] [Hint]: Other Player's Sword is at Some Location.")
                && exportedLog.Contains("[APStartingTraits] slot=2; instance=123")
                && exportedLog.Contains("Error (SocketException): Timed out connecting to")
                && exportedLog.Contains("Error (WebSocketException): wss://")
                && exportedLog.Contains("at Export in D:\\source\\LogExporter.cs:42")
        );
    }
    using var settingsReader = new StreamReader(archive.GetEntry("game/MelonPreferences.cfg")!.Open());
    var exportedPreferences = settingsReader.ReadToEnd();
    Check(
        "export masks server, slot and password in preferences",
        !exportedPreferences.Contains("private-ap.example")
            && !exportedPreferences.Contains("Private Slot")
            && !exportedPreferences.Contains("private-password")
    );
    Check("export keeps preferences section", exportedPreferences.Contains("[UntilYouFallArchipelago]"));
    using var statusReader = new StreamReader(archive.GetEntry("game/Archipelago.status")!.Open());
    var exportedStatus = statusReader.ReadToEnd();
    Check(
        "export masks status connection details",
        exportedStatus.StartsWith("Connected\n")
            && !exportedStatus.Contains("private-ap.example")
            && !exportedStatus.Contains("Private Slot")
    );
    Check(
        "export leaves source files unchanged",
        File.ReadAllText(launcherLog) == privateLog && File.ReadAllText(preferencesPath) == preferences
    );
    using var info = new StreamReader(archive.GetEntry("info.txt")!.Open());
    var content = info.ReadToEnd();
    Check(
        "export includes component versions",
        content.Contains("Component versions (package / installed):")
            && content.Contains("APWorld:")
            && content.Contains("Game mod:")
            && content.Contains("MelonLoader:")
    );
    var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    Check(
        "export masks profile path in info",
        string.IsNullOrEmpty(userProfile) || !content.Contains(userProfile, StringComparison.OrdinalIgnoreCase)
    );
}
{
    var zipPath = Path.Combine(temp, "export-retry.zip");
    Directory.CreateDirectory(zipPath);
    Throws("export failure keeps destination", () => LogExporter.Export("", payload, zipPath, "", "test"));
    Directory.Delete(zipPath);
    LogExporter.Export("", payload, zipPath, "", "test");
    Check("export can retry after failed rename", File.Exists(zipPath));
}

Directory.Delete(temp, true);
var hardStart = new Dictionary<string, string>(defaults)
{
    ["max_difficulty"] = "relentless",
    ["starting_difficulty"] = "relaxed",
};
Check(
    "start item harder than max rejected",
    OptionsCatalog.Validate("P", hardStart, options, ["Difficulty: Nightmare"]) != null
);
Check(
    "start item within max accepted",
    OptionsCatalog.Validate("P", hardStart, options, ["Difficulty: Relentless"]) == null
);
LauncherViewModelTests.Run(Check);
Console.WriteLine(failed == 0 ? "All launcher tests passed." : failed + " launcher test(s) failed.");
return failed == 0 ? 0 : 1;
