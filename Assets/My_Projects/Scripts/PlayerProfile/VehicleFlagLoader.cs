using UnityEngine;
using UnityEngine.UI;

public sealed class VehicleFlagLoader : MonoBehaviour
{
    public enum FlagSource
    {
        PlayerProfile,
        Inspector
    }

    [SerializeField] private FlagSource flagSource = FlagSource.Inspector;
    [SerializeField, Tooltip("Used when Flag Source is Inspector.")] private int inspectorFlagIndex;
    [SerializeField] private Sprite[] availableFlags;
    [SerializeField] private Image[] flagImages;
    [SerializeField] private SpriteRenderer[] flagRenderers;

    private void Awake()
    {
        LoadFlag();
    }

    private void OnValidate()
    {
        inspectorFlagIndex = Mathf.Max(0, inspectorFlagIndex);

        if (Application.isPlaying)
            LoadFlag();
    }

    [ContextMenu("Load Flag")]
    public void LoadFlag()
    {
        int flagIndex = flagSource == FlagSource.PlayerProfile
            ? PlayerProfileStore.FlagIndex
            : inspectorFlagIndex;

        if (availableFlags == null || availableFlags.Length == 0)
        {
            Debug.LogError("VehicleFlagLoader has no available flags.", this);
            return;
        }

        if (flagIndex < 0 || flagIndex >= availableFlags.Length)
        {
            Debug.LogError($"Invalid flag index {flagIndex} on {gameObject.name}.", this);
            return;
        }

        Sprite selectedFlag = availableFlags[flagIndex];
        if (selectedFlag == null)
        {
            Debug.LogError($"Flag at index {flagIndex} is not assigned.", this);
            return;
        }

        if (flagImages == null || flagImages.Length == 0)
            flagImages = GetComponentsInChildren<Image>(true);

        if (flagRenderers == null || flagRenderers.Length == 0)
            flagRenderers = GetComponentsInChildren<SpriteRenderer>(true);

        for (int i = 0; i < flagImages.Length; i++)
        {
            if (flagImages[i] != null)
                flagImages[i].sprite = selectedFlag;
        }

        for (int i = 0; i < flagRenderers.Length; i++)
        {
            if (flagRenderers[i] != null)
                flagRenderers[i].sprite = selectedFlag;
        }
    }
}
