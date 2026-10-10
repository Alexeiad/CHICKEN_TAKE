using UnityEngine;

// Повесь на ящик. Плавно включает подсветку, когда игрок рядом,
// и выключает, когда уходит. Работает через MaterialPropertyBlock (без копий материала).
[RequireComponent(typeof(Renderer))]
public class TutorialCrateHighlight : MonoBehaviour
{
    public Transform player;
    public float triggerDistance = 6f;
    public float fadeSpeed = 3f;
    public bool alwaysOn = false;

    static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    Renderer rend;
    MaterialPropertyBlock block;
    float current;

    void Awake()
    {
        rend = GetComponent<Renderer>();
        block = new MaterialPropertyBlock();
    }

    void Update()
    {
        float target = alwaysOn || (player != null &&
            Vector3.Distance(player.position, transform.position) < triggerDistance) ? 1f : 0f;

        current = Mathf.MoveTowards(current, target, fadeSpeed * Time.deltaTime);

        rend.GetPropertyBlock(block);
        block.SetFloat(IntensityId, current);
        rend.SetPropertyBlock(block);
    }
}
