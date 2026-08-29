using Infrastructure.SaveSystem;
using TMPro;
using UnityEngine;

namespace UI.Profile
{
    public sealed class SaveSlotView : MonoBehaviour
    {
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text date;

        private int _index;

        public void Bind(SaveSlotInfo info)
        {
            _index = info.Index;
            title.text = $"Слот {info.Index}";
            date.text = info.UpdatedAt.ToString("dd.MM.yyyy HH:mm");
        }

        public void OnLoadClicked()
        {
            Debug.Log($"Load slot {_index}");
        }

        public void OnDeleteClicked()
        {
            Debug.Log($"Delete slot {_index}");
        }
    }
}