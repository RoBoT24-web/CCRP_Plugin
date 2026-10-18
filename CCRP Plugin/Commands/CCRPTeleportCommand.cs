using CCRPPlugin;
using Rocket.API;
using Rocket.Core.Logging;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using SDG.Unturned;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Logger = Rocket.Core.Logging.Logger;

namespace CCRP_Plugin.Commands
{
    internal class CCRPTeleportCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;

        public string Name => "teleport";

        public string Help => "";

        public string Syntax => "<name>";

        public List<string> Aliases => new List<string> { "tp"};

        public List<string> Permissions => new List<string> { "ccrp.teleport" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            CCRPPlugin.Commands.CommandExceptionHandler.ExecuteSafely(caller, Name, () => ExecuteCore(caller, command));
        }

        private void ExecuteCore(IRocketPlayer caller, string[] command)
        {
            UnturnedPlayer player = (UnturnedPlayer)caller;
            switch (command.Length)
            {
                case 1:
                    UnturnedPlayer otherPlayer = UnturnedPlayer.FromName(command[0]);
                    if(otherPlayer == null)
                    {
                        Node item = LevelNodes.nodes.Where(n => n.type == ENodeType.LOCATION && ((LocationNode)n).name.ToLower().Contains(command[0].ToLower())).FirstOrDefault();
                        if (item != null)
                        {
                            Vector3 c = item.point + new Vector3(0f, 0.5f, 0f);
                            player.Teleport(c, 90f);
                            UnturnedChat.Say(caller, Main.Instance.Translate("TelepoortToCitySuccess", ((LocationNode)item).name), Main.Instance.MessageColor);
                            Logger.Log($"{caller.DisplayName} ({caller.Id}) teleported himself to {((LocationNode)item).name}");
                            DiscordWebhooks.DiscordTeleportWebhook(caller.DisplayName, caller.Id, "city", ((LocationNode)item).name);
                            return;
                        }
                        UnturnedChat.Say(caller, Main.Instance.Translate("TeleportFailedToFindDest"), Main.Instance.MessageColor);
                        Logger.Log($"{caller.DisplayName} ({caller.Id}) didn't find a destination (/teleport)!");
                        return;
                    }
                        player.Player.teleportToPlayer(otherPlayer.Player);

                        string playerX = otherPlayer.Position.x.ToString();
                        string playerY = otherPlayer.Position.y.ToString();
                        string playerZ = otherPlayer.Position.z.ToString();

                        UnturnedChat.Say(caller, Main.Instance.Translate("TeleportToPlayerSuccess", otherPlayer.DisplayName), Main.Instance.MessageColor);
                        Logger.Log($"{caller.DisplayName} ({caller.Id}) teleported to {otherPlayer.DisplayName} ({otherPlayer.Id}) [{playerX} {playerY} {playerZ}]!");
                        DiscordWebhooks.DiscordTeleportWebhook(caller.DisplayName, caller.Id, "player", otherPlayer.DisplayName, otherPlayer.Id, playerX, playerY, playerZ);
                    return;

                case 3:
                    float x = Convert.ToInt32(command[0]);
                    float y = Convert.ToInt32(command[1]);
                    float z = Convert.ToInt32(command[2]);

                    UnityEngine.Vector3 xyz = new UnityEngine.Vector3(x, y, z);
                    player.Player.teleportToLocationUnsafe(xyz, 90);

                    UnturnedChat.Say(caller, Main.Instance.Translate("TeleportCoordinatesSuccess", x, y, z), Main.Instance.MessageColor);
                    Logger.Log($"{caller.DisplayName} ({caller.Id}) teleported to {x} {y} {z}!");

                    DiscordWebhooks.DiscordTeleportWebhook(caller.DisplayName, caller.Id, "coords", null, null, command[0], command[1], command[2]);
                    return;

                default:
                    UnturnedChat.Say(caller, Main.Instance.Translate("TeleportSyntax"), Main.Instance.MessageColor);
                    Logger.Log($"{caller.DisplayName} ({caller.Id}) didn't use /teleport properly!");
                    break;
            }
            
        }
    }
}
