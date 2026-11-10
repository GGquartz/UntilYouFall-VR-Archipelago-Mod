using Il2CppSG.Claymore.Level;

namespace UntilYouFallArchipelago;

internal static class TutorialRooms
{
    public static int MarkVisited(Campaign campaign)
    {
        var changed = 0;
        foreach (var room in campaign.allPotentialRooms)
        {
            if (room == null || !TutorialRules.IsReactiveRoom(room.GUID))
                continue;
            var group = room.SaveGroup;
            if (group.Contains(room))
                continue;
            group.Add(room);
            changed++;
        }

        return changed;
    }
}
