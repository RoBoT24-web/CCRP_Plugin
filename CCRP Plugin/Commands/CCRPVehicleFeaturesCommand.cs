using CCRP_Plugin.Services;
using Rocket.API;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using SDG.Unturned;
using System;
using System.Collections.Generic;

namespace CCRPPlugin.Commands
{
    internal class CCRPVehicleFeaturesCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;
        public string Name => "vehiclefeatures";
        public string Help => "Enables or disables features on the vehicle you are occupying.";
        public string Syntax => "<autorefuel|autorepair|all> <on|off>";
        public List<string> Aliases => new List<string> { "vehfeatures", "vf" };
        public List<string> Permissions => new List<string> { "ccrp.vehiclefeatures" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            CommandExceptionHandler.ExecuteSafely(caller, Name, () => ExecuteCore(caller, command));
        }

        private static void ExecuteCore(IRocketPlayer caller, string[] command)
        {
            if (!(caller is UnturnedPlayer player) || player.Player == null)
            {
                return;
            }

            bool allFeatures;
            bool autoRefuel;
            bool autoRepair;
            bool enabled;
            if (!TryParse(command, out allFeatures, out autoRefuel, out autoRepair, out enabled))
            {
                UnturnedChat.Say(caller, "Syntax: /vehiclefeatures <autorefuel|autorepair|all> <on|off>");
                return;
            }

            InteractableVehicle vehicle = FindOccupiedVehicle(player.Player);
            if (vehicle == null)
            {
                UnturnedChat.Say(caller, "You must be occupying a vehicle to use this command.");
                return;
            }

            VehicleFeaturesService service = vehicle.GetComponent<VehicleFeaturesService>();
            if (service == null)
            {
                service = vehicle.gameObject.AddComponent<VehicleFeaturesService>();
            }

            service.Initialize(vehicle);

            bool nextAutoRefuel = autoRefuel;
            bool nextAutoRepair = autoRepair;
            if (allFeatures)
            {
                nextAutoRefuel = enabled;
                nextAutoRepair = enabled;
            }
            else
            {
                service.GetFeatures(out bool currentAutoRefuel, out bool currentAutoRepair);
                if (autoRefuel)
                {
                    nextAutoRefuel = enabled;
                    nextAutoRepair = currentAutoRepair;
                }
                else
                {
                    nextAutoRefuel = currentAutoRefuel;
                    nextAutoRepair = enabled;
                }
            }

            service.SetFeatures(nextAutoRefuel, nextAutoRepair);
            UnturnedChat.Say(caller, "Vehicle features for this vehicle are now " +
                DescribeFeatures(nextAutoRefuel, nextAutoRepair) + ".");
        }

        private static bool TryParse(string[] command, out bool allFeatures, out bool autoRefuel, out bool autoRepair, out bool enabled)
        {
            allFeatures = false;
            autoRefuel = false;
            autoRepair = false;
            enabled = false;

            if (command == null || command.Length != 2)
            {
                return false;
            }

            if (string.Equals(command[0], "all", StringComparison.OrdinalIgnoreCase))
            {
                allFeatures = true;
            }
            else if (string.Equals(command[0], "autorefuel", StringComparison.OrdinalIgnoreCase))
            {
                autoRefuel = true;
            }
            else if (string.Equals(command[0], "autorepair", StringComparison.OrdinalIgnoreCase))
            {
                autoRepair = true;
            }
            else
            {
                return false;
            }

            if (string.Equals(command[1], "on", StringComparison.OrdinalIgnoreCase))
            {
                enabled = true;
                return true;
            }

            return string.Equals(command[1], "off", StringComparison.OrdinalIgnoreCase);
        }

        private static InteractableVehicle FindOccupiedVehicle(Player player)
        {
            List<InteractableVehicle> vehicles = VehicleManager.vehicles;
            if (vehicles == null)
            {
                return null;
            }

            for (int i = 0; i < vehicles.Count; i++)
            {
                InteractableVehicle vehicle = vehicles[i];
                if (vehicle == null || vehicle.isDead)
                {
                    continue;
                }

                byte seat;
                if (vehicle.findPlayerSeat(player, out seat))
                {
                    return vehicle;
                }
            }

            return null;
        }

        private static string DescribeFeatures(bool autoRefuel, bool autoRepair)
        {
            if (autoRefuel && autoRepair)
            {
                return "autorefuel and autorepair enabled";
            }
            if (autoRefuel)
            {
                return "autorefuel enabled";
            }
            if (autoRepair)
            {
                return "autorepair enabled";
            }

            return "disabled";
        }
    }
}
