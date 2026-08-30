using UnityEngine;
using Zenject;


public class PlayerSkin : MonoBehaviour
{
    public MeshRenderer Mesh=>_mesh;
    private CameraSettings _settings;

    [SerializeField] private MeshRenderer _mesh;


    [Inject]
    private void Construct(CameraSettings settings)
    {
        _settings = settings;
    }

    private void Update()
    {
        _mesh.enabled = _settings.Mode switch
        {
            CameraMode.FirstPerson => false,
            CameraMode.ThirdPerson => true,
            _ => false
        };
    }

}