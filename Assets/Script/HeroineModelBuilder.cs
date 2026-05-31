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

        report.AppendLine("## Manual Visual Review Still Required");
        report.AppendLine("- Compare the silhouette to the first concept image in the Scene/Game view.");
        report.AppendLine("- Check front, side, and back readability after baking or prefab saving.");
        report.AppendLine("- Playtest movement and jumping to judge hair, cape, and skirt sway strength.");
        report.AppendLine("- Confirm the model does not visually fight the capsule/player collision size.");

        File.WriteAllText(reportPath, report.ToString(), Encoding.UTF8);
        UnityEditor.AssetDatabase.ImportAsset(reportPath);
        Debug.Log($"Exported heroine validation report: {reportPath}", this);
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
            root.localScale = new Vector3(
                SafeInverse(transform.localScale.x),
                SafeInverse(transform.localScale.y),
                SafeInverse(transform.localScale.z)) * modelScale;

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

        if (root.GetComponent<HeroineVisualSway>() == null)
            setupIssues.Add("Heroine visual root is missing HeroineVisualSway.");

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
        skinMaterial = MakeMaterial("Heroine Skin", new Color(0.94f, 0.66f, 0.49f, 1f));
        skinShadowMaterial = MakeMaterial("Heroine Skin Shadow", new Color(0.72f, 0.42f, 0.31f, 1f));
        hairMaterial = MakeMaterial("Heroine Blue Hair", new Color(0.06f, 0.34f, 0.98f, 1f));
        hairDarkMaterial = MakeMaterial("Heroine Dark Blue Hair", new Color(0.03f, 0.14f, 0.52f, 1f));
        hairHighlightMaterial = MakeMaterial("Heroine Clear Blue Hair Highlight", new Color(0.16f, 0.68f, 1.00f, 1f));
        eyeMaterial = MakeMaterial("Heroine Blue Eyes", new Color(0.22f, 0.72f, 1.00f, 1f));
        faceLineMaterial = MakeMaterial("Heroine Face Lines", new Color(0.09f, 0.07f, 0.08f, 1f));
        whiteClothMaterial = MakeMaterial("Heroine Ivory Cloth", new Color(0.94f, 0.89f, 0.78f, 1f));
        whiteShadowMaterial = MakeMaterial("Heroine Ivory Cloth Shadow", new Color(0.78f, 0.73f, 0.62f, 1f));
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
        Shader shader = Shader.Find("Universal Render Pipeline/Simple Lit");
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
        CreateLowPolySphere(root, "Head", new Vector3(0f, 1.55f, 0.02f), new Vector3(0.21f, 0.27f, 0.19f), skinMaterial, 8, 5);
        CreateTaperedCylinder(root, "Neck", new Vector3(0f, 1.27f, 0f), new Vector3(0f, 1.39f, 0f), 0.07f, 0.08f, skinMaterial, 6);
        BuildFace(root);

        CreateLowPolySphere(root, "TorsoCore", new Vector3(0f, 1.05f, 0f), new Vector3(0.34f, 0.42f, 0.20f), tealClothMaterial, 8, 4);
        CreateLowPolySphere(root, "Hips", new Vector3(0f, 0.75f, 0f), new Vector3(0.30f, 0.18f, 0.19f), darkLeggingMaterial, 8, 3);
        CreateLowPolyBox(root, "SlimWaistGap", new Vector3(0f, 0.845f, 0f), new Vector3(0.44f, 0.035f, 0.31f), darkLeggingMaterial, Quaternion.identity);

        BuildArm(root, -1f);
        BuildArm(root, 1f);
        BuildLeg(root, -1f);
        BuildLeg(root, 1f);
    }

    private void BuildFace(Transform root)
    {
        CreateLowPolyBox(root, "LeftEye", new Vector3(-0.078f, 1.585f, 0.232f), new Vector3(0.064f, 0.024f, 0.014f), eyeMaterial, Quaternion.Euler(0f, 0f, -6f));
        CreateLowPolyBox(root, "RightEye", new Vector3(0.078f, 1.585f, 0.232f), new Vector3(0.064f, 0.024f, 0.014f), eyeMaterial, Quaternion.Euler(0f, 0f, 6f));
        CreateLowPolyBox(root, "LeftBrow", new Vector3(-0.082f, 1.635f, 0.236f), new Vector3(0.082f, 0.016f, 0.014f), hairDarkMaterial, Quaternion.Euler(0f, 0f, -14f));
        CreateLowPolyBox(root, "RightBrow", new Vector3(0.082f, 1.635f, 0.236f), new Vector3(0.082f, 0.016f, 0.014f), hairDarkMaterial, Quaternion.Euler(0f, 0f, 14f));
        CreateLowPolyBox(root, "NoseBridge", new Vector3(0f, 1.535f, 0.238f), new Vector3(0.026f, 0.076f, 0.016f), skinShadowMaterial, Quaternion.Euler(0f, 0f, 0f));
        CreateLowPolyBox(root, "Mouth", new Vector3(0f, 1.445f, 0.236f), new Vector3(0.085f, 0.014f, 0.012f), faceLineMaterial, Quaternion.identity);
    }

    private void BuildArm(Transform root, float side)
    {
        Vector3 shoulder = new Vector3(side * 0.32f, 1.20f, 0.01f);
        Vector3 elbow = new Vector3(side * 0.48f, 0.84f, 0.03f);
        Vector3 wrist = new Vector3(side * 0.55f, 0.55f, 0.05f);

        CreateTaperedCylinder(root, SideName(side, "UpperArm"), shoulder, elbow, 0.055f, 0.045f, skinMaterial, 6);
        CreateTaperedCylinder(root, SideName(side, "Forearm"), elbow, wrist, 0.047f, 0.036f, skinMaterial, 6);
        CreateTaperedCylinder(root, SideName(side, "Bracer"), Vector3.Lerp(elbow, wrist, 0.25f), Vector3.Lerp(elbow, wrist, 0.92f), 0.065f, 0.055f, tealClothMaterial, 6);
        CreateLowPolySphere(root, SideName(side, "Hand"), wrist + new Vector3(side * 0.015f, -0.045f, 0.02f), new Vector3(0.045f, 0.075f, 0.035f), skinMaterial, 6, 3);
        CreateTaperedCylinder(root, SideName(side, "WristGoldBand"), Vector3.Lerp(elbow, wrist, 0.86f), Vector3.Lerp(elbow, wrist, 0.95f), 0.061f, 0.058f, goldMaterial, 6);
    }

    private void BuildLeg(Transform root, float side)
    {
        Vector3 hip = new Vector3(side * 0.16f, 0.72f, 0.02f);
        Vector3 knee = new Vector3(side * 0.17f, 0.38f, 0.02f);
        Vector3 ankle = new Vector3(side * 0.17f, 0.12f, 0.02f);

        CreateTaperedCylinder(root, SideName(side, "Thigh"), hip, knee, 0.085f, 0.075f, darkLeggingMaterial, 6);
        CreateTaperedCylinder(root, SideName(side, "Shin"), knee, ankle, 0.073f, 0.052f, darkLeggingMaterial, 6);

        CreateLowPolySphere(root, SideName(side, "StreamlinedShoe"), new Vector3(side * 0.17f, 0.060f, 0.13f), new Vector3(0.092f, 0.052f, 0.235f), shoeMetalMaterial, 8, 3);
        CreateLowPolyBox(root, SideName(side, "ShoeDarkFloatingSole"), new Vector3(side * 0.17f, 0.014f, 0.12f), new Vector3(0.18f, 0.020f, 0.38f), shoeShadowMetalMaterial, Quaternion.identity);
        CreateLowPolyBox(root, SideName(side, "ShoeSilverToeCap"), new Vector3(side * 0.17f, 0.070f, 0.315f), new Vector3(0.145f, 0.046f, 0.120f), shoeBrightMetalMaterial, Quaternion.Euler(-7f, 0f, 0f));
        CreateLowPolyBox(root, SideName(side, "ShoeGoldHeel"), new Vector3(side * 0.17f, 0.065f, -0.080f), new Vector3(0.105f, 0.060f, 0.050f), goldMaterial, Quaternion.identity);
        CreateTaperedCylinder(root, SideName(side, "ShoeMetalAnkleCuff"), new Vector3(side * 0.17f, 0.105f, 0.0f), new Vector3(side * 0.17f, 0.195f, 0.0f), 0.085f, 0.075f, shoeBrightMetalMaterial, 6);
        CreateLowPolyBox(root, SideName(side, "ShoeInstepGoldLine"), new Vector3(side * 0.17f, 0.127f, 0.135f), new Vector3(0.115f, 0.018f, 0.215f), goldMaterial, Quaternion.Euler(-9f, 0f, 0f));
        CreateLowPolyBox(root, SideName(side, "ShoeOuterFlowFin"), new Vector3(side * 0.270f, 0.065f, 0.105f), new Vector3(0.024f, 0.040f, 0.285f), shoeBrightMetalMaterial, Quaternion.Euler(0f, side * -8f, side * 8f));
        CreateLowPolyBox(root, SideName(side, "ShoeInnerDarkInset"), new Vector3(side * 0.095f, 0.062f, 0.115f), new Vector3(0.020f, 0.032f, 0.230f), shoeShadowMetalMaterial, Quaternion.Euler(0f, side * 6f, side * -4f));
        CreateLowPolyBox(root, SideName(side, "ShoeRearWing"), new Vector3(side * 0.17f, 0.098f, -0.120f), new Vector3(0.130f, 0.020f, 0.145f), shoeBrightMetalMaterial, Quaternion.Euler(10f, 0f, 0f));
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
            new Vector3(-0.22f, 1.26f, 0.205f),
            new Vector3(0.04f, 1.25f, 0.215f),
            new Vector3(0.19f, 0.97f, 0.22f),
            new Vector3(-0.27f, 0.98f, 0.22f)
        }, whiteClothMaterial);

        CreatePanel(root, "IvoryWrapRight", new[]
        {
            new Vector3(0.24f, 1.20f, 0.21f),
            new Vector3(-0.05f, 1.16f, 0.22f),
            new Vector3(-0.15f, 0.98f, 0.22f),
            new Vector3(0.25f, 0.97f, 0.22f)
        }, whiteClothMaterial);

        CreateLowPolyBox(root, "MidriffSkinBand", new Vector3(0f, 0.91f, 0.232f), new Vector3(0.29f, 0.090f, 0.020f), skinMaterial, Quaternion.identity);
        CreateLowPolyBox(root, "UpperHemGoldFront", new Vector3(0f, 0.955f, 0.234f), new Vector3(0.46f, 0.028f, 0.024f), goldMaterial, Quaternion.identity);
        CreateLowPolyBox(root, "UpperHemGoldBack", new Vector3(0f, 0.955f, -0.214f), new Vector3(0.42f, 0.028f, 0.024f), goldMaterial, Quaternion.identity);
        CreateLowPolyBox(root, "TealChestInset", new Vector3(0f, 1.115f, 0.232f), new Vector3(0.185f, 0.270f, 0.026f), tealClothMaterial, Quaternion.identity);
        CreatePanel(root, "HighCollarLeft", new[]
        {
            new Vector3(-0.04f, 1.20f, 0.205f),
            new Vector3(-0.18f, 1.28f, 0.155f),
            new Vector3(-0.18f, 1.43f, 0.060f),
            new Vector3(-0.04f, 1.34f, 0.155f)
        }, tealClothMaterial);
        CreatePanel(root, "HighCollarRight", new[]
        {
            new Vector3(0.04f, 1.20f, 0.205f),
            new Vector3(0.18f, 1.28f, 0.155f),
            new Vector3(0.18f, 1.43f, 0.060f),
            new Vector3(0.04f, 1.34f, 0.155f)
        }, tealClothMaterial);
        CreateLowPolyBox(root, "LeftShoulderIvoryCap", new Vector3(-0.315f, 1.220f, 0.040f), new Vector3(0.110f, 0.054f, 0.170f), whiteClothMaterial, Quaternion.Euler(0f, 0f, -12f));
        CreateLowPolyBox(root, "RightShoulderIvoryCap", new Vector3(0.315f, 1.220f, 0.040f), new Vector3(0.110f, 0.054f, 0.170f), whiteClothMaterial, Quaternion.Euler(0f, 0f, 12f));
        CreateLowPolyBox(root, "LeftShoulderGoldPin", new Vector3(-0.315f, 1.245f, 0.175f), new Vector3(0.050f, 0.050f, 0.025f), goldMaterial, Quaternion.Euler(0f, 0f, 45f));
        CreateLowPolyBox(root, "RightShoulderGoldPin", new Vector3(0.315f, 1.245f, 0.175f), new Vector3(0.050f, 0.050f, 0.025f), goldMaterial, Quaternion.Euler(0f, 0f, 45f));

        CreatePanel(root, "IvoryUpperBackPanel", new[]
        {
            new Vector3(-0.24f, 1.23f, -0.205f),
            new Vector3(0.24f, 1.23f, -0.205f),
            new Vector3(0.23f, 0.98f, -0.215f),
            new Vector3(-0.23f, 0.98f, -0.215f)
        }, whiteShadowMaterial);
    }

    private void CreateSeparatedWaist(Transform root)
    {
        CreateLowPolyBox(root, "WaistBeltFront", new Vector3(0f, 0.84f, 0.20f), new Vector3(0.62f, 0.075f, 0.045f), beltMaterial, Quaternion.identity);
        CreateLowPolyBox(root, "WaistBeltBack", new Vector3(0f, 0.84f, -0.20f), new Vector3(0.62f, 0.075f, 0.045f), beltMaterial, Quaternion.identity);
        CreateLowPolyBox(root, "WaistBeltLeft", new Vector3(-0.33f, 0.84f, 0f), new Vector3(0.045f, 0.075f, 0.36f), beltMaterial, Quaternion.identity);
        CreateLowPolyBox(root, "WaistBeltRight", new Vector3(0.33f, 0.84f, 0f), new Vector3(0.045f, 0.075f, 0.36f), beltMaterial, Quaternion.identity);
        CreateLowPolyBox(root, "FrontBuckle", new Vector3(0f, 0.855f, 0.235f), new Vector3(0.12f, 0.12f, 0.025f), goldMaterial, Quaternion.Euler(0f, 0f, 45f));
    }

    private void CreateSeparatedLower(Transform root)
    {
        CreateLowPolyBox(root, "LowerShortsFront", new Vector3(0f, 0.715f, 0.205f), new Vector3(0.46f, 0.155f, 0.050f), tealClothMaterial, Quaternion.identity);
        CreateLowPolyBox(root, "LowerShortsBack", new Vector3(0f, 0.715f, -0.205f), new Vector3(0.46f, 0.155f, 0.050f), tealClothMaterial, Quaternion.identity);
        CreateLowPolyBox(root, "LeftLowerShortsSide", new Vector3(-0.255f, 0.710f, 0f), new Vector3(0.055f, 0.150f, 0.320f), tealClothMaterial, Quaternion.identity);
        CreateLowPolyBox(root, "RightLowerShortsSide", new Vector3(0.255f, 0.710f, 0f), new Vector3(0.055f, 0.150f, 0.320f), tealClothMaterial, Quaternion.identity);
        CreateLowPolyBox(root, "LowerWaistGoldTrimFront", new Vector3(0f, 0.795f, 0.232f), new Vector3(0.50f, 0.026f, 0.025f), goldMaterial, Quaternion.identity);
        CreateLowPolyBox(root, "LowerWaistGoldTrimBack", new Vector3(0f, 0.795f, -0.232f), new Vector3(0.50f, 0.026f, 0.025f), goldMaterial, Quaternion.identity);
        CreateLowPolyBox(root, "LeftSeparatedShortsLeg", new Vector3(-0.135f, 0.625f, 0.060f), new Vector3(0.180f, 0.090f, 0.230f), tealClothMaterial, Quaternion.Euler(0f, 0f, -3f));
        CreateLowPolyBox(root, "RightSeparatedShortsLeg", new Vector3(0.135f, 0.625f, 0.060f), new Vector3(0.180f, 0.090f, 0.230f), tealClothMaterial, Quaternion.Euler(0f, 0f, 3f));
    }

    private void CreateCapelet(Transform root)
    {
        CreatePanel(root, "FrontCapeletLeft", new[]
        {
            new Vector3(-0.05f, 1.31f, 0.16f),
            new Vector3(-0.45f, 1.20f, 0.10f),
            new Vector3(-0.36f, 1.07f, 0.17f),
            new Vector3(-0.08f, 1.13f, 0.22f)
        }, whiteClothMaterial);

        CreatePanel(root, "FrontCapeletRight", new[]
        {
            new Vector3(0.05f, 1.31f, 0.16f),
            new Vector3(0.45f, 1.20f, 0.10f),
            new Vector3(0.36f, 1.07f, 0.17f),
            new Vector3(0.08f, 1.13f, 0.22f)
        }, whiteClothMaterial);

        CreatePanel(root, "BackCapeletLeft", new[]
        {
            new Vector3(-0.03f, 1.29f, -0.15f),
            new Vector3(-0.44f, 1.17f, -0.12f),
            new Vector3(-0.33f, 1.00f, -0.18f),
            new Vector3(-0.06f, 1.08f, -0.24f)
        }, whiteClothMaterial);

        CreatePanel(root, "BackCapeletRight", new[]
        {
            new Vector3(0.03f, 1.29f, -0.15f),
            new Vector3(0.44f, 1.17f, -0.12f),
            new Vector3(0.33f, 1.00f, -0.18f),
            new Vector3(0.06f, 1.08f, -0.24f)
        }, whiteClothMaterial);

        CreatePanel(root, "LeftCapeTail", new[]
        {
            new Vector3(-0.28f, 1.00f, -0.23f),
            new Vector3(-0.10f, 0.98f, -0.26f),
            new Vector3(-0.13f, 0.48f, -0.38f),
            new Vector3(-0.40f, 0.55f, -0.30f)
        }, tealClothMaterial);

        CreatePanel(root, "RightCapeTail", new[]
        {
            new Vector3(0.10f, 0.98f, -0.26f),
            new Vector3(0.28f, 1.00f, -0.23f),
            new Vector3(0.40f, 0.55f, -0.30f),
            new Vector3(0.13f, 0.48f, -0.38f)
        }, tealClothMaterial);
    }

    private void CreateSkirtPanels(Transform root)
    {
        CreatePanel(root, "LongLeftSkirtPanel", new[]
        {
            new Vector3(-0.31f, 0.82f, 0.20f),
            new Vector3(0.12f, 0.82f, 0.22f),
            new Vector3(-0.03f, 0.52f, 0.23f),
            new Vector3(-0.44f, 0.10f, 0.19f)
        }, tealClothMaterial);

        CreatePanel(root, "IvoryFrontSkirtPanel", new[]
        {
            new Vector3(-0.08f, 0.83f, 0.235f),
            new Vector3(0.32f, 0.82f, 0.21f),
            new Vector3(0.24f, 0.48f, 0.19f),
            new Vector3(-0.22f, 0.38f, 0.23f)
        }, whiteClothMaterial);

        CreatePanel(root, "BackAsymSkirtPanel", new[]
        {
            new Vector3(-0.30f, 0.82f, -0.22f),
            new Vector3(0.34f, 0.82f, -0.20f),
            new Vector3(0.25f, 0.42f, -0.22f),
            new Vector3(-0.42f, 0.22f, -0.24f)
        }, whiteClothMaterial);

        CreatePanel(root, "RightRearTealPanel", new[]
        {
            new Vector3(0.22f, 0.81f, -0.10f),
            new Vector3(0.42f, 0.79f, -0.05f),
            new Vector3(0.48f, 0.20f, -0.07f),
            new Vector3(0.25f, 0.40f, -0.13f)
        }, tealClothMaterial);

        CreatePanel(root, "GoldHemLongLeft", new[]
        {
            new Vector3(-0.44f, 0.10f, 0.205f),
            new Vector3(-0.02f, 0.52f, 0.245f),
            new Vector3(-0.01f, 0.49f, 0.255f),
            new Vector3(-0.43f, 0.07f, 0.215f)
        }, goldMaterial);

        CreatePanel(root, "LeftAirySideSash", new[]
        {
            new Vector3(-0.36f, 0.80f, 0.04f),
            new Vector3(-0.24f, 0.76f, 0.10f),
            new Vector3(-0.30f, 0.25f, 0.06f),
            new Vector3(-0.55f, 0.08f, -0.02f)
        }, whiteClothMaterial);

        CreatePanel(root, "RightAirySideSash", new[]
        {
            new Vector3(0.24f, 0.76f, 0.10f),
            new Vector3(0.36f, 0.80f, 0.04f),
            new Vector3(0.55f, 0.08f, -0.02f),
            new Vector3(0.30f, 0.25f, 0.06f)
        }, whiteClothMaterial);

        CreatePanel(root, "BackTealWindPanel", new[]
        {
            new Vector3(-0.10f, 0.80f, -0.255f),
            new Vector3(0.17f, 0.80f, -0.255f),
            new Vector3(0.24f, 0.05f, -0.36f),
            new Vector3(-0.18f, 0.20f, -0.36f)
        }, tealClothMaterial);
    }

    private void BuildHair(Transform root)
    {
        CreateLowPolySphere(root, "HairCap", new Vector3(0f, 1.68f, -0.065f), new Vector3(0.25f, 0.22f, 0.18f), hairMaterial, 8, 4);
        CreatePanel(root, "FrontBang", new[]
        {
            new Vector3(-0.25f, 1.76f, 0.205f),
            new Vector3(0.02f, 1.73f, 0.214f),
            new Vector3(-0.12f, 1.62f, 0.218f),
            new Vector3(-0.34f, 1.56f, 0.196f)
        }, hairMaterial);

        CreatePanel(root, "BackHairCenter", new[]
        {
            new Vector3(-0.14f, 1.55f, -0.22f),
            new Vector3(0.15f, 1.55f, -0.22f),
            new Vector3(0.12f, 0.45f, -0.36f),
            new Vector3(-0.13f, 0.40f, -0.37f)
        }, hairMaterial);

        CreatePanel(root, "BackHairLeft", new[]
        {
            new Vector3(-0.24f, 1.48f, -0.16f),
            new Vector3(-0.08f, 1.50f, -0.25f),
            new Vector3(-0.23f, 0.43f, -0.42f),
            new Vector3(-0.42f, 0.69f, -0.28f)
        }, hairMaterial);

        CreatePanel(root, "BackHairRight", new[]
        {
            new Vector3(0.08f, 1.50f, -0.25f),
            new Vector3(0.24f, 1.48f, -0.16f),
            new Vector3(0.42f, 0.69f, -0.28f),
            new Vector3(0.23f, 0.43f, -0.42f)
        }, hairMaterial);

        CreatePanel(root, "SideHairLeft", new[]
        {
            new Vector3(-0.22f, 1.52f, 0.03f),
            new Vector3(-0.30f, 1.42f, -0.12f),
            new Vector3(-0.24f, 0.92f, -0.12f),
            new Vector3(-0.14f, 1.00f, 0.02f)
        }, hairDarkMaterial);

        CreatePanel(root, "SideHairRight", new[]
        {
            new Vector3(0.30f, 1.42f, -0.12f),
            new Vector3(0.22f, 1.52f, 0.03f),
            new Vector3(0.14f, 1.00f, 0.02f),
            new Vector3(0.24f, 0.92f, -0.12f)
        }, hairDarkMaterial);

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
            new Vector3(-0.03f, 1.48f, -0.365f),
            new Vector3(0.07f, 1.46f, -0.365f),
            new Vector3(0.04f, 0.55f, -0.420f),
            new Vector3(-0.04f, 0.54f, -0.420f)
        }, hairHighlightMaterial);
    }

    private void BuildAccessories(Transform root)
    {
        CreateLowPolyBox(root, "ChestClasp", new Vector3(0f, 1.23f, 0.235f), new Vector3(0.13f, 0.13f, 0.035f), goldMaterial, Quaternion.Euler(0f, 0f, 45f));
        CreateLowPolyBox(root, "RightHipPouch", new Vector3(0.42f, 0.74f, 0.04f), new Vector3(0.16f, 0.20f, 0.10f), beltMaterial, Quaternion.Euler(0f, 8f, 0f));
        CreateTaperedCylinder(root, "BubbleCharmStrap", new Vector3(0.45f, 0.66f, 0.10f), new Vector3(0.47f, 0.53f, 0.12f), 0.012f, 0.012f, goldMaterial, 6);
        CreateLowPolySphere(root, "BubbleCharm", new Vector3(0.48f, 0.48f, 0.14f), new Vector3(0.07f, 0.07f, 0.07f), bubbleMaterial, 8, 4);
    }

    private void ValidateGeneratedModel(Transform root, List<string> validationIssues)
    {
        string[] requiredParts =
        {
            "Head", "TorsoCore", "Hips",
            "LeftUpperArm", "RightUpperArm", "LeftForearm", "RightForearm",
            "LeftBracer", "RightBracer", "LeftHand", "RightHand",
            "LeftThigh", "RightThigh", "LeftShin", "RightShin",
            "LeftStreamlinedShoe", "RightStreamlinedShoe",
            "LeftShoeMetalAnkleCuff", "RightShoeMetalAnkleCuff",
            "LeftShoeOuterFlowFin", "RightShoeOuterFlowFin",
            "LeftShoeRearWing", "RightShoeRearWing",
            "WaistBeltFront", "WaistBeltBack", "WaistBeltLeft", "WaistBeltRight",
            "UpperHemGoldFront", "LowerShortsFront", "LowerShortsBack",
            "LeftSeparatedShortsLeg", "RightSeparatedShortsLeg",
            "FrontCapeletLeft", "FrontCapeletRight", "BackCapeletLeft", "BackCapeletRight",
            "LongLeftSkirtPanel", "IvoryFrontSkirtPanel", "BackAsymSkirtPanel", "RightRearTealPanel",
            "HairCap", "BackHairCenter", "BackHairLeft", "BackHairRight",
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
        ValidateMirrorPair(root, "LeftForearm", "RightForearm", tolerance, validationIssues);
        ValidateMirrorPair(root, "LeftBracer", "RightBracer", tolerance, validationIssues);
        ValidateMirrorPair(root, "LeftHand", "RightHand", tolerance, validationIssues);
        ValidateMirrorPair(root, "LeftThigh", "RightThigh", tolerance, validationIssues);
        ValidateMirrorPair(root, "LeftShin", "RightShin", tolerance, validationIssues);
        ValidateMirrorPair(root, "LeftStreamlinedShoe", "RightStreamlinedShoe", tolerance, validationIssues);
        ValidateMirrorPair(root, "LeftShoeMetalAnkleCuff", "RightShoeMetalAnkleCuff", tolerance, validationIssues);
        ValidateMirrorPair(root, "LeftShoeOuterFlowFin", "RightShoeOuterFlowFin", tolerance, validationIssues);
        ValidateMirrorPair(root, "LeftShoeRearWing", "RightShoeRearWing", tolerance, validationIssues);
        ValidateMirrorPair(root, "LeftShoulderIvoryCap", "RightShoulderIvoryCap", tolerance, validationIssues);
        ValidateMirrorPair(root, "LeftShoulderGoldPin", "RightShoulderGoldPin", tolerance, validationIssues);
        ValidateMirrorPair(root, "LeftSeparatedShortsLeg", "RightSeparatedShortsLeg", tolerance, validationIssues);
        ValidateMirrorPair(root, "FrontCapeletLeft", "FrontCapeletRight", tolerance, validationIssues);
        ValidateMirrorPair(root, "BackCapeletLeft", "BackCapeletRight", tolerance, validationIssues);
        ValidateMirrorPair(root, "LeftCapeTail", "RightCapeTail", tolerance, validationIssues);
        ValidateMirrorPair(root, "SideHairLeft", "SideHairRight", tolerance, validationIssues);
        ValidateMirrorPair(root, "BackHairLeft", "BackHairRight", tolerance, validationIssues);
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
