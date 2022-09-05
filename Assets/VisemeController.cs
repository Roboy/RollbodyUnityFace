using System.Collections;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// This class triggers the blendshapes by values catched from ROS
/// </summary>
public class VisemeController : MonoBehaviour
{
    SkinnedMeshRenderer skinnedMeshRenderer;
    Mesh skinnedMesh;
    public int visemeIndex;
    public float triggerValue;

    private int visemeIndexBefore;


    void Awake()
    {
        skinnedMeshRenderer = GetComponent<SkinnedMeshRenderer>();
        skinnedMesh = GetComponent<SkinnedMeshRenderer>().sharedMesh;
    }

    void Start()
    {
       
    }

    void Update()
    {

        // TODO: here should the two Values (VisemeIndex & TriggerValue) be catched from ROS



        // trigger new Viseme
        skinnedMeshRenderer.SetBlendShapeWeight(visemeIndex, triggerValue);

        // when viseme change, set old viseme to 0
        if (visemeIndexBefore != visemeIndex)
            skinnedMeshRenderer.SetBlendShapeWeight(visemeIndexBefore, 0);
        // store Index
        visemeIndexBefore = visemeIndex;

    }
}