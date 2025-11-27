using UnityEngine;

// Provided by Unity ML-Agents Examples
public class WalkerSoccerSettings : MonoBehaviour
{
    public Material purpleMaterial;
    public Material blueMaterial;
    public bool randomizePlayersTeamForTraining = true;
    public float agentRunSpeed;
    public bool enableStartStabilization = true;
    public int stabilizeStepsOnReset = 10;
    public int solverIterations = 12;
    public int solverVelocityIterations = 12;
    public float standStrength = 0.9f;
}
