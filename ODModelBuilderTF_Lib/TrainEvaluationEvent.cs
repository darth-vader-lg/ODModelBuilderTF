using System.Collections.Generic;
using System.Linq;

namespace ODModelBuilderTF
{
   /// <summary>
   /// Train evaluation event's arguments
   /// </summary>
   public class TrainEvaluationEventArgs : EvaluationEventArgs
   {
      #region Properties
      /// <summary>
      /// Export the evaluated checkpoint if set by the event receiver
      /// </summary>
      public bool Export { get; set; }
      /// <summary>
      /// Average precision
      /// </summary>
      public double AP { get; }
      /// <summary>
      /// Total loss of the evaluation
      /// </summary>
      public double TotalLoss { get; }
      #endregion
      #region Methods
      /// <summary>
      /// Constructor
      /// </summary>
      /// <param name="metrics">Evaluation metrics dictionary</param>
      /// <param name="export">Enable export of the evaluated checkpoint</param>
      public TrainEvaluationEventArgs(IEnumerable<KeyValuePair<string, double>> metrics, bool export = false)
         : base(metrics)
      {
         // Extract the total loss from dictionary
         var ap = metrics.Where(m => m.Key.ToLower().EndsWith("ap")).Select(m => new double?(m.Value)).FirstOrDefault();
         var totalLoss = metrics.Where(m => m.Key.ToLower().Contains("total_loss")).Select(m => new double?(m.Value)).FirstOrDefault();
         AP = ap != null ? ap.Value : double.NaN;
         TotalLoss = totalLoss != null ? totalLoss.Value : double.NaN;
         // Export flag
         Export = export;
      }
      #endregion
   }

   /// <summary>
   /// Evaluation event's handler
   /// </summary>
   /// <param name="sender">Sender of the event</param>
   /// <param name="e">Arguments for the event</param>
   public delegate void TrainEvaluationEventHandler(object sender, TrainEvaluationEventArgs e);
}
