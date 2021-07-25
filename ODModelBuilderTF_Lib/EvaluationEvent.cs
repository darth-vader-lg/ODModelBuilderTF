using System.Collections.Generic;

namespace ODModelBuilderTF
{
   /// <summary>
   /// Evaluation event's arguments
   /// </summary>
   public class EvaluationEventArgs
   {
      #region Properties
      /// <summary>
      /// Stop the evaluation if set by the event receiver
      /// </summary>
      public bool Cancel { get; set; }
      /// <summary>
      /// Metrics of the evaluation
      /// </summary>
      public IReadOnlyDictionary<string, double> Metrics { get; }
      #endregion
      #region Methods
      /// <summary>
      /// Constructor
      /// </summary>
      /// <param name="metrics">Evaluation metrics dictionary</param>
      public EvaluationEventArgs(IEnumerable<KeyValuePair<string, double>> metrics)
      {
         Metrics = new Dictionary<string, double>(metrics);
      }
      #endregion
   }

   /// <summary>
   /// Evaluation event's handler
   /// </summary>
   /// <param name="sender">Sender of the event</param>
   /// <param name="e">Arguments for the event</param>
   public delegate void EvaluationEventHandler(object sender, EvaluationEventArgs e);
}
