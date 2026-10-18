using Rocket.API;
using Rocket.Core;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using SDG.Unturned;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace CCRPPlugin.Commands
{
    internal static class UtilityCommandSupport
    {
        public static bool TryGetPlayer(IRocketPlayer caller, string[] args, out UnturnedPlayer player)
        {
            player = null;
            if (args.Length == 0)
            {
                player = caller as UnturnedPlayer;
                if (player == null)
                    UnturnedChat.Say(caller, "This command requires a player target.");
                return player != null;
            }

            if (args.Length != 1)
            {
                UnturnedChat.Say(caller, "Usage: /clearinventory [player]");
                return false;
            }

            player = UnturnedPlayer.FromName(args[0]);
            if (player == null)
                UnturnedChat.Say(caller, "Player not found.");
            return player != null;
        }

        public static bool TryParseRadius(string value, out float radius)
        {
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out radius)
                && !float.IsNaN(radius)
                && !float.IsInfinity(radius)
                && radius > 0f
                && radius <= 500f;
        }

        public static bool SetMovementMultiplier(UnturnedPlayer player, string methodName, float value)
        {
            if (player == null || player.Player == null || player.Player.movement == null)
                return false;

            object movement = player.Player.movement;
            MethodInfo method = movement.GetType().GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[] { typeof(float) },
                null);
            if (method == null)
                return false;

            try
            {
                method.Invoke(movement, new object[] { value });
                return true;
            }
            catch (Exception exception)
            {
                Rocket.Core.Logging.Logger.LogError("Could not apply player movement multiplier: " + exception);
                return false;
            }
        }

        public static void ExecuteSafely(IRocketPlayer caller, string name, System.Action action)
        {
            CommandExceptionHandler.ExecuteSafely(caller, name, action);
        }
    }

    public sealed class CCRPClearCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Both;
        public string Name => "clear";
        public string Help => "Clear dropped items and/or empty vehicles server-wide.";
        public string Syntax => "[-i] [-ev]";
        public List<string> Aliases => new List<string>();
        public List<string> Permissions => new List<string> { "ccrp.clear" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            UtilityCommandSupport.ExecuteSafely(caller, Name, () => ExecuteCore(caller, command));
        }

        private static void ExecuteCore(IRocketPlayer caller, string[] command)
        {
            bool clearItems = command.Length == 0;
            bool clearEmptyVehicles = false;
            foreach (string option in command)
            {
                if (string.Equals(option, "-i", StringComparison.OrdinalIgnoreCase))
                    clearItems = true;
                else if (string.Equals(option, "-ev", StringComparison.OrdinalIgnoreCase))
                    clearEmptyVehicles = true;
                else
                {
                    UnturnedChat.Say(caller, "Usage: /clear [-i] [-ev]");
                    return;
                }
            }

            if (clearItems)
                ItemManager.askClearAllItems();

            int removedVehicles = 0;
            if (clearEmptyVehicles && VehicleManager.vehicles != null)
            {
                foreach (InteractableVehicle vehicle in VehicleManager.vehicles.ToList())
                {
                    if (vehicle == null || vehicle.isDead || !vehicle.isEmpty)
                        continue;

                    VehicleManager.askVehicleDestroy(vehicle);
                    removedVehicles++;
                }
            }

            UnturnedChat.Say(caller, "Clear complete. " +
                (clearItems ? "Dropped items cleared. " : string.Empty) +
                (clearEmptyVehicles ? removedVehicles + " empty vehicles removed." : string.Empty));
        }
    }

    public sealed class CCRPClearInventoryCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Both;
        public string Name => "clearinventory";
        public string Help => "Remove every item from an online player's inventory.";
        public string Syntax => "[player]";
        public List<string> Aliases => new List<string> { "clearinv" };
        public List<string> Permissions => new List<string> { "ccrp.clearinventory" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            UtilityCommandSupport.ExecuteSafely(caller, Name, () => ExecuteCore(caller, command));
        }

        private static void ExecuteCore(IRocketPlayer caller, string[] command)
        {
            UnturnedPlayer target;
            if (!UtilityCommandSupport.TryGetPlayer(caller, command, out target))
                return;

            PlayerInventory inventory = target.Player.inventory;
            int removed = 0;
            for (int pageIndex = 0; pageIndex < inventory.items.Length && pageIndex <= byte.MaxValue; pageIndex++)
            {
                Items page = inventory.items[pageIndex];
                if (page == null)
                    continue;

                for (int itemIndex = page.getItemCount() - 1; itemIndex >= 0; itemIndex--)
                {
                    inventory.removeItem((byte)pageIndex, (byte)itemIndex);
                    removed++;
                }
            }

            UnturnedChat.Say(caller, "Cleared " + removed + " inventory items from " + target.DisplayName + ".");
        }
    }

    public sealed class CCRPOpenDoorCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;
        public string Name => "opendoor";
        public string Help => "Open the door you are looking at.";
        public string Syntax => "";
        public List<string> Aliases => new List<string>();
        public List<string> Permissions => new List<string> { "ccrp.opendoor" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            UtilityCommandSupport.ExecuteSafely(caller, Name, () =>
            {
                UnturnedPlayer player = caller as UnturnedPlayer;
                if (player == null || command.Length != 0)
                {
                    UnturnedChat.Say(caller, "Usage: /opendoor");
                    return;
                }

                Transform aim = player.Player.look.aim;
                RaycastInfo hit = DamageTool.raycast(new Ray(aim.position, aim.forward), 10f, RayMasks.BARRICADE_INTERACT);
                InteractableDoor door = hit == null || hit.transform == null
                    ? null
                    : hit.transform.GetComponentInParent<InteractableDoor>();

                if (door == null || !door.isOpenable || !BarricadeManager.ServerSetDoorOpen(door, true))
                {
                    UnturnedChat.Say(caller, "No openable door found.");
                    return;
                }

                UnturnedChat.Say(caller, "Door opened.");
            });
        }
    }

    public sealed class CCRPOpenStorageCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;
        public string Name => "openstorage";
        public string Help => "Open the storage you are looking at.";
        public string Syntax => "";
        public List<string> Aliases => new List<string>();
        public List<string> Permissions => new List<string> { "ccrp.openstorage" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            UtilityCommandSupport.ExecuteSafely(caller, Name, () =>
            {
                UnturnedPlayer player = caller as UnturnedPlayer;
                if (player == null || command.Length != 0)
                {
                    UnturnedChat.Say(caller, "Usage: /openstorage");
                    return;
                }

                Transform aim = player.Player.look.aim;
                RaycastInfo hit = DamageTool.raycast(new Ray(aim.position, aim.forward), 10f, RayMasks.BARRICADE_INTERACT);
                InteractableStorage storage = hit == null || hit.transform == null
                    ? null
                    : hit.transform.GetComponentInParent<InteractableStorage>();

                if (storage == null || storage.isOpen)
                {
                    UnturnedChat.Say(caller, "No available storage found.");
                    return;
                }

                player.Player.inventory.openStorage(storage);
            });
        }
    }

    public sealed class CCRPRefuelGeneratorCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;
        public string Name => "refuelgenerator";
        public string Help => "Refuel generators within the specified radius.";
        public string Syntax => "<radius>";
        public List<string> Aliases => new List<string>();
        public List<string> Permissions => new List<string> { "ccrp.refuelgenerator" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            UtilityCommandSupport.ExecuteSafely(caller, Name, () => ExecuteCore(caller, command));
        }

        private static void ExecuteCore(IRocketPlayer caller, string[] command)
        {
            UnturnedPlayer player = caller as UnturnedPlayer;
            float radius;
            if (player == null || command.Length != 1 ||
                !UtilityCommandSupport.TryParseRadius(command[0], out radius))
            {
                UnturnedChat.Say(caller, "Usage: /refuelgenerator <radius> (maximum 500).");
                return;
            }

            List<Transform> barricades = new List<Transform>();
            BarricadeManager.getBarricadesInRadius(player.Position, radius, barricades);
            int refueled = 0;
            foreach (Transform transform in barricades)
            {
                if (transform == null)
                    continue;

                InteractableGenerator generator = transform.GetComponentInChildren<InteractableGenerator>();
                if (generator == null || !generator.isRefillable)
                    continue;

                BarricadeManager.sendFuel(generator.transform, generator.capacity);
                refueled++;
            }

            UnturnedChat.Say(caller, "Refueled " + refueled + " generators.");
        }
    }
}
