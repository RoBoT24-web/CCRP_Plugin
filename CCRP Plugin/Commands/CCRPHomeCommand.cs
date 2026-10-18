using CCRP_Plugin.Models;
using CCRP_Plugin.Services;
using CCRPPlugin;
using Rocket.API;
using Rocket.Unturned;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CCRP_Plugin.Commands
{
    internal class CCRPHomeCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;

        public string Name => "home";

        public string Help => "Teleport to one of your homes.";

        public string Syntax => "<name>";

        public List<string> Aliases => new List<string>();

        public List<string> Permissions { get; } = BuildPermissions();

        private const int DefaultDelaySeconds = 5;
        private const int MaxDelayPermission = 300;
        private const float MoveCancelDistance = 0.5f;

        private static readonly Dictionary<ulong, Coroutine> Pending = new Dictionary<ulong, Coroutine>();

        private static List<string> BuildPermissions()
        {
            // Rocket allows the command if the player holds any listed permission.
            List<string> permissions = new List<string> { "ccrp.home" };
            for (int i = 0; i <= MaxDelayPermission; i++)
            {
                permissions.Add("ccrp.home." + i);
            }
            return permissions;
        }

        // Lowest ccrp.home.N held wins (N seconds, 0 = instant); plain ccrp.home uses the default.
        private static int GetDelaySeconds(IRocketPlayer player)
        {
            for (int i = 0; i <= MaxDelayPermission; i++)
            {
                if (player.HasPermission("ccrp.home." + i))
                {
                    return i;
                }
            }
            return DefaultDelaySeconds;
        }

        public void Execute(IRocketPlayer caller, string[] command)
        {
            CCRPPlugin.Commands.CommandExceptionHandler.ExecuteSafely(caller, Name, () => ExecuteCore(caller, command));
        }

        private void ExecuteCore(IRocketPlayer caller, string[] command)
        {
            if (command.Length != 1)
            {
                UnturnedChat.Say(caller, Main.Instance.Translate("HomeSyntax"), Main.Instance.MessageColor);
                return;
            }

            UnturnedPlayer player = (UnturnedPlayer)caller;
            if (TeleportService.IsBlocked(player))
            {
                return;
            }
            ulong steamId = player.CSteamID.m_SteamID;
            Home home = Main.Instance.HomeDatabase.GetHome(steamId, command[0]);
            if (home == null)
            {
                UnturnedChat.Say(caller, Main.Instance.Translate("HomeNotFound", command[0]), Main.Instance.MessageColor);
                return;
            }

            CancelPending(steamId);

            int delay = GetDelaySeconds(caller);
            if (delay <= 0)
            {
                TeleportHome(player, home);
                return;
            }

            UnturnedChat.Say(caller, Main.Instance.Translate("HomeTeleporting", home.Name, delay), Main.Instance.MessageColor);
            Pending[steamId] = Main.Instance.StartCoroutine(WaitAndTeleport(player, steamId, home, delay));
        }

        private static void CancelPending(ulong steamId)
        {
            if (Pending.TryGetValue(steamId, out Coroutine existing))
            {
                if (existing != null && Main.Instance != null)
                {
                    Main.Instance.StopCoroutine(existing);
                }
                Pending.Remove(steamId);
            }
        }

        private static IEnumerator WaitAndTeleport(UnturnedPlayer player, ulong steamId, Home home, int delay)
        {
            Vector3 start = player.Position;
            float end = Time.time + delay;
            while (Time.time < end)
            {
                yield return new WaitForSeconds(0.25f);

                if (player == null || player.Player == null || player.Player.life.isDead)
                {
                    Pending.Remove(steamId);
                    yield break;
                }
                if (TeleportService.IsBlocked(player))
                {
                    Pending.Remove(steamId);
                    yield break;
                }

                if ((player.Position - start).sqrMagnitude > MoveCancelDistance * MoveCancelDistance)
                {
                    Pending.Remove(steamId);
                    UnturnedChat.Say(player, Main.Instance.Translate("HomeCancelled"), Main.Instance.MessageColor);
                    yield break;
                }
            }

            Pending.Remove(steamId);
            TeleportHome(player, home);
        }

        private static void TeleportHome(UnturnedPlayer player, Home home)
        {
            if (TeleportService.IsBlocked(player))
            {
                return;
            }
            player.Teleport(new Vector3(home.X, home.Y + 0.5f, home.Z), home.Rotation);
            UnturnedChat.Say(player, Main.Instance.Translate("HomeTeleported", home.Name), Main.Instance.MessageColor);
        }
    }
}
