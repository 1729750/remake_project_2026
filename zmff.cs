using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;

namespace Assembly_CSharp
{
    [RunInstaller(true)]
    public partial class zmff : System.Configuration.Install.Installer
    {
        public zmff()
        {
            InitializeComponent();
        }
    }
}
