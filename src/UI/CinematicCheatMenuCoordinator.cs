using System.Collections.Generic;
using System.Reflection;
using UltraCinematic.Core;
using UnityEngine;
using UnityEngine.UI;

namespace UltraCinematic.UI
{
    internal sealed class CinematicCheatMenuCoordinator : MonoBehaviour
    {
        private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private CinematicController controller;
        private CheatsManager manager;
        private int refreshPassesRemaining;

        public int RefreshCount;
        public int LastMenuItemCount;
        public int LastMatchedRowCount;
        public int LastVisibleActionRowCount;
        public bool LastEditModeEnabled;
        public string LastRefreshError = "";

        internal void Initialize(CinematicController cinematicController) { controller = cinematicController; }

        internal void Attach(CheatsManager cheatsManager)
        {
            manager = cheatsManager;
            Refresh();
            RequestRefresh();
        }

        internal void RequestRefresh()
        {
            if (refreshPassesRemaining < 3) refreshPassesRemaining = 3;
        }

        private void OnEnable() { RequestRefresh(); }

        private void LateUpdate()
        {
            if (refreshPassesRemaining <= 0) return;
            refreshPassesRemaining--;
            try { Refresh(); }
            catch (System.Exception error)
            {
                LastRefreshError = error.ToString();
                UltraCinematicPlugin.Log?.LogError("Deferred cinematic menu refresh failed: " + error);
            }
        }

        internal void Refresh()
        {
            if (manager == null) return;
            FieldInfo menuItemsField = typeof(CheatsManager).GetField("menuItems", PrivateInstance);
            var menuItems = menuItemsField?.GetValue(manager) as Dictionary<ICheat, CheatMenuItem>;
            if (menuItems == null) return;

            RefreshCount++;
            LastMenuItemCount = menuItems.Count;
            LastMatchedRowCount = 0;
            LastVisibleActionRowCount = 0;
            LastEditModeEnabled = controller != null && controller.EditModeEnabled;
            LastRefreshError = "";

            foreach (KeyValuePair<ICheat, CheatMenuItem> pair in menuItems)
            {
                string id = pair.Key.Identifier;
                bool visible = true;
                bool interactable = true;
                if (id == "ultracinematic.edit-mode")
                {
                    visible = true;
                    if (pair.Value.stateButton != null)
                    {
                        ICheat editCheat = pair.Key;
                        pair.Value.stateButton.onClick.RemoveAllListeners();
                        pair.Value.stateButton.onClick.AddListener(() => manager.ToggleCheat(editCheat));
                    }
                }
                else if (id == "ultracinematic.play")
                {
                    visible = controller.EditModeEnabled;
                    interactable = controller.EditModeEnabled;
                }
                else if (id == "ultracinematic.add-point") visible = controller.EditModeEnabled && !controller.PlaybackActive;
                else if (id == "ultracinematic.delete-last-point")
                {
                    visible = controller.EditModeEnabled && !controller.PlaybackActive;
                    interactable = controller.EditModeEnabled;
                }
                else if (id == "ultracinematic.open-timeline") visible = controller.EditModeEnabled && !controller.PlaybackActive;
                else if (id == "ultracinematic.pause-game")
                {
                    visible = controller.EditModeEnabled && !controller.PlaybackActive;
                    interactable = controller.EditModeEnabled && !controller.TimelineOpen;
                }
                else continue;

                Transform slotTextTransform = pair.Value.transform.Find("Slot Text");
                Text slotText = slotTextTransform == null ? null : slotTextTransform.GetComponent<Text>();
                if (slotText != null) slotText.text = pair.Key.LongName;

                LastMatchedRowCount++;
                if (id != "ultracinematic.edit-mode" && visible) LastVisibleActionRowCount++;
                pair.Value.gameObject.SetActive(visible);
                if (pair.Value.stateButton != null) pair.Value.stateButton.interactable = interactable;
            }

            manager.RefreshCheatStates();
            FieldInfo containerField = typeof(CheatsManager).GetField("itemContainer", PrivateInstance);
            GameObject container = containerField?.GetValue(manager) as GameObject;
            if (container != null)
            {
                Canvas.ForceUpdateCanvases();
                RectTransform rect = container.GetComponent<RectTransform>();
                if (rect != null) LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            }
        }
    }
}
