using FMOD.Studio;
using FMODUnity;
using UnityEngine;
using STOP_MODE = FMOD.Studio.STOP_MODE;

[RequireComponent(typeof(Rigidbody))]
public class TrackAudio : MonoBehaviour
{
    [Header("FMOD Track Event")]
    [SerializeField] private EventReference trackEvent;

    [Header("Track Pitch")]
    [Tooltip("Питч на месте (0 км/ч)")]
    [SerializeField] private float minPitch = 0.8f;

    [Tooltip("Максимальный питч на предельной скорости")]
    [SerializeField] private float maxPitch = 2.0f;

    [Tooltip("Скорость (в м/с), при которой питч станет максимальным")]
    [SerializeField] private float maxSpeed = 15f;

    [Header("Collision Events")]
    [SerializeField] private EventReference collisionEvent;
    [SerializeField] private EventReference heavyCollisionEvent;

    [Header("Collision Detection")]
    [Tooltip("Минимальная скорость столкновения, при которой вообще появляется звук")]
    [SerializeField] private float minCollisionSpeed = 1f;

    [Tooltip("Сила столкновения, после которой считается очень сильным ударом")]
    [SerializeField] private float heavyCollisionSpeed = 8f;

    [Header("Collision Pitch")]
    [Tooltip("Размер объекта, который считается маленьким")]
    [SerializeField] private float minObjectSize = 0.5f;

    [Tooltip("Размер объекта, который считается большим")]
    [SerializeField] private float maxObjectSize = 4f;

    [Tooltip("Pitch маленького объекта")]
    [SerializeField] private float smallObjectPitch = 1.4f;

    [Tooltip("Pitch большого объекта")]
    [SerializeField] private float largeObjectPitch = 0.65f;

    [Header("Collision Volume")]
    [Tooltip("Минимальная громкость столкновения")]
    [Range(0f, 1f)]
    [SerializeField] private float minCollisionVolume = 0.2f;

    [Tooltip("Максимальная громкость столкновения")]
    [Range(0f, 1f)]
    [SerializeField] private float maxCollisionVolume = 1f;

    [Tooltip("Множитель громкости маленьких объектов")]
    [SerializeField] private float smallObjectVolumeMultiplier = 0.7f;

    [Tooltip("Множитель громкости больших объектов")]
    [SerializeField] private float largeObjectVolumeMultiplier = 1.2f;

    [Header("Debug")]
    [SerializeField] private bool debugCollisions;

    private Rigidbody _rb;
    private EventInstance _trackInstance;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        StartTrack();
    }

    private void Update()
    {
        UpdateTrackPitch();
    }

    private void OnCollisionEnter(Collision collision)
    {
        HandleCollision(collision);
    }

    private void OnDestroy()
    {
        StopTrack();
    }

    private void StartTrack()
    {
        if (trackEvent.IsNull)
            return;

        _trackInstance = RuntimeManager.CreateInstance(trackEvent);

        RuntimeManager.AttachInstanceToGameObject(
            _trackInstance,
            transform,
            _rb
        );

        _trackInstance.start();
    }

    private void UpdateTrackPitch()
    {
        if (!_trackInstance.isValid())
            return;

        float currentSpeed = _rb.velocity.magnitude;

        float speedNormalized = Mathf.Clamp01(
            currentSpeed / maxSpeed
        );

        float currentPitch = Mathf.Lerp(
            minPitch,
            maxPitch,
            speedNormalized
        );

        _trackInstance.setPitch(currentPitch);
    }

    private void HandleCollision(Collision collision)
    {
        if (collision.contactCount == 0)
            return;

        /*
         * Используем именно относительную скорость столкновения.
         *
         * Если объект просто трётся о поверхность:
         * OnCollisionStay здесь вообще не вызывается.
         *
         * Поэтому обычное трение не создаёт звука.
         */
        float collisionSpeed = collision.relativeVelocity.magnitude;

        if (collisionSpeed < minCollisionSpeed)
            return;

        /*
         * Размер объекта, с которым столкнулись.
         *
         * Bounds берём от Collider, а не от Transform.localScale,
         * чтобы работало и с физически разными объектами.
         */
        float objectSize = GetCollisionObjectSize(collision);

        /*
         * Нормализованный размер:
         *
         * 0 = маленький объект
         * 1 = большой объект
         */
        float sizeNormalized = Mathf.InverseLerp(
            minObjectSize,
            maxObjectSize,
            objectSize
        );

        /*
         * Маленький объект:
         * pitch высокий.
         *
         * Большой объект:
         * pitch низкий.
         */
        float pitch = Mathf.Lerp(
            smallObjectPitch,
            largeObjectPitch,
            sizeNormalized
        );

        /*
         * Сила удара.
         *
         * 0 = минимальный слышимый удар
         * 1 = очень сильный удар
         */
        float impactNormalized = Mathf.InverseLerp(
            minCollisionSpeed,
            heavyCollisionSpeed,
            collisionSpeed
        );

        /*
         * Громкость зависит от силы удара.
         */
        float volume = Mathf.Lerp(
            minCollisionVolume,
            maxCollisionVolume,
            impactNormalized
        );

        /*
         * И одновременно учитываем размер объекта.
         *
         * Маленькие объекты немного тише.
         * Большие немного громче.
         */
        float sizeVolumeMultiplier = Mathf.Lerp(
            smallObjectVolumeMultiplier,
            largeObjectVolumeMultiplier,
            sizeNormalized
        );

        volume *= sizeVolumeMultiplier;
        volume = Mathf.Clamp01(volume);

        bool isHeavyCollision =
            collisionSpeed >= heavyCollisionSpeed;

        if (debugCollisions)
        {
            Debug.Log(
                $"Collision with {collision.gameObject.name} | " +
                $"Speed: {collisionSpeed:F2} | " +
                $"Size: {objectSize:F2} | " +
                $"Pitch: {pitch:F2} | " +
                $"Volume: {volume:F2} | " +
                $"Heavy: {isHeavyCollision}"
            );
        }

        if (isHeavyCollision)
        {
            PlayCollisionSound(
                heavyCollisionEvent,
                pitch,
                volume
            );
        }
        else
        {
            PlayCollisionSound(
                collisionEvent,
                pitch,
                volume
            );
        }
    }

    private float GetCollisionObjectSize(Collision collision)
    {
        Collider collider = collision.collider;

        if (collider == null)
            return 1f;

        Bounds bounds = collider.bounds;

        /*
         * Берём максимальную сторону bounds.
         * Например:
         *
         * маленький куб 0.5 -> размер 0.5
         * большая машина 4 -> размер 4
         */
        return Mathf.Max(
            bounds.size.x,
            bounds.size.y,
            bounds.size.z
        );
    }

    private void PlayCollisionSound(
        EventReference eventReference,
        float pitch,
        float volume)
    {
        if (eventReference.IsNull)
            return;

        EventInstance instance = RuntimeManager.CreateInstance(
            eventReference
        );

        /*
         * Ставим параметры ДО start(),
         * чтобы FMOD сразу получил правильные значения.
         */
        instance.setPitch(pitch);
        instance.setVolume(volume);

        RuntimeManager.AttachInstanceToGameObject(
            instance,
            transform,
            _rb
        );

        instance.start();

        /*
         * One-shot event больше не нужен.
         * Release после start позволяет FMOD самому
         * уничтожить instance после окончания звука.
         */
        instance.release();
    }

    private void StopTrack()
    {
        if (!_trackInstance.isValid())
            return;

        RuntimeManager.DetachInstanceFromGameObject(
            _trackInstance
        );

        _trackInstance.stop(
            STOP_MODE.IMMEDIATE
        );

        _trackInstance.release();
    }
}