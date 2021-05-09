using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Microsoft.Diagnostics.Symbols;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Stacks;

namespace Traceman.Explorer.Views
{
    public class TreeViewPage : UserControl
    {
        public TreeViewPage()
        {
            this.InitializeComponent();

            string output = Environment.ExpandEnvironmentVariables(@"%tmp%\\traceman_output.bin");

            Node root = new Node();

            string tempEtlxFilename = TraceLog.CreateFromEventPipeDataFile(output);
            using (var symbolReader = new SymbolReader(System.IO.TextWriter.Null) { SymbolPath = SymbolPath.MicrosoftSymbolServerPath })
            using (var eventLog = new TraceLog(tempEtlxFilename))
            {
                var stackSource = new MutableTraceEventStackSource(eventLog)
                {
                    OnlyManagedCodeStacks = true
                };

                var computer = new SampleProfilerThreadTimeComputer(eventLog, symbolReader);
                computer.GenerateThreadTimeStacks(stackSource);

                Dictionary<StackSourceCallStackIndex, Node> stackIndexToNode = new Dictionary<StackSourceCallStackIndex, Node>();
                Dictionary<int, Node> threadToFirstNode = new Dictionary<int, Node>();

                double totalOccurrences = 0;

                stackSource.ForEach(sample =>
                {
                    Node node = null;

                    var stackIndex = sample.StackIndex;
                    while (!stackSource.GetFrameName(stackSource.GetFrameIndex(stackIndex), false).StartsWith("Thread ("))
                    {
                        if (stackIndexToNode.TryGetValue(stackIndex, out node))
                        {
                            node.Occurrences++;
                            totalOccurrences++;
                        }
                        else
                        {
                            stackIndexToNode.Add(stackIndex, node = new Node());
                            node.Header = stackSource.GetFrameName(stackSource.GetFrameIndex(stackIndex), false);
                        }

                        stackIndex = stackSource.GetCallerIndex(stackIndex);
                    }

                    const string template = "Thread (";
                    string threadFrame = stackSource.GetFrameName(stackSource.GetFrameIndex(stackIndex), false);
                    int threadId = int.Parse(threadFrame.Substring(template.Length, threadFrame.Length - (template.Length + 1)));

                    threadToFirstNode[threadId] = node;
                });

                totalOccurrences = totalOccurrences / 48d;

                foreach (var pair in stackIndexToNode)
                {
                    pair.Value.Header = Math.Round(100d * pair.Value.Occurrences / totalOccurrences, 3) + "% / " + pair.Value.Header;

                    if (stackIndexToNode.TryGetValue(stackSource.GetCallerIndex(pair.Key), out Node parent))
                        pair.Value.Parent = parent;
                }

                foreach (var pair in stackIndexToNode)
                {
                    pair.Value.Children = pair.Value.Children.OrderByDescending(x => x.Occurrences).ToList();
                }

                root.Children = threadToFirstNode.Values.OrderByDescending(x => x.Occurrences).ToList();
            }

            DataContext = root.Children;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public class Node
        {
            public string Header { get; set; }

            public int Occurrences { get; set; }

            public List<Node> Children { get; set; } = new List<Node>();

            public Node Parent
            {
                set
                {
                    value.Children.Add(this);
                }
            }
        }
    }
}
