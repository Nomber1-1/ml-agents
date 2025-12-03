# Walker Soccer Twos - Final Project Overview (269 Observation Version)

Comprehensive documentation of the final architecture, training strategy, experimentation history, emergent behaviors, and future improvement directions for our project.

---
## 1. Agent Design

### 1.1 Ragdoll Humanoid Structure
- 16 body parts (hips, spine, chest, head, thighs, shins, feet, upper arms, forearms, hands)
- ConfigurableJoint + JointDriveController for torque-driven locomotion
- Orientation Cube provides a stable reference frame (removes global drift, improves learning consistency)

### 1.2 Observation Space (269 Floats)
Breakdown:
- Locomotion Core (243):
  - Body part positions (relative): 16 × 3
  - Body part linear velocities: 16 × 3
  - Body part angular velocities: 16 × 3
  - Body part local rotations (quaternions, subset excluding hips/hands): 13 × 4
  - Joint strength ratios: 13 × 1
  - Velocity goal vs current speed distance: 1
  - Average velocity (relative): 3
  - Velocity goal (relative): 3
  - Rotation delta hips→forward: 4 (quaternion)
  - Rotation delta head→forward: 4 (quaternion)
  - Target position (Stage 1 only / zero placeholder Stage 2): 3
- Soccer Context (26):
  - Ball position (3), Ball velocity (3)
  - Team ID (1): Blue = +1, Purple = -1
  - Role (1): Striker = +1, Goalie = -1, Generic = 0
  - Own goal position (3), Opponent goal position (3)
  - Teammate 1 position (3), Teammate 2 position (3)
  - Opponent 1 position (3), Opponent 2 position (3)
  - Stage 1: All 26 zeros (transfer consistency)
  - Stage 2: Real data for role/goal/team spatial reasoning

Rationale:
- Zero-padding in Stage 1 allows identical network input shape for seamless weight transfer
- Goals + role enable distinct positional policies without hard-coded behavior trees
- Limiting teammates/opponents to 2 each balances spatial awareness with dimensional efficiency

### 1.3 Action Space (40 Continuous)
- Joint rotations (26): chest(3), spine(3), head(2), thighs(4), shins(2), feet(6), arms(4), forearms(2)
- Joint strengths (13): distributed across major appendages for dynamic stiffness control
- Kick intensity (1): enabled only when `ball_touch >= 0.5` (Lesson 3+)

Rationale:
- Strength modulation prevents rigid gait plateaus and improves adaptation to ball interactions
- Single kick channel simplifies intent while allowing variable impulse scaling

### 1.4 Kick Mechanism
- Physics-based impulse applied when ball within range + cooldown satisfied
- Gated by curriculum (prevents early exploitative flailing)
- Upright gating in newer anti-dive design (only effective when posture ≥ threshold)

---
## 2. Training Methodology

### 2.1 Two-Stage Transfer Learning
| Stage | Algorithm | Focus | Steps | Key Mode Flag |
|-------|-----------|-------|-------|---------------|
| 1 | PPO | Robust omnidirectional locomotion | 10–30M | `locomotion_only = 1.0` |
| 2 | POCA | Multi-agent soccer strategy | 15–20M | `locomotion_only = 0.0` |

Rationale:
- Separating locomotion reduces variance: soccer reward sparsity initially would otherwise destabilize joint control
- POCA provides centralized credit assignment for coordinated behaviors (marking, clearing, goal defense)

### 2.2 Algorithms
- Stage 1 (PPO): Stable single-policy optimization, high entropy early for gait diversity
- Stage 2 (MA-POCA): Multi-agent posthumous credit assignment -> improved temporal credit for team outcomes (goals, defensive actions)

### 2.3 Hyperparameters (Representative Final)
Stage 1 (PPO):
```yaml
learning_rate: 0.0003
batch_size: 2048
hidden_units: 512
num_layers: 3
gamma: 0.99
entropy (beta): 0.005
```
Stage 2 (POCA):
```yaml
learning_rate: 0.0001
batch_size: 8192
buffer_size: 81920
gamma: 0.995
beta: 0.005
epsilon: 0.2
lambd: 0.95
num_epoch: 3
```
Rationale:
- Lower LR in Stage 2 to preserve transferred locomotion weights
- Larger batch/buffer for multi-agent stability and variance reduction
- Slightly higher gamma (0.995) for goal influence over ~150–200 step horizon

### 2.4 Curriculum Parameters
| Parameter | Lessons / Progression | Purpose |
|-----------|-----------------------|---------|
| `ball_touch` | 0.35 → 0.5 → 1.0 | Gradually enables and scales ball interaction & kicking |
| `ball_spawn_radius` | 1.5 → 2.0 → 1.5 | Early chase central → broaden spatial search → tighten for scoring |
| `locomotion_scale` | 0.5 → 0.4 → 0.15 | Fade locomotion shaping as soccer mastery increases |
| `self_play_weight` | 0.0 → 1.0 @ 50% | Automatic transition from cooperative skill acquisition to competitive play |
| `locomotion_only` | 1.0 (Stage 1) / 0.0 (Stage 2) | Toggle soccer observation activation |

Rationale:
- Early de-emphasis of competition reduces sparse defeat penalties interfering with skill acquisition
- Radius widening before kick introduction increases ball approach generalization
- Locomotion reward fading forces policy to attend to new soccer-specific features

### 2.5 Reward Structure (Design & Justification)

#### Stage 1 (Locomotion)
| Component | Value(s) | Purpose |
|----------|----------|---------|
| Valid touch | +0.2 | Reinforces upright, controlled gait rather than diving |
| Dive penalties | -0.15 to -1.35 | Suppresses exploit of falling toward target for reset cycles |
| Turn encouragement | +0.05 × progress | Ensures omnidirectional capability (critical for later soccer pursuit) |
| Standing penalty | -0.02 × angle frac | Discourages inertia lock when target moves laterally |
| Upright bonus | +0.03 | Maintains stable posture during locomotion learning |

#### Stage 2 (Soccer)
| Component | Value(s) | Purpose |
|----------|----------|---------|
| Goals (score) | +50 × time_bonus | Dominant sparse objective for team success |
| Goals (concede) | -10 × `self_play_weight` | Scales competitive pressure safely via curriculum |
| Ball touches | +0.2 × curriculum | Progressively increases salience of ball interaction |
| Kick bonus | +0.1 | Encourages deliberate kicks post–Lesson 3 |
| Upright bonus | +0.03 | Maintains stable posture in chaotic scrums |
| Role (Striker) | +0.015 near opponent goal | Encourages forward offensive positioning |
| Role (Goalie) | +0.015 alignment ball–goal | Reinforces blocking alignment & anchoring |
| Own-goal velocity | -0.04 × toward speed | Strong deterrent to accidental clears backward |
| Own-goal proximity | -0.08 × proximity factor | Increased penalty near danger zone |
| Clearance progress | +0.01 moving ball out | Positive shaping for defensive recoveries |
| Spacing penalty | -0.005 per close teammate | Mitigates bunching around ball |
| Marking bonus | +0.005 per opponent within 3m | Promotes zone control & defense |
| Corner camping | -0.08 | Removes exploit of respawn-center tactic |
| Ball freed from corner | +0.02 | Encourages active repositioning |
| Goalie leash penalty | Dynamic -0.006/m outside leash | Keeps goalie in defendable zone |
| Goalie block bonus | +0.03–0.07 | Rewards accurate alignment with incoming ball |

Design Philosophy:
- Layered shaping -> locomotion → ball interaction → spatial strategy → competition
- Anti-exploit shaping applied only where degenerate loops emerged (corner camping, own goals)
- Role-based micro-rewards small enough not to overpower universal objectives

### 2.6 Transfer Considerations
- Identical input dimension (269) across stages via zeroed soccer obs Phase 1
- Neuron reuse rate high; early Stage 2 updates focus on new soccer channels
- Optimizer state intentionally NOT transferred (different exploration dynamics PPO vs POCA)

### 2.7 Discount Factor Impact
- `gamma=0.995` => effective weighting >50% for last ~150–200 steps
- Justifies dense shaping (progress, posture) to bridge sparse goal reward

---
## 3. Experimentation Summary (Key Iterations & Learnings)
| Topic | Issue | Attempt | Outcome | Decision |
|-------|-------|--------|---------|----------|
| Redundant target obs | Target = ball in Stage 2 | Placeholder zero vector | Reduced duplicate signal | Adopted (kept count constant) |
| Observation expansion | Lack of role/goal spatial awareness | Added 19 soccer obs | Enabled distinct striker/goalie policies | Adopted |
| Dive exploitation | Agents belly-flopped for resets | Touch validation + penalties | Reduced dive frequency | Adopted |
| Corner camping | Ball forced to corner to trigger respawn | Camping penalty + movement bonus | Exploit eliminated | Adopted |
| Own-goal clears | Ball shot backward into own goal | Velocity & proximity penalties | Strong reduction in own-goals | Adopted |
| Goalie wandering | Goalie chased ball mid-field | Dynamic leash + alignment bonus | Stable defensive anchoring | Adopted |
| Manual self-play activation | Needed manual config edits | Curriculum `self_play_weight` | Automatic competition phase | Adopted |
| Spawn randomness Stage 2 | Randomization disrupted formations | Fixed team spawn anchors | Stable early tactics | Adopted |
| High locomotion reward late | Soccer signals diluted | Faded `locomotion_scale` | Better soccer focus | Adopted |
| Larger batch sizes | Instability with small buffer | Increased batch/buffer in POCA | More stable gradients | Adopted |
| Passing reward shaping | Trial heuristic detection | No reliable emergence early | Deferred |
| Removing target Stage 1 | Pure free-balance locomotion | Much slower convergence | Deferred (future experiment) |

---
## 4. Observed Behaviors & Strategies
**Locomotion:** Stable upright gait, adaptive turning, recovery from minor perturbations.
**Ball Pursuit:** Efficient curvature paths instead of beelines; modest velocity modulation near ball.
**Goalie Positioning:** Anchoring near goal center, lateral tracking of ball, improved interception alignment post leash update.
**Striker Advancement:** Forward bias into opponent half; opportunistic ball shadowing.
**Defensive Clearing:** Increased forward kicks away from own goal after penalty introduction.
**Spacing Emergence:** Reduction in triple clustering; agents occupy triangle-like formations intermittently.
**Marking / Shadowing:** Agents trail opponents between ball and goal, especially when ball near scoring zone.
**Corner Extraction:** Active attempts to nudge ball out of corner rather than waiting.
**Dribbling (Early Form):** Short controlled multi-contact pushes (improved post kick enablement).
**Failed Strategies (Pre-Fix):** Corner camping, own-goal velocity amplification, goalie full-field chasing.
**Non-Emergent (Yet):** Purposeful passing, coordinated multi-agent trapping, feints.

---
## 5. Future Improvement Opportunities
| Area | Proposal | Rationale | Expected Challenge |
|------|----------|-----------|--------------------|
| Episodic design | Remove ball respawns (continuous play) | More realistic flow & possession learning | Handling long stagnant states |
| Locomotion authenticity | Remove all respawns (no target assistance) | Teaches recovery and gait stabilization in soccer context | Longer convergence horizon |
| Passing behavior | Add pass detection + shared credit | Encourages multi-agent coordination beyond chasing | Avoiding reward hacking |
| Communication | Add discrete message channels or attention pooling | Enables intentional role coordination | Architecture complexity |
| Memory | Introduce LSTM (sequence_length 64–128) | Temporal strategy (anticipation, delayed kicks) | Longer training time |
| Curriculum refinement | Dynamic lesson advancement (adaptive thresholds) | Personalized progression for faster learning | Complexity of metric reliability |
| Observation compression | Remove low-impact locomotion features post Stage 1 | Smaller network, faster inference | Risk of losing subtle balance cues |
| Reward minimalism | Gradual removal of shaping (keep only goals & penalties) | Test robustness & generalization | Potential instability regression |
| Domain randomization | Vary friction, ball mass, field size | Improves transfer robustness | Parameter explosion |
| Self-play enhancement | Adaptive opponent snapshot selection (performance window) | Avoids stale opponent pool | Tracking & selection overhead |
| Hierarchical RL | Separate locomotion controller + tactical planner | Cleaner credit assignment | Architectural complexity |
| Action space reduction | Mirror limbs, reduce strength channels | Faster learning, simpler policy | Potential loss of fine motor control |
| Evaluation metrics | Track possession time, defensive saves | Richer training dashboards | Additional instrumentation |
| Multi-ball experiments | Introduce second ball chaos mode | Stress test policy robustness | Increased variance |
| Opponent diversity | Inject scripted baseline bots | Stabilize early soccer shaping | Handcrafted logic effort |
| Penalty tuning | Anneal anti-exploit strengths | Prevent over-optimization of safe play | Risk return of exploits |

---
## 6. Design Trade-Offs
- Added shaping vs risk of overfitting to heuristics: mitigated by small magnitudes & staged fading.
- Expanded observation space vs computational cost: 19 extra floats acceptable relative to improved tactical behavior.
- Curriculum complexity vs simplicity: prioritized clarity (3–4 key parameters) + one automation (`self_play_weight`).
- Anti-exploit penalties applied surgically only where degenerate loops materially harmed learning signal quality.

---
## 7. Final Policy Characteristics
| Aspect | Result |
|--------|--------|
| Gait Stability | High (rare collapses after 2M Stage 1) |
| Goal Defense | Improved post leash + own-goal penalties |
| Offensive Pressure | Sustained striker forward bias |
| Ball Interaction | Intentional kicking after Lesson 3 enablement |
| Exploit Avoidance | Corner camping eliminated, own-goals rare |
| Adaptability | Maintains locomotion under soccer reward regime |
| Team Coordination | Early spacing & marking; passing not yet consistent |

---
## 8. Migration Notes
- Old 250-observation models incompatible with new 269 architecture (must retrain Stage 1)
- To revert: remove added soccer obs & associated rewards; restore original 7 soccer obs layout.

---
## 9. Recommended Next Experiment Sequence
1. Train reduced-shaping variant (remove spacing & marking rewards) to test emergence quality.
2. Introduce LSTM memory; measure improvement in interception timing.
3. Add possession tracking; design neutral pass reward (+0.05 shared both agents when controlled handoff detected).
4. Begin shaping anneal schedule (linearly decay minor role & spacing rewards over final 25% steps).
5. Deploy domain randomization batch (vary ball mass ±20%, friction ±15%).

---
## 10. Summary
The 269-observation final version establishes a robust foundation for emergent multi-agent soccer in ragdoll walkers by layering locomotion mastery, structured curriculum, spatially enriched observations, strategic reward shaping, and automated competitive progression. Anti-exploit measures resolved key degeneracies without over-constraining behavior, enabling natural striker and goalie differentiation, defensive clearance behaviors, and early coordination patterns. Future work centers on reducing shaping for generality, fostering higher-order tactics (passing, formations), and stress-testing robustness under randomized conditions.

---
**End of Final Overview**
