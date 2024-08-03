# L4D2 Bridge

This is an application and service layer to a L4D2 server, that allows for external events to influence an active server live.

## Influence Services

* Tiltify Donations
* Twitch Events

## Setup

### Configuration

All configuration data is stored in a flat file called `config.json`. Upon first time running the application, a blank `config.json` file will be created for you.
For the most part, these settings are fairly straight forward to fill out. The rest of the sections are explained below.

#### Actions

The actions section is a dictionary with key names that correspond to the `RuleName` or `SuccessEvent` of a matching rule definition (see Rules section), and the string array 
of server commands that should be ran when rules with the given keyname matches.

Acceptable server commands are:

```
    SpawnTank,
    SpawnSpitter,
    SpawnJockey,
    SpawnWitch,
    SpawnMob,
    SpawnMobSmall,
    SpawnMobMedium,
    SpawnMobLarge,
    SpawnBoomer,
    SpawnHunter,
    SpawnCharger,
    SpawnSmoker,
    Lootbox,
    SupplyCrate
```

##### Example

Here is an example of some actions that are defined in a config file. Names such as "chaos" and "santa" are used in the `rules.json` later.
```
  "actions": {
    "chaos": [
      "SpawnMobLarge",
      "SpawnMobLarge",
      "SpawnCharger"
    ],
    "tank": [
      "SpawnTank"
    ],
    "lootbox": [
      "Lootbox"
    ],
    "santa": [
      "Lootbox",
      "Lootbox",
      "SupplyCrate"
    ]
  },
```

#### Mob Sizes
These are the ranges for each type of mob spawn size in the server commands list. Rand is when the size is not provided in the actions array.

---

### Rules

`rules.json` is an [MS RulesEngine](https://github.com/microsoft/RulesEngine/wiki/Getting-Started#rules-schema) formatted file. 

The following important notes are:

* `WorkflowName` has to be one of the services that this application supports. Currently `tiltify` or `twitch`
* For each rule, either `RuleName` or `SuccessEvent` should be the name of one of the actions you have defined in the `config.json` file earlier. The system will check for `RuleName` matches first and upon failure to match, will check the `SuccessEvent` for an action match.
* All rules are evaluated for every event of a given service type. Execution does not stop on the first rule event, so you can have mutliple rules running at once.
* Rules run every time against a given source event.

#### Source Event Types

Rules can check their input object types against these values to determine actions. These are the types that are supported:

```
    Donation,
    Subscription,
    Resubscription,
    GiftSubscription,
    MultiGiftSubscription,
    Raid,
    ChatCommand
```

#### Source Event Data

All input objects are data objects that contain the following fields that can be checked against:

* Type (of the Source Event Types listed above)
* Name - The user that caused this event to fire
* Channel - The twitch channel this occurred on (tiltify does not pass this data)
* Amount - The numerical amount of whatever was passed.
* Message - Any messages attached (if supported)

#### Example

```
[
  {
    "WorkflowName": "tiltify",
    "Rules": [
      {
        "RuleName": "tank",
        "RuleExpressionType": "LambdaExpression",
        "Expression": "input1.Type == EventType.Donation AND input1.Amount >= 5.00"
      },
      {
        "RuleName": "santa",
        "SuccessEvent": "nothing",
        "RuleExpressionType": "LambdaExpression",
        "Expression": "input1.Type == EventType.Donation AND input1.Amount > 1.00"
      },
      {
        "RuleName": "EveryCoolAndAwesomeRuleName",
        "SuccessEvent": "lootbox",
        "RuleExpressionType": "LambdaExpression",
        "Expression": "input1.Type == EventType.Donation"
      }
    ]
  }
]
```

---

### Server Dependencies

In addition to the modifications made in the L4D2Mods directory, the following additional plugins are needed.

* [Left 4 DHooks Direct](https://forums.alliedmods.net/showthread.php?t=321696) by Silvers.
* [Weapon Handling](https://forums.alliedmods.net/showthread.php?t=319947) by Lux.
* [Survivor Utilities](https://forums.alliedmods.net/showthread.php?t=335683) by Eärendil.
* [Explosive Shots](https://forums.alliedmods.net/showthread.php?t=342301) by Eärendil.
* [multicolors](https://github.com/fbef0102/L4D1_2-Plugins/releases/tag/Multi-Colors) by Bara, et all.
