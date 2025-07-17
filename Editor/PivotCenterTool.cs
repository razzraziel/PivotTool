using UnityEngine;
using UnityEditor;

namespace razz
{
    public class PivotTool : EditorWindow
    {
        private GameObject targetObject = null;
        private Transform pivotTarget = null;

        private float xOffset = 0f;
        private float yOffset = 0f;
        private float zOffset = 0f;

        [MenuItem("Utilities/Pivot Tool")]
        static void Init()
        {
            PivotTool window = GetWindow<PivotTool>("Pivot Tool");
            Vector2 fixedSize = new Vector2(380, 580);
            window.minSize = fixedSize;
            window.maxSize = fixedSize;
        }

        void OnGUI()
        {
            EditorGUILayout.HelpBox("This tool changes an object's pivot point without altering its position, rotation, or scale in the scene. Use bounding box or a new pivot transform. Pivot transform will also change pivot rotation.", MessageType.Info);

            GUILayout.Space(10);

            EditorGUILayout.LabelField("Target Object");
            EditorGUILayout.BeginHorizontal();
            targetObject = (GameObject)EditorGUILayout.ObjectField(targetObject, typeof(GameObject), true);

            GUI.enabled = targetObject != null;
            if (GUILayout.Button("Select", GUILayout.Width(60)))
            {
                Selection.activeGameObject = targetObject;
            }
            GUI.enabled = true;

            if (GUILayout.Button("Set Selected", GUILayout.Width(90)))
            {
                if (Selection.activeGameObject != null)
                {
                    targetObject = Selection.activeGameObject;
                }
                else
                {
                    Debug.LogWarning("No object selected in the scene.");
                }
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(10);
            EditorGUILayout.LabelField("Mesh Utilities", EditorStyles.boldLabel);

            GUI.enabled = targetObject != null;
            if (GUILayout.Button("Reset Scale"))
            {
                ApplyScaleToMesh();
            }

            MeshFilter mf = targetObject ? targetObject.GetComponent<MeshFilter>() : null;
            GUI.enabled = mf != null && mf.sharedMesh != null && !AssetDatabase.Contains(mf.sharedMesh);

            if (GUILayout.Button("Save Mesh as New"))
            {
                SaveMesh();
            }
            GUI.enabled = true;

            if (targetObject == null)
            {
                EditorGUILayout.HelpBox("Please assign a Target Object.", MessageType.Warning);
                return;
            }

            GUILayout.Space(10);
            EditorGUILayout.LabelField(new GUIContent("", "________________________________________________________________________________________________________________________________________________"), EditorStyles.boldLabel);
            GUILayout.Space(10);

            EditorGUILayout.LabelField("Change Pivot:", EditorStyles.boldLabel);

            EditorGUILayout.LabelField("1. Use Bounding Box", EditorStyles.miniBoldLabel);
            xOffset = EditorGUILayout.Slider("X Offset (%)", xOffset, -100f, 100f);
            yOffset = EditorGUILayout.Slider("Y Offset (%)", yOffset, -100f, 100f);
            zOffset = EditorGUILayout.Slider("Z Offset (%)", zOffset, -100f, 100f);
            if (GUILayout.Button("Set Pivot from Bounding Box", GUILayout.Height(40)))
            {
                MovePivotWithBBoxOffset();
            }

            GUILayout.Space(10);

            EditorGUILayout.LabelField("2. Use a Transform", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField("New Pivot Transform");
            EditorGUILayout.BeginHorizontal();
            pivotTarget = (Transform)EditorGUILayout.ObjectField(pivotTarget, typeof(Transform), true);

            GUI.enabled = pivotTarget != null;
            if (GUILayout.Button("Select", GUILayout.Width(60)))
            {
                Selection.activeGameObject = pivotTarget.gameObject;
            }
            GUI.enabled = true;

            if (GUILayout.Button("Set Selected", GUILayout.Width(90)))
            {
                if (Selection.activeTransform != null && Selection.activeTransform.gameObject != targetObject)
                {
                    pivotTarget = Selection.activeTransform;
                }
                else
                {
                    Debug.LogWarning("Select a different object to be the pivot target.");
                }
            }
            EditorGUILayout.EndHorizontal();

            GUI.enabled = pivotTarget != null;
            if (GUILayout.Button("Set Pivot from Transform", GUILayout.Height(40)))
            {
                MovePivotToTarget();
            }
            GUI.enabled = true;
        }

        private void ApplyScaleToMesh()
        {
            if (targetObject == null) return;
            MeshFilter meshFilter = targetObject.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                Debug.LogWarning($"No mesh found on {targetObject.name}");
                return;
            }

            Undo.RecordObject(targetObject.transform, "Apply Scale");
            Undo.RecordObject(meshFilter, "Apply Scale");

            Mesh originalMesh = meshFilter.sharedMesh;
            Mesh newMesh = Instantiate(originalMesh);
            newMesh.name = $"{originalMesh.name}_scaled";

            Vector3[] vertices = newMesh.vertices;
            Vector3 scale = targetObject.transform.localScale;

            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i].x *= scale.x;
                vertices[i].y *= scale.y;
                vertices[i].z *= scale.z;
            }
            newMesh.vertices = vertices;
            newMesh.RecalculateNormals();
            newMesh.RecalculateTangents();
            newMesh.RecalculateBounds();

            meshFilter.mesh = newMesh;
            UpdateChildMeshColliders(targetObject, originalMesh, newMesh);

            targetObject.transform.localScale = Vector3.one;
            Debug.Log($"Applied scale to {targetObject.name} and reset transform scale to one.", targetObject);
        }

        private void SaveMesh()
        {
            MeshFilter meshFilter = targetObject.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null) return;

            string path = EditorUtility.SaveFilePanelInProject("Save New Mesh", $"{targetObject.name}_Mesh.asset", "asset", "Enter a file name for the new mesh.");
            if (string.IsNullOrEmpty(path)) return;

            AssetDatabase.CreateAsset(meshFilter.sharedMesh, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Mesh saved to: {path}", AssetDatabase.LoadAssetAtPath<Mesh>(path));
        }

        private void MovePivotToTarget()
        {
            if (targetObject == null || pivotTarget == null) return;
            MovePivot(pivotTarget.position, pivotTarget.rotation);
        }

        private void MovePivotWithBBoxOffset()
        {
            if (targetObject == null) return;
            MeshFilter meshFilter = targetObject.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                Debug.LogWarning($"No mesh found on {targetObject.name}. Cannot use BBox method.");
                return;
            }

            Bounds bounds = meshFilter.sharedMesh.bounds;
            Vector3 size = bounds.size;

            Vector3 localOffset = new Vector3(
                size.x * (xOffset / 100f),
                size.y * (yOffset / 100f),
                size.z * (zOffset / 100f)
            );

            Vector3 newPivotLocalPosition = bounds.center + localOffset;
            Vector3 newPivotWorldPosition = targetObject.transform.TransformPoint(newPivotLocalPosition);

            MovePivot(newPivotWorldPosition, targetObject.transform.rotation);
        }

        private void MovePivot(Vector3 newPivotWorldPosition, Quaternion newPivotWorldRotation)
        {
            MeshFilter meshFilter = targetObject.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                Debug.LogWarning($"No mesh found on {targetObject.name}");
                return;
            }

            Undo.RecordObject(targetObject.transform, "Move Pivot");
            Undo.RecordObject(meshFilter, "Move Pivot");

            Mesh originalMesh = meshFilter.sharedMesh;
            Mesh newMesh = Instantiate(originalMesh);
            newMesh.name = $"{originalMesh.name}_pivoted";

            Vector3 originalLocalScale = targetObject.transform.localScale;
            Vector3 originalLossyScale = targetObject.transform.lossyScale;

            Matrix4x4 oldPivotMatrix = targetObject.transform.localToWorldMatrix;
            Matrix4x4 newPivotMatrix = Matrix4x4.TRS(newPivotWorldPosition, newPivotWorldRotation, originalLossyScale);
            Matrix4x4 meshTransformMatrix = newPivotMatrix.inverse * oldPivotMatrix;

            Vector3[] vertices = newMesh.vertices;
            Vector3[] normals = newMesh.normals;

            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = meshTransformMatrix.MultiplyPoint3x4(vertices[i]);
            }

            for (int i = 0; i < normals.Length; i++)
            {
                normals[i] = meshTransformMatrix.inverse.transpose.MultiplyVector(normals[i]).normalized;
            }

            newMesh.vertices = vertices;
            newMesh.normals = normals;
            newMesh.RecalculateTangents();
            newMesh.RecalculateBounds();

            meshFilter.mesh = newMesh;
            UpdateChildMeshColliders(targetObject, originalMesh, newMesh);

            targetObject.transform.position = newPivotWorldPosition;
            targetObject.transform.rotation = newPivotWorldRotation;
            targetObject.transform.localScale = originalLocalScale;

            Debug.Log($"Pivot for {targetObject.name} has been successfully changed.", targetObject);
        }

        private void UpdateChildMeshColliders(GameObject root, Mesh originalMesh, Mesh newMesh)
        {
            if (originalMesh == null || newMesh == null) return;

            MeshCollider[] colliders = root.GetComponentsInChildren<MeshCollider>(true);
            int updatedCount = 0;
            foreach (MeshCollider collider in colliders)
            {
                if (collider.sharedMesh == originalMesh)
                {
                    Undo.RecordObject(collider, "Update Mesh Collider");
                    collider.sharedMesh = newMesh;
                    updatedCount++;
                }
            }

            if (updatedCount > 0)
            {
                Debug.Log($"Updated {updatedCount} MeshCollider(s) to use the new mesh.", root);
            }
        }
    }
}
