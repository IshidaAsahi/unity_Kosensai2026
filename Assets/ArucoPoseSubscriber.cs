using UnityEngine;
using RosMessageTypes.Geometry;
using Unity.Robotics.ROSTCPConnector;
public enum Rotation
{
    R0, R90, R180, R270
}


public class ArucoPoseSubscriber : MonoBehaviour
{

    public string topicName;

    // 単位：1秒あたりの追従率
    public float followSpeed = 15.0f;
    [SerializeField] Rotation rotation;

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
        Vector3 v;
        switch (rotation)
        {
            case Rotation.R0:
                v = new Vector3(x, y, 0.5f);
                break;
            case Rotation.R90:
                v = new Vector3(-y, x, 0.5f);
                break;
            case Rotation.R180:
                v = new Vector3(-x, -y, 0.5f);
                break;
            case Rotation.R270:
                v = new Vector3(y, -x, 0.5f);
                break;
            default:
                v = new Vector3(x, y, 0.5f);
                break;
        }

        // XY 平面として使用
        targetPosition = v;

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
