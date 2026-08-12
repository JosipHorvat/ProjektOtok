using UnityEngine;

using System;
using System.Collections;
using DG.Tweening;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Persistent reusable screen fade controller.
/// Handles only visual transitions.
/// Does NOT load scenes or control game flow.
/// Place the prefab in Bootstrap.
/// </summary>
public class FadeCanvas : MonoBehaviour
{
    public enum TransitionStyle
    {
        ClassicFade,
        DoorSlide
    }

    public static FadeCanvas Instance { get; private set; }

    public static event Action OnFadeComplete;


    [Header("Default Settings")]
    [SerializeField] private TransitionStyle defaultStyle = TransitionStyle.ClassicFade;

    [SerializeField, Min(0f)]
    private float duration = 1f;


    [Header("Fade References")]
    [SerializeField] private Image fadeImage;

    [SerializeField] private RectTransform doorLeft;
    [SerializeField] private RectTransform doorRight;


    [Header("Startup")]
    [SerializeField] private bool startCovered = true;


    private Coroutine fadeRoutine;


    public bool IsFading { get; private set; }


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }


        Instance = this;

        DontDestroyOnLoad(gameObject);


        SetupVisuals();


        if (startCovered)
        {
            SetCovered();
        }
        else
        {
            SetRevealed();
        }
    }



    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (Instance != this)
        {
            return;
        }

        FadeIn();
    }


    private void SetupVisuals()
    {

        if (fadeImage != null)
        {
            fadeImage.raycastTarget = false;
        }


        DisableDoorRaycasts();
    }



    public void FadeOut()
    {
        FadeOut(defaultStyle);
    }



    public void FadeIn()
    {
        FadeIn(defaultStyle);
    }



    public void FadeOut(TransitionStyle style)
    {
        StartFade(FadeOutRoutine(style));
    }



    public void FadeIn(TransitionStyle style)
    {
        StartFade(FadeInRoutine(style));
    }



    private void StartFade(IEnumerator routine)
    {
        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
        }


        fadeRoutine = StartCoroutine(routine);
    }



    private IEnumerator FadeOutRoutine(TransitionStyle style)
    {
        IsFading = true;


        switch (style)
        {
            case TransitionStyle.ClassicFade:
                yield return ClassicFadeOut();
                break;


            case TransitionStyle.DoorSlide:
                yield return DoorClose();
                break;
        }


        IsFading = false;
        OnFadeComplete?.Invoke();
    }



    private IEnumerator FadeInRoutine(TransitionStyle style)
    {
        IsFading = true;


        switch (style)
        {
            case TransitionStyle.ClassicFade:
                yield return ClassicFadeIn();
                break;


            case TransitionStyle.DoorSlide:
                yield return DoorOpen();
                break;
        }


        IsFading = false;
        OnFadeComplete?.Invoke();
    }



    private IEnumerator ClassicFadeOut()
    {
        if (fadeImage == null)
            yield break;


        fadeImage.gameObject.SetActive(true);


        Color color = fadeImage.color;
        color.a = 0f;
        fadeImage.color = color;


        fadeImage.DOKill();

        yield return fadeImage.DOFade(1f, duration)
            .SetEase(Ease.InOutCubic)
            .SetUpdate(true)
            .WaitForCompletion();
    }



    private IEnumerator ClassicFadeIn()
    {
        if (fadeImage == null)
            yield break;


        fadeImage.gameObject.SetActive(true);


        fadeImage.DOKill();

        yield return fadeImage.DOFade(0f, duration)
            .SetEase(Ease.InOutCubic)
            .SetUpdate(true)
            .WaitForCompletion();


        fadeImage.gameObject.SetActive(false);
    }



    private IEnumerator DoorClose()
    {
        if (doorLeft == null || doorRight == null)
        {
            yield return ClassicFadeOut();
            yield break;
        }


        doorLeft.gameObject.SetActive(true);
        doorRight.gameObject.SetActive(true);


        doorLeft.DOKill();
        doorRight.DOKill();


        doorLeft.anchoredPosition = new Vector2(-doorLeft.rect.width, 0);
        doorRight.anchoredPosition = new Vector2(doorRight.rect.width, 0);



        Sequence sequence = DOTween.Sequence()
            .SetUpdate(true);

        sequence.Join(
            doorLeft.DOAnchorPosX(0, duration)
                .SetEase(Ease.InOutCubic)
        );

        sequence.Join(
            doorRight.DOAnchorPosX(0, duration)
                .SetEase(Ease.InOutCubic)
        );

        yield return sequence.WaitForCompletion();
    }



    private IEnumerator DoorOpen()
    {
        if (doorLeft == null || doorRight == null)
        {
            yield return ClassicFadeIn();
            yield break;
        }


        doorLeft.DOKill();
        doorRight.DOKill();


        Sequence sequence = DOTween.Sequence()
            .SetUpdate(true);

        sequence.Join(
            doorLeft.DOAnchorPosX(-doorLeft.rect.width, duration)
                .SetEase(Ease.OutCubic)
        );

        sequence.Join(
            doorRight.DOAnchorPosX(doorRight.rect.width, duration)
                .SetEase(Ease.OutCubic)
        );

        yield return sequence.WaitForCompletion();


        doorLeft.gameObject.SetActive(false);
        doorRight.gameObject.SetActive(false);
    }



    private void SetCovered()
    {
        if (defaultStyle == TransitionStyle.DoorSlide)
        {
            if (doorLeft != null)
                doorLeft.gameObject.SetActive(true);

            if (doorRight != null)
                doorRight.gameObject.SetActive(true);
        }
        else if (fadeImage != null)
        {
            fadeImage.gameObject.SetActive(true);

            Color color = fadeImage.color;
            color.a = 1f;
            fadeImage.color = color;
        }
    }



    private void SetRevealed()
    {
        if (fadeImage != null)
        {
            fadeImage.gameObject.SetActive(false);
        }


        if (doorLeft != null)
            doorLeft.gameObject.SetActive(false);


        if (doorRight != null)
            doorRight.gameObject.SetActive(false);
    }



    private void DisableDoorRaycasts()
    {
        if (doorLeft != null &&
            doorLeft.TryGetComponent<Image>(out var leftImage))
        {
            leftImage.raycastTarget = false;
        }


        if (doorRight != null &&
            doorRight.TryGetComponent<Image>(out var rightImage))
        {
            rightImage.raycastTarget = false;
        }
    }



    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }


        fadeImage?.DOKill();
        doorLeft?.DOKill();
        doorRight?.DOKill();
    }
}
