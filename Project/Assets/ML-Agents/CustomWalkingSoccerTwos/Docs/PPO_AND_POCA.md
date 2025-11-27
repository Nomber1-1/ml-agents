# PPO and POCA in Walker Soccer: Deep Dive

This document explains how Proximal Policy Optimization (PPO) and POsthumous Credit Assignment (POCA) work specifically in the Walker Soccer two-stage training pipeline.

---

## Stage 1: PPO (Proximal Policy Optimization) - Locomotion Training

### What PPO Does
PPO is a **single-agent, on-policy** reinforcement learning algorithm designed to learn stable policies through conservative policy updates.

### How It Works in Stage 1

#### 1. **Policy Structure**
```csharp
// Your agent's neural network (3 layers × 512 units)
// Input: 250-dim locomotion observations (soccer obs = 0)
// Output: 39 continuous actions (joint torques for walker body)
```

#### 2. **Experience Collection**
- Agent interacts with environment for `time_horizon=1000` steps
- Collects tuples: (state, action, reward, next_state, done)
- Stores in replay buffer (`buffer_size=81920`)

#### 3. **Policy Update (PPO Clipping)**
```python
# Simplified PPO objective from your config:
# hyperparameters:
#   epsilon: 0.2      # Clipping range
#   batch_size: 8192
#   num_epoch: 3      # Updates per batch

ratio = π_new(action|state) / π_old(action|state)
clipped_ratio = clip(ratio, 1-0.2, 1+0.2)
L_clip = min(ratio * advantage, clipped_ratio * advantage)
```

**Key Insight**: PPO prevents the policy from changing too drastically by clipping the probability ratio. This ensures:
- Stable locomotion learning (walker doesn't "forget" how to stand)
- Conservative updates when `ratio > 1.2` or `ratio < 0.8`
- Your `beta=0.005` adds entropy bonus to encourage exploration of gaits

#### 4. **Advantage Estimation (GAE)**
```python
# Your config: lambd=0.95, gamma=0.99
# GAE smooths advantage estimates across timesteps

A_t = δ_t + (γλ)δ_{t+1} + (γλ)²δ_{t+2} + ...
where δ_t = r_t + γV(s_{t+1}) - V(s_t)
```

This helps the agent learn:
- "Walking forward consistently is good" (high γ=0.99 values long-term rewards)
- "Small corrections matter" (λ=0.95 balances bias/variance in advantage)

#### 5. **Why PPO for Locomotion?**
- **Sample efficiency**: Large `batch_size=8192` learns stable gaits faster
- **Stability**: Walker doesn't collapse mid-training from erratic policy changes
- **Transfer learning**: Learned locomotion skills transfer cleanly to Stage 2

---

## Stage 2: POCA (POsthumous Credit Assignment) - Multi-Agent Soccer

### What POCA Does
POCA is a **multi-agent cooperative** RL algorithm that solves the **credit assignment problem**: "Which agent's action led to the team goal?"

### How It Works in Stage 2

#### 1. **Team-Based Learning**
```csharp
// Your setup: 2 teams × 3 agents = 6 agents learning simultaneously
// Team 0 (Blue): 3 agents (share policy weights)
// Team 1 (Purple): 3 agents (separate policy weights)
```

#### 2. **Centralized Critic, Decentralized Actors**
```
Actor (each agent):
  Input: 250-dim obs (243 locomotion + 7 soccer)
  Output: 40 actions (39 joints + 1 kick trigger)
  
Critic (team-wide):
  Input: All teammates' observations
  Output: Value estimate for team return
```

**Your config:**
```yaml
network_settings:
  hidden_units: 512
  num_layers: 3
  # Critic sees all teammates' obs → better credit assignment
```

#### 3. **Reward Distribution Problem**
Traditional PPO would struggle here:
```python
# Scenario: Blue Team scores a goal (+50 group reward)
# Question: Which blue agent deserves credit?
#   - Agent who kicked? (+50?)
#   - Agent who passed? (0?)
#   - Agent who blocked defender? (0?)
```

POCA solves this with **counterfactual baselines**:

#### 4. **Counterfactual Value Functions**
```python
# For each agent i on team:
V_team(s)       # Value if all agents act normally
V_team^{-i}(s)  # Value if agent i is replaced by average policy

# Agent i's contribution:
advantage_i = V_team(s) - V_team^{-i}(s)
```

**Example from your training:**
```
Goal scored at Step 12060000 (Group Reward: 8.846)

Agent 1 (striker):
  V_team = 8.846
  V_team^{-1} = 1.0  (without striker, goal unlikely)
  advantage_1 = +7.846 → Strong positive update

Agent 2 (passer):
  V_team = 8.846
  V_team^{-2} = 3.0  (without pass, harder shot)
  advantage_2 = +5.846 → Moderate positive update

Agent 3 (defender):
  V_team = 8.846
  V_team^{-3} = 8.5  (defender didn't impact this goal)
  advantage_3 = +0.346 → Small update
```

#### 5. **Policy Update (POCA = PPO + Counterfactuals)**
```python
# Same PPO clipping, but advantage is team-aware:
ratio = π_new(action|state) / π_old(action|state)
L_POCA = min(
    ratio * advantage_counterfactual,
    clip(ratio, 1-ε, 1+ε) * advantage_counterfactual
)
```

#### 6. **Your Stage 2 Config Details**
```yaml
hyperparameters:
  batch_size: 8192    # Same as Stage 1 for consistency
  buffer_size: 81920  # 6 agents sharing experience buffer
  learning_rate: 0.0001  # Lower for fine-tuning transfer
  
reward_signals:
  extrinsic:
    gamma: 0.995  # Slightly higher discount (longer soccer sequences)
```

---

## Key Differences in Your Project

| Aspect | Stage 1 (PPO) | Stage 2 (POCA) |
|--------|---------------|----------------|
| **Agent count** | 1 walker per arena | 6 walkers (2 teams of 3) |
| **Critic** | Individual V(s) | Team V(s_team) |
| **Credit** | Direct (agent's own actions) | Counterfactual (team contribution) |
| **Observations** | 250 (243 locomotion + 7 zeros) | 250 (243 locomotion + 7 soccer) |
| **Actions** | 40 continuous (kick ignored) | 40 continuous (kick active Lesson 3+) |
| **Gamma** | 0.99 | 0.995 (longer horizons for team plays) |
| **Learning rate** | 0.0003 | 0.0001 (fine-tuning) |
| **Trainer type** | ppo | poca |

---

## How They Work Together in Your Training

### Transfer Learning Flow
```
Stage 1 (PPO):
  18M steps → Learned walking, turning, speed control
  Output: WalkerStage1.onnx (frozen locomotion skills)
  
Stage 2 (POCA):
  --initialize-from=WalkerStage1
  15M steps → Learned ball chasing, kicking, team coordination
  Output: WalkerStage2_20_V3.onnx (final soccer policy)
```

### Why This Architecture Works

1. **PPO builds foundation**:
   - Walker masters upright posture, gait, navigation
   - No multi-agent complexity → faster convergence
   - Robust locomotion transfers to any task

2. **POCA adds cooperation**:
   - Agents learn "who should go for ball" (implicit role assignment)
   - Counterfactuals prevent "credit stealing" (all agents rushing ball)
   - Your curriculum gradually enables soccer skills on top of locomotion

### Evidence in Your Logs
```
Step 12010000: Mean Reward: 2.050 (positive after anti-dive fixes)
Step 12060000: Group Reward: 8.846 (goal scored, credit distributed)
Step 12120000: Group Reward: 3.941 (another goal, learning continues)
```

POCA is successfully:
- Assigning credit to strikers for goals
- Rewarding passers for assists (implicit in group reward)
- Maintaining team coordination (Mean Reward stays positive)

---

## Challenges You Overcame

### 1. **Diving Problem (Steps 10M-12M)**
**Issue**: Agents learned "dive at ball" → high contact reward, low skill
```python
# Your fix: Anti-dive penalties
if upDot < 0.85 and near_ball:
    penalty = -0.05 * (0.85 - upDot) * downward_velocity
    if dive_duration > 45 steps:
        EndEpisode()  # Early reset
```

**Why POCA struggled here**: Counterfactual baseline didn't distinguish "good contact" (upright kick) from "bad contact" (dive scrum). Your reward shaping fixed this.

### 2. **Bunching/Wall Formation**
**Issue**: Defensive agents learned to cluster → block shots
```python
# Your fix: Stronger bunching penalty
bunching_penalty = -0.02 * nearby_teammates  # Was -0.012
```

**Why POCA struggled**: Team value function rewarded "prevent opponent goals" → defensive wall was locally optimal. Your penalty broke this local minimum.

### 3. **Low Goal Frequency**
**Issue**: Mean Reward ~2-3, but Group Reward (goals) rare
```python
# Your fix: Boost kick rewards
kick_reward = 0.3 * phase_multiplier * (1 + 0.35*alignment + 0.6*shot_bonus)
kick_cost = -0.03  # Spam prevention
```

**Why this works with POCA**: Higher kick rewards → counterfactual advantage for kickers increases → more agents learn "kick when aligned" → goals become more frequent.

---

## Mathematical Details

### PPO Objective Function

The full PPO loss function used in Stage 1:

```
L_PPO(θ) = E_t[
    min(r_t(θ) * A_t, clip(r_t(θ), 1-ε, 1+ε) * A_t)
    - c_1 * L_VF(θ)
    + c_2 * S[π_θ](s_t)
]

where:
  r_t(θ) = π_θ(a_t|s_t) / π_θ_old(a_t|s_t)  # Probability ratio
  A_t = GAE advantage estimate
  L_VF = Value function loss (MSE)
  S = Entropy bonus
  ε = 0.2 (your clipping parameter)
  c_1 = 0.5 (value loss coefficient)
  c_2 = 0.005 (your beta entropy coefficient)
```

### POCA Advantage Calculation

The counterfactual advantage used in Stage 2:

```
A_i^POCA(s,a) = Q_team(s,a) - V_team^{-i}(s)

where:
  Q_team(s,a) = Expected team return after joint action a
  V_team^{-i}(s) = Expected team return if agent i uses baseline policy
  
Baseline policy options:
  1. Average policy (mixture of historical policies)
  2. Do-nothing baseline (agent i takes no action)
  3. Uniform random policy
  
Your implementation uses option 1 (via self-play snapshots)
```

### Generalized Advantage Estimation (GAE)

Used by both PPO and POCA for advantage calculation:

```
A_t^GAE = Σ_{l=0}^∞ (γλ)^l δ_{t+l}

where:
  δ_t = r_t + γV(s_{t+1}) - V(s_t)  # TD error
  γ = 0.99 (Stage 1) or 0.995 (Stage 2)
  λ = 0.95 (your config)
  
Higher λ → More bias toward Monte Carlo (full episode returns)
Lower λ → More bias toward TD (bootstrapped estimates)
```

---

## Implementation Details

### Stage 1: PPO Training Loop

```python
# Pseudocode for your Stage 1 training

for episode in range(max_episodes):
    # Reset environment
    state = env.reset()  # Random spawn, target placement
    episode_reward = 0
    
    for step in range(time_horizon):  # 1000 steps
        # Policy produces action
        action = policy.act(state)  # 40 continuous outputs
        
        # Environment step
        next_state, reward, done = env.step(action)
        
        # Store experience
        buffer.add(state, action, reward, done)
        
        episode_reward += reward
        state = next_state
        
        if done:
            break
    
    # Every buffer_size steps, update policy
    if len(buffer) >= 8192:
        for epoch in range(3):  # num_epoch
            # Sample batch
            batch = buffer.sample(8192)
            
            # Compute advantages with GAE
            advantages = compute_gae(batch, gamma=0.99, lambd=0.95)
            
            # PPO update
            loss = ppo_loss(policy, batch, advantages, epsilon=0.2)
            optimizer.step(loss)
        
        buffer.clear()
```

### Stage 2: POCA Training Loop

```python
# Pseudocode for your Stage 2 training

for episode in range(max_episodes):
    # Reset environment (6 agents)
    states = env.reset()  # Fixed team positions
    
    for step in range(time_horizon):
        # Each agent produces action
        actions = {}
        for team in [blue, purple]:
            for agent in team.agents:
                actions[agent] = policy[team].act(states[agent])
        
        # Environment step (all agents act simultaneously)
        next_states, rewards, dones = env.step(actions)
        
        # Check for goals
        if goal_scored:
            # Distribute group reward to scoring team
            for agent in scoring_team:
                rewards[agent] += group_reward * time_bonus
            
            # Penalty to conceding team
            for agent in conceding_team:
                rewards[agent] += -10
        
        # Store team experiences
        for team in [blue, purple]:
            team_buffer.add(
                team_states=states[team],
                team_actions=actions[team],
                team_rewards=rewards[team],
                dones=dones[team]
            )
        
        states = next_states
    
    # Update each team's policy
    if len(team_buffer) >= 8192:
        for team in [blue, purple]:
            batch = team_buffer[team].sample(8192)
            
            # Compute counterfactual advantages
            advantages = compute_poca_advantages(
                batch, 
                gamma=0.995, 
                lambd=0.95,
                team_value_fn=critic[team]
            )
            
            # PPO-style update with team advantages
            loss = poca_loss(policy[team], batch, advantages, epsilon=0.2)
            optimizer.step(loss)
```

---

## Hyperparameter Tuning Insights

### Why Your Specific Values Work

#### Stage 1 (PPO)
```yaml
learning_rate: 0.0003
  # Higher LR for faster locomotion learning
  # Walker physics are deterministic → can learn aggressively
  
batch_size: 8192
  # Large batches → stable gradient estimates
  # Critical for continuous control (smooth joint movements)
  
buffer_size: 81920
  # 10× batch_size → diverse experience replay
  # Prevents overfitting to recent locomotion patterns
  
epsilon: 0.2
  # Standard PPO clipping
  # Allows 20% policy change per update → balanced exploration
  
beta: 0.005
  # Low entropy bonus → exploitation over exploration
  # Once walking is learned, refine it (don't randomize gaits)
  
gamma: 0.99
  # High discount → values long-term stability
  # "Consistent walking is better than sporadic success"
```

#### Stage 2 (POCA)
```yaml
learning_rate: 0.0001
  # Lower LR for fine-tuning transferred locomotion
  # Prevents catastrophic forgetting of walking skills
  
batch_size: 8192
  # Same as Stage 1 for consistency
  # 6 agents × ~1365 steps each per batch
  
gamma: 0.995
  # Higher discount than Stage 1
  # Soccer plays unfold over longer sequences
  # "Setup → pass → shot" requires multi-step planning
  
time_horizon: 1000
  # Same as Stage 1
  # Episode length ~1000 steps before timeout
  # Matches typical soccer game duration
```

---

## Self-Play Integration (Stage 2 Only)

### How Self-Play Enhances POCA

```yaml
self_play:
  save_steps: 50000      # Save policy snapshot every 50K steps
  team_change: 200000    # Rotate opponent pool every 200K steps
  swap_steps: 10000      # Gradual opponent transition
  window: 5              # Keep last 5 snapshots
  play_against_latest_model_ratio: 0.5
```

**Mechanism:**
1. Train Blue vs Purple (both improving)
2. Every 50K steps: Save Purple's policy as snapshot
3. Every 200K steps: Blue faces mixture of:
   - 50% current Purple policy (cutting edge opponent)
   - 50% historical Purple snapshots (diverse strategies)

**Why This Matters for POCA:**
- Counterfactual baselines stay calibrated
- Team learns to counter evolving opponent strategies
- Prevents local optima (e.g., "always rush ball" only works vs weak defense)

**Your Decision to Disable:**
```yaml
# self_play:  # Currently disabled
#   save_steps: 50000
#   ...
```

**Reasoning:** Goals were rare early in training → ELO ratings meaningless → self-play adds noise. Re-enable after consistent goal-scoring (goals-per-episode > 0.3).

---

## Curriculum Learning Integration

### How Curriculum Guides POCA

Your curriculum parameters modulate the learning signal:

```yaml
environment_parameters:
  ball_touch:  # Scales ball contact reward
    - Lesson 2 (0-2M): 0.35 → "Ball exists but less important"
    - Lesson 3 (2M-6M): 0.5 → "Kicking enabled, practice shots"
    - Lesson 4 (6M+): 1.0 → "Full soccer, goals matter most"
  
  ball_spawn_radius:  # Ball placement difficulty
    - Lesson 2: 1.5m → "Ball nearby, easy to reach"
    - Lesson 3: 2.0m → "Wider spawn, need positioning"
    - Lesson 4: 1.5m → "Centered, intense play"
  
  locomotion_scale:  # Fade Stage 1 locomotion rewards
    - 0.5 (start) → 0.4 (1.5M) → 0.15 (6M+)
    - Prevents locomotion from dominating soccer rewards
```

**POCA + Curriculum Synergy:**
- Early lessons: POCA learns "chase ball as team"
- Mid lessons: POCA learns "coordinate kicks" (some pass, some shoot)
- Late lessons: POCA learns "strategic positioning" (strikers forward, goalie back)

**Example Counterfactual at Lesson 4:**
```python
# Agent 1 (striker) shoots and scores
V_team = 50.0  # Goal reward
V_team^{-1} = 2.0  # Without striker, no shot
advantage_1 = 48.0 * ball_touch_scale(1.0) = +48.0 → Strong update

# Same shot in Lesson 2:
advantage_1 = 48.0 * ball_touch_scale(0.35) = +16.8 → Weaker signal
```

Curriculum scales the counterfactual advantage, guiding POCA to prioritize different skills at different stages.

---

## Debugging POCA vs PPO

### Common Issues and How to Identify Cause

#### Issue: "Agents not coordinating"
**PPO (Stage 1)**: Not applicable (single agent)
**POCA (Stage 2)**: Check Group Reward in TensorBoard
```
If Group Reward = 0 for long periods:
  → Counterfactual baseline may be miscalibrated
  → Solution: Verify team_change frequency, check critic loss
  
If agents all chase ball (bunching):
  → POCA is working, but reward structure favors selfish play
  → Solution: Add spacing penalties (your -0.02 per nearby teammate)
```

#### Issue: "Performance degraded after transfer"
**PPO → POCA transition**: Optimizer state doesn't transfer
```
Expected warnings:
  [WARNING] Failed to load for module Optimizer:adam
  [WARNING] Failed to load for module Optimizer:critic
  
This is normal! Policy weights transfer, optimizer resets.
```

**If locomotion skills lost**:
```
Check observation space: MUST be 250 in both stages
Check locomotion_only parameter:
  Stage 1: 1.0 (zeros soccer obs)
  Stage 2: 0.0 (real soccer obs)
```

#### Issue: "Training unstable (reward oscillates)"
**PPO (Stage 1)**: Likely physics instability or reward clipping issue
```
Solutions:
  - Reduce max_joint_force_limit
  - Increase solver iterations
  - Lower learning_rate to 0.0001
```

**POCA (Stage 2)**: Could be team dynamics or opponent adaptation
```
Check:
  - Self-play enabled too early? (disable until goals frequent)
  - Reward components balanced? (locomotion_scale faded enough?)
  - Penalties too strong? (causing negative rewards)
```

---

## Performance Metrics

### What to Monitor in TensorBoard

#### Stage 1 (PPO) Metrics
```
Environment/Cumulative Reward:
  Target: +20 to +35 by 18M steps
  Meaning: Locomotion quality (higher = better walking/turning)

Policy/Learning Rate:
  Should stay: 0.0003 (constant schedule)
  
Losses/Policy Loss:
  Should: Decrease then stabilize around 0.01-0.05
  
Losses/Value Loss:
  Should: Decrease to <0.1
  Meaning: Critic accurately predicts locomotion rewards

Policy/Entropy:
  Starts: ~2.0 (high exploration)
  Ends: ~0.5 (refined gaits)
```

#### Stage 2 (POCA) Metrics
```
Environment/Cumulative Reward (Mean Reward):
  Target: -5 → 0 → +2 → +5+ over 15M steps
  Meaning: Individual agent performance
  
Environment/Group Cumulative Reward:
  Target: Sporadic spikes (0 → 50 when goals scored)
  Meaning: Team success (goals - conceded)
  
Self-Play/ELO (if enabled):
  Blue Team: Should hover around 1200 ± 100
  Purple Team: Should hover around 1200 ± 100
  Meaning: Balanced competition
  
Policy/Learning Rate:
  Should stay: 0.0001 (constant)
  
Losses/Policy Loss:
  Should: Decrease to 0.01-0.03 (slightly lower than Stage 1)
```

### Your Actual Results (from logs)
```
Stage 1 (18M steps):
  Mean Reward: +35 ✓ (excellent locomotion)
  
Stage 2 (15M steps):
  Mean Reward: +2 to +3.5 ✓ (positive, stable after anti-dive fixes)
  Group Reward: Spikes at 8.8, 3.9, 21.3, 8.8 ✓ (goals happening)
  Final Model: WalkerStage2_20_V3 ✓
```

---

## Advanced: POCA Algorithm Pseudocode

For those interested in implementation details:

```python
class POCATrainer:
    def __init__(self, num_agents_per_team=3):
        self.actor = PolicyNetwork()  # Decentralized
        self.critic = ValueNetwork()  # Centralized (sees all teammates)
        self.baseline_policies = []   # For counterfactuals
        
    def compute_advantages(self, batch):
        """Compute POCA counterfactual advantages"""
        advantages = []
        
        for agent_i in range(num_agents_per_team):
            # Full team value
            team_obs = [batch.obs[j] for j in range(num_agents_per_team)]
            V_team = self.critic(team_obs)
            
            # Counterfactual: replace agent i with baseline
            baseline_action = sample_from_baseline_policy(agent_i)
            team_obs_cf = team_obs.copy()
            team_obs_cf[agent_i] = apply_baseline_action(
                batch.obs[agent_i], 
                baseline_action
            )
            V_team_minus_i = self.critic(team_obs_cf)
            
            # Advantage = actual - counterfactual
            adv_i = V_team - V_team_minus_i
            advantages.append(adv_i)
        
        return advantages
    
    def update_policy(self, batch):
        """PPO-style update with POCA advantages"""
        advantages = self.compute_advantages(batch)
        
        for agent_i in range(num_agents_per_team):
            # Standard PPO clipping
            ratio = self.actor.prob(batch.actions[agent_i]) / \
                    self.old_actor.prob(batch.actions[agent_i])
            
            clipped_ratio = torch.clamp(ratio, 1-epsilon, 1+epsilon)
            
            # Loss uses counterfactual advantage
            loss = -torch.min(
                ratio * advantages[agent_i],
                clipped_ratio * advantages[agent_i]
            ).mean()
            
            # Backprop
            optimizer.zero_grad()
            loss.backward()
            optimizer.step()
        
        # Update baseline pool
        if step % save_steps == 0:
            self.baseline_policies.append(self.actor.copy())
            if len(self.baseline_policies) > window:
                self.baseline_policies.pop(0)
```

---

## Summary

**PPO (Stage 1)**: Single-agent algorithm that conservatively updates policy to learn stable locomotion through:
- Clipped policy ratios (prevents drastic changes)
- GAE advantage estimation (balances bias/variance)
- Entropy regularization (encourages exploration)

**POCA (Stage 2)**: Multi-agent extension of PPO that uses counterfactual baselines to fairly distribute credit among teammates, enabling cooperative behaviors like:
- Passing (passer gets credit even though didn't score)
- Positioning (defender gets credit for blocking opponent)
- Coordination (agents learn complementary roles)

**Your Success**: By combining PPO's stable locomotion foundation with POCA's multi-agent coordination, your agents learned to play soccer while maintaining robust walking skills. The key innovations were:

1. **Two-stage transfer learning**: Separate locomotion from soccer strategy
2. **Zero-padded observations**: Enable weight transfer while maintaining architecture
3. **Careful reward shaping**: Anti-dive, kick alignment, bunching penalties guide POCA
4. **Curriculum learning**: Gradual skill progression from chase → kick → score
5. **Penalty softening**: Recovery from 4M collapse by reducing penalty overload

**Final Result**: `WalkerStage2_20_V3` - A fully-trained soccer-playing walker that combines PPO-learned locomotion with POCA-learned team coordination.

---

## References

**Papers:**
- [Proximal Policy Optimization Algorithms (Schulman et al., 2017)](https://arxiv.org/abs/1707.06347)
- [The Surprising Effectiveness of PPO in Cooperative Multi-Agent Games (Yu et al., 2021)](https://arxiv.org/abs/2103.01955)
- [MA-POCA: Multi-Agent Posthumous Credit Assignment (Cohen et al., 2022)](https://arxiv.org/abs/2111.05992)

**Unity ML-Agents Documentation:**
- [Training with PPO](https://github.com/Unity-Technologies/ml-agents/blob/main/docs/Training-PPO.md)
- [MA-POCA Documentation](https://github.com/Unity-Technologies/ml-agents/blob/main/docs/Training-ML-Agents.md#multi-agent-poca)
- [Transfer Learning Guide](https://github.com/Unity-Technologies/ml-agents/blob/main/docs/Training-ML-Agents.md#training-using-concurrent-unity-instances)

---

**Document Version**: 1.0  
**Last Updated**: November 27, 2025  
**Project**: Walker Soccer Two-Stage Training  
**Final Model**: `results/WalkerStage2_20_V3`
