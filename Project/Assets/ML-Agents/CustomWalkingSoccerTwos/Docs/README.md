# Walker Soccer - Setup Guide

This guide walks you through setting up the Walker Soccer project for training multi-agent soccer with Unity ML-Agents.

## Prerequisites

- **Unity Editor**: Version 2022.3+ (the project was built with this version).
- **Python**: 3.10.12 recommended (ML-Agents requires Python 3.9–3.10).
- **Git**: For cloning the ML-Agents repository.
- **Conda** (optional but recommended): For managing Python environments.

## Project Structure

You should have the `Project` folder containing:
```
Project/
├── Assets/
│   └── ML-Agents/
│       └── CustomWalkingSoccerTwos/
│           ├── Scripts/
│           ├── Prefabs/
│           ├── Scenes/
│           ├── WalkerSoccerStage1_Locomotion.yaml
│           ├── WalkerSoccerStage2_Soccer.yaml
│           └── Docs/
├── Builds/
├── Library/
├── Packages/
└── ProjectSettings/
```

## Step 1: Install Unity ML-Agents Package

Follow the official Unity ML-Agents installation instructions:  
**[Unity ML-Agents Installation Guide](https://docs.unity3d.com/Packages/com.unity.ml-agents@4.0/manual/Installation.html)**

### Quick Summary
1. Open the Unity project (`Project` folder) in Unity Editor (version: 6000.0.40f1).
2. In Unity, go to **Window > Package Manager**.
3. Click the **+** button and select **Add package from git URL**.
4. Enter: `com.unity.ml-agents`
5. Unity will install the ML-Agents package (version 4.0 or latest).

## Step 2: Install ML-Agents Python Library

The Python library is required to train agents from the command line.

### Option A: Using Conda (Recommended)

1. **Create a new conda environment** with Python 3.10.12:
   ```bash
   conda create -n mlagents python=3.10.12
   conda activate mlagents
   ```

2. **Install PyTorch** (GPU-accelerated version for faster training):
   ```bash
   pip install torch==2.8.0 torchvision==0.23.0 torchaudio==2.8.0 --index-url https://download.pytorch.org/whl/cu129
   ```
   > **Note**: This installs CUDA 12.9 compatible PyTorch. If you don't have an NVIDIA GPU, use the CPU version:
   > ```bash
   > pip install torch==2.8.0 torchvision==0.23.0 torchaudio==2.8.0 --index-url https://download.pytorch.org/whl/cpu
   > ```

3. **Clone the ML-Agents repository** (if you haven't already):
   ```bash
   git clone https://github.com/Unity-Technologies/ml-agents.git C:\Documents\GitHub\ml-agents
   ```

4. **Install ML-Agents packages in editable mode**:
   ```bash
   cd C:\Documents\GitHub\ml-agents
   pip install -e ./ml-agents-envs
   pip install -e ./ml-agents
   ```

5. **Verify installation**:
   ```bash
   mlagents-learn --help
   ```
   You should see the ML-Agents command-line options.

### Option B: Using pip (Without Conda)

1. **Create a virtual environment**:
   ```bash
   python -m venv mlagents-env
   # On Windows:
   mlagents-env\Scripts\activate
   # On macOS/Linux:
   source mlagents-env/bin/activate
   ```

2. Follow steps 2–5 from Option A above.

## Step 3: Build the Unity Environment (Optional for Training)

You can train directly in the Unity Editor or build a standalone executable for faster training.

### To Build:
1. Open the project in Unity Editor.
2. Go to **File > Build Settings**.
3. Select your platform (Windows, macOS, Linux).
4. Click **Build** and save to `Project/Builds/WalkerStage2_20_V2.exe` (or your preferred name).

### To Train with a Build:
```bash
mlagents-learn Assets/ML-Agents/CustomWalkingSoccerTwos/WalkerSoccerStage2_Soccer.yaml --env="C:\Documents\GitHub\ml-agents\Project\Builds\WalkerStage2_20_V2.exe" --num-envs=8 --run-id=MyTrainingRun
```

### To Train in Editor:
1. Open the scene: `Assets/ML-Agents/CustomWalkingSoccerTwos/Scenes/WalkerSoccerStage2.unity`
2. Run the training command (without `--env` flag):
   ```bash
   mlagents-learn Assets/ML-Agents/CustomWalkingSoccerTwos/WalkerSoccerStage2_Soccer.yaml --run-id=MyTrainingRun
   ```
3. Press **Play** in Unity Editor when prompted.

## Step 4: Start Training

### Stage 1: Locomotion Training (Optional - Pre-trained Model Available)
Stage 1 trains basic walking skills. If you have a pre-trained Stage 1 model, skip to Stage 2.

```bash
mlagents-learn Assets/ML-Agents/CustomWalkingSoccerTwos/WalkerSoccerStage1_Locomotion.yaml --run-id=WalkerStage1
```

### Stage 2: Soccer Training (Transfer Learning)
Transfer locomotion skills and learn soccer tactics:

```bash
mlagents-learn Assets/ML-Agents/CustomWalkingSoccerTwos/WalkerSoccerStage2_Soccer.yaml --initialize-from=WalkerStage1 --run-id=WalkerStage2
```

### Resume Training
To resume an interrupted training session:
```bash
mlagents-learn Assets/ML-Agents/CustomWalkingSoccerTwos/WalkerSoccerStage2_Soccer.yaml --run-id=WalkerStage2 --resume
```

## Step 5: Monitor Training

Training progress is saved in the `results` folder. You can monitor with TensorBoard:

```bash
tensorboard --logdir=results
```

Open your browser and navigate to `http://localhost:6006` to view training graphs.

## Common Issues

### Issue: `torch.load` pickle error with PyTorch 2.6+
**Solution**: Use ONNX exports for initialization instead of `.pt` checkpoints, or patch `torch_model_saver.py` to add `weights_only=False`.

### Issue: Training is very slow
**Solution**: 
- Build a standalone executable instead of training in-editor.
- Use `--num-envs=4` or higher to run multiple environments in parallel.
- Ensure GPU acceleration is enabled (check PyTorch CUDA installation).

### Issue: Mean Reward stays negative
**Solution**: 
- Check curriculum thresholds in the YAML file.
- Review reward/penalty balance in `WalkerSoccerAgent.cs`.
- Ensure anti-dive and anti-bunching parameters are tuned correctly.

## Next Steps

- Review training hyperparameters in the YAML files.
- Experiment with reward shaping in `WalkerSoccerAgent.cs`.
- Enable self-play in Stage 2 YAML once rewards stabilize.

For more details, see the [ML-Agents documentation](https://github.com/Unity-Technologies/ml-agents/blob/main/docs/Readme.md).
