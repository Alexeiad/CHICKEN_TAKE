using Infrastructure.SaveSystem;
using UnityEngine;

namespace UI.Profile
{
    public sealed class NewProfileGate : MonoBehaviour
    {
        [SerializeField] private GameObject createProfileDialog;

        private readonly JsonFileGateway _gateway = new();

        public void OnPlayPressed()
        {
            bool hasSaves = _gateway.EnumerateUserFiles().Count > 0;

            if (!hasSaves)
                createProfileDialog.SetActive(true);
        }
    }
}