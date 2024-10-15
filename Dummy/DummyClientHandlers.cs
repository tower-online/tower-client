using Google.FlatBuffers;
using Microsoft.Extensions.Logging;
using tower.network.packet;

namespace Tower.Dummy;

public partial class DummyClient
{
    private void OnClientJoinResponse(ClientJoinResponse response)
    {
        _logger.LogInformation("Joined main server");

        var spawn = response.Spawn.Value;
        var playerData = spawn.Data.Value;
        var location = response.CurrentLocation.Value;

        _player = new DummyPlayer(spawn.EntityId, spawn.ClientId, playerData.Name);
        _clientId = spawn.ClientId;

        var builder = new FlatBufferBuilder(128);
        PlayerEnterZoneRequest.StartPlayerEnterZoneRequest(builder);
        PlayerEnterZoneRequest.AddLocation(builder,
            WorldLocation.CreateWorldLocation(builder, location.Floor, location.ZoneId));
        var request = PlayerEnterZoneRequest.EndPlayerEnterZoneRequest(builder);
        var packetBase = PacketBase.CreatePacketBase(builder, PacketType.PlayerEnterZoneRequest, request.Value);
        builder.FinishSizePrefixed(packetBase.Value);
        _connection.SendPacket(builder.DataBuffer);
    }

    private void OnPlayerEnterZoneResponse(PlayerEnterZoneResponse response)
    {
        if (!response.Result)
        {
            _logger.LogWarning("PlayerEnterZone failed");
            return;
        }

        _environment.Clear();
        if (!response.Location.HasValue) return;
        var location = response.Location.Value;
        _environment.CurrentZoneId = location.ZoneId;
    }

    private void OnPlayerJoinPartyRequest(PlayerJoinPartyRequest request)
    {
        var requester = request.Requester;
        var requestee = request.Requestee;

        // 60% = Accept, 30% = Reject, 10% = Ignore
        var odd = Random.Shared.NextDouble();
        PlayerJoinPartyResult result;
        if (odd <= 0.6)
            result = PlayerJoinPartyResult.OK;
        else if (odd <= 0.9)
            result = PlayerJoinPartyResult.REJECTED;
        else
            return;
        
        var builder = new FlatBufferBuilder(128);
        var response = PlayerJoinPartyResponse.CreatePlayerJoinPartyResponse(
            builder, requester, requestee, result);
        var packetBase = PacketBase.CreatePacketBase(
            builder, PacketType.PlayerJoinPartyResponse, response.Value);
        builder.FinishSizePrefixed(packetBase.Value);
        _connection.SendPacket(builder.DataBuffer); 
    }

    private void OnPlayerJoinPartyResponse(PlayerJoinPartyResponse response)
    {
        if (response.Result != PlayerJoinPartyResult.OK) return;

        _hasParty = true;
    }
}