# CCRP Plugin

Command and Permission reference for CCRP plugin.

## Commands

| Command | Description | Permission |
|---|---|---|
| `/ban <name> <reason>` | Permanently bans a player. This is the same as Temp ban just without the duration. | `ccrp.ban` |
| `/ban <name> <reason> <duration>` | Temporarily bans a player. Duration uses an integer followed by `m`, `h`, or `d`, such as `30m` or `2d`. | `ccrp.ban` |
| `/blacklist add <give\|pickup\|both\|vehicle> <id> <bypass-permission>` | Adds an item or vehicle to the selected blacklist in `Blacklists.json`. | `ccrp.blacklist.manage` |
| `/blacklist remove <give\|pickup\|both\|vehicle> <id>` | Removes an item or vehicle from the selected blacklist. | `ccrp.blacklist.manage` |
| `/duty` | Toggles duty status for the first configured duty group the player has permission to use. | `ccrp.duty` and a configured duty-group permission |
| `/editor` | Toggles the in-game object editor (F6). Requires freecam permission aswell. | `CCRPbuild.editor` |
| `/freecam` | Toggles freecam and camera controls (F1-F5). | `CCRPbuild.freecam` |
| `/god [name]` | Toggles Godmode for yourself or, with the additional permission, another online player. | `ccrp.god`; `ccrp.god.others` for another player |
| `/i <id-or-name> [amount]` (`/give`) | Gives an item by ID or name. Item blacklist rules and amount permissions apply. | `ccrp.i`; `ccrp.i.<n>` for the maximum amount, where `<n>` is 1-25 |
| `/inv <name\|Steam64>` | Opens a view-only snapshot of all of an online player's items (hands, backpack, vest, shirt, pants) in your storage window. Edits don't change the target's inventory; items taken from the snapshot are removed from you again. Cannot target yourself. | `ccrp.inv` |
| `/kick <name> <reason>` | Kicks an online player. | `ccrp.kick` |
| `/playtime [name]` | Shows your playtime, or the playtime of an online player. | `ccrp.playtime` |
| `/removewarn <name> <reason>` | Removes one warning from an online player. | `ccrp.removewarn` |
| `/spectate` | Toggles visibility of player names on the map and above players (F7). | `CCRPbuild.spectate` |
| `/teleport <player-or-location>` | Teleports you to an online player or named map location. Alias: `/tp`. | `ccrp.teleport` |
| `/teleport <x> <y> <z>` | Teleports you to coordinates. | `ccrp.teleport` |
| `/tphere <name>` | Teleports an online player to you. | `ccrp.tphere` |
| `/unban <Steam64ID> <reason>` | Unbans a Steam account. | `ccrp.unban` |
| `/v <id-or-name>` (`/vehicle`) | Spawns a vehicle by ID or name. Vehicle blacklist bypass rules apply. Players without the bypass also cannot take the driver seat of blacklisted vehicles (passenger seats stay open once the driver seat is occupied). | `ccrp.v` |
| `/vanish [name]` | Toggles Vanish for yourself or, with the additional permission, another online player. | `ccrp.vanish`; `ccrp.vanish.others` for another player |
| `/warn <name> <reason>` | Adds a warning to an online player. Configured warning thresholds can kick or ban the player. | `ccrp.warn` |
| `/warns [name]` | Shows your warning count and latest active warning reason, or another online player's with the additional permission. | `ccrp.warns`; `ccrp.warns.other` for another player |

## Permission Notes

- To let `/give` grant amounts greater than one, grant the caller the appropriate `ccrp.i.<n>` permission. The number is the maximum amount allowed by that permission, up to 25.
- `CCRPbuild.editor` enables the editor but does not grant permission to move other players' objects. Grant Unturned's `admin.editor.otherobjects` separately if that is intended.
- Blacklists (give, pickup, both, and vehicle) are only bypassed by the entry's exact bypass permission or by `ccrp.bypass.all`. Admin/blue hammer status and the `*` wildcard do not bypass them. `ccrp.bypass.all` bypasses every item and vehicle blacklist.
- Warning thresholds, actions, durations, and reasons are configurable in the `<Warnings>` section of `CCRP Plugin.configuration.xml`.
- The `/blacklist` command changes are saved to `Blacklists.json` and apply immediately.
- Max skills: set `<MaxSkills Enabled="true">` in `CCRP Plugin.configuration.xml` to max all skills when a player joins or respawns. List skills to leave alone under `<IgnoredSkills>` as `<Skill>NAME</Skill>` entries (case-insensitive game names such as `OVERKILL`, `CARDIO`, `CRAFTING`, `ENGINEER`).

## Donation Bypasses

Players can create a personal permission group and claim configured item or vehicle bypass permissions, subject to their donation tiers' combined limit.

| Command | Description | Permission |
|---|---|---|
| `/bypassgroup create <display name> <RRGGBB>` (`/bg`) | Creates your one personal bypass group. The group display name and prefix are set together. The six-digit hex color may optionally start with `#`. | `ccrp.bypassgroup`; requires a configured donation tier |
| `/bypassgroup name <display name>` (`/bg`) | Changes your personal group's display name and prefix. | `ccrp.bypassgroup`; requires your personal bypass group |
| `/bypassgroup color <RRGGBB>` (`/bg`) | Changes your personal group's color. The six-digit hex color may optionally start with `#`. | `ccrp.bypassgroup`; requires your personal bypass group |
| `/bypasses` (`/bypasslist`) | Shows your claimed item and vehicle bypasses in chat. | `ccrp.bypasses`; requires your personal bypass group |
| `/bypassadmin list <Steam64ID>` (`/badm`) | Shows the target player's personal-group bypasses in chat. | `ccrp.bypasses.admin` |
| `/bypassadmin add <Steam64ID> <item\|vehicle> <ID>` | Adds the configured bypass for that item or vehicle to the target player's personal group. | `ccrp.bypasses.admin` |
| `/bypassadmin remove <Steam64ID> <item\|vehicle> <ID>` | Removes that configured bypass from the target player's personal group. | `ccrp.bypasses.admin` |
| `/bypassadmin delete <Steam64ID> confirm` | Deletes the target player's personal bypass group and resets their one-time creation record. Requires the literal `confirm`. | `ccrp.bypasses.admin` |
| `/bypassitem <item ID>` (`/bi`) | Adds an eligible item bypass permission to your personal group. If already in your personal group, starts a removal confirmation. | `ccrp.bypassitem`; requires a configured donation tier and personal bypass group |
| `/bypassitem confirm` / `/bi confirm`; `/bypassitem cancel` / `/bi cancel` | Confirms or cancels a pending item bypass removal. Confirmation expires after 30 seconds. | `ccrp.bypassitem` |
| `/bypassvehicle <vehicle ID>` (`/bv`) | Adds an eligible vehicle bypass permission to your personal group. If already in your personal group, starts a removal confirmation. | `ccrp.bypassvehicle`; requires a configured donation tier and personal bypass group |
| `/bypassvehicle confirm` / `/bv confirm`; `/bypassvehicle cancel` / `/bv cancel` | Confirms or cancels a pending vehicle bypass removal. Confirmation expires after 30 seconds. | `ccrp.bypassvehicle` |

Configure `<DonationBypasses>` in `CCRP Plugin.configuration.xml`. `<Tiers>` is a list of donation permission group IDs, highest tier first; each `<Tier GroupId="..." MaxBypasses="..."/>` sets that tier's bypass allowance. A player's total claim limit is the sum of `MaxBypasses` for every configured tier group they belong to. The first matching tier in the list is used as the personal group's parent. For example, a player in both configured groups below can claim up to 7 distinct bypasses (5 + 2):

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
</DonationBypasses>
```

Claimable items come only from `GiveAndPickupItemBlacklist`; IDs present in either `GiveItemBlacklist` or `PickupItemBlacklist` are always excluded. Claimable vehicles come from `VehicleSpawnBlacklist`. Each entry has its own bypass permission, and `DeniedPermissions` excludes exact matching permission names (case-insensitive) from either category; it does not support prefixes or wildcards. For example, denying `ex.bypass` does not deny `ex.bypass.36547`; list every full permission you want excluded.

The item/vehicle claim commands take numeric IDs and use distinct aliases. Claimed item and vehicle bypasses share the combined tier allowance. Re-entering an ID already in your personal group prompts you to confirm removal; confirmation removes only that personal-group permission. Item and vehicle confirmations are separate, and a bypass inherited from another permission group cannot be removed this way (that group may continue granting it). Group colors use six hexadecimal digits, with or without a leading `#` (for example, `33AAFF` or `#33AAFF`); group display names are limited to 24 characters and cannot contain angle brackets. Each player's one-time group creation is recorded in `DonationBypassGroups.json`.

Staff can use `/bypassadmin` to manage registered CCRP personal bypass groups by the owner's Steam64 ID. Add/remove takes an item or vehicle ID from the configured blacklists; list shows configured bypass items and vehicles. Deletion removes the personal group and clears the creation record, allowing the player to create a new group later if eligible. These actions do not edit unrelated Rocket permission groups.

To log bypass command invocations to Discord, set `<Bypass>` under `<Webhooks>` in `CCRP Plugin.configuration.xml` to a Discord webhook URL. Logs include the player, entered command, and outcome (success, invalid syntax, or the failure message, such as an already-owned bypass).

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