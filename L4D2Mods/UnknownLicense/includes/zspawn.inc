#if defined _ZSpawn_included
#endinput
#endif
#define _ZSpawn_included

native int Bridge_SpawnZombieOfClass(int iClass);
native int Bridge_SpawnZombie(char[] zombieClass);
native void Bridge_SpawnMobs(int NumberOfZombies);

public SharedPlugin __pl_ZSpawnBridge  =
{
    name = "ZSpawn_Bridge",
    file = "l4d_zspawn.smx",
#if defined REQUIRE_PLUGIN
    required = 1,
#else
    required = 0,
#endif
};
