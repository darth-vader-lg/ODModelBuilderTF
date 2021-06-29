using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using OD = ODModelBuilderTF.ODModelBuilderTF;

namespace ODModelBuilderTF_Con
{
   class Program
   {
      static async Task Main(string[] args)
      {
         Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));
         var assemblyName = Assembly.GetEntryAssembly().GetName();
         OD.Init(true, true, Path.Combine(Path.GetTempPath(), $"{assemblyName.Name}-{assemblyName.Version}"));
         var task1 = Task.Run(() => OD.TestPythonThread("Task 1"));
         var task2 = Task.Delay(5000).ContinueWith(t => OD.TestPythonThread("Task 2"));
         await Task.WhenAll(task1, task2);
      }
   }
}
