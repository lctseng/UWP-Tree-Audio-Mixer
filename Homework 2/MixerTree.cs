using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Media.Audio;
using Windows.Media.Render;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace Homework_2
{

    public class MixerTree
    {

        const int BUTTON_WIDTH = 150;
        const int BUTTON_HEIGHT = 50;

        private MainPage rootPage;
        private Canvas displayCanvas;

        AudioGraph graph;

        

        private bool playing;

        private Node rootNode;
        public Node editingNode;

        public enum NodeType {
            Input, Output, Mixer
        }


        public class Node {
            public List<Node> outGoingNodes;
            public List<Node> incomingNodes;
            
            public IAudioNode audioNode;
            public NodeType type;

            public int sortIndex;
            public int childSerial;

            public Button currentButton;

            public Node() {
                outGoingNodes = new List<Node>();
                incomingNodes = new List<Node>();
                sortIndex = 1;
                childSerial = 0;
            }

            public virtual void AddOutgoingConnection(Node target) {
                
            }

            public virtual Button CreateUIButtom() {
                var btn = currentButton = new Button();
                btn.Content = "Dummy Node";
                btn.Width = BUTTON_WIDTH;
                btn.Height = BUTTON_HEIGHT;
                btn.Tag = this;
                btn.Click += MainPage.Current.TreeNode_Click;
                return btn;
            }

            protected void PropagateSortIndex(Node parent) {
                this.sortIndex = parent.sortIndex + 1;
                parent.childSerial++;
            }
        }

        class InputNode : Node{
            public InputNode() {
                type = NodeType.Input;
            }

            public override void AddOutgoingConnection(Node target)
            {
                GetNode().AddOutgoingConnection(target.audioNode);

                outGoingNodes.Add(target);
                target.incomingNodes.Add(this);

                PropagateSortIndex(target);

            }
            public override Button CreateUIButtom()
            {
                var btn = base.CreateUIButtom();
                btn.Content = "Input File";
                return btn;
            }

            public AudioFileInputNode GetNode()
            {
                return ((AudioFileInputNode)this.audioNode);
            }
        }

        class OutputNode : Node {
            public OutputNode()
            {
                type = NodeType.Output;
            }

            public override void AddOutgoingConnection(Node target)
            {
                MainPage.Current.NotifyUser("Cannot add outgoing node to output device", NotifyType.ErrorMessage);
            }

            public AudioDeviceOutputNode GetNode()
            {
                return ((AudioDeviceOutputNode)this.audioNode);
            }

            public override Button CreateUIButtom()
            {
                var btn = base.CreateUIButtom();
                btn.Content = "Output Device";
                return btn;
            }
        }

        class MixerNode : Node
        {
            public MixerNode()
            {
                type = NodeType.Mixer;
            }

            public override void AddOutgoingConnection(Node target)
            {
                GetNode().AddOutgoingConnection(target.audioNode);

                outGoingNodes.Add(target);
                target.incomingNodes.Add(this);

                PropagateSortIndex(target);

            }

            public AudioSubmixNode GetNode() {
                return ((AudioSubmixNode)this.audioNode);
            }

            public override Button CreateUIButtom()
            {
                var btn = base.CreateUIButtom();
                btn.Width = BUTTON_HEIGHT;
                btn.Content = "Mix";
                btn.Background = new SolidColorBrush(Colors.DarkCyan);
                return btn;
            }
        }

        class MixerAnchor {
            public Node src;
            public Node dst;
            public Button anchor;
            public Line line1, line2;
            public double centerX, centerY;
        }


        public MixerTree(Canvas displayCanvas)
        {
            playing = false;
            rootPage = MainPage.Current;
            this.displayCanvas = displayCanvas;
            InitializeTreeAsync();
        }

        private async void InitializeTreeAsync()
        {
            await CreateAudioGraph();;
            rootNode = await CreateDeviceOutputNode();
        }

        // this is test only
        public async Task<bool> LoadInitFile() {
            Stop();
            // init node
            var initInputNode = await CreateFileInputNode();
            if (initInputNode != null)
            {
                initInputNode.AddOutgoingConnection(rootNode);
                return true;
            }
            return false;
        }

        public void Play() {
            if (!playing) {
                graph.Start();
                playing = true;
            }
        }

        public void Stop() {
            if (playing)
            {
                graph.Stop();
                playing = false;
            }
        }

        public void TogglePlay()
        {
            if (playing)
            {
                Stop();
            }
            else {
                Play();
            }
        }

        private async Task<OutputNode> CreateDeviceOutputNode() {
            // Create a device output node
            CreateAudioDeviceOutputNodeResult outputDeviceNodeResult = await graph.CreateDeviceOutputNodeAsync();

            if (outputDeviceNodeResult.Status != AudioDeviceNodeCreationStatus.Success)
            {
                // Cannot create device output node
                rootPage.NotifyUser(String.Format("Audio Device Output unavailable because {0}", outputDeviceNodeResult.Status.ToString()), NotifyType.ErrorMessage);
                return null;
            }


            // create a new tree node
            OutputNode node = new OutputNode();
            node.audioNode = outputDeviceNodeResult.DeviceOutputNode;
            rootPage.NotifyUser("Device Output Node successfully created", NotifyType.StatusMessage);
            return node;
        }

        private async Task<InputNode> CreateFileInputNode() {
            FileOpenPicker filePicker = new FileOpenPicker();
            filePicker.SuggestedStartLocation = PickerLocationId.MusicLibrary;
            filePicker.FileTypeFilter.Add(".mp3");
            filePicker.FileTypeFilter.Add(".wma");
            filePicker.FileTypeFilter.Add(".wav");
            filePicker.ViewMode = PickerViewMode.Thumbnail;
            StorageFile file = await filePicker.PickSingleFileAsync();

            // File can be null if cancel is hit in the file picker
            if (file == null)
            {
                return null;
            }

            CreateAudioFileInputNodeResult fileInputResult = await graph.CreateFileInputNodeAsync(file);
            if (fileInputResult.Status != AudioFileNodeCreationStatus.Success)
            {
                // Error reading the input file
                rootPage.NotifyUser(String.Format("Can't read input file because {0}", fileInputResult.Status.ToString()), NotifyType.ErrorMessage);
                return null;
            }

            // File loaded successfully. Enable these buttons in the UI

            InputNode node = new InputNode();
            node.audioNode = fileInputResult.FileInputNode;

            rootPage.NotifyUser("Successfully loaded input file", NotifyType.StatusMessage);
            return node;

        }

        private async Task CreateAudioGraph()
        {
            // Create an AudioGraph with default settings
            AudioGraphSettings settings = new AudioGraphSettings(AudioRenderCategory.Media);
            CreateAudioGraphResult result = await AudioGraph.CreateAsync(settings);

            if (result.Status != AudioGraphCreationStatus.Success)
            {
                // Cannot create graph
                rootPage.NotifyUser(String.Format("AudioGraph Creation Error because {0}", result.Status.ToString()), NotifyType.ErrorMessage);
                return;
            }

            graph = result.Graph;
        }


        public void RefreshUI() {
            if (rootNode != null)
            {
                displayCanvas.Children.Clear();
                DrawTree();
            }
        }

        private void DrawTree() {
            List<Node> nextLayerNodes = new List<Node>();
            Queue<Node> todos = new Queue<Node>() ;
            todos.Enqueue(rootNode);
            // draw root node
            var rootBtn = rootNode.CreateUIButtom();
            displayCanvas.Children.Add(rootBtn);
            Canvas.SetZIndex(rootBtn, rootNode.sortIndex);
            // Launch a BFS
            while (todos.Count > 0)
            {
                var currentNode = todos.Dequeue();

                for (int i = 0; i < currentNode.incomingNodes.Count; i++) {
                    // draw this node
                    var node = currentNode.incomingNodes[i];
                    var button = node.CreateUIButtom();
                    var margin = button.Margin;
                    margin.Left = (node.sortIndex - 1) * 250;
                    margin.Top = i * 150;
                    button.Margin = margin;
                    displayCanvas.Children.Add(button);
                    Canvas.SetZIndex(button, node.sortIndex);
                    todos.Enqueue(node);
                    DrawEdge(node, currentNode);
                }
            }
        }

        private void DrawEdge(Node src, Node dst) {

            double startX = GetCenterX(src.currentButton);
            double startY = GetCenterY(src.currentButton);
            double endX = GetCenterX(dst.currentButton);
            double endY = GetCenterY(dst.currentButton);
            double midX = (startX + endX) / 2;
            double midY = (startY + endY) / 2;

            // line1 : src to mid
            var line1 = DrawLine(startX - BUTTON_WIDTH / 2, startY, midX + BUTTON_HEIGHT / 2, midY);
            // line2 : mid to dst
            var line2 = DrawLine(midX - BUTTON_HEIGHT / 2, midY, endX + BUTTON_WIDTH / 2, endY);
            // mixer anchor 
            var anchor = DrawMixerAnchor(midX, midY);
            var data = (MixerAnchor)anchor.Tag;
            data.line1 = line1;
            data.line2 = line2;
            data.src = src;
            data.dst = dst;



        }

        private Button DrawMixerAnchor(double posX, double posY)
        {
            var button = new Button();
            button.Background = new SolidColorBrush(Colors.DarkOliveGreen);
            button.Content = "+";
            button.Width = BUTTON_HEIGHT;
            button.Height = BUTTON_HEIGHT;
            var margin = button.Margin;
            margin.Left = posX - BUTTON_HEIGHT / 2;
            margin.Top = posY - BUTTON_HEIGHT / 2;
            button.Margin = margin;
            button.Click += MixerAnchor_Click;

            var data = new MixerAnchor();
            data.anchor = button;
            data.centerX = posX;
            data.centerY = posY;
            button.Tag = data;

            Canvas.SetZIndex(button, 90);
            displayCanvas.Children.Add(button);
            return button;

        }

        private Line DrawLine(double startX, double startY, double endX, double endY)
        {
            var line1 = new Line();
            line1.StrokeThickness = 5;
            line1.Stroke = new SolidColorBrush(Windows.UI.Color.FromArgb(60, 255, 0, 0));
            line1.X1 = startX;
            line1.Y1 = startY;
            line1.X2 = endX;
            line1.Y2 = endY;
            displayCanvas.Children.Add(line1);
            Canvas.SetZIndex(line1, 0);
            return line1;
        }

        public double GetCenterX(FrameworkElement e)
        {
            return e.Margin.Left + e.Width / 2; 
        }

        public double GetCenterY(FrameworkElement e)
        {
            return e.Margin.Top + e.Height / 2;
        }

        private void CreateMixerFromClick(MixerAnchor anchorData)
        {
            // create btn
            var mixer = new MixerNode();
            var btn = mixer.CreateUIButtom();
            displayCanvas.Children.Add(btn);
            // positioning
            var margin = btn.Margin;
            margin.Left = anchorData.centerX - BUTTON_HEIGHT / 2;
            margin.Top = anchorData.centerY - BUTTON_HEIGHT / 2;

            btn.Margin = margin;
        }

        public void MixerAnchor_Click(object sender, RoutedEventArgs e)
        {
            Button btn = (Button)(sender);
            // hide anchor
            displayCanvas.Children.Remove(btn);
            // Create mixer
            CreateMixerFromClick((MixerAnchor)btn.Tag);
        }

        public void CreateIncomingFor(Node parent)
        {

        }

        public void CreateSiblingFor(Node parent)
        {

        }
    }
    
}
