using UnityEngine;

public static class CombatProjectileTrajectory
{
    public static Vector3 EvaluatePosition(
        ShotSample shotSample,
        float elapsedTimeSeconds
    )
    {
        return shotSample.OriginWorld
            + shotSample.InitialVelocityWorld * elapsedTimeSeconds
            + 0.5f
                * shotSample.GravityWorld
                * elapsedTimeSeconds
                * elapsedTimeSeconds;
    }


    public static Vector3 EvaluateVelocity(
        ShotSample shotSample,
        float elapsedTimeSeconds
    )
    {
        return shotSample.InitialVelocityWorld
            + shotSample.GravityWorld * elapsedTimeSeconds;
    }
}
