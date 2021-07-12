namespace ODModelBuilderTF
{
   /// <summary>
   /// Train step event's arguments
   /// </summary>
   public class TrainStepEventArgs
   {
      #region Properties
      /// <summary>
      /// Stop the train if set by the event receiver
      /// </summary>
      public bool Cancel { get; set; }
      /// <summary>
      /// Flag to force the creation of a checkpoint for the step
      /// </summary>
      public bool CreateCheckpoint { get; set; }
      /// <summary>
      /// Number of the current step
      /// </summary>
      public int StepNumber { get; }
      /// <summary>
      /// Time of the step completion in seconds
      /// </summary>
      public double StepTime { get; }
      /// <summary>
      /// Loss of the step
      /// </summary>
      public double TotalLoss { get; }
      #endregion
      #region Methods
      /// <summary>
      /// Constructor
      /// </summary>
      /// <param name="stepNumber">Number of the current step</param>
      /// <param name="stepTime">Time of the step completion in seconds</param>
      /// <param name="totalLoss">Loss of the step</param>
      public TrainStepEventArgs(int stepNumber, double stepTime, double totalLoss)
      {
         StepNumber = stepNumber;
         StepTime = stepTime;
         TotalLoss = totalLoss;
      }
      #endregion
   }

   /// <summary>
   /// Train step event's handler
   /// </summary>
   /// <param name="sender">Sender of the event</param>
   /// <param name="e">Arguments for the event</param>
   public delegate void TrainStepEventHandler(object sender, TrainStepEventArgs e);
}
