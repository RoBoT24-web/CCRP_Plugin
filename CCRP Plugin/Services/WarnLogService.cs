using CCRP_Plugin.Database;
using CCRPPlugin;
using CCRPPlugin.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace CCRP_Plugin.Services
{
    public class WarnLogService : MonoBehaviour
    {
        public Dictionary<string, List<WarnLog>> WarnLogInfo { get; private set; }
        private WarnLogDatabase databaseWarnLog => Main.Instance.WarnLogDatabase;


        void Awake()
        {
            WarnLogInfo = new Dictionary<string, List<WarnLog>>();
        }

        void Start()
        {

        }

        void onDestroy()
        {

        }

        public void OnWarnAdd(string iName, string iId, string pName, string pId, int wAmount)
        {
            var warnLog = new WarnLog()
            {
                Action = "Add Warn",
                InvestigatorName = iName,
                InvestigatorId = iId,
                PlayerName = pName,
                PlayerId = pId,   
                WarnAmount = wAmount
            };
            databaseWarnLog.AddWarnToLog(warnLog);
        }
        public void OnWarnRemove(string iName, string iId, string pName, string pId, int wAmount)
        {
            var warnLog = new WarnLog()
            {
                Action = "Removed Warn",
                InvestigatorName = iName,
                InvestigatorId = iId,
                PlayerName = pName,
                PlayerId = pId,
                WarnAmount = wAmount
            };
            databaseWarnLog.AddWarnToLog(warnLog);
        }
    }
}
