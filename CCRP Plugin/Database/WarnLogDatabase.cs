using CCRPPlugin;
using CCRPPlugin.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CCRP_Plugin.Database
{
    public class WarnLogDatabase
    {
        public DataStorage<List<WarnLog>> DataStorage { get; set; }

        public List<WarnLog> Data;

        public WarnLogDatabase()
        {
            DataStorage = new DataStorage<List<WarnLog>>(Main.Instance.DataDirectory, "WarnLog.json");
        }

        public void Reload()
        {

            Data = DataStorage.Read();
            if (Data == null)
            {
                Data = new List<WarnLog>();
                DataStorage.Save(Data);
            }
        }
        
        public void AddWarnToLog(WarnLog warnLog)
        { 
            Data.Add(warnLog);
            DataStorage.Save(Data);
        }
    }
}
