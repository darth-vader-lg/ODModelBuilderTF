using Python.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;

namespace ODModelBuilderTF
{
   /// <summary>
   /// Evaluation class
   /// </summary>
   public partial class Evaluator
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
      #endregion
      #region Methods
      /// <summary>
      /// Constructor
      /// </summary>
      public Evaluator() : this(default) { }
      /// <summary>
      /// Constructor
      /// </summary>
      /// <param name="opt">Evaluator options</param>
      public Evaluator(Options opt = default) => Opt = opt ?? new Options();
      /// <summary>
      /// Model evaluation
      /// </summary>
      /// <param name="cancel">Cancellation token</param>
      public void Evaluate(CancellationToken cancel = default)
      {
         // Check arguments
         if (string.IsNullOrWhiteSpace(Opt.TrainFolder))
            throw new ArgumentNullException(nameof(Opt.TrainFolder), "Unspecified train directory");
         if (!Directory.Exists(Opt.TrainFolder))
            throw new ArgumentNullException(nameof(Opt.TrainFolder), "The train directory doesn't exist");
         try {
            // Initialize system
            ODModelBuilderTF.Init(true, true);
            // Acquire the GIL
            using var gil = Py.GIL();
            // Create a new scope
            var pyEval = ODModelBuilderTF.MainScope.NewScope();
            // Prepare the arguments
            dynamic sys = pyEval.Import("sys");
            var argv = new List<PyObject>
            {
               Assembly.GetEntryAssembly().Location.ToPython(),
               "--checkpoint_dir".ToPython(), Opt.TrainFolder.ToPython(),
               "--wait_interval".ToPython(), Opt.WaitInterval.ToString().ToPython(),
               "--eval_timeout".ToPython(), Opt.TimeoutInterval.ToString().ToPython()
            };
            if (Opt.TensorBoardPort != null)
               argv.AddRange(new[] { "--tensorboard_port".ToPython(), Opt.TensorBoardPort.Value.ToString().ToPython() });
            sys.argv = new PyList(argv.ToArray());
            // Import the main of the training
            dynamic eval_main = pyEval.Import("eval_main");
            // Import the module here just for having the flags defined
            eval_main.allow_flags_override();
            pyEval.Import("object_detection.model_main_tf2");
            // Import the TensorFlow
            dynamic tf = pyEval.Import("tensorflow");
            try {
               // Evaluation loop's action
               var eval = new Action<dynamic>(unused_argv =>
               {
                  // Evaluation ready callback action
                  var evalCallback = new Action<dynamic>(args =>
                  {
                     // Read the data
                     var items = new List<KeyValuePair<string, double>>();
                     using (Py.GIL()) {
                        var metrics = new PyDict(args.metrics).Items();
                        var count = metrics.Length();
                        for (var i = 0; i < count; i++)
                           items.Add(new(metrics[i][0].ToString(), (double)metrics[i][1].As<double>()));
                     }
                     // Call the event function
                     var data = new EvaluationEventArgs(items);
                     OnEvaluation(data);
                     // Set the response flags
                     using (Py.GIL())
                        args.cancel = data.Cancel || cancel.IsCancellationRequested;
                  });
                  // Evaluation timeout callback action
                  var evalTimeoutCallback = new Action<dynamic>(args =>
                  {
                     // Call the event function
                     var data = new EvaluationTimeoutEventArgs();
                     OnEvaluationTimeout(data);
                     using (Py.GIL())
                        args.cancel = cancel.IsCancellationRequested;
                  });
                  // Start the evaluation loop
                  using (Py.GIL())
                     eval_main.eval_main(unused_argv, eval_callback: evalCallback, eval_timeout_callback: evalTimeoutCallback);
               });
               tf.compat.v1.app.run(eval);
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
            Trace.WriteLine(exc);
         }
      }
      /// <summary>
      /// Evaluation timeout function
      /// </summary>
      /// <param name="e">Evaluation timeout arguments</param>
      protected virtual void OnEvaluationTimeout(EvaluationTimeoutEventArgs e)
      {
         try {
            EvaluationTimeout?.Invoke(this, e);
         }
         catch (Exception exc) {
            Trace.WriteLine(exc);
         }
      }
      #endregion
   }

   /// <summary>
   /// Evaluator options
   /// </summary>
   public partial class Evaluator
   {
      public class Options
      {
         #region Properties
         /// <summary>
         /// The port for the tensorboard server
         /// </summary>
         public int? TensorBoardPort { get; set; } = null;
         /// <summary>
         /// Timeout in seconds for new checkpoints
         /// </summary>
         public double TimeoutInterval { get; set; } = 3600;
         /// <summary>
         /// Folder containing the train checkpoints
         /// </summary>
         public string TrainFolder { get; set; } = null;
         /// <summary>
         /// Wait delay in seconds for new checkpoint
         /// </summary>
         public double WaitInterval { get; set; } = 300;
         #endregion
      }
   }
}
