# L4D2 Bridge

This is an application and service layer to a L4D2 server, that allows for external events to influence an active server live.

## Features

* Live server remote console
* Robust action handling
* Multiple service event ingesting
* Fairly lightweight
* History recall
* High flexibility with actions
* Raffle Support
* Configurable

### Console Functionality

Anything typed in the text box will be sent directly to the server via RCON, with the following exceptions:

* `reload` - reloads the configuration of the application
* `clear`/`cls` - clears the console log entirely
* `pause`/`unpause`/`resume` - if the test service is running, will pause/unpause the test service from generating events
* `cancel` - cancel all current command events
* `commands` - prints the number of events in the queue
* `raffle <award>` - start a raffle with the given award
* `draw` - picks a winner from all the current entrants
* `help` - prints out these commands directly to the console

## Influence Service Sources

* Tiltify Donations
* Twitch Events

## Setup

### Configuration

All configuration data is stored in a flat file called `config.json`. Upon first time running the application, a blank `config.json` file will be created for you.
For the most part, these settings are fairly straight forward to fill out. The rest of the sections are explained below.

#### Actions

The actions section is a dictionary with key names that correspond to the `RuleName` or `SuccessEvent` of a matching rule definition (see Rules section), and the string array 
of server commands that should be ran when rules with the given keyname matches.

Acceptable server actions are:

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
    SupplyCrate,
    HealAllPlayersSmall,
    HealAllPlayersLarge,
    RespawnAllPlayers,
    UppiesPlayers,
    RandomPositive,
    RandomNegative,
    RandomSpecialInfected,
    Random
```

The `Random` server action does a coin flip and if heads, will run a `RandomPositive` action, if tails, a `RandomNegative` action will execute instead.

##### Example

Here is an example of some actions that are defined in a config file. Names such as "chaos" and "santa" are used in the `rules.json` later.
```
  "Actions": {
    "chaos": [
      "SpawnMobLarge",
      "RandomSpecialInfected",
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

These are the ranges for each type of mob spawn size in the server commands list. The `Rand` setting is when the mob size is not provided (using `SpawnMob`).

#### Negative Weights

These are the weights for each of the negative-based Actions with their weightings from 1-100 on how often they should appear. These are used to calculate what affect is
used when a rule with the action `RandomNegative` is executed. If a negative action is not specified, it will not be randomly chosen when `RandomNegative` is executed. This field is also used to determine `RandomSepcialInfected` roll spawn chances.

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

* Type - the Source Event Types listed above
* Name - The user that caused this event to fire
* Channel - The twitch channel this occurred on (tiltify does not pass this data)
* Amount - The numerical amount of whatever was passed.
* Message - Any messages attached (if supported)


#### Rules Extensions

The rules can take advantage of some added extensions to the processor to do string based checks. A class named `REUtils` is provided to the RulesEngine to help you with string processing.

* `REUtils.HasValue` - Returns a boolean if the given string has an actual value instead of null/whitespace
* `REUtils.CheckContains` - Given the input and a string of a value or csv of values, will check if the input contains any of the values in the check. All values will be projected to case-insensitive checks.

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
        "Expression": "input1.Type == EventType.Donation AND REutils.CheckContains(input1.Message, \"help,santa,save\") == true AND input1.Amount > 1.00"
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
