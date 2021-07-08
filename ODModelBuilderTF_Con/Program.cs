using ODModelBuilderTF;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
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
#if DEBUG
         var virtualEnvDir = @"..\..\..\..\ODModelBuilderTF_Py\env";
#else
         var virtualEnvDir = Path.Combine(Path.GetTempPath(), $"{assemblyName.Name}-{assemblyName.Version}");
#endif
         OD.Init(true, true, virtualEnvDir);
         var modelType = ModelTypes.SSD_MobileNet_V2_320x320;
         var modelDir = @"D:\ObjectDetection\caz\TensorFlow\trained-model";
         var trainImagesDir = @"D:\ObjectDetection\caz\TensorFlow\images\train";
         var evalImagesDir = @"D:\ObjectDetection\caz\TensorFlow\images\eval";
         var exitToken = new CancellationTokenSource();
         Console.CancelKeyPress += (sender, e) => exitToken.Cancel();
         var taskTrain = Task.Run(() => OD.Train(modelType, modelDir, trainImagesDir, evalImagesDir, 4, 50000));
         await Task.Delay(30000);
         var taskEval = Task.CompletedTask;//@@@ Task.Run(() => OD.Evaluate(modelDir));
         var taskExit = Task.Delay(-1, exitToken.Token).ContinueWith(t => { });
         await Task.WhenAny(Task.WhenAll(taskTrain, taskEval), taskExit);
      }
   }
}
