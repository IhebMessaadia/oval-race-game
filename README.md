# Arcade Oval Race Game

A 3D arcade game made in **Unity 6** with **C#**. You drive clockwise around a two-lane oval while NPC cars come at you counter-clockwise. Switch lanes to dodge them: every car you dodge scores a point, and a head-on crash ends the game.

## ▶️ Play it

Download the Windows build from the [**latest release**](../../releases/latest), unzip it and run `RaceGame.exe`. A gameplay video is attached to the release too.

| Key | Action |
|---|---|
| **Space** | Switch between the inner and outer lane |

## How it works

- **`OvalTrack.cs`**: the track geometry, two parallel lanes built from straights and curves, and the position along each lane
- **`PlayerCar.cs`**: moves the player clockwise and handles lane switching
- **`NPCCar.cs`**: drives the oncoming cars counter-clockwise in either lane
- **`GameManager.cs`**: detects collisions and dodges by tracking each NPC's angle relative to the player, keeps score, and shows the game-over screen with a restart button
- **`Editor/SceneBuilder.cs`**: builds the entire scene from code (lighting, ground, track, lane markings, cars and UI) and makes the Windows build, so the project is reproducible without hand-editing the scene

## Open the project

1. Install **Unity 6000.4.6f1** (or a compatible Unity 6 version) through Unity Hub.
2. Clone the repository and choose **Add project from disk** in Unity Hub.
3. Open `Assets/RaceGame.unity` and press **Play**.

To make a Windows build from the command line:

```bash
Unity -batchmode -quit -projectPath . -executeMethod SceneBuilder.BuildGame
```

The build goes to `Builds/RaceGame.exe`.

## Author

**Iheb Messaadia**: [GitHub](https://github.com/IhebMessaadia)
