using Microsoft.Xna.Framework;
using System.Collections.Concurrent;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using static TShockAPI.GetDataHandlers;

namespace TownNPCHomes;

[ApiVersion(2, 1)]
public class Plugin : TerrariaPlugin
{
    public override string Name => "TownNPCHomes";
    public override string Author => "Neoslyke, 棱镜, 羽学";
    public override Version Version => new Version(2, 1, 0);
    public override string Description => "Teleports town NPCs to their homes when assigned.";

    private readonly ConcurrentDictionary<int, Vector2> _npcHomePositions = new();

    public Plugin(Main game) : base(game) { }

    public override void Initialize()
    {
        Commands.ChatCommands.Add(new Command("townnpchomes.use", TeleportNpcsHomeCommand, "npchome")
        {
            HelpText = "Teleports all town NPCs to their assigned homes."
        });
        
        NPCHome += OnNpcHome;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Commands.ChatCommands.RemoveAll(c => c.CommandDelegate == TeleportNpcsHomeCommand);
            NPCHome -= OnNpcHome;
        }
        base.Dispose(disposing);
    }

    private void TeleportNpcsHomeCommand(CommandArgs args)
    {
        int teleportedCount = 0;

        foreach (var npc in Main.npc)
        {
            if (npc == null || !npc.active || !npc.townNPC || npc.homeless)
                continue;

            var homePosition = new Vector2(npc.homeTileX * 16, npc.homeTileY * 16);
            _npcHomePositions.TryAdd(npc.whoAmI, homePosition);

            if (TrySendNpcToHome(npc.whoAmI, homePosition))
                teleportedCount++;
        }

        if (teleportedCount > 0)
            args.Player.SendSuccessMessage($"[TownNPCHomes] Teleported {teleportedCount} town NPC(s) home.");
        else
            args.Player.SendInfoMessage("[TownNPCHomes] No town NPCs with assigned homes found.");
    }

    private bool TrySendNpcToHome(int npcId, Vector2 position)
    {
        try
        {
            TSPlayer.All.SendData(PacketTypes.NpcTeleportPortal, "", npcId, position.X, position.Y, 0f, 0);
            return true;
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"[TownNPCHomes] Error sending NPC {npcId} home: {ex.Message}");
            return false;
        }
    }

    private void OnNpcHome(object? sender, NPCHomeChangeEventArgs args)
    {
        try
        {
            var npc = Main.npc[args.ID];

            if (npc == null)
            {
                args.Player.SendWarningMessage($"[TownNPCHomes] NPC with ID {args.ID} not found.");
                return;
            }

            if (npc.homeless)
                return;

            var homePosition = new Vector2(args.X * 16f, args.Y * 16f);

            // Only teleport if NPC is not already at home
            if (!npc.Bottom.Equals(homePosition))
            {
                npc.Bottom = homePosition;
                _npcHomePositions.TryAdd(args.ID, homePosition);
                
                if (TrySendNpcToHome(args.ID, homePosition))
                {
                    args.Player.SendSuccessMessage($"[TownNPCHomes] {npc.FullName} has been teleported home.");
                }
            }
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"[TownNPCHomes] Error in OnNpcHome: {ex.Message}");
            args.Player.SendErrorMessage("[TownNPCHomes] An error occurred while processing the NPC home event.");
        }
    }
}