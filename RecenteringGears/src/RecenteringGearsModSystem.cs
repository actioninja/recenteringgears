using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Vintagestory.Server;

namespace RecenteringGears;

public class RecenteringGearsModSystem : ModSystem
{
    public static ILogger Logger;
    private Harmony patcher;

    public override bool ShouldLoad(EnumAppSide forSide)
    {
        return forSide == EnumAppSide.Server;
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        Logger = Mod.Logger;
        if (Harmony.HasAnyPatches(Mod.Info.ModID)) return;
        patcher = new Harmony(Mod.Info.ModID);
        patcher.PatchCategory(Mod.Info.ModID);
    }

    public override void Dispose()
    {
        patcher?.UnpatchAll(Mod.Info.ModID);
    }
}

[HarmonyPatchCategory("recenteringgears")]
internal static class Patches
{
    
    static MethodInfo teleportMethod = AccessTools.Method(typeof(ServerSystemEntitySimulation), "teleport");
    static Type ServerSystemSupplyChunksType = AccessTools.TypeByName("ServerSystemSupplyChunks");

    private static MethodInfo adjustForSaveSpawnSpotMethodInfo =
        AccessTools.Method(ServerSystemSupplyChunksType, "AdjustForSaveSpawnSpot");
        
        
        
    
    [HarmonyPrefix]
    [HarmonyPatch(typeof(ServerSystemEntitySimulation))]
    [HarmonyPatch("OnPlayerRespawn")]
    private static bool RespawnReplacement(ServerSystemEntitySimulation __instance, ServerMain ___server, IServerPlayer player)
    {
        if (player.Entity == null || player.Entity.Alive)
        {
            ServerMain.Logger.VerboseDebug("Respawn key received but ignored. Cause: {0} || {1}", player.Entity == null,
                player.Entity.Alive);
        }
        else
        {
            var pos = player.GetSpawnPosition(true);
            var client = ___server.Clients[player.ClientId];
            if (pos.UsesLeft >= 0)
            {
                if (pos.UsesLeft == 99)
                    player.SendLocalisedMessage(GlobalConstants.GeneralChatGroup, "playerrespawn-nocustomspawnset");
                else if (pos.UsesLeft > 0)
                    player.SendLocalisedMessage(GlobalConstants.GeneralChatGroup,
                        "You have re-emerged at your returning point. It will vanish after {0} more uses",
                        pos.UsesLeft);
                else if (pos.UsesLeft == 0)
                    player.SendLocalisedMessage(GlobalConstants.GeneralChatGroup,
                        "You have re-emerged at your returning point, which has now vanished.");
            }

            var spawnRadius = ___server.World.Config.GetAsInt("spawnRadius");
            pos.Radius = spawnRadius;
            if (pos.Radius > 0.0)
                ___server.LocateRandomPosition(pos.XYZ, pos.Radius, 10,
                    spawnpos => (bool)adjustForSaveSpawnSpotMethodInfo.Invoke(null, [___server, spawnpos, player,
                        ___server.rand.Value]), foundpos =>
                    {
                        if (foundpos != null)
                        {
                            var targetPos = pos.Copy();
                            targetPos.X = foundpos.X;
                            targetPos.Y = foundpos.Y;
                            targetPos.Z = foundpos.Z;
                            teleportMethod.Invoke(__instance, [client, targetPos]);
                        }
                        else
                        {
                            teleportMethod.Invoke(__instance, [client, pos]);
                        }
                    });
            else 
                teleportMethod.Invoke(__instance, [client, pos]);
            ServerMain.Logger.VerboseDebug(
                "Respawn key received. Teleporting player to spawn and reviving once chunks have loaded.");
        }

        return false;
    }
}