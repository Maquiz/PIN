# PIN — Firefall Game Server Emulator

## Architecture
- MatrixServer (UDP 25000): Connection handshake, socket ID assignment
- GameServer (UDP 25001): Entity spawning, physics, combat, abilities, encounters
- WebHosts (ports 4400-4499): DEPRECATED — being replaced by RIN.WebAPI

## Game Loop
- Shard.cs runs at 60Hz game tick, 20Hz net tick
- Systems per tick: Physics -> AI -> Encounters -> Abilities -> WeaponSim -> ProjectileSim -> Loot -> Chat
- Max 64 players per shard (configurable in GameServerSettings)

## Entity System
- All entities inherit from BaseEntity, use Controller/View pattern
- Controllers handle logic, Views track field changes for network replication
- Entity types: Character, Vehicle, Turret, Thumper, Melding, Outpost, Deployable, Carryable

## Ability System (Aptitude)
- Chain-based: SDB defines ability -> chain of commands -> effects
- ~90 commands in Todo/ folders need implementation
- Follow pattern of implemented siblings (e.g., TimeDurationCommand.cs)
- Commands are in: Systems/Aptitude/Commands/{Category}/{CommandName}Command.cs

## Networking
- AeroMessages submodule defines all packet structures (code-generated)
- GSS protocol for game state, Matrix protocol for login handshake
- Keyframe + delta replication for entity state

## Current State
- Movement, jetpacks, vehicles: WORKING
- Weapon fire + damage: WORKING (ProjectileSim + ApplyDamage)
- NPC AI: WORKING (idle/chase/attack state machine)
- Inventory: HARDCODED (CharacterInventory.LoadHardcodedInventory)
- Level: HARDCODED to 45
- Persistence: NONE
