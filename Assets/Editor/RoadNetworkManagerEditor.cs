using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Este editor tinha 240 linhas, e a maior parte era a migracao
/// "catalogo -> cena", com o aviso de nao rodar duas vezes.
///
/// A migracao terminou: StructureDatabase.roadRoutesByStructure e
/// StructureData.roadRoutes foram removidos, e a cena e a UNICA fonte de layout
/// de estrada. Sem duas fontes nao ha o que migrar, nem flag para conferir.
///
/// Sobra o relatorio, que continua util: ele responde "o que esta cena ve hoje",
/// e e por ele que se percebe rota estrangeira ou bucket vazio inesperado.
/// </summary>
[CustomEditor(typeof(RoadNetworkManager))]
public class RoadNetworkManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var manager = (RoadNetworkManager)target;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Layout de Rotas (Map Scope)", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Esta cena é a ÚNICA fonte das rotas. Bucket vazio significa \"não há "
            + "rodovia deste tipo aqui\" — o catálogo não guarda mais traçado.",
            MessageType.Info);

        if (GUILayout.Button("Relatório: o que esta cena vê hoje"))
            Report(manager);
    }

    private void Report(RoadNetworkManager manager)
    {
        StructureDatabase catalogue = manager.StructureDatabase;
        if (catalogue == null || catalogue.Structures == null)
        {
            Debug.LogWarning("[RoadRoutes][Relatorio] sem StructureDatabase.", manager);
            return;
        }

        var log = new StringBuilder();
        int total = 0;

        for (int i = 0; i < catalogue.Structures.Count; i++)
        {
            StructureData structure = catalogue.Structures[i];
            if (structure == null)
                continue;

            IReadOnlyList<RoadRouteDefinition> routes = manager.GetRoadRoutes(structure);
            int count = routes != null ? routes.Count : 0;
            total += count;
            log.AppendLine($"  {structure.id}: {count} rota(s)");
        }

        Debug.Log(
            $"[RoadRoutes][Relatorio] cena='{manager.gameObject.scene.name}' total={total}\n"
            + log.ToString(),
            manager);
    }
}
