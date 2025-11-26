using System.Collections.Generic;
using Unity.MLAgents;
using UnityEngine;
using Unity.MLAgentsExamples; // Added for JointDriveController

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

    // Ball touch state for passing/assist detection
    [HideInInspector]
    public WalkerSoccerAgent lastTouchAgent;
    [HideInInspector]
    public Team lastTouchTeam;
    [HideInInspector]
    public Vector3 lastTouchPos;
    [HideInInspector]
    public int lastTouchStep;

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

    /// <summary>
    /// Called by agents when they touch the ball. Detects simple passes and assigns rewards.
    /// </summary>
    public void RegisterBallTouch(WalkerSoccerAgent toucher, Vector3 contactPos, int stepCount)
    {
        if (toucher == null) return;

        // Read lesson parameters
        float passingScale = m_EnvParams.GetWithDefault("passing_scale", 0.0f);
        float minPassDist = m_EnvParams.GetWithDefault("min_pass_dist", 2.5f);
        float assistWindow = m_EnvParams.GetWithDefault("assist_window", 60f); // in steps

        // Basic pass detection: teammate-to-teammate within window and distance
        bool validPrev = lastTouchAgent != null && lastTouchTeam == toucher.team;
        bool withinWindow = validPrev && (stepCount - lastTouchStep) <= assistWindow;
        float traveled = validPrev ? Vector3.Distance(lastTouchPos, contactPos) : 0f;
        bool enoughDistance = traveled >= minPassDist;

        if (passingScale > 0f && validPrev && withinWindow && enoughDistance)
        {
            // Reward passer and receiver (heavier to passer)
            float baseR = 0.2f * passingScale;
            lastTouchAgent.AddReward(baseR);       // passer
            toucher.AddReward(0.1f * passingScale); // receiver
        }

        // Update last touch state
        lastTouchAgent = toucher;
        lastTouchTeam = toucher.team;
        lastTouchPos = contactPos;
        lastTouchStep = stepCount;
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
        if (scoredTeam == Team.Blue)
        {
            // Goal reward: 50× base + time bonus. Baseline standing reward ~26, so goals are 2-3× more valuable.
            // Fast goals (early episode) get up to 50× reward, incentivizing aggressive play.
            m_BlueAgentGroup.AddGroupReward(50 * (1 - (float)m_ResetTimer / MaxEnvironmentSteps));
            m_PurpleAgentGroup.AddGroupReward(-10);
        }
        else
        {
            m_PurpleAgentGroup.AddGroupReward(50 * (1 - (float)m_ResetTimer / MaxEnvironmentSteps));
            m_BlueAgentGroup.AddGroupReward(-10);
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
