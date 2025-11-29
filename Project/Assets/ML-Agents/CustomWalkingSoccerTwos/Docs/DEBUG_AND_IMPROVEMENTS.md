# Walker Soccer - Debugging & Improvements Guide (Updated Nov 26, 2025)

This document provides troubleshooting tips, debugging strategies, and suggestions for improving your Walker Soccer agents.

---

## Table of Contents

1. [Common Issues & Solutions](#common-issues--solutions)
2. [Anti-Exploit Mechanisms](#anti-exploit-mechanisms)
3. [Debugging Techniques](#debugging-techniques)
4. [Performance Improvements](#performance-improvements)
5. [Advanced Features](#advanced-features)
6. [Code Optimizations](#code-optimizations)

---

 
## Common Issues & Solutions
 
### Latest Updates (Dec 2025)
 - **Observation Space Expansion**: 250 → 269 observations (added role, goals, teammates, opponents)
 - **Self-Play Automation**: `self_play_weight` curriculum (0.0 → 1.0 at 50% progress) automates competitive pressure
 - **Role-Aware Rewards**: Striker aggression near opponent goal, Goalie defensive positioning
 - **Goal-Aware Penalties**: Strong discouragement of own-goal behavior (4x velocity penalty, position penalties)
 - **Coordination Incentives**: Spacing penalties (reduce bunching), marking bonuses (cover opponents)
 - **Anti-Exploit Fixes**: Corner camping, own-goal shooting, goalie wandering (see dedicated section below)

 
### Behavioral Improvements
 - Corner camping: Strong penalty (-0.08) for staying near stuck ball, bonus for moving it away
 - Own-goal shooting: 4x velocity penalty, position-based penalty, progress bonus away from own goal
 - Goalie wandering: Dynamic leash (1.5-3.2m), doubled penalty, stronger blocking bonus

---

## Anti-Exploit Mechanisms

### Corner Camping Exploit (Fixed)
**Problem**: Agents learned to push ball into corners and camp nearby, waiting for unstuck respawn to center.

**Solution**:
```csharp
// Strong penalty for camping near stuck ball
if (ballStuckTimer > 2.0f && distToBall < 3.5f && distToCenter > 10f)
    AddReward(-0.08f);

// Bonus for moving ball away from corners
if (ballMovingFromCorner)
    AddReward(0.02f);
```

### Own-Goal Shooting (Fixed)
**Problem**: Agents sometimes shot ball into their own goal.

**Solution**:
- 4x velocity penalty (-0.04 vs -0.01)
- Position-based penalty (-0.08 scaled by proximity)
- Progress bonus (+0.01 for moving ball away)

### Goalie Wandering (Fixed)
**Problem**: Goalies left goal area too frequently.

**Solution**:
- Dynamic leash (1.5m when ball close, 3.2m when far)
- Doubled leash penalty strength
- Stronger blocking position bonus (+0.03-0.07)


### Physics Issues

#### Problem: Agents fall through the floor
**Symptoms:**
- Agents instantly fall when scene starts
- Body parts disappear below ground

**Solutions:**
1. Check Ground collider thickness
   ```
   Ground GameObject > BoxCollider > Size.y should be at least 0.1
   ```

2. Verify collision layers
   ```
   Edit > Project Settings > Physics > Layer Collision Matrix
   Ensure agent layers collide with ground layer
   ```

3. Check Rigidbody settings
   ```
   All body parts should have:
   - Is Kinematic: ✗ (unchecked)
   - Use Gravity: ✓ (checked)
   - Collision Detection: Continuous or Continuous Dynamic
   ```

4. Adjust physics timestep
   ```
   Edit > Project Settings > Time > Fixed Timestep: 0.02
   ```

#### Problem: Agents move erratically/jitter
**Symptoms:**
- Body parts vibrate rapidly
- Joints explode or stretch
- Unstable movement

**Solutions:**
1. Reduce joint forces
   ```csharp
   // In JointDriveController
   maxJointForceLimit = 300f; // Down from 500
   maxJointSpring = 100f;     // Down from 200
   ```

2. Increase solver iterations
   ```
   Edit > Project Settings > Physics
   Default Solver Iterations: 10
   Default Solver Velocity Iterations: 8
   ```

3. Adjust ConfigurableJoint drives
   ```csharp
   // For each ConfigurableJoint
   xDrive.positionSpring = 100f;
   xDrive.positionDamper = 10f;
   xDrive.maximumForce = 300f;
   ```

4. Increase decision period
   ```csharp
   // On DecisionRequester component
   Decision Period = 10  // Up from 5
   ```

#### Problem: Ball physics are wrong
**Symptoms:**
- Ball moves too fast/slow
- Ball bounces too much/little
- Ball goes through walls

**Solutions:**
1. Adjust ball Rigidbody
   ```
   Mass: 0.5
   Drag: 0.1 (higher = slower)
   Angular Drag: 0.05
   Collision Detection: Continuous Dynamic
   ```

2. Create bouncy physics material
   ```
   Create > Physics Material > BallMaterial
   Bounciness: 0.6
   Friction: 0.3
   Apply to ball's SphereCollider
   ```

3. Adjust kick power
   ```csharp
   // In WalkerSoccerAgent.cs, line with k_KickPower
   private const float k_KickPower = 1500f; // Down from 2000f
   ```

---

### Training Issues

#### Problem: Agents not learning
**Symptoms:**
- Reward stays flat or negative
- No improvement after millions of steps
- Loss values stuck

**Diagnostic Steps:**
1. Check TensorBoard metrics
   ```bash
   tensorboard --logdir results
   ```
   Look for:
   - Policy loss should decrease
   - Entropy should start high, gradually decrease
   - Value loss should decrease

2. Verify observations are normalized
   ```yaml
   # In WalkerSoccer.yaml
   network_settings:
     normalize: true  # Must be enabled
   ```

3. Test in Heuristic mode
   - Set one agent to "Heuristic Only"
   - Manually control with WASD
   - Verify physics, collisions, and rewards work

**Solutions:**
1. Verify curriculum is working (V5 uses dual parameters)
   ```yaml
   # Early lessons focus on balance
   environment_parameters:
     ball_touch:  # 0.0 in L0, ramps to 1.0 in L4
     ball_spawn_radius:  # 12m in L0, decreases to 3m in L4
   ```

2. Increase learning rate
   ```yaml
   hyperparameters:
     learning_rate: 0.0005  # Up from 0.0003
   ```

3. Adjust V5 reward tuning (already implemented, tune via Inspector)
   ```csharp
   // V5 includes multiple stability mechanisms:
   // - Speed ramp (0.5 → maxTargetSpeed over speedRampSteps)
   // - Anti-forward-tip penalty (when uprightDot < uprightDotMin)
   // - Sideways lean penalty (lateral tilt damping)
   // - Angular velocity penalty (damps flailing limbs)
   // - Delayed ball influence (first delayBallInfluenceSteps)
   
   // Tune via Inspector serialized fields or modify defaults in code
   ```

#### Problem: Training crashes with NaN
**Symptoms:**
- "NaN value encountered in policy loss"
- Training stops suddenly
- Console shows infinity/NaN warnings

**Solutions:**
1. Reduce learning rate dramatically
   ```yaml
   learning_rate: 0.00001  # Very conservative
   ```

2. Check reward calculations
   ```csharp
   // Add safety checks
   if (float.IsNaN(reward) || float.IsInfinity(reward))
   {
       Debug.LogError("Invalid reward detected!");
       return;
   }
   AddReward(reward);
   ```

3. Clamp observations
   ```csharp
   // In CollectObservations
   sensor.AddObservation(Mathf.Clamp(velocity, -100f, 100f));
   ```

4. Enable gradient clipping (automatic in ML-Agents, but verify)
   ```yaml
   hyperparameters:
     learning_rate: 0.0003
     # Gradient clipping is automatic
   ```

#### Problem: One team dominates
**Symptoms:**
- Blue always wins (or Purple always wins)
- ELO ratings diverge significantly
- No balanced gameplay

**Solutions:**
1. Verify team symmetry
   - Check both teams have same position distribution
   - Ensure reward structure is identical
   - Verify team assignments in BehaviorParameters

2. Increase self-play window
   ```yaml
   self_play:
     window: 20  # Up from 10
     swap_steps: 10000  # More frequent swaps
   ```

3. Balance existential rewards
   ```csharp
   // In FixedUpdate()
   // Make rewards symmetric
   if (position == Position.Goalie)
       AddReward(m_Existential);
   else if (position == Position.Striker)
       AddReward(-m_Existential);
   ```

---

## Debugging Techniques

### Visual Debugging

#### Add Debug Lines
```csharp
// In WalkerSoccerAgent.cs

void OnDrawGizmos()
{
    if (!Application.isPlaying) return;
    
    // Draw direction to ball
    if (ball != null)
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(hips.position, ball.position);
    }
    
    // Draw facing direction
    Gizmos.color = team == Team.Blue ? Color.blue : new Color(0.5f, 0f, 0.5f);
    Gizmos.DrawRay(head.position, head.forward * 2f);
    
    // Draw target direction
    Gizmos.color = Color.green;
    Gizmos.DrawRay(hips.position, m_OrientationCube.transform.forward * 3f);
    
    // Draw velocity
    Gizmos.color = Color.red;
    Vector3 avgVel = GetAvgVelocity();
    Gizmos.DrawRay(hips.position, avgVel);
}
```

#### Add Debug Text
```csharp
void OnGUI()
{
    if (!Application.isPlaying) return;
    
    Vector3 screenPos = Camera.main.WorldToScreenPoint(head.position + Vector3.up);
    GUI.Label(new Rect(screenPos.x, Screen.height - screenPos.y, 200, 20), 
        $"Team: {team}, Pos: {position}, Reward: {GetCumulativeReward():F2}");
}
```

### Logging

#### Add detailed logging
```csharp
// In WalkerSoccerAgent.cs

public override void OnActionReceived(ActionBuffers actionBuffers)
{
    base.OnActionReceived(actionBuffers);
    
    // Log every 100 steps
    if (StepCount % 100 == 0)
    {
        Debug.Log($"[{team}:{position}] Step: {StepCount}, " +
                  $"Reward: {GetCumulativeReward():F2}, " +
                  $"DistToBall: {Vector3.Distance(hips.position, ball.position):F2}");
    }
}
```

#### Log observations
```csharp
public override void CollectObservations(VectorSensor sensor)
{
    // ... existing code ...
    
    // Debug first agent only
    if (team == Team.Blue && position == Position.Striker)
    {
        Debug.Log($"Observations - Ball pos: {ball.position}, " +
                  $"My pos: {hips.position}, " +
                  $"Velocity: {GetAvgVelocity()}");
    }
}
```

### Performance Profiling

#### Use Unity Profiler
1. Open **Window > Analysis > Profiler**
2. During Play mode, check:
   - **CPU Usage**: ML-Agents decision making
   - **Physics**: Ragdoll computation
   - **Scripts**: Agent logic overhead
   - **Rendering**: Visual performance

#### Profile Training
```bash
# Run with profiling
mlagents-learn config.yaml --run-id=profile_run --debug

# Check Python memory usage
python -m memory_profiler train.py
```

---

## Performance Improvements

### Optimization Checklist

#### Unity Scene Optimization
- ☐ Use simple primitive shapes (no high-poly meshes)
- ☐ Disable shadows on all objects
- ☐ Remove unnecessary colliders
- ☐ Use occlusion culling
- ☐ Reduce camera render distance
- ☐ Disable post-processing effects

#### Training Optimization
```yaml
# In config file
network_settings:
  hidden_units: 256  # Smaller network = faster
  num_layers: 2      # Fewer layers = faster

hyperparameters:
  batch_size: 1024   # Smaller batches = faster iterations
  buffer_size: 10240
```

#### Multi-Environment Setup
```
Scene Layout:
┌─────────────────────────────────────┐
│  Arena1  │  Arena2  │  Arena3  │    │
├──────────┼──────────┼──────────┤    │
│  Arena4  │  Arena5  │  Arena6  │    │
└─────────────────────────────────────┘

- Position arenas far apart (100+ units)
- Each arena is a complete environment
- 6 arenas = 6x training speed
```

#### Headless Training
```bash
# Build with headless mode
# In Unity: Server Build enabled

# Train without graphics
mlagents-learn config.yaml --run-id=fast_run --no-graphics --num-envs=8
```

---

## Advanced Features

### Feature: Team Communication

Add inter-agent communication for coordination:

```csharp
// In WalkerSoccerAgent.cs

public override void CollectObservations(VectorSensor sensor)
{
    // ... existing observations ...
    
    // Add teammate positions
    foreach (var agent in GetTeammates())
    {
        Vector3 relativePos = m_OrientationCube.transform.InverseTransformPoint(
            agent.transform.position
        );
        sensor.AddObservation(relativePos);
    }
}

List<WalkerSoccerAgent> GetTeammates()
{
    return FindObjectsByType<WalkerSoccerAgent>(FindObjectsSortMode.None)
        .Where(a => a.team == this.team && a != this)
        .ToList();
}
```

Update config:
```yaml
network_settings:
  memory:
    sequence_length: 64  # Enable LSTM for temporal learning
    memory_size: 128
```

### Feature: Advanced Rewards

#### Reward for passing
```csharp
// In WalkerSoccerAgent.cs

private WalkerSoccerAgent lastBallToucher = null;

void OnCollisionEnter(Collision collision)
{
    if (collision.gameObject.CompareTag("ball"))
    {
        // Check if this is a pass
        if (lastBallToucher != null && 
            lastBallToucher.team == this.team && 
            lastBallToucher != this)
        {
            // Reward both agents for successful pass
            lastBallToucher.AddReward(0.3f);
            this.AddReward(0.2f);
        }
        
        lastBallToucher = this;
        
        // ... existing kick code ...
    }
}
```

#### Reward for positioning
```csharp
void FixedUpdate()
{
    // ... existing code ...
    
    // Strikers should be forward
    if (position == Position.Striker)
    {
        float forwardness = transform.position.x * (team == Team.Blue ? 1f : -1f);
        AddReward(0.001f * Mathf.Max(0, forwardness));
    }
    
    // Goalies should stay near goal
    if (position == Position.Goalie)
    {
        Vector3 goalPos = team == Team.Blue ? new Vector3(-18, 0, 0) : new Vector3(18, 0, 0);
        float distanceToGoal = Vector3.Distance(hips.position, goalPos);
        AddReward(-0.001f * distanceToGoal);
    }
}
```

### Feature: Curriculum for Complex Skills

Progressive skill training:

```yaml
curriculum:
  - name: SkillProgression
    lessons:
      - name: Balance
        completion_criteria:
          measure: reward
          threshold: 0.5
        value:
          env_difficulty: 0
          
      - name: Walking
        completion_criteria:
          measure: reward
          threshold: 1.0
        value:
          env_difficulty: 1
          
      - name: BallChase
        completion_criteria:
          measure: reward
          threshold: 2.0
        value:
          env_difficulty: 2
          
      - name: Teamwork
        value:
          env_difficulty: 3
```

Implement in Unity:
```csharp
public override void OnEpisodeBegin()
{
    // ... existing code ...
    
    int difficulty = Mathf.RoundToInt(m_ResetParams.GetWithDefault("env_difficulty", 0));
    
    switch (difficulty)
    {
        case 0: // Balance only
            ball.SetActive(false);
            target.position = hips.position + hips.forward * 5f;
            break;
            
        case 1: // Walking
            ball.SetActive(false);
            target.position = hips.position + hips.forward * 10f;
            break;
            
        case 2: // Ball chase
            ball.SetActive(true);
            target = ball;
            break;
            
        case 3: // Full game
            ball.SetActive(true);
            // Normal soccer setup
            break;
    }
}
```

---

## Code Optimizations

### Optimize Observations

```csharp
// Cache frequently accessed components
private Rigidbody ballRigidbody;
private Transform ballTransform;

public override void Initialize()
{
    base.Initialize();
    
    if (ball != null)
    {
        ballRigidbody = ball.GetComponent<Rigidbody>();
        ballTransform = ball.transform;
    }
}

public override void CollectObservations(VectorSensor sensor)
{
    // Use cached references instead of GetComponent
    if (ballRigidbody != null)
    {
        sensor.AddObservation(
            m_OrientationCube.transform.InverseTransformDirection(
                ballRigidbody.linearVelocity
            )
        );
    }
}
```

### Optimize Rewards

```csharp
// Cache calculations
private Vector3 cachedAvgVelocity;
private int lastVelocityCalculationFrame = -1;

Vector3 GetAvgVelocity()
{
    // Only calculate once per frame
    if (lastVelocityCalculationFrame == Time.frameCount)
        return cachedAvgVelocity;
    
    Vector3 velSum = Vector3.zero;
    int numOfRb = 0;
    
    foreach (var item in m_JdController.bodyPartsList)
    {
        numOfRb++;
        velSum += item.rb.linearVelocity;
    }
    
    cachedAvgVelocity = velSum / numOfRb;
    lastVelocityCalculationFrame = Time.frameCount;
    
    return cachedAvgVelocity;
}
```

### Reduce Action Space

For faster learning, consider reducing joint complexity:

```csharp
public override void OnActionReceived(ActionBuffers actionBuffers)
{
    var bpDict = m_JdController.bodyPartsDict;
    var i = -1;
    var continuousActions = actionBuffers.ContinuousActions;
    
    // Group symmetric movements
    float chestPitch = continuousActions[++i];
    float chestYaw = continuousActions[++i];
    float chestRoll = continuousActions[++i];
    
    bpDict[chest].SetJointTargetRotation(chestPitch, chestYaw, chestRoll);
    bpDict[spine].SetJointTargetRotation(chestPitch * 0.5f, chestYaw * 0.5f, chestRoll * 0.5f);
    
    // Mirror left/right
    float thighPitch = continuousActions[++i];
    float thighYaw = continuousActions[++i];
    
    bpDict[thighL].SetJointTargetRotation(thighPitch, thighYaw, 0);
    bpDict[thighR].SetJointTargetRotation(thighPitch, -thighYaw, 0); // Mirrored
    
    // ... continue with other joints ...
}
```

---

## Recommended Next Steps

1. **Start Simple**: Train basic walking first (ball_touch: 0)
2. **Iterate Quickly**: Use small networks and short episodes initially
3. **Debug Visually**: Add Gizmos and debug text extensively
4. **Profile Often**: Use Unity Profiler to find bottlenecks
5. **Experiment**: Try different reward structures and see what emerges

---

## Additional Resources

- [Unity ML-Agents Troubleshooting](https://github.com/Unity-Technologies/ml-agents/blob/main/docs/FAQ.md)
- [Walker Example Analysis](https://github.com/Unity-Technologies/ml-agents/blob/main/docs/Learning-Environment-Examples.md#walker)
- [Reward Design Best Practices](https://github.com/Unity-Technologies/ml-agents/blob/main/docs/Reward-Design-Best-Practices.md)
- [Unity Physics Best Practices](https://docs.unity3d.com/Manual/best-practice-physics.html)

---

## Getting Help

If you're still stuck:

1. Check the [ML-Agents Forums](https://forum.unity.com/forums/ml-agents.453/)
2. Review [GitHub Issues](https://github.com/Unity-Technologies/ml-agents/issues)
3. Join the [Unity ML-Agents Discord](https://discord.com/invite/unity)
4. Review your TensorBoard logs carefully
5. Test in Heuristic mode to verify game mechanics

Happy training! 🎮⚽🤖
