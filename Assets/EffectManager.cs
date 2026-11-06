using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class EffectManager : MonoBehaviour
{
    public static EffectManager Instance;
    [Header("勝敗")]
    [SerializeField] private GameObject[] WinnerObj;

    [Header("カウントダウン")]
    [SerializeField] private GameObject[] Count3;
    [SerializeField] private GameObject[] Count2;
    [SerializeField] private GameObject[] Count1;
    [SerializeField] private GameObject[] StartObj;
    [SerializeField] private GameObject[] FinishObj;
    [SerializeField] private float startScale = 5f;
    [SerializeField] private float stayScale = 0.3f;
    [SerializeField] private float fadeInTime = 0.5f;
    [SerializeField] private float stayTime = 0.5f;
    [SerializeField] private float fadeOutTime = 0.5f;
    [SerializeField] private GameObject TimerPref;
    [SerializeField] private int TimerNum;
    [SerializeField] private Color TimerTrueColor;
    [SerializeField] private Color TimerFalseColor;
    [SerializeField] private Vector3 TimerCenter;
    GameObject[] Timer;
    Image[] timerImages;


    private void Start()
    {
        Instance = this;
        float angle;
        Timer = new GameObject[TimerNum];
        timerImages = new Image[TimerNum];
        for (int i = 1; i <= TimerNum; i++)
        {
            angle = 360f / (float)TimerNum * i;
            Timer[i - 1] = Instantiate(TimerPref, TimerCenter, Quaternion.Euler(0, 0, angle));
            Timer[i - 1].transform.SetParent(this.gameObject.transform);
            timerImages[i - 1] = Timer[i - 1].transform.GetChild(0).GetComponent<Image>();
        }
        reset();
    }

    public IEnumerator UpdateTimerEffect()
    {
        while (GameManager.Instance.IsPlay)
        {
            if (TimerNum > 0 && GameManager.Instance.LimitTime > 0)
            {
                float segmentDuration = (float)GameManager.Instance.LimitTime / TimerNum;
                int falseCount = Mathf.Clamp(Mathf.FloorToInt(GameManager.Instance.elapsedTime / segmentDuration), 0, Timer.Length);
                for (int i = 0; i < Timer.Length; i++)
                {
                    if (i >= timerImages.Length || timerImages[i] == null)
                        continue;

                    Color targetColor = i < falseCount ? TimerFalseColor : TimerTrueColor;
                    if (timerImages[i].color != targetColor)
                        timerImages[i].color = targetColor;
                }
            }
            yield return null;
        }

        for (int i = 0; i < Timer.Length; i++)
        {
            if (i >= timerImages.Length || timerImages[i] == null)
                continue;

            if (timerImages[i].color != TimerFalseColor)
                timerImages[i].color = TimerFalseColor;
        }
    }

    public void reset()
    {
        for (int i = 0; i < WinnerObj.Length; i++)
        {
            WinnerObj[i].SetActive(false);
        }
        for (int i = 0; i < 2; i++)
        {
            Count3[i].SetActive(false);
            Count2[i].SetActive(false);
            Count1[i].SetActive(false);
            StartObj[i].SetActive(false);
            FinishObj[i].SetActive(false);
        }
    }


    public void WinnerEffect(int i)
    {
        WinnerObj[i].SetActive(true);
    }

    public IEnumerator StartCount()
    {
        yield return StartCoroutine(CountSequence());
        StartCoroutine(CountEffect(StartObj[0]));
        yield return StartCoroutine(CountEffect(StartObj[1]));
        StartCoroutine(GameManager.Instance.TimerStart());
    }

    public IEnumerator FinishCount()
    {
        StartCoroutine(CountEffect(FinishObj[0]));
        yield return StartCoroutine(CountEffect(FinishObj[1]));

        GameManager gameManager = GameManager.Instance;
        while (gameManager != null && !gameManager.IsJudgeComplete)
            yield return null;

        if (gameManager != null)
        {
            float[] percentages = gameManager.RobotPercentages;
            int winnerIndex = -1;
            float maxPercentage = 0f;
            bool tie = false;
            for (int i = 0; i < percentages.Length; i++)
            {
                if (percentages[i] > maxPercentage)
                {
                    maxPercentage = percentages[i];
                    winnerIndex = i;
                    tie = false;
                }
                else if (percentages[i] == maxPercentage && maxPercentage > 0f)
                {
                    tie = true;
                }
            }
            WinnerEffect(tie || winnerIndex < 0 ? 2 : winnerIndex);
        }
    }

    private IEnumerator CountSequence()
    {
        StartCoroutine(CountEffect(Count3[0]));
        yield return StartCoroutine(CountEffect(Count3[1]));
        StartCoroutine(CountEffect(Count2[0]));
        yield return StartCoroutine(CountEffect(Count2[1]));
        StartCoroutine(CountEffect(Count1[0]));
        yield return StartCoroutine(CountEffect(Count1[1]));

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

