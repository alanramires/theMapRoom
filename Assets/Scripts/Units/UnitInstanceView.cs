using UnityEngine;

public class UnitInstanceView : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private UnitDatabase unitDatabase;
    [SerializeField] private string unitId;
    [SerializeField] private string unitDisplayName;
    [SerializeField] private bool autoApplyOnStart = true;

    public string UnitId => unitId;
    public string UnitDisplayName => unitDisplayName;
    public UnitDatabase UnitDatabase => unitDatabase;

    private void Start()
    {
        if (!autoApplyOnStart)
            return;

        ApplyFromDatabase();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying)
            return;

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        ApplyFromDatabase();
    }
#endif

    public void Apply(UnitData data)
    {
        if (data == null)
            return;

        unitId = data.id;
        unitDisplayName = string.IsNullOrWhiteSpace(data.displayName) ? data.id : data.displayName;

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (spriteRenderer != null)
        {
            Sprite chosen = TeamUtils.GetTeamSprite(data, TeamId.Neutral);
            if (chosen != null)
                spriteRenderer.sprite = chosen;

            spriteRenderer.color = TeamUtils.GetColor(TeamId.Neutral);
        }

        gameObject.name = unitDisplayName;
    }

    public bool ApplyFromDatabase()
    {
        if (unitDatabase == null || string.IsNullOrWhiteSpace(unitId))
            return false;

        if (!unitDatabase.TryGetById(unitId, out UnitData data))
            return false;

        Apply(data);
        return true;
    }

    public void Setup(UnitDatabase database, string id)
    {
        unitDatabase = database;
        unitId = id;
    }

    [ContextMenu("Apply From Database")]
    private void ApplyFromDatabaseContext()
    {
        bool ok = ApplyFromDatabase();
        if (!ok)
            Debug.LogWarning("[UnitInstanceView] Nao foi possivel aplicar UnitData (db/id).", this);
    }
}
