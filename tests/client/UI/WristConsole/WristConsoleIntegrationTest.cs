using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Packets;
using UntilYouFallArchipelago;

internal static class WristConsoleIntegrationTest
{
    public static async Task Run(string server)
    {
        var directory = Path.Combine(Path.GetTempPath(), "uyf-wrist-console-test-" + Guid.NewGuid());
        var messages = new List<ApConsoleMessage>();
        var client = new ArchipelagoClient(directory, Console.WriteLine, console: messages.Add);
        var chatter = ArchipelagoSessionFactory.CreateSession(server);
        try
        {
            client.Connect(server, "UYFTest", "");
            await Until(() => client.Connected);
            var login = await Task.Run(() =>
                chatter.TryConnectAndLogin(
                    ArchipelagoClient.Game,
                    "UYFTest",
                    ItemsHandlingFlags.AllItems,
                    new Version(0, 6, 7),
                    uuid: "wrist-console-test"
                )
            );
            if (login is not LoginSuccessful)
                throw new Exception("Test chat client login failed.");
            const string chat = "Wrist console chat <size=99>safe</size>";
            await chatter.Socket.SendPacketAsync(new SayPacket { Text = chat });
            await Until(() => messages.Any(message => message.PlainText.Contains(chat)));
            var received = messages.Last(message => message.PlainText.Contains(chat));
            if (received.RichText.Contains("<size=99>"))
                throw new Exception("Server chat injected TMP markup.");
            if (!messages.Any(message => message.PlainText.Contains("joined")))
                throw new Exception("Non-item join event was not forwarded to the console.");
            Console.WriteLine(
                "PASS: real AP server forwards join events and chat to wrist console without granting an item"
            );
        }
        finally
        {
            client.Disconnect();
            await chatter.Socket.DisconnectAsync();
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }

        async Task Until(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(20);
            while (DateTime.UtcNow < deadline)
            {
                client.Update();
                if (condition())
                    return;
                await Task.Delay(25);
            }
            throw new Exception("Timed out waiting for AP console traffic.");
        }
    }
}
