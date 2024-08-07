namespace L4D2Bridge.Types
{
    // The physical command actions that are executed on a server, with no arguments
    // these usually tie into sourcemod plugins used by the bridge system
    public enum ServerCommands
    {
        None,
        Raw,
        CheckPause,
        TogglePause,
        SpawnMob,
        SpawnZombie,
        SpawnLootbox,
        SpawnSupplyCrate,
        HealPlayers,
        RespawnPlayers,
        UnincapPlayers,
    };
}
