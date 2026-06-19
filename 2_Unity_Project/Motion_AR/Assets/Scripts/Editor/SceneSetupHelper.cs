#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MotionAR.Interaction;
using MotionAR.Core;
using MotionAR.AI;

namespace MotionAR.Editor
{
    /// <summary>
    /// Unity Editor utility that programmatically creates the complete scene hierarchy
    /// for the Motion AR training simulator. Eliminates manual scene setup.
    /// <para>
    /// Accessible via menu: <c>Motion AR → Setup Training Scene</c>.
    /// Creates all system GameObjects, assigns references, and configures layers.
    /// </para>
    /// </summary>
    public static class SceneSetupHelper
    {
        private const string MENU_PATH = "Motion AR/Setup Training Scene";
        private const string GRABBABLE_LAYER = "Grabbable";

        [MenuItem(MENU_PATH)]
        public static void SetupTrainingScene()
        {
            if (!EditorUtility.DisplayDialog(
                "Motion AR — Scene Setup",
                "This will create the complete scene hierarchy for the Motion AR training simulator.\n\n" +
                "• System GameObjects (HandTracking, Gesture, Grab, Session)\n" +
                "• Cursor Visual\n" +
                "• Sample Parts and SnapZones\n" +
                "• UI Canvas with all panels\n" +
                "• Camera and Lighting\n\n" +
                "Proceed?",
                "Create Scene", "Cancel"))
            {
                return;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Motion AR Training Scene");

            EnsureLayerExists(GRABBABLE_LAYER);

            // ═══════════════════════ ROOT OBJECTS ═══════════════════════

            // --- SYSTEM ---
            GameObject systemRoot = CreateRootObject("--- SYSTEM ---");

            // HandTrackingSystem
            GameObject handTrackingGO = CreateChildObject(systemRoot, "HandTrackingSystem");
            var handReceiver = Undo.AddComponent<HandTrackingReceiver>(handTrackingGO);
            var mockProvider = Undo.AddComponent<MockHandTrackingProvider>(handTrackingGO);
            var gestureDetector = Undo.AddComponent<GestureDetector>(handTrackingGO);

            // PinchGrabSystem
            GameObject pinchGrabGO = CreateChildObject(systemRoot, "PinchGrabSystem");
            var pinchGrabber = Undo.AddComponent<PinchGrabber>(pinchGrabGO);

            // SessionManager
            GameObject sessionGO = CreateChildObject(systemRoot, "SessionManager");
            var sessionManager = Undo.AddComponent<SessionManager>(sessionGO);

            // GeminiConnector
            GameObject geminiGO = CreateChildObject(systemRoot, "GeminiConnector");
            var geminiConnector = Undo.AddComponent<GeminiApiConnector>(geminiGO);

            // TrainingFlowController
            GameObject flowGO = CreateChildObject(systemRoot, "TrainingFlowController");
            var flowController = Undo.AddComponent<TrainingFlowController>(flowGO);

            // SceneBootstrapper
            GameObject bootstrapGO = CreateChildObject(systemRoot, "SceneBootstrapper");
            var bootstrapper = Undo.AddComponent<SceneBootstrapper>(bootstrapGO);

            // AppFlowManager
            GameObject appFlowGO = CreateChildObject(systemRoot, "AppFlowManager");
            Undo.AddComponent<AppFlowManager>(appFlowGO);

            // PerformanceMonitor
            GameObject perfGO = CreateChildObject(systemRoot, "PerformanceMonitor");
            Undo.AddComponent<PerformanceMonitor>(perfGO);

            // --- CURSOR ---
            GameObject cursorRoot = CreateRootObject("--- CURSOR ---");
            GameObject cursorVisual = CreateChildObject(cursorRoot, "CursorVisual");
            
            // Create sphere mesh for cursor.
            var meshFilter = Undo.AddComponent<MeshFilter>(cursorVisual);
            meshFilter.sharedMesh = GetPrimitiveMesh(PrimitiveType.Sphere);
            var meshRenderer = Undo.AddComponent<MeshRenderer>(cursorVisual);
            cursorVisual.transform.localScale = Vector3.one * 0.05f;

            // Emissive cyan material.
            Material cursorMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            cursorMat.name = "CursorMaterial";
            cursorMat.color = Color.cyan;
            cursorMat.SetColor("_EmissionColor", Color.cyan * 2f);
            cursorMat.EnableKeyword("_EMISSION");
            meshRenderer.sharedMaterial = cursorMat;

            // Add CursorEffects.
            Undo.AddComponent<MotionAR.UI.CursorEffects>(cursorVisual);

            // --- WORKSPACE ---
            GameObject workspaceRoot = CreateRootObject("--- WORKSPACE ---");

            // Part_A (Cube)
            GameObject partA = CreateGrabbablePart(workspaceRoot, "Part_A", PrimitiveType.Cube,
                new Vector3(-0.5f, 0, 0), Vector3.one * 0.15f, "part_a", Color.yellow);

            // Part_B (Cylinder)
            GameObject partB = CreateGrabbablePart(workspaceRoot, "Part_B", PrimitiveType.Cylinder,
                new Vector3(0.5f, 0, 0), new Vector3(0.1f, 0.15f, 0.1f), "part_b", Color.green);

            // Part_C (Sphere)
            GameObject partC = CreateGrabbablePart(workspaceRoot, "Part_C", PrimitiveType.Sphere,
                new Vector3(0, 0.5f, 0), Vector3.one * 0.12f, "part_c", new Color(0.3f, 0.5f, 1f));

            // SnapZone_A
            GameObject snapA = CreateSnapZone(workspaceRoot, "SnapZone_A", "snap_a", "part_a",
                new Vector3(0, -0.3f, 0));

            // SnapZone_B
            GameObject snapB = CreateSnapZone(workspaceRoot, "SnapZone_B", "snap_b", "part_b",
                new Vector3(0.3f, -0.3f, 0));

            // SnapZone_C
            GameObject snapC = CreateSnapZone(workspaceRoot, "SnapZone_C", "snap_c", "part_c",
                new Vector3(-0.3f, -0.3f, 0));

            // --- UI ---
            GameObject uiRoot = CreateRootObject("--- UI ---");
            
            // Create Canvas.
            GameObject canvasGO = CreateChildObject(uiRoot, "MainCanvas");
            var canvas = Undo.AddComponent<Canvas>(canvasGO);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            Undo.AddComponent<CanvasScaler>(canvasGO);
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            Undo.AddComponent<GraphicRaycaster>(canvasGO);

            // Create EventSystem if needed.
            if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject eventSystem = new GameObject("EventSystem");
                Undo.RegisterCreatedObjectUndo(eventSystem, "Create EventSystem");
                Undo.AddComponent<UnityEngine.EventSystems.EventSystem>(eventSystem);
                Undo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>(eventSystem);
            }

            // Create UI panels (placeholders).
            CreateUIPanel(canvasGO, "LoginPanel", "Login Screen");
            CreateUIPanel(canvasGO, "LessonSelectPanel", "Select a Lesson");
            CreateUIPanel(canvasGO, "TrainingHUD", "Training HUD");
            CreateUIPanel(canvasGO, "ScorePanel", "Score");
            CreateUIPanel(canvasGO, "HistoryPanel", "Session History");
            CreateUIPanel(canvasGO, "SettingsPanel", "Settings");

            // Transition overlay.
            GameObject overlayGO = CreateChildObject(canvasGO, "TransitionOverlay");
            var overlayImage = Undo.AddComponent<Image>(overlayGO);
            overlayImage.color = Color.black;
            overlayImage.raycastTarget = true;
            var overlayCanvasGroup = Undo.AddComponent<CanvasGroup>(overlayGO);
            overlayCanvasGroup.alpha = 0f;
            overlayCanvasGroup.blocksRaycasts = false;
            var overlayRT = overlayGO.GetComponent<RectTransform>();
            overlayRT.anchorMin = Vector2.zero;
            overlayRT.anchorMax = Vector2.one;
            overlayRT.offsetMin = Vector2.zero;
            overlayRT.offsetMax = Vector2.zero;
            overlayGO.SetActive(false);

            // ═══════════════════════ ASSIGN REFERENCES ═══════════════════════

            // Use SerializedObject to assign references via Inspector fields.
            AssignSerializedField(gestureDetector, "_handTrackingReceiver", handReceiver);
            AssignSerializedField(pinchGrabber, "_handTrackingReceiver", handReceiver);
            AssignSerializedField(pinchGrabber, "_gestureDetector", gestureDetector);
            AssignSerializedField(flowController, "_pinchGrabber", pinchGrabber);

            // SceneBootstrapper references.
            AssignSerializedField(bootstrapper, "_sessionManager", sessionManager);
            AssignSerializedField(bootstrapper, "_geminiApiConnector", geminiConnector);
            AssignSerializedField(bootstrapper, "_handTrackingReceiver", handReceiver);
            AssignSerializedField(bootstrapper, "_gestureDetector", gestureDetector);
            AssignSerializedField(bootstrapper, "_pinchGrabber", pinchGrabber);
            AssignSerializedField(bootstrapper, "_trainingFlowController", flowController);

            // ═══════════════════════ FINALIZE ═══════════════════════

            Undo.CollapseUndoOperations(undoGroup);
            EditorUtility.DisplayDialog(
                "Motion AR — Scene Setup Complete",
                "Scene hierarchy created successfully!\n\n" +
                "✅ System GameObjects (8 components)\n" +
                "✅ Cursor Visual with emission material\n" +
                "✅ 3 sample GrabbableParts + 3 SnapZones\n" +
                "✅ UI Canvas with 6 panels\n" +
                "✅ EventSystem\n\n" +
                "Next steps:\n" +
                "1. Assign ONNX model to GestureDetector\n" +
                "2. Configure Grabbable layer mask in PinchGrabber\n" +
                "3. Create LessonData ScriptableObject\n" +
                "4. Assign LessonData to TrainingFlowController\n" +
                "5. Press Play to test!",
                "OK");

            Selection.activeGameObject = systemRoot;
        }

        // ────────────────────── Helper Methods ──────────────────────

        private static GameObject CreateRootObject(string name)
        {
            GameObject go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            return go;
        }

        private static GameObject CreateChildObject(GameObject parent, string name)
        {
            GameObject go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            go.transform.SetParent(parent.transform, false);
            return go;
        }

        private static GameObject CreateGrabbablePart(
            GameObject parent, string name, PrimitiveType type,
            Vector3 position, Vector3 scale, string partID, Color color)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            Undo.RegisterCreatedObjectUndo(part, $"Create {name}");
            part.transform.SetParent(parent.transform, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;

            // Set layer.
            int layer = LayerMask.NameToLayer(GRABBABLE_LAYER);
            if (layer >= 0) part.layer = layer;

            // Material.
            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.name = $"{name}_Material";
                mat.color = color;
                renderer.sharedMaterial = mat;
            }

            // Add GrabbablePart.
            var grabbable = Undo.AddComponent<GrabbablePart>(part);
            var so = new SerializedObject(grabbable);
            so.FindProperty("_partID").stringValue = partID;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Add Rigidbody (no gravity).
            var rb = Undo.AddComponent<Rigidbody>(part);
            rb.useGravity = false;
            rb.isKinematic = false;

            return part;
        }

        private static GameObject CreateSnapZone(
            GameObject parent, string name, string zoneID, string expectedPartID,
            Vector3 position)
        {
            GameObject zone = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(zone, $"Create {name}");
            zone.transform.SetParent(parent.transform, false);
            zone.transform.localPosition = position;

            var snapZone = Undo.AddComponent<SnapZone>(zone);
            var so = new SerializedObject(snapZone);
            so.FindProperty("_snapZoneID").stringValue = zoneID;
            so.FindProperty("_expectedPartID").stringValue = expectedPartID;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Visual indicator (wireframe sphere in scene view handled by SnapZone.OnDrawGizmosSelected).
            // Add a small transparent sphere for visibility.
            GameObject indicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            indicator.name = "SnapIndicator";
            Undo.RegisterCreatedObjectUndo(indicator, "Create SnapIndicator");
            indicator.transform.SetParent(zone.transform, false);
            indicator.transform.localScale = Vector3.one * 0.08f;

            // Remove collider from indicator.
            var col = indicator.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);

            // Semi-transparent material.
            Renderer renderer = indicator.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.name = $"{name}_IndicatorMat";
                mat.SetFloat("_Surface", 1f); // Transparent
                mat.SetFloat("_Blend", 0f);
                mat.color = new Color(0f, 1f, 1f, 0.3f);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
                renderer.sharedMaterial = mat;
            }

            return zone;
        }

        private static void CreateUIPanel(GameObject canvasGO, string name, string title)
        {
            GameObject panel = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(panel, $"Create {name}");
            panel.transform.SetParent(canvasGO.transform, false);

            var rt = panel.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // Background.
            var bg = panel.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.05f, 0.1f, 0.85f);

            // Canvas Group for fade.
            panel.AddComponent<CanvasGroup>();

            // Title text.
            GameObject titleGO = new GameObject("Title");
            titleGO.transform.SetParent(panel.transform, false);
            var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
            titleTMP.text = title;
            titleTMP.fontSize = 36;
            titleTMP.color = Color.white;
            titleTMP.alignment = TextAlignmentOptions.Center;

            var titleRT = titleGO.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0.2f, 0.85f);
            titleRT.anchorMax = new Vector2(0.8f, 0.95f);
            titleRT.offsetMin = Vector2.zero;
            titleRT.offsetMax = Vector2.zero;

            // Start hidden (except Login).
            if (name != "LoginPanel")
            {
                panel.SetActive(false);
            }
        }

        private static void AssignSerializedField(Object target, string fieldName, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop != null)
            {
                prop.objectReferenceValue = value;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogWarning($"[SceneSetupHelper] Field '{fieldName}' not found on {target.GetType().Name}");
            }
        }

        private static void EnsureLayerExists(string layerName)
        {
            SerializedObject tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");

            // Check if layer already exists.
            for (int i = 0; i < layers.arraySize; i++)
            {
                if (layers.GetArrayElementAtIndex(i).stringValue == layerName)
                {
                    return; // Layer already exists.
                }
            }

            // Find first empty user layer (8-31).
            for (int i = 8; i < 32; i++)
            {
                SerializedProperty layerProp = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(layerProp.stringValue))
                {
                    layerProp.stringValue = layerName;
                    tagManager.ApplyModifiedProperties();
                    Debug.Log($"[SceneSetupHelper] Created layer '{layerName}' at index {i}");
                    return;
                }
            }

            Debug.LogWarning($"[SceneSetupHelper] Could not create layer '{layerName}': all user layers occupied.");
        }

        private static Mesh GetPrimitiveMesh(PrimitiveType type)
        {
            GameObject temp = GameObject.CreatePrimitive(type);
            Mesh mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(temp);
            return mesh;
        }

        // ────────────────────── Validation ──────────────────────

        [MenuItem(MENU_PATH, true)]
        private static bool ValidateSetupTrainingScene()
        {
            // Only enable in Edit mode.
            return !Application.isPlaying;
        }
    }
}
#endif
