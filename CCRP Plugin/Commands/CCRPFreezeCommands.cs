using Rocket.API;
using Rocket.Core;
using Rocket.Unturned;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CCRPPlugin.Commands
{
    public sealed class CCRPFreezeManager : MonoBehaviour
    {
        private static readonly HashSet<string> blockedCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "teleport", "tp", "tphere", "tpr", "tpaccept", "tpdeny",
            "warp", "home", "back", "spawn", "return",
            // These movement commands could undo the frozen movement multipliers.
            "speed", "jump"
        };

        // Target SteamID -> issuer SteamIDs. A frozen player stays frozen while any
        // connected issuer's freeze remains active; console-issued freezes use ID 0.
        private readonly Dictionary<ulong, HashSet<ulong>> issuersByTarget =
            new Dictionary<ulong, HashSet<ulong>>();

        private static CCRPFreezeManager instance;

        public static void Initialize(GameObject pluginObject)
        {
            if (pluginObject == null || instance != null)
                return;

            instance = pluginObject.GetComponent<CCRPFreezeManager>();
            if (instance == null)
                instance = pluginObject.AddComponent<CCRPFreezeManager>();
        }

        public static void Shutdown()
        {
            if (instance != null)
                Destroy(instance);
        }

        public static bool IsFrozen(UnturnedPlayer player)
        {
            return player != null && instance != null &&
                instance.issuersByTarget.ContainsKey(player.CSteamID.m_SteamID);
        }

        internal static bool AddFreeze(UnturnedPlayer target, IRocketPlayer issuer)
        {
            if (target == null)
                return false;

            EnsureInitialized();
            if (instance == null)
                return false;

            ulong targetId = target.CSteamID.m_SteamID;
            HashSet<ulong> issuers;
            bool newlyFrozen = !instance.issuersByTarget.TryGetValue(targetId, out issuers);
            if (newlyFrozen)
            {
                issuers = new HashSet<ulong>();
                instance.issuersByTarget.Add(targetId, issuers);
            }

            ulong issuerId;
            if (issuer is UnturnedPlayer issuerPlayer)
                issuerId = issuerPlayer.CSteamID.m_SteamID;
            else
                issuerId = 0;

            issuers.Add(issuerId);
            if (newlyFrozen && !instance.SetMovementFrozen(target, true))
            {
                instance.issuersByTarget.Remove(targetId);
                instance.SetMovementFrozen(target, false);
                return false;
            }

            return true;
        }

        internal static bool RemoveFreeze(UnturnedPlayer target)
        {
            if (target == null || instance == null ||
                !instance.issuersByTarget.Remove(target.CSteamID.m_SteamID))
                return false;

            instance.SetMovementFrozen(target, false);
            return true;
        }

        private static void EnsureInitialized()
        {
            if (instance == null && Main.Instance != null)
                Initialize(Main.Instance.gameObject);
        }

        private void Awake()
        {
            instance = this;
            U.Events.OnPlayerDisconnected += OnPlayerDisconnected;
            if (R.Commands != null)
                R.Commands.OnExecuteCommand += OnExecuteCommand;
        }

        private void OnDestroy()
        {
            U.Events.OnPlayerDisconnected -= OnPlayerDisconnected;
            if (R.Commands != null)
                R.Commands.OnExecuteCommand -= OnExecuteCommand;

            foreach (ulong targetId in new List<ulong>(issuersByTarget.Keys))
            {
                UnturnedPlayer target = UnturnedPlayer.FromCSteamID(new Steamworks.CSteamID(targetId));
                if (target != null)
                    SetMovementFrozen(target, false);
            }

            issuersByTarget.Clear();
            if (instance == this)
                instance = null;
        }

        private void OnPlayerDisconnected(UnturnedPlayer player)
        {
            if (player == null)
                return;

            ulong disconnectedId = player.CSteamID.m_SteamID;
            if (issuersByTarget.Remove(disconnectedId))
                SetMovementFrozen(player, false);

            List<ulong> noLongerFrozen = new List<ulong>();
            foreach (KeyValuePair<ulong, HashSet<ulong>> pair in issuersByTarget)
            {
                pair.Value.Remove(disconnectedId);
                if (pair.Value.Count == 0)
                    noLongerFrozen.Add(pair.Key);
            }

            foreach (ulong targetId in noLongerFrozen)
            {
                issuersByTarget.Remove(targetId);
                UnturnedPlayer target = UnturnedPlayer.FromCSteamID(new Steamworks.CSteamID(targetId));
                if (target != null)
                    SetMovementFrozen(target, false);
            }
        }

        private void OnExecuteCommand(IRocketPlayer caller, IRocketCommand command, ref bool cancel)
        {
            UnturnedPlayer player = caller as UnturnedPlayer;
            if (player == null || command == null || !IsFrozen(player) || !blockedCommands.Contains(command.Name))
                return;

            cancel = true;
            UnturnedChat.Say(player, "You are frozen and cannot use that command.");
        }

        private bool SetMovementFrozen(UnturnedPlayer player, bool frozen)
        {
            if (player == null || player.Player == null || player.Player.movement == null)
                return false;

            float multiplier = frozen ? 0f : 1f;
            bool speedSet = UtilityCommandSupport.SetMovementMultiplier(player, "sendPluginSpeedMultiplier", multiplier);
            bool jumpSet = UtilityCommandSupport.SetMovementMultiplier(player, "sendPluginJumpMultiplier", multiplier);
            return speedSet && jumpSet;
        }
    }

    public sealed class CCRPFreezeCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Both;
        public string Name => "freeze";
        public string Help => "Freeze yourself or an online player.";
        public string Syntax => "[player]";
        public List<string> Aliases => new List<string>();
        public List<string> Permissions => new List<string> { "ccrp.freeze" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            UtilityCommandSupport.ExecuteSafely(caller, Name, () =>
            {
                if (command.Length > 1)
                {
                    UnturnedChat.Say(caller, "Usage: /freeze [player]");
                    return;
                }

                UnturnedPlayer target;
                if (command.Length == 0)
                {
                    target = caller as UnturnedPlayer;
                    if (target == null)
                    {
                        UnturnedChat.Say(caller, "Specify an online player.");
                        return;
                    }
                }
                else
                {
                    target = UnturnedPlayer.FromName(command[0]);
                    if (target == null)
                    {
                        UnturnedChat.Say(caller, "Player not found.");
                        return;
                    }
                }

                if (!CCRPFreezeManager.AddFreeze(target, caller))
                {
                    UnturnedChat.Say(caller, "Freeze control is unavailable.");
                    return;
                }

                UnturnedChat.Say(caller, target.DisplayName + " is frozen.");
            });
        }
    }

    public sealed class CCRPUnfreezeCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Both;
        public string Name => "unfreeze";
        public string Help => "Unfreeze yourself or an online player.";
        public string Syntax => "[player]";
        public List<string> Aliases => new List<string>();
        public List<string> Permissions => new List<string> { "ccrp.unfreeze" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            UtilityCommandSupport.ExecuteSafely(caller, Name, () =>
            {
                if (command.Length > 1)
                {
                    UnturnedChat.Say(caller, "Usage: /unfreeze [player]");
                    return;
                }

                UnturnedPlayer target;
                if (command.Length == 0)
                {
                    target = caller as UnturnedPlayer;
                    if (target == null)
                    {
                        UnturnedChat.Say(caller, "Specify an online player.");
                        return;
                    }
                }
                else
                {
                    target = UnturnedPlayer.FromName(command[0]);
                    if (target == null)
                    {
                        UnturnedChat.Say(caller, "Player not found.");
                        return;
                    }
                }

                if (CCRPFreezeManager.RemoveFreeze(target))
                    UnturnedChat.Say(caller, target.DisplayName + " is unfrozen.");
                else
                    UnturnedChat.Say(caller, target.DisplayName + " is not frozen.");
            });
        }
    }
}
