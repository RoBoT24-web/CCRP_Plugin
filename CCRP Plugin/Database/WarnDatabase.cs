using CCRPPlugin.Storage;
using System.Collections.Generic;
using System.Linq;

namespace CCRPPlugin.Database
{
    public class WarnDatabase
    {

        public DataStorage<List<Warns>> DataStorage { get; set; }

        public List<Warns> Data;

        public WarnDatabase()
        {
            DataStorage = new DataStorage<List<Warns>>(Main.Instance.Directory, "Warns.json");
        }

        public void Reload()
        {

            Data = DataStorage.Read();
            if (Data == null)
            {
                Data = new List<Warns>();
                DataStorage.Save(Data);
            }
        }

        public void AddWarn(Warns warn)
        {
            Data.Add(warn);
            DataStorage.Save(Data); 
        }
        public void RenoveOldWarn(Warns warn)
        {
            var itemToRemove = Data.FirstOrDefault(x => x.PlayerId == warn.PlayerId);
            Data.Remove(itemToRemove);
            DataStorage.Save(Data);
        }

    }
}
