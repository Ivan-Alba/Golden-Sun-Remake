#if UNITY_EDITOR
using UnityEngine;
using System.Collections.Generic;

namespace RCLPG
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(LightProbeGroup))]
    public class RCLPG : MonoBehaviour
    {
        [Header("Volume")]
        public Vector3 volumeSize = Vector3.one * 100f;
        public Vector3 volumeCenterOffset = Vector3.zero;

        [Header("Spacing")]
        public float horizontalSpacing = 5f;
        public float verticalSpacing = 2f;
        public float groundClearance = 0.5f;
        [Range(0f, 1f)]
        public float adaptiveFactor = 0f;

        [Header("Raycast")]
        public LayerMask raycastLayers = ~0;
        public bool ignoreTagsFilter = false;
        public string[] groundTags = new string[] { "Ground" };
        public float probeClearanceRadius = 0.3f;

        const float MIN_SPACING = 0.5f;
        const float RAYCAST_EXTRA = 10f;
        const int MAX_HITS_PER_RAY = 64;

        RaycastHit[] _raycastBuffer = new RaycastHit[MAX_HITS_PER_RAY];

        void OnValidate()
        {
            horizontalSpacing = Mathf.Max(MIN_SPACING, horizontalSpacing);
            verticalSpacing = Mathf.Max(MIN_SPACING, verticalSpacing);
            groundClearance = Mathf.Max(0f, groundClearance);
            probeClearanceRadius = Mathf.Max(0f, probeClearanceRadius);
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null) Generate();
            };
        }

        // ReSharper disable Unity.PerformanceAnalysis
        public void Generate()
        {
            var lpGroup = GetComponent<LightProbeGroup>();
            if (lpGroup == null) return;

            Vector3 halfSize = volumeSize * 0.5f;

            float minX = -halfSize.x;
            float minZ = -halfSize.z;
            float bottomY = -halfSize.y;
            float topY = halfSize.y;

            float rayLength = volumeSize.y + RAYCAST_EXTRA * 2f;
            Vector3 rayDirWorld = transform.TransformDirection(Vector3.down);

            int surfGridX = Mathf.Max(1, Mathf.RoundToInt(volumeSize.x / horizontalSpacing));
            int surfGridZ = Mathf.Max(1, Mathf.RoundToInt(volumeSize.z / horizontalSpacing));
            float surfCellX = volumeSize.x / surfGridX;
            float surfCellZ = volumeSize.z / surfGridZ;

            int totalCols = (surfGridX + 1) * (surfGridZ + 1);
            float[] surfaceMap = new float[totalCols];
            bool[] surfaceFound = new bool[totalCols];

            for (int ix = 0; ix <= surfGridX; ix++)
            {
                for (int iz = 0; iz <= surfGridZ; iz++)
                {
                    int idx = ix * (surfGridZ + 1) + iz;
                    float localX = minX + surfCellX * ix;
                    float localZ = minZ + surfCellZ * iz;

                    Vector3 rayOriginWorld = transform.TransformPoint(volumeCenterOffset + new Vector3(localX, topY + RAYCAST_EXTRA, localZ));

                    float surfaceY = bottomY;
                    bool found = false;

                    int hitCount = Physics.RaycastNonAlloc(rayOriginWorld, rayDirWorld, _raycastBuffer, rayLength, raycastLayers);

                    if (hitCount > 0)
                    {
                        if (ignoreTagsFilter)
                        {
                            float minY = float.MaxValue;
                            for (int h = 0; h < hitCount; h++)
                            {
                                Vector3 hl = transform.InverseTransformPoint(_raycastBuffer[h].point) - volumeCenterOffset;
                                if (hl.y < minY) minY = hl.y;
                            }
                            if (minY < float.MaxValue)
                            {
                                surfaceY = minY;
                                found = true;
                            }
                        }
                        else
                        {
                            float groundY = float.MaxValue;
                            bool groundHit = false;
                            for (int h = 0; h < hitCount; h++)
                            {
                                if (System.Array.Exists(groundTags, t => _raycastBuffer[h].transform.CompareTag(t)))
                                {
                                    Vector3 hl = transform.InverseTransformPoint(_raycastBuffer[h].point) - volumeCenterOffset;
                                    if (hl.y < groundY)
                                    {
                                        groundY = hl.y;
                                        groundHit = true;
                                    }
                                }
                            }
                            if (groundHit)
                            {
                                surfaceY = groundY;
                                found = true;
                            }
                        }
                    }

                    surfaceMap[idx] = surfaceY;
                    surfaceFound[idx] = found;
                }
            }

            List<Vector3> positions = new List<Vector3>();

            float y = bottomY + groundClearance;

            while (y <= topY)
            {
                float currentHSpacing = horizontalSpacing;
                float currentVSpacing = verticalSpacing;

                if (adaptiveFactor > 0f)
                {
                    float volumeHeight = volumeSize.y;
                    float t = Mathf.Clamp01((y - bottomY) / volumeHeight);
                    float spread = adaptiveFactor * 10f * t;

                    currentHSpacing = Mathf.Max(horizontalSpacing * (1f + spread), MIN_SPACING);
                    currentVSpacing = Mathf.Max(verticalSpacing * (1f + spread), MIN_SPACING);
                }

                int levelGridX = Mathf.Max(1, Mathf.RoundToInt(volumeSize.x / currentHSpacing));
                int levelGridZ = Mathf.Max(1, Mathf.RoundToInt(volumeSize.z / currentHSpacing));
                float levelCellX = volumeSize.x / levelGridX;
                float levelCellZ = volumeSize.z / levelGridZ;

                for (int ix = 0; ix <= levelGridX; ix++)
                {
                    for (int iz = 0; iz <= levelGridZ; iz++)
                    {
                        float localX = minX + levelCellX * ix;
                        float localZ = minZ + levelCellZ * iz;

                        int mapIX = Mathf.RoundToInt((localX - minX) / surfCellX);
                        int mapIZ = Mathf.RoundToInt((localZ - minZ) / surfCellZ);
                        mapIX = Mathf.Clamp(mapIX, 0, surfGridX);
                        mapIZ = Mathf.Clamp(mapIZ, 0, surfGridZ);

                        int mapIdx = mapIX * (surfGridZ + 1) + mapIZ;
                        float surfaceY = surfaceMap[mapIdx];
                        bool hasSurface = surfaceFound[mapIdx];

                        if (y < (hasSurface ? surfaceY + groundClearance : bottomY))
                            continue;

                        Vector3 localPos = new Vector3(localX, y, localZ) + volumeCenterOffset;
                        Vector3 worldPos = transform.TransformPoint(localPos);

                        if (!Physics.CheckSphere(worldPos, probeClearanceRadius, raycastLayers))
                        {
                            positions.Add(localPos);
                        }
                    }
                }

                y += Mathf.Max(currentVSpacing, MIN_SPACING);
            }

            lpGroup.probePositions = positions.ToArray();
        }

        void OnDrawGizmosSelected()
        {
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;

            Vector3 center = volumeCenterOffset;
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.5f);
            Gizmos.DrawWireCube(center, volumeSize);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(center, 0.3f);

            Gizmos.matrix = oldMatrix;
        }
    }
}
#endif
