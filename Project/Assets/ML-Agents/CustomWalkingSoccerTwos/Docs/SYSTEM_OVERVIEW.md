# Walker Soccer System Overview - Two-Stage Training Architecture

**✅ Project Status**: COMPLETED  
**Final Trained Model**: `results/WalkerStage2_20_V3/WalkerSoccer.onnx`  
**Total Training**: 18M (Stage 1) + 15M (Stage 2) = 33M steps

## 🏗️ Two-Stage Architecture Diagram

```
┌──────────────────────────────────────────────────────────────────────────┐
│                     STAGE 1: LOCOMOTION TRAINING (PPO)                    │
│                                                                            │
│  ┌────────────────────────────────────────────────────────────────────┐  │
│  │               Single-Agent Training Environment                     │  │
│  │  - 10-20 parallel arenas                                           │  │
│  │  - Each arena: 1 WalkerSoccerAgent + 1 moving target              │  │
│  │  - Random spawns (±6m X/Z, 0-360° rotation)                       │  │
│  └────────────────────────────────────────────────────────────────────┘  │
│                             │                                              │
│                             │ Agent observes (250 dims)                   │
│                             ▼                                              │
│  ┌────────────────────────────────────────────────────────────────────┐  │
│  │                    WalkerSoccerAgent                                │  │
│  │  • 16 body parts (ragdoll)                                         │  │
│  │  • 250 observations:                                               │  │
│  │    - 243 locomotion: velocity, rotations, body parts, target       │  │
│  │    - 7 soccer (ZEROS): ball pos/vel, team ID                      │  │
│  │  • 40 continuous actions (39 joints + kick ignored)                │  │
│  │  • locomotion_only = 1.0                                           │  │
│  └────────────────────────────────────────────────────────────────────┘  │
│                             │                                              │
│                             │ PPO Trainer                                  │
│                             ▼                                              │
│  ┌────────────────────────────────────────────────────────────────────┐  │
│  │                   Neural Network (PPO)                              │  │
│  │  - Input: 250 observations (learns to ignore 7 zeros)              │  │
│  │  - Hidden: 512 units × 3 layers                                    │  │
│  │  - Output: 40 continuous actions                                   │  │
│  │  - LR: 0.0003                                                      │  │
│  └────────────────────────────────────────────────────────────────────┘  │
│                             │                                              │
│                             │ 10-20M steps → Checkpoint                    │
│                             ▼                                              │
│                    Stage1_Weights.onnx                                     │
└──────────────────────────────────────────────────────────────────────────┘
                             │
                             │ --initialize-from (transfer learning)
                             ▼
┌──────────────────────────────────────────────────────────────────────────┐
│                   STAGE 2: SOCCER TRAINING (POCA)                         │
│                                                                            │
│  ┌────────────────────────────────────────────────────────────────────┐  │
│  │                  WalkerSoccerEnvController                          │  │
│  │  - Manages 3v3 episodes and resets                                 │  │
│  │  - Tracks scores and timing                                        │  │
│  │  - Coordinates Blue/Purple team groups                             │  │
│  │  - Fixed agent spawns by team (initialPos)                         │  │
│  └────────────────────────────────────────────────────────────────────┘  │
│                             │                                              │
│                             │ manages                                      │
│                             ▼                                              │
│  ┌──────────────────────────────────────────────────────────────────┐   │
│  │                    6 Walker Agents                                │   │
│  │                                                                    │   │
│  │  BLUE TEAM               │          PURPLE TEAM                   │   │
│  │  ├─ Striker 1            │          ├─ Striker 1                  │   │
│  │  ├─ Striker 2            │          ├─ Striker 2                  │   │
│  │  └─ Goalie               │          └─ Goalie                     │   │
│  │                                                                    │   │
│  │  Each agent has:                                                  │   │
│  │  • 16 body parts (ragdoll)                                        │   │
│  │  • 250 observations (SAME as Stage 1):                            │   │
│  │    - 243 locomotion: velocity, rotations, body parts, target      │   │
│  │    - 7 soccer (REAL DATA): ball pos/vel, team ID                  │   │
│  │  • 40 continuous actions (39 joints + kick ACTIVE)                │   │
│  │  • locomotion_only = 0.0                                          │   │
│  └──────────────────────────────────────────────────────────────────┘   │
│                             │                                              │
│                             │ interact with                                │
│                             ▼                                              │
│  ┌──────────────────────────────────────────────────────────────────┐   │
│  │                    Soccer Ball                                    │   │
│  │  - WalkerSoccerBallController                                    │   │
│  │  - Detects goal collisions                                       │   │
│  │  - Triggers scoring events                                       │   │
│  └──────────────────────────────────────────────────────────────────┘   │
│                                                                            │
│  ┌────────────┐                              ┌────────────┐              │
│  │ Blue Goal  │                              │Purple Goal │              │
│  └────────────┘                              └────────────┘              │
│                                                                            │
│                             │                                              │
│                             │ POCA Trainer + Self-Play                    │
│                             ▼                                              │
│  ┌────────────────────────────────────────────────────────────────────┐  │
│  │              Neural Network (POCA, Transferred Weights)             │  │
│  │  - Input: 250 observations (now uses all 7 soccer obs)             │  │
│  │  - Hidden: 512 units × 3 layers (weights from Stage 1)             │  │
│  │  - Output: 40 continuous actions                                   │  │
│  │  - LR: 0.0001 (lower for fine-tuning)                             │  │
│  │  - Self-play: window 5, team_change 200k                          │  │
│  └────────────────────────────────────────────────────────────────────┘  │
│                             │                                              │
│                             │ 15M additional steps                         │
│                             ▼                                              │
│                    Stage2_Weights.onnx                                     │
└──────────────────────────────────────────────────────────────────────────┘
```

## 🔄 Training Loop (Unified for Both Stages)

```
┌──────────────────────────────────────────────────────────────────┐
│                        TRAINING CYCLE                             │
└──────────────────────────────────────────────────────────────────┘

1. OBSERVE
   ↓
   Walker agents collect observations (250 total):
   • Locomotion (243 obs):
     - Body part positions/velocities
     - Velocity goals (4)
     - Rotations (8)
     - Target/ball position (3)
     - 16 body parts × ~14 obs each (228)
   
   • Soccer Context (7 obs):
     - Ball position relative to agent (3)
     - Ball velocity (3)
     - Team identifier (1)
     - Stage 1: All zeros (Vector3.zero + 0f)
     - Stage 2: Real soccer data
   
2. DECIDE
   ↓
   Neural network outputs actions (40 total):
   • 26 joint rotations (chest, spine, limbs, head)
   • 13 joint strengths
   • 1 kick trigger [0,1]
     - Stage 1: Ignored
     - Stage 2: Active when ball_touch >= 0.5 (Lesson 3+)
   
3. ACT
   ↓
   JointDriveController applies forces:
   • ConfigurableJoints move
   • Ragdoll walks/turns/kicks
   • Ball physics react (Stage 2 only)
   
4. REWARD
   ↓
   Agent receives feedback:
   • Stage 1:
     - Locomotion quality (speed matching, direction)
     - Valid touch rewards (+0.2 upright, fast approach)
     - Invalid touch penalties (-0.15 to -1.35 for dives)
     - Turn encouragement (angle reduction, standing penalty)
   
   • Stage 2:
     - Locomotion retained from Stage 1
     - Ball interactions (collision touch rewards)
     - Goals scored/conceded (+50 group reward)
     - Position-specific bonuses (existential rewards)
     - Kick rewards (Lesson 3+: +0.1 per intentional kick)
   
5. LEARN
   ↓
   • Stage 1 (PPO):
     - Single-agent value/policy updates
     - Batch processing
     - Policy optimization
   
   • Stage 2 (POCA):
     - Multi-agent credit assignment
     - Self-play matchmaking
     - Team-based rewards
     - Opponent pool management

   ↓ (repeat every 5 fixed updates)
   
Back to OBSERVE...
```

## 🎯 Observation Flow with Conditional Zero-Padding

```
┌─────────────────────────────────────────────────────────────────┐
│                    AGENT OBSERVATIONS (250 total)                │
└─────────────────────────────────────────────────────────────────┘

OrientationCube
    ↓ (provides stable reference frame)
    │
    ├─→ Locomotion Observations (243 obs, ALWAYS ACTIVE)
    │   ├─ Current vs goal velocity (4 floats)
    │   ├─ Rotation deltas (8 floats)
    │   ├─ Target position (3 floats)
    │   │   - Stage 1: Moving target sphere
    │   │   - Stage 2: Ball position (reused slot)
    │   └─ Per body part (16 parts × ~14 obs each = 228):
    │       ├─ Ground contact (1)
    │       ├─ Linear velocity (3)
    │       ├─ Angular velocity (3)
    │       ├─ Relative position (3)
    │       ├─ Local rotation (4, if not hips/hands)
    │       └─ Joint strength (1, if not hips/hands)
    │
    └─→ Soccer Context Observations (7 obs, CONDITIONAL)
        │
        ├─ if (locomotion_only == 1.0):  ← STAGE 1
        │   ├─ Ball position: Vector3.zero (3 zeros)
        │   ├─ Ball velocity: Vector3.zero (3 zeros)
        │   └─ Team identifier: 0f (1 zero)
        │   └─ Network learns these 7 inputs are uninformative
        │
        └─ if (locomotion_only == 0.0):  ← STAGE 2
            ├─ Ball position relative to agent (3 floats)
            ├─ Ball velocity (3 floats)
            └─ Team identifier (1 float: +1 Blue, -1 Purple)
            └─ Network now uses these 7 inputs for soccer strategy

Total: 243 + 7 = 250 observations (consistent across both stages)
```

## ⚡ Action Flow

```
┌─────────────────────────────────────────────────────────────────┐
│                      ACTION APPLICATION (40 total)               │
└─────────────────────────────────────────────────────────────────┘

Neural Network Output (40 continuous actions [-1, 1])
    ↓
    ├─→ Joint Rotations (26 actions)
    │   ├─ Chest: pitch, yaw, roll (3)
    │   ├─ Spine: pitch, yaw, roll (3)
    │   ├─ ThighL: pitch, yaw (2)
    │   ├─ ThighR: pitch, yaw (2)
    │   ├─ ShinL: pitch (1)
    │   ├─ ShinR: pitch (1)
    │   ├─ FootL: pitch, yaw, roll (3)
    │   ├─ FootR: pitch, yaw, roll (3)
    │   ├─ ArmL: pitch, yaw (2)
    │   ├─ ArmR: pitch, yaw (2)
    │   ├─ ForearmL: pitch (1)
    │   ├─ ForearmR: pitch (1)
    │   └─ Head: pitch, yaw (2)
    │
    ├─→ Joint Strengths (13 actions)
    │   ├─ Chest, Spine, Head (3)
    │   ├─ ThighL, ShinL, FootL (3)
    │   ├─ ThighR, ShinR, FootR (3)
    │   └─ ArmL, ForearmL, ArmR, ForearmR (4)
    │
    └─→ Kick Action (1 action [0,1])
        │
        ├─ Stage 1 (locomotion_only = 1.0):
        │   └─ Kick IGNORED (no ball interaction)
        │
        └─ Stage 2 (locomotion_only = 0.0):
            └─ Kick trigger intensity [0,1]
                ├─ Gated by ball_touch curriculum (active Lesson 3+)
                ├─ Applies 6.0 force impulse to ball
                ├─ 2.0m range from hips
                ├─ 25-step cooldown to prevent spam
                └─ Reward: +0.1 per intentional kick

    ↓ (applied via JointDriveController)

ConfigurableJoint.targetRotation updated
ConfigurableJoint drive force scaled by strength
    ↓
Physics simulation applies forces
    ↓
Ragdoll moves/turns/kicks!
```

## 🛡️ Anti-Exploit Mechanisms

```
┌─────────────────────────────────────────────────────────────────┐
│                   EXPLOIT PREVENTION SYSTEM                      │
└─────────────────────────────────────────────────────────────────┘

1. TOUCH VALIDATION (Stage 1 locomotion)
   ├─ Upright check: Vector3.Dot(hips.up, Vector3.up) >= 0.85
   ├─ Speed limit: horizontalSpeed <= 2.0 m/s
   └─ Dive check: verticalSpeed > -1.5 m/s
       ↓
   Valid touch: +0.2 reward
   Invalid touch: -0.15 base penalty, scaled to -1.35 for severe violations

2. GRADED PENALTIES (Stage 1)
   ├─ Posture multiplier: 1.0-2.5× (worse if upside down)
   ├─ Dive multiplier: 1.0-3.0× (worse for high vertical speed)
   └─ Speed multiplier: 1.0-1.8× (worse for excessive horizontal speed)
       ↓
   Total penalty = -0.15 × postureMult × diveMult × speedMult

3. TURN ENCOURAGEMENT (Stage 1 only, FixedUpdate)
   ├─ Active when target >15° off-center
   ├─ Reward angle reduction: turnProgress × 0.05
   │   └─ turnProgress = 1 - (angleToTarget / 180)
   └─ Penalty for standing still: -0.02 × (angleToTarget / 180)
       └─ Only when speed < 0.5 m/s
       ↓
   Prevents "forward-only" policy exploitation

4. SPAWN RANDOMIZATION
   ├─ Stage 1 (locomotion_only = 1.0):
   │   ├─ Position: ±6m X/Z random offset
   │   └─ Rotation: 0-360° random Y-axis
   │       └─ Forces learning of 360° locomotion
   │
   └─ Stage 2 (locomotion_only = 0.0):
       ├─ Position: Fixed initialPos per agent
       └─ Rotation: Team-based (Blue 90°, Purple -90°)
           └─ Maintains soccer structure

5. KICK COOLDOWN (Stage 2, Lesson 3+)
   └─ 25-step cooldown prevents kick spam
       └─ Encourages strategic timing over button-mashing
```

## 🎁 Reward Structure (Stage-Specific)

```
┌─────────────────────────────────────────────────────────────────┐
│                      STAGE 1: LOCOMOTION REWARDS                 │
└─────────────────────────────────────────────────────────────────┘

EVERY FIXED UPDATE:
├─→ Valid touch reward: +0.2
│   └─ When touching target while upright, not diving, reasonable speed
│
├─→ Invalid touch penalty: -0.15 to -1.35
│   └─ Scaled by posture, dive speed, horizontal speed violations
│
├─→ Turn encouragement (when target >15° off):
│   ├─ Angle reduction: +turnProgress × 0.05
│   └─ Standing penalty: -0.02 × angleToTarget/180 (if speed < 0.5)
│
└─→ Stability penalties:
    ├─ Forward tip: -0.02
    ├─ Sideways lean: -0.02 × lean_amount
    └─ Angular velocity: -0.002 × rad/s

EXPECTED RANGE: -10 (early) → +35 (20M steps)

┌─────────────────────────────────────────────────────────────────┐
│                      STAGE 2: SOCCER REWARDS                     │
└─────────────────────────────────────────────────────────────────┘

EVERY FIXED UPDATE:
├─→ Locomotion Rewards (inherited from Stage 1)
│   ├─ Match speed reward: [0, 1]
│   └─ Look-at reward: [0, 1]
│   
├─→ Height-Based Upright Reward
│   ├─ Optimal zone (0.85-1.3m): +0.03 × height_quality
│   ├─ Partial height (0.5-0.85m): +0.009 × partial_quality
│   └─ Collapse zone (<0.3m): -0.01 × collapse_amount
│
├─→ Stability Penalties
│   ├─ Forward tip (upright_dot < 0.7): -0.02
│   ├─ Sideways lean (abs < 0.6): -0.02 × lean_amount
│   └─ Angular velocity: -0.002 × rad/s
│
├─→ Existential Rewards (per position, delayed 50 steps)
│   ├─ Goalie: +0.25/MaxSteps
│   └─ Striker: -0.25/MaxSteps
│
├─→ Ball Interaction (on collision)
│   └─ Touch reward: +0.2 × ball_touch_curriculum
│
└─→ Kick Action (Lesson 3+ only)
    └─ Intentional kick reward: +0.1 × intensity

ON GOAL:
├─→ Scoring Team: Group reward +50 × (1 - progress_ratio)
└─→ Opposing Team: Group penalty -10

EXPECTED RANGE: -5 (early transfer) → +60+ (15M steps)
```

## 🔀 Self-Play Process (Stage 2 Only)

```
┌─────────────────────────────────────────────────────────────────┐
│                        SELF-PLAY CYCLE                           │
└─────────────────────────────────────────────────────────────────┘

Training begins (Stage 2)
    ↓
Blue Team: Current policy (always latest)
Purple Team: Opponent policy (from snapshot pool)
    ↓
Every 25,000 steps:
    ├─ Current policy plays vs latest snapshot (50%)
    └─ Current policy plays vs historical snapshot (50%)
    ↓
Every 50,000 steps:
    ├─ Save current policy as snapshot
    └─ Add to opponent pool (keep last 5 in window)
    ↓
Every 200,000 steps:
    └─ Update opponent selection distribution
        └─ Based on ELO ratings
    ↓
ELO tracking:
    ├─ Win: ELO increases
    ├─ Loss: ELO decreases
    └─ Draw: Small adjustments
    ↓
Result: Progressively more challenging opponents
→ Agents improve continuously
```

## 📈 Curriculum Progression (Stage 2 Only)

```
┌─────────────────────────────────────────────────────────────────┐
│                   CURRICULUM STAGES (Stage 2)                    │
└─────────────────────────────────────────────────────────────────┘

LESSON 2: Chase Ball (ball_touch=0.35, radius=2.3m) [0-6M steps]
│  Goal: Adapt locomotion skills to soccer
│  ├─ Moderate ball rewards (35%)
│  ├─ Ball spawns 2.3m from center
│  ├─ Kick action: DISABLED
│  ├─ Locomotion skills from Stage 1 retained
│  └─ Focus: collision-based ball touches only
│  Threshold: 40% mean reward (6M steps)
│      ↓

LESSON 3: Kicking Enabled! (ball_touch=0.5, radius=1.8m) [6M-12M steps]
│  Goal: Learn to use kick action
│  ├─ Kick action: ✅ ENABLED (ball_touch >= 0.5)
│  ├─ Ball in kick range (1.8m spawn)
│  ├─ +0.1 reward per kick
│  ├─ 25-step cooldown prevents spam
│  └─ Focus: intentional ball launching, not lunging
│  Threshold: 80% mean reward (12M steps)
│      ↓

LESSON 4: Goal Scoring (ball_touch=1.0, radius=1.4m) [12M-15M steps]
│  Goal: Competitive soccer with strategic kicking
│  ├─ Full ball rewards (100%)
│  ├─ Ball spawns 1.4m (close, intense play)
│  ├─ Goal reward: +50× time bonus
│  └─ Advanced tactics: positioning, passing, defending
│  Completion: 15M total steps
│      ↓
   GRADUATION! ⚽🎓
```

## 🎮 File Relationships

```
Unity Stage 1 Scene (Locomotion)
│
├─ WalkerSoccerAgent.cs ──────────┐
│  locomotion_only = 1.0            │
│  • CollectObservations():         │
│    - 243 locomotion obs           │ Used by agents
│    - 7 soccer obs = ZEROS         │
│  • OnEpisodeBegin():              │
│    - Random spawn ±6m, 0-360°    │
│  • TouchedTarget():               │
│    - Touch validation             │
│  • FixedUpdate():                 │
│    - Turn encouragement           │
│                                   │
├─ TargetController.cs ────────────┤
│  (Moving target sphere)           │
│                                   │
├─ JointDriveController.cs ────────┤
│  (Applies forces to joints)       │
│                                   │
├─ OrientationCubeController.cs ───┤
│  (Stabilized reference frame)     │
│                                   │
└─ DirectionIndicator.cs ──────────┘
   (Visual helper)

   ↓ (exported to)

WalkerSoccerStage1_Locomotion.yaml
   └─ PPO Training, LR 0.0003, 10-20M steps
      └─ Trains neural network
         └─ Exported as Stage1.onnx
            └─ Used for --initialize-from

─────────────────────────────────────────────

Unity Stage 2 Scene (Soccer)
│
├─ WalkerSoccerAgent.cs (×6) ──────┐
│  locomotion_only = 0.0             │
│  • CollectObservations():          │
│    - 243 locomotion obs            │
│    - 7 soccer obs = REAL DATA      │ Used by agents
│  • OnEpisodeBegin():               │
│    - Fixed spawn initialPos        │
│  • OnActionReceived():             │
│    - Kick action ACTIVE            │
│                                    │
├─ JointDriveController.cs ─────────┤
│  (Applies forces to joints)        │
│                                    │
├─ OrientationCubeController.cs ────┤
│  (Stabilized reference frame)      │
│                                    │
└─ DirectionIndicator.cs ───────────┘
   (Visual helper)

├─ WalkerSoccerEnvController.cs
│  (Manages 3v3 environment)
│  └─ References all 6 agents
│
├─ WalkerSoccerBallController.cs
│  (Ball physics & scoring)
│  └─ References environment controller
│
└─ SoccerSettings.cs
   (Configuration)
   └─ Referenced by agents

   ↓ (exported to)

WalkerSoccerStage2_Soccer.yaml
   └─ POCA Training, LR 0.0001, 15M steps
      └─ Transfers weights from Stage1.onnx
         └─ Trains with self-play
            └─ Exported as Stage2.onnx
               └─ Loaded back into Unity for inference
```

## 🚀 Complete Two-Stage Workflow

```
1. STAGE 1 SETUP
   ├─ Build Unity scene with locomotion arenas
   ├─ Configure WalkerSoccerAgent (locomotion_only=1.0)
   ├─ Set observation space: 250 in BehaviorParameters
   ├─ Create WalkerSoccerStage1_Locomotion.yaml
   └─ Build executable: WalkerStage1.exe

2. STAGE 1 TRAINING
   ├─ Run: mlagents-learn WalkerSoccerStage1_Locomotion.yaml \
   │       --env=WalkerStage1.exe --run-id=WalkerStage1
   ├─ Monitor TensorBoard (target: +20 to +35 reward)
   ├─ Watch for confident turning behavior (>30° off-center)
   └─ Train 10-20M steps (6-12 hours)

3. STAGE 2 SETUP
   ├─ Build Unity scene with 3v3 soccer
   ├─ Configure WalkerSoccerAgent (locomotion_only=0.0)
   ├─ Set observation space: 250 (MUST MATCH Stage 1)
   ├─ Create WalkerSoccerStage2_Soccer.yaml
   └─ Build executable: WalkerStage2.exe

4. STAGE 2 TRAINING (TRANSFER LEARNING)
   ├─ Run: mlagents-learn WalkerSoccerStage2_Soccer.yaml \
   │       --env=WalkerStage2.exe --run-id=WalkerStage2 \
   │       --initialize-from=WalkerStage1
   ├─ Verify Policy transfers (no Policy warning)
   ├─ Optimizer warnings normal (PPO→POCA transition)
   ├─ Monitor locomotion retention (agents walk immediately)
   ├─ Watch curriculum progress (Lesson 2 → 3 → 4)
   └─ Train 15M steps (8-15 hours)

5. EVALUATION PHASE
   ├─ Load Stage2.onnx model
   ├─ Set Behavior Type: Inference Only
   ├─ Watch agents play soccer with retained locomotion!
   └─ Test different scenarios

6. ITERATION PHASE (if needed)
   ├─ Adjust rewards (Stage 1 or Stage 2)
   ├─ Tune hyperparameters
   ├─ Add advanced features
   └─ Retrain stage(s) as needed
```

---

This two-stage architecture enables efficient transfer learning: agents master locomotion in Stage 1, then apply those skills to soccer in Stage 2. The 250-observation zero-padding strategy ensures perfect weight transfer while allowing different training focuses per stage!

## 🔄 Training Loop

```
┌──────────────────────────────────────────────────────────────────┐
│                        TRAINING CYCLE                             │
└──────────────────────────────────────────────────────────────────┘

1. OBSERVE
   ↓
   Walker agents collect observations:
   • Body part positions/velocities
   • Ball position/velocity
   • Team/role information
   • Ground contacts
   
2. DECIDE
   ↓
   Neural network outputs actions:
   • 39 continuous values [-1, 1]
   • 26 joint rotations
   • 13 joint strengths
   
3. ACT
   ↓
   JointDriveController applies forces:
   • ConfigurableJoints move
   • Ragdoll walks/kicks
   • Ball physics react
   
4. REWARD
   ↓
   Agent receives feedback:
   • Locomotion quality
   • Ball interactions
   • Goals scored/conceded
   • Position-specific bonuses
   
5. LEARN
   ↓
   MA-POCA updates network:
   • Batch processing
   • Credit assignment
   • Self-play matchmaking
   • Policy optimization

   ↓ (repeat every 5 fixed updates)
   
Back to OBSERVE...
```

## 🎯 Observation Flow

```
┌─────────────────────────────────────────────────────────────────┐
│                    AGENT OBSERVATIONS                            │
└─────────────────────────────────────────────────────────────────┘

OrientationCube
    ↓ (provides stable reference frame)
    │
    ├─→ Locomotion Observations (from WalkerAgent heritage)
    │   ├─ Current vs goal velocity (4 floats)
    │   ├─ Rotation deltas (8 floats)
    │   ├─ Target position (3 floats)
    │   └─ Per body part (16 parts × ~12 obs each):
    │       ├─ Ground contact (1)
    │       ├─ Linear velocity (3)
    │       ├─ Angular velocity (3)
    │       ├─ Relative position (3)
    │       ├─ Local rotation (4, if not hips/hands)
    │       └─ Joint strength (1, if not hips/hands)
    │
    └─→ Soccer Observations (new in merged agent)
        ├─ Ball position relative to agent (3)
        ├─ Ball velocity (3)
        ├─ Team identifier (1: +1 Blue, -1 Purple)
        └─ Position type (1: +1 Striker, -1 Goalie, 0 Generic)

Total: ~150-200 observations (exact count depends on body part config)
```

## ⚡ Action Flow

```
┌─────────────────────────────────────────────────────────────────┐
│                      ACTION APPLICATION                          │
└─────────────────────────────────────────────────────────────────┘

Neural Network Output (40 continuous actions)
    ↓
    ├─→ Joint Rotations (26 actions)
    │   ├─ Chest: pitch, yaw, roll (3)
    │   ├─ Spine: pitch, yaw, roll (3)
    │   ├─ ThighL: pitch, yaw (2)
    │   ├─ ThighR: pitch, yaw (2)
    │   ├─ ShinL: pitch (1)
    │   ├─ ShinR: pitch (1)
    │   ├─ FootL: pitch, yaw, roll (3)
    │   ├─ FootR: pitch, yaw, roll (3)
    │   ├─ ArmL: pitch, yaw (2)
    │   ├─ ArmR: pitch, yaw (2)
    │   ├─ ForearmL: pitch (1)
    │   ├─ ForearmR: pitch (1)
    │   └─ Head: pitch, yaw (2)
    │
    ├─→ Joint Strengths (13 actions)
    │   ├─ Chest, Spine, Head (3)
    │   ├─ ThighL, ShinL, FootL (3)
    │   ├─ ThighR, ShinR, FootR (3)
    │   └─ ArmL, ForearmL, ArmR, ForearmR (4)
    │
    └─→ Kick Action (1 action, V10+)
        └─ Kick trigger intensity [0,1]
            ├─ Only active when ball_touch >= 0.5 (Lesson 3+)
            ├─ Applies forward + upward impulse to ball
            ├─ 25-step cooldown to prevent spam
            └─ 2.0m range from hips

    ↓ (applied via JointDriveController)

ConfigurableJoint.targetRotation updated
ConfigurableJoint drive force scaled by strength
    ↓
Physics simulation applies forces
    ↓
Ragdoll moves!
```

## 🎁 Reward Structure

```
┌─────────────────────────────────────────────────────────────────┐
│                      REWARD BREAKDOWN                            │
└─────────────────────────────────────────────────────────────────┘

EVERY FIXED UPDATE:
│
├─→ Locomotion Rewards (continuous, scaled dynamically 1.0-2.0x)
│   ├─ Match speed reward: [0, 1]
│   │   └─ Based on how well velocity matches target
│   └─ Look-at reward: [0, 1]
│       └─ Based on head facing target direction
│   
├─→ Height-Based Upright Reward
│   ├─ Optimal zone (0.85-1.3m): +0.03 × height_quality
│   ├─ Partial height (0.5-0.85m): +0.009 × partial_quality
│   └─ Collapse zone (<0.3m): -0.01 × collapse_amount
│
├─→ Stability Penalties
│   ├─ Forward tip (upright_dot < 0.7): -0.02
│   ├─ Sideways lean (abs < 0.6): -0.02 × lean_amount
│   └─ Angular velocity: -0.002 × rad/s
│
├─→ Existential Rewards (per position, delayed 50 steps)
│   ├─ Goalie: +0.25/MaxSteps
│   └─ Striker: -0.25/MaxSteps
│
├─→ Ball Interaction (on collision)
│   └─ Touch reward: +0.2 × ball_touch_curriculum
│
└─→ Kick Action (Lesson 3+ only, V10)
    └─ Intentional kick reward: +0.1 × intensity

ON GOAL (V10: Massively Increased!):
│
├─→ Scoring Team
│   └─ Group reward: +50 × (1 - progress_ratio)
│       └─ Early goals worth 50×, late goals 5× (time bonus)
│
└─→ Opposing Team
    └─ Group penalty: -10

TOTAL EPISODE REWARD RANGE (V10):
• Standing baseline: ~26
• Good locomotion: 30-45
• With goals: 50-80+
• Poor performance: -5 to +15
```

## 🔀 Self-Play Process

```
┌─────────────────────────────────────────────────────────────────┐
│                        SELF-PLAY CYCLE                           │
└─────────────────────────────────────────────────────────────────┘

Training begins
    ↓
Blue Team: Current policy (always latest)
Purple Team: Opponent policy (from snapshot pool)
    ↓
Every 25,000 steps:
    ├─ Current policy plays vs latest snapshot (50%)
    └─ Current policy plays vs historical snapshot (50%)
    ↓
Every 50,000 steps:
    ├─ Save current policy as snapshot
    └─ Add to opponent pool (keep last 10)
    ↓
Every 200,000 steps:
    └─ Update opponent selection distribution
        └─ Based on ELO ratings
    ↓
ELO tracking:
    ├─ Win: ELO increases
    ├─ Loss: ELO decreases
    └─ Draw: Small adjustments
    ↓
Result: Progressively more challenging opponents
→ Agents improve continuously
```

## 📈 Curriculum Progression

```
┌─────────────────────────────────────────────────────────────────┐
│                   CURRICULUM STAGES                              │
└─────────────────────────────────────────────────────────────────┘

LESSON 0: Stand and Balance (ball_touch=0.0, radius=3.0m) [0-2M steps]
│  Goal: Master upright posture and stability
│  ├─ No ball influence (ball_touch=0)
│  ├─ Ball spawns far away (ignored)
│  ├─ Kick action: DISABLED
│  └─ Focus: height rewards, angular damping, collapse penalties
│  Progress: 5% of max_steps (2M steps, 50 episodes min)
│      ↓

LESSON 1: Walk Towards Ball (ball_touch=0.15, radius=2.7m) [2M-6M steps]
│  Goal: Learn forward locomotion
│  ├─ Minimal ball rewards (15%)
│  ├─ Ball medium distance
│  ├─ Kick action: DISABLED
│  └─ Focus: speed matching, direction alignment
│  Progress: 15% of max_steps (6M steps, 800 episodes min)
│      ↓

LESSON 2: Chase Ball (ball_touch=0.35, radius=2.3m) [6M-12M steps]
│  Goal: Active ball pursuit without kicking
│  ├─ Moderate ball rewards (35%)
│  ├─ Ball closer, more encounters
│  ├─ Kick action: DISABLED
│  └─ Focus: collision-based ball touches only
│  Progress: 30% of max_steps (12M steps, 1000 episodes min)
│      ↓

LESSON 3: Kicking Enabled! (ball_touch=0.5, radius=1.8m) [12M-20M steps]
│  Goal: Learn to use kick action
│  ├─ Kick action: ✅ ENABLED (ball_touch >= 0.5)
│  ├─ Ball in kick range (1.8m spawn)
│  ├─ +0.1 reward per kick
│  ├─ 25-step cooldown prevents spam
│  └─ Focus: intentional ball launching, not lunging
│  Progress: 50% of max_steps (20M steps, 1000 episodes min)
│      ↓

LESSON 4: Goal Scoring (ball_touch=1.0, radius=1.4m) [20M+ steps]
│  Goal: Competitive soccer with strategic kicking
│  ├─ Full ball rewards (100%)
│  ├─ Ball spawns near agents
│  ├─ Goal reward: +50× time bonus
│  └─ Advanced tactics: positioning, passing, defending
│      ↓
   GRADUATION! ⚽🎓
```

## 🎮 File Relationships

```
Unity Scene
│
├─ WalkerSoccerAgent.cs ──────────┐
│  (Main agent logic)               │
│                                   │
├─ JointDriveController.cs ────────┤ Used by agents
│  (Applies forces to joints)      │
│                                   │
├─ OrientationCubeController.cs ───┤
│  (Stabilized reference frame)    │
│                                   │
└─ DirectionIndicator.cs ──────────┘
   (Visual helper)

├─ WalkerSoccerEnvController.cs
│  (Manages environment)
│  └─ References all 6 agents
│
├─ WalkerSoccerBallController.cs
│  (Ball physics & scoring)
│  └─ References environment controller
│
└─ SoccerSettings.cs
   (Configuration)
   └─ Referenced by agents

   ↓ (exported to)

WalkerSoccer.yaml
   └─ Training configuration
      └─ Used by mlagents-learn command
         └─ Trains neural network
            └─ Exported as .onnx model
               └─ Loaded back into Unity
```

## 🚀 Complete Workflow

```
1. SETUP PHASE
   ├─ Build Unity scene (UNITY_SETUP_GUIDE.md)
   ├─ Configure agents and components
   └─ Create WalkerSoccer.yaml config

2. TRAINING PHASE
   ├─ Run: mlagents-learn WalkerSoccer.yaml --run-id=MyRun
   ├─ Monitor TensorBoard
   ├─ Watch agents learn in Unity
   └─ Wait for 10M steps (8-48 hours)

3. EVALUATION PHASE
   ├─ Load trained .onnx model
   ├─ Set Behavior Type: Inference Only
   ├─ Watch agents play soccer!
   └─ Test different scenarios

4. ITERATION PHASE
   ├─ Adjust rewards (if needed)
   ├─ Tune hyperparameters
   ├─ Add advanced features
   └─ Retrain with improvements
```

---

This overview shows how all components work together to create intelligent soccer-playing walker agents. Each layer builds on the previous one, from physics simulation up to strategic team play!
