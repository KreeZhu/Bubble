using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(HeroineModelBuilder))]
[CanEditMultipleObjects]
public class HeroineModelBuilderEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space(8f);

        EditorGUILayout.LabelField("Heroine Model Tools", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Use these tools to rebuild, bake, save, and inspect the player visual model.", MessageType.Info);

        if (GUILayout.Button("Rebuild Preview Model"))
            RunForEachBuilder(builder => builder.RebuildModel(), true);

        if (GUILayout.Button("Bake Model To Scene"))
            RunForEachBuilder(builder => builder.BakeModelToScene(), true);

        if (GUILayout.Button("Save Visual Prefab Asset"))
            RunForEachBuilder(builder => builder.SaveHeroineVisualPrefabAsset(), true);

        if (GUILayout.Button("Remove Baked Model"))
            RunForEachBuilder(builder => builder.RemoveBakedModel(), true);

        if (GUILayout.Button("Log Model Report"))
            RunForEachBuilder(builder => builder.LogModelReport(), false);

        if (GUILayout.Button("Validate Player Setup"))
            RunForEachBuilder(builder => builder.ValidateHeroinePlayerSetup(), true);

        if (GUILayout.Button("Export Validation Report"))
            RunForEachBuilder(builder => builder.ExportHeroineValidationReport(), true);

        if (GUILayout.Button("Export Unity Turnaround Preview"))
            RunForEachBuilder(builder => { ExportTurnaroundPreview(builder); }, false);
    }

    private void RunForEachBuilder(System.Action<HeroineModelBuilder> action, bool markDirty)
    {
        foreach (Object targetObject in targets)
        {
            HeroineModelBuilder builder = targetObject as HeroineModelBuilder;
            if (builder == null)
                continue;

            action(builder);

            if (markDirty)
                EditorUtility.SetDirty(builder);
        }
    }

    public static void ExportValidationReportForSampleScene()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);

        HeroineModelBuilder builder = Object.FindAnyObjectByType<HeroineModelBuilder>();
        if (builder == null)
        {
            Debug.LogError("Heroine validation failed: no HeroineModelBuilder found in SampleScene.");
            EditorApplication.Exit(1);
            return;
        }

        builder.SaveHeroineVisualPrefabAsset();
        builder.ExportHeroineValidationReport();
        string previewPath = ExportTurnaroundPreview(builder);
        if (string.IsNullOrEmpty(previewPath))
        {
            EditorApplication.Exit(1);
            return;
        }

        AppendPreviewReferenceToReport(previewPath);

        EditorUtility.SetDirty(builder);
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();

        Debug.Log("Heroine validation report exported for SampleScene.");
    }

    private static string ExportTurnaroundPreview(HeroineModelBuilder builder)
    {
        Transform sourceRoot = FindActiveVisualRoot(builder);
        if (sourceRoot == null)
        {
            Debug.LogError("Heroine preview export failed: no active visual root exists.", builder);
            return null;
        }

        const int previewLayer = 31;
        const int panelWidth = 512;
        const int panelHeight = 768;
        const string outputPath = "Assets/ConceptArt/heroine_unity_turnaround_preview.png";

        GameObject stageRoot = null;
        GameObject clone = null;
        GameObject lightObject = null;
        GameObject cameraObject = null;
        Camera previewCamera = null;
        Texture2D sheet = null;

        try
        {
            EnsureAssetFolder("Assets", "ConceptArt");

            stageRoot = new GameObject("HeroinePreviewStage");
            stageRoot.hideFlags = HideFlags.HideAndDontSave;

            clone = Instantiate(sourceRoot.gameObject);
            clone.name = "HeroinePreviewClone";
            clone.hideFlags = HideFlags.HideAndDontSave;
            clone.transform.SetParent(stageRoot.transform, false);
            clone.transform.localPosition = Vector3.zero;
            clone.transform.localRotation = Quaternion.identity;
            clone.transform.localScale = sourceRoot.lossyScale;
            SetLayerRecursively(clone.transform, previewLayer);
            DisablePreviewRuntimeBehaviours(clone);

            if (!TryGetRendererBounds(clone.transform, out Bounds bounds))
            {
                Debug.LogError("Heroine preview export failed: visual clone has no renderable bounds.", builder);
                return null;
            }

            lightObject = new GameObject("HeroinePreviewLight");
            lightObject.hideFlags = HideFlags.HideAndDontSave;
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.color = new Color(1.0f, 0.98f, 0.92f, 1f);
            lightObject.transform.rotation = Quaternion.Euler(45f, -35f, 0f);

            cameraObject = new GameObject("HeroinePreviewCamera");
            cameraObject.hideFlags = HideFlags.HideAndDontSave;
            previewCamera = cameraObject.AddComponent<Camera>();
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(0.90f, 0.92f, 0.94f, 1f);
            previewCamera.orthographic = true;
            previewCamera.nearClipPlane = 0.01f;
            previewCamera.farClipPlane = 50f;
            previewCamera.cullingMask = 1 << previewLayer;

            float aspect = (float)panelWidth / panelHeight;
            float uniformOrthoSize = Mathf.Max(bounds.extents.y, Mathf.Max(bounds.extents.x, bounds.extents.z) / aspect) * 1.18f;
            if (uniformOrthoSize < 0.1f)
                uniformOrthoSize = 1.2f;

            Vector3[] viewDirections =
            {
                Vector3.forward,
                Vector3.right,
                Vector3.back
            };

            sheet = new Texture2D(panelWidth * viewDirections.Length, panelHeight, TextureFormat.RGB24, false);
            FillTexture(sheet, previewCamera.backgroundColor);

            for (int i = 0; i < viewDirections.Length; i++)
            {
                Texture2D panel = RenderPreviewPanel(previewCamera, bounds, viewDirections[i], uniformOrthoSize, panelWidth, panelHeight);
                sheet.SetPixels(i * panelWidth, 0, panelWidth, panelHeight, panel.GetPixels());
                DestroyImmediate(panel);
            }

            sheet.Apply();
            System.IO.File.WriteAllBytes(outputPath, sheet.EncodeToPNG());
            AssetDatabase.ImportAsset(outputPath);
            Debug.Log($"Exported heroine Unity turnaround preview: {outputPath}", builder);
            return outputPath;
        }
        finally
        {
            if (sheet != null)
                DestroyImmediate(sheet);
            if (previewCamera != null)
                previewCamera.targetTexture = null;
            if (cameraObject != null)
                DestroyImmediate(cameraObject);
            if (lightObject != null)
                DestroyImmediate(lightObject);
            if (clone != null)
                DestroyImmediate(clone);
            if (stageRoot != null)
                DestroyImmediate(stageRoot);
        }
    }

    private static Transform FindActiveVisualRoot(HeroineModelBuilder builder)
    {
        Transform bakedRoot = builder.transform.Find("HeroineBakedVisual");
        if (builder.preferBakedVisual && bakedRoot != null)
            return bakedRoot;

        Transform previewRoot = builder.transform.Find("HeroineLowPolyVisual");
        return previewRoot != null ? previewRoot : bakedRoot;
    }

    private static Texture2D RenderPreviewPanel(Camera camera, Bounds bounds, Vector3 viewDirection, float orthographicSize, int width, int height)
    {
        Vector3 target = bounds.center;
        float cameraDistance = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z) * 3f + 3f;
        camera.transform.position = target + viewDirection.normalized * cameraDistance;
        camera.transform.LookAt(target, Vector3.up);
        camera.orthographicSize = orthographicSize;

        RenderTexture renderTexture = new RenderTexture(width, height, 24)
        {
            antiAliasing = 4
        };

        RenderTexture previousActive = RenderTexture.active;
        RenderTexture previousTarget = camera.targetTexture;
        camera.targetTexture = renderTexture;
        RenderTexture.active = renderTexture;
        camera.Render();

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
        texture.Apply();

        camera.targetTexture = previousTarget;
        RenderTexture.active = previousActive;
        renderTexture.Release();
        DestroyImmediate(renderTexture);

        return texture;
    }

    private static bool TryGetRendererBounds(Transform root, out Bounds bounds)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bounds = new Bounds(root.position, Vector3.zero);
        bool hasRenderer = false;

        foreach (Renderer renderer in renderers)
        {
            if (!renderer.enabled)
                continue;

            if (!hasRenderer)
            {
                bounds = renderer.bounds;
                hasRenderer = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasRenderer;
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        foreach (Transform child in root)
            SetLayerRecursively(child, layer);
    }

    private static void DisablePreviewRuntimeBehaviours(GameObject root)
    {
        MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (MonoBehaviour behaviour in behaviours)
            behaviour.enabled = false;
    }

    private static void FillTexture(Texture2D texture, Color color)
    {
        Color[] pixels = new Color[texture.width * texture.height];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = color;

        texture.SetPixels(pixels);
    }

    private static void AppendPreviewReferenceToReport(string previewPath)
    {
        if (string.IsNullOrEmpty(previewPath))
            return;

        const string reportPath = "Assets/ConceptArt/heroine_model_validation_report.md";
        string reportAppendix =
            "\n## Unity Turnaround Preview\n" +
            $"- Exported preview image: {previewPath}\n" +
            "- Rendered from the active generated player visual with orthographic front, side, and back cameras.\n";

        System.IO.File.AppendAllText(reportPath, reportAppendix, System.Text.Encoding.UTF8);
        AssetDatabase.ImportAsset(reportPath);
    }

    private static void EnsureAssetFolder(string parentFolder, string childFolder)
    {
        string folderPath = parentFolder + "/" + childFolder;
        if (!AssetDatabase.IsValidFolder(folderPath))
            AssetDatabase.CreateFolder(parentFolder, childFolder);
    }
}
