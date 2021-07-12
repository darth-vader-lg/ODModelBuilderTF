using Python.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace ODModelBuilderTF
{
   /// <summary>
   /// Trainer class
   /// </summary>
   public partial class Trainer
   {
      #region Properties
      /// <summary>
      /// Train options
      /// </summary>
      public Options Opt { get; }
      #endregion
      #region Events
      /// <summary>
      /// Train step event
      /// </summary>
      public event TrainStepEventHandler TrainStep;
      /// <summary>
      /// Checkpoint event
      /// </summary>
      public event CheckpointEventHandler Checkpoint;
      #endregion
      #region Methods
      /// <summary>
      /// Constructor
      /// </summary>
      public Trainer() : this(default) { }
      /// <summary>
      /// Constructor
      /// </summary>
      /// <param name="opt">Trainer options</param>
      public Trainer(Options opt = default) => Opt = opt ?? new Options();
      /// <summary>
      /// Checkpoint function
      /// </summary>
      /// <param name="e">Checkpoint arguments</param>
      protected void OnCheckpoint(CheckpointEventArgs e)
      {
         try {
            Checkpoint?.Invoke(this, e);
         }
         catch (Exception exc) {
            Trace.WriteLine(exc);
         }
      }
      /// <summary>
      /// Train step function
      /// </summary>
      /// <param name="e">Train step arguments</param>
      protected void OnTrainStep(TrainStepEventArgs e)
      {
         try {
            TrainStep?.Invoke(this, e);
         }
         catch (Exception exc) {
            Trace.WriteLine(exc);
         }
      }
      /// <summary>
      /// Train loop
      /// </summary>
      /// <param name="cancel">Cancellation token</param>
      public void Train(CancellationToken cancel)
      {
         // Initialize system
         ODModelBuilderTF.Init(true, true);
         // Check arguments
         if (string.IsNullOrWhiteSpace(Opt.TrainImagesFolder))
            throw new ArgumentNullException(nameof(Opt.TrainImagesFolder), "Unspecified train images directory");
         if (!Directory.Exists(Opt.TrainImagesFolder))
            throw new ArgumentNullException(nameof(Opt.TrainImagesFolder), "The train images directory doesn't exist");
         if (string.IsNullOrWhiteSpace(Opt.EvalImagesFolder))
            throw new ArgumentNullException(nameof(Opt.EvalImagesFolder), "Unspecified evaluation images directory");
         if (!Directory.Exists(Opt.EvalImagesFolder))
            throw new ArgumentNullException(nameof(Opt.EvalImagesFolder), "The evaluation directory doesn't exist");
         if (string.IsNullOrWhiteSpace(Opt.TrainFolder))
            throw new ArgumentNullException(nameof(Opt.TrainFolder), "Unspecified model train directory");
         // Directory for the tf records
         var delAnnotationDir = string.IsNullOrWhiteSpace(Opt.TrainRecordsFolder);
         var annotationsDir = !string.IsNullOrWhiteSpace(Opt.TrainRecordsFolder) ? Opt.TrainRecordsFolder : Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
         try {
            // Acquire the GIL
            using var gil = Py.GIL();
            // Create a new scope
            var pyTrain = ODModelBuilderTF.MainScope.NewScope();
            // Prepare the arguments
            dynamic sys = pyTrain.Import("sys");
            var argv = new List<PyObject>
            {
               Assembly.GetEntryAssembly().Location.ToPython(),
               "--model_type".ToPython(), Opt.ModelType.ToText().ToPython(),
               "--train_images_dir".ToPython(), Opt.TrainImagesFolder.ToPython(),
               "--eval_images_dir".ToPython(), Opt.EvalImagesFolder.ToPython(),
               "--model_dir".ToPython(), Opt.TrainFolder.ToPython(),
               "--annotations_dir".ToPython(), annotationsDir.ToPython(),
               "--checkpoint_every_n".ToPython(), Opt.CheckPointEvery.ToString().ToPython()
            };
            if (Opt.BatchSize != null)
               argv.AddRange(new[] { "--batch_size".ToPython(), Opt.BatchSize.Value.ToString().ToPython() });
            if (Opt.NumTrainSteps != null)
               argv.AddRange(new[] { "--num_train_steps".ToPython(), Opt.NumTrainSteps.Value.ToString().ToPython() });
            if (Opt.TensorBoardPort != null)
               argv.AddRange(new[] { "--tensorboard_port".ToPython(), Opt.TensorBoardPort.Value.ToString().ToPython() });
            sys.argv = new PyList(argv.ToArray());
            // Import the main of the training
            dynamic train_main = pyTrain.Import("train_main");
            // Import the module here just for having the flags defined
            train_main.allow_flags_override();
            pyTrain.Import("object_detection.model_main_tf2");
            // Import the TensorFlow
            dynamic tf = pyTrain.Import("tensorflow");
            try {
               // Create annotation dir and start the train
               if (!Directory.Exists(annotationsDir))
                  Directory.CreateDirectory(annotationsDir);
               // Train action
               var train = new Action<dynamic>(unused_argv =>
               {
                  // Step callback action
                  var stepCallback = new Action<dynamic>(args =>
                  {
                     // Read the data
                     TrainStepEventArgs data;
                     using (Py.GIL())
                        data = new TrainStepEventArgs((int)args.global_step, (double)args.per_step_time, (double)args.loss);
                     // Call the event function
                     OnTrainStep(data);
                     // Set the response flags
                     using (Py.GIL()) {
                        args.cancel = data.Cancel || cancel.IsCancellationRequested;
                        args.create_checkpoint = data.CreateCheckpoint;
                     }
                  });
                  //@@@Task evalTask = null;
                  var checkpointCallback = new Action<dynamic>(args =>
                  {
                     // Read the data
                     CheckpointEventArgs data;
                     using (Py.GIL())
                        data = new CheckpointEventArgs((string)args.latest_checkpoint, (string[])args.checkpoints);
                     // Call the event function
                     OnCheckpoint(data);
                     // Set the response flags
                     using (Py.GIL())
                        args.cancel = data.Cancel || cancel.IsCancellationRequested;
                     //try { @@@
                     //   evalTask ??= Task.Run(() =>
                     //   {
                     //      try {
                     //         EvaluateInternal(modelDir, waitIntervalSecs: 1, timeoutSecs: 5, cancel: cancel);
                     //      }
                     //      catch (Exception exc) {
                     //         Trace.WriteLine(exc);
                     //      }
                     //   }, cancel);
                     //}
                     //catch (OperationCanceledException) { }
                  });
                  using (Py.GIL())
                     train_main.train_main(unused_argv, step_callback: stepCallback, checkpoint_callback: checkpointCallback);
               });
               tf.compat.v1.app.run(train);
            }
            catch (PythonException exc) {
               // Response to the exceptions
               var action = exc.PyType switch
               {
                  var pexc when pexc == Exceptions.SystemExit => new Action(() => { }),
                  var pexc when pexc == Exceptions.KeyboardInterrupt => new Action(() =>
                  {
                     Trace.WriteLine("Interrupted by user");
                  }),
                  _ => new Action(() => { throw exc; })
               };
               action();
            }
         }
         catch (Exception exc) {
            Trace.WriteLine(exc.ToString().Replace("\\n", Environment.NewLine));
            throw;
         }
         finally {
            // Delete the annotations directory
            try {
               if (delAnnotationDir && Directory.Exists(annotationsDir))
                  Directory.Delete(annotationsDir, true);
            }
            catch (Exception exc) {
               Trace.WriteLine(exc);
            }
         }
      }
      #endregion
   }

   /// <summary>
   /// Trainer class
   /// </summary>
   public partial class Trainer
   {
      /// <summary>
      /// Trainer options
      /// </summary>
      public class Options
      {
         #region Properties
         /// <summary>
         /// Batch size of the train
         /// It will be used the one defined in the pipeline config if not specified here.
         /// </summary>
         public int? BatchSize { get; set; } = null;
         /// <summary>
         /// Number of steps between checkpoint generations
         /// </summary>
         public int CheckPointEvery { get; set; } = 1000;
         /// <summary>
         /// Folder containing the images and annotation labels for the evaluation
         /// </summary>
         public string EvalImagesFolder { get; set; } = null;
         /// <summary>
         /// Base model type
         /// </summary>
         public ModelTypes ModelType { get; set; } = ModelTypes.SSD_MobileNet_V2_320x320;
         /// <summary>
         /// Number of train steps.
         /// It will be used the one defined in the pipeline config if not specified here.
         /// </summary>
         public int? NumTrainSteps { get; set; } = null;
         /// <summary>
         /// The port for the tensorboard server
         /// </summary>
         public int? TensorBoardPort { get; set; } = null;
         /// <summary>
         /// Folder in witch the train will be performed
         /// </summary>
         public string TrainFolder { get; set; } = null;
         /// <summary>
         /// Folder containing the train images and annotation labels
         /// </summary>
         public string TrainImagesFolder { get; set; } = null;
         /// <summary>
         /// Folder containing the records for training.
         /// A temporary directory will be created if not specified here.
         /// </summary>
         public string TrainRecordsFolder { get; set; } = null;
         #endregion
      }
   }
}
