# Walker Soccer Twos - Unity Scene Setup Guide

## Overview
This guide will help you set up a complete Walker Soccer training environment in Unity using the ML-Agents package. Walker agents with full ragdoll physics will learn to play soccer cooperatively and competitively.

---

## Prerequisites

1. **Unity ML-Agents Package** (v2.0+)
2. **Unity ML-Agents Examples Package** (for BodyPart, JointDriveController, OrientationCubeController, DirectionIndicator)
3. **Python ML-Agents Training Environment** (for training)

---

## Scene Hierarchy Structure

```
WalkerSoccerTwosArea
├── Ground (Plane)
├── Walls (4 cubes forming boundaries)
├── Goals
│   ├── BlueGoal (with "blueGoal" tag)
│   └── PurpleGoal (with "purpleGoal" tag)
├── Ball (Sphere with Rigidbody)
│   └── WalkerSoccerBallController.cs
├── BlueTeam
│   ├── BlueStriker1 (Walker Agent)
│   ├── BlueStriker2 (Walker Agent)
│   └── BlueGoalie (Walker Agent)
└── PurpleTeam
    ├── PurpleStriker1 (Walker Agent)
    ├── PurpleStriker2 (Walker Agent)
    └── PurpleGoalie (Walker Agent)
```

---

## Step 1: Create the Environment Container

1. Create empty GameObject: `WalkerSoccerTwosArea`
2. Add component: `WalkerSoccerEnvController.cs`
3. Set **Max Environment Steps**: `25000` (adjust based on training needs)

---

## Step 2: Create the Playing Field

### Ground
- Create a **Plane** (Scale: 20, 1, 13.5 works well)
- Add **BoxCollider** if not present
- Material: Soccer field texture (optional)

### Walls
Create 4 **Cubes** as walls:
- **North Wall**: Position (0, 1, 13), Scale (40, 2, 1)
- **South Wall**: Position (0, 1, -13), Scale (40, 2, 1)
- **East Wall**: Position (20, 1, 0), Scale (1, 2, 26)
- **West Wall**: Position (-20, 1, 0), Scale (1, 2, 26)

---

## Step 3: Create Goals

### Blue Goal (Left Side)
1. Create empty GameObject: `BlueGoal`
   - Position: (-18, 1, 0)
   - Add **Tag**: `blueGoal`
2. Add **BoxCollider** with:
   - **Is Trigger**: ✓ (checked)
   - Size: (2, 2, 8)
3. Add visual posts/net (optional cubes as children)

### Purple Goal (Right Side)
1. Create empty GameObject: `PurpleGoal`
   - Position: (18, 1, 0)
   - Add **Tag**: `purpleGoal`
2. Add **BoxCollider** with:
   - **Is Trigger**: ✓ (checked)
   - Size: (2, 2, 8)
3. Add visual posts/net (optional cubes as children)

---

## Step 4: Create the Soccer Ball

1. Create **Sphere**: `Ball`
   - Position: (0, 0.5, 0)
   - Scale: (0.5, 0.5, 0.5)
   - Add **Tag**: `ball`

2. Add **Rigidbody**:
   - Mass: 0.5
   - Drag: 0.1
   - Angular Drag: 0.05
   - **Use Gravity**: ✓

3. Add **SphereCollider**:
   - Radius: 0.5
   - Physics Material: Bouncy (optional)

4. Add Script: `WalkerSoccerBallController.cs`
   - **Area**: Drag `WalkerSoccerTwosArea` GameObject
   - **Purple Goal Tag**: `purpleGoal`
   - **Blue Goal Tag**: `blueGoal`

---

## Step 5: Create Walker Agents

You need to create **6 walker agents** (3 per team). Each walker requires:

### Base Walker Ragdoll Structure

Use Unity's **Ragdoll Wizard** or create manually:
```
Walker
├── Hips (Rigidbody + Capsule Collider)
│   ├── Spine (Rigidbody + Capsule Collider + ConfigurableJoint)
│   │   └── Chest (Rigidbody + Capsule Collider + ConfigurableJoint)
│   │       ├── Head (Rigidbody + Sphere Collider + ConfigurableJoint)
│   │       ├── ArmL (Rigidbody + Capsule Collider + ConfigurableJoint)
│   │       │   └── ForearmL (Rigidbody + Capsule Collider + ConfigurableJoint)
│   │       │       └── HandL (Rigidbody + Capsule Collider + ConfigurableJoint)
│   │       └── ArmR (similar structure)
│   ├── ThighL (Rigidbody + Capsule Collider + ConfigurableJoint)
│   │   └── ShinL (Rigidbody + Capsule Collider + ConfigurableJoint)
│   │       └── FootL (Rigidbody + Box Collider + ConfigurableJoint)
│   └── ThighR (similar structure)
├── OrientationCube (for observations)
└── DirectionIndicator (visual helper)
```

### Walker Agent Components

For each walker agent GameObject:

#### 1. Add Scripts
- `WalkerSoccerAgent.cs`
- `JointDriveController.cs` (from ML-Agents Examples)
- `BehaviorParameters` (automatically added)
- `DecisionRequester` (automatically added)

#### 2. Configure WalkerSoccerAgent

**Soccer Settings:**
- **Position**: Choose `Striker`, `Goalie`, or `Generic`
- **Ball**: Drag the Ball GameObject
- **Target**: Create an empty GameObject as a target (optional, can be the opposite goal)

**Walk Speed:**
- **Target Walking Speed**: 7-10 (adjust as needed)
- **Randomize Walk Speed Each Episode**: ✓ (for generalization)

**Body Parts:** Drag and assign all 16 body part transforms:
- Hips, Chest, Spine, Head
- ThighL, ShinL, FootL
- ThighR, ShinR, FootR
- ArmL, ForearmL, HandL
- ArmR, ForearmR, HandR

#### 3. Configure BehaviorParameters

**Blue Team Agents:**
- **Behavior Name**: `WalkerSoccer`
- **Vector Observation Space Size**: Calculate based on observations (~150-200)
- **Actions**:
  - **Continuous Actions**: 39 (joint rotations + strengths)
- **Team ID**: `0` (Blue)
- **Behavior Type**: `Default` (for training) or `Heuristic Only` (for testing)

**Purple Team Agents:**
- Same as Blue, but **Team ID**: `1` (Purple)

#### 4. Configure DecisionRequester
- **Decision Period**: `5` (decisions every 5 fixed updates)
- **Take Actions Between Decisions**: ✓

#### 5. Add OrientationCube (Child of Walker)
- Create empty GameObject as child: `OrientationCube`
- Add: `OrientationCubeController.cs` (from ML-Agents Examples)
- Add visual cube (optional, for debugging)

#### 6. Add DirectionIndicator (Child of Walker)
- Create empty GameObject as child: `DirectionIndicator`
- Add: `DirectionIndicator.cs` (from ML-Agents Examples)
- Add arrow mesh/sprite (optional, for debugging)

#### 7. Configure JointDriveController
- **Max Joint Force Limit**: 300-500 (adjust for responsiveness)
- **Max Joint Spring**: 100-200

#### 8. Ground Contact Sensors
For feet and potentially hands, add:
- Empty GameObject as child: `FootLGroundContact`
- Add: `GroundContact.cs` (from ML-Agents Examples)
- Add small **SphereCollider** (trigger) at the foot bottom

---

## Step 6: Configure WalkerSoccerEnvController

Back on the `WalkerSoccerTwosArea` GameObject:

1. **Ball**: Drag the Ball GameObject
2. **Agents List**: Set size to 6
   - Add all 6 walker agents to the list
3. **Max Environment Steps**: 25000

---

## Step 7: Create SoccerSettings

1. Create empty GameObject: `SoccerSettings` (in scene root)
2. Add component: `SoccerSettings.cs`
3. Configure:
   - **Purple Material**: Assign material for purple team
   - **Blue Material**: Assign material for blue team
   - **Randomize Players Team For Training**: ✓ (optional)
   - **Agent Run Speed**: 10-15

---

## Step 8: Position Agents

### Blue Team (Left Side)
- **BlueStriker1**: (-10, 0.5, 3)
- **BlueStriker2**: (-10, 0.5, -3)
- **BlueGoalie**: (-16, 0.5, 0)

### Purple Team (Right Side)
- **PurpleStriker1**: (10, 0.5, 3)
- **PurpleStriker2**: (10, 0.5, -3)
- **PurpleGoalie**: (16, 0.5, 0)

---

## Step 9: Configure Physics

### Time Settings (Edit > Project Settings > Time)
- **Fixed Timestep**: 0.02 (50 Hz)
- **Maximum Allowed Timestep**: 0.1

### Physics Settings (Edit > Project Settings > Physics)
- **Default Solver Iterations**: 10
- **Default Solver Velocity Iterations**: 8
- **Bounce Threshold**: 2
- **Default Contact Offset**: 0.01

### Collision Matrix
Ensure:
- Agents can collide with: Ground, Walls, Ball, Other Agents
- Ball can collide with: Everything
- Goal triggers don't block agents or ball

---

## Step 10: Materials and Visuals (Optional)

### Team Materials
Create materials to distinguish teams:
- **Blue Material**: Blue color/texture
- **Purple Material**: Purple color/texture

Apply to agent bodies (chest/hips) for easy identification.

### Arena Decorations
- Add field lines
- Add goal nets
- Add team color indicators
- Add lighting

---

## Testing the Scene

### Heuristic Mode Testing
1. Set one agent's **Behavior Type** to `Heuristic Only`
2. Press Play
3. Use WASD keys to control the agent
4. Verify movement, ball physics, and collision detection

### Training Mode
1. Ensure all agents have **Behavior Type**: `Default`
2. Build the scene
3. Run Python training command (see training configuration)

---

## Common Issues and Solutions

### Issue: Agents fall through floor
**Solution:** 
- Increase Ground collider thickness
- Check physics layer collision matrix
- Verify Rigidbody settings (not kinematic)

### Issue: Agents move erratically
**Solution:**
- Reduce `Max Joint Force Limit` in JointDriveController
- Increase `Decision Period` in DecisionRequester
- Check ConfigurableJoint drive settings

### Issue: Ball goes too fast/slow
**Solution:**
- Adjust Rigidbody mass and drag
- Modify `k_KickPower` in WalkerSoccerAgent.cs
- Add physics material with appropriate bounciness

### Issue: Training is unstable
**Solution:**
- Ensure proper observation normalization
- Balance reward scales
- Increase decision period
- Reduce max environment steps initially

### Issue: Agents don't pursue ball
**Solution:**
- Verify ball reference is assigned in WalkerSoccerAgent
- Check target is properly set
- Increase ball touch reward in curriculum

---

## Performance Optimization

### For Training
- **Reduce visual quality**: Use simple primitives instead of detailed meshes
- **Disable shadows**: Uncheck cast/receive shadows on all objects
- **Time Scale**: Can increase up to 20x during training
- **Multiple environments**: Duplicate the entire area for parallel training

### Recommended Training Setup
- **4-8 parallel environments** per scene
- **Multiple scenes** for distributed training
- **Checkpoint frequently** to avoid losing progress

---

## Next Steps

1. ✅ Scene setup complete
2. ➡️ Create training configuration (YAML file)
3. ➡️ Start training with ML-Agents
4. ➡️ Monitor TensorBoard for progress
5. ➡️ Test trained models in inference mode

See `TRAINING_CONFIG.md` for ML-Agents training setup.

---

## Additional Resources

- [ML-Agents Documentation](https://github.com/Unity-Technologies/ml-agents/blob/main/docs/Readme.md)
- [Walker Example](https://github.com/Unity-Technologies/ml-agents/blob/main/docs/Learning-Environment-Examples.md#walker)
- [Soccer Twos Example](https://github.com/Unity-Technologies/ml-agents/blob/main/docs/Learning-Environment-Examples.md#soccer-twos)
- [Training Configuration](https://github.com/Unity-Technologies/ml-agents/blob/main/docs/Training-Configuration-File.md)
