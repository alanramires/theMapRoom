using UnityEngine;
using UnityEngine.EventSystems;

public sealed class MenuShortcutButton : MonoBehaviour, IPointerClickHandler
{
    private BattleMapMenuRootController menuController;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData == null || eventData.button != PointerEventData.InputButton.Left)
            return;

        if (menuController == null)
        {
            menuController = FindFirstObjectByType<BattleMapMenuRootController>(FindObjectsInactive.Include);
        }

        menuController?.TryToggleMenuFromShortcut();
    }
}
