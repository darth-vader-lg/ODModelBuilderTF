namespace ODModelBuilderTF
{
   /// <summary>
   /// Evaluation timeout event's arguments
   /// </summary>
   public class EvaluationTimeoutEventArgs
   {
      #region Properties
      /// <summary>
      /// Stop the evaluation if set by the event receiver
      /// </summary>
      public bool Cancel { get; set; }
      #endregion
   }

   /// <summary>
   /// Evaluation timeout event's handler
   /// </summary>
   /// <param name="sender">Sender of the event</param>
   /// <param name="e">Arguments for the event</param>
   public delegate void EvaluationTimeoutEventHandler(object sender, EvaluationTimeoutEventArgs e);
}
