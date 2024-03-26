using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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
        public Layer EditingLayer { get; set; }

        public double EditWidth { get; set; }
        public double EditHeight { get; set; }
        public double PaletteWidth { get; set; }
        public double PaletteHeight { get; set; }

        private uint BrushTile { get; set; }

        private uint[][] OldTileData { get; set; }
        public Layer.LayerTilesData TilesData { get; set; }
        public Layer.LayerTilesData PaletteTilesData { get; set; }
        public long RefreshTiles { get; set; }


        public Point ScrollViewStart { get; set; }
        public Point ScrollMouseStart { get; set; }

        private bool apply { get; set; }
        private bool canPick { get; set; }

        private ScrollViewer FocusedTilesScroll { get; set; }
        private Layer.LayerTilesData FocusedTilesData { get; set; }
        private TileLayerImage FocusedTilesImage { get; set; }

        public UndertaleTileEditor(Layer layer)
        {
            EditingLayer = layer;

            apply = false;
            canPick = false;

            BrushTile = 0;
            OldTileData = (uint[][])EditingLayer.TilesData.TileData.Clone();
            for (int i = 0; i < OldTileData.Length; i++)
            {
                OldTileData[i] = (uint[])OldTileData[i].Clone();
            }
            TilesData = EditingLayer.TilesData;
            RefreshTiles = 0;

            PaletteTilesData = new Layer.LayerTilesData();
            PaletteTilesData.TileData = new uint[][] { new uint[] { 0 } };
            PaletteTilesData.Background = TilesData.Background;
            PaletteTilesData.TilesX = PaletteTilesData.Background.GMS2TileColumns;
            PopulatePalette();

            EditWidth = Convert.ToDouble((long)TilesData.TilesX * (long)TilesData.Background.GMS2TileWidth);
            EditHeight = Convert.ToDouble((long)TilesData.TilesY * (long)TilesData.Background.GMS2TileHeight);

            this.DataContext = this;
            InitializeComponent();
            
            CachedTileDataLoader.Reset();
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
        }

        private void PaintTile(Layer.LayerTilesData tilesData, Point pos, uint tileID)
        {
            if (tilesData == PaletteTilesData)
                return;
            
            int x = Convert.ToInt32(Math.Floor(pos.X / tilesData.Background.GMS2TileWidth));
            int y = Convert.ToInt32(Math.Floor(pos.Y / tilesData.Background.GMS2TileHeight));
            if (SetTile(x, y, tilesData, tileID))
            {
                RefreshTiles++;
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
            BrushTile = tilesData.TileData[y][x];
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
    }
}
