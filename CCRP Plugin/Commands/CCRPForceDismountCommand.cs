using Rocket.API;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using SDG.Unturned;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace CCRPPlugin.Commands
{
    public sealed class CCRPForceDismountCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Both;
        public string Name => "forcedismount";
        public string Help => "Force a player out of their vehicle.";
        public string Syntax => "<player>";
        public List<string> Aliases => new List<string> { "forced" };
        public List<string> Permissions => new List<string> { "ccrp.forcedismount" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            CommandExceptionHandler.ExecuteSafely(caller, Name, () => ExecuteCore(caller, command));
        }

        private static void ExecuteCore(IRocketPlayer caller, string[] command)
        {
            if (command == null || command.Length != 1)
            {
                UnturnedChat.Say(caller, "Usage: /forcedismount <player>");
                return;
            }

            UnturnedPlayer target = UnturnedPlayer.FromName(command[0]);
            if (target == null || target.Player == null)
            {
                UnturnedChat.Say(caller, Main.Instance.Translate("PlayerNotFound"), Main.Instance.MessageColor);
                return;
            }

            List<InteractableVehicle> vehicles = VehicleManager.vehicles;
            if (vehicles != null)
            {
                foreach (InteractableVehicle vehicle in vehicles)
                {
                    if (vehicle == null || vehicle.isDead)
                    {
                        continue;
                    }

                    byte seat;
                    if (!vehicle.findPlayerSeat(target.Player, out seat))
                    {
                        continue;
                    }

                    MethodInfo forceGetExit = typeof(InteractableVehicle).GetMethod(
                        "forceGetExit",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    if (forceGetExit == null)
                    {
                        throw new MissingMethodException(typeof(InteractableVehicle).FullName, "forceGetExit");
                    }

                    object[] exitArguments = { target.Player, seat, Vector3.zero, (byte)0 };
                    forceGetExit.Invoke(vehicle, exitArguments);
                    Vector3 exitPosition = (Vector3)exitArguments[2];
                    byte exitAngle = (byte)exitArguments[3];
                    VehicleManager.sendExitVehicle(vehicle, seat, exitPosition, exitAngle, true);
                    UnturnedChat.Say(caller, "Forced " + target.DisplayName + " out of their vehicle.", Main.Instance.MessageColor);
                    UnturnedChat.Say(target, "You were forced out of your vehicle.", Main.Instance.MessageColor);
                    return;
                }
            }

            UnturnedChat.Say(caller, target.DisplayName + " is not in a vehicle.", Main.Instance.MessageColor);
        }
    }
}
