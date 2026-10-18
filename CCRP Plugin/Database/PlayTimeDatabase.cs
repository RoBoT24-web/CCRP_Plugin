using CCRP_Plugin.Models;
using CCRPPlugin;
using CCRPPlugin.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CCRP_Plugin.Database
{
    public class PlayTimeDatabase
    {
        public DataStorage<List<Playtime>> DataStorage { get; set; }

        public List<Playtime> Data;

        public PlayTimeDatabase()
        {
            DataStorage = new DataStorage<List<Playtime>>(Main.Instance.Directory, "Playtime.json");
        }

        public void Reload()
        {

            Data = DataStorage.Read();
            if (Data == null)
            {
                Data = new List<Playtime>();
                DataStorage.Save(Data);
            }
        }

        public void AddPlaytime(Playtime playTime)
        {
            Data.Add(playTime);
            DataStorage.Save(Data);
        }

        public void UpsertPlaytime(Playtime playTime)
        {
            Playtime existing = null;
            for (int index = Data.Count - 1; index >= 0; index--)
            {
                Playtime candidate = Data[index];
                if (candidate == null || candidate.Steam64ID != playTime.Steam64ID)
                {
                    continue;
                }

                if (existing == null)
                {
                    existing = candidate;
                }
                else
                {
                    Data.RemoveAt(index);
                }
            }

            if (existing == null)
            {
                Data.Add(playTime);
                return;
            }

            existing.PlayTime = playTime.PlayTime;
            existing.LastPlaytime = playTime.LastPlaytime;
        }

        public void Save()
        {
            DataStorage.Save(Data);
        }
        public void RemovePlaytime(Playtime playtime)
        {
            var itemToRemove = Data.SingleOrDefault(x => x.Steam64ID == playtime.Steam64ID);
            Data.Remove(itemToRemove);
            DataStorage.Save(Data);
        }
    }
}
