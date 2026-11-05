using UnityEngine;
using RosMessageTypes.Geometry;
using Unity.Robotics.ROSTCPConnector;

public class ArucoPoseSubscriber : MonoBehaviour
{
    public string topicName;

    // 単位：1秒あたりの追従率
    public float followSpeed = 15.0f;

    private Vector3 targetPosition;

    void Start()
    {
        ROSConnection.GetOrCreateInstance()
            .Subscribe<PoseStampedMsg>(topicName, PoseCallback);

        targetPosition = transform.position;
    }

    void PoseCallback(PoseStampedMsg msg)
    {
        float x = (float)msg.pose.position.x;
        float y = (float)msg.pose.position.y;

        // XY 平面として使用
        targetPosition = new Vector3(x, y, 0.5f);
    }

    void Update()
    {
        transform.position = Vector3.Lerp(
            transform.position,
            targetPosition,
            1.0f - Mathf.Exp(-followSpeed * Time.deltaTime)
        );
    }
}
