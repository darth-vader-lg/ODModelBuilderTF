using ODModelBuilderTF;
using System;
using System.Diagnostics;
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
#if DEBUG
         var virtualEnvPath = @"..\..\..\..\ODModelBuilderTF_Py\env";
#else
         var virtualEnvPath = default(string);
#endif
         OD.Init(virtualEnvPath: virtualEnvPath);
         var trainer = new Trainer(new Trainer.Options
         {
            BatchSize = 20,
            CheckpointEvery = int.MaxValue,
            CheckpointForceThreashold = 0.95,
            EvalImagesFolder = @"D:\ObjectDetection\caz\TensorFlow\images\eval",
            ExportFolder = @"D:\ObjectDetection\caz\TensorFlow\exported-model",
            ModelType = ModelTypes.SSD_MobileNet_V2_320x320,
            NumTrainSteps = 50000,
            OnnxModelFileName = null, //"Model.onnx",
            TensorBoardPort = 8080,
            TrainFolder = @"D:\ObjectDetection\caz\TensorFlow\trained-model",
            TrainImagesFolder = @"D:\ObjectDetection\caz\TensorFlow\images\train",
            TrainRecordsFolder = @"D:\ObjectDetection\caz\TensorFlow\annotations"
         });
         trainer.TrainStep += (sender, e) =>
         {
            Console.WriteLine($"Step number:{e.StepNumber}\t\tstep time: {e.StepTime:N3} secs\t\ttotal loss:{e.TotalLoss:N3}");
         };
         var exitToken = new CancellationTokenSource();
         Console.CancelKeyPress += (sender, e) => exitToken.Cancel();
         await Task.Run(() => trainer.Train(exitToken.Token));
      }
   }
}
