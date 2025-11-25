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

    private float m_Existential;
    private float m_BallTouch;
    private const float k_KickPower = 2000f;

    private BehaviorParameters m_BehaviorParameters;
    private WalkerSoccerSettings m_SoccerSettings;
    private int m_StabilizeSteps;
    private float m_BallSpawnRadius;

    [Header("Reward Tuning")]
    [SerializeField] private float uprightRewardPerStep = 0.03f; // Increased from 0.01
    [SerializeField] private float locomotionRewardScale = 2.0f;
    [SerializeField] private float antiForwardTipPenalty = 0.02f;
    [SerializeField] private float uprightDotMin = 0.7f;
    [SerializeField] private int delayBallInfluenceSteps = 50;
    [SerializeField] private float maxTargetSpeed = 3.0f;
    [SerializeField] private int speedRampSteps = 400;
    [SerializeField] private float angVelPenaltyCoef = 0.002f; // Reduced for cold-start training
    [SerializeField] private float sidewaysLeanPenalty = 0.02f;
    private int m_CurrentStepInEpisode;

    [Header("Debug Visualization")]
    [SerializeField] private bool enableDebugMode = false;
    [SerializeField] private bool freezeAtStabilization = false;
    [SerializeField] private bool logLocomotionMetrics = false;

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
        }
        else
        {
            team = Team.Purple;
            initialPos = new Vector3(transform.position.x, transform.position.y, transform.position.z);
            rotSign = -1f;
        }

        m_SoccerSettings = FindFirstObjectByType<WalkerSoccerSettings>();

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

        ConfigureRigidbodies();
    }

    /// <summary>
    /// Loop over body parts and reset them to initial conditions.
    /// </summary>
    public override void OnEpisodeBegin()
    {
        // Soccer-specific reset parameters
        m_BallTouch = m_ResetParams.GetWithDefault("ball_touch", 0);
        m_BallSpawnRadius = m_ResetParams.GetWithDefault("ball_spawn_radius", 3.0f);
        m_CurrentStepInEpisode = 0;

        //Reset all of the body parts
        foreach (var bodyPart in m_JdController.bodyPartsDict.Values)
        {
            bodyPart.Reset(bodyPart);
        }

        // Start with neutral orientation to learn balance first
        if (m_BehaviorParameters != null && m_BehaviorParameters.BehaviorType == BehaviorType.HeuristicOnly)
        {
            hips.rotation = Quaternion.identity;
        }
        else
        {
            // Neutral forward or small random variation (±10 degrees)
            float randomYaw = Random.Range(-10f, 10f);
            hips.rotation = Quaternion.Euler(0, randomYaw, 0);
        }

        UpdateOrientationObjects();

        //Set initial very low walking speed for balance acquisition
        MTargetWalkingSpeed = 0.5f; // will ramp in FixedUpdate regardless of randomize flag

        if (m_SoccerSettings == null || m_SoccerSettings.enableStartStabilization)
        {
            m_StabilizeSteps = m_SoccerSettings != null ? m_SoccerSettings.stabilizeStepsOnReset : 80; // Extended from 50
            ApplyStableStandPoseTargets(m_SoccerSettings != null ? m_SoccerSettings.standStrength : 0.9f);
        }
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

        //Position of target position relative to cube
        sensor.AddObservation(m_OrientationCube.transform.InverseTransformPoint(target.transform.position));

        // Soccer-specific observations: Ball position relative to agent
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

        // Position type
        sensor.AddObservation(position == Position.Striker ? 1f : (position == Position.Goalie ? -1f : 0f));

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
        m_CurrentStepInEpisode++;

        // Debug: freeze after stabilization to inspect pose
        if (enableDebugMode && freezeAtStabilization && m_StabilizeSteps == 0 && m_CurrentStepInEpisode == 51)
        {
            LogStandingAssessment();
            Time.timeScale = 0f; // Pause simulation
            return;
        }

        // Progressive speed ramp
        float rampT = Mathf.Clamp01(m_CurrentStepInEpisode / (float)speedRampSteps);
        float currentTarget = Mathf.Lerp(0.5f, maxTargetSpeed, rampT);
        MTargetWalkingSpeed = currentTarget;

        if (m_StabilizeSteps > 0)
        {
            ApplyStableStandPoseTargets(m_SoccerSettings != null ? m_SoccerSettings.standStrength : 0.9f);
            m_StabilizeSteps--;
        }

        var cubeForward = m_OrientationCube.transform.forward;

        var matchSpeedReward = GetMatchingVelocityReward(cubeForward * MTargetWalkingSpeed, GetAvgVelocity());
        if (float.IsNaN(matchSpeedReward))
        {
            throw new ArgumentException(
                "NaN in moveTowardsTargetReward.\n" +
                $" cubeForward: {cubeForward}\n" +
                $" hips.velocity: {m_JdController.bodyPartsDict[hips].rb.linearVelocity}\n" +
                $" maximumWalkingSpeed: {m_maxWalkingSpeed}"
            );
        }

        // Early micro-shaping for Lesson0 (ball_touch ~ 0). Encourages initiating forward movement
        // without overpowering later locomotion reward once curriculum advances.
        if (m_BallTouch <= 0.05f)
        {
            float forwardSpeedEarly = Vector3.Dot(GetAvgVelocity(), cubeForward);
            float normalizedEarly = Mathf.Clamp01(forwardSpeedEarly / 1.5f); // modest early target
            AddReward(0.01f * normalizedEarly); // small additive reward
        }

        var headForward = head.forward; headForward.y = 0;
        var lookAtTargetReward = (Vector3.Dot(cubeForward, headForward) + 1) * .5f;
        if (float.IsNaN(lookAtTargetReward))
        {
            throw new ArgumentException(
                "NaN in lookAtTargetReward.\n" +
                $" cubeForward: {cubeForward}\n" +
                $" head.forward: {head.forward}"
            );
        }

        float hipsHeight = m_JdController.bodyPartsDict[hips].rb.position.y;
        // Height-scaled reward: reward increases with height between 0.85-1.3m
        if (hipsHeight >= 0.85f)
        {
            float heightQuality = Mathf.Clamp01((hipsHeight - 0.85f) / (1.3f - 0.85f));
            AddReward(uprightRewardPerStep * (0.5f + 0.5f * heightQuality));
        }
        else if (hipsHeight >= 0.5f && hipsHeight < 0.85f)
        {
            // Gentle encouragement to stand taller (not punishment)
            float partialHeightReward = (hipsHeight - 0.5f) / (0.85f - 0.5f);
            AddReward(uprightRewardPerStep * 0.3f * partialHeightReward);
        }
        else if (hipsHeight < 0.3f)
        {
            // Only penalize complete collapse (on ground)
            float collapseAmount = (0.3f - hipsHeight) / 0.3f;
            AddReward(-0.01f * collapseAmount);
        }

        float uprightDot = Vector3.Dot(hips.up, Vector3.up);
        if (uprightDot < uprightDotMin) AddReward(-antiForwardTipPenalty);

        // Sideways tip penalty (simple heuristic)
        float sidewaysTip = Mathf.Abs(Vector3.Dot(hips.right, Vector3.up));
        if (sidewaysTip < 0.6f) AddReward(-sidewaysLeanPenalty * (0.6f - sidewaysTip));

        // Angular velocity penalty (hips)
        float angMag = m_JdController.bodyPartsDict[hips].rb.angularVelocity.magnitude;
        AddReward(-angMag * angVelPenaltyCoef);

        float ballInfluenceFactor = (m_CurrentStepInEpisode < delayBallInfluenceSteps) ? 0f : 1f;
        if (position == Position.Goalie) AddReward(m_Existential * 0.25f * ballInfluenceFactor);
        else if (position == Position.Striker) AddReward(-m_Existential * 0.25f * ballInfluenceFactor);

        float locomotionScaleDynamic = locomotionRewardScale * (0.5f + 0.5f * rampT);
        AddReward(locomotionScaleDynamic * matchSpeedReward * lookAtTargetReward);

        // Debug: log metrics every 100 steps
        if (enableDebugMode && logLocomotionMetrics && m_CurrentStepInEpisode % 100 == 0)
        {
            LogLocomotionMetrics();
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
        AddReward(1f);
    }

    /// <summary>
    /// Called when agent collides with ball - provides kick force and reward
    /// </summary>
    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("ball"))
        {
            // Reward for touching the ball (delayed influence)
            float ballInfluenceFactor = (m_CurrentStepInEpisode < delayBallInfluenceSteps) ? 0.0f : 1.0f;
            AddReward(0.2f * m_BallTouch * ballInfluenceFactor);

            // Apply kick force based on collision
            var force = k_KickPower;
            if (position == Position.Goalie)
            {
                force = k_KickPower * 0.8f; // Goalies kick slightly less hard
            }

            var dir = collision.contacts[0].point - hips.position;
            dir = dir.normalized;
            collision.gameObject.GetComponent<Rigidbody>().AddForce(dir * force);
        }
    }

    /// <summary>
    /// Heuristic for manual control (testing purposes)
    /// </summary>
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var a = actionsOut.ContinuousActions;

        // Ensure array is the expected size (39): 26 rotations + 13 strengths
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
        float standStrength = m_SoccerSettings != null ? m_SoccerSettings.standStrength : 0.9f;
        for (int i = STR_START; i < STR_START + 13; i++) a[i] = standStrength;
    }

    void LogStandingAssessment()
    {
        float hipsHeight = hips.position.y;
        float uprightDot = Vector3.Dot(hips.up, Vector3.up);
        float forwardTip = Vector3.Dot(hips.forward, Vector3.down);
        float sidewaysLean = Mathf.Abs(Vector3.Dot(hips.right, Vector3.up));
        float angVel = m_JdController.bodyPartsDict[hips].rb.angularVelocity.magnitude;

        Debug.Log($"=== Standing Assessment for {gameObject.name} (Team: {team}, Pos: {position}) ===");
        Debug.Log($"Hips Height: {hipsHeight:F2}m (target: 0.85-1.3)");
        Debug.Log($"Upright Dot: {uprightDot:F3} (target: >0.85)");
        Debug.Log($"Forward Tip: {forwardTip:F3} (target: <0.15)");
        Debug.Log($"Sideways Lean: {sidewaysLean:F3} (target: <0.3)");
        Debug.Log($"Angular Velocity: {angVel:F2} rad/s (target: <2.0)");
        Debug.Log($"Current Reward: {GetCumulativeReward():F2}");

        bool isStanding = hipsHeight > 0.85f && hipsHeight < 1.3f && uprightDot > 0.85f;
        bool isStable = forwardTip < 0.15f && sidewaysLean < 0.3f && angVel < 2.0f;

        string status = isStanding && isStable ? "✓ GOOD" : "✗ POOR";
        Debug.Log($"Overall Status: {status} (Standing: {isStanding}, Stable: {isStable})");
    }

    void LogLocomotionMetrics()
    {
        float hipsHeight = hips.position.y;
        float uprightDot = Vector3.Dot(hips.up, Vector3.up);
        Vector3 velocity = m_JdController.bodyPartsDict[hips].rb.linearVelocity;
        float forwardSpeed = Vector3.Dot(velocity, transform.forward);
        float lateralSpeed = Mathf.Abs(Vector3.Dot(velocity, transform.right));

        Debug.Log($"[{gameObject.name}] Step:{m_CurrentStepInEpisode} Height:{hipsHeight:F2} Upright:{uprightDot:F2} FwdSpeed:{forwardSpeed:F2} LatSpeed:{lateralSpeed:F2} Reward:{GetCumulativeReward():F1}");
    }

    void ConfigureRigidbodies()
    {
        int it = m_SoccerSettings != null ? m_SoccerSettings.solverIterations : 12;
        int vit = m_SoccerSettings != null ? m_SoccerSettings.solverVelocityIterations : 12;
        foreach (var bp in m_JdController.bodyPartsList)
        {
            var rb = bp.rb;
            rb.maxAngularVelocity = 50f;
            rb.solverIterations = it;
            rb.solverVelocityIterations = vit;
        }
    }

    void ApplyStableStandPoseTargets(float strength)
    {
        var bp = m_JdController.bodyPartsDict;
        bp[chest].SetJointTargetRotation(0f, 0f, 0f);
        bp[spine].SetJointTargetRotation(0f, 0f, 0f);
        bp[thighL].SetJointTargetRotation(0.12f, -0.15f, 0f);
        bp[thighR].SetJointTargetRotation(0.12f, 0.15f, 0f);
        bp[shinL].SetJointTargetRotation(-0.20f, 0f, 0f);
        bp[shinR].SetJointTargetRotation(-0.20f, 0f, 0f);
        bp[footR].SetJointTargetRotation(0.06f, 0.10f, 0.05f);
        bp[footL].SetJointTargetRotation(0.06f, -0.10f, -0.05f);
        bp[armL].SetJointTargetRotation(0f, 0.10f, 0f);
        bp[armR].SetJointTargetRotation(0f, -0.10f, 0f);
        bp[forearmL].SetJointTargetRotation(0f, 0f, 0f);
        bp[forearmR].SetJointTargetRotation(0f, 0f, 0f);
        bp[head].SetJointTargetRotation(0f, 0f, 0f);

        bp[chest].SetJointStrength(strength);
        bp[spine].SetJointStrength(strength);
        bp[head].SetJointStrength(strength);
        bp[thighL].SetJointStrength(strength);
        bp[shinL].SetJointStrength(strength);
        bp[footL].SetJointStrength(strength);
        bp[thighR].SetJointStrength(strength);
        bp[shinR].SetJointStrength(strength);
        bp[footR].SetJointStrength(strength);
        bp[armL].SetJointStrength(strength);
        bp[forearmL].SetJointStrength(strength);
        bp[armR].SetJointStrength(strength);
        bp[forearmR].SetJointStrength(strength);
    }
}
