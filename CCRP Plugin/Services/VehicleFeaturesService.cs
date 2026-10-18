using SDG.Unturned;
using UnityEngine;

namespace CCRP_Plugin.Services
{
    public class VehicleFeaturesService : MonoBehaviour
    {
        private const float UpdateInterval = 1f;

        private InteractableVehicle vehicle;
        private bool autoRefuel;
        private bool autoRepair;
        private float nextUpdate;

        public void Initialize(InteractableVehicle target)
        {
            if (target != null)
            {
                vehicle = target;
            }
        }

        public void SetFeatures(bool refuel, bool repair)
        {
            autoRefuel = refuel;
            autoRepair = repair;

            if (!autoRefuel && !autoRepair)
            {
                Destroy(this);
            }
        }

        public void GetFeatures(out bool refuel, out bool repair)
        {
            refuel = autoRefuel;
            repair = autoRepair;
        }

        private void Update()
        {
            if (vehicle == null || vehicle.isDead)
            {
                autoRefuel = false;
                autoRepair = false;
                vehicle = null;
                Destroy(this);
                return;
            }

            if (Time.time < nextUpdate)
            {
                return;
            }

            nextUpdate = Time.time + UpdateInterval;

            if (autoRepair)
            {
                RepairVehicle();
            }

            if (autoRefuel)
            {
                RefuelVehicle();
            }
        }

        private void RepairVehicle()
        {
            if (vehicle.usesHealth && !vehicle.isRepaired && vehicle.asset != null && vehicle.asset.health > 0)
            {
                // The instance API accepts an amount and applies the repair to this vehicle.
                vehicle.askRepair(vehicle.asset.health);
            }

            if (!vehicle.isTireReplaceable)
            {
                return;
            }

            byte aliveMask = vehicle.tireAliveMask;
            for (int tireIndex = 0; tireIndex < 8; tireIndex++)
            {
                if ((aliveMask & (1 << tireIndex)) == 0)
                {
                    vehicle.askRepairTire(tireIndex);
                }
            }
        }

        private void RefuelVehicle()
        {
            if (!vehicle.usesFuel)
            {
                return;
            }

            ushort currentFuel;
            ushort maximumFuel;
            vehicle.getDisplayFuel(out currentFuel, out maximumFuel);
            if (maximumFuel > currentFuel)
            {
                vehicle.askFillFuel((ushort)(maximumFuel - currentFuel));
            }
        }

        private void OnDestroy()
        {
            vehicle = null;
            autoRefuel = false;
            autoRepair = false;
        }
    }
}
