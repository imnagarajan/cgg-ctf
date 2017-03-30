using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

using Terraria;
using Microsoft.Xna.Framework;

namespace CGGCTF
{
    public class CTFTileSystem
    {
        #region Variables

        int flagDistance { get { return CTFConfig.FlagDistance;  } }
        int spawnDistance { get { return CTFConfig.SpawnDistance;  } }

        int maxX {
            get {
                return Main.maxTilesX;
            }
        }
        int maxY {
            get {
                return Main.maxTilesY;
            }
        }
        int mapMiddle {
            get {
                return maxX / 2;
            }
        }

        Point redSpawn, blueSpawn, redFlag, blueFlag;
        Rectangle redSpawnArea, blueSpawnArea;
        Rectangle redFlagNoEdit, blueFlagNoEdit;
        Rectangle redFlagArea, blueFlagArea;
        Tile[,] realTiles;

        public Point RedSpawn {
            get {
                return new Point(redSpawn.X, redSpawn.Y - 3);
            }
        }
        public Point BlueSpawn {
            get {
                return new Point(blueSpawn.X, blueSpawn.Y - 3);
            }
        }

        public CTFTeam LeftTeam {
            get {
                return redSpawn.X < blueSpawn.X ? CTFTeam.Red : CTFTeam.Blue;
            }
        }
        public CTFTeam RightTeam {
            get {
                return redSpawn.X > blueSpawn.X ? CTFTeam.Red : CTFTeam.Blue;
            }
        }

        int wallWidth { get { return CTFConfig.WallWidth; } }
        int wallMiddle {
            get {
                return maxX / 2;
            }
        }
        int wallLeft {
            get {
                return wallMiddle - wallWidth;
            }
        }
        int wallRight {
            get {
                return wallMiddle + wallWidth;
            }
        }

        // TODO - name capitalization
        public ushort redBlock = Terraria.ID.TileID.RedBrick;
        public ushort blueBlock = Terraria.ID.TileID.CobaltBrick;
        public ushort redWall = Terraria.ID.WallID.RedBrick;
        public ushort blueWall = Terraria.ID.WallID.CobaltBrick;
        public ushort grayBlock = Terraria.ID.TileID.GrayBrick;
        public ushort middleBlock = Terraria.ID.TileID.LihzahrdBrick;
        public ushort flagTile = Terraria.ID.TileID.Banners;
        public ushort flagRedStyle = 0;
        public ushort flagBlueStyle = 2;

        #endregion

        #region Code from WorldEdit

        public bool IsSolidTile(int x, int y)
        {
            return x < 0 || y < 0 || x >= maxX || y >= Main.maxTilesY || (Main.tile[x, y].active() && Main.tileSolid[Main.tile[x, y].type]);
        }

        public void SetTile(int i, int j, int tileType, int style = 0)
        {
            var tile = Main.tile[i, j];
            switch (tileType) {
                case -1:
                    tile.active(false);
                    tile.frameX = -1;
                    tile.frameY = -1;
                    tile.liquidType(0);
                    tile.liquid = 0;
                    tile.type = 0;
                    return;
                case -2:
                    tile.active(false);
                    tile.liquidType(1);
                    tile.liquid = 255;
                    tile.type = 0;
                    return;
                case -3:
                    tile.active(false);
                    tile.liquidType(2);
                    tile.liquid = 255;
                    tile.type = 0;
                    return;
                case -4:
                    tile.active(false);
                    tile.liquidType(0);
                    tile.liquid = 255;
                    tile.type = 0;
                    return;
                default:
                    if (Main.tileFrameImportant[tileType])
                        WorldGen.PlaceTile(i, j, tileType, false, false, -1, style);
                    else {
                        tile.active(true);
                        tile.frameX = -1;
                        tile.frameY = -1;
                        tile.liquidType(0);
                        tile.liquid = 0;
                        tile.slope(0);
                        tile.color(0);
                        tile.type = (ushort)tileType;
                    }
                    return;
            }
        }

        public void SetWall(int i, int j, int wallType)
        {
            Main.tile[i, j].wall = (byte)wallType;
        }

        public void ResetSection(int x, int x2, int y, int y2)
        {
            int lowX = Netplay.GetSectionX(x);
            int highX = Netplay.GetSectionX(x2);
            int lowY = Netplay.GetSectionY(y);
            int highY = Netplay.GetSectionY(y2);
            foreach (RemoteClient sock in Netplay.Clients.Where(s => s.IsActive)) {
                for (int i = lowX; i <= highX; i++) {
                    for (int j = lowY; j <= highY; j++)
                        sock.TileSections[i, j] = false;
                }
            }
        }

        #endregion

        #region Positions

        public int FindGround(int x)
        {
            int y = 0;
            for (int i = 1; i < Main.maxTilesY; ++i) {
                if (Main.tile[x, i].type == Terraria.ID.TileID.Cloud
                    || Main.tile[x, i].type == Terraria.ID.TileID.RainCloud) {
                    y = 0;
                } else if (IsSolidTile(x, i) && y == 0) {
                    y = i;
                }
            }
            y -= 2;
            return y;
        }

        public void DecidePositions()
        {
            int f1x = mapMiddle - flagDistance;
            int f1y = FindGround(f1x) - 1;

            int f2x = mapMiddle + flagDistance;
            int f2y = FindGround(f2x) - 1;

            int s1x = mapMiddle - spawnDistance;
            int s1y = FindGround(s1x) - 2;

            int s2x = mapMiddle + spawnDistance;
            int s2y = FindGround(s2x) - 2;

            if (CTFUtils.Random(2) == 0) {
                redFlag.X = f1x;
                redFlag.Y = f1y;
                redSpawn.X = s1x;
                redSpawn.Y = s1y;
                blueFlag.X = f2x;
                blueFlag.Y = f2y;
                blueSpawn.X = s2x;
                blueSpawn.Y = s2y;
            } else {
                redFlag.X = f2x;
                redFlag.Y = f2y;
                redSpawn.X = s2x;
                redSpawn.Y = s2y;
                blueFlag.X = f1x;
                blueFlag.Y = f1y;
                blueSpawn.X = s1x;
                blueSpawn.Y = s1y;
            }
        }

        #endregion

        #region Middle block

        public void AddMiddleBlock()
        {
            realTiles = new Tile[wallWidth * 2 + 1, Main.maxTilesY];

            for (int x = 0; x <= 2 * wallWidth; ++x) {
                for (int y = 0; y < Main.maxTilesY; ++y) {
                    realTiles[x, y] = new Tile(Main.tile[wallLeft + x, y]);
                    SetTile(wallLeft + x, y, middleBlock);
                }
            }

            ResetSection(wallLeft, wallRight, 0, Main.maxTilesY);
        }

        public void RemoveMiddleBlock()
        {
            for (int x = 0; x <= 2 * wallWidth; ++x) {
                for (int y = 0; y < Main.maxTilesY; ++y) {
                    Main.tile[wallLeft + x, y] = realTiles[x, y];
                }
            }

            ResetSection(wallLeft, wallRight, 0, Main.maxTilesY);
            realTiles = null;
        }

        #endregion

        #region Spawns

        public void AddSpawns()
        {
            AddLeftSpawn();
            AddRightSpawn();
        }

        public void AddLeftSpawn()
        {
            Point leftSpawn;
            ushort tileID;
            ushort wallID;

            if (LeftTeam == CTFTeam.Red) {
                leftSpawn = redSpawn;
                tileID = redBlock;
                wallID = redWall;
                redSpawnArea = new Rectangle(leftSpawn.X - 6, leftSpawn.Y - 9, 13 + 1, 11 + 1);
            } else {
                leftSpawn = blueSpawn;
                tileID = blueBlock;
                wallID = blueWall;
                blueSpawnArea = new Rectangle(leftSpawn.X - 6, leftSpawn.Y - 9, 13 + 1, 11 + 1);
            }

            for (int i = -6; i <= 7; ++i) {
                for (int j = -9; j <= 2; ++j) {
                    SetTile(leftSpawn.X + i, leftSpawn.Y + j, -1);
                    SetWall(leftSpawn.X + i, leftSpawn.Y + j, 0);
                }
            }
            for (int i = -5; i <= 6; ++i)
                SetTile(leftSpawn.X + i, leftSpawn.Y + 1, tileID);
            for (int i = -4; i <= 5; ++i)
                SetWall(leftSpawn.X + i, leftSpawn.Y - 5, wallID);
            for (int i = 1; i <= 3; ++i) {
                for (int j = 0; j < i; ++j)
                    SetWall(leftSpawn.X + 2 + j, leftSpawn.Y - 9 + i, wallID);
            }
            for (int i = 3; i >= 1; --i) {
                for (int j = 0; j < i; ++j)
                    SetWall(leftSpawn.X + 2 + j, leftSpawn.Y - 1 - i, wallID);
            }

            ResetSection(leftSpawn.X - 6, leftSpawn.X + 7, leftSpawn.Y - 9, leftSpawn.Y + 2);
        }

        public void AddRightSpawn()
        {
            Point rightSpawn;
            ushort tileID;
            ushort wallID;

            if (RightTeam == CTFTeam.Blue) {
                rightSpawn = blueSpawn;
                tileID = blueBlock;
                wallID = blueWall;
                blueSpawnArea = new Rectangle(rightSpawn.X - 7, rightSpawn.Y - 9, 13 + 1, 11 + 1);
            } else {
                rightSpawn = redSpawn;
                tileID = redBlock;
                wallID = redWall;
                redSpawnArea = new Rectangle(rightSpawn.X - 7, rightSpawn.Y - 9, 13 + 1, 11 + 1);
            }

            for (int i = -7; i <= 6; ++i) {
                for (int j = -9; j <= 2; ++j) {
                    SetTile(rightSpawn.X + i, rightSpawn.Y + j, -1);
                    SetWall(rightSpawn.X + i, rightSpawn.Y + j, 0);
                }
            }
            for (int i = -6; i <= 5; ++i)
                SetTile(rightSpawn.X + i, rightSpawn.Y + 1, tileID);
            for (int i = -5; i <= 4; ++i)
                SetWall(rightSpawn.X + i, rightSpawn.Y - 5, wallID);
            for (int i = 1; i <= 3; ++i) {
                for (int j = 0; j < i; ++j)
                    SetWall(rightSpawn.X - 2 - j, rightSpawn.Y - 9 + i, wallID);
            }
            for (int i = 3; i >= 1; --i) {
                for (int j = 0; j < i; ++j)
                    SetWall(rightSpawn.X - 2 - j, rightSpawn.Y - 1 - i, wallID);
            }

            ResetSection(rightSpawn.X - 7, rightSpawn.X + 6, rightSpawn.Y - 9, rightSpawn.Y + 2);
        }

        #endregion

        #region Flags

        public void AddFlags()
        {
            AddRedFlag(true);
            AddBlueFlag(true);
        }

        public void AddRedFlag(bool full = false)
        {
            ushort redTile = redBlock;

            if (full) {
                redFlagArea = new Rectangle(redFlag.X - 1, redFlag.Y - 4, 3 + 1, 2 + 1);
                redFlagNoEdit = new Rectangle(redFlag.X - 3, redFlag.Y - 6, 6 + 1, 7 + 1);
                for (int i = -3; i <= 3; ++i) {
                    for (int j = -6; j <= 1; ++j)
                        SetTile(redFlag.X + i, redFlag.Y + j, -1);
                }
            }
            for (int i = -1; i <= 1; ++i) {
                SetTile(redFlag.X + i, redFlag.Y, redTile);
                SetTile(redFlag.X + i, redFlag.Y - 5, redTile);
                SetTile(redFlag.X + i, redFlag.Y - 4, flagTile, flagRedStyle);
            }
            ResetSection(redFlag.X - 3, redFlag.X + 3, redFlag.Y - 6, redFlag.Y + 1);
        }

        public void AddBlueFlag(bool full = false)
        {
            ushort flagTile = Terraria.ID.TileID.Banners;
            ushort blueTile = blueBlock;

            if (full) {
                blueFlagArea = new Rectangle(blueFlag.X - 1, blueFlag.Y - 4, 3 + 1, 2 + 1);
                blueFlagNoEdit = new Rectangle(blueFlag.X - 3, blueFlag.Y - 6, 6 + 1, 7 + 1);
                for (int i = -3; i <= 3; ++i) {
                    for (int j = -6; j <= 1; ++j)
                        SetTile(blueFlag.X + i, blueFlag.Y + j, -1);
                }
            }
            for (int i = -1; i <= 1; ++i) {
                SetTile(blueFlag.X + i, blueFlag.Y, blueTile);
                SetTile(blueFlag.X + i, blueFlag.Y - 5, blueTile);
                SetTile(blueFlag.X + i, blueFlag.Y - 4, flagTile, flagBlueStyle);
            }
            ResetSection(blueFlag.X - 3, blueFlag.X + 3, blueFlag.Y - 6, blueFlag.Y + 1);
        }

        public void RemoveRedFlag()
        {
            for (int i = -1; i <= 1; ++i) {
                for (int j = 4; j >= 2; --j)
                    SetTile(redFlag.X + i, redFlag.Y - j, -1);
            }
            ResetSection(redFlag.X - 3, redFlag.X + 3, redFlag.Y - 6, redFlag.Y + 1);
        }

        public void RemoveBlueFlag()
        {
            for (int i = -1; i <= 1; ++i) {
                for (int j = 4; j >= 2; --j)
                    SetTile(blueFlag.X + i, blueFlag.Y - j, -1);
            }
            ResetSection(blueFlag.X - 3, blueFlag.X + 3, blueFlag.Y - 6, blueFlag.Y + 1);
        }

        #endregion

        #region Check functions

        public bool InRedSide(int x)
        {
            if (LeftTeam == CTFTeam.Red)
                return x < wallMiddle;
            else
                return x > wallMiddle;
        }

        public bool InBlueSide(int x)
        {
            if (LeftTeam == CTFTeam.Blue)
                return x < wallMiddle;
            else
                return x > wallMiddle;
        }

        public bool InRedFlag(int x, int y)
        {
            return redFlagArea.Contains(x, y);
        }

        public bool InBlueFlag(int x, int y)
        {
            return blueFlagArea.Contains(x, y);
        }

        public bool InvalidPlace(CTFTeam team, int x, int y, bool middle)
        {
            if ((middle && x >= wallLeft - 1 && x <= wallRight + 1)
                || (redSpawnArea.Contains(x, y))
                || (blueSpawnArea.Contains(x, y))
                || (x >= redFlagNoEdit.Left && x < redFlagNoEdit.Right && y < redFlagNoEdit.Bottom)
                || (x >= blueFlagNoEdit.Left && x < blueFlagNoEdit.Right && y < blueFlagNoEdit.Bottom)
                || (team == CTFTeam.Red && Main.tile[x, y].type == Terraria.ID.TileID.CobaltBrick)
                || (team == CTFTeam.Blue && Main.tile[x, y].type == Terraria.ID.TileID.RedBrick))
                return true;

            return false;
        }

        #endregion

        public void RemoveBadStuffs()
        {
            for (int i = 0; i < maxX; ++i) {
                for (int j = 0; j < maxY; ++j) {

                    // grass
                    if (Main.tile[i, j].type == 23
                        || Main.tile[i, j].type == 199)
                        SetTile(i, j, 2);

                    // stone
                    else if (Main.tile[i, j].type == 25
                        || Main.tile[i, j].type == 203)
                        SetTile(i, j, 1);

                    // sand
                    else if (Main.tile[i, j].type == 112
                        || Main.tile[i, j].type == 234)
                        SetTile(i, j, 53);

                    // life crystals, anvils, hellforge
                    else if (Main.tile[i, j].type == 12
                        || Main.tile[i, j].type == 16
                        || Main.tile[i, j].type == 77)
                        SetTile(i, j, -1);

                    // plants
                    else if (Main.tile[i, j].type == 24
                        || Main.tile[i, j].type == 201)
                        SetTile(i, j, -1);

                    // thorns
                    else if (Main.tile[i, j].type == 32
                        || Main.tile[i, j].type == 352)
                        SetTile(i, j, -1);

                    // grass walls
                    else if (Main.tile[i, j].wall == 69
                        || Main.tile[i, j].wall == 81)
                        SetWall(i, j, 63);

                    // stone walls
                    else if (Main.tile[i, j].wall == 3
                        || Main.tile[i, j].wall == 83)
                        SetWall(i, j, 1);

                }
            }
        }

    }
}
