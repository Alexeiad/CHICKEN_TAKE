using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class VehicleUnstuck : MonoBehaviour
{
    [Header("Кнопка")]
    public KeyCode unstuckKey = KeyCode.R;
    public float cooldown = 1f;

    [Header("Импульсы")]
    public float liftHeight = 1.0f;      // поднять выше
    public float backwardForce = 6f;      // толчок назад
    public float sidewaysForce = 2f;      // случайный боковой
    public float upwardForce = 3f;        // подброс вверх

    private Rigidbody rb;
    private float lastTime = -999f;

    void Awake() { rb = GetComponent<Rigidbody>(); }

    void Update()
    {
        if (Input.GetKeyDown(unstuckKey) && Time.time - lastTime > cooldown)
        {
            Unstuck();
            lastTime = Time.time;
        }
    }

    public void Unstuck()
    {
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // приподнимаем позицию
        rb.MovePosition(rb.position + Vector3.up * liftHeight);

        // импульс: назад + вверх + чуть вбок (случайно)
        Vector3 dir = -transform.forward * backwardForce
                    + Vector3.up * upwardForce
                    + transform.right * Random.Range(-sidewaysForce, sidewaysForce);

        rb.AddForce(dir, ForceMode.VelocityChange);
        rb.WakeUp();
    }
}