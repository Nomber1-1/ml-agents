# Walker Soccer - Quick Reference Card (Two-Stage Training)

**✅ Project Status**: COMPLETED  
**Final Model**: `results/WalkerStage2_20_V3/WalkerSoccer.onnx`  
**Total Training**: 18M (Stage 1) + 15M (Stage 2) = 33M steps

## 🎯 Essential Commands

### Stage 1: Locomotion Training (PPO)
```bash
# Start Stage 1 (locomotion only, 10-20M steps)
mlagents-learn WalkerSoccerStage1_Locomotion.yaml --run-id=WalkerStage1 --force

# With build and multiple environments
mlagents-learn WalkerSoccerStage1_Locomotion.yaml --run-id=WalkerStage1 \
  --env=Builds/WalkerStage1.exe --num-envs=4 --no-graphics

# Resume Stage 1
mlagents-learn WalkerSoccerStage1_Locomotion.yaml --run-id=WalkerStage1 --resume
```

### Stage 2: Soccer Training (POCA + Transfer)
```bash
# Start Stage 2 with transferred weights
mlagents-learn WalkerSoccerStage2_Soccer.yaml --run-id=WalkerStage2 \
  --initialize-from=WalkerStage1 --force

# With build and multiple environments
mlagents-learn WalkerSoccerStage2_Soccer.yaml --run-id=WalkerStage2 \
  --env=Builds/WalkerStage2.exe --num-envs=3 --no-graphics \
  --initialize-from=WalkerStage1

# Resume Stage 2
mlagents-learn WalkerSoccerStage2_Soccer.yaml --run-id=WalkerStage2 --resume
```

### Monitoring
```bash
# View training metrics
tensorboard --logdir results

# Open browser to http://localhost:6006
```

## 📋 Build Checklist

### Stage 1 Build (Locomotion)
- ✅ Single WalkerSoccerAgent per arena (10-20 arenas recommended)
- ✅ Moving target sphere with TargetController
- ✅ BehaviorParameters:
  - Vector Observation: **250**
  - Continuous Actions: **40**
  - Behavior Name: `WalkerSoccer`
- ✅ DecisionRequester (Decision Period: 5)
### Stage 2 Build (Soccer)
- ✅ 6 WalkerSoccerAgents (3 per team)
- ✅ BehaviorParameters (same as Stage 1):
  - Team ID: 0 (Blue), 1 (Purple)
- ✅ Ball with WalkerSoccerBallController

## 🎮 Testing Controls (Heuristic Mode)

```
```

## 🔍 Debug Checklist

### Stage 1 Issues

#### Agents dive toward target instead of walking:
1. ✅ Check `TouchedTarget()` validation is enabled
2. ✅ Verify `uprightDotThreshold = 0.85`
3. ✅ Verify `maxSuccessSpeed = 2.0`
4. ✅ Turn encouragement active in FixedUpdate
5. ✅ Walking speed range: 0.8-4.0 m/s

#### Agents won't turn (forward-only locomotion):
1. ✅ Check turn encouragement in FixedUpdate
2. ✅ Penalty for standing still when target >15° off
3. ✅ Reward for reducing angle to target
4. ✅ Random agent spawns enabled (±6m X/Z, 0-360° rotation)

#### Training reward stuck at 80+:
1. ❌ Dive-reset exploitation happening
2. ✅ Lower touch rewards to +0.2
3. ✅ Increase dive penalties to -0.15 to -1.35
4. ✅ Narrow walking speed to 0.8-4.0 m/s

### Stage 2 Issues

#### Transfer learning fails (Policy not loading):
1. ✅ Check both stages use Behavior Name: `WalkerSoccer`
2. ✅ Verify observation space: **250 in both stages**
3. ✅ Confirm `--initialize-from=<correct_stage1_run_id>`
4. ⚠️ Optimizer warnings are normal (PPO→POCA transition)

#### Agents forgot how to walk:
1. ❌ Observation space mismatch (check Unity BehaviorParameters)
2. ✅ Should be 250 in both Stage 1 and Stage 2 builds
3. ✅ Policy should transfer successfully (no Policy warning)

#### Agents spawn randomly in Stage 2:
1. ✅ Check `locomotion_only = 0.0` in Stage 2 YAML
2. ✅ Spawn should use `initialPos` for fixed positions
3. ✅ Stage 1 uses random, Stage 2 uses fixed

### General Issues

#### Agents fall through floor:
1. Ground has Collider
2. Collision layers set correctly
3. Rigidbody Use Gravity enabled
4. Fixed Timestep = 0.02

#### Agents fall over at start:
1. Random agent spawns in Stage 1 (normal)
2. Check joint strength settings
3. Solver iterations: 12+
4. Reduce max walking speed if needed

#### Training shows NaN:
1. network_settings > normalize: true
2. Reduce learning rate
3. Check for physics explosions
4. Verify all observations are valid

## 📊 Key Metrics (TensorBoard)

### Stage 1 (PPO Locomotion)
| Metric | Target |
|--------|--------|
| Mean Reward | +20 to +35 |
| Valid Touch Rate | Increasing |
| Episode Length | Varies (target resets) |
| Policy Loss | Decreasing, stable |

### Stage 2 (POCA Soccer)
| Metric | Target |
|--------|--------|
| Mean Reward | +40 to +60+ |
| Self-Play ELO | Both ~1200 |
| Goals Scored | Increasing in Lesson 4 |
| Policy Loss | Decreasing, stable |

## ⚙️ Key Parameters

### Stage 1: WalkerSoccerStage1_Locomotion.yaml
```yaml
trainer_type: ppo
learning_rate: 0.0003
batch_size: 2048
hidden_units: 512
num_layers: 3
max_steps: 10000000  # 10M minimum

environment_parameters:
  locomotion_only: 1.0  # Stage 1 mode
  target_walking_speed:
### Stage 2: WalkerSoccerStage2_Soccer.yaml
```yaml
trainer_type: poca  # Multi-agent
learning_rate: 0.0001  # Lower for fine-tuning
max_steps: 15000000  # 15M additional

self_play:
  save_steps: 50000
  team_change: 200000

environment_parameters:
  locomotion_only: 0.0  # Stage 2 mode
  ball_touch: [curriculum]  # 0.35 → 0.5 → 1.0
```

### Unity Settings (Both Stages)
```
Fixed Timestep: 0.02
Decision Period: 5
Vector Observation Space: 250
Continuous Actions: 40
```

### WalkerSoccerAgent Inspector
```
uprightDotThreshold: 0.85
maxSuccessSpeed: 2.0
spawnAreaHalfX: 6.0
spawnAreaHalfZ: 6.0
kickForce: 6.0
kickRange: 2.0
kickCooldownSteps: 25
```

## 🚦 Training Progress

### Stage 1: Locomotion (10-20M steps)
| Steps | Mean Reward | Behavior |
|-------|-------------|----------|
| 0-2M | -10 → +5 | Learning to stand |
| 2M-5M | +5 → +15 | Stable walking forward |
| 5M-10M | +15 → +25 | Beginning to turn |
| 10M-20M | +25 → +35 | Full 360° locomotion |

**Success Criteria:** Mean reward >+20, agents turn toward off-angle targets

| Steps | Lesson | Mean Reward | Kick? |
|-------|--------|-------------|-------|
| 0-2M | L2: Chase | -5 → +5 | ❌ |
| 2M-6M | L2: Chase | +5 → +20 | ❌ |
**Success Criteria:** Goals scored, coordinated team play, ELO ratings stable


## 💡 Pro Tips
3. Monitor valid vs invalid touch ratio
4. Stop when agents confidently turn >30° off-center
5. Aim for 10M-20M steps before Stage 2

### Stage 2
6. Use same Behavior Name as Stage 1
7. Observation space MUST be 250 (match Stage 1)
8. Optimizer warnings are normal (PPO→POCA)
9. Lower `num-envs` for POCA (3-4 vs 4-8 for PPO)
10. Monitor locomotion retention in early steps

### Performance
11. Build training 2-3x faster than Editor
12. Multi-GPU: separate sessions per GPU
13. Scale `--num-envs` by VRAM (12GB → 12-16 envs Stage 1, 6-9 envs Stage 2)
14. Use `--no-graphics` for headless training
15. TensorBoard running = instant feedback

## 🆘 Emergency Fixes

### Stage 1: Dive exploitation
```yaml
# Narrow walking speed range
target_walking_speed:
  min_value: 0.8
  max_value: 4.0  # Was 10.0
```

### Stage 2: Transfer failed
```bash
# Verify observation space in Unity
# Must be 250 in BOTH Stage 1 and Stage 2 builds
```

### General: Training unstable
```yaml
# Reduce learning rate
learning_rate: 0.0001  # From 0.0003
```

---

**Quick Start:** Build Stage 1 → Train 10-20M steps → Build Stage 2 → Transfer and train 15M steps!
