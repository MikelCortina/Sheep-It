using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct GrassPoint
{
    public Vector3 position;
    public Vector3 normal;
    public float height;
    public float width;
    public float randomSeed;
    public Color tint;
}

[Serializable]
public class GrassLogicCell
{
    public Vector3 center;
    public List<int> bladeIndices = new();
    public List<float> bladeBaseHeights = new();

    [Range(0f, 1f)]
    public float grassAmount = 1f;

    // ? Valores seguros por defecto — nunca llegan a 0 aunque Unity ignore [NonSerialized]
    [NonSerialized] public float consumeRate = 0.08f;
    [NonSerialized] public float regenRate = 0f;
    [NonSerialized] public float minGrassToGraze = 0.05f;
    [NonSerialized] public int maxSheepEating = 1;

    // ? Altura media de las briznas base — para saber si la celda es "short grass"
    [NonSerialized] public float averageBaseHeight = 1f;

    [NonSerialized] public int _grazers = 0;
    [NonSerialized] public int _reserved = 0;

    public bool IsGrazeable => grassAmount > minGrassToGraze;
    public int UsedSlots => _grazers + _reserved;
    public bool HasFreeSlot => UsedSlots < Mathf.Max(1, maxSheepEating);
    public bool CanBeReserved => IsGrazeable && HasFreeSlot;

    public bool TryReserve()
    {
        if (!CanBeReserved) return false;
        _reserved++;
        return true;
    }

    public void CancelReservation() =>
        _reserved = Mathf.Max(0, _reserved - 1);

    public bool TryStartGrazing()
    {
        if (!IsGrazeable) return false;
        if (_reserved > 0) { _reserved--; _grazers++; return true; }
        if (!HasFreeSlot) return false;
        _grazers++;
        return true;
    }

    public void StopGrazing() =>
        _grazers = Mathf.Max(0, _grazers - 1);
}