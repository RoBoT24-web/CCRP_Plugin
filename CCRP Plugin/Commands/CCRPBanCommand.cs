using CCRP_Plugin;
using Rocket.API;
using Rocket.Core.Logging;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using SDG.Unturned;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace CCRPPlugin.Commands
{
	internal class CCRPBanCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Both;

        public string Name => "ban";

        public string Help => "";

        public string Syntax => "<name> [Reason] [Duration]";

        public List<string> Aliases => new List<string>();

        public List<string> Permissions => new List<string> { "ccrp.ban" };


		public void Execute(IRocketPlayer caller, string[] command)
		{
			CCRPPlugin.Commands.CommandExceptionHandler.ExecuteSafely(caller, Name, () => ExecuteCore(caller, command));
		}

		private void ExecuteCore(IRocketPlayer caller, string[] command)
		{
            CSteamID callerSteam;
            if (caller is UnturnedPlayer executor)
			{
				callerSteam = executor.CSteamID;
			} else
			{
				callerSteam = CSteamID.Nil;
			}

			if (!Provider.isServer)
			{
				return;
			}

			if (!(command.Length == 3) && !(command.Length == 2))
			{
				UnturnedChat.Say(caller, Main.Instance.Translate("BanSyntax"), Main.Instance.MessageColor);
				return;
			}
            CSteamID csteamID;
            if (!PlayerTool.tryGetSteamID(command[0], out csteamID))
			{
				UnturnedChat.Say(caller, Main.Instance.Translate("PlayerNotFound"), Main.Instance.MessageColor);
				return;
			}

			uint ipv4AddressOrZero = SDG.Unturned.SteamGameServerNetworkingUtils.getIPv4AddressOrZero(csteamID);

			if (command.Length == 2)
			{
				Provider.requestBanPlayer(callerSteam, csteamID, ipv4AddressOrZero, command[1], SteamBlacklist.PERMANENT);
				UnturnedChat.Say(caller, Main.Instance.Translate("BanPerm", command[0], command[1]), Main.Instance.MessageColor);
				Logger.Log($"{caller.DisplayName} ({caller.Id}) permanently banned {csteamID.m_SteamID} ({command[1]})!");
				DiscordWebhooks.DiscordBanWebhook(caller.DisplayName, caller.Id, csteamID.m_SteamID.ToString(), command[1], "perm");
				return;
			}

			uint banTimeSeconds;
			if (!TryParseBanDuration(command[2], out banTimeSeconds))
			{
				UnturnedChat.Say(caller, Main.Instance.Translate("BanSyntax"), Main.Instance.MessageColor);
				return;
			}

			DateTime unbanDate = DateTime.Now.AddSeconds(banTimeSeconds);
			string unbanDateString = unbanDate.ToString();

			Provider.requestBanPlayer(callerSteam, csteamID, ipv4AddressOrZero, command[1], banTimeSeconds);

			UnturnedChat.Say(caller, Main.Instance.Translate("BanWithTime", csteamID.m_SteamID, command[1], banTimeSeconds, command[2]), Main.Instance.MessageColor);
			Logger.Log($"{caller.DisplayName} ({caller.Id}) banned {csteamID.m_SteamID} ({command[1]}) [{banTimeSeconds}]!");

			DiscordWebhooks.DiscordBanWebhook(caller.DisplayName, caller.Id, csteamID.m_SteamID.ToString(), command[1], unbanDateString);
			return;
		}

		private static bool TryParseBanDuration(string value, out uint banTimeSeconds)
		{
			banTimeSeconds = 0;
			if (string.IsNullOrWhiteSpace(value))
			{
				return false;
			}

			value = value.Trim();
			char unit = char.ToLowerInvariant(value[value.Length - 1]);
			uint multiplier;
			switch (unit)
			{
				case 'm':
					multiplier = 60;
					break;
				case 'h':
					multiplier = 60 * 60;
					break;
				case 'd':
					multiplier = 24 * 60 * 60;
					break;
				default:
					return false;
			}

			string amountText = value.Substring(0, value.Length - 1);
			uint amount;
			if (!uint.TryParse(amountText, NumberStyles.None, CultureInfo.InvariantCulture, out amount) || amount == 0)
			{
				return false;
			}

			ulong durationSeconds = (ulong)amount * multiplier;
			if (durationSeconds >= SteamBlacklist.PERMANENT)
			{
				return false;
			}

			banTimeSeconds = (uint)durationSeconds;
			return true;
		}
    }
}
