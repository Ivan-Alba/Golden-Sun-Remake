using UnityEngine;
using UnityEditor;

namespace RCLPG
{
    [CustomEditor(typeof(RCLPG))]
    public class RCLPGEditor : Editor
    {
        private RCLPG _target;
        private LightProbeGroup _lpGroup;

        private void OnEnable()
        {
            _target = (RCLPG)target;
            _lpGroup = _target.GetComponent<LightProbeGroup>();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("RCLPG — Light Probe Generator", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Generate", GUILayout.Height(32)))
            {
                _target.Generate();
                EditorUtility.SetDirty(_lpGroup);
            }
            if (GUILayout.Button("Clear", GUILayout.Height(32)))
            {
                if (_lpGroup != null)
                {
                    _lpGroup.probePositions = new Vector3[0];
                    EditorUtility.SetDirty(_lpGroup);
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Volume", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("volumeSize"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("volumeCenterOffset"));

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Spacing", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("horizontalSpacing"), new GUIContent("Horizontal"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("verticalSpacing"), new GUIContent("Vertical"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("groundClearance"), new GUIContent("Ground Clearance"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("probeClearanceRadius"), new GUIContent("Probe Clearance Radius"));
            EditorGUILayout.Slider(serializedObject.FindProperty("adaptiveFactor"), 0f, 1f, new GUIContent("Adaptive Spread"));

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Raycast", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("raycastLayers"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("ignoreTagsFilter"));
            if (!_target.ignoreTagsFilter)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("groundTags"), true);
            }

            EditorGUILayout.Space();

            int count = _lpGroup != null && _lpGroup.probePositions != null ? _lpGroup.probePositions.Length : 0;
            EditorGUILayout.LabelField($"Probes: {count}", EditorStyles.centeredGreyMiniLabel);

            serializedObject.ApplyModifiedProperties();
        }

        private void OnSceneGUI()
        {
            Matrix4x4 oldMatrix = Handles.matrix;
            Handles.matrix = _target.transform.localToWorldMatrix;

            Vector3 center = _target.volumeCenterOffset;
            Handles.color = new Color(1f, 0.3f, 0.3f, 0.4f);
            Handles.DrawWireCube(center, _target.volumeSize);

            Handles.matrix = oldMatrix;

            if (_lpGroup == null || _lpGroup.probePositions == null) return;

            Handles.color = new Color(0.3f, 0.8f, 1f, 0.7f);
            foreach (var localPos in _lpGroup.probePositions)
            {
                Vector3 worldPos = _target.transform.TransformPoint(localPos);
                Handles.SphereHandleCap(0, worldPos, Quaternion.identity, 0.12f, EventType.Repaint);
            }
        }
    }
}
