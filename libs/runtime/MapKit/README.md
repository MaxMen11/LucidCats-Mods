# MapKit

MapKit has been compiled and checked against the supplied game and HaniUtils room-generator assemblies, but this revision has not been validated in a running game or multiplayer session.

MapKit is the shared runtime for custom Lucid Cats levels. One installed `MapKit.dll` owns registration and ordering for MapKit levels, multiplayer compatibility, game measurements, generation hooks, and room-template cleanup.

Add it to a mod project:

```xml
<RuntimeLibs>MapKit</RuntimeLibs>
```

The build adds the compile reference and a hard BepInEx dependency. Players install `MapKit.zip` once alongside map mods that use it.

## Small complete map

```csharp
using System.Linq;
using HaniUtils.RoomGen;
using MapKit;
using UnityEngine;

Maps.Register(ModMetadata.Guid, ModMetadata.Version, map =>
{
	MapContext context = map.Context;
	GeneratedRoom hall = Maps.Room("Custom Hall", 10f, 3.5f, 14f)
		.Door(0f, 7f)
		.Door(0f, -7f)
		.ColorOf(new Color(0.6f, 0.3f, 0.9f))
		.Light(0f, 3f, 0f, intensity: 1.5f)
		.Spawn(0f, 0f)
		.Start()
		.Exit()
		.Build();

	RoomPoolConfig pool = Maps.Pool(context.DoorSource, hall);
	LevelConfig level = Maps.Level(
		"Custom Wing",
		pool,
		Maps.Environment(
			fogColor: new Color(0.1f, 0.1f, 0.2f),
			fogDensity: 0.02f));

	map.AddLevel("main", level);
});
```

`Register` hashes the provider DLL with SHA-256, so map authors do not have to maintain a content hash. The overload taking an explicit SHA-256 string exists for unusual providers whose relevant content lives outside their DLL.

A provider may call `map.AddLevel` more than once. The final stable IDs are `<provider-id>/<level-id>`, and MapKit appends them in ordinal ID order on every peer. Display names do not need to be unique. If a measured asset or required dependency is unavailable, call `map.Unavailable("reason")`; MapKit logs the reason and excludes that provider without disrupting others.

## Context and game adapters

The build callback receives a `MapBuilder`. Its `Context` exposes the existing registry and conventions measured from the running game rather than hardcoded guesses:

| Member | Meaning |
|---|---|
| `ExistingLevels` | Read-only levels already in the registry, including non-MapKit custom levels and excluding MapKit's own levels. |
| `ExistingRooms` | Distinct room templates found in those existing levels. |
| `DoorSource` | A suitable existing level from which `Pool` can borrow connected, sealed, and exit door prefabs. |
| `Metrics.DoorwayWidth/Height` | Opening dimensions measured from a game door prefab. Check `Metrics.HasDoorwaySize` before using them directly. |
| `Metrics.ExitDoorBounds` | Measured exit-door render bounds when available. |
| `Layers.Solid/RoomBounds` | Layers discovered from vanilla room templates. |
| `Materials.Create(color, emission)` | A material derived from a vanilla material or the active render pipeline. |
| `Prefabs.CloneDecoration(source, parent)` | Local clone with network, room, doorway, loot-zone, rigidbody, joint, and prop-spawner behavior removed. |

Core factories:

| Call | Result |
|---|---|
| `Maps.Level(name, pool, environment, cullRoomLights)` | `LevelConfig` tracked as part of this provider build. |
| `Maps.Pool(rooms)` | Room pool without borrowed door dressing. |
| `Maps.Pool(context.DoorSource, rooms)` | Room pool with vanilla connected, sealed, and exit prefabs. |
| `Maps.Remix(levels)` | Deduplicated blend of existing room pools and their first available dressing prefabs. |
| `Maps.Environment(...)` | Fog, ambient-light, and skybox settings. |
| `Maps.EnvironmentFrom(level)` | Copy an existing level's environment into an independently editable settings object. |

## Room builder

`Maps.Room(name, width, height, depth)` starts with a box. `Maps.Room(name, height)` starts empty for arbitrary layouts.

| Call | Meaning |
|---|---|
| `.Floor(x, z, width, depth, y, material)` | Floor patch. Ceiling mirrors each floor unless `NoCeiling()` is used. |
| `.Wall(x1, z1, x2, z2, bottom, material)` | Wall segment at any horizontal angle. |
| `.Slab(x, y, z, width, height, depth, material, collider)` | Raw box for platforms, stairs, partitions, and other geometry. |
| `.Mesh(mesh, position, rotation, material, collider)` | Arbitrary map-owned mesh with an optional solid or trigger collider. |
| `.Door(x, z, width, height, bottom)` | Convenience doorway that must be within 25 cm of a wall. Omitted dimensions use measured game values; validation requires explicit dimensions when measurement is unavailable. |
| `.Light(...)` | Point, spot, or directional light with explicit intensity, range, and shadow policy. |
| `.Decor(prefab, x, y, z, yaw)` | Clone static decoration after removing network, room, doorway, loot-zone, rigidbody, joint, and prop-spawner behavior. |
| `.Spawn(x, z, y)` | Add a player spawn point. Repeatable; a deterministic point is selected per placed room. |
| `.ValuableZone(x, y, z, width, height, depth, weight)` | Add a real game loot zone. |
| `.Start()` / `.Exit()` / `.SpawnEligible()` | Start, exit, and player-spawn rules. Start defaults to **false**. Calling `Spawn` enables spawn eligibility. |
| `.Weight()` / `.Min()` / `.Max()` | Generator selection weight and count limits. |
| `.Bounds(bounds)` | Override automatically computed full 3D bounds. |
| `.Edit(root => ...)` | Edit the emitted template root or attach arbitrary Unity behavior. |
| `.EditRoom(room => ...)` | Configure the real `GeneratedRoom`. |
| `.Component<T>(configure)` | Add and configure a map-owned component on each cloned room. |
| `.Validate()` | Return every definition error without emitting Unity objects. |
| `.Build()` | Validate, then emit the tracked template or throw one complete diagnostic. |

All floor, wall, slab, and mesh geometry contributes to automatic X/Y/Z bounds, including geometry below the room origin.

### Explicit doors

Nearest-wall doors are concise for boxes. Advanced geometry should bind openings to a specific wall:

```csharp
RoomBuilder room = Maps.Room("Angled Room", 3.2f)
	.Floor(0f, 0f, 8f, 8f)
	.Start()
	.Spawn(0f, 0f);

RoomBuilder.WallHandle north = room.AddWall(-4f, 4f, 4f, 4f);
north.Door(at: 0f, width: 1.6f);

RoomBuilder.WallHandle raised = room.AddWall(4f, -4f, 4f, 4f, bottom: 2f);
raised.Door(at: 0f, width: 1.6f, height: 2.4f, bottom: 2f, outward: Vector3.right)
	.Exit();
```

`at` is a signed distance from the wall midpoint. Explicit doors validate wall containment, vertical containment, spacing, overlap, and direction.

## Generation lifecycle and deterministic work

Register map-owned callbacks without writing raw Harmony patches:

```csharp
map.On(GenerationPhase.BeforeGeneration, context => ResetState(context));
map.On(GenerationPhase.AfterRoomsPlaced, context => DressRooms(context));
map.On(GenerationPhase.BeforeNavMesh, context => PrepareNavMesh(context));
map.On(GenerationPhase.AfterGeneration, context => FinishMap(context));
map.On(GenerationPhase.LevelCleared, context => ClearState(context));
```

`GenerationContext` exposes the stable map ID, selected level, original seed, generator, placed rooms, and current connected/open doors. Use `context.Rng("purpose")` or `context.RngForRoom(room, "purpose")`; both derive deterministic `System.Random` instances from the level seed, stable map ID, room identity/index, and named salt.

A failing lifecycle callback is logged with its provider ID and does not prevent unrelated providers from running. A failing build disables only that provider.

## Multiplayer compatibility

Lucid Cats broadcasts a selected level by array index, so peers must have the same level list in the same order. MapKit compares an ordered manifest containing:

- the MapKit API version;
- the existing level names and order;
- MapKit provider IDs, versions, and DLL hashes;
- stable MapKit level IDs.

MapKit preserves every level it does not own, including levels added by mods that do not use MapKit. Existing levels stay in their relative order; MapKit levels form one ID-sorted group after them. If another mod changes the registry later, MapKit incorporates those levels instead of restoring an old snapshot.

When more than one player is connected, MapKit levels stay out of rotation until every MapKit peer reports an identical manifest. The host repeats this check immediately before level selection. A registry reshuffle occurs only when the effective level array actually changes: initial MapKit insertion, a compatibility-state change, or another mod changing the registry around MapKit's entries. Ordinary selections do not reshuffle or reset the vanilla bag.

A non-MapKit map remains responsible for its own version/content check. MapKit can preserve and order that map, but it cannot identify another plugin's version from a `LevelConfig` alone.

`Maps.Manifest`, `Maps.CustomMapsEnabled`, and `Maps.DumpManifest()` are available for diagnostics. `Diagnostics.LogManifest` in the MapKit BepInEx config logs rebuilt manifests.

## Cleanup

MapKit owns the levels, pools, environments, template roots, and generated materials created through its build scope. It destroys old builds when the game recreates its registry and releases network handlers, templates, materials, hooks, and session state when MapKit shuts down.
