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

    public class MixerTree : IDisposable
    {
        const int BUTTON_WIDTH = 150;
        const int BUTTON_HEIGHT = 50;

        private MainPage rootPage;
        private Canvas displayCanvas;

        AudioGraph graph;



        private bool playing;

        private Node rootNode;
        public Node editingNode;

        public enum NodeType
        {
            Input, Output, Mixer
        }


        public class Node : IDisposable{
            public List<Node> outGoingNodes;
            public List<Node> incomingNodes;

            public IAudioNode audioNode;
            public NodeType type;

            public Button currentButton;

            public double elementHeight;
            public int serial;

            public string name;

            public Line line1, line2;
            public Button anchor;

            public LimiterEffectDefinition limiterEffectDefinition;
            public EqualizerEffectDefinition eqEffectDefinition;

            public bool limiterEnabled, eqEnabled;

            public Node()
            {
                outGoingNodes = new List<Node>();
                incomingNodes = new List<Node>();
                elementHeight = 0;
                serial = 0;
                name = "Dummy Node";
                limiterEnabled = false;
                eqEnabled = false;
            }

            public virtual void AddOutgoingConnection(Node target)
            {

            }

            public virtual void RemoveOutgoingConnection(Node target)
            {

            }

            public IAudioNode GetNode()
            {
                return audioNode;
            }

            public virtual Button CreateUIButtom()
            {
                var btn = currentButton = new Button();
                btn.Content = name;
                btn.Width = BUTTON_WIDTH;
                btn.Height = BUTTON_HEIGHT;
                btn.Tag = this;
                btn.Click += MainPage.Current.TreeNode_Click;
                return btn;
            }

            protected void PropagateSortIndex(Node parent)
            {
            }

            public void Dispose()
            {
                if (audioNode != null)
                {
                    audioNode.Dispose();
                }
            }
        }

        public class InputNode : Node
        {
            public static int btnCounter = 0;

            public InputNode()
            {
                serial = ++btnCounter;
                type = NodeType.Input;
                name = "Input File " + serial;
            }

            public override void AddOutgoingConnection(Node target)
            {
                GetNode().AddOutgoingConnection(target.audioNode);

                outGoingNodes.Add(target);
                target.incomingNodes.Add(this);

                PropagateSortIndex(target);

            }

            public override void RemoveOutgoingConnection(Node target)
            {
                GetNode().RemoveOutgoingConnection(target.audioNode);
                outGoingNodes.Remove(target);
                target.incomingNodes.Remove(this);

            }



            public override Button CreateUIButtom()
            {
                var btn = base.CreateUIButtom();
                btn.Content = name;
                return btn;
            }

            public new AudioFileInputNode GetNode()
            {
                return ((AudioFileInputNode)this.audioNode);
            }
        }

        public class OutputNode : Node
        {

            public static int btnCounter = 0;

            public OutputNode()
            {
                serial = ++btnCounter;
                type = NodeType.Output;
                name = "Output Device";
            }

            public override void AddOutgoingConnection(Node target)
            {
                MainPage.Current.NotifyUser("Cannot add outgoing node to output device", NotifyType.ErrorMessage);
            }

            public new AudioDeviceOutputNode GetNode()
            {
                return ((AudioDeviceOutputNode)this.audioNode);
            }

            public override Button CreateUIButtom()
            {
                var btn = base.CreateUIButtom();
                btn.Content = name;
                return btn;
            }
        }

        public class MixerNode : Node
        {

            public static int btnCounter = 0;

            public MixerNode()
            {
                serial = ++btnCounter;
                type = NodeType.Mixer;
                name = "Mixer " + serial;
            }

            public override void AddOutgoingConnection(Node target)
            {
                GetNode().AddOutgoingConnection(target.audioNode);

                outGoingNodes.Add(target);
                target.incomingNodes.Add(this);

                PropagateSortIndex(target);

            }

            public override void RemoveOutgoingConnection(Node target)
            {
                GetNode().RemoveOutgoingConnection(target.audioNode);

                outGoingNodes.Remove(target);
                target.incomingNodes.Remove(this);

            }

            public new AudioSubmixNode GetNode()
            {
                return ((AudioSubmixNode)this.audioNode);
            }

            public override Button CreateUIButtom()
            {
                var btn = base.CreateUIButtom();
                btn.Content = name;
                btn.Background = new SolidColorBrush(Colors.DarkCyan);
                return btn;
            }
        }

        class MixerAnchor
        {
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
            await CreateAudioGraph(); ;
            if (graph != null)
            {
                rootNode = await CreateDeviceOutputNode();
                RefreshUI();
            }
        }

        // this is test only
        public async Task<bool> LoadInitFile()
        {
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

        public void Play()
        {
            if (!playing)
            {
                graph.Start();
                playing = true;
            }
        }

        public void Stop()
        {
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
            else
            {
                Play();
            }
        }

        public void AttachEffect(Node node)
        {
            CreateLimiterEffect(node);
            CreateEqEffect(node);
        }

        private void CreateLimiterEffect(Node node)
        {
            if (node.limiterEffectDefinition == null)
            {
                // Create limiter effect
                node.limiterEffectDefinition = new LimiterEffectDefinition(graph);

                node.limiterEffectDefinition.Loudness = 1000;
                node.limiterEffectDefinition.Release = 10;

                node.GetNode().EffectDefinitions.Add(node.limiterEffectDefinition);
                node.GetNode().DisableEffectsByDefinition(node.limiterEffectDefinition);
            }
        }



        private void CreateEqEffect(Node node)
        {
            if (node.eqEffectDefinition == null)
            {
                // See the MSDN page for parameter explanations
                // https://msdn.microsoft.com/en-us/library/windows/desktop/microsoft.directx_sdk.xapofx.fxeq_parameters(v=vs.85).aspx
                var eqEffectDefinition = node.eqEffectDefinition = new EqualizerEffectDefinition(graph);
                eqEffectDefinition.Bands[0].FrequencyCenter = 100.0f;
                eqEffectDefinition.Bands[0].Gain = 4.033f;
                eqEffectDefinition.Bands[0].Bandwidth = 1.5f;

                eqEffectDefinition.Bands[1].FrequencyCenter = 900.0f;
                eqEffectDefinition.Bands[1].Gain = 1.6888f;
                eqEffectDefinition.Bands[1].Bandwidth = 1.5f;

                eqEffectDefinition.Bands[2].FrequencyCenter = 5000.0f;
                eqEffectDefinition.Bands[2].Gain = 2.4702f;
                eqEffectDefinition.Bands[2].Bandwidth = 1.5f;

                eqEffectDefinition.Bands[3].FrequencyCenter = 12000.0f;
                eqEffectDefinition.Bands[3].Gain = 5.5958f;
                eqEffectDefinition.Bands[3].Bandwidth = 2.0f;

                node.GetNode().EffectDefinitions.Add(eqEffectDefinition);
                node.GetNode().DisableEffectsByDefinition(eqEffectDefinition);
            }
        }

        private async Task<OutputNode> CreateDeviceOutputNode()
        {
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

        private async Task<InputNode> CreateFileInputNode()
        {
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
            node.name = file.Name;

            rootPage.NotifyUser("Successfully loaded input file", NotifyType.StatusMessage);
            return node;

        }


        private MixerNode CreateMixerNode()
        {
            MixerNode node = new MixerNode();
            node.audioNode = graph.CreateSubmixNode();
            AttachEffect(node);

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


        public void RefreshUI()
        {
            if (rootNode != null)
            {
                displayCanvas.Children.Clear();
                DrawTree(rootNode, 0, 1);
            }
        }

        private void DrawTree(Node currentNode, double height, int depth)
        {
            // draw this node
            var currentBtn = currentNode.CreateUIButtom();
            var currentMargin = currentBtn.Margin;
            currentMargin.Left = (depth - 1) * 250;
            currentMargin.Top = height;
            currentBtn.Margin = currentMargin;

            displayCanvas.Children.Add(currentBtn);
            Canvas.SetZIndex(currentBtn, depth);
            // draw  its child
            double accuHeight = height;
            for (int i = 0; i < currentNode.incomingNodes.Count; i++)
            {
                var node = currentNode.incomingNodes[i];
                DrawTree(node, accuHeight, depth + 1);
                accuHeight = node.elementHeight;
                DrawEdge(node, currentNode);
            }
            if (currentNode.incomingNodes.Count == 0)
            {
                accuHeight += 150;
            }
            currentNode.elementHeight = accuHeight;
        }

        private void DrawEdge(Node src, Node dst)
        {

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
            src.line1 = data.line1 = line1;
            src.line2 = data.line2 = line2;
            data.src = src;
            data.dst = dst;
            src.anchor = anchor;




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
            var mixer = CreateMixerNode();
            var btn = mixer.CreateUIButtom();
            displayCanvas.Children.Add(btn);
            // positioning
            var margin = btn.Margin;
            margin.Left = anchorData.centerX - BUTTON_HEIGHT / 2;
            margin.Top = anchorData.centerY - BUTTON_HEIGHT / 2;

            btn.Margin = margin;
            // relinking
            // remove two old link
            displayCanvas.Children.Remove(anchorData.line1);
            displayCanvas.Children.Remove(anchorData.line2);
            anchorData.src.RemoveOutgoingConnection(anchorData.dst);
            // make link for two side
            // link mixer to dst
            mixer.AddOutgoingConnection(anchorData.dst);
            DrawEdge(mixer, anchorData.dst);
            // link src to mixer
            anchorData.src.AddOutgoingConnection(mixer);
            DrawEdge(anchorData.src, mixer);
            RefreshUI();

        }

        public void MixerAnchor_Click(object sender, RoutedEventArgs e)
        {
            Button btn = (Button)(sender);
            // hide anchor
            displayCanvas.Children.Remove(btn);
            // Create mixer
            CreateMixerFromClick((MixerAnchor)btn.Tag);
        }

        public async Task CreateIncomingForEditing()
        {
            var parent = editingNode;
            var incoming = await CreateFileInputNode();
            incoming.AddOutgoingConnection(parent);
            AttachEffect(incoming);
            RefreshUI();

        }

        public async Task CreateSiblingForEditing()
        {
            var parent = editingNode;
            if (parent.type == NodeType.Input)
            {
                await CreateSiblingInputFor((InputNode)parent);
            }
            else if (parent.type == NodeType.Mixer)
            {
                CreateSiblingMixerFor((MixerNode)parent);
            }
            RefreshUI();
        }

        private async Task CreateSiblingInputFor(InputNode node)
        {
            var incoming = await CreateFileInputNode();
            foreach (var parent in node.outGoingNodes)
            {
                incoming.AddOutgoingConnection(parent);
            }
            AttachEffect(incoming);
        }

        private void CreateSiblingMixerFor(MixerNode node)
        {
            var incoming = CreateMixerNode();
            foreach (var parent in node.outGoingNodes)
            {
                incoming.AddOutgoingConnection(parent);
            }
        }

        public void DeleteEditingNode()
        {
            if (editingNode != null)
            {
                DeleteNodeRecursively(editingNode);
                editingNode = null;
                RefreshUI();
            }
        }

        private void DeleteNodeRecursively(Node node)
        {
            // delete children first
            var deletingNodes = new List<Node>();
            deletingNodes.AddRange(node.incomingNodes);
            foreach (var childNode in deletingNodes)
            {
                DeleteNodeRecursively(childNode);
            }
            // remove UI
            displayCanvas.Children.Remove(node.line1);
            displayCanvas.Children.Remove(node.line2);
            displayCanvas.Children.Remove(node.anchor);
            displayCanvas.Children.Remove(node.currentButton);
            // break connection to parent
            deletingNodes.Clear();
            deletingNodes.AddRange(node.outGoingNodes);
            foreach (var parentNode in deletingNodes)
            {
                node.RemoveOutgoingConnection(parentNode);
            }
            // dispose node
            node.Dispose();
        }


        public bool IsPlaying()
        {
            return playing;
        }

        public void Dispose()
        {
            if(graph != null)
            {
                DeleteNodeRecursively(rootNode);
                graph.Dispose();
                graph = null;
            }
        }
    }

}
