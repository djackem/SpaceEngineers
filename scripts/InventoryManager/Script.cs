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
using System.Reflection.Metadata;
using Sandbox.ModAPI;

using IMyCargoContainer = Sandbox.ModAPI.IMyCargoContainer;
using IMyTerminalBlock = Sandbox.ModAPI.IMyTerminalBlock;
using IMyTextSurface = Sandbox.ModAPI.IMyTextSurface;
using IMyTextPanel = Sandbox.ModAPI.IMyTextPanel;
using Microsoft.VisualBasic;
using IMyRefinery = Sandbox.ModAPI.IMyRefinery;
using System.Runtime.CompilerServices;
using VRage.Game.ModAPI.Ingame.Utilities;


namespace InventoryManager {    

public sealed class Program : MyGridProgram {
    #region InventoryManager

    Dictionary<string, List<IMyTerminalBlock>> active;
    List<string>cats = new List<string>(){
        "dump", "ore", "ingots"
    };

    public Program() {
        Runtime.UpdateFrequency = UpdateFrequency.Update100;
        UpdateGrid();
    }
        
    public void Save() {
        UpdateGrid();
    }

    public void UpdateActive( string category, IMyTerminalBlock block ){
        if ( !active.ContainsKey(category) ){
            active.Add( category, new List<IMyTerminalBlock>(){ block } );
        }else{
            active[category].Add( block );
        }
    }

    public void UpdateGrid(){
        active = new Dictionary<string, List<IMyTerminalBlock>>();
        List<IMyTerminalBlock> all_terminal_blocks  = new List<IMyTerminalBlock>();
        GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(all_terminal_blocks);

        // Container list
        foreach ( IMyTerminalBlock block in all_terminal_blocks ){
            if ( block is IMyRefinery ){
                UpdateActive("refine", block);

            }else if (  block.CustomData.Length != 0 & 
                        cats.Contains( block.CustomData ) &
                        block is IMyCargoContainer | block is IMyTextPanel ){

                UpdateActive(block.CustomData, block);
            }
        }
    }

    public string GetInv( string category ){
        string ret = "";
        
        return ret;
    }


    public void Main(string argument, UpdateType updateSource) {
        string text = "";
        
        
        foreach( KeyValuePair<string, List<IMyTerminalBlock>> category in active ){
            text += $"{category.Key}\n";

            //Dictionary<IMyCargoContainer, IMyInventory> inv = new Dictionary<IMyCargoContainer, IMyInventory>();
            List<IMyTextPanel> panels = new List<IMyTextPanel>();

            foreach( IMyTerminalBlock block in category.Value){
                text += $" - {block.CustomName}\n";
                if ( block is IMyTextPanel ){
                    panels.Add(block as IMyTextPanel);
                }else if( block is IMyCargoContainer ) {
                    IMyCargoContainer container = block as IMyCargoContainer;
                    var inv = container.GetInventory();   
                    text += $"{inv.ItemCount}";                 
                    for ( int i=inv.ItemCount-1; i>0; i-- ){
                        text += inv.ItemCount;
                        MyInventoryItem? item = inv.GetItemAt(i);
                        if (item != null){
                            text += $"{i} item: {item.ToString()}";
                        }
                    }
                }
            }
            // Process Inventories

        }
               
        Echo(text); 
        
    }

    #endregion // InventoryManager
}}