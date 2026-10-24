using SDG.Unturned;
using System.Collections.Generic;
using UnityEngine;

namespace CCRP_Plugin.Services
{
    public class ItemFeaturesService : MonoBehaviour
    {
        private sealed class ItemFeatureState
        {
            public ItemJar Item;
            public bool AutoReload;
            public bool AutoRepair;
        }

        private const float UpdateInterval = 0.05f;

        private readonly List<ItemFeatureState> itemStates = new List<ItemFeatureState>();
        private Player player;
        private bool turretAutoReload;
        private bool turretAutoRepair;
        private float nextUpdate;
        private float nextPrune;

        public void Initialize(Player owner)
        {
            if (owner != null)
            {
                player = owner;
            }
        }

        public void SetFeatures(ItemJar item, bool autoReload, bool autoRepair)
        {
            if (item == null)
            {
                return;
            }

            ItemFeatureState state = FindState(item);
            if (state == null && (autoReload || autoRepair))
            {
                state = new ItemFeatureState { Item = item };
                itemStates.Add(state);
            }

            if (state == null)
            {
                return;
            }

            state.AutoReload = autoReload;
            state.AutoRepair = autoRepair;

            if (state.AutoReload && player != null && player.equipment != null &&
                player.inventory != null && player.equipment.isEquipped &&
                ReferenceEquals(FindHeldItem(player.equipment, player.inventory), item))
            {
                ReloadHeldGun(player.equipment, false);
            }

            if (!state.AutoReload && !state.AutoRepair)
            {
                itemStates.Remove(state);
            }
        }

        public void GetFeatures(ItemJar item, out bool autoReload, out bool autoRepair)
        {
            ItemFeatureState state = item == null ? null : FindState(item);
            autoReload = state != null && state.AutoReload;
            autoRepair = state != null && state.AutoRepair;
        }

        public void SetTurretFeatures(bool autoReload, bool autoRepair)
        {
            turretAutoReload = autoReload;
            turretAutoRepair = autoRepair;
        }

        public void GetTurretFeatures(out bool autoReload, out bool autoRepair)
        {
            autoReload = turretAutoReload;
            autoRepair = turretAutoRepair;
        }

        private void Update()
        {
            if (player == null)
            {
                return;
            }

            if (Time.time < nextUpdate)
            {
                return;
            }

            nextUpdate = Time.time + UpdateInterval;

            PlayerEquipment equipment = player.equipment;
            PlayerInventory inventory = player.inventory;
            if (equipment != null && equipment.isTurret)
            {
                if (turretAutoRepair && equipment.quality < byte.MaxValue)
                {
                    equipment.quality = byte.MaxValue;
                    equipment.updateQuality();
                    equipment.sendUpdateQuality();
                }

                if (turretAutoReload)
                {
                    ReloadHeldGun(equipment, true);
                }

                return;
            }

            turretAutoReload = false;
            turretAutoRepair = false;

            if (inventory != null && Time.time >= nextPrune)
            {
                nextPrune = Time.time + 10f;
                PruneRemovedItems(inventory);
            }

            if (equipment == null || inventory == null || !equipment.isEquipped)
            {
                return;
            }

            ItemJar heldItem = FindHeldItem(equipment, inventory);
            if (heldItem == null)
            {
                return;
            }

            ItemFeatureState state = FindState(heldItem);
            if (state == null)
            {
                return;
            }

            if (state.AutoRepair && equipment.quality < byte.MaxValue)
            {
                equipment.quality = byte.MaxValue;
                equipment.updateQuality();
                equipment.sendUpdateQuality();
            }

            if (state.AutoReload)
            {
                ReloadHeldGun(equipment, false);
            }
        }

        private static ItemJar FindHeldItem(PlayerEquipment equipment, PlayerInventory inventory)
        {
            Items[] pages = inventory.items;
            byte pageIndex = equipment.equippedPage;
            if (pages == null || pageIndex >= pages.Length)
            {
                return null;
            }

            Items page = pages[pageIndex];
            if (page == null)
            {
                return null;
            }

            byte count = page.getItemCount();
            for (byte index = 0; index < count; index++)
            {
                ItemJar item = page.getItem(index);
                if (item != null && item.x == equipment.equipped_x && item.y == equipment.equipped_y)
                {
                    return item;
                }
            }

            return null;
        }

        private static void ReloadHeldGun(PlayerEquipment equipment, bool allowMissingMagazine)
        {
            ItemGunAsset gunAsset = equipment.asset as ItemGunAsset;
            if (gunAsset == null || equipment.state == null)
            {
                return;
            }

            byte[] state = equipment.state;
            // Gun item state: attachment IDs occupy bytes 0-9, ammo is byte 10,
            // and fire mode is byte 11.
            if (state.Length <= 10)
            {
                return;
            }

            ushort magazineId = (ushort)(state[8] | (state[9] << 8));
            if (!allowMissingMagazine && magazineId == 0)
            {
                return;
            }

            byte capacity = gunAsset.ammoMax;
            if (capacity == 0 || state[10] >= capacity)
            {
                return;
            }

            state[10] = capacity;
            equipment.state = state;
            equipment.updateState();
            equipment.sendUpdateState();
        }

        private ItemFeatureState FindState(ItemJar item)
        {
            for (int i = 0; i < itemStates.Count; i++)
            {
                if (ReferenceEquals(itemStates[i].Item, item))
                {
                    return itemStates[i];
                }
            }

            return null;
        }

        private void PruneRemovedItems(PlayerInventory inventory)
        {
            if (inventory == null || itemStates.Count == 0)
            {
                return;
            }

            Items[] pages = inventory.items;
            for (int stateIndex = itemStates.Count - 1; stateIndex >= 0; stateIndex--)
            {
                bool found = false;
                if (pages != null)
                {
                    for (int pageIndex = 0; pageIndex < pages.Length && !found; pageIndex++)
                    {
                        Items page = pages[pageIndex];
                        if (page == null)
                        {
                            continue;
                        }

                        byte count = page.getItemCount();
                        for (byte itemIndex = 0; itemIndex < count; itemIndex++)
                        {
                            if (ReferenceEquals(page.getItem(itemIndex), itemStates[stateIndex].Item))
                            {
                                found = true;
                                break;
                            }
                        }
                    }
                }

                if (!found)
                {
                    itemStates.RemoveAt(stateIndex);
                }
            }
        }

        private void OnDestroy()
        {
            itemStates.Clear();
            turretAutoReload = false;
            turretAutoRepair = false;
            player = null;
        }
    }
}
