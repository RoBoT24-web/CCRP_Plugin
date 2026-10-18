using CCRP_Plugin;
using CCRP_Plugin.Services;
using Rocket.API;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using SDG.Unturned;
using Steamworks;
using System.Collections.Generic;
using UnityEngine;

namespace CCRPPlugin.Commands
{
    internal class CCRPWhoisCommand : IRocketCommand
    {
        private const float MaximumDistance = 8f;

        public AllowedCaller AllowedCaller => AllowedCaller.Player;

        public string Name => "whois";

        public string Help => "Show the owner of the structure or barricade you are looking at.";

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
                UnturnedChat.Say(caller, Main.Instance.Translate("WhoisSyntax"), Main.Instance.MessageColor);
                return;
            }

            Transform aim = player.Player.look.aim;
            RaycastInfo info = DamageTool.raycast(new Ray(aim.position, aim.forward), MaximumDistance, RayMasks.BARRICADE | RayMasks.STRUCTURE);
            string name;
            ushort id;
            ulong owner;
            if (info == null || info.transform == null || !WreckService.TryGetBuildableOwner(info.transform, out name, out id, out owner))
            {
                UnturnedChat.Say(caller, Main.Instance.Translate("WreckBuildableNotFound", MaximumDistance), Main.Instance.MessageColor);
                return;
            }

            SteamPlayer owningPlayer;
            string ownerName = PlayerTool.tryGetSteamPlayer(new CSteamID(owner).ToString(), out owningPlayer) && owningPlayer.player != null
                ? owningPlayer.playerID.characterName
                : "Offline";

            Main.Instance.WreckService.SetLastLookup(player.CSteamID.m_SteamID, owner);
            UnturnedChat.Say(caller, Main.Instance.Translate("WhoisResult", name, id, ownerName, owner), Main.Instance.MessageColor);
        }
    }
}
