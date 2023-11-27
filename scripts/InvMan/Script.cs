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
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI;
using IMyTextPanel = Sandbox.ModAPI.IMyTextPanel;
using IMyTerminalBlock = Sandbox.ModAPI.IMyTerminalBlock;
using IMyCargoContainer = Sandbox.ModAPI.Ingame.IMyCargoContainer;
using IMyRefinery = Sandbox.ModAPI.IMyRefinery;
using IMyTextSurface = Sandbox.ModAPI.IMyTextSurface;
using System.Text;
using System.Reflection.Metadata;

/*
 * Must be unique per each script project.
 * Prevents collisions of multiple `class Program` declarations.
 * Will be used to detect the ingame script region, whose name is the same.
 */
namespace InvMan {

/*
 * Do not change this declaration because this is the game requirement.
 */
public sealed class Program : MyGridProgram {

    #region InvMan

    // Constants
    string NO_DATA = "No_Data";
    
    long TERMINAL_FONT = 1147350002; // Font
    //string TERMINAL_FONT_NAME = "Monospace";

    Dictionary<string, List<IMyCargoContainer>> CONTAINERS;    
    Dictionary<string, List<IMyTextPanel>>      LCDS;
    Dictionary<IMyTextPanel, Vector2>           LCD_SIZES; // in characters
List<string> LCD_TEST = new List<string>();
    List<IMyRefinery>                           REFINERIES;

    // Tick data
    Dictionary<string, List<MyInventoryItem>>   ITEMS;
    string test = "";

    public void Save() { }

    // constructor
    public Program() {
        Runtime.UpdateFrequency = UpdateFrequency.Update100;

        // Initialize Constant Dictionaries
        CONTAINERS = new Dictionary<string, List<IMyCargoContainer>>();
        LCDS = new Dictionary<string, List<IMyTextPanel>>();
        LCD_SIZES = new Dictionary<IMyTextPanel, Vector2>();
        REFINERIES = new List<IMyRefinery>();

        // Get all blocks
        List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>(); 
        GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(blocks);
        
        // Setup dictionaries        
        CONTAINERS.Add( NO_DATA, new List<IMyCargoContainer>() ); // Add Defaults for undefined custom data
        
        foreach ( IMyTerminalBlock b in blocks ){
            string data = String.IsNullOrEmpty(b.CustomData) ? NO_DATA : b.CustomData.ToLower();

            // Containers
            if ( b is IMyCargoContainer ){
                if ( CONTAINERS.ContainsKey(data) ){
                    CONTAINERS[data].Add( (IMyCargoContainer)b );
                }else{
                    CONTAINERS.Add( data, new List<IMyCargoContainer>(){ (IMyCargoContainer)b } );
                }

            // LCDS
            }else if ( b is IMyTextPanel & data != NO_DATA ){
                var panel = (IMyTextPanel)b;

                // Set TextPanel atts here
                panel.SetValue("Font", TERMINAL_FONT);

                if ( LCDS.ContainsKey(data) ){
                    LCDS[data].Add( panel );
                }else{                    
                    LCDS.Add( data, new List<IMyTextPanel>(){ panel } );
                }
            
            // Refineries
            }else if ( b is IMyRefinery /* & data != NO_DATA */){
                REFINERIES.Add( (IMyRefinery)b );
            }
        }

        // LCD sizes * This assumes the font is monospace *
        foreach ( KeyValuePair<string, List<IMyTextPanel>> lcd_category in LCDS ){
            
            foreach( IMyTextPanel panel in lcd_category.Value ){
                var surface = (IMyTextSurface) panel;
                Vector2 surface_size = surface.SurfaceSize;

                StringBuilder test_builder = new StringBuilder("x", 1 );
                Vector2 test_size = surface.MeasureStringInPixels( test_builder, surface.Font, surface.FontSize );

                LCD_TEST.Add($"\n{panel.CustomName}");
                LCD_TEST.Add($"\nSize: {surface_size}");
                LCD_TEST.Add($"\nCharSize: {test_size}\n");
                
            }
        }
    }    

    
    /////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////   HELPERS /////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////
    
    // TypeId strings are junky (GameComponent_Ore or something...) take first part out and the _ ( ...so -> Ore)
    Func<string, string> UnjunkId = s => s.Substring( s.LastIndexOf("_") + 1 );

    // Add spaces to TitleCaseString -> (Title Case String)
    Func<string, string> Spacify = s => {
        string ret = "";
        foreach ( char c in s ) ret += $"{( char.IsUpper(c) ? $" {c.ToString()}" : c.ToString() )}";
        return ret;
    };
    
    // Move Item from give to recieve. return success
    public bool MoveItem( IMyInventory give, IMyInventory recieve, MyInventoryItem item ){
        if ( recieve.CanItemsBeAdded(item.Amount, item.Type) & give.CanTransferItemTo(recieve, item.Type) ){
            return recieve.TransferItemFrom( give, item );
        }
        return false;
    }
    
    public void ProcessInventory( IMyCargoContainer container, string donateCategory, int priority=0 ){           
        var owner = (IMyInventoryOwner) container;
        var inventory = (IMyInventory) owner.GetInventory(0);
        List<MyInventoryItem> items = new List<MyInventoryItem>();
        inventory.GetItems( items );
        items.Reverse(); // go from end -> start in inventory        
        foreach( MyInventoryItem item in items ){
            string type_id = UnjunkId( item.Type.TypeId );
            string subtype_id = item.Type.SubtypeId;
            bool moved = false;
            // Item is where it should be
            if ( type_id.ToLower().Contains(donateCategory) ){
                test += item.ToString();
                // Move to lower priority value if possible
                if ( priority > 0 ){
                    IMyInventory higherContainerInventory = CONTAINERS[donateCategory][priority-1].GetInventory(0); // Clean ??
                    moved = MoveItem(inventory, higherContainerInventory, item);
                }
            // Try to move the item to a better container                  
            }else{
                foreach ( var entry in CONTAINERS ){                    
                    if ( type_id.ToLower().Contains(entry.Key) ){
                        foreach( var target_container in entry.Value ){                        
                            moved = MoveItem(inventory, target_container.GetInventory(0), item);
                            if (moved){ break; }                         
                        }                   
                    }
                    if (moved){ break; }
                }                                                           
            }
            // Item has not moved. Store item and check longest name length
            if ( !moved ){                
                if (!ITEMS.ContainsKey(type_id)){
                    ITEMS.Add( type_id, new List<MyInventoryItem>() );
                }
                ITEMS[type_id].Add(item);
            }
        }// end item loop
    }

        
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////    
    //////////////////////////////////// MAIN LOOP ///////////////////////////////////////////////////////////////////////////////    
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////    
    public void Main(string argument, UpdateType updateSource) {
        test = "";

        // Items dict reset
        ITEMS = new Dictionary<string, List<MyInventoryItem>>();

        // Dump / Unset containers !! first !!
        if ( CONTAINERS[NO_DATA].Any() ){
            foreach( IMyCargoContainer container in CONTAINERS[NO_DATA] ){
                ProcessInventory( container, NO_DATA );
            }
        }
        // Named Containers Inventory
        foreach ( KeyValuePair<string, List<IMyCargoContainer>> entry in CONTAINERS ){
            if (entry.Key == NO_DATA){ continue; }// Skip no data bc we already did it

            for ( int i=0; i<entry.Value.Count; i++ ){
                ProcessInventory( entry.Value[i], entry.Key, i );
            }
        }

        // Build LCD strings
        foreach( KeyValuePair<string, List<IMyTextPanel>> entry in LCDS ){

            // Item screens
            if ( entry.Key == "items" ){
                List<string> item_string = new List<string>();
                //var item_vals

                // ** Note the spaces at the front for formatting
                foreach( KeyValuePair<string, List<MyInventoryItem>> category in ITEMS ){
                    string appended = CONTAINERS.ContainsKey( category.Key.ToLower() ) ? "" : "● ";
                    //item_string.Add( $" {appended}{category.Key}:" );
                    
                    foreach( MyInventoryItem i in category.Value ){

                        
                        item_string.Add( $"{ Spacify(i.Type.SubtypeId) }―{i.Amount}" );
                    }
                }
                foreach( IMyTextPanel panel in entry.Value ){
                    panel.WriteText("Items:\n" );
                    panel.WriteText(string.Join( Environment.NewLine, item_string), true );
                }
            }


        }


        //Echo( ITEMS.Count() );
        Echo( string.Join(Environment.NewLine, LCD_TEST) );
        Echo( test );

    }

    #endregion // InvMan
}}