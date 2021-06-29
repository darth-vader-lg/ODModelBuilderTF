using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using OD = ODModelBuilderTF.ODModelBuilderTF;
namespace ODModelBuilderTF_Con
{
    class Program
    {
        static void Main(string[] args)
        {
            Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));
            var assemblyName = Assembly.GetEntryAssembly().GetName();
            OD.Init(true, true, Path.Combine(Path.GetTempPath(), $"{assemblyName.Name}-{assemblyName.Version}"));
            //var script = OD.GetPythonScript("install_virtual_environment.py");
            //OD.Exec(script);
        }
    }
}
