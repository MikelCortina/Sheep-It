using System;
using UnityEngine;

[Serializable]
public struct GrassPoint
{
    public Vector3 position;
    public Vector3 normal;
    public float height;
    public float width;
    public float randomSeed;
}