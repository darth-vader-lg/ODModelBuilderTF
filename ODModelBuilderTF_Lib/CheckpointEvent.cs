using System.Collections.Generic;
using System.Linq;

namespace ODModelBuilderTF
{
   /// <summary>
   /// Checkpoint event's arguments
   /// </summary>
   public class CheckpointEventArgs
   {
      #region Properties
      /// <summary>
      /// Stop the train if set by the event receiver
      /// </summary>
      public bool Cancel { get; set; }
      /// <summary>
      /// Export the checkpoint if set by the event receiver
      /// </summary>
      public bool Export { get; set; }
      /// <summary>
      /// Path of the latest generated checkpoint
      /// </summary>
      public string LatestCheckpointPath { get; }
      /// <summary>
      /// Paths of the generated checkpoints
      /// </summary>
      public string[] CheckpointPaths { get; }
      #endregion
      #region Methods
      /// <summary>
      /// Constructor
      /// </summary>
      /// <param name="latestCheckpointPath">Path of the latest generated checkpoint</param>
      /// <param name="checkpointPaths">Paths of the generated checkpoints</param>
      /// <param name="export">Enable export of the checkpoint</param>
      public CheckpointEventArgs(string latestCheckpointPath, IEnumerable<string> checkpointPaths, bool export)
      {
         LatestCheckpointPath = latestCheckpointPath;
         CheckpointPaths = checkpointPaths.ToArray();
         Export = export;
      }
      #endregion
   }

   /// <summary>
   /// Checkpoint event's handler
   /// </summary>
   /// <param name="sender">Sender of the event</param>
   /// <param name="e">Arguments for the event</param>
   public delegate void CheckpointEventHandler(object sender, CheckpointEventArgs e);
}
