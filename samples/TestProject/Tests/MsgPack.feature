Feature: MsgPack

Scenario: MsgPack Roundtrip
	When I get Entity with id 12
	Then The request succeded
	And Content-Type is application/x-msgpack
	And the Entity has
		| Key      | Value |
		| EntityId | 12    |