# Walker Soccer Two-Stage Training - Complete Project History

## Overview

This document chronicles the complete 50+ hour training journey of humanoid ragdoll agents learning locomotion and then soccer through two-stage transfer learning. It captures key challenges, solutions, and lessons learned throughout the process.

---

## Stage 1: Locomotion Training (18M Steps - COMPLETED)

### Goal
Master bipedal locomotion with full 360° turning capability before introducing soccer complexity.

### Training Configuration
- **Trainer**: PPO (single-agent)
- **Learning Rate**: 0.0003
- **Observation Space**: 250 (243 locomotion + 7 zeros for Stage 2 compatibility)
- **Actions**: 40 continuous (39 joints + 1 kick ignored)
- **Environment**: 10-20 parallel arenas with moving targets
- **Training Time**: ~12 hours

### Phase 1.1: Initial Training (0-10M steps)

**Progress Timeline:**
| Steps | Mean Reward | Behavior |
|-------|-------------|----------|
| 0-2M | -10 → +5 | Learning to stand, frequent falls |
| 2M-5M | +5 → +15 | Stable standing, basic forward walking |
| 5M-10M | +15 → +20 | Smooth forward walking, minimal turning |

**Anti-Exploit Implementation:**

**Issue**: Agents diving toward target instead of walking
- Behavior: Lunging, falling forward, triggering respawn for easier approach
- Impact: Policy learned "dive-to-respawn" instead of controlled walking

**Solution**: Touch Validation System
```csharp
// Implemented graded penalties based on violation severity
Valid touch requirements:
- Upright: Vector3.Dot(hips.up, Vector3.up) >= 0.85
- Speed limit: horizontalSpeed <= 2.0 m/s
- No dive: verticalSpeed > -1.5 m/s

Rewards:
- Valid touch: +0.2
- Invalid touch: -0.15 base × postureMult × diveMult × speedMult
- Max penalty: -1.35 for severe violations
```

**Outcome**: Agents learned proper walking without exploitation

**Spawn System Enhancement:**
```csharp
Random spawns to prevent forward-only policies:
- Position: ±6m X/Z random offset
- Rotation: 0-360° random Y-axis
- Target: Random position within arena
```

### Phase 1.2: Forward-Only Policy Crisis (10M-16M steps)

**Issue Discovered at 16M Steps:**
- Mean Reward: +20-28 (appeared successful)
- **Critical Problem**: Agents only walked forward efficiently
- When target >30° off-axis: Agents stood still and waited for target to respawn
- No turning behavior learned despite random spawns

**Root Cause Analysis:**
- Episode timeout rewarded patient waiting over active turning
- Random spawns provided enough forward-aligned cases to sustain policy
- No explicit turn incentive in reward structure

**Solution**: Turn Encouragement System (Added at 16M)

```csharp
// In FixedUpdate(), added turn rewards/penalties:

if (angleToTarget > 15f) // Only when target significantly off-axis
{
    // Reward progress toward target
    float turnProgress = 1f - (angleToTarget / 180f);
    AddReward(0.05f * turnProgress);
    
    // Penalize standing still when should be turning
    if (avgSpeed < 0.5f)
    {
        AddReward(-0.02f * (angleToTarget / 180f));
    }
}
```

**Implementation Notes:**
- Added mid-training without full restart
- Required Unity rebuild and continued training from 16M checkpoint
- Changes took effect immediately

### Phase 1.3: Turn Mastery (16M-18M steps)

**Progress After Turn Encouragement:**
| Steps | Mean Reward | Behavior |
|-------|-------------|----------|
| 16M | +28 | Forward-only, standing when off-axis |
| 17M | +30 | Beginning to turn, some rotation attempts |
| 18M | +35 | Confident 360° turning toward any target |

**Success Indicators:**
- Agents consistently turn toward targets >30° off-axis
- Smooth rotation transitions without falling
- Valid touch rewards frequent in all orientations
- Mean reward stabilized at +35

**Stage 1 Completion Criteria Met:**
✅ Upright posture maintained  
✅ Walking at 0.8-4.0 m/s speeds  
✅ Full 360° turning capability  
✅ Touch validation preventing dive exploits  
✅ Ready for Stage 2 transfer

---

## Stage 2: Soccer Training (15M Target - IN PROGRESS AT 4M)

### Goal
Transfer locomotion skills and learn multi-agent soccer: ball chasing, kicking, team coordination, goal scoring.

### Training Configuration
- **Trainer**: POCA (multi-agent)
- **Learning Rate**: 0.0001 (lower for fine-tuning)
- **Observation Space**: 250 (same as Stage 1, but soccer obs now contain real data)
- **Actions**: 40 continuous (same as Stage 1, kick now active in Lesson 3+)
- **Environment**: 3v3 soccer (6 agents, ball, goals)
- **Transfer Method**: `--initialize-from=WalkerStage1`

### Phase 2.1: Initial Transfer (0-530k steps)

**Transfer Learning Success:**
- Policy weights transferred without errors
- Optimizer warnings normal (PPO→POCA transition, expected)
- Locomotion skills immediately retained - agents walked from step 0

**Critical Issue Discovered:**
| Metric | Value | Expected | Status |
|--------|-------|----------|--------|
| Mean Reward | 60-100 | 5-20 | ❌ Too high |
| Group Reward | 0.0 | 2-10 | ❌ No goals |
| ELO | 1052→676 | Stable | ❌ Declining |

**Problem Diagnosis:**
- Locomotion rewards completely dominating (+80-100 per episode)
- Soccer rewards negligible (+0-2 per episode)
- No goals scored in 530k steps
- Agents optimizing locomotion, ignoring ball/soccer

**Solution**: Locomotion Reward Fading

```yaml
# Added locomotion_scale parameter to YAML:
environment_parameters:
  locomotion_scale:
    sampler_type: constant
    sampler_parameters:
      value: 0.5  # Multiply all locomotion rewards by 0.5
```

```csharp
// In C# FixedUpdate():
float locomotionReward = CalculateLocomotionReward();
AddReward(locomotionReward * m_LocomotionScale); // Curriculum-controlled
```

**Result**: Locomotion rewards reduced, soccer signals now competitive

### Phase 2.2: Wall-Sticking Exploitation (530k steps)

**New Issue Emerged:**
- Agents learned to push ball to arena walls
- Constant ball-wall contact triggers continuous collision rewards
- Ball stuck at wall edge for 100+ steps
- No goal-directed behavior

**Root Cause:**
- Ball touch reward (+0.2) triggered on any collision
- No cooldown between touches
- No incentive to move ball toward goal

**Solution Package:**

1. **Touch Cooldown**
```csharp
private int m_LastBallTouchStep = -999;

void OnCollisionEnter(Collision col)
{
    if (col.gameObject.CompareTag("ball"))
    {
        int stepsSinceLastTouch = StepCount - m_LastBallTouchStep;
        if (stepsSinceLastTouch >= 20) // 20-step cooldown
        {
            AddReward(0.2f * m_BallTouch);
            m_LastBallTouchStep = StepCount;
        }
    }
}
```

2. **Goal-Oriented Shaping**
```csharp
// In FixedUpdate(), reward ball progress toward opponent goal:
float ballProgress = Vector3.Distance(ball.position, myGoal.position) - 
                     Vector3.Distance(ball.position, opponentGoal.position);
AddReward(0.02f * ballProgress);

// Reward ball velocity toward opponent goal:
Vector3 dirToGoal = (opponentGoal.position - ball.position).normalized;
float velTowardGoal = Vector3.Dot(ballRigidbody.velocity, dirToGoal);
AddReward(0.005f * velTowardGoal);
```

3. **Near-Ball Stuck Penalty**
```csharp
// Penalize staying near stationary ball:
if (distToBall < 2f && ballRigidbody.velocity.magnitude < 0.5f)
{
    if (Time.frameCount % 30 == 0) // Every 30 steps
    {
        AddReward(-0.02f);
    }
}
```

**Result**: Wall-sticking reduced, agents began chasing moving ball

### Phase 2.3: Corner Piling (2.5M steps)

**Issue**: Despite round arena, agents clustering in corners
- 4-6 agents piled in same corner
- Ball trapped in agent cluster
- No dynamic play, just static pile

**Root Cause Analysis:**
- Round arena has no sharp corners, but agents still cluster
- Teammate spacing not enforced
- No penalty for excessive clustering
- Ball can get stuck in agent pile indefinitely

**Solution Package:**

1. **Corner Penalty**
```csharp
// Penalize distance from arena center:
float distFromCenter = Vector3.Distance(hips.position, arenaCenter.position);
if (distFromCenter > cornerPenaltyRadius) // 8m
{
    float excessDist = distFromCenter - cornerPenaltyRadius;
    AddReward(-cornerPenaltyStrength * excessDist); // -0.01 per meter
}
```

2. **Teammate Spacing Penalty**
```csharp
// Penalize bunching near teammates:
int nearbyCount = 0;
foreach (var agent in teammates)
{
    if (Vector3.Distance(hips.position, agent.hips.position) < 2.5f)
    {
        nearbyCount++;
    }
}
if (nearbyCount >= spacingThreshold)
{
    AddReward(-0.02f * nearbyCount); // Softened from later -0.05
}
```

3. **Ball Stuck Reset**
```csharp
// Reset ball if stuck in corner too long:
if (ballDistFromCenter > 8f && ballVelocity.magnitude < 0.3f)
{
    m_BallCornerStuckSteps++;
    if (m_BallCornerStuckSteps > 200) // 200-step threshold
    {
        ResetBall(); // Respawn at arena center
    }
}
```

4. **Ball Spawn Curriculum**
```yaml
# Progressive ball spawn distance:
ball_spawn_radius:
  curriculum:
    - threshold: 0.133, value: 1.5  # Early: centered
    - threshold: 0.4, value: 2.0     # Mid: wider
    - value: 1.5                     # Late: recentered
```

**Result**: Reduced corner clustering, more distributed play

### Phase 2.4: Tag Compatibility & Dynamic Teams (3M steps)

**Issue**: Teammate detection failing
- TargetController requires all agents tagged "agent"
- Original code looked for "blueAgent"/"purpleAgent" tags
- Teammate spacing and coordination broken

**Solution**: Team Field Detection
```csharp
// Changed from tag-based to team field-based:
List<WalkerSoccerAgent> GetTeammates()
{
    return FindObjectsByType<WalkerSoccerAgent>()
        .Where(a => a.team == this.team && a != this)
        .ToList();
}
```

**Dynamic Team Size Feature:**
```csharp
// Auto-detect team size during Initialize():
private void CountAgents()
{
    m_TotalAgentCount = GameObject.FindGameObjectsWithTag("agent").Length;
    m_AgentsPerTeam = m_TotalAgentCount / 2;
}

// Adaptive spacing threshold:
int spacingThreshold = (m_AgentsPerTeam <= 2) ? 1 : 2;
// 2v2: Triggers at 1+ nearby teammate
// Larger teams: Triggers at 2+ nearby teammates
```

**Result**: System now works for 2v2, 3v3, 4v4, or larger team sizes

### Phase 2.5: Kick Enabling Issues (2.3M steps)

**User Report**: "Agents don't seem to ever kick the ball"

**Investigation:**
- User at 2.3M steps, expecting kicks
- Curriculum shows `ball_touch = 0.35` at 2M steps
- Code gates kick action: `if (m_BallTouch >= 0.5)`

**Problem**: Threshold mismatch
- Curriculum progresses to 0.35 at 2M
- Kick gating requires 0.5
- Kick action remains disabled until 5M+ steps (when 0.5 reached)

**Solution**: Restructured Lesson Plan

```yaml
# Revised curriculum with clear phases:
ball_touch:
  curriculum:
    - name: Lesson 2 - Chase Ball (No Kicking)
      threshold: 0.133  # 2M steps (13.3% of 15M)
      value: 0.35
      
    - name: Lesson 3 - Enable Kicking (~6M steps)
      threshold: 0.4    # 6M steps (40% of 15M)
      value: 0.5        # Kick action unlocked!
      
    - name: Lesson 4 - Full Soccer
      value: 1.0
```

```csharp
// Lowered kick gating threshold:
if (m_BallTouch >= 0.35f) // Changed from 0.5
{
    TryKickBall(continuousActions[39]);
}
```

**Lesson Structure Rationale:**
- **Lesson 2 (0-2M)**: Chase only, no kicking (`ball_touch = 0.35`)
  - Focus: Locomotion transfer, ball awareness, positioning
  - Kick disabled to prevent premature lunging behavior
  
- **Lesson 3 (2M-6M)**: Kicking enabled (`ball_touch = 0.5`)
  - Kick rewards: ×1.5 phase multiplier for encouragement
  - Ball spawn: 2.0m radius (wider for kick practice)
  - Focus: Learn kick timing, direction, power control
  
- **Lesson 4 (6M+)**: Full soccer (`ball_touch = 1.0`)
  - Kick rewards: ×0.8 phase multiplier (reduced emphasis)
  - Ball spawn: 1.5m radius (recentered for intense play)
  - Focus: Goal scoring, coordination, strategy

**Touch Reward Inverse Scaling:**
```csharp
// Prevent touch farming in full soccer:
float inverseScale = Mathf.Lerp(1f, 0.1f, (m_BallTouch - 0.35f) / 0.65f);
float touchReward = 0.1f * m_BallTouch * inverseScale;

// Scaling progression:
// ball_touch = 0.35: 1.0× multiplier (full reward)
// ball_touch = 0.5:  0.77× multiplier (slight reduction)
// ball_touch = 1.0:  0.1× multiplier (minimal, focus on goals)
```

**Result**: Clear progression, kick action enabled at appropriate time

### Phase 2.6: ELO Decline Crisis (Equivalent 15M from checkpoint)

**User Report**: "ELO has been on a downward trend since the very beginning"
- ELO: 694 → 689 over extended training
- Mean Reward increasing
- Group Reward sporadic (0-50 range)

**Problem Diagnosis:**
1. **Self-Play Too Early**
   - Agents barely scoring goals
   - Self-play matchmaking based on win/loss
   - With rare goals, games decided by random chance
   - ELO ratings become meaningless noise

2. **Locomotion Still Dominant**
   - Even with 0.5× scaling, locomotion rewards ~30-40 per episode
   - Goal rewards +50 but infrequent (0.1-0.3 goals per episode)
   - Locomotion optimization still primary driver

3. **Touch Farming Present**
   - Agents touching ball frequently for +0.2 rewards
   - Accumulating 10-20 touches per episode = +2-4 reward
   - More reward from touches than goal attempts

**Solution Package:**

1. **Disable Self-Play Temporarily**
```yaml
# In YAML config, comment out self-play:
# self_play:
#   save_steps: 50000
#   ...
```
Rationale: Let agents learn to score reliably before introducing competitive dynamics

2. **Enhanced Locomotion Fade Curriculum**
```yaml
locomotion_scale:
  curriculum:
    - name: Stage2_FadeStart
      threshold: 0.10    # 1.5M steps
      value: 0.4         # 40% of Stage 1 locomotion rewards
      
    - name: Stage2_FadeMore
      threshold: 0.4     # 6M steps
      value: 0.25        # 25% of Stage 1 locomotion rewards
      
    - name: Stage2_FullSoccer
      value: 0.1         # 10% of Stage 1 locomotion rewards
```

3. **Scoring Incentive Enhancements**

```csharp
// Increased base kick reward:
private float kickReward = 0.2f; // Up from 0.1

// Added kick direction bonus:
Vector3 dirToGoal = (opponentGoal.position - ball.position).normalized;
float dirAlignment = Vector3.Dot(kickDir, dirToGoal);
if (dirAlignment > 0.5f)
{
    AddReward(0.1f * dirAlignment); // Bonus for good aim
}

// Added shot-on-goal detection:
if (distanceToGoal < 8f && dirAlignment > 0.7f)
{
    AddReward(0.2f); // Bonus for quality shot
}
```

**Result**: ELO monitoring paused, focus on skill development

### Phase 2.7: Aggressive Penalty Tuning (4M steps)

**Context**: User reports at 4M steps
- "Agents are pushing the ball towards the sides"
- Side-rolling behavior observed
- Bunching still occurring

**Initial Response**: Identified as expected Lesson 3 behavior
- In kicking practice phase, some side-pushing normal
- Should improve naturally when transitioning to Lesson 4 at 6M

**Decision**: Strengthen penalties preemptively
- Rationale: Prevent bad habits from solidifying
- Approach: Increase penalty strengths across the board

**Changes Applied:**

1. **Bunching Penalty Strengthened**
```csharp
// Increased from -0.02 to -0.05:
if (nearbyCount >= spacingThreshold)
{
    AddReward(-0.05f * nearbyCount); // 2.5× stronger
}
```

2. **Spacing Radius Tightened**
```csharp
// Reduced from 3m to 2.5m:
float spacingRadius = 2.5f; // More sensitive to clustering
```

3. **Kick Power Increased**
```csharp
// Collision kick force:
private const float k_KickPower = 14000f; // Up from 5000

// Action kick force:
private float kickForce = 12.0f; // Up from 6.0
```

4. **Near-Wall Penalty Added**
```csharp
// Penalize when ball near wall edge and slow:
if (ballDistFromCenter > cornerPenaltyRadius - 1f)
{
    if (ballVelocity.magnitude < 1f)
    {
        float wallProximity = ballDistFromCenter - (cornerPenaltyRadius - 1f);
        AddReward(-0.03f * wallProximity); // Strong wall penalty
    }
}
```

5. **Goalie Leash Tightened**
```csharp
// For goalie position type:
private float goalieMaxDistance = 2.0f; // Down from 2.5m
private float goaliePenaltyStrength = 0.05f; // Up from 0.03

// Phase multipliers increased:
float phaseMult = (m_BallTouch >= 0.5f && m_BallTouch < 1.0f) ? 1.5f : 1.2f;
// Lesson 3: 1.5× multiplier (was 1.0×)
// Lesson 4: 1.2× multiplier (was 1.0×)
```

6. **Midfield Shaping Added**
```csharp
// Reward when ball near center:
if (ballDistFromCenter < 4f)
{
    AddReward(0.002f); // Encourage central play
}
```

**Rebuild and Resume**: Changes deployed at 4M steps

### Phase 2.8: Training Collapse (4M steps) - CRITICAL FAILURE

**User Report**: Training metrics showing severe degradation
```
Steps: 4000000, Mean Reward: -18.164, Std: 51.432, Group: 21.279
Steps: 4100000, Mean Reward: -22.483, Std: 57.891, Group: 0.000
Steps: 4200000, Mean Reward: -10.627, Std: 43.215, Group: 48.553
```

**Crisis Indicators:**
| Metric | Value | Expected | Status |
|--------|-------|----------|--------|
| Mean Reward | -10 to -22 | +3 to +8 | ❌ CRITICAL |
| Reward StdDev | 40-60 | 15-25 | ❌ Chaotic |
| Group Reward | 0-48 | 5-20 | ⚠️ Sporadic |
| Episode Length | Variable | 500-1000 | ⚠️ Unstable |

**Behavior Observations:**
- Agents moving erratically
- Frequent resets and collapses
- Some episodes with positive rewards (48 group reward)
- But majority deeply negative (-22 mean)
- High variance indicating some agents succeeding, most failing

**Root Cause Analysis:**

**Cumulative Penalty Overload** - Multiple strong penalties active simultaneously:

1. **Bunching Penalty: -0.05 per nearby agent**
   - In 3v3 soccer, natural ball contests require clustering
   - 2-3 agents near ball = -0.10 to -0.15 per step
   - Over 500-step episode = -50 to -75 cumulative penalty
   - **Problem**: Legitimate soccer behavior heavily punished

2. **Goalie Penalty: 0.05 strength + 2.0m max + 1.5× phase**
   - Goalie needs to move to defend different angles
   - 2.0m radius too restrictive for effective defense
   - Penalty: -0.075 per meter beyond 2.0m (0.05 × 1.5)
   - If goalie at 3.5m (reasonable defensive position) = -0.075 per step
   - Over 500 steps = -37.5 cumulative penalty
   - **Problem**: Goalies punished for defending

3. **Wall Proximity Penalty: -0.03 per meter when ball near edge**
   - Soccer naturally involves wall/corner play
   - Ball near wall and agents pursuing = constant -0.03
   - Over 200 steps near wall = -6 cumulative penalty
   - **Problem**: Legitimate wall play over-punished

4. **Corner Penalty: -0.01 per excess meter beyond 8m**
   - Reasonable, but compounds with other penalties
   - When combined with bunching and wall penalties, amplifies negative rewards

5. **Near-Ball Stuck Penalty: -0.02 every 30 steps**
   - Triggers during contested ball situations
   - Multiple agents near slow ball = multiple penalties
   - Compounds with bunching penalty

**The Compounding Effect:**

Example episode breakdown:
```
Locomotion rewards (faded): +10 to +20
Ball touches (3-5 touches): +0.6 to +1.0
Kick rewards (2-3 kicks): +0.4 to +0.6
Upright bonuses: +5 to +10
─────────────────────────────────
Potential positive rewards: +16 to +32

Bunching penalty (400 steps bunched): -20 to -40
Goalie penalty (goalie at 3m): -25 to -35
Wall penalty (200 steps near wall): -6 to -12
Corner penalty (occasional): -2 to -5
Near-ball stuck penalty: -1 to -3
─────────────────────────────────
Cumulative penalties: -54 to -95

TOTAL EPISODE REWARD: -38 to -63 (highly negative)
```

**Why This Broke Learning:**

1. **No Positive Pathways**: Every reasonable soccer action triggered multiple penalties
2. **Exploration Crushed**: Agents couldn't discover positive behaviors through exploration
3. **Policy Collapse**: Network converged to minimize penalties rather than maximize rewards
4. **High Variance**: Some lucky episodes avoided penalty triggers (48 group reward), most didn't (-22 mean)

**Key Insight Realized:**

"Side-pushing at 4M in Lesson 3 is **expected learning behavior**, not a problem requiring aggressive intervention."

- Agents learning kick mechanics naturally push ball in various directions
- They need to explore different kick angles and powers
- Side-pushing is part of the learning process
- Should improve naturally during Lesson 4 (full soccer) when goal rewards dominate

**The Mistake**: Tried to force behavior change through strong penalties instead of allowing natural curriculum progression

### Phase 2.9: Recovery - Penalty Softening (4M steps)

**Recovery Strategy:**

1. **Acknowledge the Over-Correction**
   - Penalties implemented were collectively too strong
   - Multiple penalties active simultaneously compounded the problem
   - Need to soften to guidance levels, not punishment levels

2. **Analyze Each Penalty's Purpose**

| Penalty | Current | Purpose | Problem | Target |
|---------|---------|---------|---------|--------|
| Bunching | -0.05 | Prevent piling | Too harsh for ball contests | -0.02 |
| Goalie Distance | 2.0m | Keep goalie near goal | Too restrictive for defense | 3.0m |
| Goalie Strength | 0.05 | Enforce positioning | Too punishing | 0.02 |
| Goalie Phase | 1.5×/1.2× | Lesson emphasis | Too aggressive | 1.2×/1.1× |
| Wall Proximity | -0.03 | Discourage wall play | Conflicts with legitimate play | -0.01 |
| Corner | 0.01 | Keep play centered | Actually reasonable | 0.01 |

3. **Softening Principles**

**Bunching (-0.05 → -0.02):**
- Rationale: Natural soccer involves temporary clustering during ball contests
- New value: -0.02 gently discourages excessive piling without punishing contests
- Still active for spacing, but allows 2-3 agents near ball without devastating penalty

**Goalie Max Distance (2.0m → 3.0m):**
- Rationale: Goalies need room to defend various angles and intercept
- 2.0m: Can barely leave goal line
- 3.0m: Allows movement to intercept crosses and shots while staying near goal
- Balanced leash for defensive play

**Goalie Penalty Strength (0.05 → 0.02):**
- Rationale: Guidance, not punishment
- 0.05 was causing -25 to -35 per episode for reasonable defense
- 0.02 provides gentle feedback without overwhelming other rewards

**Goalie Phase Multipliers (1.5×/1.2× → 1.2×/1.1×):**
- Rationale: Reduced lesson-specific emphasis
- Lesson 3: 1.2× (down from 1.5×) - still some emphasis but not aggressive
- Lesson 4: 1.1× (down from 1.2×) - minimal emphasis in full soccer

**Wall Proximity (-0.03 → -0.01):**
- Rationale: Wall play is sometimes necessary and legitimate
- -0.03 was causing -6 to -12 per episode during normal play
- -0.01 provides subtle guidance away from edges without hard punishment

**Corner Penalty (Unchanged at 0.01):**
- Rationale: Already at guidance level
- Provides gentle nudge toward central play without being restrictive

4. **Implementation**

```csharp
// Changes applied to WalkerSoccerAgent.cs:

// Bunching penalty softened:
if (nearbyCount >= spacingThreshold)
{
    AddReward(-0.02f * nearbyCount); // Changed from -0.05f
}

// Goalie positioning relaxed:
[SerializeField] private float goalieMaxDistance = 3.0f; // Changed from 2.0f
[SerializeField] private float goaliePenaltyStrength = 0.02f; // Changed from 0.05f

// Goalie phase multipliers reduced:
float phaseMult = (m_BallTouch >= 0.5f && m_BallTouch < 1.0f) ? 1.2f : 1.1f;
// Lesson 3: 1.2× (was 1.5×)
// Lesson 4: 1.1× (was 1.2×)

// Wall proximity penalty softened:
if (ballDistFromCenter > cornerPenaltyRadius - 1f)
{
    AddReward(-0.01f * wallProximity); // Changed from -0.03f
}

// Corner penalty unchanged (already reasonable):
[SerializeField] private float cornerPenaltyStrength = 0.01f; // Was 0.02f, now 0.01f
```

5. **Rebuild and Resume Strategy**

```powershell
# Build new Unity executable with softened penalties
# Build → WalkerStage2_20.exe

# Resume from 3M checkpoint (before collapse began):
mlagents-learn Assets/ML-Agents/CustomWalkingSoccerTwos/WalkerSoccerStage2_Soccer.yaml `
  --env="Builds/WalkerStage2_20.exe" `
  --num-envs=4 --no-graphics `
  --initialize-from=WalkerStage2_20_V2 `
  --run-id=WalkerStage2_20_V3_recovery `
  --force
```

**Recovery Monitoring Plan:**

| Checkpoint | Expected Mean Reward | Expected Behavior |
|------------|---------------------|-------------------|
| 3M (restart) | 0 to +5 | Adapting to softened penalties |
| 3.5M | +2 to +7 | Positive trend emerging |
| 4M | +3 to +8 | Stable positive progression |
| 5M | +5 to +10 | Consistent improvement |
| 6M (Lesson 4) | +7 to +12 | Transitioning to full soccer |

**Success Criteria:**
- Mean Reward consistently positive by 4.5M
- StdDev reduced to 20-30 range (from 40-60)
- Group Reward showing regular goal events (not just 0s)
- Agents exploring ball play without excessive penalties

### Lesson 3 Extension (4M steps)

**Decision**: Extend Lesson 3 from 5M to 6M steps
- Rationale: Give agents more time to practice kicking with softened penalties
- Provides buffer for recovery from collapse
- Ensures solid kick mechanics before full soccer transition

```yaml
# Updated curriculum thresholds:
ball_touch:
  curriculum:
    - threshold: 0.133, value: 0.35  # Lesson 2 (0-2M)
    - threshold: 0.4, value: 0.5     # Lesson 3 (2M-6M) - extended
    - value: 1.0                     # Lesson 4 (6M+)

ball_spawn_radius:
  curriculum:
    - threshold: 0.133, value: 1.5   # Lesson 2
    - threshold: 0.4, value: 2.0     # Lesson 3 - extended
    - value: 1.5                     # Lesson 4

locomotion_scale:
  curriculum:
    - threshold: 0.10, value: 0.4    # 1.5M
    - threshold: 0.4, value: 0.25    # 6M - extended
    - value: 0.1                     # Full soccer
```

**New Lesson Structure:**
- **Lesson 2 (0-2M)**: Chase only, no kicking
- **Lesson 3 (2M-6M)**: Kicking practice (extended from 5M)
- **Lesson 4 (6M+)**: Full soccer with reduced kick emphasis

---

## Current Status & Next Steps

### Where We Are (Post-Softening)
- **Training Step**: Preparing to resume from 3M checkpoint
- **Unity Build**: Updated with softened penalties
- **Curriculum**: Extended Lesson 3 to 6M
- **Self-Play**: Disabled for skill development focus
- **Monitoring**: Ready to track Mean Reward recovery

### Expected Timeline (3M → 15M)

| Phase | Steps | Duration | Goal |
|-------|-------|----------|------|
| Recovery | 3M-4M | 1-2 hours | Mean Reward +3 to +8 |
| Lesson 3 Completion | 4M-6M | 3-4 hours | Solid kick mechanics |
| Lesson 4 Transition | 6M-7M | 1-2 hours | Adapt to full soccer |
| Full Soccer Training | 7M-12M | 8-10 hours | Goal-oriented play |
| Advanced Play | 12M-15M | 5-6 hours | Team coordination |

**Total Remaining**: ~20-25 hours

### Monitoring Checklist

**3M-4M (Recovery Phase):**
- ✅ Mean Reward turns positive
- ✅ StdDev decreases toward 20-30
- ✅ Agents exploring without excessive penalties
- ✅ Group Reward shows occasional goals (not 0 every episode)

**4M-6M (Lesson 3 Completion):**
- ✅ Kick rewards accumulating regularly
- ✅ Side-pushing behavior begins to reduce naturally
- ✅ Mean Reward climbs to +5 to +10
- ✅ Goals becoming more frequent

**6M-15M (Full Soccer):**
- ✅ Lesson 4 transition smooth (curriculum parameters update)
- ✅ Locomotion further faded (0.25 → 0.1)
- ✅ Touch rewards reduced (inverse scaling to 0.1×)
- ✅ Goal scoring becomes primary reward source
- ✅ Team coordination emerges
- ✅ Consider re-enabling self-play at 8M if goals-per-episode > 0.3

### Post-15M Enhancements

**If Training Successful:**
1. Re-enable self-play for competitive balance
2. Add passing rewards (detect teammate touch after your touch)
3. Add assist bonuses (credit passer when goal scored)
4. Implement goalkeeper dive action (dedicated save mechanic)
5. Experiment with larger arenas or team sizes

**If Issues Persist:**
1. Further penalty adjustments via Inspector (no rebuild)
2. Extend specific lessons (e.g., Lesson 4 to 20M)
3. Add intermediate curriculum steps
4. Implement more sophisticated reward shaping

---

## Key Lessons Learned

### 1. Transfer Learning is Powerful But Requires Care
- ✅ **Success**: Locomotion skills transferred perfectly from Stage 1 to Stage 2
- ✅ **Key**: Matching observation space (250) with zero-padding for unused dimensions
- ⚠️ **Caution**: Transferred rewards can dominate - need explicit fading mechanism

### 2. Curriculum Design is Critical
- ✅ **Progressive Skill Building**: Chase → Kick → Score prevents overwhelm
- ✅ **Parameter-Driven**: Multiple curriculum parameters (ball_touch, spawn_radius, locomotion_scale) create rich progression
- ⚠️ **Threshold Timing**: Ensure curriculum thresholds align with behavior expectations

### 3. Penalties Must Be Balanced
- ❌ **Critical Error**: Multiple strong penalties compound to crush exploration
- ✅ **Solution**: Penalties should guide, not punish
- ✅ **Principle**: Test penalties individually before combining
- ✅ **Recovery**: Can soften penalties mid-training and resume from earlier checkpoint

### 4. Expected vs Problem Behaviors
- ✅ **Key Insight**: Side-pushing at 4M in Lesson 3 is **expected learning**, not failure
- ❌ **Mistake**: Over-correcting natural exploration through aggressive penalties
- ✅ **Better Approach**: Allow curriculum progression to naturally improve behavior

### 5. Self-Play Timing Matters
- ⚠️ **Too Early**: When goals rare, self-play ELO becomes meaningless noise
- ✅ **Right Time**: Enable after agents score reliably (goals-per-episode > 0.3)
- ✅ **Recovery**: Can disable/re-enable self-play mid-training via config

### 6. Monitoring and Diagnosis
- ✅ **Mean Reward**: Primary health indicator
- ✅ **Reward StdDev**: High variance indicates some agents succeed, most fail (red flag)
- ✅ **Group Reward**: Soccer-specific success metric
- ✅ **ELO**: Only meaningful when goals frequent
- ✅ **TensorBoard**: Essential for identifying trends and issues

### 7. Anti-Exploit Systems
- ✅ **Proactive Design**: Touch cooldown, random spawns, turn encouragement
- ✅ **Detection Systems**: Ball stuck reset, agent wall stuck detection
- ✅ **Balance**: Anti-exploit measures shouldn't prevent legitimate play

### 8. Iterative Refinement Process
- ✅ **Test**: Implement change and observe
- ✅ **Measure**: Use TensorBoard and console logs
- ✅ **Analyze**: Diagnose root causes, not symptoms
- ✅ **Adjust**: Make targeted changes
- ✅ **Repeat**: Training is iterative, not one-shot

### 9. Documentation Value
- ✅ **Historical Context**: Understanding past decisions informs future choices
- ✅ **Lessons Learned**: Mistakes are learning opportunities
- ✅ **Reproducibility**: Detailed notes enable others to learn from this journey

### 10. Patience and Systematic Approach
- ✅ **Long-Term View**: 50+ hours total training time
- ✅ **Checkpoints**: Save regularly for rollback capability
- ✅ **Incremental Changes**: Small adjustments easier to debug than large overhauls
- ✅ **Trust the Process**: Curriculum works if given time

---

## Conclusion

This project demonstrates that successful reinforcement learning requires:

1. **Proper Curriculum Design**: Progressive skill building from simple to complex
2. **Balanced Rewards and Penalties**: Guide exploration, don't crush it
3. **Iterative Refinement**: Monitor, diagnose, adjust, repeat
4. **Patience**: Complex behaviors take time and many iterations
5. **Systematic Debugging**: Root cause analysis over symptom treatment

The training collapse at 4M steps, while frustrating, provided invaluable insights into penalty balancing and the importance of allowing natural curriculum progression. This recovery process is itself a learning opportunity.

**Current State**: Well-positioned for successful completion with softened penalties and extended Lesson 3

**Future Work**: Re-enable self-play after Lesson 4, implement advanced features (passing, assists, dive action)

**This document serves as**: 
- Complete training history for presentations
- Debugging reference for similar projects
- Evidence that RL training is iterative and requires systematic refinement
- Proof that mistakes and recovery are part of the learning process

---

**Last Updated**: November 26, 2025  
**Project Duration**: 50+ hours across 2 stages  
**Current Training Step**: 4M (resuming from 3M with softened penalties)  
**Estimated Completion**: 6M-15M over next 20-25 hours
