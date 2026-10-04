using System;
using System.Collections.Generic;
using System.Linq;
using HaniUtils.Gameplay;
using HaniUtils.RoomGen;
using UnityEngine;

namespace MapKit
{
	public sealed class RoomBuilder
	{
		private const float WallThickness = 0.1f;
		private readonly MapBuildScope scope;
		private readonly string roomName;
		private readonly float wallHeight;
		private readonly List<FloorSpec> floors = new List<FloorSpec>();
		private readonly List<WallSpec> walls = new List<WallSpec>();
		private readonly List<BoxSpec> boxes = new List<BoxSpec>();
		private readonly List<MeshSpec> meshes = new List<MeshSpec>();
		private readonly List<DoorSpec> doors = new List<DoorSpec>();
		private readonly List<LightSpec> lights = new List<LightSpec>();
		private readonly List<PropSpec> props = new List<PropSpec>();
		private readonly List<SpawnSpec> spawns = new List<SpawnSpec>();
		private readonly List<ValuableSpec> valuables = new List<ValuableSpec>();
		private readonly List<Action<GameObject>> rootEdits = new List<Action<GameObject>>();
		private readonly List<Action<GeneratedRoom>> roomEdits = new List<Action<GeneratedRoom>>();
		private Color color = Color.white;
		private Color glow = Color.black;
		private bool ceiling = true;
		private bool canExit;
		private bool canStart;
		private bool canSpawn;
		private bool built;
		private float weight = 1f;
		private int minCount;
		private int maxCount = -1;
		private Bounds? manualBounds;

		internal RoomBuilder(MapBuildScope scope, string name, float height)
		{
			this.scope = scope;
			roomName = name;
			wallHeight = height;
		}

		public RoomBuilder Floor(float x, float z, float width, float depth, float y = 0f, Material material = null)
		{
			EnsureMutable();
			floors.Add(new FloorSpec(new Vector3(x, y - 0.05f, z), new Vector3(width, 0.1f, depth), material));
			return this;
		}

		public RoomBuilder Wall(float x1, float z1, float x2, float z2, float bottom = 0f, Material material = null)
		{
			AddWall(x1, z1, x2, z2, bottom, material);
			return this;
		}

		public WallHandle AddWall(float x1, float z1, float x2, float z2, float bottom = 0f, Material material = null)
		{
			EnsureMutable();
			WallSpec wall = new WallSpec(x1, z1, x2, z2, bottom, material);
			walls.Add(wall);
			return new WallHandle(this, wall);
		}

		public RoomBuilder Slab(float x, float y, float z, float width, float height, float depth, Material material = null, ColliderMode collider = ColliderMode.Solid)
		{
			EnsureMutable();
			boxes.Add(new BoxSpec(new Vector3(x, y, z), new Vector3(width, height, depth), Quaternion.identity, material, collider));
			return this;
		}

		public RoomBuilder Mesh(Mesh mesh, Vector3 position, Quaternion rotation, Material material, ColliderMode collider = ColliderMode.Solid)
		{
			EnsureMutable();
			meshes.Add(new MeshSpec(mesh, position, rotation, material, collider));
			return this;
		}

		public RoomBuilder Door(float x, float z, float width = 0f, float height = 0f, float bottom = 0f)
		{
			EnsureMutable();
			doors.Add(new DoorSpec(null, x, z, 0f, width, height, bottom, null));
			return this;
		}

		public RoomBuilder ColorOf(Color value)
		{
			EnsureMutable();
			color = value;
			return this;
		}

		public RoomBuilder Glow(Color value)
		{
			EnsureMutable();
			glow = value;
			return this;
		}

		public RoomBuilder NoCeiling()
		{
			EnsureMutable();
			ceiling = false;
			return this;
		}

		public RoomBuilder Light(float x, float y, float z, Color? tint = null, float intensity = 1f, float range = 0f, LightType type = LightType.Point, LightShadows shadows = LightShadows.None)
		{
			EnsureMutable();
			lights.Add(new LightSpec(new Vector3(x, y, z), tint, intensity, range, type, shadows));
			return this;
		}

		public RoomBuilder Decor(GameObject prefab, float x, float y, float z, float yaw = 0f)
		{
			EnsureMutable();
			if (prefab == null)
			{
				throw new ArgumentNullException(nameof(prefab));
			}
			props.Add(new PropSpec(prefab, new Vector3(x, y, z), Quaternion.Euler(0f, yaw, 0f)));
			return this;
		}

		public RoomBuilder Spawn(float x, float z, float y = 0.1f)
		{
			EnsureMutable();
			spawns.Add(new SpawnSpec(new Vector3(x, y, z)));
			canSpawn = true;
			return this;
		}

		public RoomBuilder ValuableZone(float x, float y, float z, float width, float height, float depth, float weight = 1f)
		{
			EnsureMutable();
			valuables.Add(new ValuableSpec(new Vector3(x, y, z), new Vector3(width, height, depth), weight));
			return this;
		}

		public RoomBuilder Exit(bool value = true)
		{
			EnsureMutable();
			canExit = value;
			return this;
		}

		public RoomBuilder Start(bool value = true)
		{
			EnsureMutable();
			canStart = value;
			return this;
		}

		public RoomBuilder SpawnEligible(bool value = true)
		{
			EnsureMutable();
			canSpawn = value;
			return this;
		}

		public RoomBuilder Weight(float value)
		{
			EnsureMutable();
			weight = value;
			return this;
		}

		public RoomBuilder Min(int value)
		{
			EnsureMutable();
			minCount = value;
			return this;
		}

		public RoomBuilder Max(int value)
		{
			EnsureMutable();
			maxCount = value;
			return this;
		}

		public RoomBuilder Bounds(Bounds value)
		{
			EnsureMutable();
			manualBounds = value;
			return this;
		}

		public RoomBuilder Edit(Action<GameObject> edit)
		{
			EnsureMutable();
			rootEdits.Add(edit ?? throw new ArgumentNullException(nameof(edit)));
			return this;
		}

		public RoomBuilder EditRoom(Action<GeneratedRoom> edit)
		{
			EnsureMutable();
			roomEdits.Add(edit ?? throw new ArgumentNullException(nameof(edit)));
			return this;
		}

		public RoomBuilder Component<T>(Action<T> configure = null) where T : Component
		{
			EnsureMutable();
			rootEdits.Add(root =>
			{
				T component = root.AddComponent<T>();
				configure?.Invoke(component);
			});
			return this;
		}

		private void EnsureMutable()
		{
			if (built)
			{
				throw new InvalidOperationException("Room '" + roomName + "' has already been built.");
			}
		}

		public IReadOnlyList<string> Validate()
		{
			List<string> errors = new List<string>();
			if (string.IsNullOrWhiteSpace(roomName)) errors.Add("A room name is required.");
			if (!Positive(wallHeight)) errors.Add("Wall height must be finite and positive.");
			if (!Finite(weight) || weight < 0f) errors.Add("Spawn weight must be finite and nonnegative.");
			if (minCount < 0) errors.Add("Minimum count cannot be negative.");
			if (maxCount < -1) errors.Add("Maximum count must be -1 or greater.");
			if (maxCount >= 0 && minCount > maxCount) errors.Add("Minimum count cannot exceed maximum count.");
			foreach (FloorSpec floor in floors)
			{
				if (!Positive(floor.Size.x) || !Positive(floor.Size.z) || !Finite(floor.Center.y)) errors.Add("Floor dimensions and elevation must be finite and positive.");
			}
			foreach (BoxSpec box in boxes)
			{
				if (!Positive(box.Size.x) || !Positive(box.Size.y) || !Positive(box.Size.z) || !Finite(box.Center)) errors.Add("Slab dimensions and position must be finite and positive.");
			}
			foreach (MeshSpec mesh in meshes)
			{
				if (mesh.Mesh == null || !Finite(mesh.Position)) errors.Add("Mesh geometry needs a mesh and a finite position.");
			}
			foreach (LightSpec light in lights)
			{
				if (!Finite(light.Position) || !Finite(light.Intensity) || light.Intensity < 0f || !Finite(light.Range) || light.Range < 0f) errors.Add("Lights need finite positions, nonnegative intensity, and nonnegative range.");
			}
			foreach (SpawnSpec spawn in spawns)
			{
				if (!Finite(spawn.Position)) errors.Add("Spawn points need finite positions.");
			}
			foreach (WallSpec wall in walls)
			{
				if (!Finite(wall.X1) || !Finite(wall.Z1) || !Finite(wall.X2) || !Finite(wall.Z2) || !Finite(wall.Bottom) || wall.Length < 0.01f) errors.Add("Every wall must have finite coordinates and nonzero length.");
			}
			foreach (DoorSpec door in doors.Where(door => door.Wall == null))
			{
				if (!Finite(door.X) || !Finite(door.Z))
				{
					errors.Add("Door positions must be finite.");
					continue;
				}
				WallSpec nearest = NearestWall(door.X, door.Z, out float distance);
				if (nearest == null) errors.Add("A free door has no wall to cut.");
				else if (distance > 0.25f) errors.Add($"Door at ({door.X:0.##}, {door.Z:0.##}) is {distance:0.##}m from its nearest wall; bind it explicitly with AddWall(...).Door(...).");
			}
			foreach (WallSpec wall in walls)
			{
				ValidateDoors(wall, errors);
			}
			if (doors.Count == 0) errors.Add("A generated room needs at least one doorway.");
			if (canExit && doors.Count == 0) errors.Add("An exit-eligible room needs a doorway.");
			if (canSpawn && spawns.Count == 0) errors.Add("A spawn-eligible room needs at least one spawn point.");
			foreach (ValuableSpec valuable in valuables)
			{
				if (!Positive(valuable.Size.x) || !Positive(valuable.Size.y) || !Positive(valuable.Size.z) || !Finite(valuable.Weight) || valuable.Weight < 0f) errors.Add("Valuable zones need positive dimensions and a nonnegative finite weight.");
			}
			if (!manualBounds.HasValue && floors.Count == 0 && boxes.Count == 0 && meshes.Count == 0 && walls.Count == 0) errors.Add("The room has no geometry from which bounds can be computed.");
			if (manualBounds.HasValue && (!Finite(manualBounds.Value.center) || !Positive(manualBounds.Value.size.x) || !Positive(manualBounds.Value.size.y) || !Positive(manualBounds.Value.size.z))) errors.Add("Manual bounds need a finite center and finite positive size.");
			if (manualBounds.HasValue || floors.Count > 0 || boxes.Count > 0 || meshes.Count > 0 || walls.Count > 0)
			{
				ValidateDoorBounds(manualBounds ?? ComputeBounds(), errors);
			}
			return errors;
		}

		public GeneratedRoom Build()
		{
			if (built)
			{
				throw new InvalidOperationException("Room '" + roomName + "' has already been built.");
			}
			IReadOnlyList<string> errors = Validate();
			if (errors.Count > 0)
			{
				throw new InvalidOperationException("Room '" + roomName + "' is invalid:\n- " + string.Join("\n- ", errors));
			}
			built = true;
			MapGameAdapter adapter = scope.Adapter;
			if (adapter.RoomName == null || adapter.SpawnPoint == null)
			{
				throw new InvalidOperationException("Required Lucid Cats room bindings are unavailable.");
			}
			Material defaultMaterial = scope.Context.Materials.Create(color, glow);
			GameObject root = new GameObject(roomName);
			root.transform.SetParent(scope.Holder, false);
			Bounds bounds = manualBounds ?? ComputeBounds();
			root.layer = scope.Context.Layers.RoomBounds;
			BoxCollider boundsCollider = root.AddComponent<BoxCollider>();
			boundsCollider.isTrigger = true;
			boundsCollider.center = bounds.center;
			boundsCollider.size = bounds.size;
			GeneratedRoom room = root.AddComponent<GeneratedRoom>();
			room.Bounds = boundsCollider;
			room.SpawnWeight = weight;
			room.CanBeStart = canStart;
			room.CanBeSpawnRoom = canSpawn;
			room.CanContainExit = canExit;
			room.MinCount = minCount;
			room.MaxCount = maxCount;
			adapter.RoomName.SetValue(room, roomName);
			foreach (FloorSpec floor in floors)
			{
				Piece(root, floor.Material ?? defaultMaterial, floor.Center, floor.Size, Quaternion.identity, ColliderMode.Solid);
				if (ceiling)
				{
					Vector3 top = floor.Center + Vector3.up * (wallHeight + 0.1f);
					Piece(root, floor.Material ?? defaultMaterial, top, floor.Size, Quaternion.identity, ColliderMode.Solid);
				}
			}
			foreach (BoxSpec box in boxes)
			{
				Piece(root, box.Material ?? defaultMaterial, box.Center, box.Size, box.Rotation, box.Collider);
			}
			foreach (MeshSpec mesh in meshes)
			{
				EmitMesh(root, mesh, defaultMaterial);
			}
			Vector2 centroid = Centroid();
			foreach (WallSpec wall in walls)
			{
				EmitWall(root, wall, centroid, wall.Material ?? defaultMaterial);
			}
			foreach (LightSpec spec in lights)
			{
				GameObject bulb = new GameObject("Light");
				bulb.transform.SetParent(root.transform, false);
				bulb.transform.localPosition = spec.Position;
				Light light = bulb.AddComponent<Light>();
				light.type = spec.Type;
				light.color = spec.Tint ?? color;
				light.intensity = spec.Intensity;
				light.range = spec.Range > 0f ? spec.Range : Mathf.Max(bounds.size.x, bounds.size.z);
				light.shadows = spec.Shadows;
			}
			foreach (PropSpec prop in props)
			{
				GameObject copy = scope.Context.Prefabs.CloneDecoration(prop.Prefab, root.transform);
				copy.transform.localPosition = prop.Position;
				copy.transform.localRotation = prop.Rotation;
			}
			Transform[] spawnPoints = new Transform[spawns.Count];
			for (int i = 0; i < spawns.Count; i++)
			{
				GameObject point = new GameObject("Spawn " + (i + 1));
				point.transform.SetParent(root.transform, false);
				point.transform.localPosition = spawns[i].Position;
				spawnPoints[i] = point.transform;
			}
			if (spawnPoints.Length > 0)
			{
				adapter.SpawnPoint.SetValue(room, spawnPoints[0]);
				root.AddComponent<MapKitSpawnPoints>().Points = spawnPoints;
			}
			foreach (ValuableSpec valuable in valuables)
			{
				EmitValuable(root, valuable, adapter);
			}
			foreach (Action<GameObject> edit in rootEdits)
			{
				edit(root);
			}
			foreach (Action<GeneratedRoom> edit in roomEdits)
			{
				edit(room);
			}
			return room;
		}

		private void AddBoundDoor(WallSpec wall, float at, float width, float height, float bottom, Vector3? outward, bool exit)
		{
			EnsureMutable();
			DoorSpec door = new DoorSpec(wall, 0f, 0f, wall.Length * 0.5f + at, width, height, bottom, outward)
			{
				ReservedExit = exit
			};
			doors.Add(door);
		}

		private void ValidateDoorBounds(Bounds bounds, List<string> errors)
		{
			foreach (DoorSpec door in doors)
			{
				WallSpec wall = WallFor(door);
				if (wall == null || wall.Length < 0.01f)
				{
					continue;
				}
				float ratio = AlongFor(door, wall) / wall.Length;
				Vector3 position = new Vector3(
					Mathf.Lerp(wall.X1, wall.X2, ratio),
					door.Bottom,
					Mathf.Lerp(wall.Z1, wall.Z2, ratio));
				Vector3 offset = position - bounds.center;
				float inside = Mathf.Min(
					bounds.extents.x - Mathf.Abs(offset.x),
					bounds.extents.y - Mathf.Abs(offset.y),
					bounds.extents.z - Mathf.Abs(offset.z));
				if (inside > 0.01f)
				{
					errors.Add($"Door at ({position.x:0.##}, {position.y:0.##}, {position.z:0.##}) is {inside:0.##}m inside the room bounds; a doorway must lie on the bounds boundary.");
				}
			}
		}

		private void ValidateDoors(WallSpec wall, List<string> errors)
		{
			List<DoorSpec> onWall = doors.Where(door => WallFor(door) == wall).OrderBy(door => AlongFor(door, wall)).ToList();
			float previousEnd = -1f;
			foreach (DoorSpec door in onWall)
			{
				float along = AlongFor(door, wall);
				float width = DoorWidth(door);
				float height = DoorHeight(door);
				if (!Positive(width))
				{
					errors.Add(door.Width <= 0f && !scope.Context.Metrics.HasDoorwaySize
						? "Door width must be supplied because no game doorway size was measured."
						: "Door width must be finite and positive.");
				}
				else if (width >= wall.Length)
				{
					errors.Add("Door width must be smaller than its wall.");
				}
				if (!Positive(height))
				{
					errors.Add(door.Height <= 0f && !scope.Context.Metrics.HasDoorwaySize
						? "Door height must be supplied because no game doorway size was measured."
						: "Door height must be finite and positive.");
				}
				else if (!Finite(door.Bottom) || door.Bottom < wall.Bottom || door.Bottom + height > wall.Bottom + wallHeight + 0.001f)
				{
					errors.Add("Door height and bottom must fit within its wall.");
				}
				if (door.Outward.HasValue && (!Finite(door.Outward.Value) || door.Outward.Value.sqrMagnitude < 0.001f)) errors.Add("An explicit door direction must be finite and nonzero.");
				float start = along - width * 0.5f;
				float end = along + width * 0.5f;
				if (start < 0.05f || end > wall.Length - 0.05f) errors.Add("Door opening must remain inside its wall with a 5cm edge.");
				if (start < previousEnd + 0.05f) errors.Add("Door openings on one wall overlap or are too close together.");
				previousEnd = end;
			}
		}

		private void EmitWall(GameObject root, WallSpec wall, Vector2 centroid, Material material)
		{
			float dx = wall.X2 - wall.X1;
			float dz = wall.Z2 - wall.Z1;
			float length = wall.Length;
			float yaw = Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;
			List<DoorSpec> ordered = doors.Where(door => WallFor(door) == wall).OrderBy(door => AlongFor(door, wall)).ToList();
			float cursor = 0f;
			foreach (DoorSpec door in ordered)
			{
				float along = AlongFor(door, wall);
				float width = DoorWidth(door);
				float height = DoorHeight(door);
				float left = along - width * 0.5f;
				if (left > cursor)
				{
					WallSpan(root, material, wall, yaw, cursor, left, wall.Bottom, wall.Bottom + wallHeight);
				}
				if (door.Bottom > wall.Bottom)
				{
					WallSpan(root, material, wall, yaw, left, left + width, wall.Bottom, door.Bottom);
				}
				float lintelBottom = door.Bottom + height;
				if (lintelBottom < wall.Bottom + wallHeight)
				{
					WallSpan(root, material, wall, yaw, left, left + width, lintelBottom, wall.Bottom + wallHeight);
				}
				Vector3 at = new Vector3(wall.X1 + dx * (along / length), door.Bottom, wall.Z1 + dz * (along / length));
				Vector3 outward = door.Outward ?? AutoOutward(wall, centroid, at);
				GameObject doorwayObject = new GameObject("Doorway");
				doorwayObject.transform.SetParent(root.transform, false);
				doorwayObject.transform.localPosition = at;
				doorwayObject.transform.localRotation = Quaternion.LookRotation(outward, Vector3.up);
				Doorway doorway = doorwayObject.AddComponent<Doorway>();
				if (door.ReservedExit)
				{
					root.GetComponent<GeneratedRoom>().ExitDoorway = doorway;
					root.GetComponent<GeneratedRoom>().CanContainExit = true;
				}
				cursor = left + width;
			}
			if (cursor < length)
			{
				WallSpan(root, material, wall, yaw, cursor, length, wall.Bottom, wall.Bottom + wallHeight);
			}
		}

		private static Vector3 AutoOutward(WallSpec wall, Vector2 centroid, Vector3 at)
		{
			Vector2 normal = new Vector2(wall.Z2 - wall.Z1, wall.X1 - wall.X2).normalized;
			if ((new Vector2(at.x, at.z) - centroid).x * normal.x + (new Vector2(at.x, at.z) - centroid).y * normal.y < 0f)
			{
				normal = -normal;
			}
			return new Vector3(normal.x, 0f, normal.y);
		}

		private void WallSpan(GameObject root, Material material, WallSpec wall, float yaw, float from, float to, float y0, float y1)
		{
			float dx = wall.X2 - wall.X1;
			float dz = wall.Z2 - wall.Z1;
			float ratio = (from + to) * 0.5f / wall.Length;
			Vector3 center = new Vector3(wall.X1 + dx * ratio, (y0 + y1) * 0.5f, wall.Z1 + dz * ratio);
			Piece(root, material, center, new Vector3(WallThickness, y1 - y0, to - from), Quaternion.Euler(0f, yaw, 0f), ColliderMode.Solid);
		}

		private void EmitMesh(GameObject root, MeshSpec spec, Material defaultMaterial)
		{
			if (spec.Mesh == null)
			{
				throw new InvalidOperationException("Room '" + roomName + "' contains a null mesh.");
			}
			GameObject part = new GameObject("Mesh");
			part.layer = scope.Context.Layers.Solid;
			part.transform.SetParent(root.transform, false);
			part.transform.localPosition = spec.Position;
			part.transform.localRotation = spec.Rotation;
			part.AddComponent<MeshFilter>().sharedMesh = spec.Mesh;
			part.AddComponent<MeshRenderer>().sharedMaterial = spec.Material ?? defaultMaterial;
			if (spec.Collider != ColliderMode.None)
			{
				MeshCollider collider = part.AddComponent<MeshCollider>();
				collider.sharedMesh = spec.Mesh;
				collider.convex = spec.Collider == ColliderMode.Trigger;
				collider.isTrigger = spec.Collider == ColliderMode.Trigger;
			}
		}

		private void EmitValuable(GameObject root, ValuableSpec spec, MapGameAdapter adapter)
		{
			if (adapter.ZoneSize == null || adapter.ZoneOffset == null || adapter.ZoneWeight == null)
			{
				throw new InvalidOperationException("ValuableSpawnZone bindings are unavailable.");
			}
			GameObject zoneObject = new GameObject("Valuable Zone");
			zoneObject.transform.SetParent(root.transform, false);
			zoneObject.transform.localPosition = spec.Position;
			ValuableSpawnZone zone = zoneObject.AddComponent<ValuableSpawnZone>();
			adapter.ZoneSize.SetValue(zone, spec.Size);
			adapter.ZoneOffset.SetValue(zone, 0f);
			adapter.ZoneWeight.SetValue(zone, spec.Weight);
		}

		private void Piece(GameObject parent, Material material, Vector3 center, Vector3 size, Quaternion rotation, ColliderMode colliderMode)
		{
			GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
			part.name = "Geometry";
			part.layer = scope.Context.Layers.Solid;
			part.transform.SetParent(parent.transform, false);
			part.transform.localPosition = center;
			part.transform.localRotation = rotation;
			part.transform.localScale = size;
			part.GetComponent<Renderer>().sharedMaterial = material;
			Collider collider = part.GetComponent<Collider>();
			if (colliderMode == ColliderMode.None)
			{
				UnityEngine.Object.DestroyImmediate(collider);
			}
			else
			{
				collider.isTrigger = colliderMode == ColliderMode.Trigger;
			}
		}

		private Bounds ComputeBounds()
		{
			bool has = false;
			Bounds result = default;
			void Add(Bounds value)
			{
				if (!has)
				{
					result = value;
					has = true;
				}
				else
				{
					result.Encapsulate(value.min);
					result.Encapsulate(value.max);
				}
			}
			foreach (FloorSpec floor in floors)
			{
				Add(new Bounds(floor.Center, floor.Size));
				if (ceiling) Add(new Bounds(floor.Center + Vector3.up * (wallHeight + 0.1f), floor.Size));
			}
			foreach (BoxSpec box in boxes) Add(RotatedBounds(box.Center, box.Size, box.Rotation));
			foreach (MeshSpec mesh in meshes)
			{
				if (mesh.Mesh != null) Add(TransformBounds(mesh.Mesh.bounds, Matrix4x4.TRS(mesh.Position, mesh.Rotation, Vector3.one)));
			}
			foreach (WallSpec wall in walls)
			{
				Vector3 center = new Vector3((wall.X1 + wall.X2) * 0.5f, wall.Bottom + wallHeight * 0.5f, (wall.Z1 + wall.Z2) * 0.5f);
				Add(new Bounds(center, new Vector3(Mathf.Abs(wall.X2 - wall.X1), wallHeight, Mathf.Abs(wall.Z2 - wall.Z1))));
			}
			return has ? result : new Bounds(Vector3.zero, Vector3.one);
		}

		private Vector2 Centroid()
		{
			if (floors.Count > 0)
			{
				Vector2 total = Vector2.zero;
				foreach (FloorSpec floor in floors) total += new Vector2(floor.Center.x, floor.Center.z);
				return total / floors.Count;
			}
			if (walls.Count > 0)
			{
				Vector2 total = Vector2.zero;
				foreach (WallSpec wall in walls) total += new Vector2(wall.X1 + wall.X2, wall.Z1 + wall.Z2) * 0.5f;
				return total / walls.Count;
			}
			return Vector2.zero;
		}

		private WallSpec NearestWall(float x, float z, out float distance)
		{
			WallSpec nearest = null;
			float best = float.MaxValue;
			foreach (WallSpec wall in walls)
			{
				if (wall.Length < 0.01f)
				{
					continue;
				}
				float along = Mathf.Clamp(Along(wall, x, z), 0f, wall.Length);
				float ratio = along / wall.Length;
				float px = Mathf.Lerp(wall.X1, wall.X2, ratio);
				float pz = Mathf.Lerp(wall.Z1, wall.Z2, ratio);
				float candidate = new Vector2(x - px, z - pz).magnitude;
				if (candidate < best)
				{
					best = candidate;
					nearest = wall;
				}
			}
			distance = best;
			return nearest;
		}

		private WallSpec WallFor(DoorSpec door)
		{
			if (door.Wall != null)
			{
				return door.Wall;
			}
			return NearestWall(door.X, door.Z, out _);
		}

		private static float AlongFor(DoorSpec door, WallSpec wall)
		{
			return door.Wall == null ? Along(wall, door.X, door.Z) : door.Along;
		}

		private static float Along(WallSpec wall, float x, float z)
		{
			float dx = wall.X2 - wall.X1;
			float dz = wall.Z2 - wall.Z1;
			return ((x - wall.X1) * dx + (z - wall.Z1) * dz) / wall.Length;
		}

		private float DoorWidth(DoorSpec door) => door.Width > 0f ? door.Width : scope.Context.Metrics.DoorwayWidth;
		private float DoorHeight(DoorSpec door) => door.Height > 0f ? door.Height : Mathf.Min(scope.Context.Metrics.DoorwayHeight, wallHeight - door.Bottom);
		private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
		private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
		private static bool Positive(float value) => Finite(value) && value > 0f;

		private static Bounds RotatedBounds(Vector3 center, Vector3 size, Quaternion rotation)
		{
			return TransformBounds(new Bounds(Vector3.zero, size), Matrix4x4.TRS(center, rotation, Vector3.one));
		}

		private static Bounds TransformBounds(Bounds source, Matrix4x4 matrix)
		{
			Bounds result = new Bounds(matrix.MultiplyPoint3x4(source.center), Vector3.zero);
			for (int corner = 0; corner < 8; corner++)
			{
				Vector3 local = source.center + Vector3.Scale(source.extents, new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
				result.Encapsulate(matrix.MultiplyPoint3x4(local));
			}
			return result;
		}

		public sealed class WallHandle
		{
			private readonly RoomBuilder room;
			private readonly WallSpec wall;

			internal WallHandle(RoomBuilder room, WallSpec wall)
			{
				this.room = room;
				this.wall = wall;
			}

			public DoorHandle Door(float at = 0f, float width = 0f, float height = 0f, float bottom = 0f, Vector3? outward = null)
			{
				room.AddBoundDoor(wall, at, width, height, bottom, outward, false);
				return new DoorHandle(room, room.doors[room.doors.Count - 1]);
			}
		}

		public sealed class DoorHandle
		{
			private readonly RoomBuilder room;
			private readonly DoorSpec door;

			internal DoorHandle(RoomBuilder room, DoorSpec door)
			{
				this.room = room;
				this.door = door;
			}

			public DoorHandle Exit(bool value = true)
			{
				room.EnsureMutable();
				door.ReservedExit = value;
				return this;
			}
		}

		private sealed class FloorSpec
		{
			internal FloorSpec(Vector3 center, Vector3 size, Material material)
			{
				Center = center;
				Size = size;
				Material = material;
			}

			internal Vector3 Center { get; }
			internal Vector3 Size { get; }
			internal Material Material { get; }
		}

		internal sealed class WallSpec
		{
			internal WallSpec(float x1, float z1, float x2, float z2, float bottom, Material material)
			{
				X1 = x1;
				Z1 = z1;
				X2 = x2;
				Z2 = z2;
				Bottom = bottom;
				Material = material;
			}

			internal float X1 { get; }
			internal float Z1 { get; }
			internal float X2 { get; }
			internal float Z2 { get; }
			internal float Bottom { get; }
			internal Material Material { get; }
			internal float Length => new Vector2(X2 - X1, Z2 - Z1).magnitude;
		}

		private sealed class BoxSpec
		{
			internal BoxSpec(Vector3 center, Vector3 size, Quaternion rotation, Material material, ColliderMode collider)
			{
				Center = center;
				Size = size;
				Rotation = rotation;
				Material = material;
				Collider = collider;
			}

			internal Vector3 Center { get; }
			internal Vector3 Size { get; }
			internal Quaternion Rotation { get; }
			internal Material Material { get; }
			internal ColliderMode Collider { get; }
		}

		private sealed class MeshSpec
		{
			internal MeshSpec(Mesh mesh, Vector3 position, Quaternion rotation, Material material, ColliderMode collider)
			{
				Mesh = mesh;
				Position = position;
				Rotation = rotation;
				Material = material;
				Collider = collider;
			}

			internal Mesh Mesh { get; }
			internal Vector3 Position { get; }
			internal Quaternion Rotation { get; }
			internal Material Material { get; }
			internal ColliderMode Collider { get; }
		}

		internal sealed class DoorSpec
		{
			internal DoorSpec(WallSpec wall, float x, float z, float along, float width, float height, float bottom, Vector3? outward)
			{
				Wall = wall;
				X = x;
				Z = z;
				Along = along;
				Width = width;
				Height = height;
				Bottom = bottom;
				Outward = outward;
			}

			internal WallSpec Wall { get; }
			internal float X { get; }
			internal float Z { get; }
			internal float Along { get; }
			internal float Width { get; }
			internal float Height { get; }
			internal float Bottom { get; }
			internal Vector3? Outward { get; }
			internal bool ReservedExit { get; set; }
		}

		private sealed class LightSpec
		{
			internal LightSpec(Vector3 position, Color? tint, float intensity, float range, LightType type, LightShadows shadows)
			{
				Position = position;
				Tint = tint;
				Intensity = intensity;
				Range = range;
				Type = type;
				Shadows = shadows;
			}

			internal Vector3 Position { get; }
			internal Color? Tint { get; }
			internal float Intensity { get; }
			internal float Range { get; }
			internal LightType Type { get; }
			internal LightShadows Shadows { get; }
		}

		private sealed class PropSpec
		{
			internal PropSpec(GameObject prefab, Vector3 position, Quaternion rotation)
			{
				Prefab = prefab;
				Position = position;
				Rotation = rotation;
			}

			internal GameObject Prefab { get; }
			internal Vector3 Position { get; }
			internal Quaternion Rotation { get; }
		}

		private sealed class SpawnSpec
		{
			internal SpawnSpec(Vector3 position)
			{
				Position = position;
			}

			internal Vector3 Position { get; }
		}

		private sealed class ValuableSpec
		{
			internal ValuableSpec(Vector3 position, Vector3 size, float weight)
			{
				Position = position;
				Size = size;
				Weight = weight;
			}

			internal Vector3 Position { get; }
			internal Vector3 Size { get; }
			internal float Weight { get; }
		}
	}

	internal sealed class MapKitSpawnPoints : MonoBehaviour
	{
		[SerializeField]
		internal Transform[] Points = Array.Empty<Transform>();
	}
}
