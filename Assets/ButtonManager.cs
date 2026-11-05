using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class RoboEntry
{
    public string name = "ロボ";
    public TrailDrawer drawer;
}

[ExecuteAlways]
public class ButtonManager : MonoBehaviour
{

    public void PaintRobot(int index)
    {
        GameManager.Instance.PaintRobot(index);
    }

    public void OnStartButtonClicked()
    {
        OnResetButtonClicked();
        GameManager.Instance.ChangeState(true);
        StartCoroutine(EffectManager.Instance.StartCount());
    }

    // 既存ボタン互換
    public void OnPaintButton1Clicked() { PaintRobot(0); }
    public void OnPaintButton2Clicked() { PaintRobot(1); }

    public void OnResetButtonClicked()
    {
        EffectManager.Instance.reset();
        GameManager.Instance.ResetRobotTrails();
    }

    public void OnLimitTimeChangeButtonClicked(int t)
    {
        GameManager.Instance.ChangeLimitTime(t);
    }

    // ─── 判定 ───

    public void OnForceFinishButtonClicked()
    {
        GameManager.Instance.GameSet();
    }
}
