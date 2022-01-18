using System;
using UnityEngine;

[Serializable]
public struct SnapShot
{
    public AgentData[] agentDatas;
}

[Serializable]
public struct AgentData
{
    public string id;
    public Vector3 position;
    public Quaternion rotation;
    public float speed;
    public string trigger;
}
