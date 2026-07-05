using UnityEngine;
using UnityEngine.EventSystems;

public sealed class MenuShortcutButton : MonoBehaviour, IPointerClickHandler
{
    private BattleMapMenuRootController menuController;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData == null || eventData.button != PointerEventData.InputButton.Left)
            return;

        // O hq_shortcut nasceu como duplicata visual do menu_shortcut e pode ainda
        // carregar este componente no prefab. Somente o objeto de menu deve abrir
        // o menu; outros atalhos possuem suas proprias acoes.
        if (!string.Equals(gameObject.name, "menu_shortcut", System.StringComparison.OrdinalIgnoreCase))
            return;

        if (menuController == null)
        {
            menuController = FindFirstObjectByType<BattleMapMenuRootController>(FindObjectsInactive.Include);
        }

        menuController?.TryToggleMenuFromShortcut();
    }
}
