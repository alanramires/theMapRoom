using UnityEditor;
using UnityEngine;

/// <summary>
/// Inspector do AIController: o padrao de sempre, mais um quadro "Perfil em uso".
///
/// Existe porque o Inspector mostrava o CONFIGURADO ("Base Preset: Dificil") e
/// nao o que estava VALENDO — o catalogo respondia por cima e ninguem via
/// (relatorio v8.6.1). No Play, o quadro mostra o perfil resolvido e o valor
/// EFETIVO de cada capacidade, lido pelos mesmos getters que a IA consulta. No
/// Edit Mode, mostra o que cada dificuldade resolveria.
/// </summary>
[CustomEditor(typeof(AIController))]
public sealed class AIControllerEditor : Editor
{
    private bool showCapacities = true;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var controller = (AIController)target;
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Perfil em uso", EditorStyles.boldLabel);

        if (Application.isPlaying)
            DrawRuntime(controller);
        else
            DrawPreview(controller);
    }

    private void DrawRuntime(AIController controller)
    {
        AIDifficulty difficulty = controller.AppliedDifficulty;
        EditorGUILayout.LabelField(
            "Dificuldade",
            $"{AIPresetCatalog.RotuloDoJogador(difficulty)} (enum {difficulty})");
        EditorGUILayout.LabelField(
            "Perfil",
            controller.ActivePreset != null
                ? controller.PresetSource
                : "nenhum — a IA usa os flags da cena");

        showCapacities = EditorGUILayout.Foldout(
            showCapacities, "Capacidades efetivas (o que a IA lê agora)", true);
        if (showCapacities)
        {
            EditorGUI.indentLevel++;
            Capacity("Respeita lista banida", controller.RespeitaListaBanida);
            Capacity("Projeta produção inimiga", controller.ProjetaProducaoInimiga);
            Capacity("Abre com blindado", controller.AbreComBlindado);
            Capacity("Limita logística", controller.LimitaLogistica);
            Capacity("Dobra slots de capturador", controller.DobraSlotsDeCapturador);
            Capacity("Blitzkrieg (handoff em profundidade)", controller.FazHandoffEmProfundidade);
            Capacity("Conscrição sempre", controller.ConscriptionDoctrine);
            Capacity("Conscrição quando perdendo", controller.ConscriptionWhenLosing);
            Capacity("Lado forte/fraco", controller.StrongWeakSidePolitic);
            Capacity("Núcleo suave", controller.SoftCoreGate);
            EditorGUILayout.LabelField(
                "Fusão em reparo",
                ObjectNames.NicifyVariableName(controller.FusaoEmReparo.ToString()));
            EditorGUILayout.LabelField(
                "Renda fora de cidades",
                $"{controller.FracaoRendaForaDeCidades:P0}");
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.HelpBox(
            "Os NÚMEROS (elite ratio, núcleo, poupança...) ainda vêm da cena: o perfil "
            + "só manda nas capacidades até a fase 2 da migração.",
            MessageType.None);
    }

    private static void DrawPreview(AIController controller)
    {
        foreach (AIDifficulty difficulty in AIPresetCatalog.OferecidasAoJogador)
        {
            EditorGUILayout.LabelField(
                $"Botão {AIPresetCatalog.RotuloDoJogador(difficulty)}",
                controller.PreviewPresetSource(difficulty));
        }

        AIDifficulty direct = controller.SceneFlagsDifficulty;
        EditorGUILayout.HelpBox(
            $"Play direto nesta cena (sem passar pelo menu): os flags da cena dizem "
            + $"{AIPresetCatalog.RotuloDoJogador(direct)}, e o perfil resolvido é "
            + $"{controller.PreviewPresetSource(direct)}. Pelo menu, vale o botão escolhido; "
            + "num load de save, a dificuldade gravada nele.",
            MessageType.Info);
    }

    private static void Capacity(string label, bool on) =>
        EditorGUILayout.LabelField(label, on ? "ligada" : "—");
}
