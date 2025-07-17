using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace razz
{
    public class PivotTool : EditorWindow
    {
        private enum Mode { Manual, Selected }
        private Mode currentMode = Mode.Manual;

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

            GUILayout.Space(5);
            currentMode = (Mode)GUILayout.Toolbar((int)currentMode, new string[] { "Manual", "Selected" });
            GUILayout.Space(5);

            if (currentMode == Mode.Manual)
            {
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
                    if (Selection.activeGameObject != null && Selection.activeGameObject.scene.IsValid())
                    {
                        targetObject = Selection.activeGameObject;
                    }
                    else
                    {
                        targetObject = null;
                        Debug.LogWarning("Please select an object in the scene.");
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                if (Selection.gameObjects.Length > 0)
                {
                    EditorGUILayout.HelpBox($"Operating on {GetTargets().Count} selected scene object(s).", MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox("Select one or more objects in the scene to process.", MessageType.Warning);
                }
            }

            List<GameObject> currentTargets = GetTargets();

            GUILayout.Space(10);
            EditorGUILayout.LabelField("Mesh Utilities", EditorStyles.boldLabel);

            GUI.enabled = currentTargets.Count > 0;
            if (GUILayout.Button("Reset Scale"))
            {
                ApplyScaleToMesh();
            }

            List<GameObject> savableTargets = currentTargets.Where(go => {
                MeshFilter mf = go.GetComponent<MeshFilter>();
                return mf != null && mf.sharedMesh != null && !AssetDatabase.Contains(mf.sharedMesh);
            }).ToList();

            string saveButtonText = (savableTargets.Count > 1) ? "Save Meshes..." : "Save Mesh...";

            GUI.enabled = savableTargets.Count > 0;
            if (GUILayout.Button(saveButtonText))
            {
                SaveMeshes();
            }
            GUI.enabled = true;

            if (currentTargets.Count == 0 && currentMode == Mode.Manual)
            {
                EditorGUILayout.HelpBox("Please assign a valid Target Object from the scene.", MessageType.Warning);
                return;
            }

            GUILayout.Space(10);
            EditorGUILayout.LabelField(new GUIContent("", "________________________________________________________________________________________________________________________________________________"), EditorStyles.boldLabel);
            GUILayout.Space(10);

            GUI.enabled = currentTargets.Count > 0;
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
                if (Selection.activeTransform != null)
                {
                    pivotTarget = Selection.activeTransform;
                }
            }
            EditorGUILayout.EndHorizontal();

            GUI.enabled = currentTargets.Count > 0 && pivotTarget != null;
            if (GUILayout.Button("Set Pivot from Transform", GUILayout.Height(40)))
            {
                MovePivotToTarget();
            }
            GUI.enabled = true;
        }

        private List<GameObject> GetTargets()
        {
            if (currentMode == Mode.Manual)
            {
                if (targetObject != null && targetObject.scene.IsValid())
                {
                    return new List<GameObject> { targetObject };
                }
                return new List<GameObject>();
            }
            else
            {
                return Selection.gameObjects.Where(go => go != null && go.scene.IsValid()).ToList();
            }
        }

        private void ApplyScaleToMesh()
        {
            List<GameObject> targets = GetTargets();
            if (targets.Count == 0) return;

            Undo.SetCurrentGroupName("Apply Scale to Meshes");
            int group = Undo.GetCurrentGroup();

            foreach (var go in targets)
            {
                ProcessApplyScaleToMesh(go);
            }
            Undo.CollapseUndoOperations(group);
        }

        private void ProcessApplyScaleToMesh(GameObject go)
        {
            MeshFilter meshFilter = go.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                Debug.LogWarning($"Skipping {go.name}: No MeshFilter or mesh found.", go);
                return;
            }

            Undo.RecordObject(go.transform, "Apply Scale");
            Undo.RecordObject(meshFilter, "Apply Scale");

            Mesh originalMesh = meshFilter.sharedMesh;
            Mesh newMesh = Instantiate(originalMesh);
            newMesh.name = $"{originalMesh.name}_scaled";

            Vector3[] vertices = newMesh.vertices;
            Vector3 scale = go.transform.localScale;

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
            UpdateChildMeshColliders(go, originalMesh, newMesh);

            go.transform.localScale = Vector3.one;
            Debug.Log($"Applied scale to {go.name} and reset transform scale to one.", go);
        }

        private void SaveMeshes()
        {
            string path = EditorUtility.OpenFolderPanel("Save New Meshes In...", "Assets", "");
            if (string.IsNullOrEmpty(path)) return;

            if (!path.StartsWith(Application.dataPath))
            {
                Debug.LogError("The selected folder must be inside the project's Assets folder.");
                return;
            }
            string relativePath = "Assets" + path.Substring(Application.dataPath.Length);

            List<GameObject> targets = GetTargets();
            if (targets.Count == 0) return;

            Undo.SetCurrentGroupName("Save Meshes");
            int group = Undo.GetCurrentGroup();

            foreach (var go in targets)
            {
                ProcessSaveMesh(go, relativePath);
            }
            Undo.CollapseUndoOperations(group);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private void ProcessSaveMesh(GameObject go, string folderPath)
        {
            MeshFilter meshFilter = go.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null || AssetDatabase.Contains(meshFilter.sharedMesh))
            {
                return;
            }

            Mesh meshToSave = meshFilter.sharedMesh;
            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{folderPath}/{go.name}_Mesh.asset");

            AssetDatabase.CreateAsset(meshToSave, assetPath);
            Debug.Log($"Saved mesh for {go.name} to: {assetPath}", go);

            Mesh savedAsset = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (savedAsset != null)
            {
                Undo.RecordObject(meshFilter, "Assign Saved Mesh");
                meshFilter.sharedMesh = savedAsset;
                UpdateChildMeshColliders(go, meshToSave, savedAsset);
            }
        }

        private void MovePivotToTarget()
        {
            List<GameObject> targets = GetTargets();
            if (targets.Count == 0 || pivotTarget == null) return;

            Undo.SetCurrentGroupName("Set Pivot from Transform");
            int group = Undo.GetCurrentGroup();

            foreach (var go in targets)
            {
                if (go == pivotTarget.gameObject)
                {
                    Debug.LogWarning($"Skipping {go.name} because it cannot be its own pivot target.", go);
                    continue;
                }
                ProcessMovePivot(go, pivotTarget.position, pivotTarget.rotation);
            }
            Undo.CollapseUndoOperations(group);
        }

        private void MovePivotWithBBoxOffset()
        {
            List<GameObject> targets = GetTargets();
            if (targets.Count == 0) return;

            Undo.SetCurrentGroupName("Set Pivot from Bounding Box");
            int group = Undo.GetCurrentGroup();

            foreach (var go in targets)
            {
                ProcessMovePivotWithBBoxOffset(go);
            }
            Undo.CollapseUndoOperations(group);
        }

        private void ProcessMovePivotWithBBoxOffset(GameObject go)
        {
            MeshFilter meshFilter = go.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                Debug.LogWarning($"Skipping {go.name}: No MeshFilter or mesh found.", go);
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
            Vector3 newPivotWorldPosition = go.transform.TransformPoint(newPivotLocalPosition);
            ProcessMovePivot(go, newPivotWorldPosition, go.transform.rotation);
        }

        private void ProcessMovePivot(GameObject go, Vector3 newPivotWorldPosition, Quaternion newPivotWorldRotation)
        {
            MeshFilter meshFilter = go.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                Debug.LogWarning($"Skipping {go.name}: No MeshFilter or mesh found for pivot operation.", go);
                return;
            }

            Undo.RecordObject(go.transform, "Move Pivot");
            Undo.RecordObject(meshFilter, "Move Pivot");

            Mesh originalMesh = meshFilter.sharedMesh;
            Mesh newMesh = Instantiate(originalMesh);
            newMesh.name = $"{originalMesh.name}_pivoted";

            Vector3 originalLocalScale = go.transform.localScale;
            Vector3 originalLossyScale = go.transform.lossyScale;

            Matrix4x4 oldPivotMatrix = go.transform.localToWorldMatrix;
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
            UpdateChildMeshColliders(go, originalMesh, newMesh);

            go.transform.position = newPivotWorldPosition;
            go.transform.rotation = newPivotWorldRotation;
            go.transform.localScale = originalLocalScale;

            Debug.Log($"Pivot for {go.name} has been successfully changed.", go);
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
                Debug.Log($"Updated {updatedCount} MeshCollider(s) in {root.name} to use the new mesh.", root);
            }
        }
    }
}
