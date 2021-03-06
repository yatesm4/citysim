﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;

using CitySimAndroid.Content;
using CitySimAndroid.States;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Comora;
using Microsoft.Xna.Framework.Input.Touch;

namespace CitySimAndroid.Objects
{
    // tiledata for tiles
    // this data is used for savedata, and for loading / generating maps mostly
    public class TileData
    {
        public Vector2 TileIndex { get; set; } = new Vector2(0, 0);
        public Vector2 Position { get; set; } = new Vector2(0, 0);
        public int TerrainId { get; set; } = 0;
        public TileObject Object { get; set; }

        public bool IsVisible { get; set; } = false;
        public bool IsActive { get; set; } = false;
    }

    public class Tile
    {
        public GameContent Content { get; set; }
        public GraphicsDevice GraphicsDevice_ { get; set; }

        // debug texture for drawing hitbox (click area)
        public Texture2D DebugRect { get; set; }

        // basic properties related to tile (tiledata, position, scale, object, texture)
        #region TILE PROPERTIES
        // this tile's respective tiledata (will match tile properties)
        public TileData TileData { get; set; }

        // object (building or resource) belong to this tile
        public TileObject Object { get; set; }

        // the type of terrain on this tile (0 = Grass, 1 = Dirt, 2 = Water)
        public int TerrainId { get; set; }

        // index of the tile (0-MapWidth,0-MapHeight)
        public Vector2 TileIndex { get; set; }

        // the texture of this tile
        public Texture2D Texture { get; set; }

        // texture for representative object
        public Texture2D ObjectTexture { get; set; }

        // tile position
        public Vector2 Position { get; set; } = new Vector2(0, 0);

        // center point of the tile (used later for npcs moving tile to tile)
        public Vector2 CenterPoint => Position + new Vector2(16, 12);

        // scale to draw the tile at
        public Vector2 Scale { get; set; } = new Vector2(10f, 10f);
        #endregion

        // animated tile properties
        #region ANIMATION PROPERTIES
        public Texture2D Anim_Texture { get; set; }
        public bool HasAnimatedTexture => Anim_Texture != null;
        public float Anim_Time { get; set; } = 0.0f;
        public float Anim_FrameTime = 0.25f;
        public int Anim_FrameIndex = 0;
        public int Anim_FrameCount => HasAnimatedTexture ? Anim_Texture.Width / Texture.Width : 0;
        #endregion

        // destruction properties
        #region DESTRUCTION PROPERTIES
        public bool ObjectDestroyed { get; set; } = false;
        public Texture2D FX_Destroyed_Anim_Texture { get; set; }
        public float FX_Destroyed_Anim_Time { get; set; } = 0;
        public float FX_Destroyed_Anim_FrameTime = 0.25f;
        public int FX_Destroyed_Anim_FrameIndex = 0;
        public int FX_Destroyed_Anim_FrameCount => FX_Destroyed_Anim_Texture.Width / Texture.Width;
        #endregion

        // is the tile interactable, is it hovered, and the hover properties
        #region INTERACTION & VISIBILITY PROPERTIES
        public bool IsInteractable { get; set; } = false;
        public bool IsHovered { get; set; } = false;
        public Color DrawColor { get; set; } = Color.White;

        public bool IsVisible { get; set; } = false;
        public bool IsGlowing { get; set; } = false;

        private const float _touchTimeCycleDelay = 1f;
        private float _touchRemainingDelay = _touchTimeCycleDelay;
        #endregion

        // properties for when roads are placed on tile (road previewing, etc)
        #region ROAD PLACEMENT PROPERTIES
        public bool IsPreviewingRoad { get; set; } = false;
        public Texture2D LastSavedRoadTexture { get; set; } = null;
        #endregion

        // touch state, mouse state, game state, etc
        #region STATE PROPERTIES
        private GameState _gameState { get; set; }

        private MouseState _previousMouseState { get; set; }

        private TouchCollection _previousTouch;
        private TouchCollection _currentTouch;
        #endregion

        // used to determine when clicked
        #region EVENT HANDLERS
        public event EventHandler Click;
        public event EventHandler RightClick;
        public event EventHandler Pressed;
        public event EventHandler Pressing;
        #endregion

        // properties for interaction hitbox, etc
        #region TOUCH PROPERTIES
        // hitbox for mouse touch
        public Rectangle TouchHitbox => new Rectangle((int)Position.X + 16, (int)Position.Y + (83 * (int)Scale.X), 18 * (int)Scale.X, 10 * (int)Scale.X);
        #endregion

        // tile constructor, pass a gamecontent manager and tiledata to load from
        public Tile(GameContent content_, GraphicsDevice graphicsDevice_, TileData tileData_)
        {
            Content = content_;
            GraphicsDevice_ = graphicsDevice_;
            TileData = tileData_;
            Position = tileData_.Position;
            TileIndex = tileData_.TileIndex;
            TerrainId = tileData_.TerrainId;
            // set object from tiledata if not null, otherwise generate default tileobject
            Object = tileData_.Object ?? new TileObject();
            // get the texture to render from the gamecontent manager using the TextureIndex from the tile's tileobject - if not null, otherwise default to 3 (grass)

            int texture_for_terrain = 0;
            switch (TerrainId)
            {
                case 0:
                    texture_for_terrain = 1;
                    break;
                case 1:
                    texture_for_terrain = 2;
                    break;
                case 2:
                    texture_for_terrain = 4;
                    break;
            }

            Texture = content_.GetTileTexture(texture_for_terrain);

            FX_Destroyed_Anim_Texture = content_.GetTileTexture(-1);

            IsVisible = tileData_.IsVisible;

            // set DebugRect data (optional w debug options)
            DebugRect = new Texture2D(graphicsDevice_, 1, 1);
            DebugRect.SetData(new[] { Color.Red });

            // initialize previous mouse state w current mouse state (not really that big of a deal as it will only make one frame behave odd and that frame is over with before user even notices unless their pc is literally a piece of shit)
            _previousMouseState = Mouse.GetState();
        }

        // update
        // - check for mouse hovering and click (select)
        public void Update(GameTime gameTime, KeyboardState keyboardState, Camera camera, GameState state)
        {
            _previousTouch = _currentTouch;
            _currentTouch = state.CurrentTouch;

            // update tile?
            IsGlowing = false;
            IsPreviewingRoad = false;

            #region TOUCH INTERACTION LOGIC
            if (IsVisible)
            {
                if (_touchRemainingDelay > 0)
                {
                    var touch_timer = (float)gameTime.ElapsedGameTime.TotalSeconds;
                    _touchRemainingDelay -= touch_timer;
                    if (_touchRemainingDelay <= 0)
                    {
                        _touchRemainingDelay = 0;
                    }
                }
                else
                {
                    foreach (var tl in _currentTouch)
                    {
                        // get the screen position of the touch
                        var t_screenPosition = new Vector2(tl.Position.X, tl.Position.Y);
                        // construct rect to represent touch
                        var t_screenRect = new Rectangle((int)t_screenPosition.X, (int)t_screenPosition.Y, 1, 1);
                        // dont continue if touch intersects hud/ui
                        if (t_screenRect.Intersects(state.GameHUD.DisplayRect) || t_screenRect.Intersects(state.Gamepad.GamePadUnionHitbox)) continue;
                        // convert touch screen pos to in-world pos
                        var t_worldPosition = Vector2.Zero;
                        camera.ToWorld(ref t_screenPosition, out t_worldPosition);
                        // adjust output of world position (why this works idfk)
                        t_worldPosition.X -= camera.Width / 2f;
                        t_worldPosition.Y -= camera.Height / 2f;
                        // create rect to represent inworld touch location
                        var tlrect = new Rectangle((int)t_worldPosition.X, (int)t_worldPosition.Y, 1, 1);
                        // if touch doesn't intersect tile hitbox
                        if (!tlrect.Intersects(TouchHitbox)) continue;

                        // if touchstate is moved/moving or pressed
                        if (tl.State == TouchLocationState.Moved || tl.State == TouchLocationState.Pressed)
                        {
                            // set states currently hovered tile
                            state.CurrentlyHoveredTile = this;
                            state.CurrentlySelectedTile = this;
                            Log.Info("CitySim-Tile", $"Tile {TileIndex} Press Registered");
                            Pressed?.Invoke(this, new EventArgs());
                        }
                        // else if touch was released on this tile
                        else if (tl.State == TouchLocationState.Released)
                        {
                            // try to get previous touch and see if it wasn't moving before released
                            TouchLocation prevLoc;
                            if (!tl.TryGetPreviousLocation(out prevLoc) || prevLoc.State != TouchLocationState.Moved) continue;
                            // set states currently selected tile
                            state.CurrentlySelectedTile = this;
                            // reset timer on touch (only can touch every two seconds)
                            _touchRemainingDelay = _touchTimeCycleDelay;
                            Log.Info("CitySim-Tile", $"Tile {TileIndex} Press-Release Registered");
                            Click?.Invoke(this, new EventArgs());
                        }
                    }
                }
            }

            #endregion


            if (Object is Residence r)
            {
                //Console.Out.WriteLine("Listing residents for tile: {0}", TileIndex.ToString());
                foreach (var res in r.Residents)
                {
                    //Console.Out.WriteLine("res.Name = {0}", res.Name);
                }
            }

            if (HasAnimatedTexture == false) CheckForAnimatedTexture();

            _gameState = state;
        }

        // draw
        // - draw tile
        // - draw outline if selected
        public void Draw(GameTime gameTime_, SpriteBatch spriteBatch_)
        {

            // set draw color to orange red if hovered by mouse, otherwise draw normal color
            if (_gameState.CurrentlyHoveredTile == this)
            {
                // but set drawcolor to greyed out if not visible
                DrawColor = (IsVisible) ? Color.OrangeRed : Color.DarkGray;
            }
            else
            {
                DrawColor = (IsVisible) ? Color.White : Color.DarkGray;
                DrawColor = (IsGlowing) ? new Color(Color.Yellow, 0.5f) : DrawColor;
            }

            DrawColor = IsVisible
                ? (_gameState.CurrentlyHoveredTile == this
                    ? Color.OrangeRed
                    : Color.White)
                : Color.DarkGray;
            DrawColor = IsGlowing
                ? new Color(Color.Yellow, 0.5f)
                : DrawColor;

            // if there is a tile object
            if (Object.TypeId != 0)
            {
                var txt = Texture;

                // if a building, draw concrete texture on tile
                if (Object.TypeId.Equals(2)
                    && !(BuildingData.Dict_BuildingResourceLinkKeys.ContainsKey(Object.ObjectId))
                    && !(Object.ObjectId.Equals(Building.PowerLine().ObjectId))
                    && !(Object.ObjectId.Equals(Building.Windmill().ObjectId))
                    && !(Object.ObjectId.Equals(Building.Watermill().ObjectId)))
                {
                    txt = Content.GetTileTexture(3);
                }

                // if road, decide texture based on nearby roads
                if (Object.TypeId.Equals(2) && Object.ObjectId == Building.Road().ObjectId)
                {
                    txt = DecideTexture_NearbyRoadsFactor();
                    var txt_index = DecideTextureID_NearbyRoadsFactor();
                    if (txt_index != Object.TextureIndex)
                    {
                        Object.TextureIndex = txt_index;
                    }
                }

                // draw saved texture
                spriteBatch_.Draw(txt, position: Position, scale: Scale, layerDepth: 0.4f, color: DrawColor);

                var anim_src = new Rectangle();
                if (HasAnimatedTexture)
                {
                    Anim_Time += (float)gameTime_.ElapsedGameTime.TotalSeconds;
                    while (Anim_Time > Anim_FrameTime)
                    {
                        Anim_Time -= Anim_FrameTime;
                        Anim_FrameIndex = (Anim_FrameIndex + 1) % Anim_FrameCount;
                    }
                    anim_src = new Rectangle(Anim_FrameIndex * Texture.Width, 0, Texture.Width, Texture.Height);
                }

                // tile object draw attempt
                try
                {
                    // draw tile object
#pragma warning disable CS0618 // Type or member is obsolete

                    if (HasAnimatedTexture)
                    {
                        spriteBatch_.Draw(
                            Anim_Texture,
                            sourceRectangle: anim_src,
                            position: Position,
                            scale: Scale,
                            layerDepth: 0.4f,
                            color: DrawColor);
                    }
                    else
                    {
                        spriteBatch_.Draw(
                            IsPreviewingRoad
                                ? DecideTexture_NearbyRoadsFactor()
                                : Content.GetTileTexture(Object.TextureIndex),
                            position: Position,
                            scale: Scale,
                            layerDepth: 0.4f,
                            color: DrawColor);
                    }

#pragma warning restore CS0618 // Type or member is obsolete
                }
                catch (Exception e)
                {
                    Log.Info("CitySim",  "Error drawing object sprite: " + e.Message);
                }
            }
            else
            {
                spriteBatch_.Draw(Texture, position: Position, scale: Scale, layerDepth: 0.4f, color: DrawColor);
                if (IsPreviewingRoad)
                {
                    spriteBatch_.Draw(DecideTexture_NearbyRoadsFactor(), position: Position, scale: Scale, layerDepth: 0.4f, color: DrawColor);
                }
            }

            // draw destruction fx?
            if (ObjectDestroyed != true) return;

            FX_Destroyed_Anim_Time += (float)gameTime_.ElapsedGameTime.TotalSeconds;
            while (FX_Destroyed_Anim_Time > FX_Destroyed_Anim_FrameTime)
            {
                FX_Destroyed_Anim_Time -= FX_Destroyed_Anim_FrameTime;
                FX_Destroyed_Anim_FrameIndex =
                    Math.Min(FX_Destroyed_Anim_FrameIndex + 1, FX_Destroyed_Anim_FrameCount);
            }

            var FX_Destroy_Src = new Rectangle(FX_Destroyed_Anim_FrameIndex * Texture.Width, 0, Texture.Width, Texture.Height);
            if (FX_Destroyed_Anim_Texture != null)
#pragma warning disable CS0618 // Type or member is obsolete
                spriteBatch_.Draw(
                    FX_Destroyed_Anim_Texture,
                    sourceRectangle: FX_Destroy_Src,
                    position: Position,
                    scale: Scale,
                    layerDepth: 0.4f,
                    color: DrawColor);
#pragma warning restore CS0618 // Type or member is obsolete

            if (FX_Destroyed_Anim_FrameIndex == FX_Destroyed_Anim_FrameCount)
            {
                ObjectDestroyed = false;
                FX_Destroyed_Anim_FrameIndex = 0;
                FX_Destroyed_Anim_Time = 0;
            }
        }

        #region HELPER METHODS
        public TileData GetTileData()
        {
            return TileData = new TileData()
            {
                TileIndex = this.TileIndex,
                Position = this.Position,
                TerrainId = this.TerrainId,
                IsVisible = this.IsVisible,
                Object = this.Object
            };
        }

        public void CheckForAnimatedTexture()
        {
            if (Content.Dict_CorrespondingAnimTextureID.ContainsKey(Object.TextureIndex))
                Anim_Texture = Content.GetTileTexture(Content.Dict_CorrespondingAnimTextureID[Object.TextureIndex]);
        }

        public bool[] GetNearbyRoads()
        {
            var x = TileIndex.X;
            var y = TileIndex.Y;

            var blank_tile = new Tile(Content, GraphicsDevice_, new TileData());

            var left = new Vector2((int)x - 1, (int)y);
            Tile left_tile;
            if (left.X <= 0 || left.X >= _gameState.MapBounds || left.Y <= 0 || left.Y >= _gameState.MapBounds)
            {
                left_tile = blank_tile;
            }
            else
            {
                left_tile = _gameState.CurrentMap.Tiles[(int)x - 1, (int)y];
            }

            var right = new Vector2((int)x + 1, (int)y);
            Tile right_tile;
            if (right.X <= 0 || right.X >= _gameState.MapBounds || right.Y <= 0 || right.Y >= _gameState.MapBounds)
            {
                right_tile = blank_tile;
            }
            else
            {
                right_tile = _gameState.CurrentMap.Tiles[(int)x + 1, (int)y];
            }

            var top = new Vector2((int)x, (int)y - 1);
            Tile top_tile;
            if (top.X <= 0 || top.X >= _gameState.MapBounds || top.Y <= 0 || top.Y >= _gameState.MapBounds)
            {
                top_tile = blank_tile;
            }
            else
            {
                top_tile = _gameState.CurrentMap.Tiles[(int)x, (int)y - 1];
            }

            var bot = new Vector2((int)x, (int)y + 1);
            Tile bot_tile;
            if (bot.X <= 0 || bot.X >= _gameState.MapBounds || bot.Y <= 0 || bot.Y >= _gameState.MapBounds)
            {
                bot_tile = blank_tile;
            }
            else
            {
                bot_tile = _gameState.CurrentMap.Tiles[(int)x, (int)y + 1];
            }

            return new[]
            {
                left_tile.IsPreviewingRoad || (left_tile.Object.ObjectId.Equals(Building.Road().ObjectId) &&
                                               left_tile.Object.TypeId.Equals(Building.Road().TypeId)),

                right_tile.IsPreviewingRoad || (right_tile.Object.ObjectId.Equals(Building.Road().ObjectId) &&
                                                right_tile.Object.TypeId.Equals(Building.Road().TypeId)),

                top_tile.IsPreviewingRoad || (top_tile.Object.ObjectId.Equals(Building.Road().ObjectId) &&
                                              top_tile.Object.TypeId.Equals(Building.Road().TypeId)),

                bot_tile.IsPreviewingRoad || (bot_tile.Object.ObjectId.Equals(Building.Road().ObjectId) &&
                                              bot_tile.Object.TypeId.Equals(Building.Road().TypeId))
            };
        }

        public Texture2D DecideTexture_NearbyRoadsFactor()
        {
            var txt_id = DecideTextureID_NearbyRoadsFactor();
            LastSavedRoadTexture = Content.GetTileTexture(txt_id);
            return LastSavedRoadTexture;
        }

        public int DecideTextureID_NearbyRoadsFactor()
        {
            // get results factor
            var f = GetNearbyRoads();

            var bool_cnt = f.Count(b => b);

            var txt_id = 26;

            switch (bool_cnt)
            {
                case 1:
                    // if left & right, or left, or right (Straight Road (Left))
                    if (!(f[3] || f[2]))
                    {
                        txt_id = 26;
                    }
                    // if up & down, or up, or down (Straight Road (Right))
                    else if (!(f[0] || f[1]))
                    {
                        txt_id = 27;
                    }
                    break;
                case 2:
                    // if left and up
                    if (f[0] && f[2] && (bool_cnt == 2))
                    {
                        txt_id = 35;
                    }
                    // if left and down
                    else if (f[0] && f[3])
                    {
                        txt_id = 36;
                    }
                    // if right and up
                    else if (f[1] && f[2])
                    {
                        txt_id = 34;
                    }
                    // if right and down
                    else if (f[1] && f[3])
                    {
                        txt_id = 33;
                    }
                    // if left & right, or left, or right (Straight Road (Left))
                    else if (!(f[3] || f[2]))
                    {
                        txt_id = 26;
                    }
                    // if up & down, or up, or down (Straight Road (Right))
                    else if (!(f[0] || f[1]))
                    {
                        txt_id = 27;
                    }
                    break;
                case 3:
                    if (f[0] && f[1] && f[2])
                    {
                        txt_id = 30;
                    }
                    else if (f[0] && f[1] && f[3])
                    {
                        txt_id = 32;
                    }
                    else if (f[2] && f[3] && f[0])
                    {
                        txt_id = 29;
                    }
                    else if (f[2] && f[3] && f[1])
                    {
                        txt_id = 31;
                    }
                    break;
                case 4:
                    txt_id = 28;
                    break;
            }

            return txt_id;
        }
        #endregion
    }
}