using UnityEditor;
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
}
