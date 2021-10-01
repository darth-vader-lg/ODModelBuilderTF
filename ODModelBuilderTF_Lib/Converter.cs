using Python.Runtime;
using System;
using System.IO;

namespace ODModelBuilderTF
{
   /// <summary>
   /// Model converter class
   /// </summary>
   public partial class Converter
   {
      #region Methods
      /// <summary>
      /// Convert a model from a format to another
      /// </summary>
      /// <param name="src">Source model file</param>
      /// <param name="dst">Destination model file</param>
      /// <param name="srcFormat">Format of the source model</param>
      /// <param name="dstFormat">Format of the destination model</param>
      public static void Convert(string src, string dst, Formats srcFormat = Formats.Auto, Formats dstFormat = Formats.Auto)
      {
         // Check arguments
         if (string.IsNullOrWhiteSpace(src))
            throw new ArgumentNullException(nameof(src), "Unspecified source model");
         if (string.IsNullOrWhiteSpace(dst))
            throw new ArgumentNullException(nameof(dst), "Unspecified destination model");
         if (!File.Exists(src))
            throw new FileNotFoundException("Source model not found", src);
         try {
            // Initialize system
            ODModelBuilderTF.Init(true, true);
            // Acquire the GIL
            using var gil = Py.GIL();
            // Create a new scope
            var py = ODModelBuilderTF.MainScope.NewScope();
            dynamic converter_main = py.Import("converter_main").main;
            converter_main(src, dst, srcFormat.ToString(), dstFormat.ToString());
         }
         catch (Exception exc) {
            ODModelBuilderTF.TraceError(exc.ToString().Replace("\\n", Environment.NewLine));
            throw;
         }
      }
      #endregion
   }

   /// <summary>
   /// Model formats
   /// </summary>
   partial class Converter // Formats
   {
      public enum Formats
      {
         #region Values
         /// <summary>
         /// Auto (depend from model file extension and characteristics)
         /// </summary>
         Auto = 0,
         /// <summary>
         /// TensorFlow saved model .pb
         /// </summary>
         TensorFlowSavedModel,
         /// <summary>
         /// TensorFlow frozen graph .pb
         /// </summary>
         TensorFlowFrozenGraph,
         /// <summary>
         /// PyTorch
         /// </summary>
         PyTorch,
         /// <summary>
         /// Onnx
         /// </summary>
         Onnx,
         #endregion
      }
   }
   
   /// <summary>
   /// Formats extensions
   /// </summary>
   static class FormatsExtensions
   {
      public static string ToString(this Converter.Formats format)
      {
         return format switch
         {
            Converter.Formats.Auto => "auto",
            Converter.Formats.TensorFlowSavedModel => "tf_saved_model",
            Converter.Formats.TensorFlowFrozenGraph => "tf_frozen_graph",
            Converter.Formats.PyTorch => "pytorch",
            Converter.Formats.Onnx => "onnx",
            _ => throw new ArgumentException($"Invalid format {format}", nameof(format)),
         };
      }
   }
}
