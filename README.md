# Crimson Nightfall

**A first-person horror escape experience built in Unity.**

Survive a stalking entity inside a looping nightmare. Gather what you need to escape, stay out of sight, and uncover the secrets that unlock something far worse.

---

## About

**Crimson Nightfall** is a single-player stealth horror game focused on tension, sound, and careful movement. You wake in a mansion trapped in a never-ending cycle — an aggressive entity patrols the halls, and the only way out is to collect key items, solve environmental obstacles, and reach the exit without being caught.

This project is a complete personal game prototype: systems, level design, AI, UI, and game modes were built end-to-end as a first full game creation project.

> **Content warning:** Flashing lights, loud sudden audio, and horror imagery. Photosensitive players and players sensitive to frightening content should use discretion.

---

## Features

- **Stealth survival** — Crouch, hide in closets, manage stamina, and use darkness to your advantage
- **Hostile AI entity** — Line-of-sight detection, flashlight exposure, chase behavior, and audio presence
- **Exploration & puzzles** — Keys, doors, elevators, fuseboxes, levers, laser barriers, and item collection
- **Two game modes**
  - **Normal** — Balanced stamina, standard entity aggression, stable physics
  - **Chaotic** — Harder stamina drain, wider entity FOV, physics chaos as the entity collides with the world *(unlock by discovering the secret insight in Normal Mode)*
- **Multiple endings** — Escape clean, uncover the truth, or survive the nightmare of Chaotic Mode
- **Easter eggs** — Hidden unlocks and optional cosmetics for players who dig deeper
- **In-game systems** — Main menu, pause menu, settings, win/lose flows, and progression unlocks

---

## Gameplay Loop

1. Explore the mansion under pressure from the entity  
2. Search high and low for keys and required items *(crouch often — loot hides low)*  
3. Open new areas through doors, elevators, and power systems  
4. Reach the exit and escape… or learn what the nightmare is really hiding  

**Survival tips**
- Listen for footsteps and door sounds to track the entity  
- Use **CTRL** to crouch out of FOV and check lower surfaces  
- Closets are strong — but don’t linger forever  
- Sprint with **SHIFT**, but stamina is limited  

---

## Controls

| Action | Default |
| --- | --- |
| Move | `WASD` |
| Look | Mouse |
| Sprint | `Left Shift` |
| Crouch | `Left Ctrl` |
| Interact / Pickup | In-world prompts (look + interact) |
| Flashlight | Flashlight controls (in-game) |
| Pause | Escape / pause menu |

Toggle options for crouch and sprint are available in settings.

---

## Tech Stack

| | |
| --- | --- |
| **Engine** | Unity 6 (`6000.3.15f1`) |
| **Language** | C# |
| **Perspective** | First-person |
| **AI** | NavMesh patrol / chase with vision & detection rules |
| **Scenes** | `MainMenu`, `Normal_Mode`, `Chaos_Mode` |

Core systems include player movement, entity AI, inventory/collection, door & elevator logic, environmental hazards, game mode progression, and UI.

---

## Getting Started (Unity)

### Requirements
- [Unity Hub](https://unity.com/download)
- Unity Editor **6000.3.15f1** (or compatible Unity 6 version)
- Windows recommended for local playtesting

### Open the project
1. Clone the repository  
   ```bash
   git clone https://github.com/TheMasterSlayer/CrimsonNightfall.git
   ```
2. Open the folder in **Unity Hub** → Add → select the project root  
3. Let Unity import assets and regenerate project files  
4. Open `Assets/Project/Scenes/MainMenu/MainMenu.unity`  
5. Press **Play**

> Tip: Start from the Main Menu scene so mode selection and progression unlocks behave correctly.

---

## Project Structure

```text
Assets/
├── Project/
│   ├── Scenes/          # Main menu & authored scenes
│   ├── Scripts/
│   │   ├── AI/          # Entity behavior & SCP systems
│   │   ├── Core/        # GameManager, doors, elevators, hazards
│   │   ├── Items/       # Pickups, inventory, clues
│   │   ├── Player/      # Movement, flashlight, hide
│   │   └── UI/          # Menus, settings, progression
│   └── Editor/          # Scene setup & tooling
├── Scenes/              # Normal_Mode / Chaos_Mode gameplay scenes
└── Resources/           # Credits, disclaimer, runtime text
```

---

## Credits

**Created by** [TheMasterSlayer](https://github.com/TheMasterSlayer)  
**Level design by** TheMasterSlayer  

Third-party models, SFX, and packs are credited in-game and in `Assets/Resources/Credits.txt` (Sketchfab, Unity Asset Store, Freesound, and more).

---

## Status

Finished personal project — playable end-to-end with Normal Mode, unlockable Chaotic Mode, endings, and easter eggs.

If you find bugs or edge cases (especially in Chaotic Mode’s physics chaos), that’s expected territory for a challenge mode designed to break the comfort of Normal.

---

## License

Asset licenses belong to their respective creators.  
Source code in this repository is provided for portfolio / educational viewing unless otherwise noted.
