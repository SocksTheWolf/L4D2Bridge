#if defined _GamePause_included
#endinput
#endif
#define _GamePause_included

native bool Bridge_GamePaused();
native void Bridge_TogglePaused();

public SharedPlugin __pl_GamePauseBridge  =
{
    name = "GamePause_Bridge",
    file = "l4d2pause.smx",
#if defined REQUIRE_PLUGIN
    required = 1,
#else
    required = 0,
#endif
};
