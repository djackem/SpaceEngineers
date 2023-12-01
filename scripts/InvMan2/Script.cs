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
using IMyBatteryBlock = Sandbox.ModAPI.IMyBatteryBlock;
using IMyTextSurfaceProvider = Sandbox.ModAPI.IMyTextSurfaceProvider;
using System.Numerics;
using Vector2 = VRageMath.Vector2;
using System.Drawing;
using Color = System.Drawing.Color;
using RectangleF = VRageMath.RectangleF;

namespace InvMan2 {
public sealed class Program : MyGridProgram {
    #region InvMan2

    /////////////////////////////////////////////////// GLOBALS //////////////////////////////////////
    string NAME = "[X]";   
    string SEPARATOR = "——-——";
        
    Dictionary<string, List<IMyCargoContainer>> CARGO = new Dictionary<string, List<IMyCargoContainer>>();
    Dictionary<string, List<IMyTextSurface>>    PANEL = new Dictionary<string, List<IMyTextSurface>>();
    Dictionary<string, List<MyInventoryItem>>   ITEMS = new Dictionary<string, List<MyInventoryItem>>();
    List<IMyBatteryBlock>                       BATTS = new List<IMyBatteryBlock>();
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
                var surface = (IMyTextSurface)block;
                surface.ContentType = ContentType.SCRIPT;
                surface.Script = "";
                if ( !PANEL.ContainsKey(data) ) PANEL.Add( data, new List<IMyTextSurface>() );
                PANEL[data].Add( surface );
                
            }else if ( block is IMyBatteryBlock ){
                BATTS.Add( (IMyBatteryBlock)block );
            
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
       // ECHO += refinery.CustomName;
    }

    public string CreatePercentBar( double percent ){
        string r = "";        
        for ( decimal i=0; i<1; i+=(decimal)0.25 ){// Change for more chars
            r += i < (decimal)percent ? "|" : ".";
        }
        return r;
    }

    public MySprite CreateSprite( string write, IMyTextSurface panel, Vector2 position, ref Vector2 last_sprite_size ){
        var sprite = new MySprite() {
            Type = SpriteType.TEXT,
            Data = write,
            Position = position,
            RotationOrScale = 1f,
            Color = panel.FontColor,
            Alignment = TextAlignment.CENTER,
            FontId = panel.Font
        };
        last_sprite_size = StringSize(write, panel);
        return sprite;
    }

    public void UpdatePanel( IMyTextSurface panel, string category ){
        var viewport = new RectangleF( (panel.TextureSize - panel.SurfaceSize)/2, panel.SurfaceSize );
        Vector2 position = new Vector2( viewport.Width/2, 10 ); // Starting pos
        List<MySprite> sprites = new List<MySprite>();
        Vector2 last_sprite_size = new Vector2(0,0);
        string write;
        
        Func<MyFixedPoint, string>  FormatAmount = n => n > 1000 ? $"{(int)n/1000}k" : n.ToString();        
        
        if ( category == "items" ){
            foreach( KeyValuePair<string, List<MyInventoryItem>> entry in ITEMS ){
                sprites.Add( CreateSprite( 
                    CreateSpacedString(new List<string>(){ $"{entry.Key}:", " ", ""}, panel), panel, position, ref last_sprite_size )); // Titles
                position.Y += last_sprite_size.Y + 5;
                
                foreach( MyInventoryItem item in entry.Value ){ // Name --- Amount
                    write = CreateSpacedString( new List<string>() { 
                        $"{item.Type.SubtypeId} ", SEPARATOR, $" {FormatAmount(item.Amount)}" }, panel);
                    
                    sprites.Add( CreateSprite( write, panel, position, ref last_sprite_size ));
                    position.Y += last_sprite_size.Y + 5;
                }
            }
        }else if ( ITEMS.ContainsKey( UpperCase(category) ) ){
            sprites.Add( CreateSprite( $"{UpperCase(category)}:", panel, position, ref last_sprite_size )); // Titles
            position.Y += last_sprite_size.Y + 5; 
            // Lookup Item category
            foreach( MyInventoryItem item in ITEMS[UpperCase(category)] ){
                write = CreateSpacedString( new List<string>() { 
                        $"{item.Type.SubtypeId} ", SEPARATOR, $" {FormatAmount(item.Amount)}" }, panel);
                sprites.Add( CreateSprite( write, panel, position, ref last_sprite_size ));
                position.Y += last_sprite_size.Y + 5;
            }

        }else if (category == "battery"){
            foreach( IMyBatteryBlock battery in BATTS ){
                sprites.Add( CreateSprite( $"{battery.CustomName} ( {battery.ChargeMode} ):", panel, position, ref last_sprite_size ) );
                position.Y += last_sprite_size.Y + 5;
                
                // Input %
                var input_current = Math.Round(battery.CurrentInput,2);
                var input_max = Math.Round(battery.MaxInput,2);                
                var input_percent = (input_current / input_max) * input_max;
                write = CreateSpacedString( new List<string>() { 
                        $"Input {Math.Floor(input_percent*100)}% [", CreatePercentBar( input_percent ), $"] {input_current}/{input_max}" }, panel);
                sprites.Add( CreateSprite( write, panel, position, ref last_sprite_size ) );
                position.Y += last_sprite_size.Y + 5;
                
                // Stored %
                var batt_current = Math.Round(battery.CurrentStoredPower,1);
                var batt_max = Math.Round(battery.MaxStoredPower,1);                
                var batt_percent = Math.Max( (batt_current / batt_max) * batt_max, 1 );
                write = CreateSpacedString( new List<string>() { 
                        $"Input {Math.Floor(input_percent*100)}% [", CreatePercentBar( batt_percent ), $"] {batt_current}/{batt_max}" }, panel);
                sprites.Add( CreateSprite( write, panel, position, ref last_sprite_size ) );
                position.Y += last_sprite_size.Y + 5;
            }

        
        }

        // Write to panel
        //panel.WriteText("");
        /* var viewport = new RectangleF( (panel.TextureSize - panel.SurfaceSize)/2, panel.SurfaceSize );
        Vector2 position = new Vector2( viewport.Width/2, 10 ); // Starting pos
        string write_this = "";*/
        var frame = panel.DrawFrame();
        foreach ( MySprite sprite in sprites ){
            frame.Add( sprite );
        }
        frame.Dispose();
        
        /* foreach( List<string> line in sprites ){     
            var sprite_size = new Vector2(0,0);
            if ( line.Count() == 1 ){
                write_this = line[0];
                sprite_size = StringSize(write_this, panel);
            }else if( line.Count()==3 ){
                write_this = CreateSpacedString( line, panel );                
            }
            var sprite = MySprite.CreateText( write_this, panel.Font, panel.FontColor );
            sprite.Position = position;
            frame.Add( sprite );
            position.Y += StringSize(write_this, panel).Y + 5;// Vertical space between sprites
        }
        
        frame.Dispose();
        ECHO += $"{panel.DisplayName} : {category} : {position}\n";
        try { throw new InvalidOperationException("break my point"); } catch(Exception) {} */
    }

    Func<string, IMyTextSurface, Vector2> StringSize =
            ( strn, panl )=> panl.MeasureStringInPixels(new StringBuilder(strn), panl.Font, panl.FontSize );

    public string CreateSpacedString( List<string> str, IMyTextSurface panel ){
        var final_string = string.Join( "", str );
        var viewport = new RectangleF( (panel.TextureSize - panel.SurfaceSize) / 2f, panel.SurfaceSize );        
        
        if ( str.Count >= 2 ){
            float head_width = (float)Math.Ceiling(StringSize( str[0], panel ).X);
            float tail_width = (float)Math.Ceiling(StringSize( str[str.Count-1], panel ).X);            
            float separator_space = (viewport.Width) - (float)Math.Floor(head_width+tail_width);            
            float separator_space_total = (float)separator_space;
            
            if ( separator_space <= 0 ){ // No room for even 1 sep, write "headtail" cross your fingers
                final_string = $"{str[0]}{str[str.Count-1]}";            
            
            }else{ // Build (stretched) separator                
                string sep_final = "";
                float percentage_covered = 0;
                char[] sep_list = str[1].ToCharArray();

                while ( separator_space > 0 ){
                    int index = (int)Math.Round( (double)(sep_list.Length-1) * percentage_covered );
                    string character = sep_list[index].ToString();
                    float character_width = StringSize( character, panel ).X;                    
                    separator_space -= character_width;
                    percentage_covered = (separator_space_total - separator_space) / separator_space_total;
                    if ( percentage_covered > .95 ) {
                        //sep_final = sep_final.Remove(0, 1);// Knock first off if over
                        break;
                    }else{
                        sep_final += character;
                    }                   
                }
                final_string = $"{str[0]}{sep_final}{str[str.Count-1]}";
            }            
        }        
        //panel.WriteText( $"{final_string}\n", true );
        return final_string;
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
        
        foreach( IMyBatteryBlock battery in BATTS ){            
                     
        }
                
        foreach( KeyValuePair<string, List<IMyTextSurface>> entry in PANEL ){
            //ECHO += $"\n{entry.Key}\n{string.Join(Environment.NewLine, entry.Value)}";
            foreach( IMyTextSurface panel in entry.Value ){
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