using System.Collections.Generic;

namespace ODModelBuilderTF
{
   /// <summary>
   /// Model types
   /// </summary>
   public enum ModelTypes
   {
      CenterNet_HourGlass104_512x512,
      CenterNet_HourGlass104_1024x1024,
      CenterNet_Resnet50_V1_FPN_512x512,
      CenterNet_Resnet101_V1_FPN_512x512,
      CenterNet_Resnet50_V2_512x512,
      CenterNet_MobileNetV2_FPN_512x512,
      EfficientDet_D0_512x512,
      EfficientDet_D1_640x640,
      EfficientDet_D2_768x768,
      EfficientDet_D3_896x896,
      EfficientDet_D4_1024x1024,
      EfficientDet_D5_1280x1280,
      EfficientDet_D6_1280x1280,
      EfficientDet_D7_1536x1536,
      SSD_MobileNet_V2_320x320,
      SSD_MobileNet_V1_FPN_640x640,
      SSD_MobileNet_V2_FPNLite_320x320,
      SSD_MobileNet_V2_FPNLite_640x640,
      SSD_ResNet50_V1_FPN_640x640,
      SSD_ResNet50_V1_FPN_1024x1024,
      SSD_ResNet101_V1_FPN_640x640,
      SSD_ResNet101_V1_FPN_1024x1024,
      SSD_ResNet152_V1_FPN_640x640,
      SSD_ResNet152_V1_FPN_1024x1024,
      Faster_RCNN_ResNet50_V1_640x640,
      Faster_RCNN_ResNet50_V1_1024x1024,
      Faster_RCNN_ResNet50_V1_800x1333,
      Faster_RCNN_ResNet101_V1_640x640,
      Faster_RCNN_ResNet101_V1_1024x1024,
      Faster_RCNN_ResNet101_V1_800x1333,
      Faster_RCNN_ResNet152_V1_640x640,
      Faster_RCNN_ResNet152_V1_1024x1024,
      Faster_RCNN_ResNet152_V1_800x1333,
      Faster_RCNN_Inception_ResNet_V2_640x640,
      Faster_RCNN_Inception_ResNet_V2_1024x1024,
      Mask_RCNN_Inception_ResNet_V2_1024x1024
   }

   /// <summary>
   /// Extensions for the ModelTypes enum
   /// </summary>
   public static class ModelTypesExtensions
   {
      #region Fields
      /// <summary>
      /// Conversion from type to string
      /// </summary>
      private static Dictionary<ModelTypes, string> typeToString = new()
      {
         { ModelTypes.CenterNet_HourGlass104_512x512, "CenterNet HourGlass104 512x512" },
         { ModelTypes.CenterNet_HourGlass104_1024x1024, "CenterNet HourGlass104 1024x1024" },
         { ModelTypes.CenterNet_Resnet50_V1_FPN_512x512, "CenterNet Resnet50 V1 FPN 512x512" },
         { ModelTypes.CenterNet_Resnet101_V1_FPN_512x512, "CenterNet Resnet101 V1 FPN 512x512" },
         { ModelTypes.CenterNet_Resnet50_V2_512x512, "CenterNet Resnet50 V2 512x512" },
         { ModelTypes.CenterNet_MobileNetV2_FPN_512x512, "CenterNet MobileNetV2 FPN 512x512" },
         { ModelTypes.EfficientDet_D0_512x512, "EfficientDet D0 512x512" },
         { ModelTypes.EfficientDet_D1_640x640, "EfficientDet D1 640x640" },
         { ModelTypes.EfficientDet_D2_768x768, "EfficientDet D2 768x768" },
         { ModelTypes.EfficientDet_D3_896x896, "EfficientDet D3 896x896" },
         { ModelTypes.EfficientDet_D4_1024x1024, "EfficientDet D4 1024x1024" },
         { ModelTypes.EfficientDet_D5_1280x1280, "EfficientDet D5 1280x1280" },
         { ModelTypes.EfficientDet_D6_1280x1280, "EfficientDet D6 1280x1280" },
         { ModelTypes.EfficientDet_D7_1536x1536, "EfficientDet D7 1536x1536" },
         { ModelTypes.SSD_MobileNet_V2_320x320, "SSD MobileNet v2 320x320" },
         { ModelTypes.SSD_MobileNet_V1_FPN_640x640, "SSD MobileNet V1 FPN 640x640" },
         { ModelTypes.SSD_MobileNet_V2_FPNLite_320x320, "SSD MobileNet V2 FPNLite 320x320" },
         { ModelTypes.SSD_MobileNet_V2_FPNLite_640x640, "SSD MobileNet V2 FPNLite 640x640" },
         { ModelTypes.SSD_ResNet50_V1_FPN_640x640, "SSD ResNet50 V1 FPN 640x640 (RetinaNet50)" },
         { ModelTypes.SSD_ResNet50_V1_FPN_1024x1024, "SSD ResNet50 V1 FPN 1024x1024 (RetinaNet50)" },
         { ModelTypes.SSD_ResNet101_V1_FPN_640x640, "SSD ResNet101 V1 FPN 640x640 (RetinaNet101)" },
         { ModelTypes.SSD_ResNet101_V1_FPN_1024x1024, "SSD ResNet101 V1 FPN 1024x1024 (RetinaNet101)" },
         { ModelTypes.SSD_ResNet152_V1_FPN_640x640, "SSD ResNet152 V1 FPN 640x640 (RetinaNet152)" },
         { ModelTypes.SSD_ResNet152_V1_FPN_1024x1024, "SSD ResNet152 V1 FPN 1024x1024 (RetinaNet152)" },
         { ModelTypes.Faster_RCNN_ResNet50_V1_640x640, "Faster R-CNN ResNet50 V1 640x640" },
         { ModelTypes.Faster_RCNN_ResNet50_V1_1024x1024, "Faster R-CNN ResNet50 V1 1024x1024" },
         { ModelTypes.Faster_RCNN_ResNet50_V1_800x1333, "Faster R-CNN ResNet50 V1 800x1333" },
         { ModelTypes.Faster_RCNN_ResNet101_V1_640x640, "Faster R-CNN ResNet101 V1 640x640" },
         { ModelTypes.Faster_RCNN_ResNet101_V1_1024x1024, "Faster R-CNN ResNet101 V1 1024x1024" },
         { ModelTypes.Faster_RCNN_ResNet101_V1_800x1333, "Faster R-CNN ResNet101 V1 800x1333" },
         { ModelTypes.Faster_RCNN_ResNet152_V1_640x640, "Faster R-CNN ResNet152 V1 640x640" },
         { ModelTypes.Faster_RCNN_ResNet152_V1_1024x1024, "Faster R-CNN ResNet152 V1 1024x1024" },
         { ModelTypes.Faster_RCNN_ResNet152_V1_800x1333, "Faster R-CNN ResNet152 V1 800x1333" },
         { ModelTypes.Faster_RCNN_Inception_ResNet_V2_640x640, "Faster R-CNN Inception ResNet V2 640x640" },
         { ModelTypes.Faster_RCNN_Inception_ResNet_V2_1024x1024, "Faster R-CNN Inception ResNet V2 1024x1024" },
         { ModelTypes.Mask_RCNN_Inception_ResNet_V2_1024x1024, "Mask R-CNN Inception ResNet V2 1024x1024" }
      };
      #endregion
      #region Methods
      /// <summary>
      /// Conversion from model type enumeration to text
      /// </summary>
      /// <param name="modelType">Type of the model</param>
      /// <returns>The string representation or null is it doesn't exist</returns>
      public static string ToText(this ModelTypes modelType) => typeToString.TryGetValue(modelType, out var text) ? text : null;
      #endregion
   }
}
