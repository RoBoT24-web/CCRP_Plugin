using CCRPPlugin;
using CCRPPlugin.Database;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CCRP_Plugin.Services
{
    public class WarnService : MonoBehaviour
    {
        public Dictionary<string, List<Warns>> WarnPlayerInfo { get; private set; }
        private WarnDatabase databaseAddWarn => Main.Instance.WarnDatabase;


        void Awake()
        {
            WarnPlayerInfo = new Dictionary<string, List<Warns>>();
        }

        void Start()
        {

        }

        void onDestroy()
        {

        }
        public int IncreaseWarn(int warns)
        {
            warns++;

            return warns;
        }
        public int DecreaseWarn(int warns)
        {
            warns--;
            return warns;
        }
        public int WarnAmountCheck(string playerId)
        {
            Warns warning = GetPlayerWarning(playerId);
            return warning == null ? 0 : warning.WarnAmount;
        }

        public Warns GetPlayerWarning(string playerId)
        {
            return databaseAddWarn.Data.FirstOrDefault(x => x.PlayerId == playerId);
        }

        public bool WarnDoesExist(string playerId)
        {
            return GetPlayerWarning(playerId) != null;
        }

        public void RegisterWarnRemove(string playerId, int warns)
        {
            Warns existingWarning = GetPlayerWarning(playerId);
            var playerWarn = new Warns()
            {
                PlayerId = playerId,
                WarnAmount = DecreaseWarn(warns),
                WarnReason = existingWarning == null ? string.Empty : existingWarning.WarnReason
            };

            databaseAddWarn.AddWarn(playerWarn);
        }
        public void RegisterWarn(string playerId, string reason, int warns)
        {
            var playerWarn = new Warns()
            {
                PlayerId = playerId,
                WarnAmount = IncreaseWarn(warns),
                WarnReason = reason
            };

            databaseAddWarn.AddWarn(playerWarn);
        }
        public void CreateWarn(string playerId)
        {
            var playerWarn = new Warns()
            {
                PlayerId = playerId,
                WarnAmount = 0,
                WarnReason = ""
            };

            databaseAddWarn.AddWarn(playerWarn);
        }
        public void RemoveOldWarn(string playerId)
        {
            var playerWarn = new Warns()
            {
                PlayerId = playerId,
                WarnAmount = 0,
                WarnReason = ""
            };

            databaseAddWarn.RenoveOldWarn(playerWarn);
        }
    }
}
