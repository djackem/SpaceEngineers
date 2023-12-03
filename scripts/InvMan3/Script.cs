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
using System.Collections.Generic;
using Sandbox.Game.Localization;
using Sandbox.Game.Entities.Cube;
using VRage.Game.ModAPI;
using IMyInventoryItem = VRage.Game.ModAPI.IMyInventoryItem;
using IMyInventory = VRage.Game.ModAPI.IMyInventory;
using System.Linq;

/*
 * Must be unique per each script project.
 * Prevents collisions of multiple `class Program` declarations.
 * Will be used to detect the ingame script region, whose name is the same.
 */
namespace InvMan3 {

/*
 * Do not change this declaration because this is the game requirement.
 */
public sealed class Program : MyGridProgram {
    #region InvMan3
    string NAME = "[X]";
    string ALL = "all";
    public string[] PANEL_MODES = new string[]{"info", "inventory", "battery"};
    

    string INFO = "";
    string ECHO = "";

    Dictionary<string, List<PanelBlock> >       PANEL = new Dictionary<string, List<PanelBlock>>();
    Dictionary<string, List<CargoBlock> >       CARGO = new Dictionary<string, List<CargoBlock>>();
    Dictionary<string, List<MyInventoryItem> >  ITEMS = new Dictionary<string, List<MyInventoryItem>>();
    
    /////////////////////////////////////////////////////////////////////////////////////////////// Classes ////
    public class CargoBlock{
        public string[] Data = null;
        public List<MyInventoryItem> InventoryItems{ get; set; }
        public IMyInventory Inventory { get; private set; }
        public IMyTerminalBlock Block{ get; private set; }
        
        public CargoBlock( IMyCargoContainer block ){
            Data = block.CustomData.ToLower().Split(' ');
            Block = block;
            Inventory = (IMyInventory)block.GetInventory(0);
            UpdateInventory();
        }

        public List<MyInventoryItem> UpdateInventory(){ // Update inventory, return fresh list 
            // TODO : and update % vals
            InventoryItems = new List<MyInventoryItem>();            
            Inventory.GetItems(InventoryItems);
            InventoryItems.Reverse();           
            return InventoryItems;
        }

        public bool GiveItem( MyInventoryItem item, List<CargoBlock> cargo_blocks ){
            foreach ( CargoBlock block in cargo_blocks ){
                if ( block.Inventory.CanItemsBeAdded( item.Amount, item.Type ) & Inventory.CanTransferItemTo( block.Inventory, item.Type ) ) {
                    bool moved = block.Inventory.TransferItemFrom( Inventory, item );
                    if ( moved ) return true;
                }
            };
            return false;
        }
    }

    public class PanelBlock{
        public List<IMyTextSurface> Surfaces { get; private set; }
        public IMyTerminalBlock Block { get; private set; }
        public string[] Data = null;
        // constructor
        public PanelBlock( IMyTerminalBlock block ){
            Block = block;            
            Data = block.CustomData.ToLower().Split(' ');            
            // Retrieve surfaces and set them to script, with no script set
            Surfaces = new List<IMyTextSurface>(){}; 
            if ( block is IMyTextSurfaceProvider ){
                var provider = (IMyTextSurfaceProvider)block;
                for( int i=0; i<provider.SurfaceCount; i++ ){
                    var surface = provider.GetSurface(i);                    
                    surface.ContentType = ContentType.SCRIPT;                                      
                    Surfaces.Add( surface );
                }
            }
        }
        public override string ToString(){ return $"PanelBlock: {Block.CustomName}\nData:[{string.Join(", ", Data)}]\n{Surfaces.Count} Surfaces"; }
    }
    /////////////////////////////////////////////////////////////////////////////////////////////// Helper ////
    public string GetTypeID( MyInventoryItem item ){ // Get TypeId of item without ObjectBuilderXXX ( ie "ore", "component", etc ) (lower case)
        var long_id = item.Type.TypeId.ToLower();
        return long_id.Substring( long_id.LastIndexOf("_") + 1); 
    }


   /////////////////////////////////////////////////////////////////////////////////////////////// Constructor ////
    public Program() {
        Runtime.UpdateFrequency = UpdateFrequency.Update100;
        string block_errors = "";
        
        List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>(); 
        GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(blocks);

        foreach( IMyTerminalBlock block in blocks ){
            if ( !block.CustomName.Contains( NAME ) ) continue;
            string data; // CustomData

            // LCD Panels
            if ( block is IMyTextSurfaceProvider ){
                var panel_block = new PanelBlock( block );
                // If successful block found - Check customdata for a category
                if ( panel_block.Surfaces.Count>0 ){
                    data = panel_block.Data[0];
                    if ( PANEL_MODES.Contains( data ) ){
                        if ( !PANEL.ContainsKey(data) ) PANEL[data] = new List<PanelBlock>();
                        PANEL[data].Add( panel_block );
                        INFO += $"Created: {panel_block.Block.CustomName}\n";
                    }else{
                        block_errors += $"ERROR - Category not found:\n -Panel:{panel_block.ToString()}\n -Category:{data}\n";
                    }
                }
            
            // Cargo Containers
            }else if ( block is IMyCargoContainer ){
                var cargo_block = new CargoBlock( (IMyCargoContainer)block );
                data = cargo_block.Data[0];
                if ( !CARGO.ContainsKey(data) ) CARGO.Add( data, new List<CargoBlock>() );
                CARGO[data].Add( cargo_block );
                //INFO += data + "\n";
            }
        }
        Me.GetSurface(0).WriteText(INFO + block_errors);  
        Echo(INFO);
    }

    /////////////////////////////////////////////////////////////////////////////////////////////// Loop ////
    public void Main(string argument, UpdateType updateSource) {
        //ECHO = "";

        // Get all the items and move them
        var old_item_count = ITEMS.Count;
        ITEMS = new Dictionary<string, List<MyInventoryItem>>();
        foreach ( KeyValuePair<string, List<CargoBlock>> category in CARGO ){ // n
            foreach( CargoBlock container in CARGO[category.Key] ){                
                // Get all items and update inventories at same time
                foreach( MyInventoryItem item in container.UpdateInventory() ){ // n
                    var type_id = GetTypeID( item );
                    
                    // Move item that doesn't belong
                    bool moved = false;
                    string data = container.Data[0];              
                    if ( type_id != data | data == "dump" ){
                        if ( CARGO.ContainsKey(type_id) ){
                            moved = container.GiveItem( item, CARGO[type_id] ); // Give to first cargo in category
                        }                        
                    }
                    
                    if ( !moved ){
                        if ( !ITEMS.ContainsKey(type_id) ) ITEMS[type_id] = new List<MyInventoryItem>();
                        ITEMS[type_id].Add( item );
                        //ECHO += $"ITEM: {item.Type.TypeId} - {item.Amount}";
                    }
                };
                
            }
        }
        

        
        Echo(ECHO);
    }
    #endregion // InvMan3
}}