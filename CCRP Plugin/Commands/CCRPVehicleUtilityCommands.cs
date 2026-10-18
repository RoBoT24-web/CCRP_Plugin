using Rocket.API;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using CCRP_Plugin.Services;
using SDG.Unturned;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace CCRPPlugin.Commands
{
    public sealed class CCRPRefuelVehicleCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;
        public string Name => "refuelvehicle";
        public string Help => "Refuel the vehicle you are currently in.";
        public string Syntax => "";
        public List<string> Aliases => new List<string>();
        public List<string> Permissions => new List<string> { "ccrp.refuelvehicle" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            UtilityCommandSupport.ExecuteSafely(caller, Name, () =>
            {
                UnturnedPlayer player = caller as UnturnedPlayer;
                if (player == null || command.Length != 0)
                {
                    UnturnedChat.Say(caller, "Usage: /refuelvehicle");
                    return;
                }

                InteractableVehicle vehicle = player.CurrentVehicle;
                if (vehicle == null || vehicle.isDead || !vehicle.usesFuel || !vehicle.isRefillable)
                {
                    UnturnedChat.Say(caller, "You must be in a refuelable vehicle.");
                    return;
                }

                VehicleManager.sendVehicleFuel(vehicle, vehicle.asset.fuelMax);
                UnturnedChat.Say(caller, "Vehicle refueled.");
            });
        }
    }

    public sealed class CCRPRepairVehicleCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;
        public string Name => "repairvehicle";
        public string Help => "Repair the vehicle you are currently in.";
        public string Syntax => "";
        public List<string> Aliases => new List<string>();
        public List<string> Permissions => new List<string> { "ccrp.repairvehicle" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            UtilityCommandSupport.ExecuteSafely(caller, Name, () =>
            {
                UnturnedPlayer player = caller as UnturnedPlayer;
                if (player == null || command.Length != 0)
                {
                    UnturnedChat.Say(caller, "Usage: /repairvehicle");
                    return;
                }

                InteractableVehicle vehicle = player.CurrentVehicle;
                if (vehicle == null || vehicle.isDead)
                {
                    UnturnedChat.Say(caller, "You must be in a vehicle.");
                    return;
                }

                vehicle.askRepair(ushort.MaxValue);
                UnturnedChat.Say(caller, "Vehicle repair requested.");
            });
        }
    }

    public sealed class CCRPSetAttachmentCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;
        public string Name => "setattachment";
        public string Help => "Set an attachment on the held gun, or clear its attachments.";
        public string Syntax => "[id|name]";
        public List<string> Aliases => new List<string>();
        public List<string> Permissions => new List<string> { "ccrp.setattachment" };

        private static readonly string[] AttachmentSlots = { "Sight", "Tactical", "Grip", "Barrel" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            UtilityCommandSupport.ExecuteSafely(caller, Name, () => ExecuteCore(caller, command));
        }

        private static void ExecuteCore(IRocketPlayer caller, string[] command)
        {
            UnturnedPlayer player = caller as UnturnedPlayer;
            if (player == null || command.Length > 1)
            {
                UnturnedChat.Say(caller, "Usage: /setattachment [id|name]");
                return;
            }

            object useable = player.Player.equipment.useable;
            if (useable == null || !useable.GetType().Name.Contains("Gun"))
            {
                UnturnedChat.Say(caller, "Hold a gun first.");
                return;
            }

            if (command.Length == 0)
            {
                bool changed = false;
                foreach (string slot in AttachmentSlots)
                    changed |= InvokeAttachmentChange(useable, slot, 0);

                UnturnedChat.Say(caller, changed ? "Gun attachments cleared." : "Attachment changes are unavailable for this gun.");
                return;
            }

            ItemAsset attachment = FindItemAsset(command[0]);
            if (attachment == null)
            {
                UnturnedChat.Say(caller, "Attachment not found.");
                return;
            }

            string attachmentSlot = GetAttachmentSlot(attachment.GetType().Name);
            if (attachmentSlot == null)
            {
                UnturnedChat.Say(caller, "That item is not a supported gun attachment.");
                return;
            }

            if (!InvokeAttachmentChange(useable, attachmentSlot, attachment.id))
            {
                UnturnedChat.Say(caller, "This gun does not support that attachment change.");
                return;
            }

            UnturnedChat.Say(caller, "Attachment set to " + attachment.name + ".");
        }

        private static ItemAsset FindItemAsset(string search)
        {
            ushort id;
            if (ushort.TryParse(search, NumberStyles.Integer, CultureInfo.InvariantCulture, out id))
                return Assets.find(EAssetType.ITEM, id) as ItemAsset;

            var itemAssets = Assets.find(EAssetType.ITEM);
            if (itemAssets == null)
                return null;

            List<ItemAsset> candidates = itemAssets.OfType<ItemAsset>()
                .Where(asset => !string.IsNullOrWhiteSpace(asset.name))
                .ToList();
            ItemAsset exactMatch = candidates.FirstOrDefault(asset =>
                string.Equals(asset.name, search, StringComparison.OrdinalIgnoreCase));
            if (exactMatch != null)
                return exactMatch;

            candidates = candidates.Where(asset =>
                asset.name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            return candidates.Count == 1 ? candidates[0] : null;
        }

        private static string GetAttachmentSlot(string typeName)
        {
            foreach (string slot in AttachmentSlots)
            {
                if (typeName.IndexOf(slot, StringComparison.OrdinalIgnoreCase) >= 0)
                    return slot;
            }

            return null;
        }

        // Use the installed game's receive method rather than editing serialized item-state bytes.
        // The local Unturned build exposes ReceiveChangeFiremode with this same receive-method pattern.
        private static bool InvokeAttachmentChange(object useable, string slot, ushort id)
        {
            MethodInfo method = useable.GetType().GetMethod(
                "ReceiveChange" + slot,
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[] { typeof(ushort) },
                null);
            if (method == null)
                return false;

            method.Invoke(useable, new object[] { id });
            return true;
        }
    }

    public sealed class CCRPSpeedCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;
        public string Name => "speed";
        public string Help => "Set your movement speed multiplier; no argument resets it.";
        public string Syntax => "[speed]";
        public List<string> Aliases => new List<string>();
        public List<string> Permissions => new List<string> { "ccrp.speed" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            UtilityCommandSupport.ExecuteSafely(caller, Name, () =>
            {
                UnturnedPlayer player = caller as UnturnedPlayer;
                float speed = 1f;
                if (player == null || command.Length > 1 ||
                    (command.Length == 1 &&
                    (!float.TryParse(command[0], NumberStyles.Float, CultureInfo.InvariantCulture, out speed) ||
                     float.IsNaN(speed) || float.IsInfinity(speed) || speed < 0f || speed > 10f)))
                {
                    UnturnedChat.Say(caller, "Usage: /speed [speed] (0-10; 0 freezes movement, no argument resets to 1).");
                    return;
                }

                if (CCRPFreezeManager.IsFrozen(player))
                {
                    UnturnedChat.Say(caller, "You are frozen and cannot change your movement speed.");
                    return;
                }

                if (!UtilityCommandSupport.SetMovementMultiplier(player, "sendPluginSpeedMultiplier", speed))
                {
                    UnturnedChat.Say(caller, "Movement speed control is unavailable.");
                    return;
                }

                UnturnedChat.Say(caller, "Speed multiplier set to " + speed.ToString("0.##", CultureInfo.InvariantCulture) + ".");
            });
        }
    }

    public sealed class CCRPJumpCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;
        public string Name => "jump";
        public string Help => "Teleport to the point you are looking at.";
        public string Syntax => "";
        public List<string> Aliases => new List<string>();
        public List<string> Permissions => new List<string> { "ccrp.jump" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            UtilityCommandSupport.ExecuteSafely(caller, Name, () =>
            {
                UnturnedPlayer player = caller as UnturnedPlayer;
                if (player == null || command.Length != 0)
                {
                    UnturnedChat.Say(caller, "Usage: /jump");
                    return;
                }

                if (TeleportService.IsBlocked(player))
                {
                    return;
                }

                Transform aim = player.Player.look.aim;
                RaycastInfo hit = DamageTool.raycast(
                    new Ray(aim.position, aim.forward),
                    100f,
                    RayMasks.BARRICADE_INTERACT | RayMasks.STRUCTURE_INTERACT);
                if (hit == null || hit.transform == null)
                {
                    UnturnedChat.Say(caller, "No destination found within 100 meters.");
                    return;
                }

                Vector3 destination = hit.point + Vector3.up;
                player.Teleport(destination, player.Rotation);
                UnturnedChat.Say(caller, "Teleported to the point you were looking at.");
            });
        }
    }

    public sealed class CCRPKillCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Both;
        public string Name => "kill";
        public string Help => "Kill yourself or an online player.";
        public string Syntax => "[player]";
        public List<string> Aliases => new List<string>();
        public List<string> Permissions => new List<string> { "ccrp.kill" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            UtilityCommandSupport.ExecuteSafely(caller, Name, () =>
            {
                if (command.Length > 1)
                {
                    UnturnedChat.Say(caller, "Usage: /kill [player]");
                    return;
                }

                UnturnedPlayer target;
                if (command.Length == 0)
                {
                    target = caller as UnturnedPlayer;
                    if (target == null)
                    {
                        UnturnedChat.Say(caller, "Specify an online player.");
                        return;
                    }
                }
                else
                {
                    target = UnturnedPlayer.FromName(command[0]);
                    if (target == null)
                    {
                        UnturnedChat.Say(caller, "Player not found.");
                        return;
                    }
                }

                UnturnedPlayer source = caller as UnturnedPlayer;
                Steamworks.CSteamID killer = source == null ? Steamworks.CSteamID.Nil : source.CSteamID;
                target.Damage(byte.MaxValue, UnityEngine.Vector3.zero, EDeathCause.SUICIDE, ELimb.SKULL, killer);
                UnturnedChat.Say(caller, "Killed " + target.DisplayName + ".");
            });
        }
    }
}
