# Walker Soccer Two-Stage - Changelog

## December 2025 - Major Feature Update

### Observation Space Expansion (250 → 269 observations)

**Added 19 new soccer observations (total 26 soccer obs):**
- Role identifier (1 float): Striker = +1, Goalie = -1, Generic = 0
- Own goal position (3 floats): agent's goal to defend
- Opponent goal position (3 floats): target goal to score on
- Teammate 1 position (3 floats): first teammate's position (zero if none)
- Teammate 2 position (3 floats): second teammate's position (zero if <2)
- Opponent 1 position (3 floats): first opponent's position (zero if none)
- Opponent 2 position (3 floats): second opponent's position (zero if <2)

**Impact:**
- Enables role-aware behavior (Strikers vs Goalies)
- Provides spatial awareness of goals (offensive/defensive positioning)
- Allows coordination with teammates (spacing, formations)
- Enables opponent tracking (marking, intercepting)
- Maintains Stage 1/Stage 2 consistency (zeros in Stage 1, real data in Stage 2)

### Role-Aware Reward Shaping

**Striker Rewards:**
- +0.015 per step when near opponent goal (encourages attacking positioning)

**Goalie Rewards:**
- +0.015 per step when aligned with ball (defensive positioning)
- Dynamic leash system (1.5m-3.2m based on ball distance)
- Stronger blocking position bonus (+0.03-0.07 based on alignment)

**Impact:**
- Agents learn role-specific behaviors without explicit instructions
- Strikers push forward, Goalies stay back
- More realistic soccer positioning and strategy

### Goal-Aware Penalties

**Own-Goal Discouragement:**
- 4x velocity penalty: -0.04 × speedTowardOwn (was -0.01)
- Position-based penalty: -0.08 × proximity when moving ball toward own goal
- Progress bonus: +0.01 for moving ball away from own goal

**Impact:**
- Strong discouragement of shooting into own goal
- Rewards defensive clearances
- Reduces accidental own-goals during training

### Coordination Incentives

**Team Spacing:**
- Penalty: -0.005 per teammate within 2m (reduces bunching)

**Opponent Marking:**
- Bonus: +0.005 per opponent within 3m (encourages defensive coverage)

**Impact:**
- Agents spread out instead of clustering
- Better field coverage and positioning
- Emergent defensive formations

### Anti-Exploit Mechanisms

#### 1. Corner Camping (Fixed)
**Problem:** Agents pushed ball into corners and camped, waiting for unstuck respawn.

**Solution:**
- Strong penalty: -0.08 for staying near stuck ball in corners
- Movement bonus: +0.02 for moving ball away from corners
- Detection: ballStuckTimer > 2.0s, agent < 3.5m, ball > 10m from center

**Impact:** Agents actively play ball out of corners instead of exploiting respawn.

#### 2. Own-Goal Shooting (Fixed)
**Problem:** Agents sometimes shot ball into their own goal.

**Solution:**
- 4x velocity penalty (see Goal-Aware Penalties above)
- Position-based scaling by proximity to own goal
- Progress rewards for moving ball away

**Impact:** Near-zero own-goal incidents, strong defensive instinct.

#### 3. Goalie Wandering (Fixed)
**Problem:** Goalies left goal area too frequently.

**Solution:**
- Dynamic leash: 1.5m when ball close (<15m), 3.2m when ball far
- Doubled penalty strength: -0.006 per meter outside leash (was -0.003)
- Stronger blocking bonus: +0.03 base + 0.04 × alignment

**Impact:** Goalies stay in position, better shot blocking, fewer unguarded goals.

### Self-Play Automation

**New Curriculum Parameter: `self_play_weight`**
- Starts at 0.0 (cooperative learning)
- Ramps to 1.0 at 50% training progress (~7.5M steps for 15M total)
- Scales opponent penalty in `GoalTouched()` method

**Behavior:**
- Early training (0.0): No penalty for conceding, focus on ball interaction
- Mid training (0.0-1.0): Gradual competitive pressure introduction
- Late training (1.0): Full competitive penalty (-10 for conceding)

**Impact:**
- Eliminates need for manual config changes
- Smooth transition from cooperative to competitive learning
- Agents learn ball skills before facing full competitive pressure

### Documentation Updates

**Updated Files:**
- `README.md`: Observation space (269), new rewards, self_play_weight, anti-exploit features
- `SYSTEM_OVERVIEW.md`: Observation breakdown (26 soccer obs), curriculum automation, reward structure
- `TRAINING_GUIDE.md`: Observation counts, curriculum parameters, troubleshooting
- `QUICK_REFERENCE.md`: Observation space (269), debug checklist
- `DEBUG_AND_IMPROVEMENTS.md`: Anti-exploit mechanisms, latest updates, behavioral improvements
- `PPO_AND_POCA.md`: Observation count (269)

**Changes:**
- All "250 observations" → "269 observations"
- All "7 soccer obs" → "26 soccer obs" with detailed breakdown
- Added role/goal/teammate/opponent observation documentation
- Added self_play_weight curriculum explanation
- Added anti-exploit mechanism documentation
- Updated reward structure with new penalties/bonuses

## Training Recommendations

### Stage 1 (Locomotion)
- No changes from previous version
- Continue training for 10-30M steps
- Target mean reward: +20-35

### Stage 2 (Soccer)
- Use updated observation space (269)
- Enable self_play_weight curriculum (automatic competitive transition)
- Monitor role-specific behaviors in TensorBoard
- Expect:
  - Early steps: cooperative ball chasing
  - Mid steps (7.5M): increasing competitive behavior
  - Late steps (12M+): full competitive soccer with role differentiation

### Build Configuration
- BehaviorParameters: Vector Observation Space = **269** (both stages)
- Ensure all 6 agents have correct Team IDs (0 for Blue, 1 for Purple)
- Verify Position assignments (Goalie/Striker) for role-aware rewards

## Migration from Previous Version

If you have existing Stage 1 models (250 observations):
- **Cannot transfer** to new 269-observation Stage 2
- Must retrain Stage 1 with updated observation space
- Alternatively, modify code to match old 250-obs system

If you want to keep old system:
- Revert `CollectObservations()` to output only 7 soccer obs (ball, velocity, team)
- Remove role/goal/teammate/opponent observations
- Remove self_play_weight curriculum (use manual config)
- Remove anti-exploit penalties (or keep them with old obs space)

## Summary

This update represents a major enhancement to the Walker Soccer project:
- **26 soccer observations** (vs 7 previously) enable sophisticated spatial reasoning
- **Role-aware rewards** create natural Striker/Goalie differentiation
- **Goal-aware penalties** eliminate own-goal exploits
- **Coordination incentives** improve team play and positioning
- **Anti-exploit mechanisms** fix corner camping, own-goal shooting, goalie wandering
- **Self-play automation** removes need for manual competitive transition
- **Comprehensive documentation** ensures all changes are properly explained

Total observation space: **269 dimensions** (243 locomotion + 26 soccer)
