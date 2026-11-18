using UntilYouFall.Shared;

internal static class ConnectionFileTests
{
    public static void Run()
    {
        ParsesFields();
        RejectsBadInput();
        FormatRoundTrip();
        WriteAndRead();
        Console.WriteLine("PASS: connection file parsing, validation, writing and reading");
    }

    private static void ParsesFields()
    {
        var details = ConnectionFile.Parse(
            "# note\r\n\r\n  SERVER = archipelago.gg:38281 \r\nslot=My Slot\r\nPassword=a=b c\r\n"
        );
        Check(
            details == new ConnectionDetails("archipelago.gg:38281", "My Slot", "a=b c"),
            "fields are trimmed, keys are case-insensitive, passwords may contain '='"
        );
        Check(ConnectionFile.Parse("Server=localhost\nSlot=Player").Password == "", "the password is optional");
    }

    private static void RejectsBadInput()
    {
        Fails("Server=localhost\nPassword=x", "a slot is required");
        Fails("Slot=Player", "a server is required");
        Fails("Server=http://localhost\nSlot=Player", "only ws:// and wss:// are accepted");
        Fails("Server=localhost\nSlot=Player\nColor=red", "unknown settings are rejected");
        Fails("Server=localhost\nSlot=Player\nhunter2", "lines without '=' are rejected");
        var misplaced = Failure("Server=localhost\nSlot=Player\nhunter2");
        Check(!misplaced.Contains("hunter2"), "a rejected line is never echoed because it may be the password");
    }

    private static void FormatRoundTrip()
    {
        Fails(ConnectionFile.Format(new ConnectionDetails("", "", "")), "the untouched file asks for a slot");
        var original = new ConnectionDetails("localhost:38281", "Player č. 1", "pa=ss");
        Check(
            ConnectionFile.Parse(ConnectionFile.Format(original)) == original,
            "a written file parses back unchanged"
        );
        Check(
            ConnectionFile.Format(new ConnectionDetails("", "Player", "")).Contains("Server=archipelago.gg:12345"),
            "an empty server falls back to the default address"
        );
    }

    private static void WriteAndRead()
    {
        var directory = Path.Combine(Path.GetTempPath(), "uyf-connection-" + Guid.NewGuid());
        var path = Path.Combine(directory, "UserData", ConnectionFile.FileName);
        try
        {
            Check(ConnectionFile.TryRead(path) == null, "a missing file reads as nothing");
            var first = new ConnectionDetails("archipelago.gg:58880", "Player", "secret");
            ConnectionFile.Write(path, first);
            Check(ConnectionFile.TryRead(path) == first, "server, slot and password survive reopening");
            var next = new ConnectionDetails("wss://example.test:1234", "Another", "");
            ConnectionFile.Write(path, next);
            Check(ConnectionFile.TryRead(path) == next, "the latest write wins");
            Check(
                !Directory.EnumerateFiles(Path.GetDirectoryName(path)!, "*.tmp").Any(),
                "atomic writes leave no temporary files"
            );

            File.WriteAllText(path, "Server=localhost\nSlot=\n");
            Check(
                ConnectionFile.TryRead(path) == new ConnectionDetails("localhost", "", ""),
                "a half-filled file still shows its fields in the launcher"
            );
            var damaged = "Server=localhost\nhunter2\n";
            File.WriteAllText(path, damaged);
            Check(ConnectionFile.TryRead(path) == null, "a damaged file reads as nothing");
            Check(File.ReadAllText(path) == damaged, "reading never rewrites a damaged file");
            Check(ConnectionFile.TryRead(Path.GetDirectoryName(path)!) == null, "an unreadable path reads as nothing");
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    private static string Failure(string text)
    {
        try
        {
            ConnectionFile.Parse(text);
        }
        catch (ArgumentException exception)
        {
            return exception.Message;
        }
        return "";
    }

    private static void Fails(string text, string message) => Check(Failure(text) != "", message);

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new Exception("Connection file: " + message);
    }
}
