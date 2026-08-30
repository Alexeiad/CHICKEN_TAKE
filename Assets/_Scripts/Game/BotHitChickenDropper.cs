using UnityEngine;
using Zenject;

public class BotHitChickenDropper : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BotChickenCollector _collector;

    [Header("Hit Settings")]
    [SerializeField] private float _hitForceThreshold = 20f;
    [SerializeField] private float _forcePerChicken = 10f;

    [Header("Spawn Settings")]
    [SerializeField] private float _spawnDistance = 0.5f;
    [SerializeField] private float _spawnRadius = 1.5f;
    [SerializeField] private float _spawnHeight = 0.5f;

    [Header("Chicken Physics")]
    [SerializeField] private float _chickenImpulse = 5f;

    private Chicken.Factory _chickenFactory;

    [Inject]
    private void Construct(Chicken.Factory chickenFactory)
    {
        _chickenFactory = chickenFactory;
    }

    private void Awake()
    {
        if (_collector == null)
            _collector = GetComponent<BotChickenCollector>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        PrometeoCarController vehicle =
            collision.collider.GetComponentInParent<PrometeoCarController>();

        if (vehicle == null)
            return;

        Rigidbody vehicleRigidbody =
            vehicle.GetComponentInParent<Rigidbody>();

        if (vehicleRigidbody == null)
            return;

        float hitForce =
            collision.relativeVelocity.magnitude *
            vehicleRigidbody.mass;

        if (hitForce < _hitForceThreshold)
            return;

        DropChickens(hitForce);
    }

    private void DropChickens(float hitForce)
    {
        if (_collector == null || _collector.Data == null)
            return;

        int availableChickens = _collector.Data.Chicken;

        if (availableChickens <= 0)
            return;

        int chickenCount = Mathf.FloorToInt(
            hitForce / _forcePerChicken
        );

        chickenCount = Mathf.Min(
            chickenCount,
            availableChickens
        );

        if (chickenCount <= 0)
            return;

        SpawnChickens(chickenCount);

        _collector.RemoveChickens(chickenCount);
    }

    private void SpawnChickens(int count)
    {
        if (_chickenFactory == null)
        {
            Debug.LogError(
                "[BotHitChickenDropper] Chicken.Factory == null"
            );

            return;
        }

        for (int i = 0; i < count; i++)
        {
            Vector2 randomCircle =
                Random.insideUnitCircle.normalized;

            if (randomCircle.sqrMagnitude < 0.01f)
                randomCircle = Vector2.right;

            float distance = Random.Range(
                _spawnDistance,
                _spawnDistance + _spawnRadius
            );

            Vector3 direction = new Vector3(
                randomCircle.x,
                0f,
                randomCircle.y
            );

            Vector3 spawnPosition =
                transform.position +
                direction * distance;

            spawnPosition.y +=
                _spawnHeight +
                Random.Range(0f, 0.5f);

            Chicken chicken = _chickenFactory.Create();

            if (chicken == null)
            {
                Debug.LogError(
                    "[BotHitChickenDropper] Chicken.Factory.Create() вернул null."
                );

                continue;
            }

            chicken.Transform.SetPositionAndRotation(
                spawnPosition,
                Random.rotation
            );

            Rigidbody rb =
                chicken.GetComponent<Rigidbody>();

            if (rb == null)
                continue;

            Vector3 impulse =
                direction * _chickenImpulse +
                Vector3.up * Random.Range(1f, 3f) +
                Random.insideUnitSphere * 1.5f;

            rb.AddForce(
                impulse,
                ForceMode.Impulse
            );
        }
    }
}