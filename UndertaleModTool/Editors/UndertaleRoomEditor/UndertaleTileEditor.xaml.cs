using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using UndertaleModTool.Editors;
using UndertaleModLib;
using UndertaleModLib.Models;
using static UndertaleModLib.Models.UndertaleRoom;

namespace UndertaleModTool
{
    /// <summary>
    /// Interaction logic for UndertaleTileEditor.xaml
    /// </summary>
    [PropertyChanged.AddINotifyPropertyChangedInterface]
    public partial class UndertaleTileEditor : Window
    {
        public static RoutedUICommand MirrorCommand = new("Toggle the mirror tile flag", "Mirror", typeof(UndertaleTileEditor));
        public static RoutedUICommand FlipCommand = new("Toggle the flip tile flag", "Flip", typeof(UndertaleTileEditor));
        public static RoutedUICommand RotateCommand = new("Toggle the rotate tile flag", "Rotate", typeof(UndertaleTileEditor));
        public static RoutedUICommand RotateCWCommand = new("Rotate the brush 90 degrees clockwise", "RotateCW", typeof(UndertaleTileEditor));
        public static RoutedUICommand RotateCCWCommand = new("Rotate the brush 90 degrees counterclockwise", "RotateCCW", typeof(UndertaleTileEditor));
        public static RoutedUICommand ToggleGridCommand = new("Toggle the tile grid", "ToggleGrid", typeof(UndertaleTileEditor));

        public Layer EditingLayer { get; set; }

        public WriteableBitmap TilesBitmap { get; set; }

        public double EditWidth { get; set; }
        public double EditHeight { get; set; }
        public double PaletteWidth { get; set; }
        public double PaletteHeight { get; set; }

        private uint BrushTile { get; set; }
        public bool BrushFlipH { get; set; }
        public bool BrushFlipV { get; set; }
        public bool BrushRotate { get; set; }

        private const uint TILE_FLIP_H = 0b00010000000000000000000000000000;
        private const uint TILE_FLIP_V = 0b00100000000000000000000000000000;
        private const uint TILE_ROTATE = 0b01000000000000000000000000000000;
        private const uint TILE_INDEX = 0x7ffff;
        private const uint TILE_FLAGS = ~TILE_INDEX;

        private uint[][] OldTileData { get; set; }
        public Layer.LayerTilesData TilesData { get; set; }
        public Layer.LayerTilesData PaletteTilesData { get; set; }
        public uint PaletteColumns { get { return PaletteTilesData.TilesX; } set {
            PaletteTilesData.TilesX = Math.Max(value, 1);
            PaletteTilesData.Background.PaletteColumns = PaletteTilesData.TilesX;
            PopulatePalette();
        } }
        public double PaletteCursorX { get; set; }
        public double PaletteCursorY { get; set; }


        public Point ScrollViewStart { get; set; }
        public Point ScrollMouseStart { get; set; }

        private bool apply { get; set; } = false;
        private bool canPick { get; set; } = false;

        private ScrollViewer FocusedTilesScroll { get; set; }
        private Layer.LayerTilesData FocusedTilesData { get; set; }
        private TileLayerImage FocusedTilesImage { get; set; }

        private static CachedTileDataLoader loader = new();
        private byte[] emptyTile { get; set; }
        private Dictionary<uint, byte[]> TileCache { get; set; }

        public bool ShowGridBool { get { return ShowGrid == Visibility.Visible; } set {
            ShowGrid = value ? Visibility.Visible : Visibility.Hidden;
        } }
        public Visibility ShowGrid { get; set; } = Visibility.Visible;
        public Rect GridRect { get; set; }
        public Point GridPoint1 { get; set; }
        public Point GridPoint2 { get; set; }

        public string StatusText { get; set; } = "";

        public UndertaleTileEditor(Layer layer)
        {
            EditingLayer = layer;

            BrushTile = 0;
            OldTileData = (uint[][])EditingLayer.TilesData.TileData.Clone();
            for (int i = 0; i < OldTileData.Length; i++)
            {
                OldTileData[i] = (uint[])OldTileData[i].Clone();
            }
            TilesData = EditingLayer.TilesData;
            TileCache = new();

            PaletteTilesData = new Layer.LayerTilesData();
            PaletteTilesData.TileData = new uint[][] { new uint[] { 0 } };
            PaletteTilesData.Background = TilesData.Background;
            PaletteColumns = PaletteTilesData.Background.PaletteColumns;

            EditWidth = Convert.ToDouble((long)TilesData.TilesX * (long)TilesData.Background.GMS2TileWidth);
            EditHeight = Convert.ToDouble((long)TilesData.TilesY * (long)TilesData.Background.GMS2TileHeight);

            emptyTile = (byte[])Array.CreateInstance(
                typeof(byte), TilesData.Background.GMS2TileWidth * TilesData.Background.GMS2TileHeight * 4
            );
            Array.Fill<byte>(emptyTile, 0);

            GridRect = new(0, 0, TilesData.Background.GMS2TileWidth, TilesData.Background.GMS2TileHeight);
            GridPoint1 = new(TilesData.Background.GMS2TileWidth, 0);
            GridPoint2 = new(0, TilesData.Background.GMS2TileHeight);

            CachedTileDataLoader.Reset();
            TilesBitmap = new(
                (int)EditWidth, (int)EditHeight, 96, 96,
                PixelFormats.Bgra32, null
            );
            DrawTilemap(TilesData, TilesBitmap);

            this.DataContext = this;
            InitializeComponent();
        }
        private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!IsVisible || IsLoaded)
                return;

            if (Settings.Instance.EnableDarkMode)
                MainWindow.SetDarkTitleBarForWindow(this, true, false);
        }
        private void Window_Closing(object sender, EventArgs e)
        {
            PaletteTilesData.Dispose();
            if (apply)
            {
                // force a redraw
                EditingLayer.TilesData.TileData = (uint[][])TilesData.TileData.Clone();
            }
            else
                EditingLayer.TilesData.TileData = OldTileData;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            apply = false;
            this.Close();
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            apply = true;
            this.Close();
        }

        private void PopulatePalette()
        {
            PaletteTilesData.TilesY = (uint)Convert.ToInt32(
                Math.Ceiling(
                    (double)PaletteTilesData.Background.GMS2TileCount /
                    PaletteTilesData.TilesX
                )
            );

            PaletteWidth = Convert.ToDouble((long)PaletteTilesData.TilesX * (long)PaletteTilesData.Background.GMS2TileWidth);
            PaletteHeight = Convert.ToDouble((long)PaletteTilesData.TilesY * (long)PaletteTilesData.Background.GMS2TileHeight);

            int i = 0;
            for (int y = 0; y < PaletteTilesData.TilesY; y++)
            {
                for (int x = 0; x < PaletteTilesData.TilesX; x++)
                {
                    if (i >= PaletteTilesData.Background.GMS2TileCount)
                        PaletteTilesData.TileData[y][x] = 0;
                    else
                        PaletteTilesData.TileData[y][x] = PaletteTilesData.Background.GMS2TileIds[i].ID;
                    i++;
                }
            }

            UpdatePaletteCursor();
        }

        private void PaintTile(Layer.LayerTilesData tilesData, Point pos, uint tileID)
        {
            if (tilesData == PaletteTilesData)
                return;
            
            int x = Convert.ToInt32(Math.Floor(pos.X / tilesData.Background.GMS2TileWidth));
            int y = Convert.ToInt32(Math.Floor(pos.Y / tilesData.Background.GMS2TileHeight));

            uint _tileID = tileID;
            if (BrushFlipH) _tileID |= TILE_FLIP_H;
            if (BrushFlipV) _tileID |= TILE_FLIP_V;
            if (BrushRotate) _tileID |= TILE_ROTATE;

            if (SetTile(x, y, tilesData, _tileID))
            {
                DrawTile(
                    tilesData.Background, tilesData.TileData[y][x],
                    TilesBitmap, x, y, false
                );
            }
        }

        private bool SetTile(int x, int y, Layer.LayerTilesData tilesData, uint tileID)
        {
            if (x < 0 || y < 0) return false;
            if (x >= tilesData.TilesX || y >= tilesData.TilesY) return false;

            if (tilesData.TileData[y][x] != tileID)
            {
                tilesData.TileData[y][x] = tileID;
                return true;
            }
            return false;
        }

        private void Pick(Point pos, Layer.LayerTilesData tilesData)
        {
            int x = Convert.ToInt32(Math.Floor(pos.X / tilesData.Background.GMS2TileWidth));
            int y = Convert.ToInt32(Math.Floor(pos.Y / tilesData.Background.GMS2TileHeight));
            if (x < 0 || y < 0)
                return;
            if (x >= tilesData.TilesX || y >= tilesData.TilesY)
                return;
            uint tile = tilesData.TileData[y][x];
            BrushTile = tile & TILE_INDEX; // remove flags
            if (tilesData != PaletteTilesData)
            {
                BrushFlipH = (tile & TILE_FLIP_H) != 0;
                BrushFlipV = (tile & TILE_FLIP_V) != 0;
                BrushRotate = (tile & TILE_ROTATE) != 0;
            }
            UpdatePaletteCursor();
        }

        private void UpdatePaletteCursor()
        {
            int index = PaletteTilesData.Background.GMS2TileIds.FindIndex(
                id => id.ID == BrushTile
            );
            if (index == -1)
                index = 0;
            PaletteCursorX = (index % (int)PaletteTilesData.TilesX) * (int)PaletteTilesData.Background.GMS2TileWidth;
            PaletteCursorY = (index / (int)PaletteTilesData.TilesX) * (int)PaletteTilesData.Background.GMS2TileHeight;
            if (PaletteCursor is not null)
                PaletteCursor.BringIntoView();
        }

        private void Tiles_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender == TilesCanvas)
            {
                FocusedTilesScroll = TilesScroll;
                FocusedTilesImage = LayerImage;
                FocusedTilesData = TilesData;
            }
            else
            {
                FocusedTilesScroll = PaletteScroll;
                FocusedTilesImage = PaletteLayerImage;
                FocusedTilesData = PaletteTilesData;
            }

            if (FocusedTilesScroll == PaletteScroll || e.MiddleButton == MouseButtonState.Pressed)
            {
                canPick = true;
                ScrollMouseStart = e.GetPosition(this as Window);
                ScrollViewStart = new Point(FocusedTilesScroll.HorizontalOffset, FocusedTilesScroll.VerticalOffset);
            }
            else if (e.RightButton == MouseButtonState.Pressed)
                PaintTile(FocusedTilesData, e.GetPosition(FocusedTilesImage), 0);
            else if (e.LeftButton == MouseButtonState.Pressed)
            {
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
                    Pick(e.GetPosition(FocusedTilesImage), FocusedTilesData);
                else
                    PaintTile(FocusedTilesData, e.GetPosition(FocusedTilesImage), BrushTile);
            }
        }
        private void Tiles_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (canPick && FocusedTilesScroll is not null)
                Pick(e.GetPosition(FocusedTilesImage), FocusedTilesData);
            canPick = false;
            FocusedTilesScroll = null;
            FocusedTilesData = null;
            FocusedTilesImage = null;
        }
        private void Tiles_MouseMove(object sender, MouseEventArgs e)
        {
            Point mapPos = e.GetPosition(LayerImage as TileLayerImage);
            int mapX = Convert.ToInt32(Math.Floor(mapPos.X / TilesData.Background.GMS2TileWidth));
            int mapY = Convert.ToInt32(Math.Floor(mapPos.Y / TilesData.Background.GMS2TileHeight));
            StatusText = $"x: {mapX}  y: {mapY}";

            if (FocusedTilesScroll is null)
                return;

            if (e.MiddleButton == MouseButtonState.Pressed)
            {
                canPick = false;
                Point pos = e.GetPosition(this as Window);
                FocusedTilesScroll.ScrollToHorizontalOffset(Math.Clamp(
                    ScrollViewStart.X + -(pos.X - ScrollMouseStart.X), 0, FocusedTilesScroll.ScrollableWidth
                ));
                FocusedTilesScroll.ScrollToVerticalOffset(Math.Clamp(
                    ScrollViewStart.Y + -(pos.Y - ScrollMouseStart.Y), 0, FocusedTilesScroll.ScrollableHeight
                ));
                return;
            }

            if (FocusedTilesScroll == PaletteScroll)
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                    Pick(e.GetPosition(FocusedTilesImage), FocusedTilesData);
                return;
            }

            if (e.RightButton == MouseButtonState.Pressed)
                PaintTile(FocusedTilesData, e.GetPosition(FocusedTilesImage), 0);
            else if (e.LeftButton == MouseButtonState.Pressed)
            {
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
                    Pick(e.GetPosition(FocusedTilesImage), FocusedTilesData);
                else
                    PaintTile(FocusedTilesData, e.GetPosition(FocusedTilesImage), BrushTile);
            }
        }
        private void Tiles_MouseLeave(object sender, MouseEventArgs e)
        {
            
        }

        private void Scroll_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            ScrollViewer scrollViewer = (ScrollViewer)sender;
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset - e.Delta);
                e.Handled = true;
            }
        }

        private void DrawTilemap(Layer.LayerTilesData tilesData, WriteableBitmap wBitmap)
        {
            if ((loader.Convert(new object[] { tilesData }, null, "cache", null) as string) == "Error")
                return;
            
            for (int y = 0; y < tilesData.TilesY; y++)
            {
                for (int x = 0; x < tilesData.TilesX; x++)
                {
                    DrawTile(
                        tilesData.Background, tilesData.TileData[y][x],
                        wBitmap, x, y, false
                    );
                }
            }
        }

        // assumes a bgra32 writeablebitmap
        private void DrawTile(UndertaleBackground tileset, uint tile, WriteableBitmap wBitmap, int x, int y, bool transparent)
        {
            uint tileID = tile & TILE_INDEX;
            if (tileID == 0)
            {
                ClearToWBitmap(
                    wBitmap, (int)(x * tileset.GMS2TileWidth), (int)(y * tileset.GMS2TileHeight),
                    (int)tileset.GMS2TileWidth, (int)tileset.GMS2TileHeight
                );
                return;
            }

            System.Drawing.Bitmap tileBMP = CachedTileDataLoader.TileCache[new(tileset.Texture.Name.Content, tileID)];

            if ((tile & TILE_FLAGS) == 0)
            {
                if (!transparent && TileCache.ContainsKey(tileID))
                {
                    wBitmap.WritePixels(
                        new Int32Rect(0, 0, (int)tileset.GMS2TileWidth, (int)tileset.GMS2TileHeight), 
                        TileCache[tileID], (int)tileset.GMS2TileWidth * 4,
                        (int)(x * tileset.GMS2TileWidth), (int)(y * tileset.GMS2TileHeight)
                    );
                    return;
                }
                DrawBitmapToWBitmap(
                    tileBMP, wBitmap,
                    (int)(x * tileset.GMS2TileWidth), (int)(y * tileset.GMS2TileHeight),
                    transparent, tileID
                );
                return;
            }

            System.Drawing.Bitmap newBMP = (System.Drawing.Bitmap)tileBMP.Clone();

            switch ((tile & TILE_FLAGS) >> 28)
            {
                case 1:
                    newBMP.RotateFlip(System.Drawing.RotateFlipType.RotateNoneFlipX);
                    break;
                case 2:
                    newBMP.RotateFlip(System.Drawing.RotateFlipType.RotateNoneFlipY);
                    break;
                case 3:
                    newBMP.RotateFlip(System.Drawing.RotateFlipType.RotateNoneFlipXY);
                    break;
                case 4:
                    newBMP.RotateFlip(System.Drawing.RotateFlipType.Rotate90FlipNone);
                    break;
                case 5:
                    newBMP.RotateFlip(System.Drawing.RotateFlipType.Rotate270FlipY);
                    break;
                case 6:
                    newBMP.RotateFlip(System.Drawing.RotateFlipType.Rotate90FlipY);
                    break;
                case 7:
                    newBMP.RotateFlip(System.Drawing.RotateFlipType.Rotate270FlipNone);
                    break;
                default:
                    throw new InvalidDataException($"{tile & TILE_FLAGS} is not a valid tile flag value.");
            }

            DrawBitmapToWBitmap(
                newBMP, wBitmap,
                (int)(x * tileset.GMS2TileWidth), (int)(y * tileset.GMS2TileHeight),
                transparent
            );

            newBMP.Dispose();
        }
        private void DrawBitmapToWBitmap(System.Drawing.Bitmap bitmap, WriteableBitmap wBitmap, int x, int y, bool transparent, uint? cache = null)
        {
            byte[] arr = (byte[])Array.CreateInstance(typeof(byte), bitmap.Width * bitmap.Height * 4);
            
            int i = 0;
            for (int by = 0; by < bitmap.Height; by++)
            {
                for (int bx = 0; bx < bitmap.Width; bx++)
                {
                    System.Drawing.Color color = bitmap.GetPixel(bx, by);

                    arr[i] = color.B;
                    arr[i + 1] |= color.G;
                    arr[i + 2] |= color.R;
                    if (transparent)
                        arr[i + 3] |= (byte)(color.A >> 1);
                    else
                        arr[i + 3] |= color.A;
                    i += 4;
                }
            }

            if (cache is uint cacheID)
            {
                TileCache.TryAdd(cacheID, arr);
            }
            
            wBitmap.WritePixels(new Int32Rect(0, 0, bitmap.Width, bitmap.Height), arr, bitmap.Width * 4, x, y);
        }
        private void ClearToWBitmap(WriteableBitmap wBitmap, int x, int y, int width, int height)
        {
            wBitmap.WritePixels(new Int32Rect(0, 0, width, height), emptyTile, width * 4, x, y);
        }

        private void Command_Mirror(object sender, ExecutedRoutedEventArgs e)
        {
            BrushFlipH = !BrushFlipH;
        }
        private void Command_Flip(object sender, ExecutedRoutedEventArgs e)
        {
            BrushFlipV = !BrushFlipV;
        }
        private void Command_Rotate(object sender, ExecutedRoutedEventArgs e)
        {
            BrushRotate = !BrushRotate;
        }
        private void Command_RotateCW(object sender, ExecutedRoutedEventArgs e)
        {
            int rotation = Convert.ToInt32(BrushFlipH) << 2 | Convert.ToInt32(BrushFlipV) << 1 | Convert.ToInt32(BrushRotate);
            // bits left to right: fliph, flipv, rotate
            // there are essentially 2 cycles
            rotation = rotation switch
            {
                0b000 => 0b001,
                0b001 => 0b110,
                0b110 => 0b111,
                0b111 => 0b000,
                
                0b100 => 0b011,
                0b011 => 0b010,
                0b010 => 0b101,
                0b101 => 0b100,

                _ => 0b000
            };
            BrushFlipH = (rotation & 0b100) != 0;
            BrushFlipV = (rotation & 0b010) != 0;
            BrushRotate = (rotation & 0b001) != 0;
        }
        private void Command_RotateCCW(object sender, ExecutedRoutedEventArgs e)
        {
            int rotation = Convert.ToInt32(BrushFlipH) << 2 | Convert.ToInt32(BrushFlipV) << 1 | Convert.ToInt32(BrushRotate);
            rotation = rotation switch
            {
                0b000 => 0b111,
                0b111 => 0b110,
                0b110 => 0b001,
                0b001 => 0b000,

                0b100 => 0b101,
                0b101 => 0b010,
                0b010 => 0b011,
                0b011 => 0b100,

                _ => 0b000
            };
            BrushFlipH = (rotation & 0b100) != 0;
            BrushFlipV = (rotation & 0b010) != 0;
            BrushRotate = (rotation & 0b001) != 0;
        }
        private void Command_ToggleGrid(object sender, ExecutedRoutedEventArgs e)
        {
            ShowGridBool = !ShowGridBool;
        }
    }
}
