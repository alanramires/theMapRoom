using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class SpriteSequenceFx : MonoBehaviour
{
    [SerializeField] private Sprite[] frames;
    [SerializeField] private float frameDuration = 0.06f;
    [SerializeField] private bool destroyOnFinish = true;

    private SpriteRenderer spriteRenderer;
    private float elapsed;
    private int index;

    public float TotalDuration => (frames == null || frames.Length == 0)
        ? 0f
        : Mathf.Max(0.0001f, frameDuration) * frames.Length;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
        elapsed = 0f;
        index = 0;
        if (spriteRenderer != null && frames != null && frames.Length > 0)
            spriteRenderer.sprite = frames[0];
    }

    private void Update()
    {
        if (spriteRenderer == null || frames == null || frames.Length == 0)
            return;

        elapsed += Time.deltaTime;
        int nextIndex = Mathf.FloorToInt(elapsed / Mathf.Max(0.0001f, frameDuration));
        if (nextIndex == index)
            return;

        index = nextIndex;
        if (index >= frames.Length)
        {
            if (destroyOnFinish)
                Destroy(gameObject);
            else
                gameObject.SetActive(false);
            return;
        }

        spriteRenderer.sprite = frames[index];
    }

    public void Configure(Sprite[] sequenceFrames, float perFrameDuration, bool autoDestroy)
    {
        frames = sequenceFrames;
        frameDuration = Mathf.Max(0.0001f, perFrameDuration);
        destroyOnFinish = autoDestroy;
    }
}
