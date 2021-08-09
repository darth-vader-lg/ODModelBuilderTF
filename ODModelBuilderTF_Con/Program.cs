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
#if DEBUG
         // Print to the console the output of TensorFlow
         Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));
         // Define the virtual environment path
         var virtualEnvPath = default(string);
         //var virtualEnvPath = @"..\..\..\..\ODModelBuilderTF_Py\env";
#else
         // Define the virtual environment path
         var virtualEnvPath = default(string);
#endif
         // Object detection initialization
         OD.Init(virtualEnvPath: virtualEnvPath);
         // Create the trainer
         var trainer = new Trainer(new Trainer.Options
         {
            BatchSize = 16,
            CheckpointEvery = null,
            CheckpointForceThreashold = null,
            EvalImagesFolder = @"Data\Images\Eval",
            ExportFolder = @"Data\Export",
            ModelType = ModelTypes.SSD_MobileNet_V2_320x320,
            NumTrainSteps = 50000,
            TensorBoardPort = 6006,
            TrainFolder = @"Data\Train",
            TrainImagesFolder = @"Data\Images\Train",
            TrainRecordsFolder = @"Data\Records",
         });
         // Step event
         var loss = default(double?);
         trainer.TrainStep += (sender, e) =>
         {
            Console.WriteLine($"Step number:{e.StepNumber}\t\tstep time: {e.StepTime:N3} secs\t\ttotal loss:{e.TotalLoss:N3}");
            if (loss == null)
               loss = e.TotalLoss;
            else if (e.TotalLoss < loss) {
               loss = e.TotalLoss;
               e.CreateCheckpoint = true;
               Console.WriteLine($"{new string('=', 40)}> Create checkpoint with total loss {e.TotalLoss}");
            }
         };
         // Evaluation event
         var mAP = default(double?);
         trainer.TrainEvaluation += (sender, e) =>
         {
            if (mAP == null)
               mAP = e.AP;
            else if (e.AP > mAP) {
               mAP = e.AP;
               e.Export = true;
            }
            Console.WriteLine($"{new string('=', 40)}> Mean average precision {e.AP}");
            if (e.Export)
               Console.WriteLine($"{new string('=', 40)}> Exporting model...");
         };
         // Export event
         trainer.ExportedSavedModel += (sender, e) =>
         {
            Console.WriteLine($"{new string('=', 40)}> SavedModel exported");
         };
         // Token for stop training
         var exitToken = new CancellationTokenSource();
         Console.CancelKeyPress += (sender, e) => exitToken.Cancel();
         // Wait the training
         await Task.Run(() => trainer.Train(exitToken.Token));
      }
   }
}
