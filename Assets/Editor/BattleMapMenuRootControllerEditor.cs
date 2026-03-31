using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BattleMapMenuRootController))]
public class BattleMapMenuRootControllerEditor : Editor
{
    // BattleMapMenuRootController props
    private SerializedProperty mainMenuSceneNameProp;
    private SerializedProperty dockEnterProximityPixelsProp;
    private SerializedProperty dockExitProximityPixelsProp;
    private SerializedProperty dockedAnchoredPositionProp;
    private SerializedProperty menuRootProp;
    private SerializedProperty panelMenuProp;
    private SerializedProperty panelOptionsProp;
    private SerializedProperty panelGerenciarProp;
    private SerializedProperty btnStatusProp;
    private SerializedProperty btnComandoProp;
    private SerializedProperty btnRodadaProp;
    private SerializedProperty btnOpcoesProp;
    private SerializedProperty btnVoltarMenuProp;
    private SerializedProperty btnMinimapaProp;
    private SerializedProperty btnConfigProp;
    private SerializedProperty btnSaveLoadProp;
    private SerializedProperty btnGerenciarProp;
    private SerializedProperty btnVoltarOptionsProp;
    private SerializedProperty btnDestruirProp;
    private SerializedProperty btnRenderProp;
    private SerializedProperty btnSairProp;
    private SerializedProperty btnVoltarGerenciarProp;

    // MainMenuStateController stateLog (encontrado na cena)
    private SerializedObject mainMenuStateControllerSO;
    private SerializedProperty stateLogProp;

    private void OnEnable()
    {
        mainMenuSceneNameProp        = serializedObject.FindProperty("mainMenuSceneName");
        dockEnterProximityPixelsProp = serializedObject.FindProperty("dockEnterProximityPixels");
        dockExitProximityPixelsProp  = serializedObject.FindProperty("dockExitProximityPixels");
        dockedAnchoredPositionProp   = serializedObject.FindProperty("dockedAnchoredPosition");
        menuRootProp                 = serializedObject.FindProperty("menuRoot");
        panelMenuProp                = serializedObject.FindProperty("panelMenu");
        panelOptionsProp             = serializedObject.FindProperty("panelOptions");
        panelGerenciarProp           = serializedObject.FindProperty("panelGerenciar");
        btnStatusProp                = serializedObject.FindProperty("btnStatus");
        btnComandoProp               = serializedObject.FindProperty("btnComando");
        btnRodadaProp                = serializedObject.FindProperty("btnRodada");
        btnOpcoesProp                = serializedObject.FindProperty("btnOpcoes");
        btnVoltarMenuProp            = serializedObject.FindProperty("btnVoltarMenu");
        btnMinimapaProp              = serializedObject.FindProperty("btnMinimapa");
        btnConfigProp                = serializedObject.FindProperty("btnConfig");
        btnSaveLoadProp              = serializedObject.FindProperty("btnSaveLoad");
        btnGerenciarProp             = serializedObject.FindProperty("btnGerenciar");
        btnVoltarOptionsProp         = serializedObject.FindProperty("btnVoltarOptions");
        btnDestruirProp              = serializedObject.FindProperty("btnDestruir");
        btnRenderProp                = serializedObject.FindProperty("btnRender");
        btnSairProp                  = serializedObject.FindProperty("btnSair");
        btnVoltarGerenciarProp       = serializedObject.FindProperty("btnVoltarGerenciar");

        RefreshMainMenuStateControllerRef();
    }

    private void RefreshMainMenuStateControllerRef()
    {
        MainMenuStateController[] all = Resources.FindObjectsOfTypeAll<MainMenuStateController>();
        MainMenuStateController mc = all != null && all.Length > 0 ? all[0] : null;
        if (mc != null)
        {
            mainMenuStateControllerSO = new SerializedObject(mc);
            stateLogProp = mainMenuStateControllerSO.FindProperty("stateLog");
        }
        else
        {
            mainMenuStateControllerSO = null;
            stateLogProp = null;
        }
    }

    public override void OnInspectorGUI()
    {
        if (mainMenuStateControllerSO == null || stateLogProp == null)
            RefreshMainMenuStateControllerRef();

        serializedObject.Update();

        EditorGUILayout.LabelField("Scene", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(mainMenuSceneNameProp, new GUIContent("Main Menu Scene Name"));

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Dock", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(dockEnterProximityPixelsProp, new GUIContent("Dock Enter Proximity Pixels"));
        EditorGUILayout.PropertyField(dockExitProximityPixelsProp,  new GUIContent("Dock Exit Proximity Pixels"));
        EditorGUILayout.PropertyField(dockedAnchoredPositionProp,   new GUIContent("Docked Anchored Position"));

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Optional References", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(menuRootProp,       new GUIContent("Menu Root"));
        EditorGUILayout.PropertyField(panelMenuProp,      new GUIContent("Panel Menu"));
        EditorGUILayout.PropertyField(panelOptionsProp,   new GUIContent("Panel Options"));
        EditorGUILayout.PropertyField(panelGerenciarProp, new GUIContent("Panel Gerenciar"));
        EditorGUILayout.PropertyField(btnStatusProp,       new GUIContent("Btn Status"));
        EditorGUILayout.PropertyField(btnComandoProp,      new GUIContent("Btn Comando"));
        EditorGUILayout.PropertyField(btnRodadaProp,       new GUIContent("Btn Rodada"));
        EditorGUILayout.PropertyField(btnOpcoesProp,       new GUIContent("Btn Opcoes"));
        EditorGUILayout.PropertyField(btnVoltarMenuProp,   new GUIContent("Btn Voltar Menu"));
        EditorGUILayout.PropertyField(btnMinimapaProp,     new GUIContent("Btn Minimapa"));
        EditorGUILayout.PropertyField(btnConfigProp,       new GUIContent("Btn Config"));
        EditorGUILayout.PropertyField(btnSaveLoadProp,     new GUIContent("Btn Save Load"));
        EditorGUILayout.PropertyField(btnGerenciarProp,    new GUIContent("Btn Gerenciar"));
        EditorGUILayout.PropertyField(btnVoltarOptionsProp,new GUIContent("Btn Voltar Options"));
        EditorGUILayout.PropertyField(btnDestruirProp,     new GUIContent("Btn Destruir"));
        EditorGUILayout.PropertyField(btnRenderProp,       new GUIContent("Btn Render"));
        EditorGUILayout.PropertyField(btnSairProp,         new GUIContent("Btn Sair"));
        EditorGUILayout.PropertyField(btnVoltarGerenciarProp, new GUIContent("Btn Voltar Gerenciar"));

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Logs", EditorStyles.boldLabel);

        if (mainMenuStateControllerSO != null && stateLogProp != null)
        {
            mainMenuStateControllerSO.Update();
            EditorGUILayout.PropertyField(stateLogProp, new GUIContent("Main Menu State Log"));
            mainMenuStateControllerSO.ApplyModifiedProperties();
        }
        else
        {
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.LabelField("Main Menu State Log", "MainMenuStateController nao encontrado na cena");
        }

        serializedObject.ApplyModifiedProperties();
    }
}
