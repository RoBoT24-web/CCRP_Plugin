using CCRP_Plugin.Services;
using Rocket.API;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using SDG.Unturned;
using System.Collections.Generic;

namespace CCRPPlugin.Commands
{
    public sealed class CCRPBackCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;
        public string Name => "back";
        public string Help => "Return to your last death location.";
        public string Syntax => "";
        public List<string> Aliases => new List<string>();
        public List<string> Permissions => new List<string> { "ccrp.back" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            CommandExceptionHandler.ExecuteSafely(caller, Name, () => ExecuteCore(caller, command));
        }

        private static void ExecuteCore(IRocketPlayer caller, string[] command)
        {
            UnturnedPlayer player = caller as UnturnedPlayer;
            if (player == null)
            {
                UnturnedChat.Say(caller, "Only players can use /back.");
                return;
            }

            if (command.Length != 0)
            {
                UnturnedChat.Say(caller, "Usage: /back");
                return;
            }

            if (TeleportService.IsBlocked(player))
            {
                return;
            }

            Main plugin = Main.Instance;
            if (plugin == null || plugin.DeathLocationService == null)
            {
                UnturnedChat.Say(caller, "Death locations are unavailable.");
                return;
            }

            DeathLocation location;
            if (!plugin.DeathLocationService.TryConsume(player.CSteamID, out location))
            {
                UnturnedChat.Say(caller, "No saved death location is available, or it has expired.");
                return;
            }

            player.Teleport(location.Position, location.Rotation);
            UnturnedChat.Say(caller, "Returned to your last death location.");
        }
    }
}
