using System.Collections.Generic;
using Infrastructure.SaveSystem;
using UnityEngine;

namespace UI.Profile
{
    public sealed class SaveSlotsPresenter : MonoBehaviour
    {
        [SerializeField] private SaveSlotView slotPrefab;
        [SerializeField] private Transform container;

        private readonly JsonFileGateway _gateway = new();

        public void Refresh()
        {
            foreach (Transform child in container)
                Destroy(child.gameObject);

            IReadOnlyList<SaveSlotInfo> slots = _gateway.EnumerateUserFiles();

            foreach (var slot in slots)
            {
                var view = Instantiate(slotPrefab, container);
                view.Bind(slot);
            }
        }

        public void CreateNewSlot()
        {
            _gateway.CreateNextUserFile();
            Refresh();
        }
    }
}