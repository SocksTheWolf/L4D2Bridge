#pragma semicolon 1

#include <sourcemod>
#include <sdktools>
#include <sdkhooks>
#include <l4d2pause>
#include <zspawn>
#include <l4d_lootboxes>
#include <l4d2_supply_woodbox>
#include <multicolors>

#define CONSOLENAME "Admin"
#define CHATTAG "{red}[Bridge]{default}"

public Plugin myinfo =
{
	name = "L4D2 AI Bridge",
	author = "SocksTheWolf",
	description = "gameplay",
	version = "1.0.0",
	url = "https://github.com/SocksTheWolf"
};

public APLRes AskPluginLoad2(Handle plugin, bool late, char[] error, int err_max)
{
    if (GetEngineVersion() != Engine_Left4Dead2)
    {
        strcopy(error, err_max, "this plugin only runs in \"Left 4 Dead 2\"");
        return APLRes_SilentFailure;
    }
    return APLRes_Success;
}

public void OnPluginStart()
{
    // zspawn
    RegAdminCmd("sm_bridge_spawnmob", BridgeSpawnMobs, ADMFLAG_ROOT, "Spawns the mobs");
    RegAdminCmd("sm_bridge_spawnzombie", BridgeSpawnZombie, ADMFLAG_ROOT, "Spawns a zombie of name");

    // pause
    RegAdminCmd("sm_bridge_checkpause", BridgeCheckPause, ADMFLAG_ROOT, "Checks if we are paused");
    RegAdminCmd("sm_bridge_togglepause", BridgeTogglePause, ADMFLAG_ROOT, "Toggles the global server pause");

    // lootboxes
    RegAdminCmd("sm_bridge_spawnlootbox", BridgeSpawnLootbox, ADMFLAG_ROOT, "Spawns a lootbox for a client");
    RegAdminCmd("sm_bridge_cleanlootboxes", BridgeCleanLootboxes, ADMFLAG_ROOT, "Cleans up all lootboxes");

    // supply crates
    RegAdminCmd("sm_bridge_supplycrate", BridgeSupplyCrate, ADMFLAG_ROOT, "Spawns X amount of supply crates");
}

////////////////////////////////////// ZSPAWN //////////////////////////////////////
Action BridgeSpawnZombie(int client, int args)
{
    if (args != 2)
        return Plugin_Handled;

    char spawnerName[100];
    char infectedType[10];
    
    // Get the class
    GetCmdArg(1, infectedType, sizeof(infectedType));

    // Copy donor
    GetCmdArg(2, spawnerName, sizeof(spawnerName));

    if (Bridge_SpawnZombie(infectedType))
    {
        CPrintToChatAll("%s A %s was spawned by {bluegrey}%s{default}!", CHATTAG, infectedType, spawnerName);
        PrintToServer("infected spawn success");
    }
    else
    {
        PrintToServer("infected spawn failed");
    }

    return Plugin_Handled;
}

Action BridgeSpawnMobs(int client, int args)
{
    if (args != 2)
        return Plugin_Handled;

    char spawnerName[100];
    char sArgs[8];
    int iCount;

    // Take the amount
    GetCmdArg(1, sArgs, sizeof(sArgs));
    iCount = StringToInt(sArgs);

    // Copy donor
    GetCmdArg(2, spawnerName, sizeof(spawnerName));

    Bridge_SpawnMobs(iCount);
    CPrintToChatAll("%s A Horde was spawned by {bluegrey}%s{default}!", CHATTAG, spawnerName);
    PrintToServer("mob spawn success");
    return Plugin_Handled;
}

////////////////////////////////////// PAUSE //////////////////////////////////////
Action BridgeCheckPause(int client, int args)
{
    if (Bridge_GamePaused())
        PrintToServer("game paused");
    else
        PrintToServer("game not paused");

    return Plugin_Handled;
}

Action BridgeTogglePause(int client, int args)
{
    Bridge_TogglePaused();
    return Plugin_Handled;
}

////////////////////////////////////// LOOTBOX //////////////////////////////////////
Action BridgeSpawnLootbox(int client, int args)
{
    if (args <= 0) return Plugin_Handled;

    char spawnerName[100];
    // Copy donor
    GetCmdArg(1, spawnerName, sizeof(spawnerName));

    if (Bridge_SpawnRandomLootbox(client))
    {
        CPrintToChatAll("%s A loot box was spawned by {bluegrey}%s{default}!", CHATTAG, spawnerName);
        PrintToServer("lootbox spawned");
    }
    else
    {
        PrintToServer("lootbox failed");
    }
    return Plugin_Handled;
}

Action BridgeCleanLootboxes(int client, int args)
{
    Bridge_CleanLootboxes();
    return Plugin_Handled;
}

////////////////////////////////////// CRATE //////////////////////////////////////
Action BridgeSupplyCrate(int client, int args)
{
    if (args <= 0) return Plugin_Handled;
    
    char spawnerName[100];
    // Copy donor
    GetCmdArg(1, spawnerName, sizeof(spawnerName));

    if (Bridge_SpawnSupplyBox(1))
    {
        CPrintToChatAll("%s A supply crate was spawned by {bluegrey}%s{default}!", CHATTAG, spawnerName);
        PrintToServer("supplies spawned");
    }
    else
    {
        PrintToServer("supplies failed");
    }
    return Plugin_Handled;
}
