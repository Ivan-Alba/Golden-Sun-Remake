using UnityEngine;

/// <summary>
/// Controls local rotation and smooth, subtle bobbing animation for the 3D target selection diamond.
/// Synchronized across all targets using global engine time.
/// </summary>
public class BattleSelectionPointer : MonoBehaviour
{
    [Header("Animation Properties")]
    [SerializeField] private float rotationSpeed = 120f;
    [SerializeField] private float bobbingFrequency = 5f;
    [SerializeField] private float bobbingAmplitude = 0.05f;

    private Vector3 _startLocalPos;

    private void Awake()
    {
        _startLocalPos = transform.localPosition;
    }

    private void OnEnable()
    {
        ResetAnimation();
    }

    private void Update()
    {
        // 1. Rotación continua en el eje Y
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);

        // 2. Movimiento armónico sutil sincronizado mediante Time.time
        float newY = _startLocalPos.y + (Mathf.Sin(Time.time * bobbingFrequency) * bobbingAmplitude);
        transform.localPosition = new Vector3(_startLocalPos.x, newY, _startLocalPos.z);
    }

    /// <summary>
    /// Resets the pointer's local transform position and rotation to synchronize with other active pointers.
    /// </summary>
    public void ResetAnimation()
    {
        transform.localPosition = _startLocalPos;
        transform.localRotation = Quaternion.identity;
    }

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
        if (visible)
        {
            ResetAnimation();
        }
    }
}