#if defined _LootboxesSpawner_included
#endinput
#endif
#define _LootboxesSpawner_included

native bool Bridge_SpawnRandomLootbox(int client);
native void Bridge_CleanLootboxes();

public SharedPlugin __pl_LootboxesBridge  =
{
    name = "Lootboxes_Bridge",
    file = "l4d_lootboxes.smx",
#if defined REQUIRE_PLUGIN
    required = 1,
#else
    required = 0,
#endif
};