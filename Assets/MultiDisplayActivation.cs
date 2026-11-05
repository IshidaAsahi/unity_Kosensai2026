using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MultiDisplay : MonoBehaviour
{
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
        if (Display.displays.Length > 2)
        {
            Display.displays[2].Activate();
        }
    }

    // Update is called once per frame
    void Update()
    {

    }
}
