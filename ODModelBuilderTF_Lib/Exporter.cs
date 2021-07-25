using Python.Runtime;
using System;
using System.Collections.Generic;
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
      /// Exported frozen graph event
      /// </summary>
      public event ExportEventHandler ExportedFrozenGraph;
      /// <summary>
      /// Exported frozen graph configuration event
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
                  void OnExported(dynamic args, Action<ExportEventArgs> OnExportFunction)
                  {
                     // Read the data
                     string path;
                     using (Py.GIL())
                        path = (string)args.path;
                     // Call the event function
                     var data = new ExportEventArgs(path);
                     OnExportFunction(data);
                     // Set the response flags
                     using (Py.GIL())
                        args.cancel = data.Cancel || cancel.IsCancellationRequested;
                  }
                  // Start the export
                  using (Py.GIL()) {
                     export_main.export_main(
                        unused_argv,
                        saved_model_callback: new Action<dynamic>(args => OnExported(args, new Action<ExportEventArgs>(e => OnExportedSavedModel(e)))),
                        saved_model_config_callback: new Action<dynamic>(args => OnExported(args, new Action<ExportEventArgs>(e => OnExportedSavedModelConfig(e)))),
                        frozen_graph_callback: new Action<dynamic>(args => OnExported(args, new Action<ExportEventArgs>(e => OnExportedFrozenGraph(e)))),
                        frozen_graph_config_callback: new Action<dynamic>(args => OnExported(args, new Action<ExportEventArgs>(e => OnExportedFrozenGraphConfig(e)))),
                        onnx_callback: new Action<dynamic>(args => OnExported(args, new Action<ExportEventArgs>(e => OnExportedOnnx(e)))),
                        onnx_config_callback: new Action<dynamic>(args => OnExported(args, new Action<ExportEventArgs>(e => OnExportedOnnxConfig(e)))));
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
      }
      /// <summary>
      /// Exported frozen graph function
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
      /// Exported frozen graph configuration function
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
      /// Exported Onnx function
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
      /// Exported Onnx configuration function
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
      /// Exported SavedModel function
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
      /// Exported SavedModel configuration function
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
