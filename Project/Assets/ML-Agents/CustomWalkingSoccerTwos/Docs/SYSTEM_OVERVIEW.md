# Walker Soccer System Overview

## 🏗️ Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                    WALKER SOCCER ENVIRONMENT                     │
│                                                                   │
│  ┌────────────────────────────────────────────────────────────┐ │
│  │                  WalkerSoccerEnvController                  │ │
│  │  - Manages episodes and resets                             │ │
│  │  - Tracks scores and timing                                │ │
│  │  - Coordinates Blue/Purple team groups                     │ │
│  └────────────────────────────────────────────────────────────┘ │
│                             │                                     │
│                             │ manages                             │
│                             ▼                                     │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │                    6 Walker Agents                        │   │
│  │                                                            │   │
│  │  BLUE TEAM               │          PURPLE TEAM           │   │
│  │  ├─ Striker 1            │          ├─ Striker 1          │   │
│  │  ├─ Striker 2            │          ├─ Striker 2          │   │
│  │  └─ Goalie               │          └─ Goalie             │   │
│  │                                                            │   │
│  Each agent has:                                          │   │
│  • 16 body parts (ragdoll)                                │   │
│  • 40 continuous actions (39 + kick trigger)              │   │
│  • ~150-200 observations                                  │   │
│  • Position role (Striker/Goalie)                         │   │
│  └──────────────────────────────────────────────────────────┘   │
│                             │                                     │
│                             │ interact with                       │
│                             ▼                                     │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │                    Soccer Ball                            │   │
│  │  - WalkerSoccerBallController                            │   │
│  │  - Detects goal collisions                               │   │
│  │  - Triggers scoring events                               │   │
│  └──────────────────────────────────────────────────────────┘   │
│                                                                   │
│  ┌────────────┐                              ┌────────────┐     │
│  │ Blue Goal  │                              │Purple Goal │     │
│  │  (Tag)     │                              │   (Tag)    │     │
│  └────────────┘                              └────────────┘     │
│                                                                   │
└─────────────────────────────────────────────────────────────────┘

                             │
                             │ ML-Agents Communication
                             ▼

┌─────────────────────────────────────────────────────────────────┐
│                      PYTHON ML-AGENTS                            │
│                                                                   │
│  ┌────────────────────────────────────────────────────────────┐ │
│  │                    MA-POCA Trainer                          │ │
│  │  - Multi-agent credit assignment                           │ │
│  │  - Self-play for competition                               │ │
│  │  - Curriculum learning                                     │ │
│  │  - Parameter randomization                                 │ │
│  └────────────────────────────────────────────────────────────┘ │
│                             │                                     │
│                             │ trains                              │
│                             ▼                                     │
│  ┌────────────────────────────────────────────────────────────┐ │
│  │                   Neural Network                            │ │
│  │  - Input: ~200 observations                                │ │
│  │  - Hidden: 512 units x 3 layers                            │ │
│  │  - Output: 39 continuous actions                           │ │
│  │  - Normalize observations                                  │ │
│  └────────────────────────────────────────────────────────────┘ │
│                                                                   │
└─────────────────────────────────────────────────────────────────┘
```

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
