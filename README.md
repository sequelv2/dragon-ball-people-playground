<div align="center">

# Dragon Ball: Singularity

**Advanced Physics Simulation, Autonomous Combat AI & Procedural Destruction Framework for People Playground**

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/Game-People%20Playground-orange.svg)](#)
[![Language](https://img.shields.io/badge/Language-C%23%20%7C%20HLSL-239120.svg)](#)
[![Target](https://img.shields.io/badge/.NET-Framework%204.8-512BD4.svg)](#)
[![Status](https://img.shields.io/badge/Status-Active%20Development-brightgreen.svg)](#)

[Demonstration Video](https://www.youtube.com/shorts/lHkYOEv68F0) • [Architecture](#core-architectural-systems) • [Getting Started](#getting-started) • [Building](#building-from-source) • [Roadmap](#development-roadmap)

---

</div>

## Overview

**Dragon Ball: Singularity** is an open-source physics and combat framework built on top of the Unity/.NET engine powering *People Playground*. Rather than relying purely on pre-baked animations or standard scripted triggers, this project introduces modular, real-time computational systems:

* **State-machine autonomous NPC combat AI** with aerial pathfinding, target tracking, and reactive counter-attacks.
* **Multi-body environmental destruction** featuring localized explosive fracturing and secondary ballistic debris damage.
* **Procedural energy interactions**, including beam-collision solver mechanics and high-energy spacetime distortions.
* **An extensible radial menu system** enabling dynamic in-game state switching and runtime power scaling.

This repository serves as both a fully functional standalone mod suite and an open reference codebase for modders exploring complex 2D physical simulations in People Playground.

---

## Gameplay & Systems Showcase

> 🎬 **Watch the Combat AI & Physics Engine in Action:**  
> **[YouTube Shorts — Autonomous Combat AI & Destruction Showcase](https://www.youtube.com/shorts/lHkYOEv68F0)**

Key mechanics demonstrated in the video:
1. **Dynamic Target Tracking:** Autonomous NPC acquires moving physical targets in 2D space.
2. **Aerial Manoeuvring & Evasion:** Non-scripted velocity adjustments to avoid incoming projectiles and environmental obstacles.
3. **Beam-Clash Resolvers:** Physics-based cancellation and energy propagation when two opposing blast beams intersect.
4. **Interactive Radial Wheel:** In-game UI overlay allowing instant transformation adjustments and mechanical state overrides.

---

## Core Architectural Systems

### 1. Autonomous Combat AI (`Dragon Ball Singularity/`)
* **Finite State Machine (FSM):** Implements `Idle`, `Engage`, `Evasion`, `BeamCharge`, and `MeleeClash` states.
* **Dynamic Physics Steering:** Utilizes direct 2D force impulses (`Rigidbody2D.AddForce`) combined with raycast obstacle checks rather than standard grid pathfinding.
* **Predictive Ballistics:** Calculates interception trajectories against moving rigid bodies.

### 2. Environmental Fragmentation Engine
* **Dynamic Fragmentation:** Simulates planetary/terrain degradation by breaking hit colliders into persistent multi-body debris fragments.
* **Kinetic Damage Transfer:** Debris retains linear momentum and transfers damage to entities upon secondary impact.

### 3. Spacetime Distortion Shaders & VFX
* **Procedural Rifts:** Custom screen-space UV distortion shaders simulating high-density gravity anomalies and spatial tears.
* **Particle Convergence:** Dynamic particle attractors simulating Genkidama and Ki accumulation.

---

## Repository Structure

```text
dragon-ball-people-playground/
├── Dragon Ball Singularity/      # Core mod implementation
│   ├── DBPU.csproj               # C# project configuration (.NET 4.8)
│   ├── Scripts/                  # AI, physics solvers, and UI components
│   └── Resources/                # Shaders, sound design, and sprite manifests
├── DragonballWorldBreaker/       # Standalone environmental destruction module
│   ├── Loader.cs                 # Assembly injection and bootstrap logic
│   └── README.ru.md              # Module-specific documentation
├── FILE-MANIFEST.json            # SHA-256 integrity verification manifest
├── LICENSE                       # MIT License
└── README.md                     # Project documentation

<SteamLibrary>/steamapps/common/People Playground/Mods/
