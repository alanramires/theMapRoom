using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AIPlayerController))]
public class AIPlayerControllerEditor : Editor
{
    private SerializedProperty matchControllerProp;
    private SerializedProperty turnStateManagerProp;
    private SerializedProperty aiLogProp;
    private SerializedProperty shoppingProfileProp;

    private bool showInlineProfile = true;

    private void OnEnable()
    {
        matchControllerProp = serializedObject.FindProperty("matchController");
        turnStateManagerProp = serializedObject.FindProperty("turnStateManager");
        aiLogProp = serializedObject.FindProperty("aiLog");
        shoppingProfileProp = serializedObject.FindProperty("shoppingProfile");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(matchControllerProp);
        EditorGUILayout.PropertyField(turnStateManagerProp);
        EditorGUILayout.PropertyField(aiLogProp);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Shopping AI", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(shoppingProfileProp);

        AIShoppingProfile profile = shoppingProfileProp.objectReferenceValue as AIShoppingProfile;
        if (profile == null)
        {
            if (GUILayout.Button("Create New Shopping Profile"))
                CreateAndAssignProfileAsset();

            EditorGUILayout.HelpBox("Assign a Shopping Profile to configure attack/defense groups, percentages and UnitData slots.", MessageType.Info);
            serializedObject.ApplyModifiedProperties();
            return;
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Ping Profile"))
            EditorGUIUtility.PingObject(profile);
        if (GUILayout.Button("Open Profile"))
            Selection.activeObject = profile;
        if (GUILayout.Button("Duplicate Profile"))
            DuplicateAndAssignProfileAsset(profile);
        EditorGUILayout.EndHorizontal();

        showInlineProfile = EditorGUILayout.Foldout(showInlineProfile, "Inline Profile Settings", true);
        if (showInlineProfile)
            DrawInlineProfile(profile);

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawInlineProfile(AIShoppingProfile profile)
    {
        SerializedObject profileSO = new SerializedObject(profile);
        profileSO.Update();

        SerializedProperty profileName = profileSO.FindProperty("profileName");
        SerializedProperty attackMode = profileSO.FindProperty("attackMode");
        SerializedProperty defenseMode = profileSO.FindProperty("defenseMode");

        EditorGUILayout.PropertyField(profileName);
        EditorGUILayout.Space(2f);
        EditorGUILayout.PropertyField(attackMode, true);
        EditorGUILayout.Space(2f);
        EditorGUILayout.PropertyField(defenseMode, true);

        profileSO.ApplyModifiedProperties();
        EditorUtility.SetDirty(profile);
    }

    private void CreateAndAssignProfileAsset()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Create AI Shopping Profile",
            "AIShoppingProfile_New",
            "asset",
            "Choose where to save the new AI shopping profile.");

        if (string.IsNullOrEmpty(path))
            return;

        AIShoppingProfile profile = ScriptableObject.CreateInstance<AIShoppingProfile>();
        profile.ResetToBasic();

        AssetDatabase.CreateAsset(profile, path);
        AssetDatabase.SaveAssets();

        shoppingProfileProp.objectReferenceValue = profile;
        serializedObject.ApplyModifiedProperties();
        EditorGUIUtility.PingObject(profile);
    }

    private void DuplicateAndAssignProfileAsset(AIShoppingProfile source)
    {
        string sourcePath = AssetDatabase.GetAssetPath(source);
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            EditorUtility.DisplayDialog("Duplicate Profile", "Source profile must be a saved asset.", "OK");
            return;
        }
        string directory = string.IsNullOrWhiteSpace(sourcePath) ? "Assets" : System.IO.Path.GetDirectoryName(sourcePath);
        string fileName = string.IsNullOrWhiteSpace(sourcePath)
            ? "AIShoppingProfile_Copy.asset"
            : System.IO.Path.GetFileNameWithoutExtension(sourcePath) + "_Copy.asset";

        string newPath = AssetDatabase.GenerateUniqueAssetPath(System.IO.Path.Combine(directory, fileName));
        if (!AssetDatabase.CopyAsset(sourcePath, newPath))
        {
            EditorUtility.DisplayDialog("Duplicate Profile", "Failed to duplicate profile asset.", "OK");
            return;
        }

        AssetDatabase.SaveAssets();
        AIShoppingProfile duplicated = AssetDatabase.LoadAssetAtPath<AIShoppingProfile>(newPath);
        shoppingProfileProp.objectReferenceValue = duplicated;
        serializedObject.ApplyModifiedProperties();
        EditorGUIUtility.PingObject(duplicated);
    }
}

