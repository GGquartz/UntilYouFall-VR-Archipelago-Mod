using System.Text.Json;
using UntilYouFall.Shared;

namespace UntilYouFallArchipelago;

internal sealed record EliteEncounterRoom(
    int Profile,
    int Seed,
    int Index,
    string CampaignGuid,
    string OriginalRoomGuid
);

internal sealed class EliteEncounterOverrides
{
    private sealed record Entry(
        EliteEncounterRoom Room,
        string Receipt,
        string EliteRoomGuid = EliteEncounterTrapRules.CaptainRoomGuid
    );

    private readonly string _path;
    private readonly Dictionary<EliteEncounterRoom, Entry> _entries;

    private EliteEncounterOverrides(string path, Dictionary<EliteEncounterRoom, Entry> entries)
    {
        _path = path;
        _entries = entries;
    }

    public static EliteEncounterOverrides Open(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var entries = File.Exists(path)
            ? JsonSerializer.Deserialize<Entry[]>(File.ReadAllText(path))
                ?? throw new InvalidDataException("Empty elite room override file")
            : Array.Empty<Entry>();
        var rooms = new Dictionary<EliteEncounterRoom, Entry>();
        var receipts = new HashSet<string>();
        foreach (var entry in entries)
        {
            if (
                entry == null
                || !ValidRoom(entry.Room)
                || !Guid.TryParseExact(entry.Receipt, "N", out _)
                || !EliteEncounterTrapRules.EliteEncounters.Any(pair => pair.RoomGuid == entry.EliteRoomGuid)
                || !rooms.TryAdd(entry.Room, entry)
                || !receipts.Add(entry.Receipt)
            )
                throw new InvalidDataException("Invalid or duplicate elite room override");
        }

        return new EliteEncounterOverrides(path, rooms);
    }

    public bool TryGetReceipt(EliteEncounterRoom room, out string? receipt)
    {
        var found = _entries.TryGetValue(room, out var entry);
        receipt = entry?.Receipt;

        return found;
    }

    public string GetEliteRoomGuid(EliteEncounterRoom room) => _entries[room].EliteRoomGuid;

    public bool HasReceipt(string receipt) => _entries.Values.Any(entry => entry.Receipt == receipt);

    public void Reserve(
        EliteEncounterRoom room,
        string receipt,
        string eliteRoomGuid = EliteEncounterTrapRules.CaptainRoomGuid
    )
    {
        if (
            !ValidRoom(room)
            || !Guid.TryParseExact(receipt, "N", out _)
            || !EliteEncounterTrapRules.EliteEncounters.Any(pair => pair.RoomGuid == eliteRoomGuid)
        )
            throw new ArgumentException("Invalid elite room reservation");
        if (_entries.TryGetValue(room, out var existing))
        {
            if (existing.Receipt != receipt || existing.EliteRoomGuid != eliteRoomGuid)
                throw new InvalidOperationException("Elite room already reserved by another receipt or variant");
            return;
        }
        if (HasReceipt(receipt))
            throw new InvalidOperationException("Elite receipt already reserved another room");

        _entries.Add(room, new Entry(room, receipt, eliteRoomGuid));
        try
        {
            AtomicFile.Write(
                _path,
                temporary => File.WriteAllText(temporary, JsonSerializer.Serialize(_entries.Values))
            );
        }
        catch
        {
            _entries.Remove(room);
            throw;
        }
    }

    private static bool ValidRoom(EliteEncounterRoom? room) =>
        room != null
        && room.Profile >= 0
        && room.Index is >= 0 and < StageChecks.Count
        && room.CampaignGuid == EliteEncounterTrapRules.CampaignGuid
        && !string.IsNullOrWhiteSpace(room.OriginalRoomGuid);
}
