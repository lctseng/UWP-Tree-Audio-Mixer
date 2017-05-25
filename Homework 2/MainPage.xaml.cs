using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
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
            tree = new MixerTree(TreeDisplay);
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
            tree.editingNode = node;
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
        }

        public void LinkButton_Incoming_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var node = (MixerTree.Node)button.Tag;
            tree.CreateIncomingFor(node);
        }

        private void LinkButton_Sibling_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var node = (MixerTree.Node)button.Tag;
            tree.CreateSiblingFor(node);
        }
    }

    public enum NotifyType
    {
        StatusMessage,
        ErrorMessage
    };
}
