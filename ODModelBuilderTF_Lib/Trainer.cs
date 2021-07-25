using Python.Runtime;
using System;
using System.Collections.Generic;
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
      /// Checkpoint event
      /// </summary>
      public event CheckpointEventHandler Checkpoint;
      /// <summary>
      /// Evaluation ready event
      /// </summary>
      public event EvaluationEventHandler Evaluation;
      /// <summary>
      /// Exported Frozen graph event
      /// </summary>
      public event ExportEventHandler ExportedFrozenGraph;
      /// <summary>
      /// Exported Frozen graph configuration event
      /// </summary>
      public event ExportEventHandler ExportedFrozenGraphConfig;
      /// <summary>
      /// Exported Onnx event
      /// </summary>
      public event ExportEventHandler ExportedOnnx;
      /// <summary>
      /// Exported Onnx configuration event
      /// </summary>
      public event ExportEventHandler ExportedOnnxConfig;
      /// <summary>
      /// Exported Saved model event
      /// </summary>
      public event ExportEventHandler ExportedSavedModel;
      /// <summary>
      /// Exported Saved model configuration event
      /// </summary>
      public event ExportEventHandler ExportedSavedModelConfig;
      /// <summary>
      /// Train evaluation ready event
      /// </summary>
      public event TrainEvaluationEventHandler TrainEvaluation;
      /// <summary>
      /// Train step event
      /// </summary>
      public event TrainStepEventHandler TrainStep;
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
      /// Continuous evaluation
      /// </summary>
      /// <param name="checkPointReady">New check point ready signal</param>
      /// <param name="evaluationComplete">Evaluation complete signal</param>
      /// <param name="cancellation">Cancellation token</param>
      private void EvaluateContinuously(EventWaitHandle checkPointReady, EventWaitHandle evaluationComplete, CancellationToken cancellation)
      {
         var c = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
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
               c.Token.ThrowIfCancellationRequested();
               // Call the evaluation ready function
               OnEvaluation(e);
               if (e.Cancel)
                  c.Cancel();
               c.Token.ThrowIfCancellationRequested();
               // Call the train evaluation ready function
               var trainEvaluation = new TrainEvaluationEventArgs(e.Metrics);
               OnTrainEvaluation(trainEvaluation);
               e.Cancel |= trainEvaluation.Cancel;
               if (e.Cancel)
                  c.Cancel();
               c.Token.ThrowIfCancellationRequested();
               // Check if the client required an export of the checkpoint
               if (trainEvaluation.Export)
                  ExportLatestCheckpoint(c.Token);
               c.Token.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException) {
               e.Cancel = true;
            }
         };
         // Evaluation timeout event
         evaluator.EvaluationTimeout += (sender, e) =>
         {
            try {
               // Wait for the next checkpoint
               c.Token.ThrowIfCancellationRequested();
               evaluationComplete.Set();
               WaitHandle.WaitAny(new[] { checkPointReady, c.Token.WaitHandle });
               // Tell to continue if no cancellation
               c.Token.ThrowIfCancellationRequested();
               e.Cancel = false;
            }
            catch (OperationCanceledException) {
               e.Cancel = true;
            }
         };
         // Start the evaluation
         evaluator.Evaluate(c.Token);
      }
      /// <summary>
      /// Export the latest checkpoint
      /// </summary>
      /// <param name="cancel">Cancellation token</param>
      private void ExportLatestCheckpoint(CancellationToken cancel)
      {
         // Check for arguments
         if (string.IsNullOrWhiteSpace(Opt.ExportFolder))
            throw new ArgumentException("Undefined export folder");
         var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancel);
         // Evaluation options
         var exporter = new Exporter(new Exporter.Options
         {
            FrozenGraphFileName = Opt.FrozenGraphFileName,
            OnnxModelFileName = Opt.OnnxModelFileName,
            OutputFolder = Opt.ExportFolder,
            TrainFolder = Opt.TrainFolder
         });
         // On exported caller
         void OnExported(ExportEventArgs e, Action<ExportEventArgs> OnExportedFunction)
         {
            try {
               // Check if cancellation requested
               cancellation.Token.ThrowIfCancellationRequested();
               // Call the exported saved_model function
               OnExportedFunction(e);
               if (e.Cancel)
                  cancellation.Cancel();
               cancellation.Token.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException) {
               e.Cancel = true;
            }
         }
         // Link events
         exporter.ExportedSavedModel += (sender, e) => OnExported(e, e => OnExportedSavedModel(e));
         exporter.ExportedFrozenGraph += (sender, e) => OnExported(e, e => OnExportedFrozenGraph(e));
         exporter.ExportedOnnx += (sender, e) => OnExported(e, e => OnExportedOnnx(e));
         exporter.ExportedSavedModelConfig += (sender, e) => OnExported(e, e => OnExportedSavedModelConfig(e));
         exporter.ExportedFrozenGraphConfig += (sender, e) => OnExported(e, e => OnExportedFrozenGraphConfig(e));
         exporter.ExportedOnnxConfig += (sender, e) => OnExported(e, e => OnExportedOnnxConfig(e));
         // Start the export
         exporter.Export(cancel);
      }
      /// <summary>
      /// Checkpoint function
      /// </summary>
      /// <param name="e">Checkpoint arguments</param>
      protected virtual void OnCheckpoint(CheckpointEventArgs e)
      {
         try {
            Checkpoint?.Invoke(this, e);
         }
         catch (Exception exc) {
            ODModelBuilderTF.TraceError(exc.ToString());
         }
      }
      /// <summary>
      /// Evaluation ready function
      /// </summary>
      /// <param name="e">Evaluation arguments</param>
      protected virtual void OnEvaluation(EvaluationEventArgs e)
      {
         try {
            Evaluation?.Invoke(this, e);
         }
         catch (Exception exc) {
            ODModelBuilderTF.TraceError(exc.ToString());
         }
      }
      /// <summary>
      /// Frozen graph exported function
      /// </summary>
      /// <param name="e">Export arguments</param>
      protected virtual void OnExportedFrozenGraph(ExportEventArgs data)
      {
         try {
            ExportedFrozenGraph?.Invoke(this, data);
         }
         catch (Exception exc) {
            ODModelBuilderTF.TraceError(exc.ToString());
         }
      }
      /// <summary>
      /// Frozen graph configuration exported function
      /// </summary>
      /// <param name="e">Export arguments</param>
      protected virtual void OnExportedFrozenGraphConfig(ExportEventArgs data)
      {
         try {
            ExportedFrozenGraphConfig?.Invoke(this, data);
         }
         catch (Exception exc) {
            ODModelBuilderTF.TraceError(exc.ToString());
         }
      }
      /// <summary>
      /// Onnx exported function
      /// </summary>
      /// <param name="e">Export arguments</param>
      protected virtual void OnExportedOnnx(ExportEventArgs data)
      {
         try {
            ExportedOnnx?.Invoke(this, data);
         }
         catch (Exception exc) {
            ODModelBuilderTF.TraceError(exc.ToString());
         }
      }
      /// <summary>
      /// Onnx configuration exported function
      /// </summary>
      /// <param name="e">Export arguments</param>
      protected virtual void OnExportedOnnxConfig(ExportEventArgs data)
      {
         try {
            ExportedOnnxConfig?.Invoke(this, data);
         }
         catch (Exception exc) {
            ODModelBuilderTF.TraceError(exc.ToString());
         }
      }
      /// <summary>
      /// SavedModel exported function
      /// </summary>
      /// <param name="e">Export arguments</param>
      protected virtual void OnExportedSavedModel(ExportEventArgs data)
      {
         try {
            ExportedSavedModel?.Invoke(this, data);
         }
         catch (Exception exc) {
            ODModelBuilderTF.TraceError(exc.ToString());
         }
      }
      /// <summary>
      /// SavedModel configuration exported function
      /// </summary>
      /// <param name="e">Export arguments</param>
      protected virtual void OnExportedSavedModelConfig(ExportEventArgs data)
      {
         try {
            ExportedSavedModelConfig?.Invoke(this, data);
         }
         catch (Exception exc) {
            ODModelBuilderTF.TraceError(exc.ToString());
         }
      }
      /// <summary>
      /// Train evaluation ready function
      /// </summary>
      /// <param name="e">Train evaluation arguments</param>
      protected virtual void OnTrainEvaluation(TrainEvaluationEventArgs e)
      {
         try {
            TrainEvaluation?.Invoke(this, e);
         }
         catch (Exception exc) {
            ODModelBuilderTF.TraceError(exc.ToString());
         }
      }
      /// <summary>
      /// Train step function
      /// </summary>
      /// <param name="e">Train step arguments</param>
      protected virtual void OnTrainStep(TrainStepEventArgs e)
      {
         try {
            TrainStep?.Invoke(this, e);
         }
         catch (Exception exc) {
            ODModelBuilderTF.TraceError(exc.ToString());
         }
      }
      /// <summary>
      /// Train loop
      /// </summary>
      /// <param name="cancel">Cancellation token</param>
      public void Train(CancellationToken cancel)
      {
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
         // Online evaluation task
         Task evalTask = Task.CompletedTask;
         try {
            // Initialize system
            ODModelBuilderTF.Init(true, true);
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
               "--checkpoint_dir".ToPython(), "".ToPython()
            };
            if (Opt.PreTrainedModelDir != null)
               argv.AddRange(new[] { "--pre_trained_model_dir".ToPython(), Opt.PreTrainedModelDir.ToPython() });
            if (Opt.CheckpointEvery != null)
               argv.AddRange(new[] { "--checkpoint_every_n".ToPython(), Opt.CheckpointEvery.Value.ToString().ToPython() });
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
               // Checkpoint ready signal
               var checkPointReady = new AutoResetEvent(false);
               var evaluationComplete = new AutoResetEvent(false);
               // Create annotation dir and start the train
               if (!Directory.Exists(annotationsDir))
                  Directory.CreateDirectory(annotationsDir);
               // Train action
               double? minTotalLoss = null;
               var train = new Action<dynamic>(unused_argv =>
               {
                  // Step callback action
                  var stepCallback = new Action<dynamic>(args =>
                  {
                     try {
                        // Read the data
                        TrainStepEventArgs data;
                        using (Py.GIL())
                           data = new TrainStepEventArgs((int)args.global_step, (double)args.per_step_time, (double)args.loss);
                        // Check if loss is decreased enough for generating a new checkpoint
                        if (Opt.CheckpointForceThreashold != null && (minTotalLoss == null || data.TotalLoss < minTotalLoss.Value * Opt.CheckpointForceThreashold.Value)) {
                           if (minTotalLoss != null)
                              data.CreateCheckpoint = true;
                           minTotalLoss = data.TotalLoss;
                        }
                        // Call the event function
                        OnTrainStep(data);
                        if (data.Cancel)
                           cancellation.Cancel();
                        cancellation.Token.ThrowIfCancellationRequested();
                        // Set the response flags
                        using (Py.GIL())
                           args.create_checkpoint = data.CreateCheckpoint;
                     }
                     catch (OperationCanceledException) {
                        using (Py.GIL())
                           args.cancel = true;
                     }
                  });
                  // Checkpoint ready callback function
                  var checkpointCallback = new Action<dynamic>(args =>
                  {
                     try {
                        // Read the data
                        CheckpointEventArgs checkpointEvent;
                        using (Py.GIL())
                           checkpointEvent = new CheckpointEventArgs((string)args.latest_checkpoint, (string[])args.checkpoints, Opt.CheckpointForceThreashold != null);
                        // Call the event function
                        OnCheckpoint(checkpointEvent);
                        if (checkpointEvent.Cancel)
                           cancellation.Cancel();
                        cancellation.Token.ThrowIfCancellationRequested();
                        // Create the continuous evaluation task
                        if (evalTask == null || evalTask.IsCompleted)
                           evalTask = Task.Run(() => EvaluateContinuously(checkPointReady, evaluationComplete, cancellation.Token));
                        else
                           checkPointReady.Set();
                        // Wait evaluation
                        WaitHandle.WaitAny(new[] { evaluationComplete, cancellation.Token.WaitHandle });
                        evaluationComplete.Reset();
                        cancellation.Token.ThrowIfCancellationRequested();
                        // Check if the client required an export of the checkpoint
                        if (checkpointEvent.Export)
                           ExportLatestCheckpoint(cancellation.Token);
                        cancellation.Token.ThrowIfCancellationRequested();
                     }
                     catch (OperationCanceledException) {
                        // Set the operation cancel
                        using (Py.GIL())
                           args.cancel = true;
                        if (!evalTask.IsCompleted) {
                           cancellation.Cancel();
                           try { evalTask.Wait(); } catch { }
                        }
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
                     ODModelBuilderTF.TraceOutput("Interrupted by user");
                  }),
                  _ => new Action(() => { throw exc; })
               };
               action();
            }
         }
         catch (Exception exc) {
            ODModelBuilderTF.TraceError(exc.ToString().Replace("\\n", Environment.NewLine));
            throw;
         }
         finally {
            // Delete the annotations directory
            try {
               if (delAnnotationDir && Directory.Exists(annotationsDir))
                  Directory.Delete(annotationsDir, true);
            }
            catch (Exception exc) {
               ODModelBuilderTF.TraceError(exc.ToString());
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
         /// Number of steps between checkpoint generations.
         /// </summary>
         public int? CheckpointEvery { get; set; } = null;
         /// <summary>
         /// Forced checkpoint generation threshold.
         /// An automatic checkpoint it's generated if the current step loss is less than previous * thr
         /// </summary>
         public double? CheckpointForceThreashold { get; set; } = null;
         /// <summary>
         /// Folder containing the images and annotation labels for the evaluation
         /// </summary>
         public string EvalImagesFolder { get; set; } = null;
         /// <summary>
         /// Folder for model export
         /// </summary>
         public string ExportFolder { get; set; } = null;
         /// <summary>
         /// Frozen graph file name in the export folder
         /// </summary>
         public string FrozenGraphFileName { get; set; } = null;
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
         /// Onnx model file name in the export folder
         /// </summary>
         public string OnnxModelFileName { get; set; } = null;
         /// <summary>
         /// Optional path of a pre-trained model directory.
         /// For example the exported model directory or a trained model of the TensorFlow zoo
         /// </summary>
         public string PreTrainedModelDir  { get; set; } = null;
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
