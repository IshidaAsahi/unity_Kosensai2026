using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class EffectManager : MonoBehaviour
{
    public static EffectManager Instance;
    [Header("勝敗")]
    [SerializeField] private GameObject[] WinnerObj;

    [Header("カウントダウン")]
    [SerializeField] private GameObject[] Count;
    [SerializeField] private GameObject StartObj;
    [SerializeField] private GameObject FinishObj;
    [SerializeField] private float startScale = 5f;
    [SerializeField] private float stayScale = 0.3f;
    [SerializeField] private float fadeInTime = 0.5f;
    [SerializeField] private float stayTime = 0.5f;
    [SerializeField] private float fadeOutTime = 0.5f;

    private void Start()
    {
        Instance = this;
        reset();
        StartCoroutine(StartCount()); // テスト用呼び出し
    }

    public void reset()
    {
        for (int i = 0; i < WinnerObj.Length; i++)
        {
            WinnerObj[i].SetActive(false);
        }
        for (int i = 0; i < Count.Length; i++)
        {
            Count[i].SetActive(false);
        }
        StartObj.SetActive(false);
        FinishObj.SetActive(false);
    }


    public void WinnerEffect(int i)
    {
        WinnerObj[i].SetActive(true);
    }

    public IEnumerator StartCount()
    {
        yield return StartCoroutine(CountSequence());
        yield return StartCoroutine(CountEffect(StartObj));
    }

    public IEnumerator FinishCount()
    {
        yield return StartCoroutine(CountSequence());
        yield return StartCoroutine(CountEffect(FinishObj));
    }

    private IEnumerator CountSequence()
    {
        for (int i = 0; i < Count.Length; i++)
        {
            yield return StartCoroutine(CountEffect(Count[i]));
        }
    }

    private IEnumerator CountEffect(GameObject targetObject)
    {
        Vector3 startScaleVector = new Vector3(startScale, startScale, 1f);
        Vector3 stayScaleVector = new Vector3(stayScale, stayScale, 1f);
        targetObject.SetActive(true);
        targetObject.transform.localScale = startScaleVector;

        Graphic graphic = targetObject.GetComponent<Graphic>();
        SpriteRenderer spriteRenderer = targetObject.GetComponent<SpriteRenderer>();
        SetAlpha(graphic, spriteRenderer, 0f);

        float elapsed = 0f;
        while (elapsed < fadeInTime)
        {
            float progress = elapsed / fadeInTime;
            targetObject.transform.localScale = Vector3.Lerp(startScaleVector, stayScaleVector, progress);
            SetAlpha(graphic, spriteRenderer, progress);
            elapsed += Time.deltaTime;
            yield return null;
        }

        targetObject.transform.localScale = stayScaleVector;
        SetAlpha(graphic, spriteRenderer, 1f);
        yield return new WaitForSeconds(stayTime);

        elapsed = 0f;
        while (elapsed < fadeOutTime)
        {
            float progress = elapsed / fadeOutTime;
            targetObject.transform.localScale = Vector3.Lerp(stayScaleVector, Vector3.zero, progress);
            SetAlpha(graphic, spriteRenderer, 1f - progress);
            elapsed += Time.deltaTime;
            yield return null;
        }

        targetObject.transform.localScale = Vector3.zero;
        SetAlpha(graphic, spriteRenderer, 0f);
    }

    private void SetAlpha(Graphic graphic, SpriteRenderer spriteRenderer, float alpha)
    {
        if (graphic != null)
        {
            Color color = graphic.color;
            color.a = alpha;
            graphic.color = color;
        }
        else if (spriteRenderer != null)
        {
            Color color = spriteRenderer.color;
            color.a = alpha;
            spriteRenderer.color = color;
        }
    }
}
