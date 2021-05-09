using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Traceman.Explorer
{
    public class StackView : TreeView
    {
        public StackView()
        {
            string output = Environment.ExpandEnvironmentVariables(@"%tmp%\\traceman_output.bin");

        }


    }
}
