from    export_parameters import ExportParameters

def get_tf_tensor_info(tensor):
    """Dizionario del tensore

    Restituisce un dizionario del tensore serializzabile in json

    Args:
        key: Chiave del tensore
        rensor: tensore.

    Returns:
        Un dizionario corrispondente al sensore.
    """
    shape = None
    if not tensor.tensor_shape.unknown_rank:
        shape = [dim.size for dim in tensor.tensor_shape.dim]
    result = {
        'name': tensor.name,
        'type': tensor.dtype,
        'shape': shape,
        }
    return result

def get_onnx_tensor_info(tensor):
    """Dizionario del tensore

    Restituisce un dizionario del tensore serializzabile in json

    Args:
        rensor: tensore.

    Returns:
        Un dizionario corrispondente al sensore.
    """
    shape = None
    tensor_type = tensor.type.tensor_type
    # check if it has a shape:
    if (tensor_type.HasField("shape")):
        shape = []
        # iterate through dimensions of the shape:
        for d in tensor_type.shape.dim:
            # the dimension may have a definite (integer) value or a symbolic identifier or neither:
            if (d.HasField("dim_value")):
                shape.append(d.dim_value)  # known dimension
            else:
                shape.append(-1)  # unknown dimension with no name
    result = {
        'name': tensor.name,
        'type': tensor_type.elem_type,
        'shape': shape,
        }
    return result

def get_signature(path_to_pb, tag_set='serve', signature_def='serving_default'):
    """Gets MetaGraphDef from SavedModel.
  
    Returns the MetaGraphDef for the given tag-set and SavedModel directory.
  
    Args:
        saved_model: Il saved model.
        tag_set: Group of tag(s) of the MetaGraphDef to load, in string format,
            separated by ','. The empty string tag is ignored so that passing ''
            means the empty tag set. For tag-set contains multiple tags, all tags
            must be passed in.
        
    Ra  ises:
        RuntimeError: An error when the given tag-set does not exist in the
            SavedModel.
  
    Returns:
        A MetaGraphDef corresponding to the tag-set.
    """
    # Note: Discard empty tags so that "" can mean the empty tag set.
    # Read the saved model
    from    tensorflow.core.protobuf import saved_model_pb2
    from    tensorflow.python.lib.io import file_io
    saved_model = saved_model_pb2.SavedModel()
    if (not file_io.file_exists(path_to_pb)):
        raise IOError(f"The file {path_to_pb} doesn't exist")
    try:
        file_content = file_io.FileIO(path_to_pb, "rb").read()
        saved_model.ParseFromString(file_content)
    except message.DecodeError as e:
        raise IOError("Cannot parse file %s: %s." % (path_to_pb, str(e)))
    set_of_tags = set([tag for tag in tag_set.split(",") if tag])
    for meta_graph_def in saved_model.meta_graphs:
        if set(meta_graph_def.meta_info_def.tags) == set_of_tags:
            signature = meta_graph_def.signature_def[signature_def]
            return signature
    raise RuntimeError("MetaGraphDef associated with tag-set %r could not be"
                       " found in SavedModel" % tag_set)

def export_model_config(prm: ExportParameters, frozen_inputs=None, frozen_outputs=None):
    """Export model configuration

    Export the model configuration in json format

    Args:
        prm: Export parameters
    """
    import  os
    import  sys
    import  json
    import  tensorflow as tf
    from    tensorflow.python.saved_model import constants
    from    tensorflow.python.util import compat

    # Configuration
    cfg = {}
    output_directory = prm.output_directory or ''
    # Labels
    if (output_directory):
        labels_file = os.path.join(output_directory, 'label_map.pbtxt')
        labels = []
        if os.path.isfile(labels_file):
            from google.protobuf import text_format
            from object_detection.protos.string_int_label_map_pb2 import StringIntLabelMap, StringIntLabelMapItem
            msg = StringIntLabelMap()
            with open(labels_file, 'r') as f:
                text_format.Merge(f.read(), msg)
            for item in msg.item:
                label = {}
                keys = dir(item)
                if ('name' in keys):
                    label['name'] = item.name
                if ('display_name' in keys):
                    label['display_name'] = item.display_name
                if ('id' in keys):
                    label['id'] = item.id
                labels.append(label)
    # Model info
    model = None
    # File info
    cfg['format_type'] = ''
    cfg['graph_type'] = ''
    if (output_directory):
        pipeline_config_file = os.path.join(output_directory, 'pipeline.config')
        if os.path.isfile(pipeline_config_file):
            from google.protobuf import text_format
            from object_detection.protos.pipeline_pb2 import TrainEvalPipelineConfig
            msg = TrainEvalPipelineConfig()
            with open(pipeline_config_file, 'r') as f:
                text_format.Merge(f.read(), msg)
            cfg['model_type'] = msg.model.WhichOneof('model')
            model_data = getattr(msg.model, cfg['model_type'])
            image_resizer_type = model_data.image_resizer.WhichOneof('image_resizer_oneof')
            image_resizer = getattr(model_data.image_resizer, image_resizer_type)
            keys = dir(image_resizer)
            if ('width' in keys):
                cfg['image_width'] = image_resizer.width
                cfg['image_height'] = image_resizer.height
            elif ('max_dimension' in keys):
                cfg['image_width'] = image_resizer.max_dimension
                cfg['image_height'] = image_resizer.max_dimension
            import google.protobuf.json_format as json_format
            model = json_format.MessageToDict(model_data)
    signature = None
    # Saved_model
    if (output_directory):
        saved_model_dir = os.path.join(output_directory, 'saved_model')
        path_to_pb = os.path.join(saved_model_dir, constants.SAVED_MODEL_FILENAME_PB)
        path_to_config = path_to_pb + '.config'
        if (os.path.isfile(path_to_pb) and (not os.path.isfile(path_to_config) or os.path.getmtime(path_to_pb) > os.path.getmtime(path_to_config))):
            # File info
            cfg['format_type'] = 'TensorFlow-' + tf.version.VERSION
            cfg['graph_type'] = 'saved_model'
            # Obtain the default signature
            if (not signature):
                signature = get_signature(path_to_pb)
            cfg['inputs'] = {}
            # Add inputs information to the json output
            inputs = dict(sorted(signature.inputs.items(), key=lambda item: item[1].name))
            for key in inputs:
                cfg['inputs'][key] = get_tf_tensor_info(inputs[key])
            # Add outputs information to the json output
            cfg['outputs'] = {}
            outputs = dict(sorted(signature.outputs.items(), key=lambda item: item[1].name))
            for key in outputs:
                cfg['outputs'][key] = get_tf_tensor_info(outputs[key])
            # Add labels
            if (len(labels) > 0):
                cfg['labels'] = labels
            # Add model info
            if (model):
                cfg['model'] = model
            # Save information to the json output
            with open(path_to_config, 'w') as outfile:
                json.dump(cfg, outfile, indent=2)
    # Frozen graph
    if (prm.frozen_graph):
        path_to_model = os.path.join(output_directory, prm.frozen_graph)
        path_to_config = path_to_model + '.config'
        if (os.path.isfile(path_to_model) and (not os.path.isfile(path_to_config) or os.path.getmtime(path_to_model) > os.path.getmtime(path_to_config))):
            # File info
            cfg['format_type'] = 'TensorFlow-' + tf.version.VERSION
            cfg['graph_type'] = 'frozen_graph'
            # Obtain the default signature
            if (not signature):
                signature = get_signature(path_to_pb)
            # Add inputs information to the json output
            cfg['inputs'] = {}
            ix = 0
            inputs = dict(sorted(signature.inputs.items(), key=lambda item: item[1].name))
            import tensorflow._api.v2.dtypes
            for key in inputs:
                cfg['inputs'][key] = get_tf_tensor_info(inputs[key])
                if (frozen_inputs):
                    cfg['inputs'][key]['name'] = frozen_inputs[ix].name
                else:
                    cfg['inputs'][key]['name'] = 'input_tensor' + ('' if ix == 0 else ('_' + str(ix)))
                ix = ix + 1
            # Add outputs information to the json output
            cfg['outputs'] = {}
            ix = 0
            outputs = dict(sorted(signature.outputs.items(), key=lambda item: item[1].name))
            for key in outputs:
                cfg['outputs'][key] = get_tf_tensor_info(outputs[key])
                if (frozen_outputs):
                    cfg['outputs'][key]['name'] = frozen_outputs[ix].name
                else:
                    cfg['outputs'][key]['name'] = 'Identity' + ('' if ix == 0 else ('_' + str(ix)))
                ix = ix + 1
            # Add labels
            if (len(labels) > 0):
                cfg['labels'] = labels
            # Add model info
            if (model):
                cfg['model'] = model
            # Save information to the json output
            with open(path_to_config, 'w') as outfile:
                json.dump(cfg, outfile, indent=2)
    # ONNX
    if (prm.onnx):
        import onnx
        path_to_model = os.path.join(prm.output_directory, prm.onnx)
        path_to_config = path_to_model + '.config'
        if (os.path.isfile(path_to_model) and (not os.path.isfile(path_to_config) or os.path.getmtime(path_to_model) > os.path.getmtime(path_to_config))):
            cfg['format_type'] = 'ONNX-' + onnx.version.version
            cfg['graph_type'] = 'frozen_graph'
            # Load the onnx model
            onnx_model = onnx.load(path_to_model)
            cfg['inputs'] = {}
            # Add inputs information to the json output
            for input in onnx_model.graph.input:
                cfg['inputs'][input.name] = get_onnx_tensor_info(input)
            # Add outputs information to the json output
            cfg['outputs'] = {}
            for output in onnx_model.graph.output:
                cfg['outputs'][output.name] = get_onnx_tensor_info(output)
            # Add labels
            if (len(labels) > 0):
                cfg['labels'] = labels
            # Add model info
            if (model):
                cfg['model'] = model
            # Save information to the json output
            with open(path_to_config, 'w') as outfile:
                json.dump(cfg, outfile, indent=2)

if __name__ == '__main__':
    prm = ExportParameters()
    export_model_config(prm)

