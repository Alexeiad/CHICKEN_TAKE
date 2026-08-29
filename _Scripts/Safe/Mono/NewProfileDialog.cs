using TMPro;
using UnityEngine;

namespace UI.Profile
{
    public sealed class NewProfileDialog : MonoBehaviour
    {
        [SerializeField] private TMP_InputField nameInput;
        [SerializeField] private SaveSlotsPresenter presenter;

        public void CreateProfile()
        {
            string userName = nameInput.text.Trim();

            if (string.IsNullOrEmpty(userName))
                return;

            presenter.CreateNewSlot();

            // Здесь позже:
            // 1. создать UserProfileSO
            // 2. записать UserName
            // 3. сохранить профиль в новый слот
            // 4. сделать слот активным

            gameObject.SetActive(false);
        }
    }
}