using System.Collections.Generic;
using Unity.MLAgents;
using UnityEngine;
using Unity.MLAgentsExamples; // Required for JointDriveController

// Inspired by Unity ML-Agents Examples
public class WalkerSoccerEnvController : MonoBehaviour
{
    [System.Serializable]
    public class PlayerInfo
    {
        public WalkerSoccerAgent Agent;
        [HideInInspector]
        public Vector3 StartingPos;
        [HideInInspector]
        public Quaternion StartingRot;
        [HideInInspector]
        public Rigidbody Rb;
    }


    /// <summary>
    /// Max Academy steps before this platform resets
    /// </summary>
    [Tooltip("Max Environment Steps")] public int MaxEnvironmentSteps = 25000;

    /// <summary>
    /// The area bounds.
    /// </summary>

    /// <summary>
    /// We will be changing the ground material based on success/failue
    /// </summary>

    public GameObject ball;
    [HideInInspector]
    public Rigidbody ballRb;
    Vector3 m_BallStartingPos;

    //List of Agents On Platform
    public List<PlayerInfo> AgentsList = new List<PlayerInfo>();

    private WalkerSoccerSettings m_SoccerSettings;


    private SimpleMultiAgentGroup m_BlueAgentGroup;
    private SimpleMultiAgentGroup m_PurpleAgentGroup;

    private int m_ResetTimer;
    private EnvironmentParameters m_EnvParams;

    void Start()
    {

        m_SoccerSettings = FindFirstObjectByType<WalkerSoccerSettings>();
        m_EnvParams = Academy.Instance.EnvironmentParameters;
        // Initialize TeamManager
        m_BlueAgentGroup = new SimpleMultiAgentGroup();
        m_PurpleAgentGroup = new SimpleMultiAgentGroup();
        ballRb = ball.GetComponent<Rigidbody>();
        m_BallStartingPos = new Vector3(ball.transform.position.x, ball.transform.position.y, ball.transform.position.z);
        foreach (var item in AgentsList)
        {
            item.StartingPos = item.Agent.transform.position;
            item.StartingRot = item.Agent.transform.rotation;
            item.Rb = item.Agent.GetComponent<Rigidbody>();
            if (item.Agent.team == Team.Blue)
            {
                m_BlueAgentGroup.RegisterAgent(item.Agent);
            }
            else
            {
                m_PurpleAgentGroup.RegisterAgent(item.Agent);
            }
        }
        ResetScene();
    }

    void FixedUpdate()
    {
        m_ResetTimer += 1;
        if (m_ResetTimer >= MaxEnvironmentSteps && MaxEnvironmentSteps > 0)
        {
            m_BlueAgentGroup.GroupEpisodeInterrupted();
            m_PurpleAgentGroup.GroupEpisodeInterrupted();
            ResetScene();
        }
    }


    public void ResetBall()
    {
        // Get curriculum-controlled spawn radius
        float spawnRadius = m_EnvParams.GetWithDefault("ball_spawn_radius", 3.0f);

        // Random angle in circle
        float randomAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float randomDistance = Random.Range(0f, spawnRadius);

        var randomPosX = Mathf.Cos(randomAngle) * randomDistance;
        var randomPosZ = Mathf.Sin(randomAngle) * randomDistance;

        ball.transform.position = m_BallStartingPos + new Vector3(randomPosX, 0f, randomPosZ);
        ballRb.linearVelocity = Vector3.zero;
        ballRb.angularVelocity = Vector3.zero;

    }

    public void GoalTouched(Team scoredTeam)
    {
        // Gradually enable competitive self-play via curriculum parameter
        float selfPlayWeight = m_EnvParams.GetWithDefault("self_play_weight", 1.0f);

        // Base goal reward with time bonus
        float goalReward = 50 * (1 - (float)m_ResetTimer / MaxEnvironmentSteps);
        float penaltyReward = -10 * selfPlayWeight; // Scale penalty by self-play weight

        if (scoredTeam == Team.Blue)
        {
            m_BlueAgentGroup.AddGroupReward(goalReward);
            m_PurpleAgentGroup.AddGroupReward(penaltyReward);
        }
        else
        {
            m_PurpleAgentGroup.AddGroupReward(goalReward);
            m_BlueAgentGroup.AddGroupReward(penaltyReward);
        }
        m_PurpleAgentGroup.EndGroupEpisode();
        m_BlueAgentGroup.EndGroupEpisode();
        ResetScene();

    }


    public void ResetScene()
    {
        m_ResetTimer = 0;

        //Reset Agents
        foreach (var item in AgentsList)
        {
            var randomPosX = Random.Range(-5f, 5f);
            var newStartPos = item.Agent.initialPos + new Vector3(randomPosX, 0f, 0f);
            var rot = item.Agent.rotSign * Random.Range(80.0f, 100.0f);
            var newRot = Quaternion.Euler(0, rot, 0);
            item.Agent.transform.SetPositionAndRotation(newStartPos, newRot);

            // Reset body parts for walker agents
            var bodyParts = item.Agent.GetComponent<JointDriveController>();
            if (bodyParts != null)
            {
                foreach (var bodyPart in bodyParts.bodyPartsDict.Values)
                {
                    bodyPart.Reset(bodyPart);
                }
            }
        }

        //Reset Ball
        ResetBall();
    }
}
