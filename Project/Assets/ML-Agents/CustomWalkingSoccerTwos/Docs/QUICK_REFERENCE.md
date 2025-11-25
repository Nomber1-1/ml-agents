# Walker Soccer - Quick Reference Card

## 🎯 Essential Commands

```bash
# Start training
mlagents-learn WalkerSoccer.yaml --run-id=MySoccerRun

# Resume training
mlagents-learn WalkerSoccer.yaml --run-id=MySoccerRun --resume

# View in TensorBoard
tensorboard --logdir results

# Train headless (faster)
mlagents-learn WalkerSoccer.yaml --run-id=MySoccerRun --no-graphics
```

## 📋 Component Checklist

### Per Walker Agent
- ✅ WalkerSoccerAgent.cs
- ✅ JointDriveController.cs
- ✅ BehaviorParameters (Team ID: 0 for Blue, 1 for Purple)
  - ✅ Vector Action: Space Size = 40 (39 + kick action)
  - ✅ Vector Action: Space Type = Continuous
- ✅ DecisionRequester (Decision Period: 5)
- ✅ 16 Body Parts assigned
- ✅ Ball reference assigned
- ✅ OrientationCube child
- ✅ DirectionIndicator child

### Environment
- ✅ WalkerSoccerEnvController.cs on area
- ✅ 6 agents in AgentsList (3 per team)
- ✅ Ball with WalkerSoccerBallController.cs
- ✅ Goals with tags: "blueGoal", "purpleGoal"
- ✅ SoccerSettings in scene

## 🎮 Testing Controls (Heuristic Mode)

```
W/S - Forward/Backward
A/D - Rotate
WASD - Basic movement control
```

## 🔍 Debug Checklist

### If agents fall through floor:
1. Ground has BoxCollider
2. Collision layers properly set
3. Rigidbody Use Gravity ✓
4. Fixed Timestep = 0.02

### If agents fall over at episode start:
1. Enable start stabilization in WalkerSoccerSettings
2. Set stabilizeStepsOnReset = 50 (V5 default)
3. Set standStrength = 0.9
4. Set maxTargetSpeed = 3.0 (lower speeds help balance)
5. Increase solver iterations to 12

### If training isn't working:
1. Check network_settings > normalize: true
2. Verify ball reference assigned
3. Test in Heuristic mode first
4. Check TensorBoard for NaN values

### If physics are unstable:
1. Enable start stabilization (see above)
2. Reduce maxJointForceLimit (300-400)
3. Increase Decision Period (10)
4. Solver Iterations: 12-15
5. Collision Detection: Continuous Dynamic

## 📊 Key Metrics (TensorBoard)

| Metric | Good Sign | Bad Sign |
|--------|-----------|----------|
| Cumulative Reward | Increasing | Flat/Negative |
| Policy Loss | Decreasing | Increasing |
| Episode Length | Stabilizing | Erratic |
| Self-Play ELO | Both teams ~1200 | One team >> other |

## ⚙️ Key Parameters

### WalkerSoccer.yaml
```yaml
learning_rate: 0.0003
batch_size: 2048
hidden_units: 512
gamma: 0.99
max_steps: 30000000
```

### Unity Settings
```
Fixed Timestep: 0.02
Decision Period: 5
Max Environment Steps: 25000
Joint Force Limit: 300-500
```

### WalkerSoccerSettings (Scene Object)
```
enableStartStabilization: true
stabilizeStepsOnReset: 50
standStrength: 0.9
solverIterations: 12
solverVelocityIterations: 12
```

### WalkerSoccerAgent Reward Tuning (Inspector)
```
maxTargetSpeed: 3.0
speedRampSteps: 400
uprightRewardPerStep: 0.01
locomotionRewardScale: 2.0
antiForwardTipPenalty: 0.02
uprightDotMin: 0.7
angVelPenaltyCoef: 0.001
sidewaysLeanPenalty: 0.02
delayBallInfluenceSteps: 50
```

## 🚦 Training Progress Stages (V10)

| Steps | Lesson | Behavior | Kick? |
|-------|--------|----------|-------|
| 0-2M | L0: Stand | Balance, upright posture | ❌ |
| 2M-6M | L1: Walk | Walking toward ball (2.7m) | ❌ |
| 6M-12M | L2: Chase | Active pursuit (2.3m) | ❌ |
| 12M-20M | L3: Kick | **Kick action enabled!** (1.8m) | ✅ |
| 20M+ | L4: Score | Goal-focused play (1.4m) | ✅ |

## 🔗 Quick Links

- [Full Setup Guide](UNITY_SETUP_GUIDE.md)
- [Training Guide](TRAINING_GUIDE.md)
- [Debug Guide](DEBUG_AND_IMPROVEMENTS.md)
- [ML-Agents Docs](https://github.com/Unity-Technologies/ml-agents)

## 💡 Pro Tips

1. Enable start stabilization in WalkerSoccerSettings
2. Use multiple parallel environments (4-8 per GPU)
3. Multi-GPU: run separate training sessions per GPU
4. Monitor TensorBoard regularly
5. Test in Heuristic mode before training
6. Keep models from different checkpoints
7. Train builds, not editor (2-3x faster)
8. Enable Time.timeScale = 20 for faster training
9. Scale --num-envs based on GPU VRAM (12GB → 12-16 envs)

## 🆘 Emergency Fixes

```csharp
// If NaN in rewards
if (float.IsNaN(reward)) return;

// If agents too weak/strong
maxJointForceLimit = 350f; // Adjust

// If ball too fast
ballRigidbody.drag = 0.2f; // Increase
```

---

**Got 5 minutes?** Test Heuristic mode → See if physics work → Start training!
