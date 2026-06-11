using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class HeroineModelBuilder : MonoBehaviour
{
    private const string VisualRootName = "HeroineLowPolyVisual";
    private const string BakedVisualRootName = "HeroineBakedVisual";

    [Header("Build")]
    public bool rebuildInEditMode = true;
    public bool hideOriginalRenderer = true;
    public bool preferBakedVisual = true;
    public float modelScale = 1f;
    public Vector3 visualProportionScale = new Vector3(0.66f, 1.12f, 0.78f);
    public Vector3 localOffset = new Vector3(0f, -0.55f, 0f);

    [Header("Diagnostics")]
    [SerializeField] private bool drawModelBounds = true;
    [SerializeField] private int generatedPartCount;
    [SerializeField] private int generatedVertexCount;
    [SerializeField] private int generatedTriangleCount;
    [SerializeField] private Bounds generatedLocalBounds;
    [SerializeField, TextArea(2, 4)] private string lastValidationSummary = "Not built yet.";

    private Material skinMaterial;
    private Material skinShadowMaterial;
    private Material hairMaterial;
    private Material hairDarkMaterial;
    private Material hairHighlightMaterial;
    private Material eyeMaterial;
    private Material faceLineMaterial;
    private Material whiteClothMaterial;
    private Material whiteShadowMaterial;
    private Material tealClothMaterial;
    private Material darkLeggingMaterial;
    private Material beltMaterial;
    private Material goldMaterial;
    private Material shoeMetalMaterial;
    private Material shoeBrightMetalMaterial;
    private Material shoeShadowMetalMaterial;
    private Material bubbleMaterial;
    private bool buildingPersistentVisual;
#if UNITY_EDITOR
    private bool editorRebuildQueued;
#endif

    private void OnEnable()
    {
        RequestRebuild();
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        QueueEditorRebuild();
#endif
    }

    private void RequestRebuild()
    {
        if (Application.isPlaying)
        {
            RebuildModel();
            return;
        }

        if (!rebuildInEditMode)
            return;

#if UNITY_EDITOR
        QueueEditorRebuild();
#endif
    }

#if UNITY_EDITOR
    private void QueueEditorRebuild()
    {
        if (Application.isPlaying || !rebuildInEditMode || editorRebuildQueued)
            return;

        editorRebuildQueued = true;
        UnityEditor.EditorApplication.delayCall -= RebuildModelFromEditorDelay;
        UnityEditor.EditorApplication.delayCall += RebuildModelFromEditorDelay;
    }

    private void RebuildModelFromEditorDelay()
    {
        UnityEditor.EditorApplication.delayCall -= RebuildModelFromEditorDelay;
        editorRebuildQueued = false;

        if (this != null && !Application.isPlaying && rebuildInEditMode)
            RebuildModel();
    }
#endif

    [ContextMenu("Rebuild Heroine Model")]
    public void RebuildModel()
    {
        if (!Application.isPlaying && !rebuildInEditMode)
            return;

        Transform bakedRoot = transform.Find(BakedVisualRootName);
        if (preferBakedVisual && bakedRoot != null)
        {
            ClearGeneratedVisual(VisualRootName);
            SetOriginalRendererVisible(!hideOriginalRenderer);

            List<string> bakedValidationIssues = new List<string>();
            ValidateGeneratedModel(bakedRoot, bakedValidationIssues);
            UpdateDiagnostics(bakedRoot, bakedValidationIssues);
            return;
        }

        BuildVisual(VisualRootName, false, true);
    }

    [ContextMenu("Bake Heroine Model To Scene")]
    public void BakeModelToScene()
    {
        ClearGeneratedVisual(VisualRootName);
        BuildVisual(BakedVisualRootName, true, true);
    }

    [ContextMenu("Remove Baked Heroine Model")]
    public void RemoveBakedModel()
    {
        ClearGeneratedVisual(BakedVisualRootName);
        RebuildModel();
    }

#if UNITY_EDITOR
    [ContextMenu("Save Heroine Visual Prefab Asset")]
    public void SaveHeroineVisualPrefabAsset()
    {
        BakeModelToScene();

        Transform bakedRoot = transform.Find(BakedVisualRootName);
        if (bakedRoot == null)
        {
            Debug.LogError("Cannot save heroine prefab because the baked visual root was not created.", this);
            return;
        }

        const string prefabFolder = "Assets/Prefabs";
        const string heroineFolder = "Assets/Prefabs/Heroine";
        const string meshFolder = "Assets/Prefabs/Heroine/Meshes";
        const string materialFolder = "Assets/Prefabs/Heroine/Materials";
        const string prefabPath = "Assets/Prefabs/Heroine/HeroineBakedVisual.prefab";

        EnsureAssetFolder("Assets", "Prefabs");
        EnsureAssetFolder(prefabFolder, "Heroine");
        EnsureAssetFolder(heroineFolder, "Meshes");
        EnsureAssetFolder(heroineFolder, "Materials");

        ClearGeneratedAssetsInFolder(meshFolder, ".asset");
        ClearGeneratedAssetsInFolder(materialFolder, ".mat");
        PersistGeneratedMeshes(bakedRoot, meshFolder);
        PersistGeneratedMaterials(bakedRoot, materialFolder);

        GameObject prefab = UnityEditor.PrefabUtility.SaveAsPrefabAsset(bakedRoot.gameObject, prefabPath, out bool success);
        UnityEditor.AssetDatabase.SaveAssets();
        UnityEditor.AssetDatabase.Refresh();

        if (success && prefab != null)
            Debug.Log($"Saved heroine visual prefab: {prefabPath}", this);
        else
            Debug.LogError($"Failed to save heroine visual prefab: {prefabPath}", this);
    }

    [ContextMenu("Export Heroine Validation Report")]
    public void ExportHeroineValidationReport()
    {
        bool passed = ValidateHeroinePlayerSetup();
        Transform root = GetActiveVisualRoot();

        EnsureAssetFolder("Assets", "ConceptArt");
        const string reportPath = "Assets/ConceptArt/heroine_model_validation_report.md";

        StringBuilder report = new StringBuilder();
        report.AppendLine("# Heroine Model Validation Report");
        report.AppendLine();
        report.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        report.AppendLine($"Status: {(passed ? "PASS" : "NEEDS REVIEW")}");
        report.AppendLine($"Player object: {gameObject.name}");
        report.AppendLine($"Active visual root: {(root != null ? root.name : "None")}");
        report.AppendLine();

        report.AppendLine("## Binding");
        report.AppendLine($"- PlayerMovement present: {GetComponent<PlayerMovement>() != null}");
        report.AppendLine($"- Rigidbody present: {GetComponent<Rigidbody>() != null}");
        MeshRenderer ownRenderer = GetComponent<MeshRenderer>();
        report.AppendLine($"- Original MeshRenderer hidden: {ownRenderer == null || !ownRenderer.enabled}");
        report.AppendLine($"- Prefer baked visual: {preferBakedVisual}");
        report.AppendLine($"- Baked scene visual present: {transform.Find(BakedVisualRootName) != null}");
        report.AppendLine($"- Preview visual present: {transform.Find(VisualRootName) != null}");
        bool prefabAssetPresent = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Heroine/HeroineBakedVisual.prefab") != null;
        report.AppendLine($"- Saved prefab asset present: {prefabAssetPresent}");
        report.AppendLine();

        report.AppendLine("## Visual Diagnostics");
        report.AppendLine($"- Summary: {lastValidationSummary}");
        report.AppendLine($"- Generated part count: {generatedPartCount}");
        report.AppendLine($"- Generated vertex count: {generatedVertexCount}");
        report.AppendLine($"- Generated triangle count: {generatedTriangleCount}");
        report.AppendLine($"- Visual proportion scale: {FormatVector(visualProportionScale)}");
        report.AppendLine($"- Local bounds center: {FormatVector(generatedLocalBounds.center)}");
        report.AppendLine($"- Local bounds size: {FormatVector(generatedLocalBounds.size)}");
        report.AppendLine();

        if (root != null && TryCalculateRootSpaceBounds(root, out Bounds bounds, out int partCount, out int vertexCount, out int triangleCount))
        {
            report.AppendLine("## Recalculated Bounds");
            report.AppendLine($"- Parts: {partCount}");
            report.AppendLine($"- Vertices: {vertexCount}");
            report.AppendLine($"- Triangles: {triangleCount}");
            report.AppendLine($"- Bounds min: {FormatVector(bounds.min)}");
            report.AppendLine($"- Bounds max: {FormatVector(bounds.max)}");
            report.AppendLine($"- Bounds size: {FormatVector(bounds.size)}");
            report.AppendLine();
        }

        AppendRuntimeBindingDiagnostics(report, root);

        report.AppendLine("## Manual Visual Review Still Required");
        report.AppendLine("- Compare the silhouette to the first concept image in the Scene/Game view.");
        report.AppendLine("- Check front, side, and back readability after baking or prefab saving.");
        report.AppendLine("- Playtest movement and jumping to judge hair, cape, and skirt sway strength.");
        report.AppendLine("- Confirm the model does not visually fight the capsule/player collision size.");

        File.WriteAllText(reportPath, report.ToString(), Encoding.UTF8);
        UnityEditor.AssetDatabase.ImportAsset(reportPath);
        Debug.Log($"Exported heroine validation report: {reportPath}", this);
    }

    private void AppendRuntimeBindingDiagnostics(StringBuilder report, Transform root)
    {
        report.AppendLine("## Runtime Visual Binding Check");

        if (root == null)
        {
            report.AppendLine("- Runtime binding test: SKIPPED, no active visual root.");
            report.AppendLine();
            return;
        }

        Vector3 originalPlayerPosition = transform.position;
        Vector3 originalRootPosition = root.position;
        Vector3 testDelta = new Vector3(0.37f, 0.21f, -0.16f);

        transform.position = originalPlayerPosition + testDelta;
        Vector3 followedDelta = root.position - originalRootPosition;
        float followError = (followedDelta - testDelta).magnitude;
        transform.position = originalPlayerPosition;

        HeroineVisualSway sway = root.GetComponent<HeroineVisualSway>();
        int swayTargetCount = 0;
        int changedSwayTargets = 0;

        if (sway != null)
        {
            sway.RefreshTargets();
            swayTargetCount = sway.TargetCount;

            Dictionary<Transform, Quaternion> originalRotations = new Dictionary<Transform, Quaternion>();
            foreach (Transform child in root)
                originalRotations[child] = child.localRotation;

            changedSwayTargets = sway.ApplySwayForValidation(new Vector3(1.3f, 3.8f, 0.7f), 0.16f, 1.25f);

            foreach (KeyValuePair<Transform, Quaternion> pair in originalRotations)
            {
                if (pair.Key != null)
                    pair.Key.localRotation = pair.Value;
            }

            sway.RefreshTargets();
        }

        report.AppendLine($"- Visual root parented to Player: {root.parent == transform}");
        report.AppendLine($"- Visual follow test error: {followError:0.0000}");
        report.AppendLine($"- Sway component present: {sway != null}");
        report.AppendLine($"- Sway target count: {swayTargetCount}");
        report.AppendLine($"- Sway targets changed in simulated jump: {changedSwayTargets}");
        report.AppendLine();
    }

    private static void EnsureAssetFolder(string parentFolder, string childFolder)
    {
        string folderPath = parentFolder + "/" + childFolder;
        if (!UnityEditor.AssetDatabase.IsValidFolder(folderPath))
            UnityEditor.AssetDatabase.CreateFolder(parentFolder, childFolder);
    }

    private static void ClearGeneratedAssetsInFolder(string folderPath, string extension)
    {
        string[] assetGuids = UnityEditor.AssetDatabase.FindAssets(string.Empty, new[] { folderPath });
        foreach (string assetGuid in assetGuids)
        {
            string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(assetGuid);
            if (Path.GetExtension(assetPath) == extension)
                UnityEditor.AssetDatabase.DeleteAsset(assetPath);
        }
    }

    private static void PersistGeneratedMeshes(Transform root, string meshFolder)
    {
        MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < filters.Length; i++)
        {
            Mesh sourceMesh = filters[i].sharedMesh;
            if (sourceMesh == null)
                continue;

            string assetName = SafeAssetName(filters[i].gameObject.name + "_" + i + "_Mesh");
            string assetPath = meshFolder + "/" + assetName + ".asset";
            UnityEditor.AssetDatabase.DeleteAsset(assetPath);

            Mesh meshAsset = Instantiate(sourceMesh);
            meshAsset.name = assetName;
            meshAsset.hideFlags = HideFlags.None;
            UnityEditor.AssetDatabase.CreateAsset(meshAsset, assetPath);
            filters[i].sharedMesh = meshAsset;
            UnityEditor.EditorUtility.SetDirty(filters[i]);
        }
    }

    private static void PersistGeneratedMaterials(Transform root, string materialFolder)
    {
        Dictionary<Material, Material> materialAssets = new Dictionary<Material, Material>();
        MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
        int materialIndex = 0;

        foreach (MeshRenderer meshRenderer in renderers)
        {
            Material[] sharedMaterials = meshRenderer.sharedMaterials;
            bool changed = false;

            for (int i = 0; i < sharedMaterials.Length; i++)
            {
                Material sourceMaterial = sharedMaterials[i];
                if (sourceMaterial == null)
                    continue;

                if (!materialAssets.TryGetValue(sourceMaterial, out Material materialAsset))
                {
                    string assetName = SafeAssetName(sourceMaterial.name + "_" + materialIndex);
                    string assetPath = materialFolder + "/" + assetName + ".mat";
                    UnityEditor.AssetDatabase.DeleteAsset(assetPath);

                    materialAsset = Instantiate(sourceMaterial);
                    materialAsset.name = assetName;
                    materialAsset.hideFlags = HideFlags.None;
                    UnityEditor.AssetDatabase.CreateAsset(materialAsset, assetPath);
                    materialAssets.Add(sourceMaterial, materialAsset);
                    materialIndex++;
                }

                sharedMaterials[i] = materialAsset;
                changed = true;
            }

            if (changed)
            {
                meshRenderer.sharedMaterials = sharedMaterials;
                UnityEditor.EditorUtility.SetDirty(meshRenderer);
            }
        }
    }

    private static string SafeAssetName(string rawName)
    {
        string safeName = string.IsNullOrWhiteSpace(rawName) ? "HeroineAsset" : rawName.Trim();
        foreach (char invalidChar in System.IO.Path.GetInvalidFileNameChars())
            safeName = safeName.Replace(invalidChar, '_');

        return safeName.Replace(' ', '_');
    }
#endif

    private void BuildVisual(string rootName, bool persistentInScene, bool clearExisting)
    {
        if (clearExisting)
            ClearGeneratedVisual(rootName);

        SetOriginalRendererVisible(!hideOriginalRenderer);
        buildingPersistentVisual = persistentInScene;
        try
        {
            CreateMaterials();

            Transform root = new GameObject(rootName).transform;
            ApplyGeneratedObjectFlags(root.gameObject);
            root.SetParent(transform, false);
            root.localPosition = localOffset;
            root.localRotation = Quaternion.identity;
            Vector3 parentCompensationScale = new Vector3(
                SafeInverse(transform.localScale.x),
                SafeInverse(transform.localScale.y),
                SafeInverse(transform.localScale.z));
            root.localScale = Vector3.Scale(parentCompensationScale, visualProportionScale) * modelScale;

            BuildBody(root);
            BuildClothing(root);
            BuildHair(root);
            BuildAccessories(root);
            ConfigureVisualSway(root);

            List<string> validationIssues = new List<string>();
            ValidateGeneratedModel(root, validationIssues);
            UpdateDiagnostics(root, validationIssues);
        }
        finally
        {
            buildingPersistentVisual = false;
        }
    }

    private void ConfigureVisualSway(Transform root)
    {
        HeroineVisualSway sway = root.GetComponent<HeroineVisualSway>();
        if (sway == null)
            sway = root.gameObject.AddComponent<HeroineVisualSway>();

        ApplyGeneratedObjectFlags(sway);
        sway.RefreshTargets();
    }

    [ContextMenu("Log Heroine Model Report")]
    public void LogModelReport()
    {
        Transform root = GetActiveVisualRoot();
        if (root != null)
        {
            List<string> validationIssues = new List<string>();
            ValidateGeneratedModel(root, validationIssues);
            UpdateDiagnostics(root, validationIssues);
        }

        Debug.Log($"Heroine model report: {lastValidationSummary}", this);
    }

    [ContextMenu("Validate Heroine Player Setup")]
    public bool ValidateHeroinePlayerSetup()
    {
        RebuildModel();

        List<string> setupIssues = new List<string>();
        if (GetComponent<PlayerMovement>() == null)
            setupIssues.Add("Player is missing PlayerMovement.");

        if (GetComponent<Rigidbody>() == null)
            setupIssues.Add("Player is missing Rigidbody.");

        MeshRenderer ownRenderer = GetComponent<MeshRenderer>();
        if (hideOriginalRenderer && ownRenderer != null && ownRenderer.enabled)
            setupIssues.Add("Original player MeshRenderer is still visible.");

        Transform root = GetActiveVisualRoot();
        if (root == null)
        {
            setupIssues.Add("No generated or baked heroine visual root exists.");
            UpdateDiagnosticsFromIssues(setupIssues);
            Debug.LogWarning($"Heroine player setup validation failed: {lastValidationSummary}", this);
            return false;
        }

        if (root.parent != transform)
            setupIssues.Add("Heroine visual root is not parented to the Player.");

        HeroineVisualSway sway = root.GetComponent<HeroineVisualSway>();
        if (sway == null)
        {
            setupIssues.Add("Heroine visual root is missing HeroineVisualSway.");
        }
        else
        {
            sway.RefreshTargets();
            if (sway.TargetCount < 8)
                setupIssues.Add($"Heroine visual has too few sway targets: {sway.TargetCount}.");
        }

        ValidateGeneratedModel(root, setupIssues);
        ValidateModelBounds(root, setupIssues);
        UpdateDiagnostics(root, setupIssues);

        if (setupIssues.Count == 0)
        {
            Debug.Log($"Heroine player setup validation passed: {lastValidationSummary}", this);
            return true;
        }

        Debug.LogWarning($"Heroine player setup validation failed: {lastValidationSummary}", this);
        return false;
    }

    private Transform GetActiveVisualRoot()
    {
        Transform bakedRoot = transform.Find(BakedVisualRootName);
        if (preferBakedVisual && bakedRoot != null)
            return bakedRoot;

        Transform previewRoot = transform.Find(VisualRootName);
        return previewRoot != null ? previewRoot : bakedRoot;
    }

    private void ValidateModelBounds(Transform root, List<string> validationIssues)
    {
        if (!TryCalculateRootSpaceBounds(root, out Bounds bounds, out int partCount, out int vertexCount, out int triangleCount))
        {
            validationIssues.Add("Heroine visual has no mesh bounds.");
            return;
        }

        if (partCount < 50)
            validationIssues.Add($"Heroine visual has too few mesh parts: {partCount}.");

        if (vertexCount < 900)
            validationIssues.Add($"Heroine visual has too few vertices for the current low-poly model: {vertexCount}.");

        if (triangleCount < 400)
            validationIssues.Add($"Heroine visual has too few triangles for the current low-poly model: {triangleCount}.");

        if (bounds.size.y < 1.55f || bounds.size.y > 2.15f)
            validationIssues.Add($"Heroine visual height looks out of range: {bounds.size.y:0.00}.");

        if (bounds.min.y < -0.08f || bounds.min.y > 0.18f)
            validationIssues.Add($"Heroine visual foot height may be misaligned: {bounds.min.y:0.00}.");
    }

    private void UpdateDiagnosticsFromIssues(List<string> validationIssues)
    {
        generatedPartCount = 0;
        generatedVertexCount = 0;
        generatedTriangleCount = 0;
        generatedLocalBounds = default;
        lastValidationSummary = validationIssues.Count == 0
            ? "OK."
            : $"Issues: {validationIssues.Count}. " + string.Join("; ", validationIssues.GetRange(0, Mathf.Min(validationIssues.Count, 4)));
    }

    private void ClearGeneratedVisual(string rootName)
    {
        List<GameObject> childrenToRemove = new List<GameObject>();
        foreach (Transform child in transform)
        {
            if (child.name == rootName)
                childrenToRemove.Add(child.gameObject);
        }

        foreach (GameObject child in childrenToRemove)
        {
            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }
    }

    private void SetOriginalRendererVisible(bool visible)
    {
        MeshRenderer ownRenderer = GetComponent<MeshRenderer>();
        if (ownRenderer != null)
            ownRenderer.enabled = visible;
    }

    private void CreateMaterials()
    {
        skinMaterial = MakeMaterial("Heroine Skin", new Color(1.00f, 0.72f, 0.55f, 1f));
        skinShadowMaterial = MakeMaterial("Heroine Skin Shadow", new Color(0.78f, 0.46f, 0.34f, 1f));
        hairMaterial = MakeMaterial("Heroine Blue Hair", new Color(0.06f, 0.34f, 0.98f, 1f));
        hairDarkMaterial = MakeMaterial("Heroine Dark Blue Hair", new Color(0.03f, 0.14f, 0.52f, 1f));
        hairHighlightMaterial = MakeMaterial("Heroine Clear Blue Hair Highlight", new Color(0.16f, 0.68f, 1.00f, 1f));
        eyeMaterial = MakeMaterial("Heroine Blue Eyes", new Color(0.22f, 0.72f, 1.00f, 1f));
        faceLineMaterial = MakeMaterial("Heroine Face Lines", new Color(0.34f, 0.16f, 0.13f, 1f));
        whiteClothMaterial = MakeMaterial("Heroine Ivory Cloth", new Color(1.00f, 0.96f, 0.84f, 1f));
        whiteShadowMaterial = MakeMaterial("Heroine Ivory Cloth Shadow", new Color(0.86f, 0.80f, 0.66f, 1f));
        tealClothMaterial = MakeMaterial("Heroine Teal Cloth", new Color(0.09f, 0.38f, 0.42f, 1f));
        darkLeggingMaterial = MakeMaterial("Heroine Dark Leggings", new Color(0.12f, 0.13f, 0.16f, 1f));
        beltMaterial = MakeMaterial("Heroine Leather Belt", new Color(0.42f, 0.26f, 0.15f, 1f));
        goldMaterial = MakeMaterial("Heroine Warm Gold", new Color(0.95f, 0.67f, 0.22f, 1f));
        shoeMetalMaterial = MakeMaterial("Heroine Light Shoe Metal", new Color(0.73f, 0.78f, 0.79f, 1f));
        shoeBrightMetalMaterial = MakeMaterial("Heroine Bright Shoe Metal", new Color(0.90f, 0.96f, 0.98f, 1f));
        shoeShadowMetalMaterial = MakeMaterial("Heroine Cool Shoe Shadow Metal", new Color(0.32f, 0.40f, 0.44f, 1f));
        bubbleMaterial = MakeMaterial("Heroine Bubble Charm", new Color(0.35f, 0.86f, 1f, 0.65f));
        ConfigureTransparentMaterial(bubbleMaterial);
    }

    private Material MakeMaterial(string materialName, Color color)
    {
        Shader shader = Shader.Find("Bubble/Heroine Low Poly Flat");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Simple Lit");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material material = new Material(shader);
        material.name = materialName;
        material.color = color;
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 0.12f);
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", 0f);
        if (material.HasProperty("_SpecColor"))
            material.SetColor("_SpecColor", new Color(0.08f, 0.08f, 0.08f, 1f));
        if (material.HasProperty("_ShadeStrength"))
            material.SetFloat("_ShadeStrength", 0.42f);
        ApplyGeneratedObjectFlags(material);
        return material;
    }

    private void ConfigureTransparentMaterial(Material material)
    {
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        material.SetOverrideTag("RenderType", "Transparent");

        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend"))
            material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_Mode"))
            material.SetFloat("_Mode", 3f);
        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend"))
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 0f);

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
    }

    private void BuildBody(Transform root)
    {
        CreateLowPolySphere(root, "Head", new Vector3(0f, 1.56f, 0.02f), new Vector3(0.19f, 0.265f, 0.17f), skinMaterial, 8, 5);
        CreateTaperedCylinder(root, "Neck", new Vector3(0f, 1.28f, 0f), new Vector3(0f, 1.40f, 0f), 0.055f, 0.070f, skinMaterial, 6);
        BuildFace(root);

        CreateLowPolyPrism(root, "TorsoCore", new Vector3(0f, 1.075f, 0.010f), 0.52f, 0.335f, 0.250f, 0.245f, tealClothMaterial);
        CreateLowPolyPrism(root, "Hips", new Vector3(0f, 0.780f, 0.005f), 0.190f, 0.270f, 0.310f, 0.215f, darkLeggingMaterial);
        CreateLowPolyBox(root, "SlimWaistGap", new Vector3(0f, 0.885f, 0f), new Vector3(0.34f, 0.032f, 0.26f), darkLeggingMaterial, Quaternion.identity);

        BuildArm(root, -1f);
        BuildArm(root, 1f);
        BuildLeg(root, -1f);
        BuildLeg(root, 1f);
    }

    private void BuildFace(Transform root)
    {
        CreateLowPolySphere(root, "FaceSoftPlane", new Vector3(0f, 1.535f, 0.188f), new Vector3(0.135f, 0.185f, 0.028f), skinMaterial, 8, 4);
        CreatePanel(root, "FaceFrontPanel", new[]
        {
            new Vector3(-0.110f, 1.655f, 0.236f),
            new Vector3(0.110f, 1.655f, 0.236f),
            new Vector3(0.095f, 1.430f, 0.246f),
            new Vector3(-0.095f, 1.430f, 0.246f)
        }, skinMaterial);
        CreateLowPolyBox(root, "LeftEye", new Vector3(-0.058f, 1.585f, 0.260f), new Vector3(0.052f, 0.022f, 0.010f), eyeMaterial, Quaternion.Euler(0f, 0f, -4f));
        CreateLowPolyBox(root, "RightEye", new Vector3(0.058f, 1.585f, 0.260f), new Vector3(0.052f, 0.022f, 0.010f), eyeMaterial, Quaternion.Euler(0f, 0f, 4f));
        CreateLowPolyBox(root, "LeftBrow", new Vector3(-0.060f, 1.625f, 0.262f), new Vector3(0.060f, 0.010f, 0.010f), hairDarkMaterial, Quaternion.Euler(0f, 0f, -12f));
        CreateLowPolyBox(root, "RightBrow", new Vector3(0.060f, 1.625f, 0.262f), new Vector3(0.060f, 0.010f, 0.010f), hairDarkMaterial, Quaternion.Euler(0f, 0f, 12f));
        CreateLowPolyBox(root, "NoseBridge", new Vector3(0f, 1.530f, 0.264f), new Vector3(0.012f, 0.040f, 0.008f), skinShadowMaterial, Quaternion.identity);
        CreateLowPolyBox(root, "Mouth", new Vector3(0f, 1.462f, 0.262f), new Vector3(0.040f, 0.008f, 0.008f), faceLineMaterial, Quaternion.identity);
        CreateLowPolySphere(root, "LeftEar", new Vector3(-0.185f, 1.545f, 0.005f), new Vector3(0.028f, 0.052f, 0.022f), skinMaterial, 6, 3);
        CreateLowPolySphere(root, "RightEar", new Vector3(0.185f, 1.545f, 0.005f), new Vector3(0.028f, 0.052f, 0.022f), skinMaterial, 6, 3);
    }

    private void BuildArm(Transform root, float side)
    {
        Vector3 shoulder = new Vector3(side * 0.285f, 1.18f, 0.01f);
        Vector3 elbow = new Vector3(side * 0.405f, 0.86f, 0.03f);
        Vector3 wrist = new Vector3(side * 0.455f, 0.56f, 0.05f);

        CreateTaperedCylinder(root, SideName(side, "UpperArm"), shoulder, elbow, 0.045f, 0.037f, skinMaterial, 6);
        CreateTaperedCylinder(root, SideName(side, "Forearm"), elbow, wrist, 0.039f, 0.030f, skinMaterial, 6);
        CreateTaperedCylinder(root, SideName(side, "Bracer"), Vector3.Lerp(elbow, wrist, 0.25f), Vector3.Lerp(elbow, wrist, 0.92f), 0.054f, 0.045f, tealClothMaterial, 6);
        CreateLowPolySphere(root, SideName(side, "Hand"), wrist + new Vector3(side * 0.015f, -0.045f, 0.02f), new Vector3(0.045f, 0.075f, 0.035f), skinMaterial, 6, 3);
        CreateTaperedCylinder(root, SideName(side, "WristGoldBand"), Vector3.Lerp(elbow, wrist, 0.86f), Vector3.Lerp(elbow, wrist, 0.95f), 0.050f, 0.047f, goldMaterial, 6);
    }

    private void BuildLeg(Transform root, float side)
    {
        Vector3 hip = new Vector3(side * 0.125f, 0.79f, 0.02f);
        Vector3 knee = new Vector3(side * 0.145f, 0.43f, 0.02f);
        Vector3 ankle = new Vector3(side * 0.155f, 0.12f, 0.02f);

        CreateTaperedCylinder(root, SideName(side, "Thigh"), hip, knee, 0.062f, 0.054f, darkLeggingMaterial, 6);
        CreateTaperedCylinder(root, SideName(side, "Shin"), knee, ankle, 0.052f, 0.040f, darkLeggingMaterial, 6);

        CreateLowPolySphere(root, SideName(side, "StreamlinedShoe"), new Vector3(side * 0.155f, 0.060f, 0.13f), new Vector3(0.074f, 0.046f, 0.220f), shoeMetalMaterial, 8, 3);
        CreateLowPolyBox(root, SideName(side, "ShoeDarkFloatingSole"), new Vector3(side * 0.155f, 0.014f, 0.12f), new Vector3(0.138f, 0.018f, 0.335f), shoeShadowMetalMaterial, Quaternion.identity);
        CreateLowPolyBox(root, SideName(side, "ShoeSilverToeCap"), new Vector3(side * 0.155f, 0.070f, 0.305f), new Vector3(0.112f, 0.040f, 0.104f), shoeBrightMetalMaterial, Quaternion.Euler(-7f, 0f, 0f));
        CreateLowPolyBox(root, SideName(side, "ShoeGoldHeel"), new Vector3(side * 0.155f, 0.064f, -0.078f), new Vector3(0.082f, 0.052f, 0.044f), goldMaterial, Quaternion.identity);
        CreateTaperedCylinder(root, SideName(side, "ShoeMetalAnkleCuff"), new Vector3(side * 0.155f, 0.105f, 0.0f), new Vector3(side * 0.155f, 0.190f, 0.0f), 0.066f, 0.058f, shoeBrightMetalMaterial, 6);
        CreateLowPolyBox(root, SideName(side, "ShoeInstepGoldLine"), new Vector3(side * 0.155f, 0.125f, 0.135f), new Vector3(0.088f, 0.014f, 0.195f), goldMaterial, Quaternion.Euler(-9f, 0f, 0f));
        CreateLowPolyBox(root, SideName(side, "ShoeOuterFlowFin"), new Vector3(side * 0.230f, 0.064f, 0.105f), new Vector3(0.018f, 0.034f, 0.250f), shoeBrightMetalMaterial, Quaternion.Euler(0f, side * -8f, side * 8f));
        CreateLowPolyBox(root, SideName(side, "ShoeInnerDarkInset"), new Vector3(side * 0.090f, 0.062f, 0.115f), new Vector3(0.016f, 0.028f, 0.205f), shoeShadowMetalMaterial, Quaternion.Euler(0f, side * 6f, side * -4f));
        CreateLowPolyBox(root, SideName(side, "ShoeRearWing"), new Vector3(side * 0.155f, 0.096f, -0.112f), new Vector3(0.098f, 0.018f, 0.125f), shoeBrightMetalMaterial, Quaternion.Euler(10f, 0f, 0f));
        CreateLowPolyBox(root, SideName(side, "ShoeForwardBlade"), new Vector3(side * 0.155f, 0.044f, 0.375f), new Vector3(0.086f, 0.016f, 0.112f), shoeBrightMetalMaterial, Quaternion.Euler(-10f, 0f, 0f));
        CreateLowPolyBox(root, SideName(side, "ShoeTealFlowLine"), new Vector3(side * 0.205f, 0.105f, 0.160f), new Vector3(0.014f, 0.014f, 0.195f), tealClothMaterial, Quaternion.Euler(-8f, side * -8f, side * 6f));
    }

    private void BuildClothing(Transform root)
    {
        CreateSeparatedUpper(root);
        CreateSeparatedWaist(root);
        CreateSeparatedLower(root);
        CreateCapelet(root);
        CreateSkirtPanels(root);
    }

    private void CreateSeparatedUpper(Transform root)
    {
        CreatePanel(root, "IvoryWrapLeft", new[]
        {
            new Vector3(-0.205f, 1.235f, 0.200f),
            new Vector3(-0.028f, 1.210f, 0.215f),
            new Vector3(-0.090f, 1.000f, 0.225f),
            new Vector3(-0.250f, 0.995f, 0.210f)
        }, whiteClothMaterial);

        CreatePanel(root, "IvoryWrapRight", new[]
        {
            new Vector3(0.028f, 1.210f, 0.215f),
            new Vector3(0.205f, 1.235f, 0.200f),
            new Vector3(0.250f, 0.995f, 0.210f),
            new Vector3(0.090f, 1.000f, 0.225f)
        }, whiteClothMaterial);

        CreateLowPolyBox(root, "UpperJacketLeftThickness", new Vector3(-0.205f, 1.110f, 0.070f), new Vector3(0.040f, 0.230f, 0.220f), whiteShadowMaterial, Quaternion.Euler(0f, -8f, -8f));
        CreateLowPolyBox(root, "UpperJacketRightThickness", new Vector3(0.205f, 1.110f, 0.070f), new Vector3(0.040f, 0.230f, 0.220f), whiteShadowMaterial, Quaternion.Euler(0f, 8f, 8f));

        CreateLowPolyBox(root, "MidriffSkinBand", new Vector3(0f, 0.925f, 0.232f), new Vector3(0.225f, 0.082f, 0.018f), skinMaterial, Quaternion.identity);
        CreateLowPolyBox(root, "UpperHemGoldFront", new Vector3(0f, 0.968f, 0.234f), new Vector3(0.330f, 0.022f, 0.020f), goldMaterial, Quaternion.identity);
        CreateLowPolyBox(root, "UpperHemGoldBack", new Vector3(0f, 0.968f, -0.200f), new Vector3(0.320f, 0.022f, 0.020f), goldMaterial, Quaternion.identity);
        CreateLowPolyBox(root, "TealChestInset", new Vector3(0f, 1.115f, 0.226f), new Vector3(0.205f, 0.260f, 0.024f), tealClothMaterial, Quaternion.identity);
        CreatePanel(root, "TealTunicFrontFacet", new[]
        {
            new Vector3(-0.130f, 1.245f, 0.238f),
            new Vector3(0.130f, 1.245f, 0.238f),
            new Vector3(0.100f, 0.980f, 0.246f),
            new Vector3(-0.100f, 0.980f, 0.246f)
        }, tealClothMaterial);
        CreateLowPolyBox(root, "LeftUpperWrapGoldSeam", new Vector3(-0.095f, 1.105f, 0.240f), new Vector3(0.014f, 0.235f, 0.014f), goldMaterial, Quaternion.Euler(0f, 0f, -10f));
        CreateLowPolyBox(root, "RightUpperWrapGoldSeam", new Vector3(0.095f, 1.105f, 0.240f), new Vector3(0.014f, 0.235f, 0.014f), goldMaterial, Quaternion.Euler(0f, 0f, 10f));
        CreatePanel(root, "HighCollarLeft", new[]
        {
            new Vector3(-0.075f, 1.155f, 0.120f),
            new Vector3(-0.150f, 1.210f, 0.010f),
            new Vector3(-0.130f, 1.260f, -0.045f),
            new Vector3(-0.060f, 1.230f, 0.055f)
        }, tealClothMaterial);
        CreatePanel(root, "HighCollarRight", new[]
        {
            new Vector3(0.075f, 1.155f, 0.120f),
            new Vector3(0.150f, 1.210f, 0.010f),
            new Vector3(0.130f, 1.260f, -0.045f),
            new Vector3(0.060f, 1.230f, 0.055f)
        }, tealClothMaterial);
        CreateLowPolyBox(root, "LeftShoulderIvoryCap", new Vector3(-0.275f, 1.220f, 0.045f), new Vector3(0.078f, 0.040f, 0.125f), whiteClothMaterial, Quaternion.Euler(0f, 0f, -13f));
        CreateLowPolyBox(root, "RightShoulderIvoryCap", new Vector3(0.275f, 1.220f, 0.045f), new Vector3(0.078f, 0.040f, 0.125f), whiteClothMaterial, Quaternion.Euler(0f, 0f, 13f));
        CreateLowPolyBox(root, "LeftShoulderGoldPin", new Vector3(-0.275f, 1.240f, 0.145f), new Vector3(0.034f, 0.034f, 0.018f), goldMaterial, Quaternion.Euler(0f, 0f, 45f));
        CreateLowPolyBox(root, "RightShoulderGoldPin", new Vector3(0.275f, 1.240f, 0.145f), new Vector3(0.034f, 0.034f, 0.018f), goldMaterial, Quaternion.Euler(0f, 0f, 45f));

        CreatePanel(root, "IvoryUpperBackPanel", new[]
        {
            new Vector3(-0.205f, 1.215f, -0.190f),
            new Vector3(0.205f, 1.215f, -0.190f),
            new Vector3(0.195f, 0.990f, -0.200f),
            new Vector3(-0.195f, 0.990f, -0.200f)
        }, whiteShadowMaterial);
    }

    private void CreateSeparatedWaist(Transform root)
    {
        CreateLowPolyBox(root, "WaistBeltFront", new Vector3(0f, 0.855f, 0.185f), new Vector3(0.440f, 0.060f, 0.036f), beltMaterial, Quaternion.identity);
        CreateLowPolyBox(root, "WaistBeltBack", new Vector3(0f, 0.855f, -0.185f), new Vector3(0.440f, 0.060f, 0.036f), beltMaterial, Quaternion.identity);
        CreateLowPolyBox(root, "WaistBeltLeft", new Vector3(-0.245f, 0.855f, 0f), new Vector3(0.034f, 0.060f, 0.305f), beltMaterial, Quaternion.identity);
        CreateLowPolyBox(root, "WaistBeltRight", new Vector3(0.245f, 0.855f, 0f), new Vector3(0.034f, 0.060f, 0.305f), beltMaterial, Quaternion.identity);
        CreateLowPolyBox(root, "FrontBuckle", new Vector3(0f, 0.865f, 0.218f), new Vector3(0.090f, 0.090f, 0.020f), goldMaterial, Quaternion.Euler(0f, 0f, 45f));
    }

    private void CreateSeparatedLower(Transform root)
    {
        CreateLowPolyBox(root, "LowerShortsFront", new Vector3(0f, 0.735f, 0.190f), new Vector3(0.340f, 0.125f, 0.040f), tealClothMaterial, Quaternion.identity);
        CreateLowPolyBox(root, "LowerShortsBack", new Vector3(0f, 0.735f, -0.190f), new Vector3(0.340f, 0.125f, 0.040f), tealClothMaterial, Quaternion.identity);
        CreateLowPolyBox(root, "LeftLowerShortsSide", new Vector3(-0.190f, 0.730f, 0f), new Vector3(0.040f, 0.125f, 0.275f), tealClothMaterial, Quaternion.identity);
        CreateLowPolyBox(root, "RightLowerShortsSide", new Vector3(0.190f, 0.730f, 0f), new Vector3(0.040f, 0.125f, 0.275f), tealClothMaterial, Quaternion.identity);
        CreateLowPolyBox(root, "LowerWaistGoldTrimFront", new Vector3(0f, 0.805f, 0.218f), new Vector3(0.365f, 0.020f, 0.020f), goldMaterial, Quaternion.identity);
        CreateLowPolyBox(root, "LowerWaistGoldTrimBack", new Vector3(0f, 0.805f, -0.218f), new Vector3(0.365f, 0.020f, 0.020f), goldMaterial, Quaternion.identity);
        CreateLowPolyBox(root, "LeftSeparatedShortsLeg", new Vector3(-0.105f, 0.660f, 0.055f), new Vector3(0.130f, 0.072f, 0.190f), tealClothMaterial, Quaternion.Euler(0f, 0f, -3f));
        CreateLowPolyBox(root, "RightSeparatedShortsLeg", new Vector3(0.105f, 0.660f, 0.055f), new Vector3(0.130f, 0.072f, 0.190f), tealClothMaterial, Quaternion.Euler(0f, 0f, 3f));
        CreateLowPolyPrism(root, "LowerShortsUnifiedShell", new Vector3(0f, 0.725f, 0.005f), 0.170f, 0.320f, 0.250f, 0.250f, tealClothMaterial);
    }

    private void CreateCapelet(Transform root)
    {
        CreatePanel(root, "FrontCapeletLeft", new[]
        {
            new Vector3(-0.095f, 1.285f, 0.175f),
            new Vector3(-0.315f, 1.220f, 0.105f),
            new Vector3(-0.255f, 1.135f, 0.140f),
            new Vector3(-0.105f, 1.175f, 0.218f)
        }, whiteClothMaterial);

        CreatePanel(root, "FrontCapeletRight", new[]
        {
            new Vector3(0.095f, 1.285f, 0.175f),
            new Vector3(0.315f, 1.220f, 0.105f),
            new Vector3(0.255f, 1.135f, 0.140f),
            new Vector3(0.105f, 1.175f, 0.218f)
        }, whiteClothMaterial);

        CreatePanel(root, "FrontCapeletLeftGoldTrim", new[]
        {
            new Vector3(-0.252f, 1.132f, 0.153f),
            new Vector3(-0.105f, 1.166f, 0.228f),
            new Vector3(-0.096f, 1.148f, 0.230f),
            new Vector3(-0.252f, 1.114f, 0.166f)
        }, goldMaterial);

        CreatePanel(root, "FrontCapeletRightGoldTrim", new[]
        {
            new Vector3(0.105f, 1.166f, 0.228f),
            new Vector3(0.252f, 1.132f, 0.153f),
            new Vector3(0.252f, 1.114f, 0.166f),
            new Vector3(0.096f, 1.148f, 0.230f)
        }, goldMaterial);

        CreatePanel(root, "BackCapeletLeft", new[]
        {
            new Vector3(-0.03f, 1.275f, -0.145f),
            new Vector3(-0.330f, 1.175f, -0.115f),
            new Vector3(-0.255f, 1.020f, -0.175f),
            new Vector3(-0.055f, 1.080f, -0.225f)
        }, whiteClothMaterial);

        CreatePanel(root, "BackCapeletRight", new[]
        {
            new Vector3(0.03f, 1.275f, -0.145f),
            new Vector3(0.330f, 1.175f, -0.115f),
            new Vector3(0.255f, 1.020f, -0.175f),
            new Vector3(0.055f, 1.080f, -0.225f)
        }, whiteClothMaterial);

        CreatePanel(root, "LeftCapeTail", new[]
        {
            new Vector3(-0.235f, 1.00f, -0.225f),
            new Vector3(-0.095f, 0.98f, -0.255f),
            new Vector3(-0.115f, 0.44f, -0.365f),
            new Vector3(-0.330f, 0.54f, -0.295f)
        }, tealClothMaterial);

        CreatePanel(root, "RightCapeTail", new[]
        {
            new Vector3(0.095f, 0.98f, -0.255f),
            new Vector3(0.235f, 1.00f, -0.225f),
            new Vector3(0.330f, 0.54f, -0.295f),
            new Vector3(0.115f, 0.44f, -0.365f)
        }, tealClothMaterial);
    }

    private void CreateSkirtPanels(Transform root)
    {
        CreatePanel(root, "LongLeftSkirtPanel", new[]
        {
            new Vector3(-0.235f, 0.835f, 0.195f),
            new Vector3(0.070f, 0.835f, 0.215f),
            new Vector3(-0.035f, 0.530f, 0.225f),
            new Vector3(-0.340f, 0.090f, 0.190f)
        }, tealClothMaterial);
        CreateLowPolyBox(root, "LongLeftSkirtPanelFold", new Vector3(-0.165f, 0.505f, 0.205f), new Vector3(0.020f, 0.540f, 0.026f), tealClothMaterial, Quaternion.Euler(0f, 0f, -24f));

        CreatePanel(root, "IvoryFrontSkirtPanel", new[]
        {
            new Vector3(-0.055f, 0.835f, 0.228f),
            new Vector3(0.240f, 0.825f, 0.205f),
            new Vector3(0.185f, 0.490f, 0.190f),
            new Vector3(-0.160f, 0.390f, 0.225f)
        }, whiteClothMaterial);
        CreateLowPolyBox(root, "IvoryFrontSkirtFold", new Vector3(0.055f, 0.615f, 0.218f), new Vector3(0.018f, 0.365f, 0.024f), whiteShadowMaterial, Quaternion.Euler(0f, 0f, 16f));

        CreatePanel(root, "BackAsymSkirtPanel", new[]
        {
            new Vector3(-0.230f, 0.825f, -0.210f),
            new Vector3(0.250f, 0.825f, -0.195f),
            new Vector3(0.190f, 0.430f, -0.215f),
            new Vector3(-0.315f, 0.220f, -0.230f)
        }, whiteClothMaterial);

        CreatePanel(root, "RightRearTealPanel", new[]
        {
            new Vector3(0.175f, 0.815f, -0.095f),
            new Vector3(0.315f, 0.795f, -0.050f),
            new Vector3(0.360f, 0.190f, -0.070f),
            new Vector3(0.205f, 0.400f, -0.125f)
        }, tealClothMaterial);

        CreatePanel(root, "GoldHemLongLeft", new[]
        {
            new Vector3(-0.340f, 0.090f, 0.205f),
            new Vector3(-0.030f, 0.520f, 0.238f),
            new Vector3(-0.022f, 0.495f, 0.248f),
            new Vector3(-0.330f, 0.065f, 0.215f)
        }, goldMaterial);

        CreatePanel(root, "LeftAirySideSash", new[]
        {
            new Vector3(-0.275f, 0.805f, 0.040f),
            new Vector3(-0.185f, 0.765f, 0.095f),
            new Vector3(-0.225f, 0.250f, 0.060f),
            new Vector3(-0.410f, 0.080f, -0.020f)
        }, whiteClothMaterial);

        CreatePanel(root, "RightAirySideSash", new[]
        {
            new Vector3(0.185f, 0.765f, 0.095f),
            new Vector3(0.275f, 0.805f, 0.040f),
            new Vector3(0.410f, 0.080f, -0.020f),
            new Vector3(0.225f, 0.250f, 0.060f)
        }, whiteClothMaterial);

        CreatePanel(root, "BackTealWindPanel", new[]
        {
            new Vector3(-0.080f, 0.805f, -0.245f),
            new Vector3(0.130f, 0.805f, -0.245f),
            new Vector3(0.185f, 0.050f, -0.345f),
            new Vector3(-0.140f, 0.200f, -0.345f)
        }, tealClothMaterial);
    }

    private void BuildHair(Transform root)
    {
        CreateLowPolySphere(root, "HairCap", new Vector3(0f, 1.68f, -0.065f), new Vector3(0.225f, 0.205f, 0.165f), hairMaterial, 8, 4);
        CreatePanel(root, "FrontBang", new[]
        {
            new Vector3(-0.225f, 1.750f, 0.190f),
            new Vector3(0.010f, 1.725f, 0.205f),
            new Vector3(-0.095f, 1.645f, 0.214f),
            new Vector3(-0.275f, 1.600f, 0.188f)
        }, hairMaterial);

        CreatePanel(root, "FrontBangRightShard", new[]
        {
            new Vector3(0.010f, 1.725f, 0.205f),
            new Vector3(0.205f, 1.690f, 0.190f),
            new Vector3(0.235f, 1.585f, 0.182f),
            new Vector3(0.065f, 1.640f, 0.214f)
        }, hairMaterial);

        CreatePanel(root, "BackHairCenter", new[]
        {
            new Vector3(-0.125f, 1.55f, -0.220f),
            new Vector3(0.130f, 1.55f, -0.220f),
            new Vector3(0.100f, 0.45f, -0.340f),
            new Vector3(-0.110f, 0.40f, -0.350f)
        }, hairMaterial);
        CreatePanel(root, "BackHairCenterRidge", new[]
        {
            new Vector3(-0.018f, 1.400f, -0.330f),
            new Vector3(0.040f, 1.385f, -0.330f),
            new Vector3(0.025f, 0.520f, -0.365f),
            new Vector3(-0.028f, 0.540f, -0.365f)
        }, hairMaterial);

        CreatePanel(root, "BackHairLeft", new[]
        {
            new Vector3(-0.220f, 1.48f, -0.160f),
            new Vector3(-0.075f, 1.50f, -0.245f),
            new Vector3(-0.205f, 0.43f, -0.395f),
            new Vector3(-0.350f, 0.69f, -0.275f)
        }, hairMaterial);

        CreatePanel(root, "BackHairRight", new[]
        {
            new Vector3(0.075f, 1.50f, -0.245f),
            new Vector3(0.220f, 1.48f, -0.160f),
            new Vector3(0.350f, 0.69f, -0.275f),
            new Vector3(0.205f, 0.43f, -0.395f)
        }, hairMaterial);

        CreatePanel(root, "SideHairLeft", new[]
        {
            new Vector3(-0.195f, 1.520f, 0.020f),
            new Vector3(-0.265f, 1.420f, -0.120f),
            new Vector3(-0.215f, 0.920f, -0.120f),
            new Vector3(-0.125f, 1.000f, 0.020f)
        }, hairDarkMaterial);

        CreatePanel(root, "SideHairRight", new[]
        {
            new Vector3(0.265f, 1.420f, -0.120f),
            new Vector3(0.195f, 1.520f, 0.020f),
            new Vector3(0.125f, 1.000f, 0.020f),
            new Vector3(0.215f, 0.920f, -0.120f)
        }, hairDarkMaterial);

        CreatePanel(root, "FaceLockLeft", new[]
        {
            new Vector3(-0.185f, 1.525f, 0.140f),
            new Vector3(-0.275f, 1.430f, 0.060f),
            new Vector3(-0.215f, 1.005f, 0.038f),
            new Vector3(-0.112f, 1.165f, 0.150f)
        }, hairMaterial);

        CreatePanel(root, "FaceLockRight", new[]
        {
            new Vector3(0.275f, 1.430f, 0.060f),
            new Vector3(0.185f, 1.525f, 0.140f),
            new Vector3(0.112f, 1.165f, 0.150f),
            new Vector3(0.215f, 1.005f, 0.038f)
        }, hairMaterial);

        CreatePanel(root, "BackHairOuterLeft", new[]
        {
            new Vector3(-0.270f, 1.42f, -0.165f),
            new Vector3(-0.170f, 1.47f, -0.275f),
            new Vector3(-0.300f, 0.30f, -0.420f),
            new Vector3(-0.420f, 0.62f, -0.300f)
        }, hairDarkMaterial);

        CreatePanel(root, "BackHairOuterRight", new[]
        {
            new Vector3(0.170f, 1.47f, -0.275f),
            new Vector3(0.270f, 1.42f, -0.165f),
            new Vector3(0.420f, 0.62f, -0.300f),
            new Vector3(0.300f, 0.30f, -0.420f)
        }, hairDarkMaterial);
        CreatePanel(root, "LeftBackHairInnerFacet", new[]
        {
            new Vector3(-0.170f, 1.410f, -0.290f),
            new Vector3(-0.065f, 1.455f, -0.320f),
            new Vector3(-0.120f, 0.430f, -0.405f),
            new Vector3(-0.240f, 0.620f, -0.365f)
        }, hairMaterial);

        CreatePanel(root, "RightBackHairInnerFacet", new[]
        {
            new Vector3(0.065f, 1.455f, -0.320f),
            new Vector3(0.170f, 1.410f, -0.290f),
            new Vector3(0.240f, 0.620f, -0.365f),
            new Vector3(0.120f, 0.430f, -0.405f)
        }, hairMaterial);

        CreatePanel(root, "BackHairLowerTip", new[]
        {
            new Vector3(-0.13f, 0.48f, -0.36f),
            new Vector3(0.12f, 0.48f, -0.36f),
            new Vector3(0.05f, 0.12f, -0.31f),
            new Vector3(-0.07f, 0.10f, -0.31f)
        }, hairDarkMaterial);

        CreatePanel(root, "HairHighlightFrontSlash", new[]
        {
            new Vector3(-0.08f, 1.735f, 0.222f),
            new Vector3(0.02f, 1.705f, 0.228f),
            new Vector3(-0.08f, 1.610f, 0.230f),
            new Vector3(-0.15f, 1.630f, 0.220f)
        }, hairHighlightMaterial);

        CreatePanel(root, "HairHighlightBackRibbon", new[]
        {
            new Vector3(-0.018f, 1.365f, -0.345f),
            new Vector3(0.040f, 1.350f, -0.345f),
            new Vector3(0.025f, 0.690f, -0.378f),
            new Vector3(-0.025f, 0.705f, -0.378f)
        }, hairHighlightMaterial);
    }

    private void BuildAccessories(Transform root)
    {
        CreateLowPolyBox(root, "ChestClasp", new Vector3(0f, 1.185f, 0.238f), new Vector3(0.092f, 0.092f, 0.030f), goldMaterial, Quaternion.Euler(0f, 0f, 45f));
        CreateLowPolyBox(root, "RightHipPouch", new Vector3(0.335f, 0.750f, 0.050f), new Vector3(0.110f, 0.160f, 0.080f), beltMaterial, Quaternion.Euler(0f, 8f, 0f));
        CreateTaperedCylinder(root, "BubbleCharmStrap", new Vector3(0.365f, 0.680f, 0.100f), new Vector3(0.390f, 0.540f, 0.120f), 0.010f, 0.010f, goldMaterial, 6);
        CreateLowPolySphere(root, "BubbleCharm", new Vector3(0.405f, 0.495f, 0.140f), new Vector3(0.060f, 0.060f, 0.060f), bubbleMaterial, 8, 4);
    }

    private void ValidateGeneratedModel(Transform root, List<string> validationIssues)
    {
        string[] requiredParts =
        {
            "Head", "FaceSoftPlane", "FaceFrontPanel", "LeftEar", "RightEar", "TorsoCore", "Hips",
            "LeftUpperArm", "RightUpperArm", "LeftForearm", "RightForearm",
            "LeftBracer", "RightBracer", "LeftHand", "RightHand",
            "LeftThigh", "RightThigh", "LeftShin", "RightShin",
            "LeftStreamlinedShoe", "RightStreamlinedShoe",
            "LeftShoeMetalAnkleCuff", "RightShoeMetalAnkleCuff",
            "LeftShoeOuterFlowFin", "RightShoeOuterFlowFin",
            "LeftShoeRearWing", "RightShoeRearWing",
            "LeftShoeForwardBlade", "RightShoeForwardBlade",
            "LeftShoeTealFlowLine", "RightShoeTealFlowLine",
            "WaistBeltFront", "WaistBeltBack", "WaistBeltLeft", "WaistBeltRight",
            "UpperHemGoldFront", "TealTunicFrontFacet",
            "LeftUpperWrapGoldSeam", "RightUpperWrapGoldSeam",
            "UpperJacketLeftThickness", "UpperJacketRightThickness",
            "LowerShortsFront", "LowerShortsBack",
            "LowerShortsUnifiedShell", "LeftSeparatedShortsLeg", "RightSeparatedShortsLeg",
            "FrontCapeletLeft", "FrontCapeletRight", "BackCapeletLeft", "BackCapeletRight",
            "FrontCapeletLeftGoldTrim", "FrontCapeletRightGoldTrim",
            "LongLeftSkirtPanel", "IvoryFrontSkirtPanel", "BackAsymSkirtPanel", "RightRearTealPanel",
            "HairCap", "BackHairCenter", "BackHairLeft", "BackHairRight",
            "BackHairCenterRidge", "BackHairOuterLeft", "BackHairOuterRight",
            "LeftBackHairInnerFacet", "RightBackHairInnerFacet", "FaceLockLeft", "FaceLockRight",
            "BackHairLowerTip", "RightHipPouch", "BubbleCharm"
        };

        foreach (string partName in requiredParts)
        {
            if (root.Find(partName) == null)
            {
                validationIssues.Add($"Missing part: {partName}");
                Debug.LogWarning($"Heroine model missing generated part: {partName}", this);
            }
        }

        ValidateMirrorPairs(root, validationIssues);
    }

    private void ValidateMirrorPairs(Transform root, List<string> validationIssues)
    {
        const float tolerance = 0.055f;
        ValidateMirrorPair(root, "LeftUpperArm", "RightUpperArm", tolerance, validationIssues);
        ValidateMirrorPair(root, "LeftEar", "RightEar", tolerance, validationIssues);
        ValidateMirrorPair(root, "LeftForearm", "RightForearm", tolerance, validationIssues);
        ValidateMirrorPair(root, "LeftBracer", "RightBracer", tolerance, validationIssues);
        ValidateMirrorPair(root, "LeftHand", "RightHand", tolerance, validationIssues);
        ValidateMirrorPair(root, "LeftThigh", "RightThigh", tolerance, validationIssues);
        ValidateMirrorPair(root, "LeftShin", "RightShin", tolerance, validationIssues);
        ValidateMirrorPair(root, "LeftStreamlinedShoe", "RightStreamlinedShoe", tolerance, validationIssues);
        ValidateMirrorPair(root, "LeftShoeMetalAnkleCuff", "RightShoeMetalAnkleCuff", tolerance, validationIssues);
        ValidateMirrorPair(root, "LeftShoeOuterFlowFin", "RightShoeOuterFlowFin", tolerance, validationIssues);
        ValidateMirrorPair(root, "LeftShoeRearWing", "RightShoeRearWing", tolerance, validationIssues);
        ValidateMirrorPair(root, "LeftShoeForwardBlade", "RightShoeForwardBlade", tolerance, validationIssues);
        ValidateMirrorPair(root, "LeftShoeTealFlowLine", "RightShoeTealFlowLine", tolerance, validationIssues);
        ValidateMirrorPair(root, "LeftShoulderIvoryCap", "RightShoulderIvoryCap", tolerance, validationIssues);
        ValidateMirrorPair(root, "LeftShoulderGoldPin", "RightShoulderGoldPin", tolerance, validationIssues);
        ValidateMirrorPair(root, "LeftUpperWrapGoldSeam", "RightUpperWrapGoldSeam", tolerance, validationIssues);
        ValidateMirrorPair(root, "UpperJacketLeftThickness", "UpperJacketRightThickness", tolerance, validationIssues);
        ValidateMirrorPair(root, "LeftSeparatedShortsLeg", "RightSeparatedShortsLeg", tolerance, validationIssues);
        ValidateMirrorPair(root, "FrontCapeletLeft", "FrontCapeletRight", tolerance, validationIssues);
        ValidateMirrorPair(root, "FrontCapeletLeftGoldTrim", "FrontCapeletRightGoldTrim", tolerance, validationIssues);
        ValidateMirrorPair(root, "BackCapeletLeft", "BackCapeletRight", tolerance, validationIssues);
        ValidateMirrorPair(root, "LeftCapeTail", "RightCapeTail", tolerance, validationIssues);
        ValidateMirrorPair(root, "SideHairLeft", "SideHairRight", tolerance, validationIssues);
        ValidateMirrorPair(root, "FaceLockLeft", "FaceLockRight", tolerance, validationIssues);
        ValidateMirrorPair(root, "BackHairLeft", "BackHairRight", tolerance, validationIssues);
        ValidateMirrorPair(root, "BackHairOuterLeft", "BackHairOuterRight", tolerance, validationIssues);
        ValidateMirrorPair(root, "LeftBackHairInnerFacet", "RightBackHairInnerFacet", tolerance, validationIssues);
    }

    private void ValidateMirrorPair(Transform root, string leftName, string rightName, float tolerance, List<string> validationIssues)
    {
        if (!TryGetRootSpaceBounds(root, leftName, out Bounds leftBounds) ||
            !TryGetRootSpaceBounds(root, rightName, out Bounds rightBounds))
            return;

        Vector3 leftCenter = leftBounds.center;
        Vector3 rightCenter = rightBounds.center;
        Vector3 leftSize = leftBounds.size;
        Vector3 rightSize = rightBounds.size;

        bool centerMatches =
            Mathf.Abs(leftCenter.x + rightCenter.x) <= tolerance &&
            Mathf.Abs(leftCenter.y - rightCenter.y) <= tolerance &&
            Mathf.Abs(leftCenter.z - rightCenter.z) <= tolerance;

        bool sizeMatches =
            Mathf.Abs(leftSize.x - rightSize.x) <= tolerance &&
            Mathf.Abs(leftSize.y - rightSize.y) <= tolerance &&
            Mathf.Abs(leftSize.z - rightSize.z) <= tolerance;

        if (!centerMatches || !sizeMatches)
        {
            validationIssues.Add($"Mirror mismatch: {leftName} / {rightName}");
            Debug.LogWarning($"Heroine model mirror check failed: {leftName} and {rightName}", this);
        }
    }

    private bool TryGetRootSpaceBounds(Transform root, string partName, out Bounds rootSpaceBounds)
    {
        Transform part = root.Find(partName);
        if (part == null)
        {
            rootSpaceBounds = default;
            return false;
        }

        MeshFilter filter = part.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null)
        {
            rootSpaceBounds = default;
            return false;
        }

        Matrix4x4 rootSpaceMatrix = root.worldToLocalMatrix * part.localToWorldMatrix;
        rootSpaceBounds = TransformBounds(rootSpaceMatrix, filter.sharedMesh.bounds);
        return true;
    }

    private void UpdateDiagnostics(Transform root, List<string> validationIssues)
    {
        if (!TryCalculateRootSpaceBounds(root, out Bounds bounds, out int partCount, out int vertexCount, out int triangleCount))
        {
            generatedPartCount = 0;
            generatedVertexCount = 0;
            generatedTriangleCount = 0;
            generatedLocalBounds = default;
            lastValidationSummary = "No generated mesh parts found.";
            return;
        }

        generatedPartCount = partCount;
        generatedVertexCount = vertexCount;
        generatedTriangleCount = triangleCount;
        generatedLocalBounds = bounds;

        if (validationIssues.Count == 0)
        {
            lastValidationSummary = $"OK: {partCount} parts, {vertexCount} vertices, {triangleCount} triangles. Local bounds size {FormatVector(bounds.size)}.";
            return;
        }

        int issueCountToShow = Mathf.Min(validationIssues.Count, 4);
        lastValidationSummary = $"Issues: {validationIssues.Count}. " + string.Join("; ", validationIssues.GetRange(0, issueCountToShow));
    }

    private bool TryCalculateRootSpaceBounds(Transform root, out Bounds rootSpaceBounds, out int partCount, out int vertexCount, out int triangleCount)
    {
        MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
        partCount = 0;
        vertexCount = 0;
        triangleCount = 0;
        rootSpaceBounds = default;
        bool hasBounds = false;

        foreach (MeshFilter filter in filters)
        {
            if (filter.sharedMesh == null)
                continue;

            Matrix4x4 rootSpaceMatrix = root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            Bounds meshBounds = TransformBounds(rootSpaceMatrix, filter.sharedMesh.bounds);
            if (!hasBounds)
            {
                rootSpaceBounds = meshBounds;
                hasBounds = true;
            }
            else
            {
                rootSpaceBounds.Encapsulate(meshBounds);
            }

            partCount++;
            vertexCount += filter.sharedMesh.vertexCount;
            triangleCount += filter.sharedMesh.triangles.Length / 3;
        }

        return hasBounds;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawModelBounds)
            return;

        Transform root = GetActiveVisualRoot();
        if (root == null)
            return;

        Bounds bounds = generatedLocalBounds;
        if (bounds.size == Vector3.zero && !TryCalculateRootSpaceBounds(root, out bounds, out _, out _, out _))
            return;

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;

        Gizmos.matrix = root.localToWorldMatrix;
        Gizmos.color = new Color(0.1f, 0.85f, 1f, 0.85f);
        Gizmos.DrawWireCube(bounds.center, bounds.size);

        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(1f, 0.72f, 0.16f, 0.9f);
        Gizmos.DrawLine(new Vector3(-0.42f, localOffset.y + 0.02f, 0f), new Vector3(0.42f, localOffset.y + 0.02f, 0f));
        Gizmos.DrawWireSphere(new Vector3(0f, localOffset.y + 0.62f, 0f), 0.06f);

        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }

    private static Bounds TransformBounds(Matrix4x4 matrix, Bounds sourceBounds)
    {
        Vector3 min = sourceBounds.min;
        Vector3 max = sourceBounds.max;

        Vector3 firstCorner = matrix.MultiplyPoint3x4(new Vector3(min.x, min.y, min.z));
        Bounds transformedBounds = new Bounds(firstCorner, Vector3.zero);
        transformedBounds.Encapsulate(matrix.MultiplyPoint3x4(new Vector3(max.x, min.y, min.z)));
        transformedBounds.Encapsulate(matrix.MultiplyPoint3x4(new Vector3(min.x, max.y, min.z)));
        transformedBounds.Encapsulate(matrix.MultiplyPoint3x4(new Vector3(max.x, max.y, min.z)));
        transformedBounds.Encapsulate(matrix.MultiplyPoint3x4(new Vector3(min.x, min.y, max.z)));
        transformedBounds.Encapsulate(matrix.MultiplyPoint3x4(new Vector3(max.x, min.y, max.z)));
        transformedBounds.Encapsulate(matrix.MultiplyPoint3x4(new Vector3(min.x, max.y, max.z)));
        transformedBounds.Encapsulate(matrix.MultiplyPoint3x4(new Vector3(max.x, max.y, max.z)));
        return transformedBounds;
    }

    private static string FormatVector(Vector3 value)
    {
        return $"({value.x:0.00}, {value.y:0.00}, {value.z:0.00})";
    }

    private GameObject CreateMeshObject(Transform root, string objectName, Mesh mesh, Material material, Vector3 localPosition, Quaternion localRotation)
    {
        GameObject child = new GameObject(objectName);
        ApplyGeneratedObjectFlags(child);
        child.transform.SetParent(root, false);
        child.transform.localPosition = localPosition;
        child.transform.localRotation = localRotation;
        child.transform.localScale = Vector3.one;

        MeshFilter filter = child.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;

        MeshRenderer renderer = child.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        renderer.receiveShadows = true;
        return child;
    }

    private void CreateLowPolyBox(Transform root, string objectName, Vector3 localPosition, Vector3 size, Material material, Quaternion localRotation)
    {
        Mesh mesh = CreateBoxMesh(size);
        CreateMeshObject(root, objectName, mesh, material, localPosition, localRotation);
    }

    private void CreateLowPolySphere(Transform root, string objectName, Vector3 localPosition, Vector3 radius, Material material, int longitudeSegments, int latitudeSegments)
    {
        Mesh mesh = CreateSphereMesh(radius, longitudeSegments, latitudeSegments);
        CreateMeshObject(root, objectName, mesh, material, localPosition, Quaternion.identity);
    }

    private void CreateLowPolyPrism(Transform root, string objectName, Vector3 localPosition, float height, float topWidth, float bottomWidth, float depth, Material material)
    {
        Mesh mesh = CreateTaperedPrismMesh(height, topWidth, bottomWidth, depth);
        CreateMeshObject(root, objectName, mesh, material, localPosition, Quaternion.identity);
    }

    private void CreateTaperedCylinder(Transform root, string objectName, Vector3 start, Vector3 end, float startRadius, float endRadius, Material material, int segments)
    {
        Vector3 direction = end - start;
        float height = direction.magnitude;
        if (height <= 0.001f)
            return;

        Mesh mesh = CreateTaperedCylinderMesh(height, startRadius, endRadius, segments);
        Quaternion rotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);
        CreateMeshObject(root, objectName, mesh, material, (start + end) * 0.5f, rotation);
    }

    private void CreatePanel(Transform root, string objectName, Vector3[] corners, Material material)
    {
        Mesh mesh = new Mesh();
        ApplyGeneratedObjectFlags(mesh);
        mesh.name = objectName + "Mesh";
        mesh.vertices = new[]
        {
            corners[0], corners[1], corners[2], corners[3],
            corners[3], corners[2], corners[1], corners[0]
        };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 4, 5, 6, 4, 6, 7 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        CreateMeshObject(root, objectName, mesh, material, Vector3.zero, Quaternion.identity);
    }

    private Mesh CreateBoxMesh(Vector3 size)
    {
        Vector3 h = size * 0.5f;
        Vector3[] corners =
        {
            new Vector3(-h.x, -h.y, -h.z), new Vector3(h.x, -h.y, -h.z),
            new Vector3(h.x, h.y, -h.z), new Vector3(-h.x, h.y, -h.z),
            new Vector3(-h.x, -h.y, h.z), new Vector3(h.x, -h.y, h.z),
            new Vector3(h.x, h.y, h.z), new Vector3(-h.x, h.y, h.z)
        };

        int[] faceIndices =
        {
            0, 2, 1, 0, 3, 2,
            4, 5, 6, 4, 6, 7,
            0, 4, 7, 0, 7, 3,
            1, 2, 6, 1, 6, 5,
            3, 7, 6, 3, 6, 2,
            0, 1, 5, 0, 5, 4
        };

        return CreateFlatMesh("BoxMesh", corners, faceIndices);
    }

    private Mesh CreateTaperedPrismMesh(float height, float topWidth, float bottomWidth, float depth)
    {
        float top = height * 0.5f;
        float bottom = -height * 0.5f;
        float topHalf = topWidth * 0.5f;
        float bottomHalf = bottomWidth * 0.5f;
        float front = depth * 0.5f;
        float back = -depth * 0.5f;

        Vector3[] vertices =
        {
            new Vector3(-topHalf, top, front),
            new Vector3(topHalf, top, front),
            new Vector3(bottomHalf, bottom, front),
            new Vector3(-bottomHalf, bottom, front),
            new Vector3(-topHalf, top, back),
            new Vector3(topHalf, top, back),
            new Vector3(bottomHalf, bottom, back),
            new Vector3(-bottomHalf, bottom, back)
        };

        int[] triangles =
        {
            0, 1, 2, 0, 2, 3,
            5, 4, 7, 5, 7, 6,
            4, 0, 3, 4, 3, 7,
            1, 5, 6, 1, 6, 2,
            4, 5, 1, 4, 1, 0,
            3, 2, 6, 3, 6, 7
        };

        return CreateFlatMesh("TaperedPrismMesh", vertices, triangles);
    }

    private Mesh CreateSphereMesh(Vector3 radius, int longitudeSegments, int latitudeSegments)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        for (int lat = 0; lat <= latitudeSegments; lat++)
        {
            float v = lat / (float)latitudeSegments;
            float phi = Mathf.PI * v;
            float y = Mathf.Cos(phi);
            float ring = Mathf.Sin(phi);

            for (int lon = 0; lon < longitudeSegments; lon++)
            {
                float u = lon / (float)longitudeSegments;
                float theta = Mathf.PI * 2f * u;
                vertices.Add(new Vector3(
                    Mathf.Cos(theta) * ring * radius.x,
                    y * radius.y,
                    Mathf.Sin(theta) * ring * radius.z));
            }
        }

        for (int lat = 0; lat < latitudeSegments; lat++)
        {
            for (int lon = 0; lon < longitudeSegments; lon++)
            {
                int current = lat * longitudeSegments + lon;
                int next = lat * longitudeSegments + (lon + 1) % longitudeSegments;
                int above = (lat + 1) * longitudeSegments + lon;
                int aboveNext = (lat + 1) * longitudeSegments + (lon + 1) % longitudeSegments;

                triangles.Add(current);
                triangles.Add(above);
                triangles.Add(next);
                triangles.Add(next);
                triangles.Add(above);
                triangles.Add(aboveNext);
            }
        }

        return CreateFlatMesh("LowPolySphereMesh", vertices.ToArray(), triangles.ToArray());
    }

    private Mesh CreateTaperedCylinderMesh(float height, float bottomRadius, float topRadius, int segments)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        float bottomY = -height * 0.5f;
        float topY = height * 0.5f;

        for (int i = 0; i < segments; i++)
        {
            float angle = Mathf.PI * 2f * i / segments;
            vertices.Add(new Vector3(Mathf.Cos(angle) * bottomRadius, bottomY, Mathf.Sin(angle) * bottomRadius));
        }

        for (int i = 0; i < segments; i++)
        {
            float angle = Mathf.PI * 2f * i / segments;
            vertices.Add(new Vector3(Mathf.Cos(angle) * topRadius, topY, Mathf.Sin(angle) * topRadius));
        }

        vertices.Add(new Vector3(0f, bottomY, 0f));
        vertices.Add(new Vector3(0f, topY, 0f));
        int bottomCenter = segments * 2;
        int topCenter = bottomCenter + 1;

        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            int top = i + segments;
            int nextTop = next + segments;

            triangles.Add(i);
            triangles.Add(top);
            triangles.Add(next);
            triangles.Add(next);
            triangles.Add(top);
            triangles.Add(nextTop);

            triangles.Add(bottomCenter);
            triangles.Add(next);
            triangles.Add(i);

            triangles.Add(topCenter);
            triangles.Add(top);
            triangles.Add(nextTop);
        }

        return CreateFlatMesh("TaperedCylinderMesh", vertices.ToArray(), triangles.ToArray());
    }

    private Mesh CreateFlatMesh(string meshName, Vector3[] sourceVertices, int[] sourceTriangles)
    {
        Vector3[] flatVertices = new Vector3[sourceTriangles.Length];
        int[] flatTriangles = new int[sourceTriangles.Length];

        for (int i = 0; i < sourceTriangles.Length; i++)
        {
            flatVertices[i] = sourceVertices[sourceTriangles[i]];
            flatTriangles[i] = i;
        }

        Mesh mesh = new Mesh();
        ApplyGeneratedObjectFlags(mesh);
        mesh.name = meshName;
        mesh.vertices = flatVertices;
        mesh.triangles = flatTriangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static float SafeInverse(float value)
    {
        return Mathf.Abs(value) < 0.001f ? 1f : 1f / value;
    }

    private static string SideName(float side, string label)
    {
        return (side < 0f ? "Left" : "Right") + label;
    }

    private void ApplyGeneratedObjectFlags(UnityEngine.Object generatedObject)
    {
#if UNITY_EDITOR
        if (buildingPersistentVisual)
            return;

        if (!Application.isPlaying && generatedObject != null)
            generatedObject.hideFlags = HideFlags.DontSaveInEditor;
#endif
    }
}
