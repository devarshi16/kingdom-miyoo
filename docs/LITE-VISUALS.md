# Lite visuals

`KNL_VISUALS=lite` reduces decoration. An unset value or `KNL_VISUALS=full` uses the existing rendering and particle dispatch. This setting does not alter simulation timestep, collision logic, AI, upgrades, saving, canvas UI, sprite animation, lighting, or resolution.

The lite water pass fills the same waterline-to-bottom region with an opaque, tinted depth gradient. It retains the original water tint and current ambient sky color, so weather and day/night lighting still affect it. Ripples and mirrored scenery/characters are removed. Backdrop gradients and point lighting remain enabled. Screenshot inspection found a complete, continuous water region with no blank strips; visual preference and other times of day still need device gameplay review.

Particle Playground remains initialized and callable. Lite bypasses its manager's expensive `LateUpdate` particle calculations only when there are no subscribed particle callbacks, serialized particle events, or consumers that track particles. The plugin clock still advances. A newly attached consumer restores ordinary particle dispatch on the next frame. Other lifecycle methods and emitter API calls remain intact. The compatibility renderer currently does not draw `ParticleSystemRenderer`, so this primarily avoids computing particles that were already invisible; it does not remove existing rendered fire sprites, selection UI, tutorial text, or weather gameplay controllers.

The original owned Assembly-CSharp.dll examined has SHA-256 `89e7bdc9b18ccb4a6d37e93c09245c58d67506d92d1fdc26206ef096c73150e3`. A full scan of method bodies outside the ParticlePlayground namespace found game callers only emitting/configuring particles: Boat, ChristmasSleigh, Farmhouse, Flame, Payable, Precipitation, Stars, Steed, TutorialTrail, Wall, Crown, InterfaceOverlay, and PayableTeleporter. None read computed particle output for gameplay or subscribed to particle events. Playground preset/recorder helpers also reference the plugin; they are not treated as gameplay evidence. The manager CIL confirms `LateUpdate` prepares and simulates particles, dispatches events, and copies results into the Unity particle system. This evidence supports the scoped bypass, not disabling arbitrary scripts by names such as “fire” or “weather.”

Validation on the desktop compatibility runtime:

- Nine visual regressions cover full-mode dispatch, initialization, lite clock advancement, newly subscribed manager callbacks, serialized events, local tracking and callback consumers, return to full mode, and opaque depth fill.
- All 34 existing runtime regressions pass.
- Full and lite modes each completed a fresh 2,400-frame starting sequence with identical scripted movement, zero logged exceptions, 31 audio starts and no missing clips. No seed-for-seed comparison to the original Unity runtime is claimed.
- A separate matching 1,000-frame capture comparison changed pixels only within the water region, rows 344–479 of the 640×480 screenshot. Every pixel above that region was identical. Lite and full captures were inspected visually.
- Desktop profiling showed lower particle and mesh work; these numbers do not establish Miyoo FPS. Device measurements must use the same scene/save/route, real elapsed frame time, and profiling disabled.

Local inspection captures contain owned game graphics and must not be packaged in public runtime releases.
