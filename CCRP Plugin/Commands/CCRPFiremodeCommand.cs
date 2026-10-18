using CCRPPlugin;
using Rocket.API;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using SDG.Unturned;
using System;
using System.Collections.Generic;

namespace CCRP_Plugin.Commands
{
    internal class CCRPFiremodeCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;
        public string Name => "firemode";
        public string Help => "Sets the held gun's firemode.";
        public string Syntax => "<safety|semi|auto|burst>";
        public List<string> Aliases => new List<string>();
        public List<string> Permissions => new List<string> { "ccrp.firemode" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            CCRPPlugin.Commands.CommandExceptionHandler.ExecuteSafely(caller, Name, () => ExecuteCore(caller, command));
        }

        private void ExecuteCore(IRocketPlayer caller, string[] command)
        {
            if (!(caller is UnturnedPlayer player))
            {
                return;
            }

            if (command.Length != 1 ||
                !Enum.TryParse(command[0], true, out EFiremode firemode) ||
                !Enum.IsDefined(typeof(EFiremode), firemode))
            {
                UnturnedChat.Say(caller, Main.Instance.Translate("FiremodeSyntax"), Main.Instance.MessageColor);
                return;
            }

            PlayerEquipment equipment = player.Player.equipment;
            ItemGunAsset gunAsset = equipment.asset as ItemGunAsset;
            UseableGun gun = equipment.useable as UseableGun;
            if (gunAsset == null || gun == null)
            {
                UnturnedChat.Say(caller, Main.Instance.Translate("FiremodeNoGun"), Main.Instance.MessageColor);
                return;
            }

            bool originalModeSupport;
            switch (firemode)
            {
                case EFiremode.SAFETY:
                    originalModeSupport = gunAsset.hasSafety;
                    gunAsset.hasSafety = true;
                    break;
                case EFiremode.SEMI:
                    originalModeSupport = gunAsset.hasSemi;
                    gunAsset.hasSemi = true;
                    break;
                case EFiremode.AUTO:
                    originalModeSupport = gunAsset.hasAuto;
                    gunAsset.hasAuto = true;
                    break;
                case EFiremode.BURST:
                    originalModeSupport = gunAsset.hasBurst;
                    gunAsset.hasBurst = true;
                    break;
                default:
                    UnturnedChat.Say(caller, Main.Instance.Translate("FiremodeSyntax"), Main.Instance.MessageColor);
                    return;
            }

            try
            {
                gun.ReceiveChangeFiremode(firemode);
            }
            finally
            {
                switch (firemode)
                {
                    case EFiremode.SAFETY:
                        gunAsset.hasSafety = originalModeSupport;
                        break;
                    case EFiremode.SEMI:
                        gunAsset.hasSemi = originalModeSupport;
                        break;
                    case EFiremode.AUTO:
                        gunAsset.hasAuto = originalModeSupport;
                        break;
                    case EFiremode.BURST:
                        gunAsset.hasBurst = originalModeSupport;
                        break;
                }
            }

            byte[] state = equipment.state;
            if (state == null || state.Length <= 11 || state[11] != (byte)firemode)
            {
                UnturnedChat.Say(caller, Main.Instance.Translate("FiremodeUnavailable"), Main.Instance.MessageColor);
                return;
            }

            UnturnedChat.Say(caller, Main.Instance.Translate("FiremodeSet", firemode.ToString().ToLowerInvariant()), Main.Instance.MessageColor);
        }
    }
}
