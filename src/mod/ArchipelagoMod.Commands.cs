using HarmonyLib;
using Il2CppSG.Claymore.Entities;
using Il2CppSG.Claymore.Level;
using Il2CppSG.Claymore.PlayerLoopControllers;
using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;
using UntilYouFall.Shared;

namespace UntilYouFallArchipelago;

public sealed partial class ArchipelagoMod
{
    private void ProcessApCommand()
    {
        if (!File.Exists(_apCommandPath))
            return;

        var age = DateTime.UtcNow - File.GetLastWriteTimeUtc(_apCommandPath);
        var commandLines = File.ReadAllText(_apCommandPath).Replace("\r", "").Split('\n');
        var command = commandLines[0].Trim();
        File.Delete(_apCommandPath);
        if (age > TimeSpan.FromSeconds(30))
        {
            Channel("AP").Write("Expired launcher command discarded");
            return;
        }
        switch (command)
        {
            case "Connect":
                try
                {
                    if (commandLines.Skip(1).Any(line => !string.IsNullOrWhiteSpace(line)))
                    {
                        if (commandLines.Length != 4)
                            throw new ArgumentException("Failed Connect command");
                        LauncherProtocol.ConnectCommand(commandLines[1], commandLines[2], commandLines[3]);
                        _apServer.Value = commandLines[1];
                        _apSlot.Value = commandLines[2];
                        _apPassword = commandLines[3];
                        MelonPreferences.GetCategory(LauncherProtocol.PreferencesSection).SaveToFile(false);
                    }
                    _apClient.Connect(_apServer.Value, _apSlot.Value, _apPassword);
                }
                catch (Exception ex)
                {
                    Channel("AP").Write("Connect rejected: " + ex.Message);
                    WriteApStatus(ConnectionState.Error, ex.GetType().Name + ": " + ex.Message);
                }
                break;
            case LauncherProtocol.DisconnectCommand:
                _apClient.Disconnect();
                WriteApStatus(ConnectionState.Disconnected, "Disconnected by launcher");
                break;
            default:
                Channel("AP").Warn("Unknown launcher command rejected");
                break;
        }
    }
}
