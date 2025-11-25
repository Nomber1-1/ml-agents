# Walker Soccer Twos - ML-Agents Project

A Unity ML-Agents project featuring humanoid ragdoll walkers playing competitive 3v3 soccer. Combines complex locomotion control with team-based gameplay.

![Walker Soccer Banner](https://via.placeholder.com/800x200.png?text=Walker+Soccer+Twos)

## 🎮 Overview

Walker Soccer Twos is an advanced ML-Agents environment where humanoid walker agents with full ragdoll physics learn to:
- **Walk and balance** with 16-joint articulated bodies
- **Play soccer** in competitive 3v3 matches
- **Cooperate** with teammates (strikers and goalies)
- **Compete** against opposing teams using self-play

## 🚀 Quick Start

### Prerequisites
- Unity 2022.3+ with ML-Agents package
- Python 3.8-3.10
- ML-Agents Python package (`pip install mlagents`)

### Setup in 3 Steps

1. **Scene Setup** - Follow `UNITY_SETUP_GUIDE.md` to build your Unity scene
2. **Train Agents** - Follow `TRAINING_GUIDE.md` to start training
3. **Debug & Improve** - Use `DEBUG_AND_IMPROVEMENTS.md` for troubleshooting

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
