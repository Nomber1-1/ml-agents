# Walker Soccer Training Guide

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
4. **Unity scene built** following `UNITY_SETUP_GUIDE.md`

---

## Quick Start Training

### 1. Build Your Unity Scene

In Unity Editor:
1. Open **File > Build Settings**
2. Add your Walker Soccer scene
3. Ensure **Target Platform** matches your OS
4. Click **Build** (save as `WalkerSoccer.exe` or similar)
5. Place the build in a known location

### 2. Start Training

Open terminal/command prompt and navigate to your project directory:

```bash
# Basic training command
mlagents-learn Assets/ML-Agents/CustomWalkingSoccerTwos/WalkerSoccer.yaml --run-id=WalkerSoccer_v1

# With TensorBoard monitoring
mlagents-learn Assets/ML-Agents/CustomWalkingSoccerTwos/WalkerSoccer.yaml --run-id=WalkerSoccer_v1 --tensorboard

# Resume from checkpoint
mlagents-learn Assets/ML-Agents/CustomWalkingSoccerTwos/WalkerSoccer.yaml --run-id=WalkerSoccer_v1 --resume

# Train with specific executable
mlagents-learn Assets/ML-Agents/CustomWalkingSoccerTwos/WalkerSoccer.yaml --run-id=WalkerSoccer_v1 --env=Builds/WalkerSoccer.exe

# Force overwrite existing run
mlagents-learn Assets/ML-Agents/CustomWalkingSoccerTwos/WalkerSoccer.yaml --run-id=WalkerSoccer_v1 --force
```

### 3. When Prompted

After running the command, you'll see:
```
Start training by pressing the Play button in the Unity Editor
```

- If training in **Editor**: Press Play in Unity
- If training with **Build**: The executable will start automatically

---

## Training Parameters Explained

### Hyperparameters (WalkerSoccer.yaml)

| Parameter | Value | Why? |
|-----------|-------|------|
| `learning_rate` | 0.0003 | Moderate learning rate for stable training |
| `batch_size` | 2048 | Large batches for stable gradients with complex agents |
| `buffer_size` | 20480 | 10x batch size, standard ratio |
| `hidden_units` | 512 | Large network for complex walker + soccer control |
| `num_layers` | 3 | Deep enough for hierarchical behaviors |
| `gamma` | 0.99 | Discount factor for long-term goal planning |
| `max_steps` | 30M | Sufficient for learning complex behaviors |

### Why MA-POCA?

We use **Multi-Agent POsthumous Credit Assignment (MA-POCA)** because:
- ✅ Handles variable team sizes
- ✅ Learns cooperative behaviors (passing, positioning)
- ✅ Assigns credit appropriately in team settings
- ✅ Includes self-play for competitive learning

### Start Pose Stabilization

Agents use an initial stabilization window to prevent early collapse:

1. **Stabilization Period** (configurable in `WalkerSoccerSettings`)
   - First 10-20 physics steps hold a stable standing pose
   - High joint strengths (0.9-1.0) enforce the pose
   - Prevents ragdoll collapse during episode initialization
   
2. **Solver Iterations** (configurable)
   - Increased to 12 for better joint stability
   - Helps maintain balance during rapid movements
   
3. **Configuration Options**:
   - `enableStartStabilization`: Enable/disable pose hold
   - `stabilizeStepsOnReset`: Number of steps to hold pose (10-30)
   - `standStrength`: Joint strength during stabilization (0.9-1.0)
   - `solverIterations`: Physics solver iterations (10-15)
   - `solverVelocityIterations`: Velocity solver iterations (10-15)

---

## Monitoring Training Progress

### TensorBoard

View real-time training metrics:

```bash
# Start TensorBoard (in a separate terminal)
tensorboard --logdir results

# Open browser to
http://localhost:6006
```

**Key Metrics to Watch:**

- **Environment/Cumulative Reward**: Should increase over time
  - Target: 5-15+ after millions of steps
  
- **Environment/Episode Length**: Varies, but should stabilize
  - Longer episodes = agents lasting longer without goals
  
- **Losses/Policy Loss**: Should decrease and stabilize
  - If increasing steadily, reduce learning rate
  
- **Policy/Learning Rate**: Should stay constant (or decay if scheduled)

- **Self-Play/ELO**: Team skill ratings
  - Higher ELO = better performance

### Console Output

Monitor training in real-time:
```
[INFO] WalkerSoccer. Step: 50000. Time Elapsed: 312.5 s. Mean Reward: 2.345. Std of Reward: 1.234.
```

**What to look for:**
- ✅ Mean Reward gradually increasing
- ✅ Steps progressing steadily
- ❌ NaN values (indicates training instability)
- ❌ Reward stuck at negative values

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
- Increase `ball_touch` reward in curriculum
- Verify ball reference is assigned in WalkerSoccerAgent
- Check ball collision layer settings
- Reduce locomotion reward weight (currently 0.5x)

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

Expected performance timeline:

| Steps | Expected Behavior |
|-------|------------------|
| 0-100k | Agents learning to stand/balance |
| 100k-500k | Basic walking, occasional ball touches |
| 500k-1M | Consistent walking, chasing ball |
| 1M-3M | Kicking ball toward goals, basic strategy |
| 3M-5M | Coordinated team play, passing attempts |
| 5M-10M | Advanced strategy, defending, goal scoring |

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
