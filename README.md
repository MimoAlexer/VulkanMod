# Vulkan Performance Mod

This repository now contains a source-based NeoModLoader mod for WorldBox.

What the mod does:

- Forces WorldBox's built-in `ParallelJobsUpdater` and `ParallelChunks` debug options on.
- Forces `Config.parallel_jobs_updater` and `Config.parallel_chunk_manager` on at runtime.
- Enables Unity's `Texture.allowThreadedTextureCreation`.
- Disables v-sync, unlocks the frame cap, and pushes Unity background loading priority higher.
- Raises Unity async upload settings to reduce streaming stalls.
- Caps WorldBox's `ParallelOptions.MaxDegreeOfParallelism` to a sensible worker count based on CPU cores.
- Sets Unity job workers and thread-pool minimums to reduce thread spin-up overhead.
- Uses Harmony patches to keep frame-critical parallel flags on and replace the fixed `256` render prep batch size with a CPU-scaled value.
- Logs the active graphics backend and threading mode once the mod starts.

What it does not do:

- It does not replace Unity's graphics backend from inside the running game.
- If WorldBox starts on OpenGL, Direct3D, or another backend, the mod can only report that fact.
- To run WorldBox on Vulkan, Vulkan has to be selected before the Unity player finishes booting, for example with a launch option like `-force-vulkan` when the build actually supports Vulkan.

## Install

1. Install NeoModLoader.
2. Copy this folder into `worldbox_Data/StreamingAssets/mods`.
3. Start WorldBox with experimental mode enabled.
4. If you want Vulkan and the Windows build supports it, add `-force-vulkan` to the game's launch options before starting the game.

## Building In An IDE

The included `VulkanMod.csproj` expects a local `Libraries` directory with at least:

- `Assembly-CSharp-Publicized.dll`
- `0Harmony.dll`
- `NeoModLoader.dll`
- `UnityEngine.dll`
- `UnityEngine.CoreModule.dll`

That mirrors the way most public WorldBox NML source mods are structured. A local build is optional because NML can compile source mods at runtime.
