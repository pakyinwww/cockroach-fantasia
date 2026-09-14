# Fixed Food Definitions and Spawning

The MVP uses three tunable `FoodDefinition` assets:

| Size | Placeholder | Points | Carry speed |
| --- | --- | ---: | ---: |
| Small | Cracker crumb | 1 | 95% |
| Medium | Cheese cube | 2 | 85% |
| Large | Doughnut piece | 3 | 70% |

Each definition references a matching placeholder network prefab. Designers can change
points and speed multipliers in the asset without editing code.

`FixedKitchenFood.asset` assigns three items of each size to nine explicit Kitchen marker
indices. The resulting set is worth exactly 18 available points:

```text
3 × 1 + 3 × 2 + 3 × 3 = 18
```

Only `KitchenFoodSpawner` on the server instantiates and spawns these objects. Clients
receive the same NGO `NetworkObject`s and cannot create authoritative food. Every
`FoodItem` replicates its size, lifecycle (`World`, `Carried`, or `Deposited`), and
carrier client ID. World position uses a server-authoritative `NetworkTransform`.

Before spawning, `FoodConfigurationValidator` rejects missing definitions, invalid
prefabs, duplicate or negative markers, inconsistent definitions for a size, missing
sizes, invalid speed/point values, and totals other than 18. Development builds log the
rejection rather than partially spawning a malformed set.

After making a Windows development build, verify all four Relay peers with:

```powershell
& .\Tools\Run-MovementRelayDiagnostic.ps1 `
  -OutputName food-spawn `
  -FoodSpawn `
  -SkipMovement
```
