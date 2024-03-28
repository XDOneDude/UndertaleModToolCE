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
    /// Global settings used by the tile editor
    /// </summary>
    [PropertyChanged.AddINotifyPropertyChangedInterface]
    public class TileEditorSettings
    {
        public static TileEditorSettings instance { get; set; } = new();
        public bool BrushTiling { get; set; } = true;

        public bool RoomPreviewBool { get { return RoomPreviewVisibility == Visibility.Visible; } set {
            RoomPreviewVisibility = value ? Visibility.Visible : Visibility.Hidden;
        } }
        public Visibility RoomPreviewVisibility { get; set; } = Visibility.Visible;

        public bool ShowGridBool { get { return ShowGrid == Visibility.Visible; } set {
            ShowGrid = value ? Visibility.Visible : Visibility.Hidden;
        } }
        public Visibility ShowGrid { get; set; } = Visibility.Visible;
    }

    /// <summary>
    /// Interaction logic for UndertaleTileEditor.xaml
    /// </summary>
    [PropertyChanged.AddINotifyPropertyChangedInterface]
    public partial class UndertaleTileEditor : Window
    {
        public static RoutedUICommand MirrorCommand = new("Mirror the brush", "Mirror", typeof(UndertaleTileEditor));
        public static RoutedUICommand FlipCommand = new("Flip the brush", "Flip", typeof(UndertaleTileEditor));
        public static RoutedUICommand RotateCWCommand = new("Rotate the brush 90 degrees clockwise", "RotateCW", typeof(UndertaleTileEditor));
        public static RoutedUICommand RotateCCWCommand = new("Rotate the brush 90 degrees counterclockwise", "RotateCCW", typeof(UndertaleTileEditor));
        public static RoutedUICommand ToggleGridCommand = new("Toggle the tile grid", "ToggleGrid", typeof(UndertaleTileEditor));
        public static RoutedUICommand ToggleBrushTilingCommand = new("Toggle the \"tiling\" behavior on multi-tile brushes", "ToggleBrushTiling", typeof(UndertaleTileEditor));
        public static RoutedUICommand TogglePreviewCommand = new("Toggle the room preview", "TogglePreview", typeof(UndertaleTileEditor));

        public TileEditorSettings settings { get; set; } = TileEditorSettings.instance;

        public bool Modified { get; set; } = false;

        public Layer EditingLayer { get; set; }

        public WriteableBitmap TilesBitmap { get; set; }

        public double EditWidth { get; set; }
        public double EditHeight { get; set; }
        public double PaletteWidth { get; set; }
        public double PaletteHeight { get; set; }

        private const uint TILE_FLIP_H = 0b00010000000000000000000000000000;
        private const uint TILE_FLIP_V = 0b00100000000000000000000000000000;
        private const uint TILE_ROTATE = 0b01000000000000000000000000000000;
        private const uint TILE_INDEX = 0x7ffff;
        private const uint TILE_FLAGS = ~TILE_INDEX;
        // flags shifted 28 bits to the right
        private static Dictionary<uint, uint> ROTATION_CW = new Dictionary<uint, uint>{
            {0b000, 0b100},
            {0b100, 0b011},
            {0b011, 0b111},
            {0b111, 0b000},

            {0b001, 0b110},
            {0b110, 0b010},
            {0b010, 0b101},
            {0b101, 0b001},
        };
        private static Dictionary<uint, uint> ROTATION_CCW = new Dictionary<uint, uint>{
            {0b100, 0b000},
            {0b011, 0b100},
            {0b111, 0b011},
            {0b000, 0b111},

            {0b110, 0b001},
            {0b010, 0b110},
            {0b101, 0b010},
            {0b001, 0b101},
        };

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
        public double PaletteCursorWidth { get; set; }
        public double PaletteCursorHeight { get; set; }
        public Visibility PaletteCursorVisibility { get; set; }

        public Layer.LayerTilesData BrushTilesData { get; set; }
        private bool BrushEmpty { get; set; } = true;
        public double BrushWidth { get; set; }
        public double BrushHeight { get; set; }
        public double BrushPreviewX { get; set; } = 0;
        public double BrushPreviewY { get; set; } = 0;
        public Visibility BrushPreviewVisibility { get; set; }
        public Visibility BrushOutlineVisibility { get; set; }
        public Visibility BrushPickVisibility { get; set; }
        public long RefreshBrush { get; set; } = 0;


        public RenderTargetBitmap RoomPreview { get; set; }
        public float RoomPrevOffsetX { get; set; }
        public float RoomPrevOffsetY { get; set; }
        public Point ScrollViewStart { get; set; }
        public Point DrawingStart { get; set; }
        public Point LastMousePos { get; set; }

        private bool apply { get; set; } = false;

        private ScrollViewer FocusedTilesScroll { get; set; }
        private Layer.LayerTilesData FocusedTilesData { get; set; }
        private TileLayerImage FocusedTilesImage { get; set; }

        private enum Painting {
            None,
            Draw,
            Erase,
            Pick,
            DragPick,
            Drag,

        }
        private Painting painting { get; set; } = Painting.None;

        private static CachedTileDataLoader loader = new();
        private byte[] emptyTile { get; set; }
        private Dictionary<uint, byte[]> TileCache { get; set; }

        public Rect GridRect { get; set; }
        public Point GridPoint1 { get; set; }
        public Point GridPoint2 { get; set; }

        public string StatusText { get; set; } = "";

        public UndertaleTileEditor(Layer layer)
        {
            EditingLayer = layer;

            RoomPrevOffsetX = -EditingLayer.XOffset;
            RoomPrevOffsetY = -EditingLayer.YOffset;

            OldTileData = (uint[][])EditingLayer.TilesData.TileData.Clone();
            for (int i = 0; i < OldTileData.Length; i++)
            {
                OldTileData[i] = (uint[])OldTileData[i].Clone();
            }
            TilesData = EditingLayer.TilesData;
            TileCache = new();

            BrushTilesData = new Layer.LayerTilesData();
            BrushTilesData.TileData = new uint[][] { new uint[] { 0 } };
            BrushTilesData.Background = TilesData.Background;
            BrushTilesData.TilesX = 1;
            BrushTilesData.TilesY = 1;
            UpdateBrush();

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
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (
                !apply && Modified && this.ShowQuestion(
                    "Cancel changes to the tilemap?", MessageBoxImage.Warning, "Confirmation"
                ) == MessageBoxResult.No
            ) {
                e.Cancel = true;
                return;
            }
        }
        private void Window_Closed(object sender, EventArgs e)
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

            FindPaletteCursor();
        }
        private void UpdateBrush()
        {
            if (painting == Painting.DragPick)
            {
                BrushWidth = Convert.ToDouble(PaletteTilesData.Background.GMS2TileWidth);
                BrushHeight = Convert.ToDouble(PaletteTilesData.Background.GMS2TileHeight);
            }
            else
            {
                BrushWidth = Convert.ToDouble(
                    (long)BrushTilesData.TilesX * (long)BrushTilesData.Background.GMS2TileWidth
                );
                BrushHeight = Convert.ToDouble(
                    (long)BrushTilesData.TilesY * (long)BrushTilesData.Background.GMS2TileHeight
                );
            }
            BrushEmpty = true;
            for (int y = 0; y < BrushTilesData.TilesY; y++)
            {
                for (int x = 0; x < BrushTilesData.TilesX; x++)
                {
                    if ((BrushTilesData.TileData[y][x] & TILE_INDEX) != 0)
                    {
                        BrushEmpty = false;
                        break;
                    }
                }
                if (!BrushEmpty)
                    break;
            }
            UpdateBrushVisibility();
        }
        private void UpdateBrushVisibility()
        {
            bool over = TilesScroll is not null ? TilesScroll.IsMouseOver : false;
            BrushPreviewVisibility = (painting == Painting.None && over) ? Visibility.Visible : Visibility.Hidden;
            BrushOutlineVisibility = 
                ((BrushEmpty && (painting == Painting.None || painting == Painting.Draw)) ||
                painting == Painting.Erase) && over ? Visibility.Visible : Visibility.Hidden;
            BrushPickVisibility =
                ((painting == Painting.Pick || painting == Painting.DragPick)
                && FocusedTilesImage == LayerImage) ? Visibility.Visible : Visibility.Hidden;
        }

        // Places the current brush onto a tilemap.
        // ox and oy specify the origin point of multi-tile brushes.
        private void PaintTile(int x, int y, int ox, int oy, Layer.LayerTilesData tilesData, bool erase = false)
        {
            int maxX = (int)Math.Min(x + BrushTilesData.TilesX, tilesData.TilesX);
            int maxY = (int)Math.Min(y + BrushTilesData.TilesY, tilesData.TilesY);
            for (int ty = (int)Math.Max(y, 0); ty < maxY; ty++)
            {
                for (int tx = (int)Math.Max(x, 0); tx < maxX; tx++)
                {
                    if (erase)
                        SetTile(tx, ty, tilesData, 0);
                    else
                        SetBrushTile(tilesData, tx, ty, ox, oy);
                }
            }
        }
        private void PaintLine(Layer.LayerTilesData tilesData, Point pos1, Point pos2, Point start, bool erase = false)
        {
            PositionToTile(pos1, tilesData, out int x1, out int y1);
            PositionToTile(pos2, tilesData, out int x2, out int y2);
            PositionToTile(start, tilesData, out int ox, out int oy);

            Line(tilesData, x1, y1, x2, y2, ox, oy, erase);
        }

        private void SetTile(int x, int y, Layer.LayerTilesData tilesData, uint tileID)
        {
            Modified = true;
            if (tilesData.TileData[y][x] != tileID)
            {
                tilesData.TileData[y][x] = tileID;
                DrawTile(
                    tilesData.Background, tilesData.TileData[y][x],
                    TilesBitmap, x, y
                );
            }
        }

        // Places one tile of the current brush.
        // ox and oy specify the origin point of multi-tile brushes.
        private void SetBrushTile(Layer.LayerTilesData tilesData, int x, int y, int ox, int oy)
        {
            int tx = mod(x - ox, (int)BrushTilesData.TilesX);
            int ty = mod(y - oy, (int)BrushTilesData.TilesY);
            uint tile = BrushTilesData.TileData[ty][tx];
            if ((tile & TILE_INDEX) != 0 || BrushEmpty)
                SetTile(x, y, tilesData, tile);
        }

        private int mod(int left, int right)
        {
            int remainder = left % right;
            return remainder < 0 ? remainder + right : remainder;
        }

        private void Fill(Layer.LayerTilesData tilesData, int x, int y, bool global, bool erase = false)
        {
            uint[][] data = tilesData.TileData;
            uint replace = data[y][x];

            if (global)
            {
                for (int fy = 0; fy < tilesData.TilesY; fy++)
                {
                    for (int fx = 0; fx < tilesData.TilesX; fx++)
                    {
                        if (data[fy][fx] == replace)
                        {
                            if (erase)
                                SetTile(fx, fy, tilesData, 0);
                            else
                                SetBrushTile(tilesData, fx, fy, x, y);
                        }
                    }
                }
                return;
            }

            Stack<Tuple<int, int>> stack = new();
            stack.Push(new(x, y));
            while (stack.Count > 0)
            {
                Tuple<int, int> tuple = stack.Pop();
                int fx = tuple.Item1;
                int fy = tuple.Item2;
                if (data[fy][fx] == replace && (!erase || data[fy][fx] != 0))
                {
                    if (erase)
                        SetTile(fx, fy, tilesData, 0);
                    else
                        SetBrushTile(tilesData, fx, fy, x, y);
                    if (fx > 0) stack.Push(new(fx - 1, fy));
                    if (fy > 0) stack.Push(new(fx, fy - 1));
                    if (fx < (tilesData.TilesX - 1)) stack.Push(new(fx + 1, fy));
                    if (fy < (tilesData.TilesY - 1)) stack.Push(new(fx, fy + 1));
                }
            }
        }

        private void Line(Layer.LayerTilesData tilesData, int x1, int y1, int x2, int y2, int ox, int oy, bool erase = false)
        {
            int dx = Math.Abs(x2 - x1);
            int sx = x1 < x2 ? 1 : -1;
            int dy = -Math.Abs(y2 - y1);
            int sy = y1 < y2 ? 1 : -1;
            int error = dx + dy;
            
            while (true)
            {
                PaintTile(x1, y1, settings.BrushTiling ? ox : x1, settings.BrushTiling ? oy : y1, tilesData, erase);
                
                if (x1 == x2 && y1 == y2)
                    break;
                
                int e2 = 2 * error;
                if (e2 >= dy)
                {
                    if (x1 == x2)
                        break;
                    error = error + dy;
                    x1 = x1 + sx;
                }
                if (e2 <= dx)
                {
                    if (y1 == y2)
                        break;
                    error = error + dx;
                    y1 = y1 + sy;
                }
            }
        }

        private void Pick(Point pos, Point drawingStart, Layer.LayerTilesData tilesData)
        {
            PositionToTile(drawingStart, tilesData, out int x1, out int y1);
            x1 = Math.Clamp(x1, 0, (int)tilesData.TilesX - 1);
            y1 = Math.Clamp(y1, 0, (int)tilesData.TilesY - 1);
            PositionToTile(pos, tilesData, out int x2, out int y2);
            x2 = Math.Clamp(x2, 0, (int)tilesData.TilesX - 1);
            y2 = Math.Clamp(y2, 0, (int)tilesData.TilesY - 1);
            if (x2 < x1) {
                int _x1 = x1;
                x1 = x2;
                x2 = _x1;
            }
            if (y2 < y1) {
                int _y1 = y1;
                y1 = y2;
                y2 = _y1;
            }

            BrushTilesData.TilesX = (uint)(Math.Abs(x2 - x1) + 1);
            BrushTilesData.TilesY = (uint)(Math.Abs(y2 - y1) + 1);

            for (int y = 0; y < BrushTilesData.TilesY; y++)
            {
                for (int x = 0; x < BrushTilesData.TilesX; x++)
                {
                    BrushTilesData.TileData[y][x] = tilesData.TileData[y1 + y][x1 + x];
                }
            }

            UpdateBrush();
            
            if (tilesData == PaletteTilesData)
            {
                MovePaletteCursor(x1, y1);
                ResizePaletteCursor();
                PaletteCursorVisibility = Visibility.Visible;
            }
        }

        private void FindPaletteCursor()
        {
            if (BrushTilesData.TilesX > 1 || BrushTilesData.TilesY > 1)
            {
                PaletteCursorVisibility = Visibility.Hidden;
                return;
            }
            PaletteCursorVisibility = Visibility.Visible;
            
            uint brushTile = BrushTilesData.TileData[0][0] & TILE_INDEX;
            int index = PaletteTilesData.Background.GMS2TileIds.FindIndex(
                id => id.ID == brushTile
            );
            if (index == -1)
                index = 0;
            MovePaletteCursor(index);
            ResizePaletteCursor();
            if (PaletteCursor is not null)
                PaletteCursor.BringIntoView();
        }
        private void MovePaletteCursor(int index)
        {
            MovePaletteCursor((index % (int)PaletteTilesData.TilesX), (index / (int)PaletteTilesData.TilesX));
        }
        private void MovePaletteCursor(int x, int y)
        {
            PaletteCursorX = x * (int)PaletteTilesData.Background.GMS2TileWidth;
            PaletteCursorY = y * (int)PaletteTilesData.Background.GMS2TileHeight;
        }
        private void ResizePaletteCursor()
        {
            PaletteCursorWidth = BrushTilesData.TilesX * (int)PaletteTilesData.Background.GMS2TileWidth;
            PaletteCursorHeight = BrushTilesData.TilesY * (int)PaletteTilesData.Background.GMS2TileHeight;
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

            DrawingStart = e.GetPosition(FocusedTilesImage);
            LastMousePos = DrawingStart;

            if (e.MiddleButton == MouseButtonState.Pressed)
            {
                painting = Painting.DragPick;
                DrawingStart = e.GetPosition(this as Window);
                ScrollViewStart = new Point(FocusedTilesScroll.HorizontalOffset, FocusedTilesScroll.VerticalOffset);
                UpdateBrush();
            }
            else if (FocusedTilesScroll == PaletteScroll)
            {
                painting = Painting.Pick;
                Pick(DrawingStart, DrawingStart, FocusedTilesData);
            }
            else if (e.RightButton == MouseButtonState.Pressed)
            {
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                {
                    if (PositionToTile(DrawingStart, FocusedTilesData, out int x, out int y))
                    {
                        Fill(FocusedTilesData, x, y, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift), true);
                        painting = Painting.None;
                    }
                }
                else
                {
                    if (PositionToTile(DrawingStart, FocusedTilesData, out int x, out int y))
                        PaintTile(x, y, x, y, FocusedTilesData, true);
                    painting = Painting.Erase;
                }
            }
            else if (e.LeftButton == MouseButtonState.Pressed)
            {
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
                {
                    Pick(DrawingStart, DrawingStart, FocusedTilesData);
                    painting = Painting.Pick;
                }
                else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                {
                    if (PositionToTile(DrawingStart, FocusedTilesData, out int x, out int y))
                    {
                        Fill(FocusedTilesData, x, y, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift), false);
                        painting = Painting.None;
                    }
                }
                else
                {
                    if (PositionToTile(DrawingStart, FocusedTilesData, out int x, out int y))
                        PaintTile(x, y, x, y, FocusedTilesData, false);
                    painting = Painting.Draw;
                }
            }
            UpdateBrushVisibility();
        }
        private void Tiles_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (painting == Painting.DragPick)
            {
                Pick(e.GetPosition(FocusedTilesImage), e.GetPosition(FocusedTilesImage), FocusedTilesData);
                FindPaletteCursor();
            }
            EndDrawing();
        }
        private void Tiles_MouseMove(object sender, MouseEventArgs e)
        {
            PositionToTile(e.GetPosition(LayerImage as TileLayerImage), TilesData, out int mapX, out int mapY);
            StatusText = $"x: {mapX}  y: {mapY}";

            if (painting != Painting.Pick)
            {
                BrushPreviewX = Convert.ToDouble((long)mapX * (long)TilesData.Background.GMS2TileWidth);
                BrushPreviewY = Convert.ToDouble((long)mapY * (long)TilesData.Background.GMS2TileHeight);
            }
            else
            {
                PositionToTile(
                    DrawingStart, TilesData, out int startX, out int startY
                );
                if (mapX < startX) startX = mapX;
                if (mapY < startY) startY = mapY;
                BrushPreviewX = Convert.ToDouble((long)startX * (long)TilesData.Background.GMS2TileWidth);
                BrushPreviewY = Convert.ToDouble((long)startY * (long)TilesData.Background.GMS2TileHeight);
            }

            UpdateBrushVisibility();

            if (FocusedTilesScroll is null)
                return;

            if (painting == Painting.DragPick || painting == Painting.Drag)
            {
                Point pos = e.GetPosition(this as Window);
                if (painting == Painting.DragPick && pos != DrawingStart)
                {
                    painting = Painting.Drag;
                    UpdateBrush();
                }
                FocusedTilesScroll.ScrollToHorizontalOffset(Math.Clamp(
                    ScrollViewStart.X + -(pos.X - DrawingStart.X), 0, FocusedTilesScroll.ScrollableWidth
                ));
                FocusedTilesScroll.ScrollToVerticalOffset(Math.Clamp(
                    ScrollViewStart.Y + -(pos.Y - DrawingStart.Y), 0, FocusedTilesScroll.ScrollableHeight
                ));
                return;
            }

            if (painting == Painting.Draw)
            {
                Point pos = e.GetPosition(FocusedTilesImage);
                PaintLine(FocusedTilesData, LastMousePos, pos, DrawingStart, false);
                LastMousePos = pos;
            }
            else if (painting == Painting.Erase)
            {
                Point pos = e.GetPosition(FocusedTilesImage);
                PaintLine(FocusedTilesData, LastMousePos, pos, DrawingStart, true);
                LastMousePos = pos;
            }
            else if (painting == Painting.Pick)
                Pick(e.GetPosition(FocusedTilesImage), DrawingStart, FocusedTilesData);
        }
        private void Window_MouseLeave(object sender, MouseEventArgs e)
        {
            EndDrawing();
        }
        private void EndDrawing()
        {
            if (painting == Painting.Pick)
            {
                if (FocusedTilesData != PaletteTilesData)
                {
                    FindPaletteCursor();
                }
                RefreshBrush++;
            }
            painting = Painting.None;
            FocusedTilesScroll = null;
            FocusedTilesData = null;
            FocusedTilesImage = null;
            UpdateBrush();
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
                        wBitmap, x, y
                    );
                }
            }
        }

        // assumes a bgra32 writeablebitmap
        private void DrawTile(UndertaleBackground tileset, uint tile, WriteableBitmap wBitmap, int x, int y)
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
                if (TileCache.ContainsKey(tileID))
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
                    tileID
                );
                return;
            }

            System.Drawing.Bitmap newBMP = (System.Drawing.Bitmap)tileBMP.Clone();

            switch (tile >> 28)
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
                (int)(x * tileset.GMS2TileWidth), (int)(y * tileset.GMS2TileHeight)
            );

            newBMP.Dispose();
        }
        private void DrawBitmapToWBitmap(System.Drawing.Bitmap bitmap, WriteableBitmap wBitmap, int x, int y, uint? cache = null)
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

        private uint[][] CloneTileData(uint[][] tileData)
        {
            uint[][] newTileData = (uint[][])tileData.Clone();
            for (int i = 0; i < tileData.Length; i++)
                newTileData[i] = (uint[])tileData[i].Clone();
            return newTileData;
        }

        private void Command_Mirror(object sender, RoutedEventArgs e)
        {
            for (int y = 0; y < BrushTilesData.TilesY; y++)
            {
                Array.Reverse(BrushTilesData.TileData[y]);
                for (int x = 0; x < BrushTilesData.TilesX; x++)
                    BrushTilesData.TileData[y][x] ^= TILE_FLIP_H;
            }
            RefreshBrush++;
        }
        private void Command_Flip(object sender, RoutedEventArgs e)
        {
            Array.Reverse(BrushTilesData.TileData);
            for (int y = 0; y < BrushTilesData.TilesY; y++)
            {
                for (int x = 0; x < BrushTilesData.TilesX; x++)
                    BrushTilesData.TileData[y][x] ^= TILE_FLIP_V;
            }
            RefreshBrush++;
        }
        private void Command_RotateCW(object sender, RoutedEventArgs e)
        {
            uint[][] oldTileData = CloneTileData(BrushTilesData.TileData);
            uint _tilesX = BrushTilesData.TilesX;
            uint _tilesY = BrushTilesData.TilesY;
            BrushTilesData.TilesX = _tilesY;
            BrushTilesData.TilesY = _tilesX;
            for (int y = 0; y < _tilesY; y++)
            {
                for (int x = 0; x < _tilesX; x++)
                {
                    uint tile = oldTileData[y][x];
                    uint flags = ROTATION_CW[(uint)(tile >> 28)] << 28;
                    BrushTilesData.TileData[x][_tilesY - y - 1] = (uint)((tile & TILE_INDEX) | flags);
                }
            }
            UpdateBrush();
            RefreshBrush++;
        }
        private void Command_RotateCCW(object sender, RoutedEventArgs e)
        {
            uint[][] oldTileData = CloneTileData(BrushTilesData.TileData);
            uint _tilesX = BrushTilesData.TilesX;
            uint _tilesY = BrushTilesData.TilesY;
            BrushTilesData.TilesX = _tilesY;
            BrushTilesData.TilesY = _tilesX;
            for (int y = 0; y < _tilesY; y++)
            {
                for (int x = 0; x < _tilesX; x++)
                {
                    uint tile = oldTileData[y][x];
                    uint flags = ROTATION_CCW[(uint)(tile >> 28)] << 28;
                    BrushTilesData.TileData[_tilesX - x - 1][y] = (uint)((tile & TILE_INDEX) | flags);
                }
            }
            UpdateBrush();
            RefreshBrush++;
        }
        private void Command_ToggleGrid(object sender, RoutedEventArgs e)
        {
            settings.ShowGridBool = !settings.ShowGridBool;
        }
        private void Command_ToggleBrushTiling(object sender, RoutedEventArgs e)
        {
            settings.BrushTiling = !settings.BrushTiling;
        }
        private void Command_TogglePreview(object sender, RoutedEventArgs e)
        {
            settings.RoomPreviewBool = !settings.RoomPreviewBool;
        }

        private bool PositionToTile(Point p, Layer.LayerTilesData tilesData, out int x, out int y)
        {
            x = Convert.ToInt32(Math.Floor(p.X / tilesData.Background.GMS2TileWidth));
            y = Convert.ToInt32(Math.Floor(p.Y / tilesData.Background.GMS2TileHeight));
            
            return TileInBounds(x, y, tilesData);
        }

        private bool TileInBounds(int x, int y, Layer.LayerTilesData tilesData)
        {
            if (x < 0 || y < 0) return false;
            if (x >= tilesData.TilesX || y >= tilesData.TilesY) return false;
            return true;
        }
    }
}
