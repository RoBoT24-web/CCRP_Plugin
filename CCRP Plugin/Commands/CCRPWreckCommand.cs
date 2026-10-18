using CCRP_Plugin;
using Rocket.API;
using Rocket.Core.Logging;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using SDG.Unturned;
using Steamworks;
using System;
using System.Collections.Generic;
using CCRP_Plugin.Services;
using System.Globalization;

namespace CCRPPlugin.Commands
{
    internal class CCRPWreckCommand : IRocketCommand
    {
        private const int MaximumRadius = 200;

        public AllowedCaller AllowedCaller => AllowedCaller.Player;

        public string Name => "wreck";

        public string Help => "Queue nearby structures, barricades, or vehicles for destruction.";

        public string Syntax => "<filter> <radius> [username|Steam64] | scan <filter|itemID> <radius> [username|Steam64] | confirm <reason> | abort";

        public List<string> Aliases => new List<string>();

        public List<string> Permissions => new List<string> { "ccrp.wreck" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            CommandExceptionHandler.ExecuteSafely(caller, Name, () => ExecuteCore(caller, command, false));
        }

        internal static void ExecuteItemCommand(IRocketPlayer caller, string[] command)
        {
            CommandExceptionHandler.ExecuteSafely(caller, "wreckitem", () => ExecuteCore(caller, command, true));
        }

        private static void ExecuteCore(IRocketPlayer caller, string[] command, bool itemCommand)
        {
            UnturnedPlayer player = caller as UnturnedPlayer;
            if (player == null)
            {
                return;
            }

            string commandText = $"/{(itemCommand ? "wreckitem" : "wreck")} {string.Join(" ", command)}".TrimEnd();
            string location = $"X: {player.Position.x:F0} Y: {player.Position.y:F0} Z: {player.Position.z:F0}";

            if (command.Length > 0 && command[0].Equals("confirm", StringComparison.OrdinalIgnoreCase))
            {
                string reason = string.Join(" ", command, 1, command.Length - 1).Trim();
                if (reason.Length == 0)
                {
                    UnturnedChat.Say(caller, Main.Instance.Translate("WreckReasonRequired", itemCommand ? "wreckitem" : "wreck"), Main.Instance.MessageColor);
                    return;
                }

                int count;
                if (!Main.Instance.WreckService.Confirm(player.CSteamID.m_SteamID, out count))
                {
                    string translation = Main.Instance.WreckService.HasRequest(player.CSteamID.m_SteamID)
                        ? "WreckAlreadyProcessing"
                        : "WreckNoRequest";
                    UnturnedChat.Say(caller, Main.Instance.Translate(translation), Main.Instance.MessageColor);
                    return;
                }

                UnturnedChat.Say(caller, Main.Instance.Translate("WreckConfirm", count), Main.Instance.MessageColor);
                Logger.Log($"{caller.DisplayName} ({caller.Id}) confirmed a wreck request for {count} objects.");
                string callerName = caller.DisplayName;
                string callerId = caller.Id;
                string result = $"Confirmed, destroying {count} objects";
                int sent = 0;
                Action<byte[]> sendLog = screenshot =>
                {
                    if (System.Threading.Interlocked.Exchange(ref sent, 1) == 0)
                    {
                        DiscordWebhooks.DiscordWreckWebhook(callerName, callerId, commandText, result, location, reason, screenshot);
                    }
                };

                try
                {
                    player.Player.sendScreenshot(CSteamID.Nil, (steamId, jpg) => sendLog(jpg));
                    // Fall back to a log without a screenshot if the client never responds.
                    System.Threading.Tasks.Task.Delay(15000).ContinueWith(_ => sendLog(null));
                }
                catch (Exception exception)
                {
                    Logger.LogWarning($"Unable to request wreck screenshot: {exception.Message}");
                    sendLog(null);
                }
                return;
            }

            if (command.Length == 1 && command[0].Equals("abort", StringComparison.OrdinalIgnoreCase))
            {
                int destroyed;
                if (!Main.Instance.WreckService.Abort(player.CSteamID.m_SteamID, out destroyed))
                {
                    UnturnedChat.Say(caller, Main.Instance.Translate("WreckNoRequest"), Main.Instance.MessageColor);
                    return;
                }

                UnturnedChat.Say(caller, Main.Instance.Translate("WreckAborted", destroyed), Main.Instance.MessageColor);
                Logger.Log($"{caller.DisplayName} ({caller.Id}) aborted a wreck request.");
                DiscordWebhooks.DiscordWreckWebhook(caller.DisplayName, caller.Id, commandText, $"Aborted ({destroyed} already destroyed)", location);
                return;
            }

            bool scan = command.Length > 0 && command[0].Equals("scan", StringComparison.OrdinalIgnoreCase);
            int filterIndex = scan ? 1 : 0;
            if (command.Length < filterIndex + 2)
            {
                ShowSyntax(caller, itemCommand);
                return;
            }

            HashSet<char> categories;
            HashSet<ushort> itemIds = null;
            bool itemIdScan = false;
            ushort scanItemId = 0;
            int radiusIndex;
            if (itemCommand)
            {
                categories = new HashSet<char> { '*' };
                if (!TryParseItemIds(command[filterIndex], out itemIds))
                {
                    UnturnedChat.Say(caller, Main.Instance.Translate("WreckInvalidItem"), Main.Instance.MessageColor);
                    return;
                }
                radiusIndex = filterIndex + 1;
            }
            else
            {
                if (scan && TryParseItemId(command[filterIndex], out scanItemId))
                {
                    categories = null;
                    itemIdScan = true;
                }
                else if (!TryParseCategories(command[filterIndex], out categories))
                {
                    UnturnedChat.Say(caller, Main.Instance.Translate("WreckInvalidFilter"), Main.Instance.MessageColor);
                    return;
                }
                radiusIndex = filterIndex + 1;
            }

            int radius;
            if (command.Length <= radiusIndex ||
                !int.TryParse(command[radiusIndex], NumberStyles.None, CultureInfo.InvariantCulture, out radius) ||
                radius < 1 || radius > MaximumRadius)
            {
                UnturnedChat.Say(caller, Main.Instance.Translate("WreckInvalidRadius", MaximumRadius), Main.Instance.MessageColor);
                return;
            }

            ulong? ownerId = null;
            int ownerIndex = radiusIndex + 1;
            if (command.Length > ownerIndex)
            {
                if (command.Length != ownerIndex + 1)
                {
                    ShowSyntax(caller, itemCommand);
                    return;
                }

                if (!TryParseOwner(caller, command[ownerIndex], out ownerId))
                {
                    return;
                }
            }

            if (scan)
            {
                int count;
                bool withinLimit = itemIdScan
                    ? Main.Instance.WreckService.TryScanByItemId(player.Position, radius, scanItemId, ownerId, out count)
                    : Main.Instance.WreckService.TryScan(player.Position, radius, categories, ownerId, itemIds, out count);
                if (!withinLimit)
                {
                    UnturnedChat.Say(caller, Main.Instance.Translate("WreckTooMany", WreckService.MaximumTargets), Main.Instance.MessageColor);
                    return;
                }

                if (count == 0)
                {
                    UnturnedChat.Say(caller, Main.Instance.Translate("WreckNone"), Main.Instance.MessageColor);
                    return;
                }

                UnturnedChat.Say(caller, Main.Instance.Translate("WreckScan", count, radius), Main.Instance.MessageColor);
                Logger.Log($"{caller.DisplayName} ({caller.Id}) scanned {count} wreck targets within {radius}m.");
                DiscordWebhooks.DiscordWreckWebhook(caller.DisplayName, caller.Id, commandText, $"Scanned {count} targets within {radius}m", location);
                return;
            }

            int queued;
            bool replacedRequest;
            Main.Instance.WreckService.Queue(player.CSteamID, player.Position, radius, categories, ownerId, itemIds, out queued, out replacedRequest);

            if (replacedRequest)
            {
                UnturnedChat.Say(caller, Main.Instance.Translate("WreckRequestReplaced"), Main.Instance.MessageColor);
            }

            if (queued > WreckService.MaximumTargets)
            {
                UnturnedChat.Say(caller, Main.Instance.Translate("WreckTooMany", WreckService.MaximumTargets), Main.Instance.MessageColor);
                return;
            }

            if (queued == 0)
            {
                UnturnedChat.Say(caller, Main.Instance.Translate("WreckNone"), Main.Instance.MessageColor);
                return;
            }

            UnturnedChat.Say(caller, Main.Instance.Translate("WreckQueued", queued, radius), Main.Instance.MessageColor);
            Logger.Log($"{caller.DisplayName} ({caller.Id}) queued {queued} wreck targets within {radius}m.");
            DiscordWebhooks.DiscordWreckWebhook(caller.DisplayName, caller.Id, commandText, $"Queued {queued} targets within {radius}m", location);
        }

        private static void ShowSyntax(IRocketPlayer caller, bool itemCommand)
        {
            UnturnedChat.Say(caller, Main.Instance.Translate(itemCommand ? "WreckItemSyntax" : "WreckSyntax"), Main.Instance.MessageColor);
        }

        private static bool TryParseCategories(string value, out HashSet<char> categories)
        {
            const string validCategories = "btdclwfprsgiav*";
            categories = new HashSet<char>();
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            foreach (char valueChar in value.ToLowerInvariant())
            {
                if (validCategories.IndexOf(valueChar) < 0)
                {
                    return false;
                }
                categories.Add(valueChar);
            }

            return categories.Count > 0;
        }

        private static bool TryParseOwner(IRocketPlayer caller, string value, out ulong? ownerId)
        {
            ownerId = null;
            if (value.Equals("last", StringComparison.OrdinalIgnoreCase))
            {
                ulong lastOwner;
                if (!Main.Instance.WreckService.TryGetLastLookup(((UnturnedPlayer)caller).CSteamID.m_SteamID, out lastOwner))
                {
                    UnturnedChat.Say(caller, Main.Instance.Translate("WreckInvalidUser"), Main.Instance.MessageColor);
                    return false;
                }

                ownerId = lastOwner;
                return true;
            }

            ulong parsedOwner;
            if (ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out parsedOwner) && parsedOwner != 0)
            {
                ownerId = parsedOwner;
                return true;
            }

            CSteamID steamId;
            if (!PlayerTool.tryGetSteamID(value, out steamId) || steamId.m_SteamID == 0)
            {
                UnturnedChat.Say(caller, Main.Instance.Translate("WreckInvalidUser"), Main.Instance.MessageColor);
                return false;
            }

            ownerId = steamId.m_SteamID;
            return true;
        }

        private static bool TryParseItemIds(string value, out HashSet<ushort> itemIds)
        {
            itemIds = new HashSet<ushort>();
            foreach (string idText in value.Split(','))
            {
                ushort id;
                if (!ushort.TryParse(idText, NumberStyles.None, CultureInfo.InvariantCulture, out id) || id == 0)
                {
                    return false;
                }
                itemIds.Add(id);
            }

            return itemIds.Count > 0;
        }

        private static bool TryParseItemId(string value, out ushort itemId)
        {
            return ushort.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out itemId) && itemId != 0;
        }
    }
}
