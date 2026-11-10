namespace UntilYouFall.Shared;

internal static class SteamQuestIcons
{
    public const string FolderRelativeToUserData = "Archipelago/steam-icons";

    public static readonly IReadOnlyDictionary<string, string> Sha1ByIconId = new Dictionary<string, string>
    {
        ["ACH_AETHER_BANKED"] = "68c92f3ee7bc90bdc4a26bd30e7ef42239538fec",
        ["ACH_AETHER_RUN"] = "231a62a400053ed8e82414c91902fa3f8270f68c",
        ["ACH_AETHER_TOTAL"] = "4323f4e756204c73cd2888703d17905bbf3917d5",
        ["ACH_CAPTAINS_KILLED"] = "595491ed552cdd0bab5a6b9d58e4815ab6d58c5f",
        ["ACH_COMBOS_COMPLETED"] = "6d4a101153c246187d2a4f8918a21f44ea5b0f45",
        ["ACH_DODGES"] = "830e3a6463c94bede1dadf6adf54c09a93444baa",
        ["ACH_EMPOWEREDS_KILLED"] = "2775a7311f23b4ebc9fb1ba6c1f14cc1d199320b",
        ["ACH_ENEMIES_KILLED"] = "5f81660dc1ca16d57c751cf88105f728caad2d04",
        ["ACH_GUARDBREAKS"] = "93eeb87192ac63c2fb4ca8aa4f7ff33d111ec5e3",
        ["ACH_HORRORS_KILLED"] = "c3239bf115b0a5bde9ab1cf0902662b13cee659e",
        ["ACH_HUSKS_KILLED"] = "4e1b5c85697630d0f64f4bedd5f12843983a7c7e",
        ["ACH_KNIGHTS_KILLED"] = "762b44c11fd7c9fbd0d86a5ed741ad9203f67946",
        ["ACH_LONG_COMBO"] = "e695bd39a38db43926cc8ef7ddc2d8300b72cc49",
        ["ACH_MANY_TRAITS_EQUIPPED"] = "20f5fa943d517da9d8cdf013120ebb4ee556601e",
        ["ACH_MELEE_BLOCKS"] = "ae4610c4837fdb66c3245470d73315f714cb504f",
        ["ACH_RAGDOLL_DISTANCE"] = "fb31f46efd7f4c1300027f666ad4caf20b026e5c",
        ["ACH_RANGEDBLOCKS"] = "8585abf6fcaa62fd132351fb6f0b63ba890fa99e",
        ["ACH_RUNEGUARD"] = "7998db8204398bec6fec00d442017753c346ed6b",
        ["ACH_SHATTEREDWOODS_HEAVYWEAPONS"] = "60bd0e6e370e38f41e70599d216ab48d32bfe86e",
        ["ACH_SHATTEREDWOODS_KATARS"] = "51d771bb12a48890991b7008c53c088670bd786b",
        ["ACH_SHATTEREDWOODS_MONK"] = "bb96f6929f964b678584bf042513116d60afaee5",
        ["ACH_SHATTEREDWOODS_NIGHTMAREST"] = "5aff8625dbd7a5e69e0cc4b0d30bf7493053f6a6",
        ["ACH_SHATTEREDWOODS_NO_HEAL"] = "c9c28ad4588ccf8f43669fc5e6fbe78388d907ee",
        ["ACH_SHATTEREDWOODS_SPELLSWORD"] = "ab1bd5768b3aee7124495d0ea5b2d9776a346b31",
        ["ACH_SHATTEREDWOODS_STARTER_WEAPONS"] = "2b9f4dee6ce05b72ad04464393c77f04fd140628",
        ["ACH_SOUL_TENDERS"] = "91346495697f4a894441a9327dcb360426a048a4",
        ["ACH_SPEEDRUN"] = "223e99cf20b62410a48389a18d9f8452bb17c56b",
        ["ACH_SPELLWEAVERS_KILLED"] = "b9fdca9704e55f202244e51870c91eaaf17c204d",
        ["ACH_SUPER_LEVEL_4"] = "993db1c64d167bb75c978f5d9a4153264f9ca46d",
        ["ACH_TRAITS_ON_WEAPON_LOTS"] = "163874d17045e506fac9d63f901a2d60975bd775",
        ["ACH_UNBROKEN"] = "bfcbad06aae95725add31842fde4f4fd676327b9",
        ["ACH_VOID_KEEPERS"] = "e6abb7a8cd14f143814d61e49a09a036eb5c964e",
        ["ACH_WEAPONS_ACQUIRED_12"] = "615e42e2e2a1828b48c7a5c16171fbe391a6d74b",
        ["ACH_WEAPON_PURCHASED"] = "02ec2795aaf48804765ee31b9ca31c25c96da667",
        ["ACH_WEAPON_UPGRADED"] = "203ee5f755c203e40dabc26f6896f457bd95d914",
        ["ACH_WEAPON_UPGRADED_X"] = "6b52990d678e8f9c8d82f76ac219a8e7a3bab28e",
    };

    public static bool IsSteamIcon(string iconId) => Sha1ByIconId.ContainsKey(iconId);

    public static string FileName(string iconId) => iconId + ".jpg";
}
