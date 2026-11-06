using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Std;

[System.Serializable]
public class RoboEntry
{
    public string name = "ロボ";
    public TrailDrawer drawer;
}

[ExecuteAlways]
public class ButtonManager : MonoBehaviour
{
    [SerializeField]
    RoboEntry[] robots = new RoboEntry[]
    {
        new RoboEntry { name = "ロボ1" },
        new RoboEntry { name = "ロボ2" }
    };

    ROSConnection ros;
    // ROS2のトピック名
    string[] topicNames;

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();

        topicNames = new string[robots.Length];

        for (int i = 0; i < robots.Length; i++)
        {
            topicNames[i] = "/button" + (i + 1);

            ros.RegisterPublisher<BoolMsg>(topicNames[i]);
        }

        ros.RegisterPublisher<BoolMsg>("/reset");
    }

    void PublishButtonData(int index)
    {
        BoolMsg msg = new BoolMsg(true);
        ros.Publish(topicNames[index], msg);
    }

    void PublishResetData()
    {
        BoolMsg msg = new BoolMsg(true);
        ros.Publish("/reset", msg);
    }

    public void PaintRobot(int index)
    {
        GameManager.Instance.PaintRobot(index);
        PublishButtonData(index);
    }

    public void OnStartButtonClicked()
    {
        OnResetButtonClicked();
        GameManager.Instance.ChangeState(true);
        StartCoroutine(EffectManager.Instance.StartCount());
        StartCoroutine(EffectManager.Instance.GageEffect());
    }

    // 既存ボタン互換
    public void OnPaintButton1Clicked()
    {
        PaintRobot(0);
        StartCoroutine(EffectManager.Instance.GorlEffect(0));
    }
    public void OnPaintButton2Clicked()
    {
        PaintRobot(1);
        StartCoroutine(EffectManager.Instance.GorlEffect(1));
    }

    public void OnResetButtonClicked()
    {
        EffectManager.Instance.reset();
        GameManager.Instance.ResetRobotTrails();
        GameManager.Instance.reset();

        PublishResetData();
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