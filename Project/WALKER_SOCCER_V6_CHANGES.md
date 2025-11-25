# Walker Soccer V6 - Reward Exploitation Fix

**Date:** November 24, 2025  
**Problem Identified:** Agents achieving high training rewards (~23.77) while exhibiting poor standing quality

## Diagnostic Results (V5 at Step 3.15M)

### Debug Visualization Output:
```
Hips Height: 0.69m (target: 0.85-1.3) ✗
Upright Dot: 0.988 (target: >0.85) ✓
Forward Tip: 0.155 (target: <0.15) ✗ (borderline)
Sideways Lean: 0.012 (target: <0.3) ✓
Angular Velocity: 2.21 rad/s (target: <2.0) ✗
Current Reward: 23.77
Status: POOR (Standing: False, Stable: False)
```

### Root Cause Analysis:
The V5 reward structure allowed exploitation:
- Agents learned to maintain upright torso orientation (0.988 dot) → earns upright reward
- Agents minimized sideways lean (0.012) → avoids penalty
- **BUT** agents remained collapsed at 0.69m height (far below 0.85m target)
- **AND** maintained excessive angular velocity (2.21 > 2.0 rad/s)

The simple binary height check (`if height > 0.8f`) gave full reward at 0.8m, same as 1.3m, incentivizing minimal height.

## V6 Changes

### 1. Height-Scaled Reward (Lines 394-405)
**Old:**
```csharp
if (hipsHeight > 0.8f) AddReward(uprightRewardPerStep);
else if (hipsHeight < 0.3f) AddReward(-0.005f);
```

**New:**
```csharp
// Height-scaled reward: reward increases with height between 0.85-1.3m
if (hipsHeight >= 0.85f)
{
    float heightQuality = Mathf.Clamp01((hipsHeight - 0.85f) / (1.3f - 0.85f));
    AddReward(uprightRewardPerStep * (0.5f + 0.5f * heightQuality));
}
else if (hipsHeight < 0.75f)
{
    // Strong penalty for collapsed/crouched state
    float collapseAmount = (0.75f - hipsHeight) / 0.75f;
    AddReward(-0.05f * collapseAmount);
}
```

**Impact:**
- At 0.69m: Strong penalty (~-0.004 per step)
- At 0.85m: 50% reward (0.015 instead of 0.03)
- At 1.075m: 75% reward (0.0225)
- At 1.3m: 100% reward (0.03)
- Creates gradient incentivizing taller stance

### 2. Increased Height Reward Weight (Line 40)
```csharp
[SerializeField] private float uprightRewardPerStep = 0.03f; // Increased from 0.01
```

**Impact:** 3x stronger signal for proper height maintenance

### 3. Strengthened Angular Velocity Penalty (Line 47)
```csharp
[SerializeField] private float angVelPenaltyCoef = 0.005f; // Increased 5x from 0.001
```

**Impact:**
- At 2.21 rad/s: penalty = -0.011 per step (was -0.0022)
- At 1.0 rad/s: penalty = -0.005 per step (was -0.001)
- Strong disincentive for wobbling/instability

### 4. Extended Stabilization Period (Line 204)
```csharp
m_StabilizeSteps = m_SoccerSettings != null ? m_SoccerSettings.stabilizeStepsOnReset : 80; // Extended from 50
```

**Impact:** Agents get 30 extra frames (0.6 seconds) to settle into stable pose before soccer rewards activate

## Expected Outcomes

### Immediate Effects (First 100k Steps):
- Mean reward may **drop** initially as agents receive penalties for collapsed height
- Agents should gradually learn to stand taller (0.85m+ range)
- Angular velocity should decrease as penalty becomes more costly

### Medium-Term (500k Steps):
- Mean reward should recover to ~20-25 range with improved pose quality
- Visual inspection should show agents standing upright before walking
- Height metrics should center around 0.9-1.1m range

### Long-Term (1M+ Steps):
- Stable standing and walking locomotion before engaging soccer gameplay
- Reward-behavior alignment: high rewards correlate with good visual quality
- Potential to advance past Lesson 4 with actual skill improvement

## Testing Protocol

### 1. Visual Inspection (Debug Mode)
Enable `enableDebugMode = true` and `freezeAtStabilization = true` in Inspector:
- Observe agents at step 81 (end of stabilization)
- Check console logs for pose metrics
- Verify height is 0.85m+ and angular velocity < 2.0 rad/s

### 2. Training Metrics (TensorBoard)
Monitor during training:
- `Environment/Cumulative Reward`: expect initial drop, then recovery
- Custom metrics (if added): standing_success_rate, avg_hips_height

### 3. Curriculum Progression
- If agents remain stuck in Lesson 4 with V6 rewards, consider:
  - Reducing `min_lesson_length` to 800-1000 episodes
  - Adjusting threshold from 24.0 to 20.0 for Lesson 4
  - Adding explicit "standing time" curriculum parameter

## Rollback Plan

If V6 training shows no improvement after 500k steps:
1. Revert to V5 code (git checkout previous commit)
2. Consider alternative: Add telemetry-based curriculum (Option 3 from diagnostics)
3. Test isolation approach: Disable soccer rewards temporarily (Option 4)

## Files Modified

- `WalkerSoccerAgent.cs`:
  - Line 40: uprightRewardPerStep (0.01 → 0.03)
  - Line 47: angVelPenaltyCoef (0.001 → 0.005)
  - Lines 394-405: Height reward logic (binary → scaled)
  - Line 204: stabilizeSteps default (50 → 80)

## Next Steps

1. ✓ Rebuild Unity environment with V6 changes
2. ✓ Test debug visualization to confirm changes
3. **Resume training from step 3.15M** with new reward structure
4. Monitor metrics for 200-300k steps to assess improvement
5. Document results and adjust if needed

---

**Version History:**
- V5: Speed ramp, angular velocity penalty, sideways lean, delayed ball influence
- **V6: Height-scaled rewards, collapse penalty, 5x angular penalty, extended stabilization**
