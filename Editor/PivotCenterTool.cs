using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace razz
{
    public class PivotTool : EditorWindow
    {
        private enum Mode { Single, Selected }
        private enum PivotMethod { BoundingBox, Transform }

        private Mode currentMode = Mode.Single;
        private PivotMethod pivotMethod = PivotMethod.BoundingBox;

        private GameObject targetObject = null;
        private Transform pivotTarget = null;

        private float xOffset = 0f;
        private float yOffset = 0f;
        private float zOffset = 0f;

        [MenuItem("Utilities/Pivot Tool")]
        static void Init()
        {
            PivotTool window = GetWindow<PivotTool>("Pivot Tool");
            Vector2 fixedSize = new Vector2(380, 520);
            window.minSize = fixedSize;
            window.maxSize = fixedSize;
        }

        void OnGUI()
        {
            EditorGUILayout.HelpBox("This tool lets you change an object’s pivot point and transform properties without modifying the actual position, rotation, or scale of its mesh in the scene. You can use the object’s center or specify another transform to adjust the pivot point.", MessageType.Info);

            GUILayout.Space(5);
            currentMode = (Mode)GUILayout.Toolbar((int)currentMode, new string[] { "Single", "Selected" });
            GUILayout.Space(5);

            if (currentMode == Mode.Single)
            {
                EditorGUILayout.LabelField("Target Object");
                EditorGUILayout.BeginHorizontal();
                targetObject = (GameObject)EditorGUILayout.ObjectField(targetObject, typeof(GameObject), true);
                GUI.enabled = targetObject != null;
                if (GUILayout.Button("Select", GUILayout.Width(60))) Selection.activeGameObject = targetObject;
                GUI.enabled = true;
                if (GUILayout.Button("Set Selected", GUILayout.Width(90)))
                {
                    if (Selection.activeGameObject != null && Selection.activeGameObject.scene.IsValid()) targetObject = Selection.activeGameObject;
                    else { targetObject = null; Debug.LogWarning("Please select an object in the scene."); }
                }
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.HelpBox(Selection.gameObjects.Length > 0 ? $"Operating on {GetTargets().Count} selected scene object(s)." : "Select one or more objects in the scene to process.", Selection.gameObjects.Length > 0 ? MessageType.Info : MessageType.Warning);
            }

            List<GameObject> currentTargets = GetTargets();
            bool hasTargets = currentTargets.Count > 0;

            GUILayout.Space(10);
            EditorGUILayout.LabelField(new GUIContent("", "________________________________________________________________________________________________________________________________________________"), EditorStyles.boldLabel);
            GUILayout.Space(10);

            GUI.enabled = hasTargets;

            pivotMethod = (PivotMethod)GUILayout.Toolbar((int)pivotMethod, new string[] { "Bounding Box", "Transform" });
            GUILayout.Space(10);

            if (pivotMethod == PivotMethod.BoundingBox)
            {
                xOffset = EditorGUILayout.Slider("X Offset (%)", xOffset, -100f, 100f);
                yOffset = EditorGUILayout.Slider("Y Offset (%)", yOffset, -100f, 100f);
                zOffset = EditorGUILayout.Slider("Z Offset (%)", zOffset, -100f, 100f);
            }
            else // Transform Method
            {
                EditorGUILayout.LabelField("New Pivot Transform");
                EditorGUILayout.BeginHorizontal();
                pivotTarget = (Transform)EditorGUILayout.ObjectField(pivotTarget, typeof(Transform), true);
                GUI.enabled = pivotTarget != null;
                if (GUILayout.Button("Select", GUILayout.Width(60))) Selection.activeGameObject = pivotTarget.gameObject;
                GUI.enabled = hasTargets;
                if (GUILayout.Button("Set Selected", GUILayout.Width(90)))
                {
                    if (Selection.activeTransform != null) pivotTarget = Selection.activeTransform;
                }
                EditorGUILayout.EndHorizontal();
            }

            GUI.enabled = hasTargets && (pivotMethod == PivotMethod.BoundingBox || pivotTarget != null);
            if (GUILayout.Button("Set Pivot", GUILayout.Height(40)))
            {
                if (pivotMethod == PivotMethod.BoundingBox) MovePivotWithBBoxOffset();
                else MovePivotToTarget();
            }
            GUI.enabled = true;

            GUILayout.Space(15);

            EditorGUILayout.BeginHorizontal();
            GUI.enabled = hasTargets;
            if (GUILayout.Button("Reset Scale", GUILayout.Height(40))) ApplyScaleToMesh();
            if (GUILayout.Button("Reset Rotation", GUILayout.Height(40))) ResetRotation();
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            List<GameObject> savableTargets = currentTargets.Where(go => {
                MeshFilter mf = go.GetComponent<MeshFilter>();
                return mf != null && mf.sharedMesh != null && !AssetDatabase.Contains(mf.sharedMesh);
            }).ToList();
            string saveButtonText = (savableTargets.Count > 1) ? "Save Meshes..." : "Save Mesh...";
            GUI.enabled = savableTargets.Count > 0;
            if (GUILayout.Button(saveButtonText)) SaveMeshes();
            GUI.enabled = true;
            GUILayout.Space(5);
        }

        private List<GameObject> GetTargets()
        {
            if (currentMode == Mode.Single)
            {
                return (targetObject != null && targetObject.scene.IsValid()) ? new List<GameObject> { targetObject } : new List<GameObject>();
            }
            return Selection.gameObjects.Where(go => go != null && go.scene.IsValid()).ToList();
        }

        private void ApplyScaleToMesh()
        {
            List<GameObject> targets = GetTargets();
            if (targets.Count == 0) return;
            Undo.SetCurrentGroupName("Apply Scale to Meshes");
            int group = Undo.GetCurrentGroup();
            foreach (var go in targets) ProcessApplyScaleToMesh(go);
            Undo.CollapseUndoOperations(group);
        }

        private void ProcessApplyScaleToMesh(GameObject go)
        {
            MeshFilter mf = go.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) { Debug.LogWarning($"Skipping {go.name}: No MeshFilter/mesh.", go); return; }
            Undo.RecordObject(go.transform, "Apply Scale");
            Undo.RecordObject(mf, "Apply Scale");
            Mesh originalMesh = mf.sharedMesh;
            Mesh newMesh = Instantiate(originalMesh);
            newMesh.name = $"{originalMesh.name}_scaled";
            Vector3[] vertices = newMesh.vertices;
            Vector3 scale = go.transform.localScale;
            for (int i = 0; i < vertices.Length; i++) { vertices[i].x *= scale.x; vertices[i].y *= scale.y; vertices[i].z *= scale.z; }
            newMesh.vertices = vertices;
            newMesh.RecalculateNormals(); newMesh.RecalculateTangents(); newMesh.RecalculateBounds();
            mf.mesh = newMesh;
            UpdateChildMeshColliders(go, originalMesh, newMesh);
            go.transform.localScale = Vector3.one;
            Debug.Log($"Applied scale to {go.name}.", go);
        }

        private void ResetRotation()
        {
            List<GameObject> targets = GetTargets();
            if (targets.Count == 0) return;
            Undo.SetCurrentGroupName("Reset Rotations");
            int group = Undo.GetCurrentGroup();
            foreach (var go in targets) ProcessResetRotation(go);
            Undo.CollapseUndoOperations(group);
        }

        private void ProcessResetRotation(GameObject go)
        {
            MeshFilter mf = go.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) { Debug.LogWarning($"Skipping {go.name}: No MeshFilter/mesh.", go); return; }
            Undo.RecordObject(go.transform, "Reset Rotation");
            Undo.RecordObject(mf, "Reset Rotation");

            Mesh originalMesh = mf.sharedMesh;
            Mesh newMesh = Instantiate(originalMesh);
            newMesh.name = $"{originalMesh.name}_rotated";

            Matrix4x4 oldMatrix = go.transform.localToWorldMatrix;
            Matrix4x4 newMatrix = Matrix4x4.TRS(go.transform.position, Quaternion.identity, go.transform.lossyScale);
            Matrix4x4 meshMatrix = newMatrix.inverse * oldMatrix;

            Vector3[] vertices = newMesh.vertices;
            Vector3[] normals = newMesh.normals;
            for (int i = 0; i < vertices.Length; i++) vertices[i] = meshMatrix.MultiplyPoint3x4(vertices[i]);
            for (int i = 0; i < normals.Length; i++) normals[i] = meshMatrix.inverse.transpose.MultiplyVector(normals[i]).normalized;

            newMesh.vertices = vertices;
            newMesh.normals = normals;
            newMesh.RecalculateTangents(); newMesh.RecalculateBounds();

            mf.mesh = newMesh;
            UpdateChildMeshColliders(go, originalMesh, newMesh);

            go.transform.rotation = Quaternion.identity;
            Debug.Log($"Reset rotation for {go.name}.", go);
        }

        private void SaveMeshes()
        {
            string path = EditorUtility.OpenFolderPanel("Save New Meshes In...", "Assets", "");
            if (string.IsNullOrEmpty(path) || !path.StartsWith(Application.dataPath)) return;
            string relativePath = "Assets" + path.Substring(Application.dataPath.Length);
            List<GameObject> targets = GetTargets();
            if (targets.Count == 0) return;
            Undo.SetCurrentGroupName("Save Meshes");
            int group = Undo.GetCurrentGroup();
            foreach (var go in targets) ProcessSaveMesh(go, relativePath);
            Undo.CollapseUndoOperations(group);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        }

        private void ProcessSaveMesh(GameObject go, string folderPath)
        {
            MeshFilter mf = go.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null || AssetDatabase.Contains(mf.sharedMesh)) return;
            Mesh meshToSave = mf.sharedMesh;
            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{folderPath}/{go.name}_Mesh.asset");
            AssetDatabase.CreateAsset(meshToSave, assetPath);
            Debug.Log($"Saved mesh for {go.name} to: {assetPath}", go);
            Mesh savedAsset = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (savedAsset != null)
            {
                Undo.RecordObject(mf, "Assign Saved Mesh");
                mf.sharedMesh = savedAsset;
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
                if (go == pivotTarget.gameObject) { Debug.LogWarning($"Skipping {go.name}: cannot be its own pivot target.", go); continue; }
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
            foreach (var go in targets) ProcessMovePivotWithBBoxOffset(go);
            Undo.CollapseUndoOperations(group);
        }

        private void ProcessMovePivotWithBBoxOffset(GameObject go)
        {
            MeshFilter mf = go.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) { Debug.LogWarning($"Skipping {go.name}: No MeshFilter/mesh.", go); return; }
            Bounds bounds = mf.sharedMesh.bounds;
            Vector3 newPivotWorldPosition = go.transform.TransformPoint(bounds.center + new Vector3(bounds.size.x * (xOffset / 100f), bounds.size.y * (yOffset / 100f), bounds.size.z * (zOffset / 100f)));
            ProcessMovePivot(go, newPivotWorldPosition, go.transform.rotation);
        }

        private void ProcessMovePivot(GameObject go, Vector3 newPivotWorldPosition, Quaternion newPivotWorldRotation)
        {
            MeshFilter mf = go.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) { Debug.LogWarning($"Skipping {go.name}: No MeshFilter/mesh.", go); return; }
            Undo.RecordObject(go.transform, "Move Pivot");
            Undo.RecordObject(mf, "Move Pivot");
            Mesh originalMesh = mf.sharedMesh;
            Mesh newMesh = Instantiate(originalMesh);
            newMesh.name = $"{originalMesh.name}_pivoted";
            Matrix4x4 meshTransformMatrix = Matrix4x4.TRS(newPivotWorldPosition, newPivotWorldRotation, go.transform.lossyScale).inverse * go.transform.localToWorldMatrix;
            Vector3[] vertices = newMesh.vertices;
            Vector3[] normals = newMesh.normals;
            for (int i = 0; i < vertices.Length; i++) vertices[i] = meshTransformMatrix.MultiplyPoint3x4(vertices[i]);
            for (int i = 0; i < normals.Length; i++) normals[i] = meshTransformMatrix.inverse.transpose.MultiplyVector(normals[i]).normalized;
            newMesh.vertices = vertices;
            newMesh.normals = normals;
            newMesh.RecalculateTangents(); newMesh.RecalculateBounds();
            mf.mesh = newMesh;
            UpdateChildMeshColliders(go, originalMesh, newMesh);
            go.transform.SetPositionAndRotation(newPivotWorldPosition, newPivotWorldRotation);
            Debug.Log($"Pivot for {go.name} has been changed.", go);
        }

        private void UpdateChildMeshColliders(GameObject root, Mesh originalMesh, Mesh newMesh)
        {
            if (originalMesh == null || newMesh == null) return;
            MeshCollider[] colliders = root.GetComponentsInChildren<MeshCollider>(true);
            foreach (MeshCollider collider in colliders)
            {
                if (collider.sharedMesh == originalMesh)
                {
                    Undo.RecordObject(collider, "Update Mesh Collider");
                    collider.sharedMesh = newMesh;
                }
            }
        }
    }
}
