﻿using Microsoft.Win32;
using NAudio.Vorbis;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using UndertaleModLib.Models;

namespace UndertaleModTool
{
    /// <summary>
    /// Logika interakcji dla klasy UndertaleEmbeddedAudioEditor.xaml
    /// </summary>
    public partial class UndertaleEmbeddedAudioEditor : DataUserControl
    {
        private WaveOutEvent waveOut;
        private WaveFileReader wavReader;
        private VorbisWaveReader oggReader;

        private static readonly MainWindow mainWindow = Application.Current.MainWindow as MainWindow;

        public UndertaleEmbeddedAudioEditor()
        {
            InitializeComponent();
            this.Unloaded += Unload;
        }

        public void Unload(object sender, RoutedEventArgs e)
        {
            if (waveOut != null)
                waveOut.Stop();
        }

        private void Import_Click(object sender, RoutedEventArgs e)
        {
            UndertaleEmbeddedAudio target = DataContext as UndertaleEmbeddedAudio;
            bool isWav = target.Data[0] == 'R' && target.Data[1] == 'I' && target.Data[2] == 'F' && target.Data[3] == 'F';
            bool isOgg = target.Data[0] == 'O' && target.Data[1] == 'g' && target.Data[2] == 'g' && target.Data[3] == 'S';

            OpenFileDialog dlg = new OpenFileDialog();

            if (isWav) {
                dlg.DefaultExt = ".wav";
                dlg.Filter = "WAV files|*.wav|All files|*";
            } else if (isOgg) {
                dlg.DefaultExt = ".ogg";
                dlg.Filter = "OGG files|*.ogg|All files|*";
            } else {
                dlg.DefaultExt = "";
                dlg.Filter = "All files|*";
            }

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    byte[] data = File.ReadAllBytes(dlg.FileName);
                    bool dataIsWav = data[0] == 'R' && data[1] == 'I' && data[2] == 'F' && data[3] == 'F';
                    bool dataIsOgg = data[0] == 'O' && data[1] == 'g' && data[2] == 'g' && data[3] == 'S';
                    if (!dataIsWav && !dataIsOgg) {
                        mainWindow.ShowError("Failed to import file!\r\nNot a WAV or OGG.", "Failed to import file");
                    }
                    else if ((isWav && dataIsOgg) || (isOgg && dataIsWav)) {
                        mainWindow.ShowError("Failed to import file!\r\nFormat doesn't match the original file.", "Failed to import file");
                    } else
                        target.Data = data;
                }
                catch (Exception ex)
                {
                    mainWindow.ShowError("Failed to import file: " + ex.Message, "Failed to import file");
                }
            }
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            UndertaleEmbeddedAudio target = DataContext as UndertaleEmbeddedAudio;
            bool isWav = target.Data[0] == 'R' && target.Data[1] == 'I' && target.Data[2] == 'F' && target.Data[3] == 'F';
            bool isOgg = target.Data[0] == 'O' && target.Data[1] == 'g' && target.Data[2] == 'g' && target.Data[3] == 'S';

            SaveFileDialog dlg = new SaveFileDialog();
            if (isWav) {
                dlg.DefaultExt = ".wav";
                dlg.Filter = "WAV files|*.wav|All files|*";
            } else if (isOgg) {
                dlg.DefaultExt = ".ogg";
                dlg.Filter = "OGG files|*.ogg|All files|*";
            } else {
                dlg.DefaultExt = "";
                dlg.Filter = "All files|*";
            }

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllBytes(dlg.FileName, target.Data);
                }
                catch (Exception ex)
                {
                    mainWindow.ShowError("Failed to export file: " + ex.Message, "Failed to export file");
                }
            }
        }

        private void InitAudio()
        {
            if (waveOut == null)
                waveOut = new WaveOutEvent() { DeviceNumber = 0 };
            else if (waveOut.PlaybackState != PlaybackState.Stopped)
                waveOut.Stop();
        }

        private void Play_Click(object sender, RoutedEventArgs e)
        {
            UndertaleEmbeddedAudio target = DataContext as UndertaleEmbeddedAudio;
            bool isWav = target.Data[0] == 'R' && target.Data[1] == 'I' && target.Data[2] == 'F' && target.Data[3] == 'F';
            bool isOgg = target.Data[0] == 'O' && target.Data[1] == 'g' && target.Data[2] == 'g' && target.Data[3] == 'S';

            if (target.Data.Length > 4)
            {
                try
                {
                    if (isWav)
                    {
                        wavReader = new WaveFileReader(new MemoryStream(target.Data));
                        InitAudio();
                        waveOut.Init(wavReader);
                        waveOut.Play();
                    }
                    else if (isOgg)
                    {
                        oggReader = new VorbisWaveReader(new MemoryStream(target.Data));
                        InitAudio();
                        waveOut.Init(oggReader);
                        waveOut.Play();
                    } else
                        mainWindow.ShowError("Failed to play audio!\r\nNot a WAV or OGG.", "Audio failure");
                } catch (Exception ex)
                {
                    waveOut = null;
                    mainWindow.ShowError("Failed to play audio!\r\n" + ex.Message, "Audio failure");
                }
            }
        }


        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            if (waveOut != null)
                waveOut.Stop();
        }
    }
}
