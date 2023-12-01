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

/*
 * Must be unique per each script project.
 * Prevents collisions of multiple `class Program` declarations.
 * Will be used to detect the ingame script region, whose name is the same.
 */
namespace ScreenDraw {

public sealed class Program : MyGridProgram {
    #region ScreenDraw

    IMyTextSurface _drawingSurface;
    RectangleF _viewport;

    public Program() {
        _drawingSurface = Me.GetSurface(0);
        Runtime.UpdateFrequency = UpdateFrequency.Update100;

        // Calculate the viewport offset by centering the surface size onto the texture size
        _viewport = new RectangleF(
            (_drawingSurface.TextureSize - _drawingSurface.SurfaceSize) / 2f,
            _drawingSurface.SurfaceSize
        );
    }

    public void Save() {}

    // Drawing Sprites
public void DrawSprites(ref MySpriteDrawFrame frame)
{
    // Set up the initial position - and remember to add our viewport offset
    var position = new Vector2(256, 20) + _viewport.Position;

    // Create background sprite
    var sprite = new MySprite()
    {
        Type = SpriteType.TEXTURE,
        Data = "Grid",
        Position = _viewport.Center,
        Size = _viewport.Size,
        Color = Color.White.Alpha(0.66f),
        Alignment = TextAlignment.CENTER
    };
    frame.Add(sprite);

    //try { throw new InvalidOperationException("break my point"); } catch(Exception) {}
    

    // Create our first line
    sprite = new MySprite()
    {
        Type = SpriteType.TEXT,
        Data = "Line 1",
        Position = position,
        RotationOrScale = 0.8f /* 80 % of the font's default size */,
        Color = Color.Red,
        Alignment = TextAlignment.CENTER /* Center the text on the position */,
        FontId = "White"
    };
    // Add the sprite to the frame
    frame.Add(sprite);
    
    // Move our position 20 pixels down in the viewport for the next line
    position += new Vector2(0, 20);

    // Create our second line, we'll just reuse our previous sprite variable - this is not necessary, just
    // a simplification in this case.
    sprite = new MySprite()
    {
        Type = SpriteType.TEXT,
        Data = "Line 2",
        Position = position,
        RotationOrScale = 0.8f,
        Color = Color.Blue,
        Alignment = TextAlignment.CENTER,
        FontId = "White"
    };
    // Add the sprite to the frame
    frame.Add(sprite);
}

    public void Main(string argument, UpdateType updateSource) {
        var frame = _drawingSurface.DrawFrame();
        DrawSprites(ref frame);

            // We are done with the frame, send all the sprites to the text panel
            frame.Dispose();
    }

    #endregion // ScreenDraw
}}