import  os
from    export_parameters import ExportParameters

def _construct_concrete_function(func, output_graph_def,
                                 converted_input_indices):
    """Constructs a concrete function from the `output_graph_def`.

    Args:
        func: ConcreteFunction
        output_graph_def: GraphDef proto.
        converted_input_indices: Set of integers of input indices that were
        converted to constants.

    Returns:
        ConcreteFunction.
    """
    from    tensorflow.python.util import lazy_loader, object_identity
    wrap_function = lazy_loader.LazyLoader("wrap_function", globals(), "tensorflow.python.eager.wrap_function")
    # Create a ConcreteFunction from the new GraphDef.
    input_tensors = func.graph.internal_captures
    converted_inputs = object_identity.ObjectIdentitySet([input_tensors[index] for index in converted_input_indices])
    not_converted_inputs = [tensor for tensor in func.inputs if tensor not in converted_inputs]
    not_converted_inputs_map = { tensor.name: tensor for tensor in not_converted_inputs }
    new_input_names = [tensor.name for tensor in not_converted_inputs]
    new_output_names = [name + ':0' for name in sorted(func.output_shapes)]
    new_func = wrap_function.function_from_graph_def(output_graph_def, new_input_names, new_output_names)
    # Manually propagate shape for input tensors where the shape is not correctly
    # propagated. Scalars shapes are lost when wrapping the function.
    for input_tensor in new_func.inputs:
        input_tensor.set_shape(not_converted_inputs_map[input_tensor.name].shape)
    return new_func

def _convert_to_frozen_graph(func, lower_control_flow=True, aggressive_inlining=False):
    """Replaces all the variables in a graph with constants of the same values.

    This function works as same as convert_variables_to_constants_v2, but it
    returns the intermediate `GraphDef` as well. This `GraphDef` contains all the
    debug information after all the transformations in the frozen phase.

    Args:
        func: ConcreteFunction.
        lower_control_flow: Boolean indicating whether or not to lower control flow
            ops such as If and While. (default True)
        aggressive_inlining: Boolean indicating whether or not to to aggressive
            function inlining (might be unsafe if function has stateful ops, not
            properly connected to control outputs).

    Returns:
        ConcreteFunction containing a simplified version of the original, and also
        the intermediate GraphDef containing the node debug information for the
        transformations in the frozen phase.
    """
    from    tensorflow.python.framework.convert_to_constants import _FunctionConverterData, _replace_variables_by_constants
    converter_data = _FunctionConverterData(func=func, lower_control_flow=lower_control_flow, aggressive_inlining=aggressive_inlining)
    output_graph_def, converted_input_indices = _replace_variables_by_constants(converter_data=converter_data)
    import tensorflow as tf
    with (tf.Graph().as_default()) as g:
        tf.graph_util.import_graph_def(output_graph_def, name='')
        graph_outputs = sorted([o.name for o in output_graph_def.node if o.name.startswith('Identity')])
        mnemonic_outputs = sorted(func.output_shapes)
        for graph_name, mnemonic_name in zip(graph_outputs, mnemonic_outputs):
            t = g.get_tensor_by_name(graph_name + ':0')
            tf.identity(t, mnemonic_name)
        output_graph_def = g.as_graph_def() 
    frozen_func = _construct_concrete_function(func, output_graph_def, converted_input_indices)
    return frozen_func, output_graph_def

def export_frozen_graph(prm: ExportParameters):
    """Export a frozen graph

    Args:
        prm: Parameters.
    """
    # Imports
    import tensorflow as tf
    # Load the model
    imported = tf.saved_model.load(os.path.join(prm.output_directory, 'saved_model'))
    # Read the signature
    f = imported.signatures['serving_default']
    frozen_func, graph_def = _convert_to_frozen_graph(f, lower_control_flow=False)
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
    export_frozen_graph(prm)

