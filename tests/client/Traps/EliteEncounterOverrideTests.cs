using UntilYouFallArchipelago;

internal static class EliteEncounterOverrideTests
{
    public static void Run()
    {
        var directory = Path.Combine(Path.GetTempPath(), "uyf-elite-overrides-" + Guid.NewGuid());
        var path = Path.Combine(directory, "rooms.json");
        var room = new EliteEncounterRoom(2, 123, 0, EliteEncounterTrapRules.CampaignGuid, "original-room");
        var token = Guid.NewGuid().ToString("N");
        try
        {
            var store = EliteEncounterOverrides.Open(path);
            store.Reserve(room, token);
            store.Reserve(room, token);
            Assert(
                store.HasReceipt(token) && store.TryGetReceipt(room, out var saved) && saved == token,
                "a receipt reserves exactly one room"
            );
            var offline = EliteEncounterOverrides.Open(path);
            Assert(
                offline.TryGetReceipt(room, out saved) && saved == token,
                "room restoration works before AP login and independently of its identity"
            );
            Assert(
                offline.GetEliteRoomGuid(room) == EliteEncounterTrapRules.CaptainRoomGuid,
                "existing Captain reservations retain their variant"
            );
            for (var index = 0; index < EliteEncounterTrapRules.EliteEncounters.Count; index++)
            {
                var variant = EliteEncounterTrapRules.EliteEncounters[index];
                var variantRoom = room with { Index = 3 + index };
                var variantToken = Guid.NewGuid().ToString("N");
                store.Reserve(variantRoom, variantToken, variant.RoomGuid);
                store.Reserve(variantRoom, variantToken, variant.RoomGuid);
                var restored = EliteEncounterOverrides.Open(path);
                Assert(
                    restored.TryGetReceipt(variantRoom, out saved)
                        && saved == variantToken
                        && restored.GetEliteRoomGuid(variantRoom) == variant.RoomGuid,
                    "each randomly selected variant survives offline reload without rerolling"
                );
                var rerollRejected = false;
                var different = EliteEncounterTrapRules.EliteEncounters.First(pair => pair != variant);
                try
                {
                    store.Reserve(variantRoom, variantToken, different.RoomGuid);
                }
                catch (InvalidOperationException)
                {
                    rerollRejected = true;
                }
                Assert(rerollRejected, "a reserved slot cannot change variant for the same receipt");
            }
            var legacyLateRoom = room with { Index = 14 };
            var legacyLateToken = Guid.NewGuid().ToString("N");
            store.Reserve(legacyLateRoom, legacyLateToken, EliteEncounterTrapRules.KnightRoomGuid);
            Assert(
                EliteEncounterOverrides.Open(path).GetEliteRoomGuid(legacyLateRoom)
                    == EliteEncounterTrapRules.KnightRoomGuid,
                "an existing late-stage Knight reservation restores unchanged after the selection rule update"
            );
            foreach (
                var other in new[]
                {
                    room with
                    {
                        Profile = 3,
                    },
                    room with
                    {
                        Seed = 124,
                    },
                    room with
                    {
                        Index = 1,
                    },
                    room with
                    {
                        CampaignGuid = "tutorial",
                    },
                    room with
                    {
                        OriginalRoomGuid = "different-room",
                    },
                }
            )
                Assert(!offline.TryGetReceipt(other, out _), "run/profile/campaign/depth/room isolation");
            var duplicateRejected = false;
            try
            {
                store.Reserve(room with { Index = 1 }, token);
            }
            catch (InvalidOperationException)
            {
                duplicateRejected = true;
            }
            Assert(duplicateRejected, "one token cannot reserve a second room after a crash or replay");
            var overwriteRejected = false;
            try
            {
                store.Reserve(room, Guid.NewGuid().ToString("N"));
            }
            catch (InvalidOperationException)
            {
                overwriteRejected = true;
            }
            Assert(overwriteRejected, "restoring a previously reserved room cannot spend a new pending receipt");

            Directory.Delete(directory, true);
            var second = room with { Index = 2 };
            var secondToken = Guid.NewGuid().ToString("N");
            var failureObserved = false;
            try
            {
                store.Reserve(second, secondToken);
            }
            catch (IOException)
            {
                failureObserved = true;
            }
            Assert(
                failureObserved && !store.HasReceipt(secondToken) && !store.TryGetReceipt(second, out _),
                "failed persistence cannot produce an in-memory-only override"
            );
            Directory.CreateDirectory(directory);
            File.WriteAllText(
                path,
                System.Text.Json.JsonSerializer.Serialize(new[] { new { Room = room, Receipt = token } })
            );
            Assert(
                EliteEncounterOverrides.Open(path).GetEliteRoomGuid(room) == EliteEncounterTrapRules.CaptainRoomGuid,
                "legacy reservations without a variant restore Captain"
            );
            var invalidVariantRejected = false;
            try
            {
                store.Reserve(second, secondToken, "non-elite-room");
            }
            catch (ArgumentException)
            {
                invalidVariantRejected = true;
            }
            Assert(invalidVariantRejected, "non-elite variants cannot be reserved");
            File.WriteAllText(
                path,
                System.Text.Json.JsonSerializer.Serialize(
                    new[]
                    {
                        new
                        {
                            Room = room,
                            Receipt = token,
                            EliteRoomGuid = "non-elite-room",
                        },
                    }
                )
            );
            invalidVariantRejected = false;
            try
            {
                EliteEncounterOverrides.Open(path);
            }
            catch (InvalidDataException)
            {
                invalidVariantRejected = true;
            }
            Assert(invalidVariantRejected, "unknown variants in saved state are rejected");
            File.WriteAllText(path, "[{\"Room\":null,\"Receipt\":\"bad\"}]");
            var corruptRejected = false;
            try
            {
                EliteEncounterOverrides.Open(path);
            }
            catch (InvalidDataException)
            {
                corruptRejected = true;
            }
            Assert(
                corruptRejected,
                "corrupt overrides must disable the feature rather than silently changing a saved room"
            );
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
        Console.WriteLine("PASS: elite room override persistence, offline restore, isolation and reservation rollback");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception("Elite overrides: " + message);
    }
}
