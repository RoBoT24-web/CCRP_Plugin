using CCRP_Plugin;
using Rocket.API;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using SDG.Unturned;
using System.Collections.Generic;
using UnityEngine;
using Logger = Rocket.Core.Logging.Logger;

namespace CCRPPlugin.Commands
{
    internal class CCRPWreckVehicleCommand : IRocketCommand
    {
        private const float MaximumDistance = 10f;

        public AllowedCaller AllowedCaller => AllowedCaller.Player;

        public string Name => "wv";

        public string Help => "Destroy the single vehicle you are looking at.";

        public string Syntax => "";

        public List<string> Aliases => new List<string>();

        public List<string> Permissions => new List<string> { "ccrp.wreck" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            CommandExceptionHandler.ExecuteSafely(caller, Name, () => ExecuteCore(caller, command));
        }

        private static void ExecuteCore(IRocketPlayer caller, string[] command)
        {
            UnturnedPlayer player = caller as UnturnedPlayer;
            if (player == null)
            {
                return;
            }

            if (command.Length != 0)
            {
                UnturnedChat.Say(caller, Main.Instance.Translate("WreckVehicleSyntax"), Main.Instance.MessageColor);
                return;
            }

            Transform aim = player.Player.look.aim;
            RaycastInfo info = DamageTool.raycast(new Ray(aim.position, aim.forward), MaximumDistance, RayMasks.VEHICLE);
            InteractableVehicle vehicle = info != null ? (info.vehicle ?? DamageTool.getVehicle(info.transform)) : null;

            if (vehicle == null || vehicle.isDead)
            {
                UnturnedChat.Say(caller, Main.Instance.Translate("WreckVehicleNotFound", MaximumDistance), Main.Instance.MessageColor);
                return;
            }

            string name = vehicle.asset != null ? vehicle.asset.vehicleName : "Unknown";
            ushort id = vehicle.id;
            VehicleManager.askVehicleDestroy(vehicle);
            UnturnedChat.Say(caller, Main.Instance.Translate("WreckVehicleDestroyed", name, id), Main.Instance.MessageColor);
            Logger.Log($"{caller.DisplayName} ({caller.Id}) wrecked vehicle {name} ({id}) with /wv.");
            DiscordWebhooks.DiscordWreckWebhook(caller.DisplayName, caller.Id, "/wv", $"Destroyed vehicle {name} ({id})", $"X: {player.Position.x:F0} Y: {player.Position.y:F0} Z: {player.Position.z:F0}");
        }
    }
}
