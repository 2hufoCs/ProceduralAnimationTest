using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;
using TMPro;
using System;

public abstract class TweenUtility : MonoBehaviour
{
    public static float SlideOut(RectTransform image, float duration)
    {
        image.gameObject.SetActive(true);
        image.anchoredPosition = Vector2.zero;

        DOTween.Sequence().Append(image.DOAnchorPosX(1920 * 1.5f, .7f)).OnComplete(() => image.gameObject.SetActive(false)).SetUpdate(true);
        return duration;
    }

    public static float SlideIn(RectTransform image, float duration)
    {
        image.anchoredPosition = new Vector2(-1920 * 1.5f, image.anchoredPosition.y);
        image.gameObject.SetActive(true);

        image.DOAnchorPosX(0, .7f).SetUpdate(true);
        return duration;
    }

    public static float FadeInOut(Image image, float inOutDuration, float idleDuration)
    {
        image.color = new Color(image.color.r, image.color.g, image.color.b, 0);
        image.gameObject.SetActive(true);

        DOTween.Sequence().SetUpdate(true)
        .Append(image.DOFade(1, inOutDuration))
        .AppendInterval(idleDuration)
        .Append(image.DOFade(0, inOutDuration))
        .OnComplete(() => image.gameObject.SetActive(false));

        return idleDuration + inOutDuration * 2;
    }

    public static float FadeInOut(TextMeshProUGUI text, float inOutDuration, float idleDuration)
    {
        text.color = new Color(text.color.r, text.color.g, text.color.b, 0);
        text.gameObject.SetActive(true);

        DOTween.Sequence().SetUpdate(true)
        .Append(text.DOFade(1, inOutDuration))
        .AppendInterval(idleDuration)
        .Append(text.DOFade(0, inOutDuration))
        .OnComplete(() => text.gameObject.SetActive(false));

        return idleDuration + inOutDuration * 2;
    }

    public static float HidePanel(RectTransform panel)
    {
        Sequence seq = DOTween.Sequence().SetUpdate(true);

        seq.Append(panel.DOScale(new Vector2(.95f, 1.1f), .05f).SetEase(Ease.OutSine));
        seq.Append(panel.DOScale(new Vector2(1.06f, 0f), .15f).SetEase(Ease.InSine));

        seq.AppendCallback(() =>
        {
            panel.gameObject.SetActive(false);
            panel.localScale = Vector2.one;
        });

        return .21f;
    }

    public static float ShowPanel(RectTransform panel, bool resetScale = false)
    {
        Sequence seq = DOTween.Sequence().SetUpdate(true);
        panel.gameObject.SetActive(true);
        if (resetScale) panel.localScale = Vector2.zero;

        seq.Append(panel.DOScale(new Vector2(.95f, 1.1f), .15f).SetEase(Ease.OutSine));
        seq.Append(panel.DOScale(Vector2.one, .05f).SetEase(Ease.InSine));
        return .21f;
    }

    public static float ShowPanel(RectTransform panel, Vector2 stretchScale, float stretchDuration, bool resetScale = false)
    {
        Sequence seq = DOTween.Sequence().SetUpdate(true);
        panel.gameObject.SetActive(true);
        if (resetScale) panel.localScale = Vector2.zero;

        seq.Append(panel.DOScale(stretchScale, stretchDuration).SetEase(Ease.OutSine));
        return stretchDuration;
    }

    public static float ShowPanel(RectTransform panel, Vector2 stretchScale, float stretchDuration, Vector2 squishScale, float squishDuration, bool resetScale)
    {
        Sequence seq = DOTween.Sequence().SetUpdate(true);
        panel.gameObject.SetActive(true);
        if (resetScale) panel.localScale = Vector2.zero;

        seq.Append(panel.DOScale(stretchScale, stretchDuration).SetEase(Ease.OutSine));
        seq.Append(panel.DOScale(squishScale, squishDuration).SetEase(Ease.InSine));
        return stretchDuration + squishDuration;
    }

    public static void TriggerPopup(RectTransform popup, Vector2 targetPos, float duration)
    {
        // Trigger popup
        Vector2 initialPos = popup.anchoredPosition;
        popup.gameObject.SetActive(true);
        popup.DOAnchorPos(targetPos, duration).SetEase(Ease.InOutSine);

        // Try getting image and text components, otherwise throw error
        Image bgImg = null;
        TextMeshProUGUI checkpointTxt = null;
        try
        {
            bgImg = popup.GetComponent<Image>();
            checkpointTxt = popup.GetChild(0).GetComponent<TextMeshProUGUI>();
        }
        catch (Exception ex)
        {
            if (!bgImg)
            {
                Debug.LogError($"Couldn't tween popup called {popup.gameObject.name} because it had no image to fade.\n{ex}"); 
                return;
            }
            Debug.LogError($"Couldn't tween popup called {popup.gameObject.name} because couldn't get child text (must be child with index 0).\n{ex}");
            return;
        }
    

        // Fade bg and text
        DOTween.Sequence()
        .Append(bgImg.DOFade(1, duration / 4))
        .Join(checkpointTxt.DOFade(1, duration / 4))
        .AppendInterval(duration / 2)
        .Append(bgImg.DOFade(0, duration / 4))
        .Join(checkpointTxt.DOFade(0, duration / 4))
        .OnComplete(() => // Reset popup state
        {
            popup.anchoredPosition = initialPos;
            popup.gameObject.SetActive(false);
        });
    }
}