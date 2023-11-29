using System;

// Space Engineers DLLs
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using Sandbox.Game.EntityComponents;
using VRageMath;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;
using SpaceEngineers.Game.ModAPI.Ingame;
using Sandbox.ModAPI;
using System.Collections.Generic;
using IMyCargoContainer = Sandbox.ModAPI.IMyCargoContainer;
using IMyTerminalBlock = Sandbox.ModAPI.IMyTerminalBlock;
using System.Linq;
using IMyTextPanel = Sandbox.ModAPI.IMyTextPanel;
using System.ComponentModel;
using IMyTextSurface = Sandbox.ModAPI.IMyTextSurface;
using System.Text;
using System.Text.Encodings.Web;

/*
 * Must be unique per each script project.
 * Prevents collisions of multiple `class Program` declarations.
 * Will be used to detect the ingame script region, whose name is the same.
 */
namespace InvMan2 {

/*
 * Do not change this declaration because this is the game requirement.
 */
public sealed class Program : MyGridProgram {
    #region InvMan2   

    /////////////////////////////////////////////////// GLOBALS //////////////////////////////////////
    string NAME = "[X]";   
    char SEPARATOR = '-';
    
    Dictionary<string, List<IMyCargoContainer>> CARGO = new Dictionary<string, List<IMyCargoContainer>>();
    Dictionary<string, List<IMyTextPanel>>      PANEL = new Dictionary<string, List<IMyTextPanel>>();
    Dictionary<string, List<MyInventoryItem>>   ITEMS = new Dictionary<string, List<MyInventoryItem>>();
    
    //Dictionary<string, Vector2>           PANEL_INFO = new Dictionary<IMyTextPanel, Vector2>();
    
    
    string ECHO = "";    
    
    public void Save() { }

    //----------------------- Constructor -------------------------
    public Program() {
        Runtime.UpdateFrequency = UpdateFrequency.Update100;

        // Get all blocks and discriminate
        List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>(); 
        GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(blocks);

        foreach( IMyTerminalBlock block in blocks ){
            if ( !block.CustomName.Contains( NAME ) ) continue;
            string data = block.CustomData=="" ? "dump" : FormatCustomData( block.CustomData );
            block.CustomData = data; // Preformat the actual custom data for later

            if ( block is IMyCargoContainer ){
                if ( !CARGO.ContainsKey(data) ) CARGO.Add( data, new List<IMyCargoContainer>() );
                CARGO[data].Add( (IMyCargoContainer)block );
            }else if ( block is IMyTextPanel ){
                var surface = (IMyTextSurface) block;
                //PANEL_INFO.Add( (IMyTextPanel)block, ((IMyTextSurface)block).SurfaceSize );
                if ( !PANEL.ContainsKey(data) ) PANEL.Add( data, new List<IMyTextPanel>() );
                PANEL[data].Add( (IMyTextPanel)block );
            }
        }
    }

    //------------------------- Helpers -------------------------
    Func<string, string> FormatCustomData = s => s.ToLower().Trim();
    Func<string, string> FormatId = s => s.Substring( s.LastIndexOf("_") + 1 ).Trim();

    public void RegisterItem( MyInventoryItem item ){
        string id = FormatId( item.Type.TypeId );
        if ( !ITEMS.ContainsKey(id) ) ITEMS.Add( id, new List<MyInventoryItem>() );
        ITEMS[id].Add( item );
    }

    public bool UpdateCargo( IMyCargoContainer container, string category=null ){
        bool updated_inventory = false;     
        List<MyInventoryItem> items = new List<MyInventoryItem>(); // Get Items
        IMyInventory container_inventory = container.GetInventory(0);
        container_inventory.GetItems( items );
        items.Reverse();
        foreach( MyInventoryItem item in items ){
            string type_id = FormatId( item.Type.TypeId ).ToLower();
            bool item_moved = false;            
            if ( type_id == category ){ // Item is where it should be

            }else{ // Find another place
                if ( CARGO.ContainsKey( type_id ) ){
                    foreach( IMyCargoContainer possible_container in CARGO[type_id] ){
                        var possible_inventory = possible_container.GetInventory(0);
                        if ( possible_inventory.CanItemsBeAdded(item.Amount, item.Type) & container_inventory.CanTransferItemTo(possible_inventory, item.Type) ){
                            item_moved = possible_inventory.TransferItemFrom( container_inventory, item );
                            if ( !updated_inventory & item_moved ) updated_inventory = true;
                        }
                        if ( item_moved ) break;
                    }
                }
            }
            if ( !item_moved ) RegisterItem( item );
        }
        return updated_inventory;
    }

    public void UpdatePanel( IMyTextPanel panel, string category ){
        List<string> lines = new List<string>();
        if ( category == "items" ){
            foreach( KeyValuePair<string, List<MyInventoryItem>> entry in ITEMS ){
                //lines.Add($"{entry.Key}:");
                foreach( MyInventoryItem item in entry.Value ){
                    lines.Add($"{item.Type.SubtypeId}${SEPARATOR}{item.Amount}");
                }
            }
        }else{
            // Lookup category
        }

        // Write to panel
        panel.WriteText("");
        foreach( string line in lines ) WriteSpacedString( line, SEPARATOR, panel );
    }

    public void WriteSpacedString( string str, char separator, IMyTextPanel panel ){
        float panel_width = ((IMyTextSurface)panel).SurfaceSize.X;
        string[] split = str.Split(separator);

        Func<string, IMyTextPanel, int> width = 
            ( s, p )=> (int)Math.Ceiling( p.MeasureStringInPixels(new StringBuilder(s, s.Length), p.Font, p.FontSize).X ) ;
        
        int size_sep = width( separator.ToString(), panel );
        int size_name = width( split[0], panel );
        int size_amount = width( split[1], panel );
        
        int seps_to_add = (int)Math.Floor( (panel_width-(size_name + size_amount)) / size_sep) - 1;
        string seps = new StringBuilder().Insert(0, separator.ToString(), seps_to_add-1).ToString();
        
        panel.WriteText( $"{split[0]}{seps}{split[1]}\n", true );
    }


    //------------------------- Tick -------------------------
    public void Main(string argument, UpdateType updateSource) {
        ECHO = "";
        ITEMS = new Dictionary<string, List<MyInventoryItem>>();

        foreach( KeyValuePair<string, List<IMyCargoContainer>> entry in CARGO ){
            foreach( IMyCargoContainer container in entry.Value ){
                if ( UpdateCargo(container, entry.Key) ) {
                    
                }
            }
        }
                
        foreach( KeyValuePair<string, List<IMyTextPanel>> entry in PANEL ){
            ECHO += $"\n{entry.Key}\n{string.Join(Environment.NewLine, entry.Value)}";
            foreach( IMyTextPanel panel in entry.Value ){
                UpdatePanel( panel, entry.Key );
            }
        }

        Echo( ECHO );        
    }

    #endregion // InvMan2
}}