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
using IMyRefinery = Sandbox.ModAPI.IMyRefinery;

namespace InvMan2 {
public sealed class Program : MyGridProgram {
    #region InvMan2

    /////////////////////////////////////////////////// GLOBALS //////////////////////////////////////
    string NAME = "[X]";   
    char SEPARATOR = '-';
    
    
    Dictionary<string, List<IMyCargoContainer>> CARGO = new Dictionary<string, List<IMyCargoContainer>>();
    Dictionary<string, List<IMyTextPanel>>      PANEL = new Dictionary<string, List<IMyTextPanel>>();
    Dictionary<string, List<MyInventoryItem>>   ITEMS = new Dictionary<string, List<MyInventoryItem>>();
    Dictionary<string, List<IMyRefinery>>       REFIN = new Dictionary<string, List<IMyRefinery>>();
    
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
            
            }else if ( block is IMyRefinery ){
                if ( !REFIN.ContainsKey(data) ) REFIN.Add( data, new List<IMyRefinery>() );
                REFIN[data].Add( (IMyRefinery)block );
            }
        }
    }

    //------------------------- Helpers -------------------------
    Func<string, string>        FormatCustomData = s => s.ToLower().Trim();
    Func<string, string>        FormatId = s => s.Substring( s.LastIndexOf("_") + 1 ).Trim();
    Func<string, string>        UpperCase = s => char.ToUpper(s[0]) + s.Substring(1);

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

    public void UpdateRefinery( IMyRefinery refinery, string category ){
        ECHO += refinery.CustomName;
    }

    public void UpdatePanel( IMyTextPanel panel, string category ){
        List<List<string>> lines = new List<List<string>>();

        Func<MyFixedPoint, string>  FormatAmount = n => n > 1000 ? $"{(int)n/1000}k" : n.ToString(); 
    
        if ( category == "items" ){
            foreach( KeyValuePair<string, List<MyInventoryItem>> entry in ITEMS ){
                lines.Add( new List<string>(){ $"{entry.Key}:" } ); // Titles
                foreach( MyInventoryItem item in entry.Value ){
                    lines.Add( new List<string>() { item.Type.SubtypeId, "._-=-_.", FormatAmount(item.Amount) });
                }
            }
        }else if ( ITEMS.ContainsKey( UpperCase(category) ) ){
            // Lookup category
            foreach( MyInventoryItem item in ITEMS[UpperCase(category)] ){
                lines.Add( new List<string>() { item.Type.SubtypeId, "._-=-_.", FormatAmount(item.Amount) });
            }
        }
        // Write to panel
        panel.WriteText("");
        foreach( List<string> line in lines ){            
            if ( line.Count() == 1 ){
                panel.WriteText( $"{line[0]}\n", true );
            }else if( line.Count()==3 ){
                WriteSpacedString( line, panel );
            }
        }
    }

    public void WriteSpacedString( List<string> str, IMyTextPanel panel ){
        string final_string = string.Join( "", str );

        // Size of the panel / text
        float panel_width = panel.SurfaceSize.X;
        Func<string, IMyTextPanel, Vector2> StringSize =
            ( strn, panl )=> panl.MeasureStringInPixels(new StringBuilder(strn), panl.Font, panl.FontSize );

        if ( str.Count >= 2 ){
            float head_width = StringSize( str[0], panel ).X;
            float tail_width = StringSize( str[str.Count-1], panel ).X;
            float separator_space = panel_width - ((panel.TextPadding / 100) * panel_width) - (head_width + tail_width);
            ECHO += panel.TextPadding + "\n";
            float separator_space_total = (float)separator_space;
            
            if ( separator_space <= 0 ){ // No room for even 1 sep, write "headtail" cross your fingers
                final_string = $"{str[0]}{str[str.Count-1]}";            
            }else{
                string sep_final = "";
                float percentage_covered = 0;
                char[] sep_list = str[1].ToCharArray();
                
                while ( separator_space > 0 ){
                    int index = (int)Math.Round( (double)(sep_list.Length-1) * percentage_covered );
                    string character = sep_list[index].ToString();
                    float character_width = StringSize( character, panel ).X;                    
                    separator_space -= character_width;
                    percentage_covered = (separator_space_total - separator_space) / separator_space_total;
                    if (percentage_covered > .95) {
                        sep_final = sep_final.Remove(0, 1);// Knock first off if over
                        break;
                    }else{
                        sep_final += character;
                    }                   
                }
                final_string = $"{str[0]}{sep_final}{str[str.Count-1]}";
            }            
        }        
        panel.WriteText( $"{final_string}\n", true );
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

       /*  foreach( KeyValuePair<string, List<IMyRefinery>> entry in REFIN ){
            ECHO += $"\n{entry.Key}\n{string.Join(Environment.NewLine, entry.Value)}";
            foreach( IMyRefinery refinery in entry.Value ){
                UpdateRefinery( refinery, entry.Key );
            }
        } */

        Echo( ECHO );        
    }

    #endregion // InvMan2
}}