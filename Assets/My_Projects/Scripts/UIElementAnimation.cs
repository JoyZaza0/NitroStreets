using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PrimeTween;

public class UIElementAnimation : MonoBehaviour
{
    public AnimatedItem[] animatedItems;

    private readonly List<Tween> activeTweens = new List<Tween>();

    private void Awake()
    {
        CaptureInitialStates();
    }

    void OnEnable()
    {
        CaptureInitialStates();
        ResetItemsToStartState();
        AnimateItems();
    }

    void OnDisable()
    {
        StopAllCoroutines();

        foreach (var tween in activeTweens)
        {
            if (tween.isAlive)
            {
                tween.Stop();
            }
        }

        activeTweens.Clear();
        ResetItemsToStartState();
    }

    public void AnimateItems()
    {
        if (animatedItems == null)
        {
            return;
        }

        foreach (var animatedItem in animatedItems)
        {
            if (animatedItem == null || animatedItem.item == null)
            {
                continue;
            }

            if (animatedItem.delay > 0f)
            {
                StartCoroutine(AnimateWithDelay(animatedItem));
            }
            else
            {
                activeTweens.Add(PlayAnimation(animatedItem));
            }
        }
    }

    private IEnumerator AnimateWithDelay(AnimatedItem animatedItem)
    {
        yield return new WaitForSeconds(animatedItem.delay);
        activeTweens.Add(PlayAnimation(animatedItem));
    }

    private Tween PlayAnimation(AnimatedItem animatedItem)
    {
        switch (animatedItem.animationType)
        {
            case AnimationType.Position:
                return AnimatePosition(animatedItem);
            case AnimationType.Scale:
                return AnimateScale(animatedItem);
            case AnimationType.Fade:
                return AnimateFade(animatedItem);
            default:
                return default;
        }
    }

    private Tween AnimatePosition(AnimatedItem animatedItem)
    {
        var duration = Mathf.Max(0.01f, animatedItem.duration);
        var target = animatedItem.item.transform;
        var rect = animatedItem.item.GetComponent<RectTransform>();

        if (rect != null)
        {
            if (animatedItem.useAnchoredPosition)
            {
                var startAnchoredPosition = rect.anchoredPosition;
                var endAnchoredPosition = new Vector2(animatedItem.endPosition.x, animatedItem.endPosition.y);
                return Tween.Custom(rect, startAnchoredPosition, endAnchoredPosition, duration,
                    (r, value) => r.anchoredPosition = value,
                    animatedItem.ease);
            }

            if (animatedItem.useLocalPosition)
            {
                return Tween.LocalPosition(rect, animatedItem.endPosition, duration, animatedItem.ease);
            }
        }

        if (animatedItem.useLocalPosition)
        {
            return Tween.LocalPosition(target, animatedItem.endPosition, duration, animatedItem.ease);
        }

        return Tween.Position(target, animatedItem.endPosition, duration, animatedItem.ease);
    }

    private Tween AnimateScale(AnimatedItem animatedItem)
    {
        var duration = Mathf.Max(0.01f, animatedItem.duration);
        return Tween.Scale(animatedItem.item.transform, animatedItem.endScale, duration, animatedItem.ease);
    }

    private Tween AnimateFade(AnimatedItem animatedItem)
    {
        var duration = Mathf.Max(0.01f, animatedItem.duration);
        var graphic = animatedItem.item.GetComponent<Graphic>();
        if (graphic != null)
        {
            return Tween.Alpha(graphic, animatedItem.startAlpha, animatedItem.endAlpha, duration, animatedItem.ease);
        }

        var canvasGroup = animatedItem.item.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = animatedItem.item.AddComponent<CanvasGroup>();
        }

        return Tween.Alpha(canvasGroup, animatedItem.startAlpha, animatedItem.endAlpha, duration, animatedItem.ease);
    }

    private void CaptureInitialStates()
    {
        if (animatedItems == null)
        {
            return;
        }

        foreach (var animatedItem in animatedItems)
        {
            if (animatedItem == null || animatedItem.item == null)
            {
                continue;
            }

            if (animatedItem.hasInitialState)
            {
                continue;
            }

            var transform = animatedItem.item.transform;
            var rect = animatedItem.item.GetComponent<RectTransform>();

            if (rect != null && animatedItem.useAnchoredPosition)
            {
                animatedItem.initialPosition = rect.anchoredPosition;
            }
            else if (animatedItem.useLocalPosition)
            {
                animatedItem.initialPosition = transform.localPosition;
            }
            else
            {
                animatedItem.initialPosition = transform.position;
            }

            animatedItem.initialScale = transform.localScale;
            animatedItem.hasInitialState = true;
        }
    }

    private void ResetItemsToStartState()
    {
        if (animatedItems == null)
        {
            return;
        }

        foreach (var animatedItem in animatedItems)
        {
            if (animatedItem == null || animatedItem.item == null || !animatedItem.hasInitialState)
            {
                continue;
            }

            var transform = animatedItem.item.transform;
            var rect = animatedItem.item.GetComponent<RectTransform>();

            if (animatedItem.animationType == AnimationType.Position)
            {
                if (rect != null && animatedItem.useAnchoredPosition)
                {
                    rect.anchoredPosition = animatedItem.initialPosition;
                }
                else if (animatedItem.useLocalPosition)
                {
                    transform.localPosition = animatedItem.initialPosition;
                }
                else
                {
                    transform.position = animatedItem.initialPosition;
                }
            }

            if (animatedItem.animationType == AnimationType.Scale)
            {
                transform.localScale = animatedItem.initialScale;
            }

            if (animatedItem.animationType == AnimationType.Fade)
            {
                var graphic = animatedItem.item.GetComponent<Graphic>();
                if (graphic != null)
                {
                    var color = graphic.color;
                    color.a = animatedItem.startAlpha;
                    graphic.color = color;
                }
                else
                {
                    var canvasGroup = animatedItem.item.GetComponent<CanvasGroup>();
                    if (canvasGroup != null)
                    {
                        canvasGroup.alpha = animatedItem.startAlpha;
                    }
                }
            }
        }
    }
}

[System.Serializable]
public class AnimatedItem
{
    public AnimationType animationType;
    public GameObject item;
    public float delay;
    public float duration = 0.3f;
    public Ease ease = Ease.OutBack;
    public bool useLocalPosition = false;
    public bool useAnchoredPosition = true;
    public Vector3 endPosition;
    public Vector3 endScale = Vector3.one;
    public float startAlpha = 1f;
    public float endAlpha = 1f;

    [System.NonSerialized] internal Vector3 initialPosition;
    [System.NonSerialized] internal Vector3 initialScale;
    [System.NonSerialized] internal bool hasInitialState;
}

public enum AnimationType
{
    Position,
    Scale,
    Fade
}
