
using CCRP_Plugin.Models;
using Rocket.API;
using Rocket.Core.Logging;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using SDG.Unturned;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CCRPPlugin.Commands
{
    internal class CCRPVehicleCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;

        public string Name => "v";

        public string Help => "";

        public string Syntax => "[id] ";

        public List<string> Aliases => new List<string> { "vehicle" };

        public List<string> Permissions => new List<string> { "ccrp.v" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            CCRPPlugin.Commands.CommandExceptionHandler.ExecuteSafely(caller, Name, () => ExecuteCore(caller, command));
        }

        private void ExecuteCore(IRocketPlayer caller, string[] command)
        {
            if (!(caller is UnturnedPlayer player))
                return;

            switch (command.Length)
            {
                //Case no amount
                case 1:
                    VehicleAsset vehicleAsset = null;
                    if (ushort.TryParse(command[0], out var assetSearchId))
                        vehicleAsset = Assets.find(EAssetType.VEHICLE, assetSearchId) as VehicleAsset;
                    else
                        vehicleAsset = Assets.find(EAssetType.VEHICLE)?.OfType<VehicleAsset>().FirstOrDefault(vAsset =>
                        {
                            if (string.IsNullOrWhiteSpace(vAsset.vehicleName))
                                return false;

                            return vAsset.vehicleName.IndexOf(command[0], StringComparison.OrdinalIgnoreCase) != -1;
                        });

                    if (vehicleAsset == null)
                    {
                        UnturnedChat.Say(caller, Main.Instance.Translate("VehicleNotFound"), Main.Instance.MessageColor);
                        Logger.Log($"{caller.DisplayName} ({caller.Id}) vehicle not found! (/vehicle)");
                        return;
                    }
                    ushort vehicleId = vehicleAsset.id;

                    VehicleSpawnBlacklist vehicleSpawnBlacklist = null;
                    vehicleSpawnBlacklist = Main.Instance.Blacklists.VehicleSpawnBlacklist.FirstOrDefault(x => x.VehicleId == vehicleId);

                    if (vehicleSpawnBlacklist != null)
                    {
                        if (Main.HasBlacklistBypass(caller, vehicleSpawnBlacklist.Bypass))
                        {
                            player.GiveVehicle(vehicleId);

                            UnturnedChat.Say(caller, Main.Instance.Translate("VehicleSuccess", vehicleAsset.vehicleName, vehicleId), Main.Instance.MessageColor);
                            Logger.Log($"{caller.DisplayName} ({caller.Id}) received vehicle {vehicleAsset.vehicleName} ({vehicleId})!");
                            return;
                        }
                        else
                        {
                            UnturnedChat.Say(caller, Main.Instance.Translate("VehicleNoBypass"), Main.Instance.MessageColor);
                            Logger.Log($"{caller.DisplayName} ({caller.Id}) doesn't have the bypass for {vehicleId}!");
                            return;
                        }
                    }

                    player.GiveVehicle(vehicleId);

                    UnturnedChat.Say(caller, Main.Instance.Translate("VehicleSuccess", vehicleAsset.vehicleName, vehicleId), Main.Instance.MessageColor);
                    Logger.Log($"{caller.DisplayName} ({caller.Id}) received vehicle {vehicleAsset.vehicleName} ({vehicleId})!");
                    return;

                //Case default
                default:
                    UnturnedChat.Say(caller, Main.Instance.Translate("VehicleSyntax"), Main.Instance.MessageColor);
                    Logger.Log($"{caller.DisplayName} ({caller.Id}) Didn't use /vehicle properly!");
                    return;
            }
        }
    }
}