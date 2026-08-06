using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Migracao do layout de rotas: catalogo -> CENA.
///
/// O StructureDatabase e catalogo compartilhado por todos os mapas — ele diz o
/// que uma rodovia E. Onde ela ESTA e deste tabuleiro, e por isso passa a viver
/// no RoadNetworkManager da cena.
///
/// A migracao COPIA e nao apaga a origem. Limpar o catalogo e um passo separado,
/// depois de o autor conferir mapa a mapa que as estradas continuam desenhando.
/// </summary>
[CustomEditor(typeof(RoadNetworkManager))]
public class RoadNetworkManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var manager = (RoadNetworkManager)target;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField(
            "Layout de Rotas (Map Scope)",
            EditorStyles.boldLabel);

        if (manager.RoutesMigratedToScene)
        {
            EditorGUILayout.HelpBox(
                "Esta cena e a UNICA fonte das rotas. O catalogo nao e mais "
                + "consultado, e bucket vazio significa \"nao ha rodovia deste "
                + "tipo aqui\" — nao \"ainda nao migrei\".",
                MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Ainda NAO migrado: as rotas continuam sendo lidas do "
                + "StructureDatabase (e do StructureData legado). Migre para que "
                + "duplicar esta cena pare de herdar o layout de outro mapa.",
                MessageType.Warning);
        }

        using (new EditorGUI.DisabledScope(manager.StructureDatabase == null))
        {
            if (GUILayout.Button("Migrar rotas do catalogo para esta cena"))
                Migrate(manager);
        }

        if (GUILayout.Button("Relatorio: o que esta cena veria hoje"))
            Report(manager);
    }

    private void Migrate(RoadNetworkManager manager)
    {
        StructureDatabase catalogue = manager.StructureDatabase;
        if (catalogue == null || catalogue.Structures == null)
            return;

        // Segunda passada numa cena ja migrada e o caso perigoso: o catalogo
        // pode ja ter sido limpo, e ai "substituir pelo catalogo" apaga o
        // layout da cena. Por isso pergunta em vez de decidir sozinho.
        if (manager.RoutesMigratedToScene
            && !EditorUtility.DisplayDialog(
                "Rotas ja migradas",
                $"'{manager.gameObject.scene.name}' ja esta marcada como "
                + "migrada.\n\nMigrar de novo SUBSTITUI as rotas desta cena "
                + "pelas do catalogo. Se o catalogo ja foi limpo, o layout "
                + "desta cena se perde.\n\nSubstituir mesmo assim?",
                "Substituir",
                "Cancelar"))
        {
            return;
        }

        Undo.RecordObject(manager, "Migrar rotas para a cena");

        // SUBSTITUI, nao empilha. Rodar duas vezes tem que dar o mesmo numero.
        manager.ClearSceneRoadRoutes();

        int structuresTouched = 0;
        int routesCopied = 0;
        int skippedForeign = 0;
        var log = new StringBuilder();

        for (int i = 0; i < catalogue.Structures.Count; i++)
        {
            StructureData structure = catalogue.Structures[i];
            if (structure == null)
                continue;

            List<RoadRouteDefinition> source = CollectSourceRoutes(
                catalogue,
                structure,
                ref skippedForeign);
            if (source.Count == 0)
                continue;

            List<RoadRouteDefinition> destination =
                manager.GetOrCreateRoadRoutes(structure);
            if (destination == null)
                continue;

            // Copia PROFUNDA: limpar o catalogo depois nao pode esvaziar a cena.
            for (int r = 0; r < source.Count; r++)
            {
                RoadRouteDefinition original = source[r];
                destination.Add(new RoadRouteDefinition
                {
                    routeName = original.routeName,
                    ownerDatabase = original.ownerDatabase,
                    cells = original.cells != null
                        ? new List<Vector3Int>(original.cells)
                        : new List<Vector3Int>()
                });
                routesCopied++;
            }

            structuresTouched++;
            log.AppendLine(
                $"  {structure.id}: {source.Count} rota(s)");
        }

        manager.MarkRoutesMigratedToScene(true);
        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        manager.RebuildRoadVisuals();

        // Confere o que a cena passou a ENXERGAR contra o que foi copiado. Sem
        // isto, copiar para lugar nenhum (ou copiar em dobro) so aparece no
        // relatorio que alguem lembrou de clicar.
        int visible = 0;
        for (int i = 0; i < catalogue.Structures.Count; i++)
        {
            StructureData structure = catalogue.Structures[i];
            if (structure == null)
                continue;

            IReadOnlyList<RoadRouteDefinition> seen =
                manager.GetRoadRoutes(structure);
            visible += seen != null ? seen.Count : 0;
        }

        string summary =
            $"[RoadRoutes][Migracao] cena='{manager.gameObject.scene.name}' "
            + $"estruturas={structuresTouched} rotas={routesCopied} "
            + $"visiveis={visible} "
            + $"descartadas_de_outro_catalogo={skippedForeign}\n"
            + log.ToString();

        if (visible != routesCopied)
        {
            Debug.LogError(
                summary
                + $"  DIVERGENCIA: copiei {routesCopied} e a cena enxerga "
                + $"{visible}. NAO salve; desfaca com Ctrl+Z.",
                manager);
            return;
        }

        Debug.Log(
            summary
            + "  O catalogo NAO foi alterado. Confira as estradas desenhadas "
            + "antes de limpar a origem.",
            manager);
    }

    /// <summary>
    /// Mesma ordem que o runtime usava antes da migracao: bucket do catalogo
    /// primeiro, StructureData legado como ultimo recurso. Rota autorada para
    /// OUTRO catalogo nao entra — ela nunca foi deste tabuleiro.
    /// </summary>
    private List<RoadRouteDefinition> CollectSourceRoutes(
        StructureDatabase catalogue,
        StructureData structure,
        ref int skippedForeign)
    {
        var kept = new List<RoadRouteDefinition>();
        IReadOnlyList<RoadRouteDefinition> source =
            catalogue.GetRoadRoutes(structure);
        if (source == null || source.Count == 0)
            source = structure.roadRoutes;
        if (source == null)
            return kept;

        for (int i = 0; i < source.Count; i++)
        {
            RoadRouteDefinition route = source[i];
            if (route == null)
                continue;

            if (route.ownerDatabase != null
                && route.ownerDatabase != catalogue)
            {
                skippedForeign++;
                continue;
            }

            kept.Add(route);
        }

        return kept;
    }

    private void Report(RoadNetworkManager manager)
    {
        StructureDatabase catalogue = manager.StructureDatabase;
        if (catalogue == null || catalogue.Structures == null)
        {
            Debug.LogWarning(
                "[RoadRoutes][Relatorio] sem StructureDatabase.",
                manager);
            return;
        }

        var log = new StringBuilder();
        int total = 0;
        for (int i = 0; i < catalogue.Structures.Count; i++)
        {
            StructureData structure = catalogue.Structures[i];
            if (structure == null)
                continue;

            IReadOnlyList<RoadRouteDefinition> routes =
                manager.GetRoadRoutes(structure);
            int count = routes != null ? routes.Count : 0;
            total += count;
            log.AppendLine($"  {structure.id}: {count} rota(s)");
        }

        Debug.Log(
            $"[RoadRoutes][Relatorio] cena='{manager.gameObject.scene.name}' "
            + $"migrado={manager.RoutesMigratedToScene} total={total}\n"
            + log.ToString(),
            manager);
    }
}
