import  os
from    export_parameters import ExportParameters

def export_frozen_graph(prm: ExportParameters):
    # Imports
    import tensorflow as tf
    from tensorflow.python.framework.convert_to_constants import convert_variables_to_constants_v2_as_graph
    # Load the model
    imported = tf.saved_model.load(os.path.join(prm.output_directory, 'saved_model'))
    # Read the signature
    f = imported.signatures['serving_default']
    frozen_func, graph_def = convert_variables_to_constants_v2_as_graph(f, lower_control_flow=False)
    # Extract the input output tensors
    input_tensors = [tensor for tensor in frozen_func.inputs if tensor.dtype != tf.resource]
    output_tensors = frozen_func.outputs
    input_tensor_names = [tensor.name for tensor in input_tensors]
    output_tensor_names = [tensor.name for tensor in output_tensors]
    print('input_tensor_names:', input_tensor_names)
    print('output_tensor_names:', output_tensor_names)
    # Run optimization for inference
    from tensorflow.lite.python.util import run_graph_optimizations, get_grappler_config
    graph_def = run_graph_optimizations(
       graph_def,
       input_tensors,
       output_tensors,
       config=get_grappler_config(["constfold", "function"]),
       graph=frozen_func.graph)
    # Save frozen graph to disk
    tf.io.write_graph(graph_or_graph_def=graph_def,
                      logdir=prm.output_directory,
                      name=prm.frozen_graph,
                      as_text=False)
    # Return the lists of input outputs
    return frozen_func.inputs, frozen_func.outputs

if __name__ == '__main__':
    prm = ExportParameters()
    #@@@
    #import os
    #prm.model_dir = os.path.join('exported-model-ssd-320x320', 'saved_model')
    #prm.output_directory = 'exported-model-ssd-320x320'
    #from tensorflow.python.tools.import_pb_to_tensorboard import import_to_tensorboard
    #import_to_tensorboard(prm.model_dir, os.path.join(prm.output_directory, 'logs'), 'serve')
    #@@@
    export_frozen_graph(prm)

