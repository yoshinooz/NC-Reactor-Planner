﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Reflection;
using System.IO;

namespace NC_Reactor_Planner
{
    public partial class PlannerUI : Form
    {
        public Panel ReactorGrid { get => reactorGrid; }
        public decimal DrawingScale { get => imageScale.Value; }
        public Point PalettePanelLocation { get => new Point(resetLayout.Location.X - Palette.PalettePanel.spacing, resetLayout.Location.Y + resetLayout.Size.Height); }

        ToolTip uiToolTip;
        public static ToolTip gridToolTip;

        public static int blockSize;
        private static ConfigurationUI configurationUI;
        
        private static readonly Pen PaletteHighlightPen = new Pen(Color.Blue, 4);
        public static readonly Pen ErrorPen = new Pen(Brushes.Red, 3);
        public static readonly Pen PrimedFuelCellPen = new Pen(Brushes.Orange, 4);
        public static readonly Pen InactiveClusterPen = new Pen(Brushes.Pink, 4);
        public static readonly Pen ValidModeratorPen = new Pen(Brushes.Green, 3);

        public static bool drawAllLayers = true;
        string appName;
        FileInfo loadedSaveFileInfo;
        public static Block[,] layerBuffer;

        private bool showClustersInStats;

        public PlannerUI()
        {
            InitializeComponent();
            Version aVersion = Assembly.GetExecutingAssembly().GetName().Version;
            appName = string.Format("NC Reactor Planner v{0}.{1}.{2} ", aVersion.Major, aVersion.Minor, aVersion.Build);
            this.Text = appName;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
#if !DEBUG
            SetUpdateAvailableTextAsync();
#endif
            blockSize = (int)(Palette.Textures.First().Value.Size.Height * imageScale.Value);
            showClustersInStats = true;

            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);

            resetLayout.MouseLeave += new EventHandler(ResetButtonFocusLost);
            resetLayout.LostFocus += new EventHandler(ResetButtonFocusLost);

            SetUpToolTips();

            fuelSelector.Items.AddRange(Palette.FuelPalette.Values.ToArray());

            SetUIToolTips();

            ResetLayout(false);
        }

        private void SetUpToolTips()
        {
            uiToolTip = new ToolTip
            {
                AutoPopDelay = 10000,
                InitialDelay = 0,
                ReshowDelay = 0,
            };

            gridToolTip = new ToolTip
            {
                AutoPopDelay = 10000,
                InitialDelay = 1200,
                ReshowDelay = 1000,
            };
        }

        private void SetUIToolTips()
        {
            uiToolTip.SetToolTip(imageScale, "Scale of blocks' textures. Also affects saved PNG scale.");
            uiToolTip.SetToolTip(reactorHeight, "Reactor hight (number of internal layers)");
            uiToolTip.SetToolTip(reactorLength, "Reactor length (Z axis internal size)");
            uiToolTip.SetToolTip(reactorWidth, "Reactor width (X axis internal size)");
            uiToolTip.SetToolTip(layerScrollBar, "Scrolls through reactor layers. Scrollwheel works, so do arrow keys");
            uiToolTip.SetToolTip(viewStyleSwitch, "Toggles between drawing layers one-by-one or all at once.");
            uiToolTip.SetToolTip(saveAsImage, "Saves an image of the reactor. Stats are also added to the output so you have a full description in one picture ^-^");
            uiToolTip.SetToolTip(resetLayout, "Create a new reactor with the specified dimensions. Click again to confirm (overwrites your current layout! Save if you want to keep it.)");
        }

        private void ResetButtonFocusLost(object sender, EventArgs e)
        {
            resetLayout.Text = "Reset layout";
        }

        private void resetLayout_Click(object sender, EventArgs e)
        {
            if (resetLayout.Text == "Confirm reset?")
            {

                resetLayout.Text = "RESETTING...";
                ResetLayout(false);
                resetLayout.Text = "Reset layout";
            }
            else
                resetLayout.Text = "Confirm reset?";
        }

        public void ResetLayout(bool loading)
        {

            EnableUIElements();

            gridToolTip.RemoveAll();
            
            reactorGrid.Controls.Clear();

            if (!loading)
            {
                loadedSaveFileInfo = null;
                Reactor.InitializeReactor((int)reactorWidth.Value, (int)reactorHeight.Value, (int)reactorLength.Value);
            }
            else
            {
                reactorWidth.Value = (int)Reactor.interiorDims.X;
                reactorHeight.Value = (int)Reactor.interiorDims.Y;
                reactorLength.Value = (int)Reactor.interiorDims.Z;
                Reactor.ConstructLayers();
            }

            UpdateWindowTitle();
            fuelSelector.SelectedItem = fuelSelector.Items[0];

            Reactor.Update();

            RefreshStats(showClustersInStats);

            if (drawAllLayers)
            {
                Reactor.Redraw();
                foreach (ReactorGridLayer layer in Reactor.layers)
                    UpdateLocation(layer);
                reactorGrid.Controls.AddRange(Reactor.layers.ToArray());
            }
            else
            {
                ReactorGridLayer layer = Reactor.layers[layerScrollBar.Value - 1];
                UpdateLocation(layer);
                reactorGrid.Controls.Add(layer);
                layerScrollBar.Maximum = (int)Reactor.interiorDims.Y;
            }
            

        }

        private void EnableUIElements()
        {
            if (!drawAllLayers)
                layerScrollBar.Enabled = true;

            layerScrollBar.Value = 1;
            layerLabel.Text = "Layer 1";
            saveReactor.Enabled = true;
            viewStyleSwitch.Enabled = true;
            saveAsImage.Enabled = true;
            imageScale.Enabled = true;
            fuelSelector.Enabled = true;
            fuelBaseEfficiency.Enabled = true;
            fuelBaseHeat.Enabled = true;
            OpenConfig.Enabled = true;
        }

        private void ClearDisposeLayers()
        {
            if (reactorGrid.Controls.Count > 0)
            {
                reactorGrid.Controls.Clear();
                foreach (Control c in Reactor.layers)
                    c.Dispose();
            }
        }

        private void SwitchToPerLayer()
        {
            drawAllLayers = false;
            ResetLayout(true);
            viewStyleSwitch.Text = "Per layer";
            layerScrollBar.Enabled = true;
            layerLabel.Show();
        }

        private void Redraw()
        {
            if (drawAllLayers)
                Reactor.Redraw();
            else
            {
                ReactorGridLayer layer;

                reactorGrid.Controls.Clear();
                layer = Reactor.layers[layerScrollBar.Value - 1];

                UpdateLocation(layer);
                reactorGrid.Controls.Add(layer);
                //layer.Invalidate();
            }
        }

        private void layerScrollBar_ValueChanged(object sender, EventArgs e)
        {
            layerLabel.Text = "Layer " + layerScrollBar.Value;

            Redraw();

            gridToolTip.Active = false;
            gridToolTip.Active = true;
        }

        private void reactorGrid_MouseEnter(object sender, EventArgs e)
        {
            if (configurationUI != null && !configurationUI.IsDisposed)
                return;
            if (drawAllLayers)
                reactorGrid.Focus();
            else
                layerScrollBar.Focus();
        }

        private void saveReactor_Click(object sender, EventArgs e)
        {
            SaveReactor();
        }

        private void SaveReactor()
        {
            using (SaveFileDialog fileDialog = new SaveFileDialog { Filter = "JSON files(*.json)|*.json" })
            {
                fileDialog.FileName = ConstructSaveFileName();
                fileDialog.AddExtension = false;
                if (fileDialog.ShowDialog() == DialogResult.OK)
                {
                    loadedSaveFileInfo = new FileInfo(fileDialog.FileName);
                    Reactor.Save(loadedSaveFileInfo);
                }
            }
        }

        private string ConstructSaveFileName()
        {
            return (loadedSaveFileInfo == null)
                                      ? string.Format("{0} {1} x {2} x {3}", ((Fuel)fuelSelector.SelectedItem).Name, Reactor.interiorDims.X, Reactor.interiorDims.Y, Reactor.interiorDims.Z)
                                      : loadedSaveFileInfo.Name;
        }

        private void loadReactor_Click(object sender, EventArgs e)
        {
            LoadReactor();
        }

        private void LoadReactor()
        {
            using (OpenFileDialog fileDialog = new OpenFileDialog { Filter = "NuclearCraft Reactor files(*.ncr)|*.ncr|JSON files(*.json)|*.json" })
            {
                fileDialog.FilterIndex = 2;
                if (fileDialog.ShowDialog() == DialogResult.OK)
                {
                    loadedSaveFileInfo = new FileInfo(fileDialog.FileName);
                    ValidationResult vr = Reactor.Load(loadedSaveFileInfo);
                    if (!vr.Successful)
                    {
                        MessageBox.Show(vr.Result);
                        loadedSaveFileInfo = null;
                        return;
                    }
                }
                else
                    return;
            }

            reactorHeight.Value = (decimal)Reactor.interiorDims.Y;
            reactorLength.Value = (decimal)Reactor.interiorDims.Z;
            reactorWidth.Value = (decimal)Reactor.interiorDims.X;

            ResetLayout(true);
        }

        public void RefreshStats(bool includeClustersInStats = true)
        {
            stats.Text = Reactor.GetStatString(includeClustersInStats);
        }

        private void viewStyleSwitch_Click(object sender, EventArgs e)
        {
            if (drawAllLayers)
                SwitchToPerLayer();
            else
                SwitchToDrawAllLayers();
        }

        private void SwitchToDrawAllLayers()
        {
            drawAllLayers = true;

            viewStyleSwitch.Text = "All layers";

            ResetLayout(true);

            layerScrollBar.Enabled = false;
            layerLabel.Hide();
        }

        private void saveAsImage_Click(object sender, EventArgs e)
        {
            DialogResult res = MessageBox.Show("Save the entire reactor? Selecting \"No\" will save the currently displayed (or first) layer", "Save entire structure?", MessageBoxButtons.YesNoCancel);
            if (res == DialogResult.Cancel)
                return;
            bool saveAll = (res == DialogResult.Yes) ? true : false;
            string fileName = null;
            using (SaveFileDialog fileDialog = new SaveFileDialog { Filter = "Image files (*.png)|*.png" })
            {
                string autoFileName = "";
                if (loadedSaveFileInfo == null)
                {
                    if (saveAll)
                        autoFileName = string.Format("{0} {1} x {2} x {3}.png", fuelSelector.SelectedItem.ToString(), Reactor.interiorDims.X, Reactor.interiorDims.Y, Reactor.interiorDims.Z);
                    else
                        autoFileName = string.Format("{0} {1} x {2} x {3} layer {4}.png", fuelSelector.SelectedItem.ToString(), Reactor.interiorDims.X, Reactor.interiorDims.Y, Reactor.interiorDims.Z, layerScrollBar.Value);
                }
                else
                {
                    if (saveAll)
                        autoFileName = loadedSaveFileInfo.Name.Replace(".json", "") + ".png";
                    else
                        autoFileName = loadedSaveFileInfo.Name.Replace(".json", "") + " layer " + layerScrollBar.Value + ".png";
                }
                fileDialog.FileName = autoFileName;

                if (fileDialog.ShowDialog() == DialogResult.OK)
                    fileName = fileDialog.FileName;
            }
            if (fileName != null)
            {
                if (saveAll)
                    Reactor.SaveReactorAsImage(fileName, stats.Lines.Length, (int)imageScale.Value);
                else
                    Reactor.SaveLayerAsImage(layerScrollBar.Value, fileName);
            }
        }

        private void imageScale_ValueChanged(object sender, EventArgs e)
        {
            blockSize = (int)(Palette.Textures.First().Value.Size.Height * imageScale.Value);
            
            foreach (ReactorGridLayer layer in Reactor.layers)
            {
                layer.Rescale();
                UpdateLocation(layer);
            }
                
        }

        private void UpdateLocation(ReactorGridLayer layer)
        {
            Point origin;
            if (drawAllLayers)
            {
                int layersPerRow = (int)Math.Ceiling(Math.Sqrt(Reactor.interiorDims.Y));
                origin = new Point((layer.Y - 1) % layersPerRow * layer.Size.Width + (layer.Y - 1) % layersPerRow * blockSize,
                                    (layer.Y - 1) / layersPerRow * layer.Size.Height + (layer.Y - 1) / layersPerRow * blockSize);
            }
            else
            {
                origin = new Point(Math.Max(0, (int)(reactorGrid.Size.Width / 2 - Reactor.interiorDims.X * blockSize / 2)),
                                            Math.Max(0, (int)(reactorGrid.Size.Height / 2 - Reactor.interiorDims.Z * blockSize / 2)));
            }
            layer.Location = origin;
        }

        public void fuelSelector_SelectedIndexChanged(object sender, EventArgs e)
        {
            Fuel selectedFuel = (Fuel)fuelSelector.SelectedItem;
            if(selectedFuel == null)
            {
                fuelSelector.Text = "";
                return;
            }
            fuelBaseEfficiency.Text = selectedFuel.BaseEfficiency.ToString();
            fuelBaseHeat.Text = selectedFuel.BaseHeat.ToString();
            fuelCriticalityFactor.Text = selectedFuel.CriticalityFactor.ToString();
            Palette.SelectedFuel = selectedFuel; //[TODO]Change to a method you criminal
        }

        private void reactorWidth_Enter(object sender, EventArgs e)
        {
            reactorWidth.Select(0, reactorWidth.Value.ToString().Length);
        }

        private void reactorHeight_Enter(object sender, EventArgs e)
        {
            reactorHeight.Select(0, reactorHeight.Value.ToString().Length);
        }

        private void reactorLength_Enter(object sender, EventArgs e)
        {
            reactorLength.Select(0, reactorLength.Value.ToString().Length);
        }

        private void OpenConfiguration(object sender, EventArgs e)
        {
            if (configurationUI == null || configurationUI.IsDisposed)
            {
                configurationUI = new ConfigurationUI();
                configurationUI.FormClosed += new FormClosedEventHandler(ConfigurationClosed);
                configurationUI.Show();
            }
            else
                configurationUI.Focus();
        }

        private void ConfigurationClosed(object sender, FormClosedEventArgs e)
        {
            fuelSelector.Items.Clear();
            fuelSelector.Items.AddRange(Palette.FuelPalette.Values.ToArray());
            Reactor.Redraw();
            RefreshStats(showClustersInStats);
        }

        private void UpdateWindowTitle()
        {
            if (loadedSaveFileInfo != null)
                this.Text = appName + "   " + loadedSaveFileInfo.FullName;
            else
                this.Text = appName;
        }

        private void showClusterInfo_CheckedChanged(object sender, EventArgs e)
        {
            showClustersInStats = showClusterInfo.Checked;
            RefreshStats(showClustersInStats);
        }

        private void PlannerUI_Leave(object sender, EventArgs e)
        {
            gridToolTip.Hide(reactorGrid);
        }

        private async void checkForUpdates_Click(object sender, EventArgs e)
        {
            Tuple<bool,Version,string> updateInfo = await Updater.CheckForUpdateAsync();
            if(updateInfo.Item1)
            {
                DialogResult updatePropmpt = MessageBox.Show("Download " + Updater.ShortVersionString(updateInfo.Item2) + "? Last commit message:\r\n\r\n" + updateInfo.Item3, "Update available!", MessageBoxButtons.YesNo);
                if (updatePropmpt == DialogResult.Yes)
                {
                    SaveFileDialog saveDialog = new SaveFileDialog();
                    saveDialog.FileName = Updater.ExecutableName(updateInfo.Item2);
                    DialogResult saveResult = saveDialog.ShowDialog();
                    if(saveResult == DialogResult.OK)
                        Updater.DownloadVersionAsync(updateInfo.Item2, saveDialog.FileName);
                }
            }
            else
            {
                MessageBox.Show("You are using the latest version: " + Updater.ShortVersionString(Reactor.saveVersion), "No updates");
            }
        }

        private async void SetUpdateAvailableTextAsync()
        {
            Tuple<bool, Version,string> updateInfo = await Updater.CheckForUpdateAsync();
            if (updateInfo.Item1)
            {
                checkForUpdates.Font = new Font(checkForUpdates.Font, FontStyle.Bold);
                checkForUpdates.Text = Updater.ShortVersionString(updateInfo.Item2) + " Available!";
            }
        }
    }
}
