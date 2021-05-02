import  os
from    export_parameters import ExportParameters

def export_frozen_graph(prm: ExportParameters):
    # Tensorflow 2.x
    import tensorflow as tf
    from tensorflow.python.framework.convert_to_constants import convert_variables_to_constants_v2

    #Import the model
    saved_model = tf.saved_model.load(os.path.join(prm.output_directory, 'saved_model'))
    model = saved_model.signatures['serving_default']
    # Convert Keras model to ConcreteFunction
    full_model = tf.function(lambda input_tensor: model(input_tensor))
    full_model = full_model.get_concrete_function(
        tf.TensorSpec(model.inputs[0].shape, model.inputs[0].dtype))
 
    # Get frozen ConcreteFunction
    frozen_func = convert_variables_to_constants_v2(full_model)
    graph_def = frozen_func.graph.as_graph_def()
 
    # Print out model inputs and outputs
    print("Frozen model inputs: ", frozen_func.inputs)
    print("Frozen model outputs: ", frozen_func.outputs)
 
    # Save frozen graph to disk
    tf.io.write_graph(graph_or_graph_def=frozen_func.graph,
                      logdir=prm.output_directory,
                      name=prm.frozen_graph,
                      as_text=False)
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

