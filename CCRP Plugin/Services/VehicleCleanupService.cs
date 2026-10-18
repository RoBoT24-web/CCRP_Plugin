using CCRPPlugin;
using Rocket.Core.Logging;
using Rocket.Unturned.Chat;
using SDG.Unturned;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CCRP_Plugin.Services
{
    public class VehicleCleanupService : MonoBehaviour
    {
        private const float FirstWarningSeconds = 30f;
        private const float SecondWarningSeconds = 10f;
        private const float PollSeconds = 10f;
        private const float MoveThresholdSqr = 1f;

        private class Activity
        {
            public Vector3 Position;
            public float LastActive;
        }

        private readonly Dictionary<InteractableVehicle, Activity> activity = new Dictionary<InteractableVehicle, Activity>();

        void Start()
        {
            var settings = Main.Instance.Configuration.Instance.VehicleCleanup;
            if (settings == null)
            {
                return;
            }

            if (settings.EmptyVehicles != null && settings.EmptyVehicles.Enabled)
            {
                StartCoroutine(CleanupLoop(settings.EmptyVehicles.IntervalMinutes, true));
            }

            if (settings.AllVehicles != null && settings.AllVehicles.Enabled)
            {
                StartCoroutine(TrackActivity());
                StartCoroutine(CleanupLoop(settings.AllVehicles.IntervalMinutes, false));
            }
        }

        private IEnumerator CleanupLoop(double intervalMinutes, bool emptyOnly)
        {
            string key = emptyOnly ? "Empty" : "All";
            float interval = (float)(System.Math.Max(intervalMinutes, VehicleCleanupSettings.MinimumIntervalMinutes) * 60d);

            while (true)
            {
                yield return new WaitForSeconds(interval - FirstWarningSeconds);
                Announce("VehicleCleanup" + key + "Warning", (int)FirstWarningSeconds);

                yield return new WaitForSeconds(FirstWarningSeconds - SecondWarningSeconds);
                Announce("VehicleCleanup" + key + "Warning", (int)SecondWarningSeconds);

                yield return new WaitForSeconds(SecondWarningSeconds);
                int removed = DestroyVehicles(emptyOnly);
                Announce("VehicleCleanup" + key + "Done", removed);
            }
        }

        private static bool IsOccupied(InteractableVehicle vehicle)
        {
            return vehicle.passengers != null && vehicle.passengers.Any(p => p != null && p.player != null);
        }

        private IEnumerator TrackActivity()
        {
            while (true)
            {
                float now = Time.time;
                var current = VehicleManager.vehicles ?? new List<InteractableVehicle>();

                foreach (InteractableVehicle vehicle in current)
                {
                    if (vehicle == null || vehicle.isDead)
                    {
                        continue;
                    }

                    Vector3 pos = vehicle.transform.position;
                    Activity a;
                    if (!activity.TryGetValue(vehicle, out a))
                    {
                        activity[vehicle] = new Activity { Position = pos, LastActive = now };
                    }
                    else if (IsOccupied(vehicle) || (pos - a.Position).sqrMagnitude > MoveThresholdSqr)
                    {
                        a.Position = pos;
                        a.LastActive = now;
                    }
                }

                foreach (InteractableVehicle gone in activity.Keys.Where(v => v == null || v.isDead).ToList())
                {
                    activity.Remove(gone);
                }

                yield return new WaitForSeconds(PollSeconds);
            }
        }

        private bool IsUnused(InteractableVehicle vehicle)
        {
            var settings = Main.Instance.Configuration.Instance.VehicleCleanup.AllVehicles;
            Activity a;
            return !IsOccupied(vehicle)
                && activity.TryGetValue(vehicle, out a)
                && Time.time - a.LastActive >= settings.MinIdleMinutes * 60d;
        }

        private static void Announce(string key, int value)
        {
            UnturnedChat.Say(Main.Instance.Translate(key, value), Main.Instance.MessageColor);
        }

        private int DestroyVehicles(bool emptyOnly)
        {
            if (VehicleManager.vehicles == null)
            {
                return 0;
            }

            int removed = 0;
            foreach (InteractableVehicle vehicle in new List<InteractableVehicle>(VehicleManager.vehicles))
            {
                if (vehicle == null || vehicle.isDead)
                {
                    continue;
                }

                if (emptyOnly ? IsOccupied(vehicle) : !IsUnused(vehicle))
                {
                    continue;
                }

                VehicleManager.askVehicleDestroy(vehicle);
                removed++;
            }

            Rocket.Core.Logging.Logger.Log("Vehicle cleanup (" + (emptyOnly ? "empty" : "all") + ") removed " + removed + " vehicles.");
            return removed;
        }
    }
}
