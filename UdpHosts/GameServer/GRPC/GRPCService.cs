using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GameServer.GRPC.EventHandlers;
using Grpc.Core;
using Grpc.Net.Client;
using GrpcGameServerAPIClient;

namespace GameServer.GRPC;

public static class GRPCService
{
    private static GrpcChannel _channel;
    private static GameServerAPI.GameServerAPIClient _client;
    private static AsyncDuplexStreamingCall<Command, Event> _stream;

    public static void Init(string address)
    {
        _channel = GrpcChannel.ForAddress(address);
        _client = new GameServerAPI.GameServerAPIClient(_channel);
    }

    public static bool IsAvailable()
    {
        return _channel.State == ConnectivityState.Ready;
    }

    public static async Task<CharacterAndBattleframeVisuals> GetCharacterAndBattleframeVisualsAsync(long characterId)
    {
        return await _client.GetCharacterAndBattleframeVisualsAsync(new CharacterID { ID = characterId });
    }

    public static async Task<CharacterInventoryResp> GetCharacterInventoryAsync(long characterId)
    {
        return await _client.GetCharacterInventoryAsync(new CharacterID { ID = characterId });
    }

    public static async Task SaveInventoryItemAsync(ulong charGuid, long itemGuid, uint sdbId, uint subInventory, uint dynamicFlags, uint durability, uint[] modules)
    {
        var cmd = new Command();
        cmd.SaveInventoryItem = new SaveInventoryItem
        {
            CharacterGuid = charGuid,
            ItemGuid = itemGuid,
            SdbId = sdbId,
            SubInventory = subInventory,
            DynamicFlags = dynamicFlags,
            Durability = durability,
        };
        if (modules != null)
        {
            cmd.SaveInventoryItem.Modules.AddRange(modules);
        }
        await SendCommandAsync(cmd);
    }

    public static async Task SaveInventoryResourceAsync(ulong charGuid, uint sdbId, uint quantity, uint subInventory)
    {
        var cmd = new Command();
        cmd.SaveInventoryResource = new SaveInventoryResource
        {
            CharacterGuid = charGuid,
            SdbId = sdbId,
            Quantity = quantity,
            SubInventory = subInventory
        };
        await SendCommandAsync(cmd);
    }

    public static async Task SeedCharacterInventoryAsync(ulong charGuid,
        IEnumerable<InventoryItemData> items,
        IEnumerable<InventoryResourceData> resources,
        IEnumerable<InventoryLoadoutData> loadouts,
        IEnumerable<InventoryLoadoutSlotData> slots)
    {
        var cmd = new Command();
        cmd.SeedCharacterInventory = new SeedCharacterInventory
        {
            CharacterGuid = charGuid,
        };
        cmd.SeedCharacterInventory.Items.AddRange(items);
        cmd.SeedCharacterInventory.Resources.AddRange(resources);
        cmd.SeedCharacterInventory.Loadouts.AddRange(loadouts);
        cmd.SeedCharacterInventory.Slots.AddRange(slots);
        await SendCommandAsync(cmd);
    }

    public static async Task SaveCharacterSessionDataAsync(ulong characterId, uint zoneId, uint outpostId, uint timePlayed)
    {
        var data = new SaveGameSessionData()
           {
               CharacterId = characterId, ZoneId = zoneId, OutpostId = outpostId, TimePlayed = timePlayed
           };

        await SendCommandAsync(new Command() { SaveGameSessionData = data });
    }

    public static async Task SendCommandAsync(Command command)
    {
        await _stream.RequestStream.WriteAsync(command);
    }

    public static async Task ListenAsync(ConcurrentDictionary<uint, INetworkPlayer> clientMap, CancellationToken ct)
    {
        _stream = _client.Stream(cancellationToken: ct);

        await foreach (var evt in _stream.ResponseStream.ReadAllAsync(ct))
        {
            Console.WriteLine(evt);
            switch (evt.SubtypeCase)
            {
                case Event.SubtypeOneofCase.ArmyApplicationApproved:
                    ArmyEventHandler.HandleEvent(evt.ArmyApplicationApproved, clientMap);
                    break;
                case Event.SubtypeOneofCase.ArmyApplicationReceived:
                    ArmyEventHandler.HandleEvent(evt.ArmyApplicationReceived, clientMap);
                    break;
                case Event.SubtypeOneofCase.ArmyApplicationRejected:
                    ArmyEventHandler.HandleEvent(evt.ArmyApplicationRejected, clientMap);
                    break;
                case Event.SubtypeOneofCase.ArmyApplicationsUpdated:
                    ArmyEventHandler.HandleEvent(evt.ArmyApplicationsUpdated, clientMap);
                    break;
                case Event.SubtypeOneofCase.ArmyIdChanged:
                    ArmyEventHandler.HandleEvent(evt.ArmyIdChanged, clientMap);
                    break;
                case Event.SubtypeOneofCase.ArmyInfoUpdated:
                    ArmyEventHandler.HandleEvent(evt.ArmyInfoUpdated, clientMap);
                    break;
                case Event.SubtypeOneofCase.ArmyInviteApproved:
                    ArmyEventHandler.HandleEvent(evt.ArmyInviteApproved, clientMap);
                    break;
                case Event.SubtypeOneofCase.ArmyInviteReceived:
                    ArmyEventHandler.HandleEvent(evt.ArmyInviteReceived, clientMap);
                    break;
                case Event.SubtypeOneofCase.ArmyInviteRejected:
                    ArmyEventHandler.HandleEvent(evt.ArmyInviteRejected, clientMap);
                    break;
                case Event.SubtypeOneofCase.ArmyMembersUpdated:
                    ArmyEventHandler.HandleEvent(evt.ArmyMembersUpdated, clientMap);
                    break;
                case Event.SubtypeOneofCase.ArmyRanksUpdated:
                    ArmyEventHandler.HandleEvent(evt.ArmyRanksUpdated, clientMap);
                    break;
                case Event.SubtypeOneofCase.ArmyTagUpdated:
                    ArmyEventHandler.HandleEvent(evt.ArmyTagUpdated, clientMap);
                    break;
                case Event.SubtypeOneofCase.CharacterVisualsUpdated:
                    CharacterEventHandler.HandleEvent(evt.CharacterVisualsUpdated, clientMap);
                    break;
                case Event.SubtypeOneofCase.None:
                default:
                    break;
            }
        }
    }
}