using System.Linq;
using FMODUnity;
using UnityEngine;
using Zenject;

public class VehicleSpawner : MonoBehaviour
{
    [Header("Vehicle")]
    [SerializeField] private GameObject _vehicleObject;
    [SerializeField] private GameObject _vehiclePlayerObject;
    [SerializeField] private Component[] _vehiclePlayerComponents;

    [Header("Interaction")]
    [SerializeField] private float _interactionDistance = 2f;
    [SerializeField] private KeyCode _interactionKey = KeyCode.E;

    [Header("Interaction UI")]
    [SerializeField] private PlayerInteractionZoneSO _interactionZone;

    [Header("Audio")]
    [SerializeField] private EventReference _interactionEvent;

    private IEntityRegistry<IEntity> _entityRegistry;

    private bool _wasInZone;

    [Inject]
    public void Construct(IEntityRegistry<IEntity> entityRegistry)
    {
        _entityRegistry = entityRegistry;
    }

    private void Update()
    {
        if (_vehicleObject == null || _vehiclePlayerObject == null)
            return;

        if (_vehiclePlayerObject.activeInHierarchy)
        {
            TryExitVehicle();
            return;
        }

        UpdateInteractionZone();
        TryEnterVehicle();
    }

    private void UpdateInteractionZone()
    {
        if (_interactionZone == null)
            return;

        Player player = GetActivePlayer();

        if (player == null)
        {
            SetInteractionZone(false);
            return;
        }

        float distance = GetHorizontalDistance(
            player.transform.position,
            GetInteractionPosition());

        SetInteractionZone(distance <= _interactionDistance);
    }

    private void SetInteractionZone(bool isInZone)
    {
        if (_wasInZone == isInZone)
            return;

        _wasInZone = isInZone;

        _interactionZone.SetZoneState(isInZone);
    }

    private void TryEnterVehicle()
    {
        Player player = GetActivePlayer();

        if (player == null)
            return;

        float distance = GetHorizontalDistance(
            player.transform.position,
            GetInteractionPosition());

        if (distance > _interactionDistance)
            return;

        if (!Input.GetKeyDown(_interactionKey))
            return;

        EnterVehicle(player);
    }

    private void EnterVehicle(Player player)
    {
        if (!_vehicleObject.activeInHierarchy)
        {
            Vector3 spawnPosition = transform.position;
            spawnPosition.y = _vehicleObject.transform.position.y;

            _vehicleObject.transform.SetPositionAndRotation(
                spawnPosition,
                transform.rotation);

            _vehicleObject.SetActive(true);
        }

        PlayerFootstepAudio footstepAudio =
            player.GetComponent<PlayerFootstepAudio>();

        if (footstepAudio != null)
            footstepAudio.StopFootsteps();

        PlayerFootstepAudio.isOnSeat = true;

        player.gameObject.SetActive(false);

        _vehiclePlayerObject.SetActive(true);
        SetVehiclePlayerComponentsActive(true);

        // Сразу отключаем UI взаимодействия
        SetInteractionZone(false);

        PlayInteractionSound();
    }

    private void TryExitVehicle()
    {
        if (!_vehiclePlayerObject.activeInHierarchy)
            return;

        float distance = GetHorizontalDistance(
            _vehiclePlayerObject.transform.position,
            _vehicleObject.transform.position);

        if (distance > _interactionDistance)
            return;

        if (!Input.GetKeyDown(_interactionKey))
            return;

        ExitVehicle();
    }

    private void ExitVehicle()
    {
        Player player = GetPlayer();

        if (player == null)
            return;

        Vector3 exitPosition =
            _vehicleObject.transform.position +
            _vehicleObject.transform.right * _interactionDistance;

        exitPosition.y = player.transform.position.y;

        PlayerFootstepAudio footstepAudio =
            player.GetComponent<PlayerFootstepAudio>();

        if (footstepAudio != null)
            footstepAudio.StopFootsteps();

        SetVehiclePlayerComponentsActive(false);

        _vehiclePlayerObject.SetActive(false);

        player.transform.position = exitPosition;
        player.gameObject.SetActive(true);

        PlayerFootstepAudio.isOnSeat = false;

        // После выхода проверяем, находится ли игрок снова в зоне
        UpdateInteractionZone();
    }

    private void PlayInteractionSound()
    {
        if (_interactionEvent.IsNull)
            return;

        RuntimeManager.PlayOneShot(
            _interactionEvent,
            transform.position);
    }

    private Vector3 GetInteractionPosition()
    {
        if (_vehicleObject.activeInHierarchy)
            return _vehicleObject.transform.position;

        return transform.position;
    }

    private float GetHorizontalDistance(
        Vector3 firstPosition,
        Vector3 secondPosition)
    {
        firstPosition.y = 0f;
        secondPosition.y = 0f;

        return Vector3.Distance(firstPosition, secondPosition);
    }

    private void SetVehiclePlayerComponentsActive(bool isActive)
    {
        foreach (Component component in _vehiclePlayerComponents)
        {
            if (component == null)
                continue;

            switch (component)
            {
                case Behaviour behaviour:
                    behaviour.enabled = isActive;
                    break;

                case Collider collider:
                    collider.enabled = isActive;
                    break;

                case Renderer renderer:
                    renderer.enabled = isActive;
                    break;
            }
        }
    }

    private Player GetActivePlayer()
    {
        return _entityRegistry.AllEntities
            .OfType<Player>()
            .FirstOrDefault(player =>
                player != null &&
                player.gameObject.activeInHierarchy &&
                player.gameObject != _vehiclePlayerObject);
    }

    private Player GetPlayer()
    {
        return _entityRegistry.AllEntities
            .OfType<Player>()
            .FirstOrDefault(player =>
                player != null &&
                player.gameObject != _vehiclePlayerObject);
    }
}