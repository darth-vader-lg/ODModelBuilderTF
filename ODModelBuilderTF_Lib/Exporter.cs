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
   /// Exporter class
   /// </summary>
   public partial class Exporter
   {
      #region Properties
      /// <summary>
      /// Train options
      /// </summary>
      public Options Opt { get; }
      #endregion
      #region Events
      /// <summary>
      /// Frozen graph exported event
      /// </summary>
      public event ExportEventHandler FrozenGraphExported;
      /// <summary>
      /// Onnx exported event
      /// </summary>
      public event ExportEventHandler OnnxExported;
      /// <summary>
      /// Saved model exported event
      /// </summary>
      public event ExportEventHandler SavedModelExported;
      #endregion
      #region Methods
      /// <summary>
      /// Constructor
      /// </summary>
      public Exporter() : this(default) { }
      /// <summary>
      /// Constructor
      /// </summary>
      /// <param name="opt">Evaluator options</param>
      public Exporter(Options opt = default) => Opt = opt ?? new Options();
      /// <summary>
      /// Model evaluation
      /// </summary>
      /// <param name="cancel">Cancellation token</param>
      public void Export(CancellationToken cancel = default)
      {
         // Check arguments
         if (string.IsNullOrWhiteSpace(Opt.TrainFolder))
            throw new ArgumentNullException(nameof(Opt.TrainFolder), "Unspecified train directory");
         if (!Directory.Exists(Opt.TrainFolder))
            throw new ArgumentNullException(nameof(Opt.TrainFolder), "The train directory doesn't exist");
         if (string.IsNullOrWhiteSpace(Opt.OutputFolder))
            throw new ArgumentNullException(nameof(Opt.OutputFolder), "Unspecified output directory");
         try {
            // Acquire the GIL
            using var gil = Py.GIL();
            // Create a new scope
            var pyEval = ODModelBuilderTF.MainScope.NewScope();
            // Prepare the arguments
            dynamic sys = pyEval.Import("sys");
            var argv = new List<PyObject>
            {
               Assembly.GetEntryAssembly().Location.ToPython(),
               "--trained_checkpoint_dir".ToPython(), Opt.TrainFolder.ToPython(),
               "--output_directory".ToPython(), Opt.OutputFolder.ToPython()
            };
            if (!string.IsNullOrWhiteSpace(Opt.FrozenGraphFileName))
               argv.AddRange(new[] { "--frozen_graph".ToPython(), Opt.FrozenGraphFileName.ToString().ToPython() });
            if (!string.IsNullOrWhiteSpace(Opt.OnnxModelFileName))
               argv.AddRange(new[] { "--onnx".ToPython(), Opt.OnnxModelFileName.ToString().ToPython() });
            sys.argv = new PyList(argv.ToArray());
            // Import the main of the training
            dynamic export_main = pyEval.Import("export_main");
            // Import the module here just for having the flags defined
            export_main.allow_flags_override();
            pyEval.Import("object_detection.exporter_main_v2");
            // Import the TensorFlow
            dynamic tf = pyEval.Import("tensorflow");
            try {
               // Export action
               var export = new Action<dynamic>(unused_argv =>
               {
                  // Exported saved_model callback action
                  var savedModelCallback = new Action<dynamic>(args =>
                  {
                     // Read the data
                     string path;
                     using (Py.GIL())
                        path = (string)args.path;
                     // Call the event function
                     var data = new ExportEventArgs(path);
                     OnExportedSavedModel(data);
                     // Set the response flags
                     using (Py.GIL())
                        args.cancel = data.Cancel || cancel.IsCancellationRequested;
                  });
                  // Exported frozen graph callback action
                  var frozenGraphCallback = new Action<dynamic>(args =>
                  {
                     // Read the data
                     string path;
                     using (Py.GIL())
                        path = (string)args.path;
                     // Call the event function
                     var data = new ExportEventArgs(path);
                     OnExportedFrozenGraph(data);
                     // Set the response flags
                     using (Py.GIL())
                        args.cancel = data.Cancel || cancel.IsCancellationRequested;
                  });
                  // Exported saved_model callback action
                  var onnxCallback = new Action<dynamic>(args =>
                  {
                     // Read the data
                     string path;
                     using (Py.GIL())
                        path = (string)args.path;
                     // Call the event function
                     var data = new ExportEventArgs(path);
                     OnExportedOnnx(data);
                     // Set the response flags
                     using (Py.GIL())
                        args.cancel = data.Cancel || cancel.IsCancellationRequested;
                  });
                  // Start the evaluation loop
                  using (Py.GIL()) {
                     export_main.export_main(
                        unused_argv,
                        saved_model_callback: savedModelCallback,
                        frozen_graph_callback: frozenGraphCallback,
                        onnx_callback: onnxCallback);
                  }
               });
               tf.compat.v1.app.run(export);
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
      /// Frozen graph exported function
      /// </summary>
      /// <param name="e">Export arguments</param>
      protected virtual void OnExportedFrozenGraph(ExportEventArgs data)
      {
         try {
            FrozenGraphExported?.Invoke(this, data);
         }
         catch (Exception exc) {
            Trace.WriteLine(exc);
         }
      }
      /// <summary>
      /// Onnx exported function
      /// </summary>
      /// <param name="e">Export arguments</param>
      protected virtual void OnExportedOnnx(ExportEventArgs data)
      {
         try {
            OnnxExported?.Invoke(this, data);
         }
         catch (Exception exc) {
            Trace.WriteLine(exc);
         }
      }
      /// <summary>
      /// SavedModel exported function
      /// </summary>
      /// <param name="e">Export arguments</param>
      protected virtual void OnExportedSavedModel(ExportEventArgs data)
      {
         try {
            SavedModelExported?.Invoke(this, data);
         }
         catch (Exception exc) {
            Trace.WriteLine(exc);
         }
      }
      #endregion
   }

   /// <summary>
   /// Exporter options
   /// </summary>
   public partial class Exporter
   {
      public class Options
      {
         #region Properties
         /// <summary>
         /// The name of the optional frozen graph file
         /// </summary>
         public string FrozenGraphFileName { get; set; } = null;
         /// <summary>
         /// The name of the optional onnx file
         /// </summary>
         public string OnnxModelFileName { get; set; } = null;
         /// <summary>
         /// Output folder
         /// </summary>
         public string OutputFolder { get; set; } = null;
         /// <summary>
         /// Folder containing the train checkpoints
         /// </summary>
         public string TrainFolder { get; set; } = null;
         #endregion
      }
   }
}
