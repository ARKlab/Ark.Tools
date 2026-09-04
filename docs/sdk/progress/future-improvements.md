# Ark.Tools SDK — future improvements (drafts, not scheduled)

Deliberately deferred work. Nothing here is a task on the
[board](tasks/README.md), nothing here blocks a release, and nothing here has an
owner. An item moves out of this file by becoming a task document, not by being
picked up from here.

## 1. Upstream Vogen contributions

Deferred because **Ark.Tools does not use Vogen today**, so this is contribution
work for a dependency nobody has taken. It stays recorded because two of the
traps `ARKPII010` reports (§14.5 of the
[compliance PRD](../privacy-by-default-prd.md#145-interop-not-exclusion)) have no
user-side switch in Vogen, and if a consumer ever does adopt Vogen alongside
`Ark.Tools.Compliance`, these are the two fixes that make the combination safe
without a workaround.

Both are generic improvements that benefit every Vogen user, which is why they
belong upstream rather than in an Ark workaround:

1. **`DebuggerAttributeGeneration.None`** — Vogen emits
   `[DebuggerDisplay("… { _value }")]` and a `DebuggerTypeProxy` unconditionally,
   so a Vogen value object holding a secret cannot hide its value from a
   debugger or from tooling that reads those attributes.
2. **`Conversions.Protobuf`** — protobuf-net has no Vogen support at all; the
   documented answer is a hand-written surrogate per type. It is a serializer
   flag in the same category as the accepted `MessagePack` and `XmlSerializable`
   ones.

Prerequisite before opening anything: re-verify both gaps against current Vogen
`main`. Until then `ARKPII010` keeps reporting the trap with the manual
workaround in its message, which is a complete answer on its own — see
[§14.3](../privacy-by-default-prd.md#143-contributing-to-vogen-instead).

## 2. EF Core and Orleans targets for sensitive value objects

Out of scope for the first compliance release because neither is a plain
converter. An EF Core mapping has to carry the storage policy of
[§6.6](../privacy-by-default-prd.md#66-persistence-policy) (`Masked`,
`ApplicationEncrypted`, and Always Encrypted's equality-only comparison
semantics), and an Orleans surrogate has to carry classification across
grain-state versioning. Shipping either as a bare converter would quietly create
a new cleartext egress, which is the failure mode the design exists to prevent.
They land with their compliance bits or they do not land.
