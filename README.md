
# ODModelBuilderTF
Object detection models builder with TensorFlow.

# .NET (C#) object detection models builder with TensorFlow.

ODModelBuilderTF is a .NET library which allow to train object detection models directly in .NET environment, without the needing of Python code.<BR>
* It's based on the TensorFlow framework.<BR>
* It can be used with all .NET languages simply including the package on your project.<BR>
* It's also event oriented, so it's possible to control the whole train process registering the events in the client application.<BR>
* Tuning parameters are provided to manage the train process directly from the client without manually writing configuration files.<BR>
* Can train and export TensorFlow's saved model format, frozen graph and ONNX models that can be afterwards consumed, for example, in the [ML.NET](https://dotnet.microsoft.com/apps/machinelearning-ai/ml-dotnet) framework.

## Getting started with ODModelBuilderTF

Simply include the package (or the reference to the project if you include it in your solution) to used the library.<BR>
At the first initialization it will install the train environment on your device downloading it from Internet (the process can take long time).<BR>
If you prefer to have a ready to use environment (or your device is offline) you can include in your application the redistributables containing all the needed resources.

## Sample apps

For a quick usage example you can take a look to the console application included in this repository, used to test the library.

## Packages
[LG.ODModelBuilderTF](https://www.nuget.org/packages/LG.ODModelBuilderTF): the train library.<BR>
[LG.ODModelBuilderTF-Redist-Win](https://www.nuget.org/packages/LG.ODModelBuilderTF-Redist-Win): the redistributable with TensorFlow object detection packages.<BR>
[LG.ODModelBuilderTF-Redist-Win-TF](https://www.nuget.org/packages/LG.ODModelBuilderTF-Redist-Win-TF): the redistributable with TensorFlow packages.
* [LG.ODModelBuilderTF-Redist-Win-TF-A](https://www.nuget.org/packages/LG.ODModelBuilderTF-Redist-Win-TF-A): the redistributable with TensorFlow library (part A).<BR>
* [LG.ODModelBuilderTF-Redist-Win-TF-B](https://www.nuget.org/packages/LG.ODModelBuilderTF-Redist-Win-TF-B): the redistributable with TensorFlow library (part B).<BR>

[LG.ODModelBuilderTF-Redist-Win-CUDA10_1-TF](https://www.nuget.org/packages/LG.ODModelBuilderTF-Redist-Win-CUDA10_1-TF): the redistributable with a special TensorFlow library for old GPUs which can work with CUDA 10.1 SM30. To use instead of the ODModelBuilderTF-Redist-Win-TF (CUDA 11) in old devices.<BR>

### Simple installation with environment download at the first run:
[LG.ODModelBuilderTF](https://www.nuget.org/packages/LG.ODModelBuilderTF)
### Installation with full environment:
[LG.ODModelBuilderTF](https://www.nuget.org/packages/LG.ODModelBuilderTF),
[LG.ODModelBuilderTF-Redist-Win](https://www.nuget.org/packages/LG.ODModelBuilderTF-Redist-Win),
[LG.ODModelBuilderTF-Redist-Win-TF](https://www.nuget.org/packages/LG.ODModelBuilderTF-Redist-Win-TF)
### Installation on old GPUs:
[LG.ODModelBuilderTF](https://www.nuget.org/packages/LG.ODModelBuilderTF),
[LG.ODModelBuilderTF-Redist-Win](https://www.nuget.org/packages/LG.ODModelBuilderTF-Redist-Win),
[LG.ODModelBuilderTF-Redist-Win-CUDA10_1-TF](https://www.nuget.org/packages/LG.ODModelBuilderTF-Redist-Win-CUDA10_1-TF)

## Building ODModelBuilderTF

It's possible to build ODModelBuilderTF and the packages directly from the command line launching the *build.cmd* or opening the solution in Visual Studio 2019 or above.

## Code examples

Here is a snippet code to show how to train a model.

```C#
using ODModelBuilderTF;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ODModelBuilderTF_Con
{
   class Program
   {
      static async Task Main(string[] args)
      {
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
            OnnxModelFileName = "Model.onnx",
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
         // Export events
         trainer.ExportedSavedModel += (sender, e) =>
         {
            Console.WriteLine($"{new string('=', 40)}> SavedModel exported");
         };
         trainer.ExportedOnnx += (sender, e) =>
         {
            Console.WriteLine($"{new string('=', 40)}> Onnx model exported");
         };
         // Token for stop training
         var exitToken = new CancellationTokenSource();
         Console.CancelKeyPress += (sender, e) => exitToken.Cancel();
         // Wait the training
         await Task.Run(() => trainer.Train(exitToken.Token));
      }
   }
}
```

## Colab notebook
It's possible to test / train directly on a google colab environment using the notebook [ODModelBuilderTF.ipynb](https://colab.research.google.com/github/darth-vader-lg/ODModelBuilderTF/blob/master/ODModelBuilderTF_Py/ODModelBuilderTF.ipynb)

## License

ODModelBuilderTF is licensed under the [MIT license](LICENSE) and it is free to use commercially.
