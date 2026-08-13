using UnityEngine;

public class RotateSkybox : MonoBehaviour
{
    [SerializeField] private float rotationSpeed = 1.5f;

    private void Update()
    {
        if (RenderSettings.skybox != null)
        {
            // Rotate the skybox material continuously around the horizon
            RenderSettings.skybox.SetFloat("_Rotation", Time.time * rotationSpeed);
        }
    }
}