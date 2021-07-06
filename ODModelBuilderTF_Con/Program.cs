using ODModelBuilderTF;
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
         var modelType = ModelTypes.SSD_MobileNet_V2_320x320;
         var modelDir = @"D:\ObjectDetection\caz\TensorFlow\trained-model";
         var trainImagesDir = @"D:\ObjectDetection\caz\TensorFlow\images\train";
         var evalImagesDir = @"D:\ObjectDetection\caz\TensorFlow\images\eval";
         var taskTrain = Task.Run(() => OD.Train(modelType, modelDir, trainImagesDir, evalImagesDir, 4, 50000));
         await taskTrain;
      }
   }
}
