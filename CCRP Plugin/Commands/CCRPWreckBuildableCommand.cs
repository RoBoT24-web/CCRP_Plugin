using CCRP_Plugin;
using CCRP_Plugin.Services;
using Rocket.API;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using SDG.Unturned;
using System.Collections.Generic;
using UnityEngine;
using Logger = Rocket.Core.Logging.Logger;

namespace CCRPPlugin.Commands
{
    internal class CCRPWreckBuildableCommand : IRocketCommand
    {
        private const float MaximumDistance = 8f;

        public AllowedCaller AllowedCaller => AllowedCaller.Player;

        public string Name => "wb";

        public string Help => "Destroy the single structure or barricade you are looking at.";

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
                UnturnedChat.Say(caller, Main.Instance.Translate("WreckBuildableSyntax"), Main.Instance.MessageColor);
                return;
            }

            Transform aim = player.Player.look.aim;
            RaycastInfo info = DamageTool.raycast(new Ray(aim.position, aim.forward), MaximumDistance, RayMasks.BARRICADE | RayMasks.STRUCTURE);
            string name;
            ushort id;
            ulong owner;
            if (info == null || info.transform == null || !WreckService.TryDestroyBuildable(info.transform, out name, out id, out owner))
            {
                UnturnedChat.Say(caller, Main.Instance.Translate("WreckBuildableNotFound", MaximumDistance), Main.Instance.MessageColor);
                return;
            }

            UnturnedChat.Say(caller, Main.Instance.Translate("WreckBuildableDestroyed", name, id), Main.Instance.MessageColor);
            Logger.Log($"{caller.DisplayName} ({caller.Id}) wrecked buildable {name} ({id}) owned by {owner} with /wb.");
            DiscordWebhooks.DiscordWreckWebhook(caller.DisplayName, caller.Id, "/wb", $"Destroyed buildable {name} ({id}) owned by {owner}", $"X: {player.Position.x:F0} Y: {player.Position.y:F0} Z: {player.Position.z:F0}");
        }
    }
}
