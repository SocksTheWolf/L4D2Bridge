#if defined _CarExplode_included
#endinput
#endif
#define _CarExplode_included

native void Bridge_EnableCarExplosion(bool newState);

public SharedPlugin __pl_CarExplodeBridge  =
{
    name = "CarExplode_Bridge",
    file = "l4d_explosive_cars.smx",
#if defined REQUIRE_PLUGIN
    required = 1,
#else
    required = 0,
#endif
};
