# CCRP Plugin

Command and Permission reference for CCRP plugin.

## Commands

| Command | Description | Permission |
|---|---|---|
| `/ban <name> <reason>` | Permanently bans a player. | `ccrp.ban` |
| `/ban <name> <reason> <duration>` | Temporarily bans a player (`10s`, `30m`, `2h`, `1d`). | `ccrp.ban` |
| `/back` | Returns to your most recent death location. | `ccrp.back` |
| `/blacklist add <give\|pickup\|both\|vehicle> <id> <bypass-permission>` | Adds an item or vehicle blacklist entry. | `ccrp.blacklist.manage` |
| `/blacklist remove <give\|pickup\|both\|vehicle> <id>` | Removes a blacklist entry. | `ccrp.blacklist.manage` |
| `/clear [-i] [-ev]` | Clears dropped items (`-i`; default) and/or empty vehicles (`-ev`). | `ccrp.clear` |
| `/clearinventory [player]` (`/clearinv`) | Clears your or a player's inventory, including worn items. | `ccrp.clearinventory` |
| `/duty` | Toggles your configured staff duty group. | `ccrp.duty` and a configured duty-group permission |
| `/editor` | Toggles the F6 object editor. | `CCRPbuild.editor` |
| `/ck <entry\|yes\|no> [arguments]` | Starts a configured vote or casts a ballot. Some votes also require `ccrp.ck.<alias-or-name>`. | `ccrp.ck` |
| `/firemode <safety\|semi\|auto\|burst>` | Changes the held gun's firemode. | `ccrp.firemode` |
| `/freecam` | Toggles freecam controls (F1-F5). | `CCRPbuild.freecam` |
| `/freeze [player]` | Freezes a player until unfrozen or either party disconnects. | `ccrp.freeze` |
| `/forcedismount <player>` (`/forced`) | Forces a player out of their vehicle. | `ccrp.forcedismount` |
| `/god [name]` | Toggles Godmode for you or another player. | `ccrp.god`; `ccrp.god.others` for another player |
| `/delhome <name>` | Deletes one of your homes. | `ccrp.delhome` |
| `/delwarp <name>` | Deletes a shared warp. | `ccrp.delwarp` |
| `/home <name>` | Teleports to a home; moving cancels the delay. | `ccrp.home` (5s) or `ccrp.home.<n>` (0-300s; lowest applies) |
| `/homes` | Lists your homes and limit. | `ccrp.homes` |
| `/if <autoreload\|autorepair\|all> <on\|off>` (`/itemfeatures`) | Toggles reloading and/or repair for the held item or vehicle weapon. | `ccrp.itemfeatures` |
| `/sethome <name>` | Saves or updates a home. | `ccrp.sethome` (1 home) or `ccrp.sethome.<n>` (1-100; highest applies) |
| `/i <id-or-name> [amount]` (`/give`) | Gives an item, subject to blacklist and amount limits. | `ccrp.i`; `ccrp.i.<n>` (maximum amount, up to 25) |
| `/inv <name\|Steam64>` | Opens a read-only inventory snapshot of an online player. | `ccrp.inv` |
| `/jump` | Teleports to the point you're looking at. | `ccrp.jump` |
| `/kill [player]` | Kills you or an online player. | `ccrp.kill` |
| `/kick <name> <reason>` | Kicks an online player. | `ccrp.kick` |
| `/msg <player> <message>` (`/tell`, `/pm`) | Sends a private message. | `ccrp.tell` |
| `/openstorage` | Opens the storage you are looking at. | `ccrp.openstorage` |
| `/opendoor` | Opens the door you are looking at. | `ccrp.opendoor` |
| `/playtime [name]` | Shows your or another player's playtime. | `ccrp.playtime` |
| `/r <message>` (`/reply`) | Replies to your latest private message. | `ccrp.reply` |
| `/refuelgenerator <radius>` | Refuels nearby generators (max 500m). | `ccrp.refuelgenerator` |
| `/refuelvehicle` | Refuels your current vehicle. | `ccrp.refuelvehicle` |
| `/repairvehicle` | Repairs your current vehicle. | `ccrp.repairvehicle` |
| `/removewarn <name> <reason>` | Removes a warning. | `ccrp.removewarn` |
| `/setattachment [id\|name]` | Forces an attachment onto the held gun regardless of its normal requirements; no argument clears attachments. | `ccrp.setattachment` |
| `/speed [speed]` | Sets run speed (0-10; default 1). | `ccrp.speed` |
| `/spectate` | Toggles player markers (F7). | `CCRPbuild.spectate` |
| `/spypm` | Toggles private-message monitoring. | `ccrp.spypm` |
| `/sudo <player\|*\|*console*> <command>` | Runs a command as a player, everyone, or console. | `ccrp.sudo` |
| `/teleport <player-or-location>` (`/tp`) | Teleports to a player or map location. | `ccrp.teleport` |
| `/teleport <x> <y> <z>` | Teleports to coordinates. | `ccrp.teleport` |
| `/tpa <player\|accept\|deny\|cancel>` | Sends or manages teleport requests (expire in 60s; teleport after 5s, cancelled by movement). | `ccrp.tpa` |
| `/tphere <name>` | Teleports a player to you. | `ccrp.tphere` |
| `/uncuff <player>` | Removes a player's handcuffs. | `ccrp.uncuff` |
| `/unfreeze [player]` | Unfreezes a player or yourself. | `ccrp.unfreeze` |
| `/warp <name>` | Teleports to a shared warp (5s; movement cancels). | `ccrp.warp` |
| `/setwarp <name>` | Creates or updates a shared warp. | `ccrp.setwarp` |
| `/warps` | Lists shared warps. | `ccrp.warps` |
| `/unban <Steam64ID> <reason>` | Unbans a Steam account. | `ccrp.unban` |
| `/v <id-or-name>` (`/vehicle`) | Spawns a vehicle, subject to blacklist rules. | `ccrp.v` |
| `/vehfeatures <autorefuel\|autorepair\|all> <on\|off>` (`/vehiclefeatures`, `/vf`) | Toggles refuelling and/or repair for your current vehicle. | `ccrp.vehiclefeatures` |
| `/vanish [name]` | Toggles your or another player's vanish. | `ccrp.vanish`; `ccrp.vanish.others` for another player |
| `/warn <name> <reason>` | Warns an online player. | `ccrp.warn` |
| `/warns [name]` | Shows your warnings or another player's. | `ccrp.warns`; `ccrp.warns.other` for another player |
| `/wreck scan <filter\|itemID> <radius> [username\|Steam64]` | Counts matching structures, barricades and vehicles without removing them. Use an exact asset ID to scan by ID. | `ccrp.wreck` |

## Permission Notes

- Plugin-owned JSON files are stored in the plugin's `Data` folder. On its first startup after this update, existing top-level `.json` files are copied there only when the matching destination file does not already exist; the originals are retained as backups.
- `/back` death locations are saved only for players with `ccrp.back`, expire after 60 seconds, and are consumed when used. They are stored by Steam64 ID in `Data\DeathLocations.json`.
- To let `/give` grant amounts greater than one, grant the caller the appropriate `ccrp.i.<n>` permission. The number is the maximum amount allowed by that permission, up to 25.
- Homes are stored per player in `Homes.json`. A player needs only one of `ccrp.sethome` or `ccrp.sethome.<n>` to use `/sethome`, and only one of `ccrp.home` or `ccrp.home.<n>` to use `/home`.
- Rocket.Unturned has its own built-in `/home` (bed teleport). If it takes precedence on your server, disable it with `<Command Name="home" Enabled="false" />` in `Rocket\Commands.config.xml`.
- `CCRPbuild.editor` (with `CCRPbuild.freecam`) enables the editor, but the plugin only lets players move barricades and structures they own. Moving another player's buildables, or any buildable that can't be resolved, is denied and resets the requested transform to the buildable's current server-side position and rotation unless the player's effective Rocket permissions include the exact `admin.editor.otherobjects` grant. Rocket's automatic admin or wildcard permission result alone does not grant this override.
- Blacklists (give, pickup, both, and vehicle) are only bypassed by the entry's exact bypass permission or by `ccrp.bypass.all`. Admin/blue hammer status and the `*` wildcard do not bypass them. `ccrp.bypass.all` bypasses every item and vehicle blacklist.
- Warning thresholds, actions, durations, and reasons are configurable in the `<Warnings>` section of `CCRP Plugin.configuration.xml`.
- The `/blacklist` command changes are saved to `Blacklists.json` and apply immediately.
- Max skills: set `<MaxSkills Enabled="true">` in `CCRP Plugin.configuration.xml` to max all skills when a player joins or respawns. List skills to leave alone under `<IgnoredSkills>` as `<Skill>NAME</Skill>` entries (case-insensitive game names such as `OVERKILL`, `CARDIO`, `CRAFTING`, `ENGINEER`).
- Item and vehicle automation is scoped to the exact held item or spawned vehicle and does not persist across restarts. It does not consume fuel, repair items or magazines.
- Votes and private-message formatting are configured in `CCRP Plugin.configuration.xml`. A vote with `RequirePermission="true"` additionally requires `ccrp.ck.<alias-or-name>`; false lets anyone with `/ck` access start that vote.
- Example vote and private-message configuration:

```xml
<PrivateMessages Enabled="true">
  <FormatFrom>(From {0}): {1}</FormatFrom>
  <FormatTo>(To {0}): {1}</FormatTo>
  <FormatSpy>&lt;gray&gt;Spy: {0} -&gt; {1}: {2}</FormatSpy>
  <ConsoleDisplayName>*console*</ConsoleDisplayName>
</PrivateMessages>
<Votes Enabled="true">
  <Vote Name="Day" Alias="d" Command="/day" MinimumArguments="0" Color="#fff333" Icon=""
        Enabled="true" MinimumPlayers="1" RequiredPercent="50" Timer="60"
        CooldownTime="600" RequirePermission="false" />
</Votes>
```
- The auto-announcer sends one non-empty `<Message>` per interval, rotates through the list and saves its `LastIndex` in the plugin configuration. Example:

```xml
<AutoAnnouncer Enabled="true" Interval="720" LastIndex="0">
  <Message>
    <Text>Welcome to the server!</Text>
    <Icon></Icon>
  </Message>
</AutoAnnouncer>
```

## Donation Bypasses

Players can create a personal permission group and claim configured item or vehicle bypass permissions, subject to their donation tiers' combined limit.

All players can also buy permanent bypasses from the administrator-maintained shop list. Purchases are placed in the player's personal Rocket permission group; if they do not have one, the plugin creates a plain group automatically. Shop-only players do not need `/bypassgroup`. If they later qualify for a donation tier, their existing group and purchased permissions are retained, and `/bypassgroup create` can adopt and customize that shop-created group. The command refuses to create a second personal group and reports when one already exists. Paid purchases do not use donor free-claim slots and cannot be removed using player free-claim commands. A bypass already granted by a free claim, another permission group, or `ccrp.bypass.all` cannot be purchased again.

### For all players

Players need `ccrp.bypass.shop` for these shop and listing commands. Grant it to the default/player group in Rocket's permissions configuration for regular player access.

| Command | Description | Permission |
|---|---|---|
| `/bypasses` (`/bypasslist`) | Shows your purchased and claimed item and vehicle bypasses in chat. | `ccrp.bypass.shop` |
| `/bypassshop` (`/bshop`) | Lists item and vehicle bypass offers currently available for purchase with in-game money. | `ccrp.bypass.shop` |
| `/bypasscost <ID>` | Shows the price for the listed item or vehicle bypass offer with that ID. If both item and vehicle offers use the ID, specify `item` or `vehicle`. | `ccrp.bypass.shop` |
| `/bypassbuy <item\|vehicle> <ID>` (`/bbuy`) | Purchases one listed bypass offer with in-game money. | `ccrp.bypass.shop` |

### For donators

These claim and personal-group commands require the listed bypass permission; commands marked with donation-tier eligibility also require a configured donation tier.

| Command | Description | Permission |
|---|---|---|
| `/bypassgroup create <display name> <RRGGBB>` (`/bg`) | Creates your one personal bypass group, or adopts and customizes your existing shop-created group. An already-created personal group is not created again. The group display name and prefix are set together. The six-digit hex color may optionally start with `#`. | `ccrp.bypassgroup`; requires a configured donation tier |
| `/bypassgroup name <display name>` (`/bg`) | Changes your personal group's display name and prefix. | `ccrp.bypassgroup`; requires your personal bypass group |
| `/bypassgroup color <RRGGBB>` (`/bg`) | Changes your personal group's color. The six-digit hex color may optionally start with `#`. | `ccrp.bypassgroup`; requires your personal bypass group |
| `/bypassitem <item ID>` (`/bi`) | Adds an eligible item bypass permission to your personal group. If already in your personal group, starts a removal confirmation. | `ccrp.bypassitem`; requires a configured donation tier and personal bypass group |
| `/bypassitem confirm` / `/bi confirm`; `/bypassitem cancel` / `/bi cancel` | Confirms or cancels a pending item bypass removal. Confirmation expires after 30 seconds. | `ccrp.bypassitem` |
| `/bypassvehicle <vehicle ID>` (`/bv`) | Adds an eligible vehicle bypass permission to your personal group. If already in your personal group, starts a removal confirmation. | `ccrp.bypassvehicle`; requires a configured donation tier and personal bypass group |
| `/bypassvehicle confirm` / `/bv confirm`; `/bypassvehicle cancel` / `/bv cancel` | Confirms or cancels a pending vehicle bypass removal. Confirmation expires after 30 seconds. | `ccrp.bypassvehicle` |

### For admins

| Command | Description | Permission |
|---|---|---|
| `/bypassadmin list <Steam64ID>` (`/badm`) | Shows the target player's personal-group bypasses in chat. | `ccrp.bypasses.admin` |
| `/bypassadmin add <Steam64ID> <item\|vehicle> <ID>` | Adds the configured bypass for that item or vehicle to the target player's personal group. | `ccrp.bypasses.admin` |
| `/bypassadmin remove <Steam64ID> <item\|vehicle> <ID>` | Removes that configured bypass from the target player's personal group. | `ccrp.bypasses.admin` |
| `/bypassadmin delete <Steam64ID> confirm` | Deletes the target player's personal bypass group and resets their one-time creation record. Requires the literal `confirm`. | `ccrp.bypasses.admin` |

Configure `<DonationBypasses>` in `CCRP Plugin.configuration.xml`. `<Tiers>` is a list of donation permission group IDs, highest tier first; each `<Tier GroupId="..." MaxBypasses="..."/>` sets that tier's bypass allowance. A player's total claim limit is the sum of `MaxBypasses` for every configured tier group they belong to. The first matching tier in the list is used as the personal group's parent. For example, a player in both configured groups below can claim up to 7 distinct free bypasses (5 + 2):

```xml
<DonationBypasses>
  <Tiers>
    <Tier GroupId="supporter_gold" MaxBypasses="5" />
    <Tier GroupId="supporter_silver" MaxBypasses="2" />
  </Tiers>
  <DeniedPermissions>
    <Permission>bypass.invis</Permission>
    <Permission>bypass.glitched</Permission>
  </DeniedPermissions>
  <MaxPaidBypasses>5</MaxPaidBypasses>
</DonationBypasses>
```

`MaxPaidBypasses` limits each player to this many distinct paid bypass permissions, separately from donor free claims. It defaults to 5; set it to `0` to disable paid bypass purchases. A bypass already granted through donation or another permission group does not count as a paid purchase.

Claimable items come only from `GiveAndPickupItemBlacklist`; IDs present in either `GiveItemBlacklist` or `PickupItemBlacklist` are always excluded. Claimable vehicles come from `VehicleSpawnBlacklist`. Each entry has its own bypass permission, and `DeniedPermissions` excludes exact matching permission names (case-insensitive) from either category; it does not support prefixes or wildcards. For example, denying `ex.bypass` does not deny `ex.bypass.36547`; list every full permission you want excluded.

The shop catalog is stored separately in `Data\BypassShop.json` and is created with an empty offer list if it does not exist. Add offers using this format; `Type` is `Item` or `Vehicle`, `AssetId` is an Unturned ID, and `Cost` is a positive in-game-money amount with at most two decimal places:

```json
{
  "Offers": [
    { "Type": "Item", "AssetId": 363, "Cost": 1000.00 },
    { "Type": "Vehicle", "AssetId": 123, "Cost": 5000.00 }
  ]
}
```

Each offer is checked against `Data\Blacklists.json` when listed or purchased. An item ID must resolve to one unambiguous bypass permission across active item blacklist entries; conflicting permissions make that offer unavailable. Vehicles are matched by exact vehicle ID. Permissions listed in `<DonationBypasses><DeniedPermissions>` cannot be sold. Purchased ownership is tracked by Steam64 ID in `Data\BypassPurchases.json`; do not edit that file manually. Use `/bypasscost <ID>` to check a price before purchase; when the same ID is offered for both an item and vehicle, specify `item` or `vehicle` in the command.

The shop uses the AdvancedEconomy API when available and falls back to the legacy Uconomy API. For AdvancedEconomy installations, install its Uconomy compatibility bridge (`UconomyToAdvancedEconomy`) if the direct API is not exposed. The plugin reports an error instead of charging players when no supported provider is available or a balance change cannot be verified.

The item/vehicle claim commands take numeric IDs and use distinct aliases. Claimed item and vehicle bypasses share the combined tier allowance. Re-entering an ID already in your personal group prompts you to confirm removal; confirmation removes only that personal-group permission. Item and vehicle confirmations are separate, and a bypass inherited from another permission group cannot be removed this way (that group may continue granting it). Group colors use six hexadecimal digits, with or without a leading `#` (for example, `33AAFF` or `#33AAFF`); group display names are limited to 24 characters and cannot contain angle brackets. Each player's one-time group creation is recorded in `DonationBypassGroups.json`.

To log bypass command invocations to Discord, set `<Bypass>` under `<Webhooks>` in `CCRP Plugin.configuration.xml` to a Discord webhook URL. Logs include the player, entered command, and outcome.

## Wrecking Ball

The `/wreck` and `/wreckitem` commands queue nearby structures, barricades, and vehicles for destruction. Without a user filter, they match targets owned by anyone. Review the targets, then confirm or abort the request.

| Command | Description |
|---|---|
| `/wreck <filter> <radius> [username\|Steam64]` | Queue targets matching the category filter within the radius. The optional final username or Steam64 ID limits matches to that owner's buildables or locked vehicles. |
| `/wreck scan <filter> <radius> [username\|Steam64]` | Preview how many targets match without queuing them. |
| `/wreckitem <id[,id...]> <radius> [username\|Steam64]` | Queue targets matching one or more exact asset IDs within the radius. |
| `/wreckitem scan <id[,id...]> <radius> [username\|Steam64]` | Preview targets matching the exact asset IDs. |
| `/wreck confirm` | Start destroying the queued targets. |
| `/wreck abort` | Cancel a queued or active request. |
| `/whois` | Show the owner name and Steam64 ID of the structure or barricade you are looking at (within 8 meters). Use `last` as the owner in `/wreck` to target that player. |
| `/wb` | Destroy the single structure or barricade you are looking at (within 8 meters, no confirmation). |
| `/wv` | Destroy the single vehicle you are looking at (within 10 meters, no confirmation). |

**Permission:** `ccrp.wreck`

**Logging:** set `<Wreck>` under `<Webhooks>` in the config to a Discord webhook URL to log every `/wreck`, `/wreckitem`, `/wb`, and `/wv` use (player, command, result, location).

Filters use the category letters `b` (bed), `t` (trap), `d` (door), `c` (container), `l` (ladder), `w` (wall/window), `f` (floor), `p` (pillar), `r` (roof), `s` (stairs), `g` (guard), `i` (illumination), `a` (agriculture), `v` (vehicle), or `*` (all categories).

Radius is limited to 200 meters and each request to 2,000 targets.
