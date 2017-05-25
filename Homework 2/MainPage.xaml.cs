using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

//空白頁項目範本收錄在 http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace Homework_2
{
    /// <summary>
    /// 可以在本身使用或巡覽至框架內的空白頁面。
    /// </summary>
    public sealed partial class MainPage : Page
    {

        private MainPage rootPage;
        public static MainPage Current;
        private MixerTree tree;


        public MainPage()
        {
            this.InitializeComponent();
            Current = this;
            rootPage = this;
            
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            tree = new MixerTree(TreeDisplay);
            RefreshControlPanel(null);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            // Destroy the graph if the page is naviated away from
            if (tree != null)
            {
                tree.Dispose();
            }
        }

        /// <summary>
        /// Display a message to the user.
        /// This method may be called from any thread.
        /// </summary>
        /// <param name="strMessage"></param>
        /// <param name="type"></param>
        public void NotifyUser(string strMessage, NotifyType type)
        {
            // If called from the UI thread, then update immediately.
            // Otherwise, schedule a task on the UI thread to perform the update.
            if (Dispatcher.HasThreadAccess)
            {
                UpdateStatus(strMessage, type);
            }
            else
            {
                var task = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => UpdateStatus(strMessage, type));
            }
        }

        private void UpdateStatus(string strMessage, NotifyType type)
        {
            switch (type)
            {
                case NotifyType.StatusMessage:
                    StatusBorder.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                    break;
                case NotifyType.ErrorMessage:
                    StatusBorder.Background = new SolidColorBrush(Windows.UI.Colors.Red);
                    break;
            }

            StatusBlock.Text = strMessage;

            // Collapse the StatusBlock if it has no text to conserve real estate.
            StatusBorder.Visibility = (StatusBlock.Text != String.Empty) ? Visibility.Visible : Visibility.Collapsed;
            if (StatusBlock.Text != String.Empty)
            {
                StatusBorder.Visibility = Visibility.Visible;
                StatusPanel.Visibility = Visibility.Visible;
            }
            else
            {
                StatusBorder.Visibility = Visibility.Collapsed;
                StatusPanel.Visibility = Visibility.Collapsed;
            }
        }


        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Splitter.IsPaneOpen = !Splitter.IsPaneOpen;

        }

        private async void LoadInitTree(object sender, RoutedEventArgs e)
        {
            bool result = await tree.LoadInitFile();
            if (result)
            {
                tree.Play();
                tree.RefreshUI();
            }
        }

        private void ButtonPlay_Click(object sender, RoutedEventArgs e)
        {
            tree.TogglePlay();
            if (tree.IsPlaying())
            {
                buttonPlay.Content = "Stop";
            }
            else {
                buttonPlay.Content = "Play";
            }
        }

        private void RefreshTree(object sender, RoutedEventArgs e)
        {
            tree.RefreshUI();
        }

        public void TreeNode_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var node = (MixerTree.Node)button.Tag;
            RefreshControlPanel(node);
        }

        public void RefreshControlPanel(MixerTree.Node node)
        {
            if(node != null)
            {
                TextForNode.IsReadOnly = false;
                tree.editingNode = node;
                TextForNode.Text = node.name;
                // force open panel
                Splitter.IsPaneOpen = true;
                LinkPanel.Visibility = Visibility.Visible;
                // open effect panel for input/mixer
                if (node.type == MixerTree.NodeType.Input || node.type == MixerTree.NodeType.Mixer)
                {
                    EffectPanel.Visibility = Visibility.Visible;
                }
                else
                {
                    EffectPanel.Visibility = Visibility.Collapsed;
                }
                // open input panel for input
                if (node.type == MixerTree.NodeType.Input)
                {
                    InputPanel.Visibility = Visibility.Visible;
                }
                else
                {
                    InputPanel.Visibility = Visibility.Collapsed;
                }
                // enable sibling button for mixer and incoming
                ButtonCreateSibling.IsEnabled = node.type == MixerTree.NodeType.Input || node.type == MixerTree.NodeType.Mixer;
                // enable incoming for mixer and output
                ButtonCreateIncoming.IsEnabled = node.type == MixerTree.NodeType.Output || node.type == MixerTree.NodeType.Mixer;
                // enable delete button for non-output node
                ButtonDelete.IsEnabled = node.type != MixerTree.NodeType.Output;
                // refresh playback control
                if(node.type == MixerTree.NodeType.Input)
                {
                    RefreshPlaybackControl((MixerTree.InputNode)node);
                }
                // refresh effect control
                if (node.type == MixerTree.NodeType.Input || node.type == MixerTree.NodeType.Mixer)
                {
                    RefreshEffectControl(node);
                }
            }
            else
            {
                TextForNode.IsReadOnly = true;
                TextForNode.Text = "(No node selected)";
                LinkPanel.Visibility = Visibility.Collapsed;
                EffectPanel.Visibility = Visibility.Collapsed;
                InputPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void RefreshPlaybackControl(MixerTree.InputNode node)
        {
            // refresh loop
            loopToggle.IsOn = node.GetNode().LoopCount == null;
            // refresh speed
            playSpeedSlider.Value = node.GetNode().PlaybackSpeedFactor;
        }

        private void RefreshEffectControl(MixerTree.Node node)
        {
            // update limiter
            limiterEffectToggle.IsOn = node.limiterEnabled;
            loudnessSlider.Value = tree.editingNode.limiterEffectDefinition.Loudness;
            UpdateLimiterUI();
            // update equalizer
            eqToggle.IsOn = node.eqEnabled;
            eq1Slider.Value = InverseConvertRange(tree.editingNode.eqEffectDefinition.Bands[0].Gain);
            eq2Slider.Value = InverseConvertRange(tree.editingNode.eqEffectDefinition.Bands[1].Gain);
            eq3Slider.Value = InverseConvertRange(tree.editingNode.eqEffectDefinition.Bands[2].Gain);
            eq4Slider.Value = InverseConvertRange(tree.editingNode.eqEffectDefinition.Bands[3].Gain);
            UpdateEqualizerUI();

        }

        public async void LinkButton_Incoming_Click(object sender, RoutedEventArgs e)
        {
            await tree.CreateIncomingForEditing();
        }

        private async void LinkButton_Sibling_Click(object sender, RoutedEventArgs e)
        {
            await tree.CreateSiblingForEditing();
        }

        private void LinkButton_Delete_Click(object sender, RoutedEventArgs e)
        {
            tree.DeleteEditingNode();
            RefreshControlPanel(null);
        }

        private void PlaySpeedSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (tree != null)
            {
                if (tree.editingNode != null)
                {
                    ((MixerTree.InputNode)tree.editingNode).GetNode().PlaybackSpeedFactor = playSpeedSlider.Value;
                }
            }
        }

        private void LoopToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (tree != null)
            {
                if (tree.editingNode != null)
                {
                    var fileInput = ((MixerTree.InputNode)tree.editingNode).GetNode();
                    // Set loop count to null for infinite looping
                    // Set loop count to 0 to stop looping after current iteration
                    // Set loop count to non-zero value for finite looping
                    if (loopToggle.IsOn)
                    {
                        // If turning on looping, make sure the file hasn't finished playback yet
                        if (fileInput.Position >= fileInput.Duration - TimeSpan.FromSeconds(0.3))
                        {
                            // If finished playback, seek back to the start time we set
                            fileInput.Seek(TimeSpan.FromSeconds(0));
                            
                        }
                        fileInput.LoopCount = null; // infinite looping
                    }
                    else
                    {
                        fileInput.LoopCount = 0; // stop looping
                    }
                }
            }
        }

        private void LimiterEffectToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (tree != null)
            {
                if (tree.editingNode != null)
                {
                    // Enable/Disable the effect in the graph
                    // Also enable/disable the associated UI for effect parameters
                    if (limiterEffectToggle.IsOn)
                    {
                        tree.editingNode.audioNode.EnableEffectsByDefinition(tree.editingNode.limiterEffectDefinition);
                    }
                    else
                    {
                        tree.editingNode.audioNode.DisableEffectsByDefinition(tree.editingNode.limiterEffectDefinition);
                    }
                    tree.editingNode.limiterEnabled = limiterEffectToggle.IsOn;
                    UpdateLimiterUI();
                }
            }
        }

        private void UpdateLimiterUI()
        {
            if (limiterEffectToggle.IsOn)
            {
                loudnessSlider.IsEnabled = true;
                loudnessLabel.Foreground = new SolidColorBrush(Colors.Black);
            }
            else
            {
                loudnessSlider.IsEnabled = false;
                loudnessLabel.Foreground = new SolidColorBrush(Color.FromArgb(255, 74, 74, 74));
            }
            uint currentValue = (uint)loudnessSlider.Value;
            loudnessLabel.Text = "Loudness: " + currentValue.ToString();
        }

        private void LoudnessSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (tree != null)
            {
                if (tree.editingNode != null)
                {
                    if (tree.editingNode.limiterEffectDefinition != null)
                    {
                        uint currentValue = (uint)loudnessSlider.Value;
                        tree.editingNode.limiterEffectDefinition.Loudness = currentValue;
                        UpdateLimiterUI();
                    }
                }
            }
        }

        private void UpdateEqualizerUI()
        {
            if (eqToggle.IsOn)
            {
                eq1Slider.IsEnabled = true;
                eq2Slider.IsEnabled = true;
                eq3Slider.IsEnabled = true;
                eq4Slider.IsEnabled = true;
                eq1SliderLabel.Foreground = new SolidColorBrush(Colors.Black);
                eq2SliderLabel.Foreground = new SolidColorBrush(Colors.Black);
                eq3SliderLabel.Foreground = new SolidColorBrush(Colors.Black);
                eq4SliderLabel.Foreground = new SolidColorBrush(Colors.Black);
            }
            else
            {
                eq1Slider.IsEnabled = false;
                eq2Slider.IsEnabled = false;
                eq3Slider.IsEnabled = false;
                eq4Slider.IsEnabled = false;
                eq1SliderLabel.Foreground = new SolidColorBrush(Color.FromArgb(255, 74, 74, 74));
                eq2SliderLabel.Foreground = new SolidColorBrush(Color.FromArgb(255, 74, 74, 74));
                eq3SliderLabel.Foreground = new SolidColorBrush(Color.FromArgb(255, 74, 74, 74));
                eq4SliderLabel.Foreground = new SolidColorBrush(Color.FromArgb(255, 74, 74, 74));
            }
        }

        private void EqToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (tree != null)
            {
                if (tree.editingNode != null)
                {
                    // Enable/Disable the effect in the graph
                    // Also enable/disable the associated UI for effect parameters
                    if (eqToggle.IsOn)
                    {
                        tree.editingNode.audioNode.EnableEffectsByDefinition(tree.editingNode.eqEffectDefinition);
                    }
                    else
                    {
                        tree.editingNode.audioNode.DisableEffectsByDefinition(tree.editingNode.eqEffectDefinition);
                    }
                    tree.editingNode.eqEnabled = eqToggle.IsOn;
                    UpdateEqualizerUI();
                }
            }
        }

        // Mapping the 0-100 scale of the slider to a value between the min and max gain
        private double ConvertRange(double value)
        {
            // These are the same values as the ones in xapofx.h
            const double fxeq_min_gain = 0.126;
            const double fxeq_max_gain = 7.94;

            double scale = (fxeq_max_gain - fxeq_min_gain) / 100;
            return (fxeq_min_gain + ((value) * scale));
        }
        
        // mapping gain to 0-100
        private double InverseConvertRange(double gain)
        {
            // These are the same values as the ones in xapofx.h
            const double fxeq_min_gain = 0.126;
            const double fxeq_max_gain = 7.94;

            double diff = (fxeq_max_gain - fxeq_min_gain);
            return (gain - fxeq_min_gain)/diff * 100;
        }

        // Change effect paramters to reflect UI control
        private void Eq1Slider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (tree != null)
            {
                if (tree.editingNode != null)
                {
                    if (tree.editingNode.eqEffectDefinition != null)
                    {
                        double currentValue = ConvertRange(eq1Slider.Value);
                        tree.editingNode.eqEffectDefinition.Bands[0].Gain = currentValue;
                    }
                }
            }
        }

        private void Eq2Slider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (tree != null)
            {
                if (tree.editingNode != null)
                {

                    if (tree.editingNode.eqEffectDefinition != null)
                    {
                        double currentValue = ConvertRange(eq2Slider.Value);
                        tree.editingNode.eqEffectDefinition.Bands[1].Gain = currentValue;
                    }
                }
            }
        }

        private void Eq3Slider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (tree != null)
            {
                if (tree.editingNode != null)
                {
                    if (tree.editingNode.eqEffectDefinition != null)
                    {
                        double currentValue = ConvertRange(eq3Slider.Value);
                        tree.editingNode.eqEffectDefinition.Bands[2].Gain = currentValue;
                    }
                }
            }
        }

        private void Eq4Slider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (tree != null)
            {
                if (tree.editingNode != null)
                {
                    if (tree.editingNode.eqEffectDefinition != null)
                    {
                        double currentValue = ConvertRange(eq4Slider.Value);
                        tree.editingNode.eqEffectDefinition.Bands[3].Gain = currentValue;
                    }
                }
            }
        }

        private void TextBoxForNode_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (tree != null)
            {
                if (tree.editingNode != null)
                {
                    tree.editingNode.name = TextForNode.Text;
                    tree.editingNode.currentButton.Content = TextForNode.Text;
                }
            }
        }
    }

    public enum NotifyType
    {
        StatusMessage,
        ErrorMessage
    };
}
