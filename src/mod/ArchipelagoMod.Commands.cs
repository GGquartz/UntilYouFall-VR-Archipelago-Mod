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
                        Connect(commandLines[1], commandLines[2], commandLines[3]);
                    }
                    else
                    {
                        ConnectFromFile();
                    }
                }
                catch (Exception ex)
                {
                    Channel("AP").Write("Connect rejected: " + ex.Message);
                    WriteApStatus(ConnectionState.Error, ex.GetType().Name + ": " + ex.Message);
                }
                break;
            case LauncherProtocol.DisconnectCommand:
                Disconnect("Disconnected by launcher");
                break;
            default:
                Channel("AP").Warn("Unknown launcher command rejected");
                break;
        }
    }

    private void Connect(string server, string slot, string password)
    {
        if (_apClient.Connected)
        {
            Channel("AP").Write("Already connected");
            return;
        }

        LauncherProtocol.ConnectCommand(server, slot, password);
        ConnectionFile.Write(_connectionPath, new ConnectionDetails(server, slot, password));
        _apClient.Connect(server, slot, password);
    }

    private void Disconnect(string detail)
    {
        _apClient.Disconnect();
        WriteApStatus(ConnectionState.Disconnected, detail);
    }

    private void EnsureConnectionFile()
    {
        if (File.Exists(_connectionPath))
            return;

        try
        {
            ConnectionFile.Write(_connectionPath, new ConnectionDetails("", "", ""));
            Channel("AP").Write("Connection file created: " + _connectionPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Channel("AP").Warn("Failed to create the connection file: " + ex.Message);
        }
    }

    private void ConnectFromFile()
    {
        if (_apClient.Connected)
        {
            Channel("AP").Write("Already connected");
            return;
        }

        try
        {
            if (!File.Exists(_connectionPath))
            {
                EnsureConnectionFile();
                _noticeUi.ShowTechnical(ApNotice.Plain($"Fill in UserData/{ConnectionFile.FileName} first"));
                return;
            }

            var details = ConnectionFile.Parse(File.ReadAllText(_connectionPath));
            _buttonConnectPending = true;
            _apClient.Connect(details.Server, details.Slot, details.Password);
        }
        catch (Exception ex)
        {
            _buttonConnectPending = false;
            Channel("AP").Write("Connect button rejected: " + ex.Message);
            _noticeUi.ShowTechnical(ApNotice.Error(ex.Message));
        }
    }
}
