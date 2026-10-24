using CCRPPlugin;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using SDG.Unturned;
using Steamworks;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Logger = Rocket.Core.Logging.Logger;

namespace CCRP_Plugin.Services
{
    public class WreckService : MonoBehaviour
    {
        public const int MaximumTargets = 2000;
        private const int TargetsPerFrame = 20;

        private enum TargetKind
        {
            Structure,
            Barricade,
            Vehicle
        }

        private sealed class WreckTarget
        {
            public TargetKind Kind;
            public Transform Transform;
            public InteractableVehicle Vehicle;
            public ushort ItemId;
            public ulong OwnerId;
        }

        private sealed class WreckRequest
        {
            public ulong CallerId;
            public Vector3 Center;
            public float Radius;
            public HashSet<char> Categories;
            public HashSet<ushort> ItemIds;
            public ulong? OwnerId;
            public List<WreckTarget> Targets;
            public bool IsProcessing;
            public bool IsCancelled;
            public int Destroyed;
            public int Failed;
        }

        private static readonly Dictionary<ushort, char> ItemCategories = CreateItemCategories();
        private readonly Dictionary<ulong, WreckRequest> requests = new Dictionary<ulong, WreckRequest>();

        public bool HasRequest(ulong callerId)
        {
            return requests.ContainsKey(callerId);
        }

        public bool TryScan(Vector3 center, int radius, HashSet<char> categories, ulong? ownerId, HashSet<ushort> itemIds, out int count)
        {
            List<WreckTarget> targets = FindTargets(center, radius, categories, ownerId, itemIds);
            count = targets.Count;
            return count <= MaximumTargets;
        }

        public bool TryScanByItemId(Vector3 center, int radius, ushort itemId, ulong? ownerId, out int count)
        {
            return TryScan(
                center,
                radius,
                new HashSet<char> { '*' },
                ownerId,
                new HashSet<ushort> { itemId },
                out count);
        }

        public void Queue(CSteamID caller, Vector3 center, int radius, HashSet<char> categories, ulong? ownerId, HashSet<ushort> itemIds, out int count, out bool replacedRequest)
        {
            ulong callerId = caller.m_SteamID;
            WreckRequest existingRequest;
            replacedRequest = requests.TryGetValue(callerId, out existingRequest);
            if (replacedRequest)
            {
                existingRequest.IsCancelled = true;
                requests.Remove(callerId);
            }

            List<WreckTarget> targets = FindTargets(center, radius, categories, ownerId, itemIds);
            count = targets.Count;
            if (count == 0 || count > MaximumTargets)
            {
                return;
            }

            requests.Add(callerId, new WreckRequest
            {
                CallerId = callerId,
                Center = center,
                Radius = radius,
                Categories = categories,
                OwnerId = ownerId,
                ItemIds = itemIds,
                Targets = targets
            });
        }

        public static bool TryDestroyBuildable(Transform hit, out string name, out ushort id, out ulong owner)
        {
            name = null;
            id = 0;
            owner = 0;

            for (Transform current = hit; current != null; current = current.parent)
            {
                StructureDrop structure = StructureManager.FindStructureByRootTransform(current);
                if (structure != null && structure.asset != null)
                {
                    byte x, y;
                    StructureRegion region;
                    StructureData data = structure.GetServersideData();
                    if (data == null || !StructureManager.tryGetRegion(current, out x, out y, out region))
                    {
                        return false;
                    }

                    name = structure.asset.itemName;
                    id = structure.asset.id;
                    owner = data.owner;
                    StructureManager.destroyStructure(structure, x, y, Vector3.zero);
                    return true;
                }

                BarricadeDrop barricade = BarricadeManager.FindBarricadeByRootTransform(current);
                if (barricade != null && barricade.asset != null)
                {
                    byte x, y;
                    ushort plant;
                    BarricadeRegion region;
                    BarricadeData data = barricade.GetServersideData();
                    if (data == null || !BarricadeManager.tryGetRegion(current, out x, out y, out plant, out region))
                    {
                        return false;
                    }

                    name = barricade.asset.itemName;
                    id = barricade.asset.id;
                    owner = data.owner;
                    BarricadeManager.destroyBarricade(barricade, x, y, plant);
                    return true;
                }
            }

            return false;
        }

        private readonly Dictionary<ulong, ulong> lastLookups = new Dictionary<ulong, ulong>();

        public void SetLastLookup(ulong callerId, ulong owner)
        {
            lastLookups[callerId] = owner;
        }

        public bool TryGetLastLookup(ulong callerId, out ulong owner)
        {
            return lastLookups.TryGetValue(callerId, out owner);
        }

        public static bool TryGetBuildableOwner(Transform hit, out string name, out ushort id, out ulong owner)
        {
            name = null;
            id = 0;
            owner = 0;

            for (Transform current = hit; current != null; current = current.parent)
            {
                StructureDrop structure = StructureManager.FindStructureByRootTransform(current);
                if (structure != null && structure.asset != null)
                {
                    StructureData data = structure.GetServersideData();
                    if (data == null)
                    {
                        return false;
                    }

                    name = structure.asset.itemName;
                    id = structure.asset.id;
                    owner = data.owner;
                    return true;
                }

                BarricadeDrop barricade = BarricadeManager.FindBarricadeByRootTransform(current);
                if (barricade != null && barricade.asset != null)
                {
                    BarricadeData data = barricade.GetServersideData();
                    if (data == null)
                    {
                        return false;
                    }

                    name = barricade.asset.itemName;
                    id = barricade.asset.id;
                    owner = data.owner;
                    return true;
                }
            }

            return false;
        }

        public bool Confirm(ulong callerId, out int count)
        {
            WreckRequest request;
            if (!requests.TryGetValue(callerId, out request) || request.IsProcessing)
            {
                count = 0;
                return false;
            }

            request.IsProcessing = true;
            count = request.Targets.Count;
            StartCoroutine(DestroyTargets(request));
            return true;
        }

        public bool Abort(ulong callerId, out int destroyed)
        {
            WreckRequest request;
            if (!requests.TryGetValue(callerId, out request))
            {
                destroyed = 0;
                return false;
            }

            request.IsCancelled = true;
            destroyed = request.Destroyed;
            requests.Remove(callerId);
            return true;
        }

        private static List<WreckTarget> FindTargets(Vector3 center, float radius, HashSet<char> categories, ulong? ownerId, HashSet<ushort> itemIds)
        {
            List<WreckTarget> targets = new List<WreckTarget>();
            float radiusSquared = radius * radius;
            List<RegionCoordinate> regionCoordinates = new List<RegionCoordinate>();
            Regions.getRegionsInRadius(center, radius, regionCoordinates);
            HashSet<Transform> seenTransforms = new HashSet<Transform>();

            bool includeBuildables = categories.Contains('*') || categories.Contains('b') || categories.Contains('t') ||
                categories.Contains('d') || categories.Contains('c') || categories.Contains('l') || categories.Contains('w') ||
                categories.Contains('f') || categories.Contains('p') || categories.Contains('r') || categories.Contains('s') ||
                categories.Contains('g') || categories.Contains('i') || categories.Contains('a');
            bool includeVehicles = categories.Contains('*') || categories.Contains('v');

            if (includeBuildables)
            {
                List<Transform> structureTransforms = new List<Transform>();
                StructureManager.getStructuresInRadius(center, radiusSquared, regionCoordinates, structureTransforms);
                foreach (Transform transform in structureTransforms)
                {
                    if (transform == null || !seenTransforms.Add(transform) || (transform.position - center).sqrMagnitude > radiusSquared)
                    {
                        continue;
                    }

                    StructureDrop drop = StructureManager.FindStructureByRootTransform(transform);
                    if (drop == null || drop.asset == null)
                    {
                        continue;
                    }

                    StructureData data = drop.GetServersideData();
                    if (data == null || !Matches(TargetKind.Structure, drop.asset.id, data.owner, categories, ownerId, itemIds))
                    {
                        continue;
                    }

                    targets.Add(new WreckTarget
                    {
                        Kind = TargetKind.Structure,
                        Transform = transform,
                        ItemId = drop.asset.id,
                        OwnerId = data.owner
                    });
                    if (targets.Count > MaximumTargets)
                    {
                        return targets;
                    }
                }

                List<Transform> barricadeTransforms = new List<Transform>();
                BarricadeManager.getBarricadesInRadius(center, radiusSquared, regionCoordinates, barricadeTransforms);
                foreach (Transform transform in barricadeTransforms)
                {
                    if (transform == null || !seenTransforms.Add(transform) || (transform.position - center).sqrMagnitude > radiusSquared)
                    {
                        continue;
                    }

                    BarricadeDrop drop = BarricadeManager.FindBarricadeByRootTransform(transform);
                    if (drop == null || drop.asset == null)
                    {
                        continue;
                    }

                    BarricadeData data = drop.GetServersideData();
                    if (data == null || !Matches(TargetKind.Barricade, drop.asset.id, data.owner, categories, ownerId, itemIds))
                    {
                        continue;
                    }

                    targets.Add(new WreckTarget
                    {
                        Kind = TargetKind.Barricade,
                        Transform = transform,
                        ItemId = drop.asset.id,
                        OwnerId = data.owner
                    });
                    if (targets.Count > MaximumTargets)
                    {
                        return targets;
                    }
                }
            }

            if (includeVehicles && VehicleManager.vehicles != null)
            {
                foreach (InteractableVehicle vehicle in VehicleManager.vehicles)
                {
                    if (vehicle == null || vehicle.isDead || vehicle.transform == null ||
                        (vehicle.transform.position - center).sqrMagnitude > radiusSquared ||
                        !Matches(TargetKind.Vehicle, vehicle.id, vehicle.lockedOwner.m_SteamID, categories, ownerId, itemIds))
                    {
                        continue;
                    }

                    targets.Add(new WreckTarget
                    {
                        Kind = TargetKind.Vehicle,
                        Transform = vehicle.transform,
                        Vehicle = vehicle,
                        ItemId = vehicle.id,
                        OwnerId = vehicle.lockedOwner.m_SteamID
                    });
                    if (targets.Count > MaximumTargets)
                    {
                        return targets;
                    }
                }
            }

            return targets;
        }

        private static bool Matches(TargetKind kind, ushort itemId, ulong targetOwnerId, HashSet<char> categories, ulong? ownerId, HashSet<ushort> itemIds)
        {
            if (ownerId.HasValue && ownerId.Value != targetOwnerId)
            {
                return false;
            }

            if (itemIds != null && !itemIds.Contains(itemId))
            {
                return false;
            }

            if (categories.Contains('*'))
            {
                return true;
            }

            if (kind == TargetKind.Vehicle)
            {
                return categories.Contains('v');
            }

            char category;
            return ItemCategories.TryGetValue(itemId, out category) && categories.Contains(category);
        }

        private IEnumerator DestroyTargets(WreckRequest request)
        {
            for (int i = 0; i < request.Targets.Count; i++)
            {
                if (request.IsCancelled)
                {
                    break;
                }

                if (i > 0 && i % TargetsPerFrame == 0)
                {
                    yield return null;
                    if (request.IsCancelled)
                    {
                        break;
                    }
                }

                try
                {
                    if (DestroyTarget(request, request.Targets[i]))
                    {
                        request.Destroyed++;
                    }
                }
                catch (Exception exception)
                {
                    request.Failed++;
                    Logger.LogError("Wreck failed for item " + request.Targets[i].ItemId + " owned by " + request.Targets[i].OwnerId + ": " + exception);
                }
            }

            WreckRequest currentRequest;
            if (requests.TryGetValue(request.CallerId, out currentRequest) && ReferenceEquals(currentRequest, request))
            {
                requests.Remove(request.CallerId);
            }

            if (!request.IsCancelled)
            {
                UnturnedPlayer caller = UnturnedPlayer.FromCSteamID(new CSteamID(request.CallerId));
                if (caller != null)
                {
                    UnturnedChat.Say(
                        caller,
                        Main.Instance.Translate("WreckCompleted", request.Destroyed, request.Targets.Count - request.Destroyed - request.Failed, request.Failed),
                        Main.Instance.MessageColor);
                }
            }
        }

        private static bool DestroyTarget(WreckRequest request, WreckTarget target)
        {
            if (target.Transform == null || (target.Transform.position - request.Center).sqrMagnitude > request.Radius * request.Radius)
            {
                return false;
            }

            switch (target.Kind)
            {
                case TargetKind.Structure:
                    StructureDrop structure = StructureManager.FindStructureByRootTransform(target.Transform);
                    if (structure == null || structure.asset == null || structure.asset.id != target.ItemId)
                    {
                        return false;
                    }

                    StructureData structureData = structure.GetServersideData();
                    if (structureData == null || structureData.owner != target.OwnerId ||
                        !Matches(target.Kind, target.ItemId, structureData.owner, request.Categories, request.OwnerId, request.ItemIds))
                    {
                        return false;
                    }

                    byte structureX, structureY;
                    StructureRegion structureRegion;
                    if (!StructureManager.tryGetRegion(target.Transform, out structureX, out structureY, out structureRegion))
                    {
                        return false;
                    }

                    StructureManager.destroyStructure(structure, structureX, structureY, Vector3.zero);
                    return true;

                case TargetKind.Barricade:
                    BarricadeDrop barricade = BarricadeManager.FindBarricadeByRootTransform(target.Transform);
                    if (barricade == null || barricade.asset == null || barricade.asset.id != target.ItemId)
                    {
                        return false;
                    }

                    BarricadeData barricadeData = barricade.GetServersideData();
                    if (barricadeData == null || barricadeData.owner != target.OwnerId ||
                        !Matches(target.Kind, target.ItemId, barricadeData.owner, request.Categories, request.OwnerId, request.ItemIds))
                    {
                        return false;
                    }

                    byte barricadeX, barricadeY;
                    ushort plant;
                    BarricadeRegion barricadeRegion;
                    if (!BarricadeManager.tryGetRegion(target.Transform, out barricadeX, out barricadeY, out plant, out barricadeRegion))
                    {
                        return false;
                    }

                    BarricadeManager.destroyBarricade(barricade, barricadeX, barricadeY, plant);
                    return true;

                case TargetKind.Vehicle:
                    InteractableVehicle vehicle = target.Vehicle;
                    if (vehicle == null || vehicle.isDead || vehicle.id != target.ItemId ||
                        vehicle.lockedOwner.m_SteamID != target.OwnerId ||
                        !Matches(target.Kind, target.ItemId, vehicle.lockedOwner.m_SteamID, request.Categories, request.OwnerId, request.ItemIds))
                    {
                        return false;
                    }

                    VehicleManager.askVehicleDestroy(vehicle);
                    return true;

                default:
                    return false;
            }
        }

        private void OnDestroy()
        {
            foreach (WreckRequest request in requests.Values)
            {
                request.IsCancelled = true;
            }
            requests.Clear();
        }

        private static Dictionary<ushort, char> CreateItemCategories()
        {
            return new Dictionary<ushort, char>
            {
                { 288, 'b' }, { 289, 'b' }, { 290, 'b' }, { 291, 'b' }, { 292, 'b' }, { 293, 'b' }, { 294, 'b' }, { 295, 'b' },
                { 382, 't' }, { 383, 't' }, { 384, 't' }, { 385, 't' }, { 386, 't' },
                { 281, 'd' }, { 282, 'd' }, { 283, 'd' }, { 378, 'd' }, { 284, 'd' }, { 286, 'd' }, { 451, 'd' }, { 455, 'd' }, { 456, 'd' }, { 457, 'd' },
                { 328, 'c' }, { 366, 'c' }, { 367, 'c' }, { 368, 'c' },
                { 325, 'l' }, { 326, 'l' }, { 327, 'l' }, { 379, 'l' },
                { 33, 'w' }, { 57, 'w' }, { 58, 'w' }, { 371, 'w' }, { 32, 'w' }, { 49, 'w' }, { 50, 'w' }, { 370, 'w' },
                { 34, 'w' }, { 59, 'w' }, { 60, 'w' }, { 372, 'w' }, { 450, 'w' }, { 452, 'w' }, { 453, 'w' }, { 454, 'w' }, { 442, 'w' }, { 444, 'w' }, { 445, 'w' }, { 446, 'w' },
                { 31, 'f' }, { 51, 'f' }, { 52, 'f' }, { 369, 'f' },
                { 36, 'p' }, { 53, 'p' }, { 54, 'p' }, { 374, 'p' }, { 443, 'p' }, { 447, 'p' }, { 448, 'p' }, { 449, 'p' },
                { 35, 'r' }, { 55, 'r' }, { 56, 'r' }, { 373, 'r' }, { 319, 'r' }, { 321, 'r' }, { 320, 'r' }, { 376, 'r' },
                { 316, 's' }, { 318, 's' }, { 317, 's' }, { 375, 's' }, { 322, 's' }, { 323, 's' }, { 324, 's' }, { 377, 's' },
                { 29, 'g' }, { 30, 'g' }, { 45, 'g' }, { 46, 'g' }, { 47, 'g' }, { 287, 'g' }, { 365, 'g' },
                { 359, 'i' }, { 360, 'i' }, { 361, 'i' }, { 362, 'i' }, { 458, 'i' }, { 459, 'i' },
                { 330, 'a' }, { 331, 'a' }, { 336, 'a' }, { 339, 'a' }, { 341, 'a' }, { 343, 'a' }, { 345, 'a' }
            };
        }
    }
}
