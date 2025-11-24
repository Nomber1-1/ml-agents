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

### If training isn't working:
1. Check network_settings > normalize: true
2. Verify ball reference assigned
3. Test in Heuristic mode first
4. Check TensorBoard for NaN values

### If physics are unstable:
1. Reduce maxJointForceLimit (300-400)
2. Increase Decision Period (10)
3. Solver Iterations: 10
4. Collision Detection: Continuous Dynamic

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
max_steps: 10000000
```

### Unity Settings
```
Fixed Timestep: 0.02
Decision Period: 5
Max Environment Steps: 25000
Joint Force Limit: 300-500
```

## 🚦 Training Progress Stages

| Steps | Behavior |
|-------|----------|
| 100k | Standing/balancing |
| 500k | Walking, occasional ball touches |
| 1M | Ball chasing, basic kicking |
| 3M | Team coordination, strategy |
| 5M+ | Advanced tactics, passing |

## 🔗 Quick Links

- [Full Setup Guide](UNITY_SETUP_GUIDE.md)
- [Training Guide](TRAINING_GUIDE.md)
- [Debug Guide](DEBUG_AND_IMPROVEMENTS.md)
- [ML-Agents Docs](https://github.com/Unity-Technologies/ml-agents)

## 💡 Pro Tips

1. Start with curriculum learning (just walking first)
2. Use multiple parallel environments (4-8)
3. Monitor TensorBoard regularly
4. Test in Heuristic mode before training
5. Keep models from different checkpoints
6. Train builds, not editor (2-3x faster)
7. Enable Time.timeScale = 20 for faster training
8. Use GPU if available

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
