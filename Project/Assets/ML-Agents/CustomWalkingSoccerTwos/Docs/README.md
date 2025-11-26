# Walker Soccer Twos - Two-Stage Transfer Learning Project

A Unity ML-Agents project featuring humanoid ragdoll walkers that learn locomotion first, then apply those skills to competitive 3v3 soccer through transfer learning.

![Walker Soccer Banner](https://via.placeholder.com/800x200.png?text=Walker+Soccer+Twos)

## 🎮 Overview

Walker Soccer Twos uses a **two-stage training architecture** where humanoid walker agents with full ragdoll physics progressively learn to:
1. **Stage 1 (PPO)**: Master bipedal locomotion with 360° turning capability
2. **Stage 2 (POCA)**: Transfer locomotion skills to competitive 3v3 soccer gameplay
3. **Cooperate** with teammates (strikers and goalies)
4. **Compete** against opposing teams using self-play

This approach dramatically improves training efficiency by separating locomotion learning from soccer strategy.

## 🚀 Quick Start

### Prerequisites
- Unity 2022.3+ with ML-Agents package
- Python 3.8-3.10
- ML-Agents Python package (`pip install mlagents`)

### Two-Stage Training Workflow

1. **Stage 1 Setup** - Build locomotion training scene (10-20 arenas, single agents, moving targets)
2. **Stage 1 Training** - Train 10-20M steps until agents walk confidently in all directions
3. **Stage 2 Setup** - Build 3v3 soccer scene (6 agents, ball, goals)
4. **Stage 2 Training** - Transfer Stage 1 weights and train 15M additional steps
5. **Evaluation** - Watch trained agents play soccer!

See `TRAINING_GUIDE.md` for complete step-by-step instructions.

## 📁 Project Structure

```
CustomWalkingSoccerTwos/
├── Scripts/
│   ├── WalkerSoccerAgent.cs          # Unified agent (both stages)
│   ├── WalkerSoccerEnvController.cs  # Environment management (Stage 2)
│   ├── WalkerSoccerBallController.cs # Ball physics and scoring (Stage 2)
│   ├── TargetController.cs           # Moving target (Stage 1)
│   └── SoccerSettings.cs             # Configuration settings
├── Configs/
│   ├── WalkerSoccerStage1_Locomotion.yaml  # Stage 1 PPO config
│   └── WalkerSoccerStage2_Soccer.yaml      # Stage 2 POCA config
├── Docs/
│   ├── TRAINING_GUIDE.md             # Complete two-stage training guide
│   ├── QUICK_REFERENCE.md            # Commands and checklists
│   ├── SYSTEM_OVERVIEW.md            # Architecture documentation
│   ├── UNITY_SETUP_GUIDE.md          # Scene setup instructions
│   └── DEBUG_AND_IMPROVEMENTS.md     # Troubleshooting
└── README.md                         # This file
```

## 🎯 Key Features

### Two-Stage Transfer Learning
- **Stage 1 (PPO)**: Single-agent locomotion training (10-20M steps)
  - Random spawns (±6m, 0-360° rotation) to prevent exploitation
  - Turn encouragement to learn full 360° locomotion
  - Touch validation to prevent dive-to-respawn exploits
  
- **Stage 2 (POCA)**: Multi-agent soccer with transferred weights (15M steps)
  - `--initialize-from` transfers Policy weights from Stage 1
  - Locomotion skills retained, agents learn soccer strategy
  - Curriculum: Chase → Kicking → Goal Scoring
  - Self-play for competitive skill development

### Unified Agent Architecture
- **Single agent script** with conditional behavior (`locomotion_only` flag)
- **250 observations** consistent across both stages:
  - 243 locomotion observations (always active)
  - 7 soccer observations (zeros in Stage 1, real data in Stage 2)
- **40 continuous actions**: 39 joints + 1 kick (gated by curriculum)

### Anti-Exploit Mechanisms
- **Touch validation**: Rewards proper walking, penalizes diving (-0.15 to -1.35)
- **Turn encouragement**: Rewards angle reduction, penalizes standing still when target off-center
- **Random spawns (Stage 1)**: Prevents forward-only policy exploitation
- **Fixed spawns (Stage 2)**: Maintains team positioning for soccer

## 🧠 Agent Design

### Observation Space (250 dimensions)
**Locomotion Observations (243, both stages):**
- Velocity goals (4): Current vs target speeds
- Rotations (8): Body orientation deltas
- Target position (3): Direction to goal
- Body parts (228): 16 parts × ~14 obs each (positions, velocities, rotations, contacts)

**Soccer Context Observations (7, conditional):**
- Ball position relative to agent (3)
- Ball velocity (3)
- Team identifier (1: +1 Blue, -1 Purple)
- **Stage 1**: All zeros (Vector3.zero + 0f)
- **Stage 2**: Real soccer data

### Action Space (40 continuous)
- **Joint rotations (26)**: chest (3), spine (3), head (2), limbs (18)
- **Joint strengths (13)**: torso (3), legs (6), arms (4)
- **Kick action (1)**: [0,1] intensity
  - Stage 1: Ignored (locomotion only)
  - Stage 2: Active in Lesson 3+ (ball_touch >= 0.5)

### Reward Structure

**Stage 1 (Locomotion Focus):**
- Valid touch: +0.2 (upright, reasonable speed, not diving)
- Invalid touch: -0.15 to -1.35 (scaled by violation severity)
- Turn encouragement: +0.05 × turnProgress (when target >15° off)
- Standing penalty: -0.02 × angleToTarget/180 (when speed < 0.5 m/s)
- Stability penalties: Forward tip, sideways lean, angular velocity

**Stage 2 (Soccer Focus):**
- Goals: +50 × time_bonus (scoring), -10 (conceding)
- Ball touches: +0.2 × curriculum (delayed 50 steps)
- Kick reward: +0.1 per intentional kick (Lesson 3+)
- Locomotion rewards: Retained from Stage 1
- Upright bonus: +0.03 per step (height-scaled)
- Existential rewards: ±0.25 by role (goalie/striker)

## 📊 Training Results

### Stage 1 Expected Progress (PPO, 10-20M steps)
| Steps | Mean Reward | Behavior |
|-------|-------------|----------|
| 0-2M | -10 → +5 | Learning to stand |
| 2M-5M | +5 → +15 | Stable forward walking |
| 5M-10M | +15 → +25 | Beginning to turn |
| 10M-20M | +25 → +35 | Full 360° locomotion |

**Goal**: Agents confidently turn toward targets >30° off-center

### Stage 2 Expected Progress (POCA, 15M steps)
| Steps | Lesson | Mean Reward | Kick? | Ball Radius |
|-------|--------|-------------|-------|-------------|
| 0-2M | L2: Chase | -5 → +5 | ❌ | 2.3m |
| 2M-6M | L2: Chase | +5 → +20 | ❌ | 2.3m |
| 6M-12M | L3: Kicking | +20 → +40 | ✅ | 1.8m |
| 12M-15M | L4: Full Soccer | +40 → +60+ | ✅ | 1.4m |

**Goal**: Goals scored, coordinated team play, ELO ratings stable

Training time: 
- Stage 1: 6-12 hours (10-20M steps)
- Stage 2: 8-15 hours (15M steps)
- Total: 14-27 hours depending on hardware

## 🛠️ Configuration

### Stage 1: WalkerSoccerStage1_Locomotion.yaml
```yaml
trainer_type: ppo
learning_rate: 0.0003
batch_size: 2048
hidden_units: 512
num_layers: 3
max_steps: 10000000  # 10M minimum, 20M recommended

environment_parameters:
  locomotion_only: 1.0  # Stage 1 mode
  target_walking_speed:
    min_value: 0.8
    max_value: 4.0
```

### Stage 2: WalkerSoccerStage2_Soccer.yaml
```yaml
trainer_type: poca  # Multi-agent
learning_rate: 0.0001  # Lower for fine-tuning
max_steps: 15000000

self_play:
  save_steps: 50000
  team_change: 200000
  window: 5

environment_parameters:
  locomotion_only: 0.0  # Stage 2 mode
  ball_touch:  # Curriculum
    - 0.35 (Lesson 2, 0-6M)
    - 0.5 (Lesson 3, 6M-12M, kick enabled)
    - 1.0 (Lesson 4, 12M-15M)
```

## 📖 Documentation

### Essential Guides (In Order)
1. **[Training Guide](TRAINING_GUIDE.md)** - Complete two-stage training workflow
2. **[Quick Reference](QUICK_REFERENCE.md)** - Commands, checklists, troubleshooting
3. **[System Overview](SYSTEM_OVERVIEW.md)** - Architecture and reward details
4. **[Unity Setup Guide](UNITY_SETUP_GUIDE.md)** - Scene construction
5. **[Debug & Improvements](DEBUG_AND_IMPROVEMENTS.md)** - Advanced troubleshooting

## 🔧 Common Issues

### Stage 1: Agents dive toward target instead of walking
→ Touch validation active, harsh dive penalties implemented

### Stage 1: Agents won't turn (forward-only policy at 16M steps)
→ Turn encouragement added in FixedUpdate, rebuild and continue training

### Stage 2: Transfer learning fails (Policy not loading)
→ Verify observation space is **250 in both Stage 1 and Stage 2 builds**

### Stage 2: Agents forgot how to walk
→ Check observation space mismatch, Policy should transfer without warnings

See `TRAINING_GUIDE.md` and `QUICK_REFERENCE.md` for detailed solutions.

## 🎓 Learning Resources

- [ML-Agents Documentation](https://github.com/Unity-Technologies/ml-agents)
- [Transfer Learning in ML-Agents](https://github.com/Unity-Technologies/ml-agents/blob/main/docs/Training-ML-Agents.md#training-using-concurrent-unity-instances)
- [Walker Example](https://github.com/Unity-Technologies/ml-agents/blob/main/docs/Learning-Environment-Examples.md#walker)
- [Soccer Twos Example](https://github.com/Unity-Technologies/ml-agents/blob/main/docs/Learning-Environment-Examples.md#soccer-twos)
- [MA-POCA Paper](https://arxiv.org/abs/2111.05992)

## 🚀 Advanced Features

### Current Implementation
- ✅ Two-stage transfer learning (PPO → POCA)
- ✅ 250-observation zero-padding for perfect weight transfer
- ✅ Turn encouragement to prevent forward-only policies
- ✅ Touch validation to prevent dive exploits
- ✅ Conditional spawning (random Stage 1, fixed Stage 2)
- ✅ Curriculum learning (chase → kick → score)

### Planned Enhancements
- [ ] LSTM memory for temporal reasoning
- [ ] Communication between agents
- [ ] Advanced reward shaping (passing, positioning)
- [ ] Dynamic difficulty scaling
- [ ] Visual observations (camera input)

### Customization Ideas
- Train Stage 1 with obstacles or varied terrain
- Experiment with Stage 1 target speeds (currently 0.8-4.0 m/s)
- Add Stage 3: Advanced tactics training
- Different field sizes/shapes in Stage 2
- Variable team sizes (4v4, 5v5)

## 📝 Training Commands

### Stage 1: Locomotion
```bash
# Start Stage 1 training
mlagents-learn WalkerSoccerStage1_Locomotion.yaml --run-id=WalkerStage1 \
  --env=Builds/WalkerStage1.exe --num-envs=4 --no-graphics

# Monitor progress
tensorboard --logdir results
```

### Stage 2: Soccer (Transfer)
```bash
# Start Stage 2 with transferred weights
mlagents-learn WalkerSoccerStage2_Soccer.yaml --run-id=WalkerStage2 \
  --env=Builds/WalkerStage2.exe --num-envs=3 --no-graphics \
  --initialize-from=WalkerStage1

# Monitor progress (locomotion should be retained!)
tensorboard --logdir results
```

## 🤝 Contributing

Improvements welcome! Consider:
- Optimizing Stage 1 turn encouragement parameters
- Improving Stage 2 curriculum thresholds
- Adding intermediate transfer checkpoints
- Creating better visualization tools

## 📄 License

This project uses Unity ML-Agents (Apache 2.0 License).
Your custom code and configurations can be licensed as you prefer.

## 🙏 Acknowledgments

- Unity ML-Agents team for the excellent framework and transfer learning support
- Walker and Soccer Twos example environments for inspiration
- Community contributors for best practices and debugging tips

---

**Ready to train?** 

1. Read `TRAINING_GUIDE.md` for complete setup
2. Build Stage 1 scene with locomotion arenas
3. Train Stage 1 to 10-20M steps
4. Build Stage 2 scene with 3v3 soccer
5. Transfer weights and train Stage 2 to completion
6. Watch your agents play soccer! ⚽🤖

For questions or issues, consult `QUICK_REFERENCE.md` or open an issue.

## 📁 Project Structure

```
CustomWalkingSoccerTwos/
├── Scripts/
│   ├── WalkerSoccerAgent.cs          # Main agent (locomotion + soccer)
│   ├── WalkerSoccerEnvController.cs  # Environment management
│   ├── WalkerSoccerBallController.cs # Ball physics and scoring
│   └── SoccerSettings.cs             # Configuration settings
├── WalkerSoccer.yaml                 # ML-Agents training config
├── UNITY_SETUP_GUIDE.md              # Complete scene setup instructions
├── TRAINING_GUIDE.md                 # Training walkthrough & tips
├── DEBUG_AND_IMPROVEMENTS.md         # Troubleshooting & optimization
└── README.md                         # This file
```

## 🎯 Key Features

### Unified Agent Architecture
- **Single agent script** combining walker locomotion with soccer gameplay
- **39 continuous actions** controlling joint rotations and strengths
- **Rich observations** including body positions, velocities, ball state, and team info
- **Position-based roles**: Strikers, Goalies, and Generic players

### Advanced Training
- **MA-POCA** (Multi-Agent POsthumous Credit Assignment) for team learning
- **Self-play** for competitive skill development
- **Start pose stabilization** to prevent early episode collapse
- **Multi-GPU support** for faster parallel training

### Soccer Gameplay
- **Team-based rewards** for goals, ball touches, and positioning
- **Physics-based kicking** using collision forces
- **Dynamic targeting** - agents prioritize ball over static targets
- **Goalie/Striker behaviors** with role-specific rewards

## 🧠 Agent Design

### Observation Space (~150-200 dimensions)
- Walker locomotion: body part positions, velocities, rotations, ground contact
- Soccer context: ball position/velocity, team ID, role, goal locations
- Orientation cube: stabilized reference frame for observations

### Action Space (40 continuous)
- **Joint rotations**: chest (3), spine (3), head (2), thighs (4), shins (2), feet (6), arms (4), forearms (2)
- **Joint strengths**: 13 configurable strength values
- **Kick action**: 1 continuous trigger (enabled only in Lesson 3+)

### Reward Structure
- **Goals**: +50 × time_bonus (scoring team), -10 (conceding team) — massively increased in V10!
- **Ball touches**: +0.2 (scaled by curriculum, delayed first 50 steps)
- **Kick reward**: +0.1 per intentional kick (Lesson 3+ only)
- **Locomotion**: Speed matching + direction alignment (dynamic 1.0x-2.0x weight with speed ramp)
- **Upright bonus**: +0.03 per step with height scaling (0.85-1.3m optimal)
- **Anti-forward-tip penalty**: -0.02 when tilting forward (upright dot < 0.7)
- **Sideways lean penalty**: -0.02 scaled by lateral tilt
- **Angular velocity penalty**: -0.002 per rad/s (damps flailing)
- **Collapse penalty**: -0.01 when hips < 0.3m
- **Existential**: ±0.25 based on role (reduced, delayed first 50 steps)

## 📊 Training Results

Expected progression (V10 progressive skill curriculum):
- **0-2M steps** (Lesson 0): Standing and balance — no ball influence, kick disabled
- **2M-6M steps** (Lesson 1): Walking toward ball — minimal ball rewards (0.15), kick disabled
- **6M-12M steps** (Lesson 2): Chasing ball actively — moderate rewards (0.35), kick disabled
- **12M-20M steps** (Lesson 3): **Kicking enabled!** — ball_touch 0.5, intentional kick mechanic unlocked
- **20M+ steps** (Lesson 4): Goal-scoring & strategy — full rewards (1.0), competitive play

Training time: 12-72 hours depending on hardware and parallel environments.

## 🛠️ Configuration

### Key Hyperparameters
```yaml
learning_rate: 0.0003
batch_size: 2048
hidden_units: 512
num_layers: 3
gamma: 0.99
max_steps: 30M
```

### Stabilization Features
- **Start pose hold**: Agents hold stable stance for first 50 steps (increased from 10)
- **Progressive speed ramp**: Speed increases from 0.5 → 3.0 m/s over 400 steps
- **Neutral orientation**: Agents start facing forward (±10°) instead of toward ball
- **Increased solver iterations**: Better joint constraint solving (12 iterations)
- **Configurable via WalkerSoccerSettings**: Toggle and tune all stabilization parameters

## 📖 Documentation

### Essential Guides
- **[Unity Setup Guide](UNITY_SETUP_GUIDE.md)** - Complete scene construction walkthrough
- **[Training Guide](TRAINING_GUIDE.md)** - Training commands, monitoring, and tips
- **[Debug & Improvements](DEBUG_AND_IMPROVEMENTS.md)** - Troubleshooting and optimization

### Quick References
- ML-Agents config: `WalkerSoccer.yaml`
- Main agent: `WalkerSoccerAgent.cs`
- Environment controller: `WalkerSoccerEnvController.cs`

## 🔧 Common Issues

### Agents fall through floor?
→ Check collision layers and Rigidbody settings

### Training not improving?
→ Enable observation normalization, verify rewards with Heuristic mode

### Physics too unstable?
→ Reduce `max_joint_force_limit`, increase solver iterations

See `DEBUG_AND_IMPROVEMENTS.md` for detailed solutions.

## 🎓 Learning Resources

- [ML-Agents Documentation](https://github.com/Unity-Technologies/ml-agents)
- [Walker Example](https://github.com/Unity-Technologies/ml-agents/blob/main/docs/Learning-Environment-Examples.md#walker)
- [Soccer Twos Example](https://github.com/Unity-Technologies/ml-agents/blob/main/docs/Learning-Environment-Examples.md#soccer-twos)
- [MA-POCA Paper](https://arxiv.org/abs/2111.05992)

## 🚀 Advanced Features

### Planned Enhancements
- [ ] LSTM memory for temporal reasoning
- [ ] Communication between agents
- [ ] Advanced reward shaping (passing, positioning)
- [ ] Dynamic difficulty scaling
- [ ] Visual observations (camera input)

### Customization Ideas
- Add obstacles or power-ups
- Increase team size (4v4, 5v5)
- Multiple ball variants
- Different field sizes/shapes

## 📝 Code Examples

### Testing in Heuristic Mode
```csharp
// In WalkerSoccerAgent.cs, Heuristic() is already implemented
// Set Behavior Type to "Heuristic Only" and use WASD to control
```

### Training Command
```bash
mlagents-learn WalkerSoccer.yaml --run-id=MySoccerRun_v1
```

### TensorBoard Monitoring
```bash
tensorboard --logdir results
```

## 🤝 Contributing

Improvements welcome! Consider:
- Optimizing reward structures
- Adding new agent behaviors
- Improving training stability
- Creating better visualizations

## 📄 License

This project uses Unity ML-Agents (Apache 2.0 License).
Your custom code and configurations can be licensed as you prefer.

## 🙏 Acknowledgments

- Unity ML-Agents team for the excellent framework
- Walker and Soccer Twos example environments for inspiration
- Community contributors for best practices and debugging tips

---

**Ready to train?** Start with `UNITY_SETUP_GUIDE.md` → `TRAINING_GUIDE.md` → Train amazing soccer agents! ⚽🤖

For questions or issues, consult `DEBUG_AND_IMPROVEMENTS.md` or open an issue.
