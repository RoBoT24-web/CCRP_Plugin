using CCRP_Plugin.Models;
using CCRPPlugin;
using CCRPPlugin.Storage;
using Rocket.API;
using Rocket.Unturned;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CCRP_Plugin.Database
{
    public class HomeDatabase
    {
        public const int MaxPermissionLimit = 100;

        // Highest ccrp.sethome.N the player holds; 1 when they only have ccrp.sethome.
        public static int GetMaxHomes(Rocket.API.IRocketPlayer player)
        {
            for (int i = MaxPermissionLimit; i > 0; i--)
            {
                if (player.HasPermission("ccrp.sethome." + i))
                {
                    return i;
                }
            }
            return 1;
        }

        private readonly object sync = new object();
        public DataStorage<List<Home>> DataStorage { get; set; }

        public List<Home> Data;

        public HomeDatabase()
        {
            DataStorage = new DataStorage<List<Home>>(Main.Instance.Directory, "Homes.json");
        }

        public void Reload()
        {
            lock (sync)
            {
                Data = DataStorage.Read();
                if (Data == null)
                {
                    Data = new List<Home>();
                    DataStorage.Save(Data);
                }
            }
        }

        public List<Home> GetHomes(ulong steamId)
        {
            lock (sync)
            {
                return Data.Where(h => h.Steam64ID == steamId).OrderBy(h => h.Name, StringComparer.OrdinalIgnoreCase).ToList();
            }
        }

        public Home GetHome(ulong steamId, string name)
        {
            lock (sync)
            {
                return Data.FirstOrDefault(h => h.Steam64ID == steamId && string.Equals(h.Name, name, StringComparison.OrdinalIgnoreCase));
            }
        }

        // Returns false when adding a new home would exceed maxHomes. Overwriting an existing home is always allowed.
        public bool SetHome(Home home, int maxHomes, out bool replaced)
        {
            lock (sync)
            {
                Home existing = Data.FirstOrDefault(h => h.Steam64ID == home.Steam64ID && string.Equals(h.Name, home.Name, StringComparison.OrdinalIgnoreCase));
                replaced = existing != null;
                if (existing == null)
                {
                    if (Data.Count(h => h.Steam64ID == home.Steam64ID) >= maxHomes)
                    {
                        return false;
                    }
                    Data.Add(home);
                }
                else
                {
                    existing.X = home.X;
                    existing.Y = home.Y;
                    existing.Z = home.Z;
                    existing.Rotation = home.Rotation;
                }
                DataStorage.Save(Data);
                return true;
            }
        }

        public bool DeleteHome(ulong steamId, string name)
        {
            lock (sync)
            {
                Home existing = Data.FirstOrDefault(h => h.Steam64ID == steamId && string.Equals(h.Name, name, StringComparison.OrdinalIgnoreCase));
                if (existing == null)
                {
                    return false;
                }
                Data.Remove(existing);
                DataStorage.Save(Data);
                return true;
            }
        }
    }
}
