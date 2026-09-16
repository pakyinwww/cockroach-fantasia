# Kitchen greybox

The MVP Kitchen is an 18 by 14 metre bounded play space with three deliberately
different routes away from the back-wall nest:

| Route | Shape | Tradeoff |
| --- | --- | --- |
| A — floor | Long path around two solid sight blockers | safest cover, longest carry |
| B — counter | Two exposed ramps and raised counter runs | shortest high-risk food route |
| C — cabinet | Low-clearance tunnel along the right cabinets | hidden from the Human, narrow exits |

The blockers break cross-map sight lines, so the Human cannot watch all three
routes from one position. Four outer wall colliders close the playable bounds.
A trigger beneath the floor returns future player controllers through the
`IKitchenRecoverable` seam instead of allowing permanent falls.

The protected wall-crack nest has a 0.5 metre ceiling and narrow entrance: the
Cockroach scale fits, while a Human capsule cannot enter. The interior contains
three Cockroach respawn markers; the fourth marker is the Human start. Eighteen
numbered food markers are grouped in the hierarchy and split evenly across low,
medium, and high risk. Their colored gizmos remain directly movable in the
Scene view. Decorative mug and bowl meshes have no colliders and cannot snag
either role.
