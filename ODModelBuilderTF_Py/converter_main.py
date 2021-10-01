from    absl import flags
import  argparse
import  os
from    pathlib import Path

def main(src:str, dst:str, input_type = 'auto', output_type:str='auto'):
    """Converter function.
    Args:
        src:                        The source model
        dst:                        The destination model
        input_type:                 Type of the input model ('auto', 'tf_saved_model', 'tf_frozen_graph', 'pytorch', 'onnx')
        output_type:                Type of the output model ('auto', 'tf_saved_model', 'tf_frozen_graph', 'pytorch', 'onnx')
    """
    # Check parameters
    if (not src):
        raise Exception('Unspecified src model')
    if (not dst):
        raise Exception('Unspecified dst model')
    if (not os.path.exists(src)):
        raise FileNotFoundError(f'{src} model doesn`t exist')
    def get_model_type(path:str, type:str) -> str:
        if (not type or type.casefold() == 'auto'):
            model_path = Path(path)
            type = None
            # TensorFlow protobuf
            if (model_path.suffix.casefold() == '.pb'):
                # Test if it`s saved_model or frozen graph
                if (str(model_path).casefold() == 'saved_model.pb' and model_path.parent.casefold() == 'saved_model'):
                    type = 'tf_saved_model'
                else:
                    type = 'tf_frozen_graph'
            elif (model_path.suffix.casefold() == '.pt'):
                type = 'pytorch'
            elif (model_path.suffix.casefold() == '.onnx'):
                type = 'onnx'
        return type and type.casefold()

    # Ensure the input and output type
    input_type = get_model_type(src, input_type)
    output_type = get_model_type(dst, output_type)
    # Check for type definitions
    if (not input_type):
        raise Exception('Unknown input format type')
    if (not output_type):
        raise Exception('Unknown output format type')
    if (input_type == 'pytorch'):
        if (output_type == 'onnx'):
            # Export onnx
            from yolov5.export import main as export_yolov5
            class Prms:
                def __init__(self, *args, **kwargs):
                    self.weights = src
                    self.simplify = True
                    self.include = ['onnx']
            export_yolov5(Prms())
            # Move the exported model to the specified destination
            src_path = Path(src).with_suffix('.onnx')
            dst_path = Path(dst)
            Path(dst_path.parent).mkdir(parents=True,exist_ok=True)
            src_path.replace(dst_path)
            return
    raise Exception(f'Not supported conversion from {input_type} to {output_type}')

def parse_opt():
    """Parse the command line options.
    Return:
        The parsed options
    """
    parser = argparse.ArgumentParser()
    parser.add_argument('--src', type=str, required=True, help='The path of the source model')
    parser.add_argument('--dst', type=str, required=True, help='The path of the destination model')
    parser.add_argument('--src-type', type=str, default='auto', help="The type of the source model ('auto', 'tf_saved_model', 'tf_frozen_graph', 'pytorch', 'onnx')")
    parser.add_argument('--dst-type', type=str, default='auto', help="The type of the destination model ('auto', 'tf_saved_model', 'tf_frozen_graph', 'pytorch', 'onnx')")
    parser.add_argument('--device', default='cpu', help='cpu or cuda')
    opt = parser.parse_args()
    print_args(FILE.stem, opt)
    return opt

if __name__ == '__main__':
    opt = parse_opt()
    main(**vars(opt))
