using UnityEngine;

public readonly struct CombatDamageResult
{
    internal CombatDamageResult(
        bool accepted,
        bool applied,
        ShotSample sourceShot,
        GameObject targetShipRoot,
        bool hasHitRegion,
        CombatHullRegion hitRegion,
        bool hasRakingResult,
        CombatRakingResult rakingResult,
        float requestedDamage,
        float appliedDamage,
        bool hasIntegrityTransition,
        ShipIntegrityTransition integrityTransition
    )
    {
        Accepted = accepted;
        Applied = applied;
        SourceShot = sourceShot;
        TargetShipRoot = targetShipRoot;
        HasHitRegion = hasHitRegion;
        HitRegion = hitRegion;
        HasRakingResult = hasRakingResult;
        RakingResult = rakingResult;
        RequestedDamage = requestedDamage;
        AppliedDamage = appliedDamage;
        HasIntegrityTransition = hasIntegrityTransition;
        IntegrityTransition = integrityTransition;
    }


    public bool Accepted { get; }

    public bool Applied { get; }

    public ShotSample SourceShot { get; }

    public GameObject TargetShipRoot { get; }

    public bool HasHitRegion { get; }

    public CombatHullRegion HitRegion { get; }

    public bool HasRakingResult { get; }

    public CombatRakingResult RakingResult { get; }

    public bool IsRaking =>
        HasRakingResult && RakingResult.IsRaking;

    public CombatRakingType RakingType =>
        HasRakingResult
            ? RakingResult.Type
            : CombatRakingType.None;

    public float LongitudinalAngleDegrees =>
        HasRakingResult
            ? RakingResult.LongitudinalAngleDegrees
            : 0f;

    public float RakingMultiplier =>
        HasRakingResult ? RakingResult.DamageMultiplier : 0f;

    public float RequestedDamage { get; }

    public float AppliedDamage { get; }

    public bool HasIntegrityTransition { get; }

    public ShipIntegrityTransition IntegrityTransition { get; }

    public float PreviousIntegrity =>
        IntegrityTransition.PreviousIntegrity;

    public float CurrentIntegrity =>
        IntegrityTransition.CurrentIntegrity;

    public ShipCombatLifecycleState PreviousLifecycleState =>
        IntegrityTransition.PreviousLifecycleState;

    public ShipCombatLifecycleState CurrentLifecycleState =>
        IntegrityTransition.CurrentLifecycleState;

    public bool LifecycleChanged =>
        IntegrityTransition.LifecycleChanged;
}
