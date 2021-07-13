namespace ODModelBuilderTF
{
   /// <summary>
   /// Evaluation event's arguments
   /// </summary>
   public class ExportEventArgs
   {
      #region Properties
      /// <summary>
      /// Stop the export if set by the event receiver
      /// </summary>
      public bool Cancel { get; set; }
      /// <summary>
      /// Path of the exported item
      /// </summary>
      public string Path { get; }
      #endregion
      #region Methods
      /// <summary>
      /// Constructor
      /// </summary>
      /// <param name="path">Path of the exported item</param>
      public ExportEventArgs(string path)
      {
         Path = path;
      }
      #endregion
   }

   /// <summary>
   /// Export event's handler
   /// </summary>
   /// <param name="sender">Sender of the event</param>
   /// <param name="e">Arguments for the event</param>
   public delegate void ExportEventHandler(object sender, ExportEventArgs e);
}
