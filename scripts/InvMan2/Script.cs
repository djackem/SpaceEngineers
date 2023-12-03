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
using RectangleF = VRageMath.RectangleF;
using System.Security.Cryptography.X509Certificates;
using Rectangle = System.Drawing.Rectangle;
using Point = System.Drawing.Point;
using Color = VRageMath.Color;


namespace InvMan2 {
public sealed class Program : MyGridProgram {
    #region InvMan2

    /////////////////////////////////////////////////// GLOBALS //////////////////////////////////////
    string NAME = "[X]";   
    string SEPARATOR = "——-——";
        
    Dictionary<string, List<IMyCargoContainer>> CARGO = new Dictionary<string, List<IMyCargoContainer>>();
    Dictionary<string, List<IMyTextSurface>>    PANEL = new Dictionary<string, List<IMyTextSurface>>();
    
    Dictionary<long, List<MySprite>>            PANEL_OVERFLOW = new Dictionary<long, List<MySprite>>();

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
                PANEL_OVERFLOW[ block.EntityId ] = new List<MySprite>();
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

    public Vector2 StringSize(string strn, IMyTextSurface panl, float scale=1){
        return panl.MeasureStringInPixels(new StringBuilder(strn), panl.Font, panl.FontSize * scale );
    }
    
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

    /* public void UpdateRefinery( IMyRefinery refinery, string category ){
       // ECHO += refinery.CustomName;
    } */

    public string CreatePercentBar( double percent ){
        string r = "";        
        for ( decimal i=0; i<1; i+=(decimal)0.25 ){// Change for more chars
            r += i < (decimal)percent ? "|" : ".";
        }
        return r;
    }

    public MySprite CreateTextSprite( string write, IMyTextSurface panel, Vector2 position, ref Vector2 SPRITE_SIZE, float scale=1 ){
        SPRITE_SIZE = StringSize( write, panel, scale );
        var sprite = new MySprite() {
            Type = SpriteType.TEXT,
            Data = write,
            Position = position,
            RotationOrScale = scale,
            Color = panel.FontColor,
            Alignment = TextAlignment.CENTER,
            FontId = panel.Font            
        };        
        return sprite;
    }
    
    private class SuperSprite{
        public MySprite sprite;
        public Rectangle rect;
               
        private IMyTextSurface _panel;

        public SuperSprite( 
                string          data, 
                IMyTextPanel    panel,
                Vector2?        position = null, 
                float           rotation_scale=1f,
                string          font = null,
                SpriteType      type = SpriteType.TEXT, 
                Vector2?        size = null,
                Color?          color = null, 
                TextAlignment alignment = TextAlignment.CENTER
            ){

            _panel = panel;
            rotation_scale = type==SpriteType.TEXT ? rotation_scale : ( rotation_scale==1f ? 0f : rotation_scale );// If icon, change default rotation to 0
            font = font==null ? panel.Font : font;

            sprite = new MySprite(){ Type=type, Data=data, Position=position, Color=color, Alignment=alignment, Size=size, RotationOrScale=rotation_scale, FontId=font };

            if ( type == SpriteType.TEXT & size == null ){
                var string_size = panel.MeasureStringInPixels(new StringBuilder(data), panel.Font, panel.FontSize * rotation_scale );
                //rect = 
            }
            
        }
    }

    public MySprite CreateIconSprite( string icon, Rectangle rectangle, Color color ){
        //SPRITE_SIZE= new Vector2( rectangle.Width, rectangle.Height );
        var sprite = new MySprite() {
            Type = SpriteType.TEXTURE,
            Data = icon,
            Position = new Vector2( rectangle.X, rectangle.Y ),
            Color = color,
            Alignment = TextAlignment.CENTER,
            Size = new Vector2( rectangle.Width, rectangle.Height ) 
        };        
        return sprite;
    }

    public List<MySprite> CreateInvertText( string write, IMyTextSurface panel, Vector2 position, float scale, ref Vector2 SPRITE_SIZE ){
        var start = new Vector2(SPRITE_SIZE.X, SPRITE_SIZE.Y);
        //write = CreateSpacedString( new List<string>{ write, "-", "-"}, panel );

        // text sprite
        var sprite = CreateTextSprite( write, panel, position, ref SPRITE_SIZE, 1.5f );
        sprite.Color = panel.BackgroundColor;
        sprite.Position = position;
        // background sprite
        var background = CreateIconSprite("LCD_Economy_Detail", 
            new Rectangle( (int)position.X, (int)Math.Floor(position.Y+(SPRITE_SIZE.Y/2)), (int)Math.Floor(SPRITE_SIZE.X), (int)Math.Floor(SPRITE_SIZE.Y) ), panel.FontColor );
        background.Color = panel.FontColor.Alpha(0.25f);

        return new List<MySprite>(){background, sprite};
    }


    public MySprite CloneSpriteAt( MySprite sprite, Vector2 position, IMyTextSurface panel, ref Vector2 SPRITE_SIZE ){
        if (sprite.Type == SpriteType.TEXT) SPRITE_SIZE = StringSize( sprite.Data, panel );
        var return_sprite = new MySprite(){
            Type = sprite.Type,
            Data = sprite.Data,
            Position = position,
            RotationOrScale = sprite.RotationOrScale,
            Color = sprite.Color,
            Alignment = sprite.Alignment,
            FontId = sprite.FontId,
            Size = sprite.Size
        };
        return return_sprite;
    }



    public void UpdatePanel( IMyTextSurface panel, string category ){
        long id = ((IMyEntity)panel).EntityId;
        string write = "";
        var viewport = new RectangleF( (panel.TextureSize - panel.SurfaceSize)/2, panel.SurfaceSize );
        List<MySprite> sprites = new List<MySprite>();
        Vector2 position = new Vector2( viewport.Width/2, 10 ); // Starting pos
        Vector2 SPRITE_SIZE = new Vector2( position.X, position.Y ); // Used to pass the size back from creating sprites
        bool was_overflowing = false;
        
        Func<MyFixedPoint, string>  FormatAmount = n => n > 1000 ? $"{(int)n/1000}k" : n.ToString();        
        
        // If we have items in queue, use those
        if ( PANEL_OVERFLOW[id].Count > 0 ){            
            was_overflowing = true;
            PANEL_OVERFLOW[id].RemoveAt(0);            

            // We are offsetting everything by the size (position) of the first
            var offset = new Vector2(0, 0);
            if ( PANEL_OVERFLOW[id].Count>1 ) offset.Y = PANEL_OVERFLOW[id][0].Position.Value.Y;
            
            // All sprites in queue until position is outside of panel
            foreach( MySprite old_sprite in PANEL_OVERFLOW[id] ) {
                sprites.Add( CloneSpriteAt( old_sprite, (Vector2)(old_sprite.Position - offset), panel, ref SPRITE_SIZE ) );
                position.Y += SPRITE_SIZE.Y + 5;
                // Do not draw upon the unseen
                if ( position.Y > (viewport.Y + viewport.Height) ) { break; };
            }
            // If there is too much space at the end, stop and refresh
            var empty_space = viewport.Height - position.Y;
            if ( empty_space >= SPRITE_SIZE.Y ) PANEL_OVERFLOW[id] = new List<MySprite>();

        // Gets all the items everywhere
        }else if ( category == "items" ){
            foreach( KeyValuePair<string, List<MyInventoryItem>> entry in ITEMS ){
                sprites.Add( CreateTextSprite( 
                    CreateSpacedString(new List<string>(){ $"{entry.Key}:", " ", ""}, panel), panel, position, ref SPRITE_SIZE )); // Titles
                position.Y += SPRITE_SIZE.Y + 5;                
                foreach( MyInventoryItem item in entry.Value ){ // Name --- Amount
                    write = CreateSpacedString( new List<string>() { 
                        $"{item.Type.SubtypeId} ", SEPARATOR, $" {FormatAmount(item.Amount)}" }, panel);
                    
                    sprites.Add( CreateTextSprite( write, panel, position, ref SPRITE_SIZE ));
                    position.Y += SPRITE_SIZE.Y + 5;
                }
            }

        // Gets items with category set as custom data string
        }else if ( ITEMS.ContainsKey( UpperCase(category) ) ){
            //sprites.Add( CreateTextSprite( $"{UpperCase(category)}:", panel, position, ref SPRITE_SIZE )); // Titles
            sprites.AddRange( CreateInvertText($"{UpperCase(category)}:", panel, position, 1.5f, ref SPRITE_SIZE ) );

            position.Y += SPRITE_SIZE.Y + 5; 
            // Lookup Item category
            foreach( MyInventoryItem item in ITEMS[UpperCase(category)] ){
                write = CreateSpacedString( new List<string>() { 
                        $"{item.Type.SubtypeId} ", SEPARATOR, $" {FormatAmount(item.Amount)}" }, panel);
                sprites.Add( CreateTextSprite( write, panel, position, ref SPRITE_SIZE ));
                position.Y += SPRITE_SIZE.Y + 5;
            }

        // Battery Info
        }else if (category == "battery"){
            foreach( IMyBatteryBlock battery in BATTS ){
                //sprites.Add( CreateTextSprite( $"{battery.CustomName} ( {battery.ChargeMode} ):", panel, position, ref SPRITE_SIZE ) );
                sprites.AddRange(
                    
                    CreateInvertText( $" {battery.CustomName}",  
                        //CreateSpacedString( new List<string>(){ $" {battery.CustomName}", "-", " " }, panel ), */

                    panel, position, 1f, ref SPRITE_SIZE )
                );
                position.Y += SPRITE_SIZE.Y + 5;

                
                // Input %                
                var input_current = Math.Round(battery.CurrentInput,2);
                var input_max = Math.Round(battery.MaxInput,2);                
                var input_percent = (input_current / input_max) * input_max;
                write = CreateSpacedString( new List<string>() { 
                        $"Input {Math.Floor(input_percent*100)}% [", CreatePercentBar( input_percent ), $"] {input_current}/{input_max}" }, panel);
                sprites.Add( CreateTextSprite( write, panel, position, ref SPRITE_SIZE ) );
                position.Y += SPRITE_SIZE.Y + 5;                
                
                // Stored %
                var batt_current = Math.Round(battery.CurrentStoredPower,1);
                var batt_max = Math.Round(battery.MaxStoredPower,1);             
                var batt_percent = Math.Round(batt_current / batt_max, 2);
                write = CreateSpacedString( new List<string>() { 
                        $"Battery {batt_percent*100}% [", CreatePercentBar( batt_percent ), $"] {batt_current}/{batt_max}" }, panel);
                sprites.Add( CreateTextSprite( write, panel, position, ref SPRITE_SIZE ) );
                position.Y += SPRITE_SIZE.Y + 5;
                ECHO += $"{batt_current} : {batt_max} : {batt_percent}\n";                              
            }        
        }

        // Write to panel
        var frame = panel.DrawFrame();
        for ( int i=0; i<sprites.Count; i++ ){
            var sprite = sprites[i];            
            // If height is too much and we didn't already know, add to queue
            if ( !was_overflowing & sprite.Position?.Y > viewport.Y + viewport.Height ){
                PANEL_OVERFLOW[id] = sprites;
                break;
            }
            frame.Add( sprite );
        }
        frame.Dispose();
    }
    
    

    /* public string CreateTitleString( string title, IMyTextSurface panel ){
        return CreateSpacedString( new List<string>(){ $" {title}", " ", "" }, panel); 
    } */

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