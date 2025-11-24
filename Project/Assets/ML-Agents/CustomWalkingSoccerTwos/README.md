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
- **Curriculum learning** progressing from walking to advanced soccer
- **Parameter randomization** for robust generalization

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

### Action Space (39 continuous)
- **Joint rotations**: chest (3), spine (3), head (2), thighs (4), shins (2), feet (6), arms (4), forearms (2)
- **Joint strengths**: 13 configurable strength values

### Reward Structure
- **Goals**: +1.0 (team reward), -1.0 (opponent penalty)
- **Ball touches**: +0.2 (scaled by curriculum)
- **Locomotion**: Speed matching + direction alignment (0.5x weight)
- **Existential**: +/- based on role (goalies/strikers)

## 📊 Training Results

Expected progression:
- **100k steps**: Basic standing and balance
- **500k steps**: Consistent walking, occasional ball contact
- **1M steps**: Chasing ball, intentional kicking
- **3M steps**: Coordinated team play, basic strategy
- **5M+ steps**: Advanced tactics, passing, defending

Training time: 8-48 hours depending on hardware and parallel environments.

## 🛠️ Configuration

### Key Hyperparameters
```yaml
learning_rate: 0.0003
batch_size: 2048
hidden_units: 512
num_layers: 3
gamma: 0.995
max_steps: 10M
```

### Curriculum Stages
1. **Just Walking** - Learn locomotion (ball_touch: 0.0)
2. **Chase and Touch** - Interact with ball (ball_touch: 0.3)
3. **Play Soccer** - Basic soccer skills (ball_touch: 0.5)
4. **Competitive Soccer** - Full gameplay (ball_touch: 1.0)

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
