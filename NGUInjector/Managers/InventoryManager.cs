﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using static NGUInjector.Main;

namespace NGUInjector
{
    internal class InventoryManager
    {
        private readonly Character _character;
        private readonly InventoryController _controller;

        private readonly int[] _pendants = { 53, 76, 94, 142, 170, 229, 295, 388, 430, 504 };
        private readonly int[] _lootys = { 67, 128, 169, 230, 296, 389, 431, 505 };
        private readonly int[] _convertibles;
        private readonly int[] _wandoos = {66, 169};
        private readonly int[] _guffs = {228, 211, 250, 291, 289, 290, 298, 299, 300};
        private int counter = 0;


        //Wandoos 98, Giant Seed, Wandoos XL, Lonely Flubber, Wanderer's Cane, Guffs
        private readonly int[] _filterExcludes = { 66, 92, 163, 120, 154, 278, 279, 280, 281, 282, 283, 284, 285, 286, 287  };
        public InventoryManager()
        {
            _character = Main.Character;
            _controller = Controller;
            var temp = _pendants.Concat(_lootys).ToList();
            temp.Add(154);
            _convertibles = temp.ToArray();
        }
        
        internal void BoostEquipped()
        {
            // Boost Equipped Slots
            if (!Settings.BoostBlacklist.Contains(_character.inventory.head.id))
                _controller.applyAllBoosts(-1);
            if (!Settings.BoostBlacklist.Contains(_character.inventory.chest.id))
                _controller.applyAllBoosts(-2);
            if (!Settings.BoostBlacklist.Contains(_character.inventory.legs.id))
                _controller.applyAllBoosts(-3);
            if (!Settings.BoostBlacklist.Contains(_character.inventory.boots.id))
                _controller.applyAllBoosts(-4);
            if (!Settings.BoostBlacklist.Contains(_character.inventory.weapon.id))
                _controller.applyAllBoosts(-5);

            if (_controller.weapon2Unlocked() && !Settings.BoostBlacklist.Contains(_character.inventory.head.id))
                _controller.applyAllBoosts(-6);
        }

        internal void BoostAccessories()
        {
            for (var i = 10000; _controller.accessoryID(i) < _controller.accessorySpaces(); i++)
            {
                if (!Settings.BoostBlacklist.Contains(_character.inventory.accs[_controller.accessoryID(i)].id))
                    _controller.applyAllBoosts(i);
            }
        }

        internal void BoostInventory(ih[] ih)
        {
            foreach (var item in Settings.BoostIDs)
            {
                if (isEquipped(item))
                    continue;
                //Find all inventory slots that match this item id
                var targets =
                    ih.Where(x => x.id == item && !Settings.BoostBlacklist.Contains(x.id)).ToArray();

                switch (targets.Length)
                {
                    case 0:
                        continue;
                    case 1:
                        _controller.applyAllBoosts(targets[0].slot);
                        continue;
                    default:
                        //Find the highest level version of the item (locked = +100) and apply boosts to it
                        _controller.applyAllBoosts(targets.MaxItem().slot);
                        break;
                }
            }
        }

        private bool isEquipped(int id)
        {
            var inv = Main.Character.inventory;
            if (inv.head.id == id)
            {
                return true;
            }

            if (inv.chest.id == id)
            {
                return true;
            }

            if (inv.legs.id == id)
            {
                return true;
            }

            if (inv.boots.id == id)
            {
                return true;
            }

            if (inv.weapon.id == id)
            {
                return true;
            }

            if (Controller.weapon2Unlocked())
            {
                if (inv.weapon2.id == id)
                {
                    return true;
                }
            }

            return inv.accs.Any(t => t.id == id);
        }

        private int ChangePage(int slot)
        {
            var page = (int)Math.Floor((double)slot / 60);
            _controller.changePage(page);
            return slot - (page * 60);
        }

        internal void BoostInfinityCube()
        {
            _controller.infinityCubeAll();
            _controller.updateInventory();
        }

        internal void MergeEquipped()
        {
            // Boost Equipped Slots
            _controller.mergeAll(-1);
            _controller.mergeAll(-2);
            _controller.mergeAll(-3);
            _controller.mergeAll(-4);
            _controller.mergeAll(-5);

            if (_controller.weapon2Unlocked())
            {
                _controller.mergeAll(-6);
            }

            //Boost Accessories
            for (var i = 10000; _controller.accessoryID(i) < _controller.accessorySpaces(); i++)
            {
                _controller.mergeAll(i);
            }
        }

        internal void MergeBoosts(ih[] ci)
        {
            var grouped = ci.Where(x =>
                x.id <= 40 && !_character.inventory.inventory[x.slot].removable &&
                !_character.inventory.itemList.itemMaxxed[x.id]);

            foreach (var target in grouped)
            {
                if (ci.Count(x => x.id == target.id) <= 1) continue;
                Log($"Merging {target.name} in slot {target.slot}");
                _controller.mergeAll(target.slot);
            }
        }

        internal void ManageQuestItems(ih[] ci)
        {
            //Merge quest items first
            var toMerge = ci.Where(x =>
                x.id >= 278 && x.id <= 287 && !_character.inventory.inventory[x.slot].removable &&
                !_character.inventory.itemList.itemMaxxed[x.id]);

            foreach (var target in toMerge)
            {
                if (ci.Count(x => x.id == target.id) <= 1) continue;
                Log($"Merging {target.name} in slot {target.slot}");
                _controller.mergeAll(target.slot);
            }

            //Consume quest items that dont need to be merged
            var questItems = ci.Where(x =>
                x.id >= 278 && x.id <= 287 && _character.inventory.inventory[x.slot].removable);

            foreach (var target in questItems)
            {
                var newSlot = ChangePage(target.slot);
                var ic = _controller.inventory[newSlot];
                Log($"Using quest item {target.name} in slot {target.slot}");
                typeof(ItemController).GetMethod("consumeItem", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.Invoke(ic, null);
            }
        }

        internal void MergeInventory(ih[] ci)
        {
            var grouped =
                ci.Where(x => x.id > 40 && x.level < 100 && (x.id < 278 || x.id > 287)).GroupBy(x => x.id).Where(x => x.Count() > 1);

            foreach (var item in grouped)
            {
                var target = item.MaxItem();

                Log($"Merging {target.name} in slot {target.slot}");
                _controller.mergeAll(target.slot);
            }
        }

        internal void MergeGuffs()
        {
            for (var id = 1000000; id - 1000000 < _character.inventory.macguffins.Count; ++id)
                _controller.mergeAll(id);
        }

        internal void ManageConvertibles(ih[] ci)
        {
            var grouped = ci.Where(x => _convertibles.Contains(x.id));
            foreach (var item in grouped)
            {
                if (item.level != 100) continue;
                var temp = _character.inventory.inventory[item.slot];
                if (!temp.removable) continue;
                var newSlot = ChangePage(item.slot);
                var ic = _controller.inventory[newSlot];
                typeof(ItemController).GetMethod("consumeItem", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.Invoke(ic, null);
            }
        }

        internal void ChangeBoostConversion(ih[] ci)
        {
            if (!Settings.AutoConvertBoosts)
                return;
            var needed = new BoostsNeeded();

            needed.Add(_character.inventory.head.GetNeededBoosts(true));
            needed.Add(_character.inventory.chest.GetNeededBoosts(true));
            needed.Add(_character.inventory.legs.GetNeededBoosts(true));
            needed.Add(_character.inventory.boots.GetNeededBoosts(true));
            needed.Add(_character.inventory.weapon.GetNeededBoosts(true));
            if (_controller.weapon2Unlocked())
                needed.Add(_character.inventory.weapon2.GetNeededBoosts(true));

            for (var i = 10000; _controller.accessoryID(i) < _controller.accessorySpaces(); i++)
            {
                needed.Add(_character.inventory.accs[_controller.accessoryID(i)].GetNeededBoosts(true));
            }

            foreach (var item in Settings.BoostIDs)
            {
                if (isEquipped(item))
                    continue;
                //Find all inventory slots that match this item name
                var targets =
                    ci.Where(x => x.id == item && !Settings.BoostBlacklist.Contains(x.id)).ToArray();

                switch (targets.Length)
                {
                    case 0:
                        continue;
                    case 1:
                        needed.Add(targets[0].equipment.GetNeededBoosts(false));
                        continue;
                    default:
                        //Find the highest level version of the item (locked = +100) and apply boosts to it
                        needed.Add(targets.MaxItem().equipment.GetNeededBoosts(false));
                        break;
                }
            }

            if (counter == 0)
                Log($"Boosts Needed to Green: {needed.Power} Power, {needed.Toughness} Toughness, {needed.Special} Special");

            counter++;
            if (counter == 6) counter = 0;

            if (needed.Power > 0)
            {
                _controller.selectAutoPowerTransform();
                return;
            }

            if (needed.Toughness > 0)
            {
                _controller.selectAutoToughTransform();
                return;
            }

            if (needed.Special > 0)
            {
                _controller.selectAutoSpecialTransform();
                return;
            }

            _controller.selectAutoNoneTransform();
        }

        #region Filtering
        internal void EnsureFiltered(ih[] ci)
        {
            var targets = ci.Where(x => x.level == 100);
            foreach (var target in targets)
            {
                FilterItem(target.id);
            }

            FilterEquip(_character.inventory.head);
            FilterEquip(_character.inventory.boots);
            FilterEquip(_character.inventory.chest);
            FilterEquip(_character.inventory.legs);
            FilterEquip(_character.inventory.weapon);
            if (_character.inventoryController.weapon2Unlocked())
                FilterEquip(_character.inventory.weapon2);

            foreach (var acc in _character.inventory.accs)
            {
                FilterEquip(acc);
            }
        }

        void FilterItem(int id)
        {
            if (_pendants.Contains(id) || _lootys.Contains(id) || _wandoos.Contains(id) ||
                _filterExcludes.Contains(id) || _guffs.Contains(id) || id < 40)
                return;

            //Dont filter quest items
            if (id >= 278 && id <= 287)
                return;


            _character.inventory.itemList.itemFiltered[id] = true;
        }

        void FilterEquip(Equipment e)
        {
            if (e.level == 100)
            {
                FilterItem(e.id);
            }
        }
        #endregion

    }

    public class ih
    {
        internal int slot { get; set; }
        internal string name { get; set; }
        internal int level { get; set; }
        internal bool locked { get; set; }
        internal int id { get; set; }
        internal Equipment equipment { get; set; }
    }

    public class BoostsNeeded
    {
        internal float Power { get; set; }
        internal float Toughness { get; set; }
        internal float Special { get; set; }

        public BoostsNeeded()
        {
            Power = 0;
            Toughness = 0;
            Special = 0;
        }

        public void Add(BoostsNeeded other)
        {
            Power += other.Power;
            Toughness += other.Toughness;
            Special += other.Special;
        }
    }
}