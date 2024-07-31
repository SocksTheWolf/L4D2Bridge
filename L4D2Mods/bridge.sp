#pragma semicolon 1

#include <sourcemod>
#include <sdktools>
#include <l4d2pause>
#include <zspawn>
#include <l4d_lootboxes>
#include <l4d2_supply_woodbox>
#include <multicolors>
#include <keyvalues>

#define CONSOLENAME "Admin"
#define CHAT_TAG "[Bridge]"
#define CHAT_TAG_COLORED "{olive}[Bridge]{default}"
#define TEAM_SURVIVORS 2
#define TEAM_INFECTED 3
#define MAX_ENEMY_DIGITS 8
#define DONOR_NAME_LEN 100

////////////////////////////////////// GLOBALS //////////////////////////////////////
public Plugin myinfo =
{
	name = "L4D2 AI Bridge",
	author = "SocksTheWolf",
	description = "gameplay",
	version = "1.0.1",
	url = "https://github.com/SocksTheWolf"
};

KeyValues g_specialTrackers = null; // A KV map that is used to deterime if a zombie was spawned by us or not.
static char g_zombieNames[9][] = 
{
    "invalid",
    "Smoker",
    "Boomer",
    "Hunter",
    "Spitter",
    "Jockey",
    "Charger",
    "Witch",
    "Tank"
};

bool g_CanHandleCommands = false;

////////////////////////////////////// INIT //////////////////////////////////////
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
    RegAdminCmd("sm_bridge_supplycrate", BridgeSupplyCrate, ADMFLAG_ROOT, "Spawn a supply crate");
	
	// Show names above attacked spawned zombies
    //HookEvent("player_hurt", Event_OnPlayerHurt);
    
    // Print messages whenever a zombie dies
    HookEvent("player_death", Event_OnPlayerDeath);
    HookEvent("witch_killed", Event_OnWitchKilled);

    // Keeping track of special infected we have spawned.
    HookEvent("round_start", Event_OnRoundStart, EventHookMode_PostNoCopy);
    HookEvent("round_end", Event_OnRoundEnd, EventHookMode_PostNoCopy);
    HookEvent("map_transition", Event_OnRoundEnd, EventHookMode_PostNoCopy);
    HookEvent("mission_lost", Event_OnRoundEnd, EventHookMode_PostNoCopy);
    HookEvent("finale_vehicle_leaving", Event_OnRoundEnd, EventHookMode_PostNoCopy);
}

public APLRes AskPluginLoad2(Handle plugin, bool late, char[] error, int err_max)
{
    if (GetEngineVersion() != Engine_Left4Dead2)
    {
        strcopy(error, err_max, "this plugin only runs in \"Left 4 Dead 2\"");
        return APLRes_SilentFailure;
    }
    return APLRes_Success;
}

////////////////////////////////////// EVENTS //////////////////////////////////////
public Action Event_OnRoundStart(Event event, const char[] name, bool dontBroadcast)
{
    if (!IsSpecialMapValid())
    {
        g_specialTrackers = new KeyValues("specialInfectTracker");
        PrintToServer("Craeted new Special Infected Tracker");
    }
    g_CanHandleCommands = true;
    return Plugin_Continue;
}

public Action Event_OnRoundEnd(Event event, const char[] name, bool dontBroadcast)
{
    if (IsSpecialMapValid())
    {
        delete g_specialTrackers;
        g_specialTrackers = null;
        PrintToServer("Deleted Special Infected Tracker");
    }
    g_CanHandleCommands = false;
    return Plugin_Continue;
}

public Action Event_OnPlayerHurt(Event event, const char[] name, bool dontBroadcast)
{
	int attackerID = GetClientOfUserId(event.GetInt("attacker"));
	int zombieID = GetClientOfUserId(event.GetInt("userid"));
    // Ignore handling if this hit is caused by a bot or the world
	if (!attackerID || !IsClientInGame(attackerID) || GetClientTeam(attackerID) != TEAM_SURVIVORS || IsFakeClient(attackerID)) 
        return Plugin_Continue;
	
	// Only print if this is a special zombie being attacked.
	if (!IsSpecialZombie(zombieID)) return Plugin_Continue;

	// Check if the special map is currently made/valid
	if (!IsSpecialMapValid())
	{
        PrintToServer("Special Map Tracking is invalid!");
        return Plugin_Continue;
    }

	// Check to see if the zombie is something we spawned.
	if (SetKVPtrToLocation(zombieID))
	{
        PrintCenterText(attackerID, "Attacking %N!", zombieID);
    }
	return Plugin_Continue;
}

public Action Event_OnPlayerDeath(Event event, const char[] name, bool dontBroadcast)
{
    // This is the id of the zombie, also the key in our KV pair.
    int zombieID = GetClientOfUserId(GetEventInt(event, "userid"));
    if (!zombieID || GetClientTeam(zombieID) != TEAM_INFECTED || !IsSpecialMapValid()) 
        return Plugin_Continue;

    HandleDeathEvents(zombieID);
    return Plugin_Continue;
}

public Action Event_OnWitchKilled(Event event, const char[] name, bool dontBroadcast)
{
    int witchID = event.GetInt("witchid", -1);
    HandleDeathEvents(witchID, true);
    return Plugin_Continue;
}

void HandleDeathEvents(int zombieID, bool isWitch=false)
{
    // Look from the top in the array
    if (SetKVPtrToLocation(zombieID))
    {
        // Grab donor name
        char donorName[DONOR_NAME_LEN];
        g_specialTrackers.GetString(NULL_STRING, donorName, sizeof(donorName), "donor");

        // Get the zombie class name
        int zombieClass;
        if (!isWitch) 
            zombieClass = GetEntProp(zombieID, Prop_Send, "m_zombieClass");
        else
            zombieClass = 7;

        // Remove this key specifically from the tree
        g_specialTrackers.DeleteThis();

        char PrintMsg[MAX_MESSAGE_LENGTH];
        FormatEx(PrintMsg, sizeof(PrintMsg), "%s spawned by %s has been defeated!", g_zombieNames[zombieClass], donorName);

        // Print the message out to everyone's HUD
        PrintHintTextToAll(PrintMsg);
        // Display message to all users.
        CPrintToChatAll("%s %s", CHAT_TAG_COLORED, PrintMsg);
        // Send it to the console as well
        PrintToServer("%s %s", CHAT_TAG, PrintMsg);
    }
}

////////////////////////////////////// KV //////////////////////////////////////
bool AddZombieToTracker(int zombieID, char[] spawnerName)
{
    if (!IsSpecialMapValid()) return false;

    char idStr[MAX_ENEMY_DIGITS];
    if (IntToString(zombieID, idStr, sizeof(idStr)) == 0) return false;

    g_specialTrackers.SetString(idStr, spawnerName);
    return true;
}

bool SetKVPtrToLocation(int zombieID)
{
    if (!IsSpecialMapValid() || zombieID < 0) return false;

    char idStr[MAX_ENEMY_DIGITS];
    if (IntToString(zombieID, idStr, sizeof(idStr)) == 0) return false;

    g_specialTrackers.Rewind();
    return g_specialTrackers.JumpToKey(idStr);
}

bool IsSpecialMapValid() // tracks if our KV map is valid
{
    return g_specialTrackers != null;
}

////////////////////////////////////// UTILS //////////////////////////////////////
bool IsSpecialZombie(int enemyID)
{
	if (enemyID > 0 && IsClientInGame(enemyID) && GetClientTeam(enemyID) == TEAM_INFECTED)
	{
		return HasEntProp(enemyID, Prop_Send, "m_zombieClass");
	}
	return false;
}

void RenameZombie(int zombieID, const char[] spawnerName)
{
	char newName[MAX_NAME_LENGTH];
	Format(newName, sizeof(newName), "%N by %s", zombieID, spawnerName);
	SetClientName(zombieID, newName);
}

void ShowHintActionText(char[] actionText, int len)
{
	CRemoveTags(actionText, len);
	PrintHintTextToAll(actionText);
}

void PrintSpawnMessage(const char[] spawnName, const char[] spawnColor, const char[] spawnType)
{
    char msgbuf[MAX_MESSAGE_LENGTH];
    FormatEx(msgbuf, sizeof(msgbuf), "A {%s}%s{default} was spawned by {olive}%s{default}!", spawnColor, spawnType, spawnName);
    CPrintToChatAll("%s %s", CHAT_TAG_COLORED, msgbuf);
    ShowHintActionText(msgbuf, sizeof(msgbuf));
}

////////////////////////////////////// ZSPAWN //////////////////////////////////////
Action BridgeSpawnZombie(int client, int args)
{
    if (args != 2) return Plugin_Handled;
    if (!g_CanHandleCommands) return Plugin_Handled;

    char spawnerName[DONOR_NAME_LEN];
    char infectedType[10];
    
    // Get the class
    GetCmdArg(1, infectedType, sizeof(infectedType));

    // Copy donor
    GetCmdArg(2, spawnerName, sizeof(spawnerName));
	
    int zombieID = Bridge_SpawnZombie(infectedType);
    if (zombieID != -1)
    {
        // Rename zombies if they aren't the witch (cannot be renamed)
        if (!StrEqual(infectedType, "witch"))
		    RenameZombie(zombieID, spawnerName);

        PrintSpawnMessage(spawnerName, "red", infectedType);

        if (!AddZombieToTracker(zombieID, spawnerName))
            PrintToServer("%s WARN unable to track id %d", CHAT_TAG, zombieID);
        
        PrintToServer("%s infected spawn success", CHAT_TAG);
    }
    else
    {
        PrintToServer("%s infected spawn failed", CHAT_TAG);
    }

    return Plugin_Handled;
}

Action BridgeSpawnMobs(int client, int args)
{
    if (args != 2) return Plugin_Handled;
    if (!g_CanHandleCommands) return Plugin_Handled;

    char spawnerName[DONOR_NAME_LEN];
    char sArgs[8];
    int iCount;

    // Take the amount
    GetCmdArg(1, sArgs, sizeof(sArgs));
    iCount = StringToInt(sArgs);

    // Copy donor
    GetCmdArg(2, spawnerName, sizeof(spawnerName));

    Bridge_SpawnMobs(iCount);
    PrintSpawnMessage(spawnerName, "red", "horde");
	
    PrintToServer("%s mob spawn success", CHAT_TAG);
    return Plugin_Handled;
}

////////////////////////////////////// PAUSE //////////////////////////////////////
Action BridgeCheckPause(int client, int args)
{
    if (Bridge_GamePaused())
        PrintToServer("%s game paused", CHAT_TAG);
    else
        PrintToServer("%s game not paused", CHAT_TAG);

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
    if (!g_CanHandleCommands) return Plugin_Handled;

    char spawnerName[DONOR_NAME_LEN];
    // Copy donor
    GetCmdArg(1, spawnerName, sizeof(spawnerName));

    if (Bridge_SpawnRandomLootbox(client))
    {
        PrintSpawnMessage(spawnerName, "blue", "loot box");
        PrintToServer("%s lootbox spawned", CHAT_TAG);
    }
    else
    {
        PrintToServer("%s lootbox failed", CHAT_TAG);
    }
    return Plugin_Handled;
}

Action BridgeCleanLootboxes(int client, int args)
{
    if (!g_CanHandleCommands) return Plugin_Handled;

    Bridge_CleanLootboxes();
    return Plugin_Handled;
}

////////////////////////////////////// CRATE //////////////////////////////////////
Action BridgeSupplyCrate(int client, int args)
{
    if (args <= 0) return Plugin_Handled;
    if (!g_CanHandleCommands) return Plugin_Handled;
    
    char spawnerName[DONOR_NAME_LEN];
    // Copy donor
    GetCmdArg(1, spawnerName, sizeof(spawnerName));

    if (Bridge_SpawnSupplyBox(1))
    {
        PrintSpawnMessage(spawnerName, "blue", "supply crate");
        PrintToServer("%s supplies spawned", CHAT_TAG);
    }
    else
    {
        PrintToServer("%s supplies failed", CHAT_TAG);
    }
    return Plugin_Handled;
}
