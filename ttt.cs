using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ttt : MonoBehaviour
{
    public AssetInfo asd;

    void Start()
    {
        asd.Instantiate<GameObject>();
    }
}
