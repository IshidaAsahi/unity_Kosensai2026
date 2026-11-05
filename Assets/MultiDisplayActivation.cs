using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MultiDisplay : MonoBehaviour
{
    private Camera targetCamera;
    private float referenceOrthographicSize;
    private const float ReferenceAspect = 16f / 9f;

    void Awake()
    {
        targetCamera = GetComponent<Camera>();
        if (targetCamera == null)
        {
            Debug.LogError("MultiDisplay requires a Camera component on the same GameObject.");
            enabled = false;
            return;
        }

        referenceOrthographicSize = targetCamera.orthographicSize;
    }

    // Start is called before the first frame update
    //複数のディスプレイ(プロジェクター)を有効化する
    void Start()
    {
        if (Display.displays.Length > 0)
        {
            Display.displays[0].Activate();
        }
        if (Display.displays.Length > 1)
        {
            Display.displays[1].Activate();
        }
    }

    // Update is called once per frame
    void LateUpdate()
    {
        if (!targetCamera.orthographic)
        {
            return;
        }

        // Keep the 16:9 reference area visible when the Game view is narrower.
        targetCamera.orthographicSize =
            referenceOrthographicSize * Mathf.Max(1f, ReferenceAspect / targetCamera.aspect);
    }
}
