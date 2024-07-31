#if defined _AirDropSupplies_included
#endinput
#endif
#define _AirDropSupplies_included

native bool Bridge_SpawnSupplyBox(int Amount);

public SharedPlugin __pl_AirDropSuppliesBridge  =
{
    name = "SupplyDropBox_Bridge",
    file = "l4d2_supply_woodbox.smx",
#if defined REQUIRE_PLUGIN
    required = 1,
#else
    required = 0,
#endif
};
