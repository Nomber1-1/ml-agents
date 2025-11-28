using System;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgentsExamples;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Policies;
using BodyPart = Unity.MLAgentsExamples.BodyPart;
using Random = UnityEngine.Random;

public class WalkerSoccerAgent : Agent
{
    // ============================================
    // SOCCER-SPECIFIC PROPERTIES
    // ============================================
    public enum Position
    {
        Striker,
        Goalie,
        Generic
    }

    [Header("Soccer Settings")]
    [HideInInspector] public Team team;
    public Position position;

    [HideInInspector] public Vector3 initialPos;
    [HideInInspector] public float rotSign;

    // Dynamic team size detection
    private int m_TotalAgentCount = 0;
    private int m_AgentsPerTeam = 0;

    private float m_Existential;
    private float m_BallTouch;
    private const float k_KickPower = 14000f;

    private BehaviorParameters m_BehaviorParameters;
    private bool m_LocomotionOnly; // Stage 1: true (locomotion), Stage 2: false (soccer)

    [Header("Kick Action Settings")]
    [SerializeField] private float kickForce = 12.0f; // Stronger impulse applied to ball
    [SerializeField] private float kickUpFactor = 0.3f; // Adds slight upward component
    [SerializeField] private float kickRange = 1.5f; // Horizontal distance within which kick can trigger
    [SerializeField] private int kickCooldownSteps = 25; // Steps between kicks to avoid spam
    [SerializeField] private float kickReward = 0.3f; // Reward for successful intentional kick (increased)
    private int m_LastKickStep = -999;
    private int m_LastBallTouchStep = -999;
    private int m_BallNearStuckSteps = 0;
    private Transform opponentGoal;
    private Vector3 m_LastBallPos;
    private float m_LastBallDistToOppGoal;
    private Vector3 m_LastBallVelocity;
    // Cached lists for nearby agents
    private System.Collections.Generic.List<Transform> m_Teammates = new System.Collections.Generic.List<Transform>();
    private System.Collections.Generic.List<Transform> m_Opponents = new System.Collections.Generic.List<Transform>();

    [Header("Anti-Piling")]
    [SerializeField] private float cornerPenaltyRadius = 8f; // Distance from arena center
    [SerializeField] private float cornerPenaltyStrength = 0.01f;
    [SerializeField] private Transform arenaCenter; // Assign in Inspector

    private Vector3 m_BallStuckCheckPos;
    private int m_BallStuckSteps = 0;
    private const int BALL_STUCK_THRESHOLD = 200; // ~20 seconds at 10 steps/sec

    // Ball wall contact and pile detection
    private int m_BallWallContactSteps = 0;
    private const int BALL_WALL_THRESHOLD = 100; // ~10 seconds
    private int m_BallAgentPileSteps = 0;
    private const int BALL_AGENT_PILE_THRESHOLD = 150; // ~15 seconds of sustained piling

    // Agent wall stuck detection
    private int m_AgentWallStuckSteps = 0;
    private const int AGENT_WALL_STUCK_THRESHOLD = 50; // ~5 seconds
    private Vector3 m_LastAgentPos;
    private float m_AgentStuckMoveThreshold = 0.3f; // Movement less than this = stuck
    // Dive detection near ball
    private int m_DiveNearBallSteps = 0;
    private const int DIVE_NEAR_BALL_RESET_THRESHOLD = 20; // ~4.5 seconds at 10 steps/sec

    // Goalie positioning
    private Transform myGoal;
    [Header("Goalie Settings")]
    [SerializeField] private float goalieMaxDistance = 3.2f; // Relaxed leash to reduce incidental penalties
    [SerializeField] private float goaliePenaltyStrength = 0.012f; // Further softened from 0.015f

    // ============================================
    // WALKER LOCOMOTION PROPERTIES
    // ============================================
    [Header("Walk Speed")]
    [Range(0.1f, 10)]
    [SerializeField]
    //The walking speed to try and achieve
    private float m_TargetWalkingSpeed = 10;

    public float MTargetWalkingSpeed // property
    {
        get { return m_TargetWalkingSpeed; }
        set { m_TargetWalkingSpeed = Mathf.Clamp(value, .1f, m_maxWalkingSpeed); }
    }

    const float m_maxWalkingSpeed = 10; //The max walking speed

    //Should the agent sample a new goal velocity each episode?
    //If true, walkSpeed will be randomly set between zero and m_maxWalkingSpeed in OnEpisodeBegin()
    //If false, the goal velocity will be walkingSpeed
    public bool randomizeWalkSpeedEachEpisode;

    //The direction an agent will walk during training - now influenced by soccer ball position
    private Vector3 m_WorldDirToWalk = Vector3.right;

    [Header("Soccer Ball Target")]
    public Transform ball; // The soccer ball to pursue

    [Header("Target To Walk Towards")]
    public Transform target; //Target the agent will walk towards during training.

    [Header("Body Parts")] public Transform hips;
    public Transform chest;
    public Transform spine;
    public Transform head;
    public Transform thighL;
    public Transform shinL;
    public Transform footL;
    public Transform thighR;
    public Transform shinR;
    public Transform footR;
    public Transform armL;
    public Transform forearmL;
    public Transform handL;
    public Transform armR;
    public Transform forearmR;
    public Transform handR;

    //This will be used as a stabilized model space reference point for observations
    //Because ragdolls can move erratically during training, using a stabilized reference transform improves learning
    OrientationCubeController m_OrientationCube;

    //The indicator graphic gameobject that points towards the target
    DirectionIndicator m_DirectionIndicator;
    JointDriveController m_JdController;
    EnvironmentParameters m_ResetParams;

    // Stage 1 locomotion anti-exploit tuning
    [Header("Locomotion Success Gating")]
    [SerializeField] private float uprightDotThreshold = 0.85f; // Must be mostly upright to count touch
    [SerializeField] private float maxSuccessSpeed = 2.0f;       // Walk pace threshold to avoid dive touches
    [Header("Spawn Area")]
    [SerializeField] private float spawnAreaHalfX = 6f;          // Randomize agent start X within [-half, +half]
    [SerializeField] private float spawnAreaHalfZ = 6f;          // Randomize agent start Z within [-half, +half]

    public override void Initialize()
    {
        // Soccer initialization
        WalkerSoccerEnvController envController = GetComponentInParent<WalkerSoccerEnvController>();
        if (envController != null)
        {
            m_Existential = 1f / envController.MaxEnvironmentSteps;
        }
        else
        {
            m_Existential = 1f / MaxStep;
        }

        m_BehaviorParameters = gameObject.GetComponent<BehaviorParameters>();
        if (m_BehaviorParameters.TeamId == (int)Team.Blue)
        {
            team = Team.Blue;
            initialPos = new Vector3(transform.position.x, transform.position.y, transform.position.z);
            rotSign = 1f;
            var opp = GameObject.FindGameObjectWithTag("purpleGoal");
            opponentGoal = opp != null ? opp.transform : null;
            var own = GameObject.FindGameObjectWithTag("blueGoal");
            myGoal = own != null ? own.transform : null;
        }
        else
        {
            team = Team.Purple;
            initialPos = new Vector3(transform.position.x, transform.position.y, transform.position.z);
            rotSign = -1f;
            var opp = GameObject.FindGameObjectWithTag("blueGoal");
            opponentGoal = opp != null ? opp.transform : null;
            var own = GameObject.FindGameObjectWithTag("purpleGoal");
            myGoal = own != null ? own.transform : null;
        }

        // Walker initialization
        m_OrientationCube = GetComponentInChildren<OrientationCubeController>();
        m_DirectionIndicator = GetComponentInChildren<DirectionIndicator>();

        //Setup each body part
        m_JdController = GetComponent<JointDriveController>();
        m_JdController.SetupBodyPart(hips);
        m_JdController.SetupBodyPart(chest);
        m_JdController.SetupBodyPart(spine);
        m_JdController.SetupBodyPart(head);
        m_JdController.SetupBodyPart(thighL);
        m_JdController.SetupBodyPart(shinL);
        m_JdController.SetupBodyPart(footL);
        m_JdController.SetupBodyPart(thighR);
        m_JdController.SetupBodyPart(shinR);
        m_JdController.SetupBodyPart(footR);
        m_JdController.SetupBodyPart(armL);
        m_JdController.SetupBodyPart(forearmL);
        m_JdController.SetupBodyPart(handL);
        m_JdController.SetupBodyPart(armR);
        m_JdController.SetupBodyPart(forearmR);
        m_JdController.SetupBodyPart(handR);

        m_ResetParams = Academy.Instance.EnvironmentParameters;
        m_LastBallTouchStep = -999;
        m_BallNearStuckSteps = 0;
        m_BallStuckSteps = 0;
        m_BallStuckCheckPos = Vector3.zero;
        m_BallWallContactSteps = 0;
        m_BallAgentPileSteps = 0;
        m_AgentWallStuckSteps = 0;
        m_LastAgentPos = transform.position;

        // Detect team size for dynamic threshold adjustment -> For Testing 2v2 / 3v3 / etc
        var allAgents = GameObject.FindGameObjectsWithTag("agent");
        m_TotalAgentCount = allAgents.Length;
        m_AgentsPerTeam = m_TotalAgentCount / 2; // Assuming equal teams

        // Build teammate/opponent caches
        m_Teammates.Clear();
        m_Opponents.Clear();
        foreach (var go in allAgents)
        {
            if (go == this.gameObject) continue;
            var bp = go.GetComponent<BehaviorParameters>();
            if (bp == null) continue;
            bool isBlue = bp.TeamId == (int)Team.Blue;
            bool sameTeam = (team == Team.Blue && isBlue) || (team == Team.Purple && !isBlue);
            if (sameTeam)
            {
                m_Teammates.Add(go.transform);
            }
            else
            {
                m_Opponents.Add(go.transform);
            }
        }
    }

    /// <summary>
    /// Loop over body parts and reset them to initial conditions.
    /// </summary>
    public override void OnEpisodeBegin()
    {
        // Check if we're in locomotion-only training (Stage 1) or full soccer (Stage 2)
        m_LocomotionOnly = m_ResetParams.GetWithDefault("locomotion_only", 0f) > 0.5f;

        // Soccer-specific reset parameters
        m_BallTouch = m_ResetParams.GetWithDefault("ball_touch", 0);

        //Reset all of the body parts
        foreach (var bodyPart in m_JdController.bodyPartsDict.Values)
        {
            bodyPart.Reset(bodyPart);
        }

        var hipsBp = m_JdController.bodyPartsDict[hips];

        // Stage 1: Randomize spawn to prevent dive-reset exploits
        // Stage 2: Use fixed spawn position for consistent soccer setup
        if (m_LocomotionOnly)
        {
            // Randomize agent spawn position and facing to prevent dive-reset exploits
            Vector3 baseAreaCenter = transform.position;
            float randX = Random.Range(-spawnAreaHalfX, spawnAreaHalfX);
            float randZ = Random.Range(-spawnAreaHalfZ, spawnAreaHalfZ);
            Vector3 spawnPos = new Vector3(baseAreaCenter.x + randX, baseAreaCenter.y, baseAreaCenter.z + randZ);
            hipsBp.rb.transform.position = spawnPos;

            // Random start rotation to help generalize
            hipsBp.rb.transform.rotation = Quaternion.Euler(0, Random.Range(0.0f, 360.0f), 0);
        }
        else
        {
            // Stage 2: Use initialPos for consistent soccer positioning
            hipsBp.rb.transform.position = initialPos;
            hipsBp.rb.transform.rotation = Quaternion.Euler(0, rotSign * 90f, 0);
        }

        // Zero initial velocities
        hipsBp.rb.linearVelocity = Vector3.zero;
        hipsBp.rb.angularVelocity = Vector3.zero;

        UpdateOrientationObjects();

        //Set our goal walking speed
        MTargetWalkingSpeed =
            randomizeWalkSpeedEachEpisode ? Random.Range(0.1f, m_maxWalkingSpeed) : MTargetWalkingSpeed;

        // Reset ball progress tracking
        m_LastBallPos = ball != null ? ball.position : Vector3.zero;
        m_LastBallVelocity = Vector3.zero;
        if (ball != null && opponentGoal != null)
        {
            m_LastBallDistToOppGoal = Vector3.Distance(ball.position, opponentGoal.position);
        }
        else
        {
            m_LastBallDistToOppGoal = 0f;
        }
        m_LastBallTouchStep = -999;
        m_BallNearStuckSteps = 0;
        m_BallStuckSteps = 0;
        m_BallStuckCheckPos = ball != null ? ball.position : Vector3.zero;
        m_BallWallContactSteps = 0;
        m_BallAgentPileSteps = 0;
        m_AgentWallStuckSteps = 0;
        m_LastAgentPos = hipsBp.rb.transform.position;
    }

    /// <summary>
    /// Add relevant information on each body part to observations.
    /// </summary>
    public void CollectObservationBodyPart(BodyPart bp, VectorSensor sensor)
    {
        //GROUND CHECK
        sensor.AddObservation(bp.groundContact.touchingGround); // Is this bp touching the ground

        //Get velocities in the context of our orientation cube's space
        //Note: You can get these velocities in world space as well but it may not train as well.
        sensor.AddObservation(m_OrientationCube.transform.InverseTransformDirection(bp.rb.linearVelocity));
        sensor.AddObservation(m_OrientationCube.transform.InverseTransformDirection(bp.rb.angularVelocity));

        //Get position relative to hips in the context of our orientation cube's space
        sensor.AddObservation(m_OrientationCube.transform.InverseTransformDirection(bp.rb.position - hips.position));

        if (bp.rb.transform != hips && bp.rb.transform != handL && bp.rb.transform != handR)
        {
            sensor.AddObservation(bp.rb.transform.localRotation);
            sensor.AddObservation(bp.currentStrength / m_JdController.maxJointForceLimit);
        }
    }

    /// <summary>
    /// Loop over body parts to add them to observation.
    /// Always 269 observations for proper transfer learning between stages.
    /// Stage 1 (locomotion_only=1): Soccer observations set to zero (ignored during training)
    /// Stage 2 (locomotion_only=0): Soccer observations contain real data
    /// </summary>
    public override void CollectObservations(VectorSensor sensor)
    {
        var cubeForward = m_OrientationCube.transform.forward;

        //velocity we want to match
        var velGoal = cubeForward * MTargetWalkingSpeed;
        //ragdoll's avg vel
        var avgVel = GetAvgVelocity();

        //current ragdoll velocity. normalized
        sensor.AddObservation(Vector3.Distance(velGoal, avgVel));
        //avg body vel relative to cube
        sensor.AddObservation(m_OrientationCube.transform.InverseTransformDirection(avgVel));
        //vel goal relative to cube
        sensor.AddObservation(m_OrientationCube.transform.InverseTransformDirection(velGoal));

        //rotation deltas
        sensor.AddObservation(Quaternion.FromToRotation(hips.forward, cubeForward));
        sensor.AddObservation(Quaternion.FromToRotation(head.forward, cubeForward));

        // Target position observation:
        // Stage 1 (locomotion_only): keep target position (locomotion objective).
        // Stage 2 (soccer): target == ball; omit duplicate by supplying zero placeholder to preserve observation count.
        if (m_LocomotionOnly)
        {
            sensor.AddObservation(m_OrientationCube.transform.InverseTransformPoint(target.transform.position));
        }
        else
        {
            sensor.AddObservation(Vector3.zero); // placeholder (removed redundancy)
        }

        // Soccer-specific observations
        // Previous: 7 floats (ball pos 3 + ball vel 3 + team 1)
        // Added: role (1), myGoal pos (3), oppGoal pos (3), up to 2 teammate positions (2x3), up to 2 opponent positions (2x3)
        // Total added = 19; new soccer obs total = 26
        if (m_LocomotionOnly)
        {
            // Stage 1: Dummy soccer observations (all zeros)
            sensor.AddObservation(Vector3.zero); // Ball position
            sensor.AddObservation(Vector3.zero); // Ball velocity
            sensor.AddObservation(0f); // Team identifier
            sensor.AddObservation(0f); // Role
            sensor.AddObservation(Vector3.zero); // My goal
            sensor.AddObservation(Vector3.zero); // Opponent goal
            sensor.AddObservation(Vector3.zero); // Teammate 1
            sensor.AddObservation(Vector3.zero); // Teammate 2
            sensor.AddObservation(Vector3.zero); // Opponent 1
            sensor.AddObservation(Vector3.zero); // Opponent 2
        }
        else
        {
            // Stage 2: Real soccer observations
            // Ball position relative to agent
            if (ball != null)
            {
                sensor.AddObservation(m_OrientationCube.transform.InverseTransformPoint(ball.position));

                // Ball velocity
                Rigidbody ballRb = ball.GetComponent<Rigidbody>();
                if (ballRb != null)
                {
                    sensor.AddObservation(m_OrientationCube.transform.InverseTransformDirection(ballRb.linearVelocity));
                }
                else
                {
                    sensor.AddObservation(Vector3.zero);
                }
            }
            else
            {
                sensor.AddObservation(Vector3.zero); // ball position
                sensor.AddObservation(Vector3.zero); // ball velocity
            }

            // Team identifier
            sensor.AddObservation(team == Team.Blue ? 1f : -1f);

            // Role (Striker=1, Goalie=-1, Generic=0)
            float roleVal = position == Position.Striker ? 1f : (position == Position.Goalie ? -1f : 0f);
            sensor.AddObservation(roleVal);

            // Goal positions
            sensor.AddObservation(myGoal != null
                ? m_OrientationCube.transform.InverseTransformPoint(myGoal.position)
                : Vector3.zero);
            sensor.AddObservation(opponentGoal != null
                ? m_OrientationCube.transform.InverseTransformPoint(opponentGoal.position)
                : Vector3.zero);

            // Teammates (up to 2)
            Vector3 tm1 = Vector3.zero, tm2 = Vector3.zero;
            if (m_Teammates.Count > 0)
                tm1 = m_OrientationCube.transform.InverseTransformPoint(m_Teammates[0].position);
            if (m_Teammates.Count > 1)
                tm2 = m_OrientationCube.transform.InverseTransformPoint(m_Teammates[1].position);
            sensor.AddObservation(tm1);
            sensor.AddObservation(tm2);

            // Opponents (up to 2)
            Vector3 op1 = Vector3.zero, op2 = Vector3.zero;
            if (m_Opponents.Count > 0)
                op1 = m_OrientationCube.transform.InverseTransformPoint(m_Opponents[0].position);
            if (m_Opponents.Count > 1)
                op2 = m_OrientationCube.transform.InverseTransformPoint(m_Opponents[1].position);
            sensor.AddObservation(op1);
            sensor.AddObservation(op2);
        }

        // Body part observations (same for both stages)
        foreach (var bodyPart in m_JdController.bodyPartsList)
        {
            CollectObservationBodyPart(bodyPart, sensor);
        }
    }

    public override void OnActionReceived(ActionBuffers actionBuffers)

    {
        var bpDict = m_JdController.bodyPartsDict;
        var i = -1;

        var continuousActions = actionBuffers.ContinuousActions;
        bpDict[chest].SetJointTargetRotation(continuousActions[++i], continuousActions[++i], continuousActions[++i]);
        bpDict[spine].SetJointTargetRotation(continuousActions[++i], continuousActions[++i], continuousActions[++i]);

        bpDict[thighL].SetJointTargetRotation(continuousActions[++i], continuousActions[++i], 0);
        bpDict[thighR].SetJointTargetRotation(continuousActions[++i], continuousActions[++i], 0);
        bpDict[shinL].SetJointTargetRotation(continuousActions[++i], 0, 0);
        bpDict[shinR].SetJointTargetRotation(continuousActions[++i], 0, 0);
        bpDict[footR].SetJointTargetRotation(continuousActions[++i], continuousActions[++i], continuousActions[++i]);
        bpDict[footL].SetJointTargetRotation(continuousActions[++i], continuousActions[++i], continuousActions[++i]);

        bpDict[armL].SetJointTargetRotation(continuousActions[++i], continuousActions[++i], 0);
        bpDict[armR].SetJointTargetRotation(continuousActions[++i], continuousActions[++i], 0);
        bpDict[forearmL].SetJointTargetRotation(continuousActions[++i], 0, 0);
        bpDict[forearmR].SetJointTargetRotation(continuousActions[++i], 0, 0);
        bpDict[head].SetJointTargetRotation(continuousActions[++i], continuousActions[++i], 0);

        //update joint strength settings
        bpDict[chest].SetJointStrength(continuousActions[++i]);
        bpDict[spine].SetJointStrength(continuousActions[++i]);
        bpDict[head].SetJointStrength(continuousActions[++i]);
        bpDict[thighL].SetJointStrength(continuousActions[++i]);
        bpDict[shinL].SetJointStrength(continuousActions[++i]);
        bpDict[footL].SetJointStrength(continuousActions[++i]);
        bpDict[thighR].SetJointStrength(continuousActions[++i]);
        bpDict[shinR].SetJointStrength(continuousActions[++i]);
        bpDict[footR].SetJointStrength(continuousActions[++i]);
        bpDict[armL].SetJointStrength(continuousActions[++i]);
        bpDict[forearmL].SetJointStrength(continuousActions[++i]);
        bpDict[armR].SetJointStrength(continuousActions[++i]);
        bpDict[forearmR].SetJointStrength(continuousActions[++i]);

        // Optional kick action (additional continuous action at end if present)
        // Stage 1 (locomotion_only): Kick action ignored
        // Stage 2 (soccer): Kick enabled in Lesson 3+ (ball_touch >= 0.5)
        if (continuousActions.Length > i + 1)
        {
            float kickIntensity = continuousActions[++i]; // Expect value in [0,1]
            // Only attempt kick in Stage 2 + Lesson 3+
            if (!m_LocomotionOnly && m_BallTouch >= 0.5f)
            {
                TryKickBall(kickIntensity);
            }
        }
    }

    //Update OrientationCube and DirectionIndicator
    void UpdateOrientationObjects()
    {
        // Prioritize ball position for soccer gameplay, fallback to target
        Transform targetToUse = (ball != null) ? ball : target;

        m_WorldDirToWalk = targetToUse.position - hips.position;
        m_OrientationCube.UpdateOrientation(hips, targetToUse);
        if (m_DirectionIndicator)
        {
            m_DirectionIndicator.MatchOrientation(m_OrientationCube.transform);
        }
    }

    void FixedUpdate()
    {
        UpdateOrientationObjects();

        var cubeForward = m_OrientationCube.transform.forward;

        // Set reward for this step according to mixture of the following elements.
        // a. Match target speed
        //This reward will approach 1 if it matches perfectly and approach zero as it deviates
        var matchSpeedReward = GetMatchingVelocityReward(cubeForward * MTargetWalkingSpeed, GetAvgVelocity());

        //Check for NaNs
        if (float.IsNaN(matchSpeedReward))
        {
            throw new ArgumentException(
                "NaN in moveTowardsTargetReward.\n" +
                $" cubeForward: {cubeForward}\n" +
                $" hips.velocity: {m_JdController.bodyPartsDict[hips].rb.linearVelocity}\n" +
                $" maximumWalkingSpeed: {m_maxWalkingSpeed}"
            );
        }

        // b. Rotation alignment with target direction.
        //This reward will approach 1 if it faces the target direction perfectly and approach zero as it deviates
        var headForward = head.forward;
        headForward.y = 0;
        var lookAtTargetReward = (Vector3.Dot(cubeForward, headForward) + 1) * .5F;

        //Check for NaNs
        if (float.IsNaN(lookAtTargetReward))
        {
            throw new ArgumentException(
                "NaN in lookAtTargetReward.\n" +
                $" cubeForward: {cubeForward}\n" +
                $" head.forward: {head.forward}"
            );
        }

        // Scale locomotion reward in Stage 2 so soccer signals (touches, goals, kicks)
        // can dominate. In Stage 1, keep full locomotion reward.
        float locomotionScale = m_LocomotionOnly ? 1f : Mathf.Clamp01(m_ResetParams.GetWithDefault("locomotion_scale", 0.5f));
        AddReward(locomotionScale * matchSpeedReward * lookAtTargetReward);

        // Stage 2: ball-to-goal shaping rewards and anti-stall near ball
        if (!m_LocomotionOnly && ball != null && opponentGoal != null)
        {
            var currDist = Vector3.Distance(ball.position, opponentGoal.position);
            var progress = m_LastBallDistToOppGoal - currDist; // positive if closer to goal
            if (progress > 0f)
            {
                AddReward(Mathf.Clamp(progress * 0.03f, 0f, 0.15f));
            }
            m_LastBallDistToOppGoal = currDist;

            var ballRb = ball.GetComponent<Rigidbody>();
            if (ballRb != null)
            {
                var toGoalDir = (opponentGoal.position - ball.position).normalized;
                var speedAlong = Vector3.Dot(ballRb.linearVelocity, toGoalDir);
                if (speedAlong > 0f)
                {
                    AddReward(Mathf.Clamp(speedAlong, 0f, 10f) * 0.0075f);
                }

                // Discourage ball movement toward own goal (stronger penalty)
                if (myGoal != null)
                {
                    var toOwnDir = (myGoal.position - ball.position).normalized;
                    var speedTowardOwn = Vector3.Dot(ballRb.linearVelocity, toOwnDir);
                    if (speedTowardOwn > 0f)
                    {
                        AddReward(-Mathf.Clamp(speedTowardOwn, 0f, 10f) * 0.04f); // Stronger penalty
                    }

                    // Penalty for ball position close to own goal (not scored)
                    float distToOwnGoal = Vector3.Distance(ball.position, myGoal.position);
                    if (distToOwnGoal < 6f)
                    {
                        AddReward(-0.08f * (6f - distToOwnGoal)); // Up to -0.48 if ball is very close
                    }

                    // Bonus for ball progress away from own goal
                    Vector3 ballToOppGoal = (opponentGoal.position - ball.position).normalized;
                    float progressAway = Vector3.Dot(ballRb.linearVelocity, ballToOppGoal);
                    if (progressAway > 0f)
                    {
                        AddReward(progressAway * 0.01f); // Small bonus for moving ball toward opponent goal
                    }
                }

                var toBall = ball.position - hips.position;
                float horizDist = new Vector2(toBall.x, toBall.z).magnitude;
                float ballSpeedMag = ballRb.linearVelocity.magnitude;
                // Anti-dive posture shaping near ball: discourage forward falls
                // Compute uprightness (hips up vs world up) and penalize if below threshold when close to ball
                float upDot = Mathf.Clamp01(Vector3.Dot(hips.up, Vector3.up));
                if (horizDist < 2.0f)
                {
                    // Small bonus for staying upright near ball
                    if (upDot >= uprightDotThreshold)
                    {
                        AddReward(0.02f);
                        // Bonus for recovering from a fall (get-up behavior)
                        if (m_DiveNearBallSteps > 0)
                        {
                            AddReward(0.08f); // Reward for getting up
                        }
                        m_DiveNearBallSteps = 0;
                    }
                    else
                    {
                        // Penalty scales with how far below threshold the posture is
                        float deficit = Mathf.Clamp01(uprightDotThreshold - upDot);
                        float downwardVel = Mathf.Max(0f, -GetAvgVelocity().y);
                        AddReward(-0.03f * (0.5f + deficit + 0.5f * downwardVel));
                        // Track sustained dive posture near ball
                        m_DiveNearBallSteps++;
                        // Remove early reset: let agent learn to get up
                        // If agent stays down too long, apply a small penalty
                        if (m_DiveNearBallSteps >= DIVE_NEAR_BALL_RESET_THRESHOLD)
                        {
                            AddReward(-0.08f); // Penalty for staying down too long
                        }
                    }
                }

                // Note: Positive kick/touch rewards are gated inside TryKickBall by posture already.
                if (horizDist < 1.2f && ballSpeedMag < 0.1f)
                {
                    m_BallNearStuckSteps++;
                    if (m_BallNearStuckSteps % 30 == 0)
                    {
                        AddReward(-0.02f);
                    }
                }
                else
                {
                    m_BallNearStuckSteps = 0;
                }
            }

            // Anti-piling: Discourage corner camping
            if (arenaCenter != null)
            {
                float distFromCenter = Vector3.Distance(
                    new Vector3(hips.position.x, 0, hips.position.z),
                    new Vector3(arenaCenter.position.x, 0, arenaCenter.position.z)
                );

                if (distFromCenter > cornerPenaltyRadius)
                {
                    float excessDist = distFromCenter - cornerPenaltyRadius;
                    AddReward(-cornerPenaltyStrength * excessDist);
                }
            }

            // Anti-piling: Penalize agents bunching together
            var allAgents = GameObject.FindGameObjectsWithTag("agent");
            int nearbyCount = 0;
            foreach (var agentObj in allAgents)
            {
                if (agentObj != gameObject)
                {
                    var otherAgent = agentObj.GetComponent<WalkerSoccerAgent>();
                    // Only count teammates (same team)
                    if (otherAgent != null && otherAgent.team == team)
                    {
                        float dist = Vector3.Distance(hips.position, agentObj.transform.position);
                        if (dist < 2.5f)
                        {
                            nearbyCount++;
                        }
                    }
                }
            }

            // Dynamic threshold: 2v2 (2 per team) triggers at 1+ nearby, larger teams at 2+
            int spacingThreshold = (m_AgentsPerTeam <= 2) ? 1 : 2;
            // Use slightly larger radius for detection to catch bunching earlier
            // Penalty remains gentle to avoid collapse
            if (nearbyCount >= spacingThreshold)
            {
                // Increase bunching penalty to reduce "walling" that blocks shots
                AddReward(-0.02f * nearbyCount);
            }

            // Ball stuck detection and reset
            if (arenaCenter != null)
            {
                var ballRbStuck = ball.GetComponent<Rigidbody>();
                if (ballRbStuck != null)
                {
                    float ballSpeed = ballRbStuck.linearVelocity.magnitude;
                    float ballDistFromCenter = Vector3.Distance(
                        new Vector3(ball.position.x, 0, ball.position.z),
                        new Vector3(arenaCenter.position.x, 0, arenaCenter.position.z)
                    );

                    bool ballInCorner = ballDistFromCenter > cornerPenaltyRadius;
                    bool ballNotMoving = ballSpeed < 0.1f;

                    if (ballInCorner && ballNotMoving)
                    {
                        float posDiff = Vector3.Distance(ball.position, m_BallStuckCheckPos);
                        if (posDiff < 0.5f)
                        {
                            m_BallStuckSteps++;

                            // Penalize agents near ball when stuck in corner
                            float agentDistToBall = Vector3.Distance(hips.position, ball.position);
                            if (agentDistToBall < 3.0f)
                            {
                                AddReward(-0.08f); // Strong penalty for camping near stuck ball
                            }

                            // Small bonus for moving ball away from wall/corner
                            float ballToCenter = cornerPenaltyRadius - ballDistFromCenter;
                            if (ballToCenter > 0.5f)
                            {
                                AddReward(0.02f * ballToCenter); // Encourage ball progress away from wall
                            }

                            if (m_BallStuckSteps > BALL_STUCK_THRESHOLD)
                            {
                                // Only first agent in scene resets ball
                                if (StepCount % 6 == 0)
                                {
                                    ball.position = new Vector3(
                                        arenaCenter.position.x,
                                        ball.position.y,
                                        arenaCenter.position.z
                                    );
                                    ballRbStuck.linearVelocity = Vector3.zero;
                                    ballRbStuck.angularVelocity = Vector3.zero;
                                    m_BallStuckSteps = 0;
                                }
                            }
                        }
                        else
                        {
                            m_BallStuckSteps = 0;
                        }
                    }
                    else
                    {
                        m_BallStuckSteps = 0;
                    }

                    m_BallStuckCheckPos = ball.position;
                }
            }

            // Ball wall contact detection and reset
            if (arenaCenter != null)
            {
                var ballRbWall = ball.GetComponent<Rigidbody>();
                if (ballRbWall != null)
                {
                    // Check if ball is near wall edge (distance from center close to radius)
                    float ballDistFromCenter = Vector3.Distance(
                        new Vector3(ball.position.x, 0, ball.position.z),
                        new Vector3(arenaCenter.position.x, 0, arenaCenter.position.z)
                    );

                    // Assuming arena radius is around 10-12m, wall contact is when ball is > 9m from center
                    bool nearWall = ballDistFromCenter > 6f;
                    bool ballSlowOrStuck = ballRbWall.linearVelocity.magnitude < 0.5f;

                    if (nearWall && ballSlowOrStuck)
                    {
                        m_BallWallContactSteps++;

                        if (m_BallWallContactSteps > BALL_WALL_THRESHOLD)
                        {
                            // Only first agent resets (based on step modulo)
                            if (StepCount % 6 == 0)
                            {
                                ball.position = new Vector3(
                                    arenaCenter.position.x,
                                    ball.position.y,
                                    arenaCenter.position.z
                                );
                                ballRbWall.linearVelocity = Vector3.zero;
                                ballRbWall.angularVelocity = Vector3.zero;
                                m_BallWallContactSteps = 0;
                            }
                        }
                    }
                    else
                    {
                        m_BallWallContactSteps = 0;
                    }
                }
            }

            // Encourage keeping ball near midfield line (center area)
            if (arenaCenter != null)
            {
                float ballDistFromCenter = Vector3.Distance(
                    new Vector3(ball.position.x, 0, ball.position.z),
                    new Vector3(arenaCenter.position.x, 0, arenaCenter.position.z)
                );
                // Small positive shaping when ball remains within 4m of center; discourages long wall rolls
                float midfieldReward = Mathf.Clamp01(1f - (ballDistFromCenter / cornerPenaltyRadius));
                if (ballDistFromCenter < 4f)
                {
                    AddReward(0.02f * midfieldReward);
                }
                // Extra penalty when ball very close to wall (beyond radius-1m)
                if (ballDistFromCenter > cornerPenaltyRadius - 1f)
                {
                    AddReward(-0.01f * (ballDistFromCenter - (cornerPenaltyRadius - 1f)));
                }
            }

            // Agent wall stuck detection and respawn
            if (arenaCenter != null)
            {
                float agentDistFromCenter = Vector3.Distance(
                    new Vector3(hips.position.x, 0, hips.position.z),
                    new Vector3(arenaCenter.position.x, 0, arenaCenter.position.z)
                );

                // Check if agent is near/in wall (> 9m from center)
                bool agentNearWall = agentDistFromCenter > 9f;

                // Check if agent hasn't moved much
                float movementDist = Vector3.Distance(hips.position, m_LastAgentPos);
                bool agentNotMoving = movementDist < m_AgentStuckMoveThreshold;

                if (agentNearWall && agentNotMoving)
                {
                    m_AgentWallStuckSteps++;

                    if (m_AgentWallStuckSteps > AGENT_WALL_STUCK_THRESHOLD)
                    {
                        // Respawn agent at initial position
                        var hipsBp = m_JdController.bodyPartsDict[hips];
                        hipsBp.rb.transform.position = initialPos;
                        hipsBp.rb.transform.rotation = Quaternion.Euler(0, rotSign * 90f, 0);
                        hipsBp.rb.linearVelocity = Vector3.zero;
                        hipsBp.rb.angularVelocity = Vector3.zero;

                        // Small penalty for getting stuck
                        AddReward(-0.1f);

                        m_AgentWallStuckSteps = 0;
                    }
                }
                else
                {
                    m_AgentWallStuckSteps = 0;
                }

                m_LastAgentPos = hips.position;
            }

            // Ball agent pile detection and reset (check every 10 frames to reduce overhead)
            if (arenaCenter != null && StepCount % 10 == 0)
            {
                var allAgentsPile = GameObject.FindGameObjectsWithTag("agent");
                int agentsNearBall = 0;

                foreach (var agentObj in allAgentsPile)
                {
                    float distToBall = Vector3.Distance(
                        new Vector3(agentObj.transform.position.x, 0, agentObj.transform.position.z),
                        new Vector3(ball.position.x, 0, ball.position.z)
                    );

                    if (distToBall < 2f) // 2m radius around ball
                    {
                        agentsNearBall++;
                    }
                }

                // Dynamic threshold: 2v2 (4 total) triggers at 3+ agents, larger teams at 4+
                int pileThreshold = (m_TotalAgentCount <= 4) ? 3 : 4;

                // If threshold+ agents sustained piling on ball
                if (agentsNearBall >= pileThreshold)
                {
                    m_BallAgentPileSteps++;

                    if (m_BallAgentPileSteps > BALL_AGENT_PILE_THRESHOLD)
                    {
                        // Only one agent resets (based on step modulo to avoid conflicts)
                        if (StepCount % 6 == 0)
                        {
                            ball.position = new Vector3(
                                arenaCenter.position.x,
                                ball.position.y,
                                arenaCenter.position.z
                            );
                            var ballRbPile = ball.GetComponent<Rigidbody>();
                            if (ballRbPile != null)
                            {
                                ballRbPile.linearVelocity = Vector3.zero;
                                ballRbPile.angularVelocity = Vector3.zero;
                            }
                            m_BallAgentPileSteps = 0;
                        }
                    }
                }
                else
                {
                    m_BallAgentPileSteps = 0;
                }
            }

            // Goalie positioning penalty (scaled by lesson phase)
            if (position == Position.Goalie && myGoal != null)
            {
                float horizDistFromGoal = Vector3.Distance(
                    new Vector3(hips.position.x, 0, hips.position.z),
                    new Vector3(myGoal.position.x, 0, myGoal.position.z)
                );

                // Tighter leash when ball is near own goal
                float leash = goalieMaxDistance;
                float distToBall = ball != null ? Vector3.Distance(ball.position, myGoal.position) : 99f;
                if (distToBall < 10f)
                {
                    leash = Mathf.Lerp(goalieMaxDistance, 1.5f, (10f - distToBall) / 10f); // 1.5m leash if ball is very close
                }

                if (horizDistFromGoal > leash)
                {
                    float excessDist = horizDistFromGoal - leash;
                    float phaseMult = (m_BallTouch >= 0.5f && m_BallTouch < 1.0f) ? 1.25f : 1.0f;
                    AddReward(-goaliePenaltyStrength * phaseMult * excessDist * 2.0f); // Double penalty for leaving goal
                }

                // Defensive shaping: reward lining up between ball and own goal (stronger bonus if ball is close)
                if (ball != null)
                {
                    Vector3 toBallFromGoal = (ball.position - myGoal.position).normalized;
                    Vector3 toAgentFromGoal = (hips.position - myGoal.position).normalized;
                    float align = Mathf.Clamp01((Vector3.Dot(toBallFromGoal, toAgentFromGoal) + 1f) * 0.5f);
                    float blockBonus = 0.03f * align;
                    if (distToBall < 8f)
                    {
                        blockBonus += 0.04f * (8f - distToBall) / 8f; // Extra bonus for blocking when ball is close
                    }
                    AddReward(blockBonus);
                }
            }

            // Striker situational aggression near opponent goal
            if (position == Position.Striker && opponentGoal != null && ball != null)
            {
                float d = Vector3.Distance(ball.position, opponentGoal.position);
                float nearOpp = Mathf.Clamp01(6f / (d + 0.001f));
                AddReward(0.015f * nearOpp);
            }

            // Mild coordination: spacing penalty for close teammates, bonus for marking opponents
            float spacingPenalty = 0f;
            foreach (var tm in m_Teammates)
            {
                float d = Vector3.Distance(hips.position, tm.position);
                if (d < 2.0f)
                {
                    spacingPenalty += (2.0f - d) * 0.005f;
                }
            }
            AddReward(-spacingPenalty);

            float markingBonus = 0f;
            foreach (var op in m_Opponents)
            {
                if (myGoal != null)
                {
                    Vector3 toGoal = (myGoal.position - op.position).normalized;
                    Vector3 toAgent = (hips.position - op.position).normalized;
                    float proj = Vector3.Dot(toAgent, toGoal);
                    if (proj > 0f)
                    {
                        markingBonus += proj * 0.005f;
                    }
                }
            }
            AddReward(markingBonus);
        }

        // Stage 1 only: Encourage turning toward off-angle targets to prevent "forward-only" exploitation
        if (m_LocomotionOnly && target != null)
        {
            Vector3 toTarget = (target.position - hips.position);
            toTarget.y = 0;
            toTarget.Normalize();

            Vector3 fwdFlat = hips.forward;
            fwdFlat.y = 0;
            fwdFlat.Normalize();

            float angleToTarget = Vector3.Angle(fwdFlat, toTarget);

            // Penalize standing still when target is off-angle (>15 degrees)
            if (angleToTarget > 15f)
            {
                float turnProgress = 1f - (angleToTarget / 180f); // 0 at 180°, 1 at 0°
                AddReward(turnProgress * 0.05f); // Small reward for reducing angle

                // Penalty for low movement when target is off-angle
                float speed = GetAvgVelocity().magnitude;
                if (speed < 0.5f) // Barely moving
                {
                    AddReward(-0.02f * (angleToTarget / 180f)); // Scales with how far off target is
                }
            }
        }
    }

    //Returns the average velocity of all of the body parts
    //Using the velocity of the hips only has shown to result in more erratic movement from the limbs, so...
    //...using the average helps prevent this erratic movement
    Vector3 GetAvgVelocity()
    {
        Vector3 velSum = Vector3.zero;

        //ALL RBS
        int numOfRb = 0;
        foreach (var item in m_JdController.bodyPartsList)
        {
            numOfRb++;
            velSum += item.rb.linearVelocity;
        }

        var avgVel = velSum / numOfRb;
        return avgVel;
    }

    //normalized value of the difference in avg speed vs goal walking speed.
    public float GetMatchingVelocityReward(Vector3 velocityGoal, Vector3 actualVelocity)
    {
        //distance between our actual velocity and goal velocity
        var velDeltaMagnitude = Mathf.Clamp(Vector3.Distance(actualVelocity, velocityGoal), 0, MTargetWalkingSpeed);

        //return the value on a declining sigmoid shaped curve that decays from 1 to 0
        //This reward will approach 1 if it matches perfectly and approach zero as it deviates
        return Mathf.Pow(1 - Mathf.Pow(velDeltaMagnitude / MTargetWalkingSpeed, 2), 2);
    }

    /// <summary>
    /// Agent touched the target
    /// </summary>
    public void TouchedTarget()
    {
        // Harsh dive suppression: only reward proper upright walking touches
        var hipsBp = m_JdController.bodyPartsDict[hips];
        float upDot = Mathf.Clamp01(Vector3.Dot(hips.up, Vector3.up));
        Vector3 hipVel = hipsBp.rb.linearVelocity;
        float horizontalSpeed = new Vector2(hipVel.x, hipVel.z).magnitude;
        float verticalSpeed = hipVel.y;

        bool postureOk = upDot >= uprightDotThreshold;
        bool speedOk = horizontalSpeed <= maxSuccessSpeed;
        bool notDiving = verticalSpeed > -1.5f; // strict downward velocity check

        if (postureOk && speedOk && notDiving)
        {
            // Small reward for valid touches only - no exploitation
            AddReward(0.2f);
        }
        else
        {
            // Heavy penalty for dives to break the cycle
            float postureMult = postureOk ? 1f : 2.5f;
            float diveMult = verticalSpeed < -2.0f ? 3.0f : (notDiving ? 1f : 2.0f);
            float speedMult = speedOk ? 1f : 1.8f;
            float totalPenalty = 0.15f * postureMult * diveMult * speedMult;
            AddReward(-totalPenalty);
        }
    }

    /// <summary>
    /// Called when agent collides with ball - provides kick force and reward
    /// </summary>
    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("ball"))
        {
            // Reward for touching the ball with cooldown to prevent farming
            // Scale inversely with ball_touch: high in early lessons (learning chase), low in full soccer
            if (m_BallTouch > 0f)
            {
                if (m_LastBallTouchStep < 0 || StepCount - m_LastBallTouchStep >= 20)
                {
                    // Inverse scaling: 0.35→full reward, 1.0→minimal reward
                    // This encourages ball engagement early, then prioritizes goals in full soccer
                    float touchScale = Mathf.Lerp(1.0f, 0.1f, (m_BallTouch - 0.35f) / 0.65f);
                    AddReward(0.1f * m_BallTouch * touchScale);
                    m_LastBallTouchStep = StepCount;
                }
            }

            // In Lessons 0-1 (ball_touch = 0.0), ball respawns on touch like original Walker
            // This encourages locomotion learning without soccer complexity
            if (m_BallTouch <= 0.01f)
            {
                // Ball respawn behavior (like Walker's target touch)
                var envController = GetComponentInParent<WalkerSoccerEnvController>();
                if (envController != null)
                {
                    envController.ResetBall();
                }
            }
            else
            {
                // In Lessons 2+ (ball_touch > 0.0), ball stays in play for soccer
                // Apply kick force based on collision
                var force = k_KickPower;
                if (position == Position.Goalie)
                {
                    force = k_KickPower * 0.8f; // Goalies kick slightly less hard
                }

                var dir = collision.contacts[0].point - hips.position;
                dir = dir.normalized;
                var ballRb = collision.gameObject.GetComponent<Rigidbody>();
                ballRb.AddForce(dir * force);

                // Apply small cost for any incidental kick to discourage random scrums,
                // and penalize clearly misaligned impacts toward own goal/sidelines.
                if (opponentGoal != null)
                {
                    Vector3 ballToGoal = (opponentGoal.position - ball.transform.position).normalized;
                    float alignment = Vector3.Dot(dir, ballToGoal);
                    AddReward(-0.02f); // base cost per physical kick
                    if (alignment < 0.3f)
                    {
                        AddReward(-0.12f * (0.3f - alignment));
                    }
                }
            }
        }
    }

    /// <summary>
    /// Heuristic for manual control (testing purposes)
    /// </summary>
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var a = actionsOut.ContinuousActions;
        // Expected size: 26 rotations + 13 strengths + 1 kick = 40
        // Default to a balanced standing pose with moderate strengths
        for (int i = 0; i < a.Length; i++) a[i] = 0f;

        // Indices mapping (must match OnActionReceived order)
        int CHEST_X = 0, CHEST_Y = 1, CHEST_Z = 2;
        int SPINE_X = 3, SPINE_Y = 4, SPINE_Z = 5;
        int THIGH_L_X = 6, THIGH_L_Y = 7;
        int THIGH_R_X = 8, THIGH_R_Y = 9;
        int SHIN_L_X = 10;
        int SHIN_R_X = 11;
        int FOOT_R_X = 12, FOOT_R_Y = 13, FOOT_R_Z = 14;
        int FOOT_L_X = 15, FOOT_L_Y = 16, FOOT_L_Z = 17;
        int ARM_L_Y = 19;
        int ARM_R_Y = 21;
        int HEAD_X = 24, HEAD_Y = 25;
        int STR_START = 26; // chest strength index
        int KICK_IDX = 39;  // last index (after strengths)

        // Slightly bent-knee standing pose for better stability
        a[THIGH_L_X] = 0.12f;
        a[THIGH_R_X] = 0.12f;
        a[THIGH_L_Y] = -0.15f; // toes slightly outward
        a[THIGH_R_Y] = 0.15f;
        a[SHIN_L_X] = -0.20f; // slight knee bend
        a[SHIN_R_X] = -0.20f;
        a[FOOT_L_X] = 0.06f;
        a[FOOT_R_X] = 0.06f;
        a[FOOT_L_Y] = -0.10f; // match outward stance
        a[FOOT_R_Y] = 0.10f;
        a[FOOT_L_Z] = -0.05f; // slight eversion/inversion to resist roll
        a[FOOT_R_Z] = 0.05f;

        // Keep torso upright
        a[CHEST_X] = 0f; a[CHEST_Y] = 0f; a[CHEST_Z] = 0f;
        a[SPINE_X] = 0f; a[SPINE_Y] = 0f; a[SPINE_Z] = 0f;
        a[HEAD_X] = 0f; a[HEAD_Y] = 0f;

        // Arms slightly out for balance (optional, small values)
        a[ARM_L_Y] = 0.10f;
        a[ARM_R_Y] = -0.10f;

        // Map simple user input to torso orientation for quick tests
        float turn = 0f;
        float pitch = 0f;
        if (Input.GetKey(KeyCode.A)) turn = -0.2f;
        if (Input.GetKey(KeyCode.D)) turn = 0.2f;
        if (Input.GetKey(KeyCode.W)) pitch = 0.2f;
        if (Input.GetKey(KeyCode.S)) pitch = -0.2f;

        a[CHEST_Y] = turn;
        a[SPINE_Y] = turn * 0.5f;   // smaller twist on spine
        a[CHEST_X] = pitch * 0.5f;  // gentle forward lean
        a[SPINE_X] = pitch * 0.25f;

        // Set joint strengths (26..38) to moderate-high so joints can hold pose
        for (int i = STR_START; i < STR_START + 13; i++) a[i] = 0.9f;

        // Spacebar triggers kick attempt at full intensity
        if (KICK_IDX < a.Length && Input.GetKey(KeyCode.Space))
        {
            a[KICK_IDX] = 1f;
        }
    }

    // Triggered kick: applies impulse to ball without requiring precise leg contact
    // Note: This method is only called when ball_touch >= 0.5 (Lesson 3+)
    void TryKickBall(float intensity)
    {
        if (ball == null) return;
        if (intensity <= 0.01f) return;
        if (m_LastKickStep < 0 || StepCount - m_LastKickStep >= kickCooldownSteps)
        {
            Vector3 toBall = ball.position - hips.position;
            // Horizontal distance check (ignore vertical component for range)
            float horizDist = new Vector2(toBall.x, toBall.z).magnitude;
            if (horizDist > kickRange) return;
            // Require reasonably upright posture to attempt kicks to reduce dive-kicks
            float upDot = Mathf.Clamp01(Vector3.Dot(hips.up, Vector3.up));
            if (upDot < (uprightDotThreshold - 0.05f))
            {
                // Abort kick when clearly not upright; no extra penalty
                return;
            }

            Rigidbody ballRb = ball.GetComponent<Rigidbody>();
            if (ballRb == null) return;

            Vector3 dir = toBall.normalized;
            dir.y = Mathf.Clamp(dir.y + kickUpFactor, 0f, 1f);
            float force = kickForce * Mathf.Clamp01(intensity);
            ballRb.AddForce(dir * force, ForceMode.Impulse);

            // Base kick reward, scaled by curriculum phase
            float baseKick = kickReward * Mathf.Clamp01(intensity);
            // Lesson 3 (ball_touch==0.5): boost kick rewards to learn shooting
            // Lesson 4 (ball_touch==1.0): reduce kick rewards to prioritize scoring strategy
            // Boost kick rewards in L4 (full soccer) to strengthen goal-directed behavior
            // Stronger phase multipliers to make purposeful kicks matter more
            float phaseMultiplier = (m_BallTouch >= 0.5f && m_BallTouch < 1.0f) ? 1.8f : 2.0f;
            float reward = baseKick * phaseMultiplier;

            // Bonus for kicking toward opponent goal
            if (opponentGoal != null)
            {
                Vector3 ballToGoal = (opponentGoal.position - ball.position).normalized;
                float alignment = Vector3.Dot(dir, ballToGoal);
                // Base kick cost to discourage random kicks
                AddReward(-0.03f);
                if (alignment > 0.3f) // Kick is somewhat toward goal
                {
                    reward += 0.35f * alignment; // Stronger directional bonus
                }
                else
                {
                    // Penalty for misaligned kicks (toward sidelines/own goal)
                    float misalign = Mathf.Clamp01(0.3f - alignment);
                    reward -= 0.2f * misalign;
                }

                // Extra bonus for shots on goal (close to goal + good alignment)
                float distToGoal = Vector3.Distance(ball.position, opponentGoal.position);
                if (distToGoal < 12f && alignment > 0.6f) // Slightly wider detection and stronger bonus
                {
                    reward += 0.6f; // Stronger shot incentive
                }
            }

            AddReward(reward);
            m_LastKickStep = StepCount;
        }
    }
}
