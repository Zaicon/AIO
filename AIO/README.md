# AIO
![Version 2.0.0](https://img.shields.io/badge/Version-2.0.0-blue.svg)
![API 2.0](https://img.shields.io/badge/API-2.0-green.svg)

Introduction
-----
This plugin provides various commands in one package.

Commands
-----
#### StaffChat Commands
`/sinvite <player>`<br />
Allows the specified player to participate in staffchat.

`/skick <player>`<br />
Kicks the specified player from staffchat (if applicable).

`/slist`<br />
Lists all invited staffchat participants.

`/sclear`<br />
Kicks all invited staffchat participants.

#### Report Commands
`/reportgrief`
`/rg`<br />
_Cannot use from Console_<br />
Reports a location of grief.

`/listgrief [all]`
`/lg [all]`<br />
Lists all reported locations of grief.

`/checkgrief`
`/cg`<br />
_Cannot use from Console_<br />
Teleports user to the oldest reported location of grief and removes it from the list.

`/building`<br />
_Cannot use from Console_<br />
Reports a location for building protection.

`/listbuilding [all]`
`/lb [all]`<br />
Lists all reported locations for building protections.

`/checkbuilding`
`/cb`<br />
_Cannot use from Console_<br />
Teleports user to the oldest reported location for building protection and removes it from the list.

#### Misc Commands
`/staff`<br />
Lists all online staff members.

`/freeze <player>`<br />
Toggles whether or not the specified player is disabled.

`/read <inventory/buff/armor/dye> <player>`<br />
Displays a list of the specified item type that is in the specified player's inventory.

`/copy <inventory/buff/armor/dye> <player>`<br />
_Cannot use from Console_<br />
Gives you an identical set of items as the specified player (or gives you an indentical set of buffs).

`/gen <shroompatch/islandhouse/island/dungeon/minehouse/hive/cloudisland/temple/hellfort/hellhouse/mountain/pyramid/crimson/trees/cloudlake/livingtree/softice/mayantrap>`<br />
_Cannot use from Console_<br />
Attempts to generate the specified biome/structure at the user's current location.

Permissions
-----
`aio.staffchat.chat`<br />
Allows player to participate in staffchat.

`aio.staffchat.admin`<br />
Allows use of: `/sinvite` `/skick` `/sclear`

`aio.checkgrief`<br />
Allows use of: `/checkgrief`

`aio.listgrief`<br />
Allows use of: `/listgrief`

`aio.checkbuilding`<br />
Allows use of: `/checkbuilding`

`aio.listbuilding`<br />
Allows use of: `/listbuilding`

`aio.freeze`<br />
Allows use of: `/freeze`

`aio.read`<br />
Allows use of: `/read`

`aio.copy`<br />
Allows use of: `/copy`

`aio.worldgen`<br />
Allows use of: `/gen`