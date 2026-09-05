using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.PixelFantasy.Common.Scripts.EditorScripts
{
    public class Inventory : MonoBehaviour
    {
        public Transform Grid;
        public GameObject ItemPrefab;
        public Sprite EmptyIcon;

        public void Initialize(List<InventoryItem> items, int selectedIndex = 0)
        {
            foreach (Transform item in Grid)
            {
                Destroy(item.gameObject);
            }

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var instance = Instantiate(ItemPrefab, Grid);
                
                instance.transform.Find("Icon").GetComponent<Image>().sprite = item.Icon ?? EmptyIcon;

                var toggle = instance.GetComponent<Toggle>();

                toggle.isOn = i == selectedIndex;
                toggle.group = Grid.GetComponent<ToggleGroup>();
                toggle.onValueChanged.AddListener(value => { if (value) { item.OnSelect?.Invoke(); } });
            }
        }
    }

    public class InventoryItem
    {
        public string Name;
        public Sprite Icon;
        public Action OnSelect;
    }
}