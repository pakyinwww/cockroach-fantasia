# Nest deposits and match victory

The Kitchen nest is a server-only deposit trigger. When a carrying Cockroach enters it, the server validates the
carrier/item relationship, advances that item from `Carried` to `Deposited`, clears the Cockroach's carried item,
adds the food definition's points to the shared match score, and despawns the deposited network object.

The lifecycle transition is one-way and conditional on the current carrier. Repeated trigger callbacks, stale RPCs,
drops, and later pickup attempts therefore cannot score or recover the same item. The score and winner live in
`NetworkGameManager` network variables, so clients display the same `current / 12` value.

`MatchStateMachine` resolves 12 or more points immediately as a Cockroach victory. If the authoritative four-minute
deadline arrives below quota, it resolves a Human victory. Deposits and gameplay mutation are rejected outside the
Playing phase.
