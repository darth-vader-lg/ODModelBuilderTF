﻿using Python.Runtime;
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
      /// Evaluation ready event
      /// </summary>
      public event EvaluationEventHandler Evaluation;
      /// <summary>
      /// Evaluation timeout ready event
      /// </summary>
      public event EvaluationTimeoutEventHandler EvaluationTimeout;
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
      /// Evaluation task
      /// </summary>
      /// <param name="checkPointReady">New check point ready signal</param>
      /// <param name="cancel">Cancellation token</param>
      /// <returns></returns>
      private Task EvaluationTask(EventWaitHandle checkPointReady, CancellationToken cancel)
      {
         return Task.Run(() =>
         {
            // Evaluation options
            var evaluator = new Evaluator(new Evaluator.Options
            {
               TimeoutInterval = 0,
               TrainFolder = Opt.TrainFolder,
               WaitInterval = 0
            });
            // Evaluation done event
            evaluator.Evaluation += (sender, e) =>
            {
               try {
                  // Check if cancellation requested
                  cancel.ThrowIfCancellationRequested();
                  // Print metrics
                  Trace.WriteLine($"Evaluation done."); //@@@
                  Trace.WriteLine(new string('=', 80));
                  foreach (var m in e.Metrics)
                     Trace.WriteLine($"{m.Key}\t\t\t{m.Value}");
                  Trace.WriteLine(new string('=', 80));
                  // Wait for the next checkpoint
                  while (!checkPointReady.WaitOne(500))
                     cancel.ThrowIfCancellationRequested();
                  cancel.ThrowIfCancellationRequested();
                  // Call the evaluation ready function
                  OnEvaluation(e);
               }
               catch (Exception) {
                  e.Cancel = true;
               }
            };
            // Evaluation timeout event
            evaluator.EvaluationTimeout += (sender, e) =>
            {
               try {
                  cancel.ThrowIfCancellationRequested();
                  OnEvaluationTimeout(e);
               }
               catch (Exception) {
                  e.Cancel = true;
               }
            };
            // Start the evaluation
            evaluator.Evaluate(cancel);
         }, cancel);
      }
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
      /// Evaluation ready function
      /// </summary>
      /// <param name="e">Evaluation arguments</param>
      protected void OnEvaluation(EvaluationEventArgs e)
      {
         try {
            Evaluation?.Invoke(this, e);
         }
         catch (Exception exc) {
            Trace.WriteLine(exc);
         }
      }
      /// <summary>
      /// Evaluation timeout function
      /// </summary>
      /// <param name="e">Evaluation timeout arguments</param>
      protected void OnEvaluationTimeout(EvaluationTimeoutEventArgs e)
      {
         try {
            EvaluationTimeout?.Invoke(this, e);
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
         // Cancellation token
         var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancel);
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
                     if (data.Cancel)
                        cancellation.Cancel();
                     // Set the response flags
                     using (Py.GIL()) {
                        args.cancel = cancellation.IsCancellationRequested;
                        args.create_checkpoint = data.CreateCheckpoint;
                     }
                  });
                  // Online evaluation task
                  Task evalTask = null;
                  // Checkpoint ready signal
                  var checkPointReady = new AutoResetEvent(false);
                  // Checkpoint ready callback function
                  var checkpointCallback = new Action<dynamic>(args =>
                  {
                     // Read the data
                     CheckpointEventArgs data;
                     using (Py.GIL())
                        data = new CheckpointEventArgs((string)args.latest_checkpoint, (string[])args.checkpoints);
                     // Call the event function
                     OnCheckpoint(data);
                     if (data.Cancel)
                        cancellation.Cancel();
                     // Set the response flags
                     using (Py.GIL())
                        args.cancel = cancellation.IsCancellationRequested;
                     // Check if no cancellation was required
                     if (!cancellation.IsCancellationRequested) {
                        try {
                           // Create the continuous evaluation task
                           if (evalTask == null)
                              evalTask = EvaluationTask(checkPointReady, cancellation.Token);
                           // Or just signal to start a new evaluation if the task was already active
                           else
                              checkPointReady.Set();
                        }
                        catch (OperationCanceledException) { }
                     }
                  });
                  // Start the train loop
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
   /// Trainer options
   /// </summary>
   public partial class Trainer
   {
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
