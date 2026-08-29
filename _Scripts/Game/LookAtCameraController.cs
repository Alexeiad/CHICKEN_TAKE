using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Zenject;

public class LookAtCameraController : MonoBehaviour
{
    [Header("Screen Size Settings")]
    [Tooltip("Базовый размер шрифта на референсной дистанции")]
    [SerializeField] private float _baseFontSize = 36f;

    [Tooltip("Дистанция, на которой размер шрифта равен базовому")]
    [SerializeField] private float _referenceDistance = 10f;

    [Header("Image Settings")]
    [SerializeField] private Image _image;

    [Tooltip("Базовый размер картинки на референсной дистанции")]
    [SerializeField] private Vector2 _baseImageSize = new Vector2(100f, 100f);

    [Header("Fade Settings")]
    [Tooltip("Дистанция, ближе которой текст начинает затухать")]
    [SerializeField] private float _fadeDistance = 10f;

    [Header("Visibility Settings")]
    [Tooltip("Если включено, текст будет виден сквозь препятствия")]
    [SerializeField] private bool _alwaysVisible = false;

    private CameraController _cameraController;
    private Transform _cameraTransform;
    private TMP_Text _text;

    [Inject]
    private void Construct(CameraController cameraController)
    {
        _cameraController = cameraController;
    }

    private void Awake()
    {
        _text = GetComponent<TMP_Text>();

        if (_text == null)
            _text = GetComponentInChildren<TMP_Text>();

        if (_alwaysVisible)
            ApplyOverlayShader();
    }

    private void Start()
    {
        CacheCamera();
    }

    private void LateUpdate()
    {
        if (_cameraTransform == null)
        {
            CacheCamera();

            if (_cameraTransform == null)
                return;
        }

        // Только родительский объект поворачиваем к камере.
        // Image отдельно не вращаем.
        transform.rotation = _cameraTransform.rotation;

        float distance = Vector3.Distance(
            transform.position,
            _cameraTransform.position);

        float scale = distance / _referenceDistance;

        // TEXT
        if (_text != null)
        {
            _text.fontSize = _baseFontSize * scale;

            float alpha = Mathf.Clamp01(distance / _fadeDistance);

            Color color = _text.color;
            color.a = alpha;
            _text.color = color;
        }

        // IMAGE
        if (_image != null)
        {
            _image.rectTransform.sizeDelta = _baseImageSize * scale;

            float alpha = Mathf.Clamp01(distance / _fadeDistance);

            Color color = _image.color;
            color.a = alpha;
            _image.color = color;
        }
    }

    private void CacheCamera()
    {
        if (_cameraController != null)
        {
            _cameraTransform = _cameraController.transform;
        }
        else
        {
            if (Camera.main != null)
                _cameraTransform = Camera.main.transform;
            else
                _cameraTransform = null;
        }
    }

    private void ApplyOverlayShader()
    {
        if (_text == null)
            return;

        Shader overlayShader =
            Shader.Find("TextMeshPro/Distance Field Overlay");

        if (overlayShader == null)
            return;

        Material overlayMaterial = new Material(overlayShader);

        overlayMaterial.SetFloat(
            "_ZTest",
            (float)UnityEngine.Rendering.CompareFunction.Always);

        overlayMaterial.renderQueue = 4000;

        _text.fontMaterial = overlayMaterial;
        _text.UpdateMeshPadding();
    }
}