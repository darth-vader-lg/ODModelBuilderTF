namespace ODModelBuilderTF
{
   /// <summary>
   /// Arguments for the log event
   /// </summary>
   public class LogEventArgs
   {
      #region Properties
      /// <summary>
      /// The log message
      /// </summary>
      public string Message { get; }
      /// <summary>
      /// The log message
      /// </summary>
      public LogMessageTypes Type { get; }
      #endregion
      #region Methods
      /// <summary>
      /// Constructor
      /// </summary>
      /// <param name="message">Log message</param>
      public LogEventArgs(string message, LogMessageTypes type = LogMessageTypes.Output)
      {
         Message = message;
         Type = type;
      }
      #endregion
   }

   /// <summary>
   /// Log event delegate
   /// </summary>
   /// <param name="e">Event arguments</param>
   public delegate void LogEventHandler(LogEventArgs e);

   /// <summary>
   /// Enumeration of log message types
   /// </summary>
   public enum LogMessageTypes
   {
      #region Constants
      /// <summary>
      /// Standard output message
      /// </summary>
      Output = 0,
      /// <summary>
      /// Standard error message
      /// </summary>
      Error = 2
      #endregion
   }
}
