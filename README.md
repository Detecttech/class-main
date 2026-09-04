# class-main

Unity quiz-battle client with a Node.js authoritative match server.

## Elemental Arena

- Four equipped champions: fire gauntlets, a layered shield, wind blades, and an arcane staff with orbiting crystals.
- Dusk tournament stadium with open foreground sightlines, layered terraces, floating crystals, and a champion trophy gate.
- Traveling fireballs, wind crescents, ice lances, shield domes, arcane drain motes, and strike slashes with delayed impacts.
- Grounded movement rings, visible frozen characters, tighter toon shading, and responsive arena framing.

The visual layer uses the models already in this repository and procedural geometry. Match rules and server authority are unchanged.

## Verification

The client requires Unity **6000.5.7f1** with the repository's URP packages.

Run server checks from `server/`:

```sh
npm ci
npm test
npm run build
```

Run client tests from the repository root, replacing `Unity` with your editor executable:

```sh
Unity -batchmode -projectPath game-client -runTests -testPlatform EditMode -testResults /tmp/class-main-editmode.xml -logFile -
```

With graphics support available, run the existing visual verification runner:

```sh
Unity -batchmode -projectPath game-client -executeMethod ArenaDemoRunner.Run -logFile -
```

Screenshots are written to `game-client/Builds/VisualChecks/`, including separate attack travel, impact, and recovery frames. Build output is ignored by Git.

Before release, verify character equipment and portraits, attack timing and cleanup, frozen/eliminated states, and HUD readability on desktop and portrait screens. Profile a full-player match on the target Android/WebGL device; per-cast particle limits are not a substitute for device testing.
