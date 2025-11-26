# Walker Soccer Training Guide - Two-Stage Approach

## Overview

This project uses a **two-stage transfer learning approach** to train humanoid ragdoll agents for 3v3 soccer:

- **Stage 1 (PPO)**: Pure locomotion training (stand, walk, turn) - 10-20M steps
- **Stage 2 (POCA)**: Soccer gameplay with transferred locomotion skills - 15M steps

This approach is more efficient than single-phase training and produces better final results by separating skill acquisition.

---

## Prerequisites

Before training, ensure you have:

1. **Python 3.8-3.10** installed
2. **ML-Agents Python package** installed:
   ```bash
   pip install mlagents==1.0.0
   ```
3. **PyTorch** installed (CPU or GPU version):
   ```bash
   # CPU version
   pip install torch
   
   # GPU version (CUDA 11.8)
   pip install torch --index-url https://download.pytorch.org/whl/cu118
   ```
4. **Two Unity builds** following `UNITY_SETUP_GUIDE.md`:
   - Stage 1: Locomotion training scene (single agent + target)
   - Stage 2: Full 3v3 soccer scene

---

## Stage 1: Locomotion Training (PPO)

### Purpose
Train the humanoid ragdoll to:
- Stand upright and maintain balance
- Walk toward moving targets at various speeds
- Turn in all directions (360° locomotion)
- Handle randomized spawn positions and orientations

### Build Setup

**Unity Scene Configuration:**
- Single `WalkerSoccerAgent` per arena (recommend 10-20 arenas)
- Moving target sphere (using `TargetController`)
- BehaviorParameters:
  - Vector Observation Space: **250**
  - Continuous Actions: **40**
  - Behavior Name: `WalkerSoccer`

### Training Command

```powershell
mlagents-learn Assets/ML-Agents/CustomWalkingSoccerTwos/WalkerSoccerStage1_Locomotion.yaml `
  --env="Builds/WalkerStage1_Locomotion.exe" `
  --num-envs=4 --no-graphics `
  --run-id=WalkerStage1 --force
```

### Stage 1 Config Highlights

```yaml
trainer_type: ppo  # Single-agent PPO (faster than POCA)
learning_rate: 0.0003
max_steps: 10000000  # 10M steps minimum (15-20M recommended)
hidden_units: 512
num_layers: 3

environment_parameters:
  locomotion_only:
    value: 1.0  # Stage 1 mode (zeros out 7 soccer observations)
  target_walking_speed:
    min_value: 0.8
    max_value: 4.0  # Moderate speeds to encourage walking
```

### Expected Progress

| Steps | Mean Reward | Behavior |
|-------|-------------|----------|
| 0-2M | -10 → +5 | Learning to stand, frequent falls |
| 2M-5M | +5 → +15 | Stable standing, basic forward walking |
| 5M-10M | +15 → +25 | Smooth walking, beginning to turn |
| 10M-20M | +25 → +35 | Confident 360° turning, robust locomotion |

**Key Success Indicators:**
- Agents walk smoothly at 0.8-4.0 m/s speeds
- Turn toward off-angle targets (not just stand and wait for reset)
- Mean reward stabilizes above +20
- Touch target rewards (+0.2) occur frequently while upright

### When to Stop Stage 1

Move to Stage 2 when:
- ✅ Mean reward consistently above +20-25
- ✅ Agents turn confidently toward targets >30° off-center
- ✅ Training reached 10M-20M steps
- ✅ Valid target touches (upright, controlled speed) outnumber dive penalties

---

## Stage 2: Soccer Training (POCA)

### Purpose
Transfer locomotion skills and learn:
- Ball chasing and positioning
- Intentional kicking (40th action)
- Team coordination (3v3)
- Goal scoring and defense

### Build Setup

**Unity Scene Configuration:**
- Full 3v3 soccer setup (6 agents total)
- Ball with physics + goal triggers
- BehaviorParameters:
  - Vector Observation Space: **250** (same as Stage 1)
  - Continuous Actions: **40**
  - Behavior Name: `WalkerSoccer` (must match Stage 1)

### Training Command

```powershell
mlagents-learn Assets/ML-Agents/CustomWalkingSoccerTwos/WalkerSoccerStage2_Soccer.yaml `
  --env="Builds/WalkerStage2_Soccer.exe" `
  --num-envs=3 --no-graphics `
  --run-id=WalkerStage2 `
  --initialize-from=WalkerStage1 --force
```

**Critical:** Use `--initialize-from=<Stage1_run_id>` to transfer weights.

### Stage 2 Config Highlights

```yaml
trainer_type: poca  # Multi-agent with self-play
learning_rate: 0.0001  # Lower LR for fine-tuning transferred weights
max_steps: 15000000  # 15M additional steps

self_play:
  save_steps: 50000
  team_change: 200000
  window: 5

environment_parameters:
  locomotion_only:
    value: 0.0  # Stage 2 mode (activates 7 soccer observations)
  
  ball_touch:  # Progressive curriculum
    curriculum:
      - name: Lesson 2 - Chase Ball
        threshold: 0.40  # 6M steps (40% of 15M)
        value: 0.35
      - name: Lesson 3 - Enable Kicking
        threshold: 0.80  # 12M steps (80% of 15M)
        value: 0.5  # Kick action unlocked
      - name: Lesson 4 - Full Soccer
        value: 1.0
```

### Expected Progress

| Steps | Lesson | Mean Reward | Behavior |
|-------|--------|-------------|----------|
| 0-2M | Lesson 2 | -5 → +5 | Adapting locomotion to ball context |
| 2M-6M | Lesson 2 | +5 → +20 | Chasing ball, learning spatial awareness |
| 6M-12M | Lesson 3 | +20 → +40 | **Kicking unlocked**, intentional strikes |
| 12M-15M | Lesson 4 | +40 → +60+ | Coordinated play, goal scoring |

**Key Success Indicators:**
- Locomotion skills retained from Stage 1
- Agents chase and intercept ball
- Intentional kicks occur in Lesson 3+
- Goals scored increase in Lesson 4
- Self-play ELO ratings diverge (competitive balance)

### Transfer Learning Notes

**What Transfers:**
- ✅ Policy network weights (locomotion skills)
- ✅ Standing/walking/turning abilities
- ✅ Balance and posture control

**What Resets:**
- ⚠️ Optimizer states (adam/critic start fresh - **this is normal**)
- ⚠️ Step counter (starts from 0 for Stage 2)
- ⚠️ Curriculum position (starts at Lesson 2)

**Expected Warnings (Safe to Ignore):**
```
[WARNING] Failed to load for module Optimizer:adam. Initializing
[WARNING] Failed to load for module Optimizer:critic. Initializing
```

These are **expected** when transferring PPO→POCA. Policy weights transfer successfully.

---

## Observation Space Architecture

Both stages use **250 observations** for perfect weight transfer:

**Base Locomotion (243 obs):**
- Velocity goals: 4 floats
- Rotation deltas: 8 floats
- Target position: 3 floats
- Body parts (16 parts): 228 floats
  - Ground contact, velocities, positions, rotations, strengths

**Soccer Context (7 obs):**
- Ball position relative to agent: 3 floats
- Ball velocity: 3 floats
- Team identifier: 1 float

**Stage 1 vs Stage 2:**
- Stage 1 (`locomotion_only=1.0`): Soccer observations **set to zero**
- Stage 2 (`locomotion_only=0.0`): Soccer observations **contain real data**

---

## Action Space (40 Continuous Actions)

**Actions 0-38: Joint Control**
- 26 joint rotations (chest, spine, limbs, head)
- 13 joint strengths (adaptive stiffness)

**Action 39: Kick Trigger**
- Value range [0, 1] indicates kick intensity
- Stage 1: Ignored (locomotion_only mode)
- Stage 2: Active in Lesson 3+ (`ball_touch >= 0.5`)
- Applies impulse to ball (6.0 force, 2.0m range, 25-step cooldown)

---

## Anti-Exploit Mechanisms

### Stage 1: Dive Prevention

**Problem:** Agents dive toward target to trigger respawn instead of walking/turning.

**Solutions Implemented:**
1. **Touch Validation:**
   - Upright check: `Vector3.Dot(hips.up, Vector3.up) >= 0.85`
   - Speed limit: `horizontalSpeed <= 2.0 m/s`
   - Downward velocity check: `verticalSpeed > -1.5 m/s`

2. **Graded Rewards:**
   - Valid touch: +0.2 reward
   - Invalid touch (dive/sprint): -0.15 to -1.35 penalty (scaled by severity)

3. **Turn Encouragement** (FixedUpdate):
   - Small reward for reducing angle to off-axis targets
   - Penalty for standing still when target >15° off-center
   - Prevents "forward-only" locomotion exploitation

4. **Randomized Spawns:**
   - Agent position: ±6m X/Z random offset
   - Agent rotation: Random 0-360°
   - Target position: Random within arena
   - Breaks exploitation patterns

### Stage 2: Consistent Positioning

- Fixed spawn positions using `initialPos`
- Team-based rotations (Blue: 90°, Purple: -90°)
- No randomization (soccer requires consistent team structure)

---

## Monitoring Training Progress

### TensorBoard

View real-time metrics:

```bash
# Start TensorBoard
tensorboard --logdir results

# Open browser to http://localhost:6006
```

**Key Metrics:**

**Stage 1 (PPO):**
- `Environment/Cumulative Reward`: Target +20-35
- `Policy/Learning Rate`: Should stay 0.0003
- `Losses/Policy Loss`: Should decrease and stabilize

**Stage 2 (POCA):**
- `Environment/Cumulative Reward`: Target +40-60+
- `Self-Play/ELO`: Team ratings (higher = better)
- `Environment/Group Cumulative Reward`: Team performance
- `Policy/Learning Rate`: Should stay 0.0001

### Console Output

```
[INFO] WalkerSoccer. Step: 50000. Mean Reward: 12.345. Training. ELO: 1205.3
```

Watch for:
- ✅ Steadily increasing Mean Reward
- ✅ Valid target touches (Stage 1 logs)
- ✅ Goal events (Stage 2 logs)
- ❌ NaN values (training instability)
- ❌ Reward stuck negative

---

## Training Tips

### Performance Optimization

1. **Use multiple environments**
   - Duplicate your arena 4-8 times in one scene
   - Parallel training speeds up data collection

2. **Increase Time Scale (Editor only)**
   ```csharp
   Time.timeScale = 20f; // In Unity
   ```
   - Training runs 20x faster
   - May cause physics instability above 20x

3. **Use Build instead of Editor**
   - Builds are 2-3x faster than training in Editor
   - Can run headless: `--no-graphics`

4. **GPU Training**
   - If you have NVIDIA GPU, install CUDA version of PyTorch
   - Significant speedup for large neural networks

5. **Multi-GPU Training** (2+ GPUs)
   - Run separate training sessions on each GPU:
   ```powershell
   # Terminal 1 (GPU 0)
   $env:CUDA_VISIBLE_DEVICES="0"
   mlagents-learn WalkerSoccer.yaml --run-id=WalkerSoccer_GPU0 --num-envs=12
   
   # Terminal 2 (GPU 1)
   $env:CUDA_VISIBLE_DEVICES="1"
   mlagents-learn WalkerSoccer.yaml --run-id=WalkerSoccer_GPU1 --num-envs=6
   ```
   - Each trains independently; compare results afterward
   - Scale `--num-envs` based on GPU VRAM (12GB = 12-16 envs, 6GB = 4-8 envs)

### Common Training Issues

#### Issue: Agents not learning to walk
**Solutions:**
- Start with lower `max_joint_force_limit` (300-400)
- Increase `time_horizon` to 1500
- Ensure `randomizeWalkSpeedEachEpisode` is enabled
- Check body part assignments in Unity

#### Issue: Agents fall over constantly
**Solutions:**
- Enable start stabilization in `WalkerSoccerSettings`:
  - `enableStartStabilization = true`
  - `stabilizeStepsOnReset = 15-20`
  - `standStrength = 0.95-1.0`
- Increase solver iterations (12-15)
- Reduce `max_joint_force_limit` if still unstable
- Increase decision period (5-10 steps)
- Check ConfigurableJoint drive settings
- Ensure feet have ground contact sensors

#### Issue: Agents ignore the ball
**Solutions:**
- Wait for curriculum progression - early lessons intentionally delay ball influence
- Verify ball reference is assigned in WalkerSoccerAgent
- Check ball collision layer settings
- Ensure `delayBallInfluenceSteps` (50) has passed in episode
- Current curriculum uses dual parameters:
  - `ball_touch`: scales ball collision reward (0.0 → 1.0)
  - `ball_spawn_radius`: ball distance from center (12m → 3m)

#### Issue: Training is unstable (NaN errors)
**Solutions:**
- Reduce `learning_rate` to 0.0001
- Decrease `max_joint_force_limit`
- Enable `normalize: true` in network_settings
- Check for physics explosions in Unity

#### Issue: One team always wins
**Solutions:**
- Self-play should balance this naturally
- Verify team assignments are correct
- Check reward structure is symmetric
- Ensure both teams have equal capabilities

---

## Advanced Training Options

### Imitation Learning (Optional)

If you have expert demonstrations:

1. **Record Demonstrations**
   ```bash
   # In Unity, use Demonstration Recorder component
   ```

2. **Enable in config**
   ```yaml
   behavioral_cloning:
     demo_path: demonstrations/WalkerSoccer.demo
     steps: 50000
     strength: 0.5
   ```

3. **Train with BC**
   - Agents bootstrap from demonstrations
   - Speeds up initial learning

### Distributed Training

For faster training across multiple machines:

```bash
# Machine 1 (trainer)
mlagents-learn config.yaml --run-id=dist_run --num-envs=4

# Machine 2+ (additional environments)
mlagents-learn config.yaml --run-id=dist_run --num-envs=4 --trainer-config-path=<IP>:5005
```

### Custom Reward Tuning

Modify rewards in `WalkerSoccerAgent.cs`:

```csharp
// Increase importance of ball positioning
var distanceToBall = Vector3.Distance(hips.position, ball.position);
AddReward(-0.001f * distanceToBall);

// Reward facing opponent's goal
var goalDirection = (opponentGoal.position - hips.position).normalized;
var facingReward = Vector3.Dot(head.forward, goalDirection);
AddReward(0.1f * facingReward);
```

---

## Training Benchmarks

Expected performance timeline (V10 progressive skill curriculum):

| Steps | Lesson | Expected Behavior | Mean Reward | Kick? |
|-------|--------|-------------------|-------------|-------|
| 0-2M | Lesson0: StandAndBalance | Learning upright posture, balance | +15 → +25 | ❌ |
| 2M-6M | Lesson1: WalkTowardsBall | Stable walking toward ball (2.7m radius) | +20 → +30 | ❌ |
| 6M-12M | Lesson2: ChaseBall | Active pursuit (2.3m radius), ball_touch=0.35 | +25 → +40 | ❌ |
| 12M-20M | Lesson3: KickingEnabled | **Kick action unlocked!** (1.8m radius, ball_touch=0.5) | +30 → +60 | ✅ |
| 20M+ | Lesson4: GoalScoring | Full rewards (1.4m radius), competitive scoring | +50+ | ✅ |

**Curriculum Thresholds:**
- Lesson progression based on training progress (% of max_steps)
- Min lesson length: 50-1000 episodes to ensure stability
- Kick action gated by `ball_touch >= 0.5` (Lesson 3+)
- Goal reward: 50× time bonus (increased from 10× in V9)

**Hardware:**
- **CPU Training**: ~500-1000 steps/sec (4-8 environments)
- **GPU Training**: ~2000-5000 steps/sec (8-16 environments)
- **Total Training Time**: 8-48 hours depending on hardware

---

## Evaluating Trained Models

### Testing in Unity

1. **Load trained model**:
   - Copy `.onnx` file from `results/WalkerSoccer_v1/` to Unity
   - Drag onto agent's Behavior Parameters > Model

2. **Set Behavior Type**: `Inference Only`

3. **Press Play** - watch trained agents play!

### Model Comparison

```bash
# Compare two models
mlagents-learn config.yaml --run-id=comparison --resume --initialize-from=results/WalkerSoccer_v1
```

---

## Next Steps After Training

1. ✅ **Test trained models** in inference mode
2. ✅ **Fine-tune hyperparameters** based on results
3. ✅ **Add complexity**: obstacles, power-ups, larger teams
4. ✅ **Experiment with rewards**: shape emergent behaviors
5. ✅ **Share your results**: export and share trained models

---

## Useful Commands Reference

```bash
# Start fresh training
mlagents-learn config.yaml --run-id=run_name --force

# Resume interrupted training
mlagents-learn config.yaml --run-id=run_name --resume

# Train headless (no graphics)
mlagents-learn config.yaml --run-id=run_name --no-graphics

# Train with specific number of environments
mlagents-learn config.yaml --run-id=run_name --num-envs=8

# Initialize from previous model
mlagents-learn config.yaml --run-id=new_run --initialize-from=old_run

# View help
mlagents-learn --help
```

---

## Troubleshooting

### Python Environment Issues

```bash
# Create fresh conda environment
conda create -n mlagents python=3.9
conda activate mlagents
pip install mlagents torch

# Verify installation
mlagents-learn --help
```

### Unity Connection Issues

- Ensure **Unity > Player Settings > Resolution and Presentation > Run In Background** is enabled
- Check firewall isn't blocking Unity/Python communication
- Try running as administrator

### Out of Memory

- Reduce `num_envs` or `batch_size`
- Use smaller `hidden_units` (256 instead of 512)
- Close other applications
- Use build instead of Editor

---

## Additional Resources

- [ML-Agents GitHub](https://github.com/Unity-Technologies/ml-agents)
- [Training Configuration File](https://github.com/Unity-Technologies/ml-agents/blob/main/docs/Training-Configuration-File.md)
- [MA-POCA Documentation](https://github.com/Unity-Technologies/ml-agents/blob/main/docs/Training-ML-Agents.md#multi-agent-poca)
- [TensorBoard Guide](https://github.com/Unity-Technologies/ml-agents/blob/main/docs/Using-Tensorboard.md)
